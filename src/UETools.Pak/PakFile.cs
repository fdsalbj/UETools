﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UETools.Core;
using UETools.Core.Interfaces;
using UETools.Pak.Enums;
using UETools.Pak.Interfaces;

namespace UETools.Pak
{
    [DebuggerDisplay("{FileName}, {AbsoluteIndex.Count} files", Name = "PakFile {FileName}")]
    public sealed class PakFile : IDisposable, IAsyncDisposable
    {
        public const uint Magic = 0x5A6F12E1;
        public const int BufferSize = 32 * 1024;
        private readonly IVersionProvider _versionProvider;
        private readonly AesPakCryptoProvider? _aesProvider;

        public Dictionary<string, PakEntry> AbsoluteIndex { get; private set; } = null!;
        public int FileCount => AbsoluteIndex.Count;
        private string MountPoint { get; set; } = null!;
        private string FileName { get; }
        private long Size { get; }
        private Stream SourceStream { get; }

        private PakFile(string fileName, long size, Stream sourceStream, IVersionProvider? versionProvider = null, AesPakCryptoProvider? aesProvider = null)
        {
            FileName = fileName;
            Size = size;
            SourceStream = sourceStream;
            _aesProvider = aesProvider;
            _versionProvider = versionProvider ?? AutomaticVersionProvider.Instance;
        }

        public IMemoryOwner<byte> ReadEntry(PakEntry entry)
        {
            var buf = entry.Read(SourceStream);
            if (entry.IsAnyEncrypted)
            {
                if (_aesProvider is null)
                    throw new PakEncryptedException("Pak file contains encrypted entries. AES encryption key is necessary for reading this asset.");

                _aesProvider.DecryptEntry(buf.Memory, entry);
            }
            if (entry.IsAnyCompressed)
            {
                var decompressed = UnrealCompression.Decompress(buf.Memory, entry);
                buf.Dispose();
                return decompressed;
            }
            else
                return buf;
        }
        public async ValueTask<IMemoryOwner<byte>> ReadEntryAsync(PakEntry entry, CancellationToken cancellationToken = default)
        {
            var buf = await entry.ReadAsync(SourceStream, cancellationToken).ConfigureAwait(false);
            if (entry.IsAnyEncrypted)
            {
                if (_aesProvider is null)
                    throw new PakEncryptedException("Pak file contains encrypted entries. AES encryption key is necessary for reading this asset.");

                _aesProvider.DecryptEntry(buf.Memory, entry);
            }
            if (entry.IsAnyCompressed)
            {
                var result = await UnrealCompression.DecompressAsync(buf.Memory, entry, cancellationToken).ConfigureAwait(false);
                buf.Dispose();
                return result;
            }
            else
                return buf;
        }

        private static bool ReadPakInfo(Stream stream, long size, AesPakCryptoProvider? _aesProvider, [NotNullWhen(true)] out PakInfo? info)
        {
            var values = Enum.GetValues(typeof(PakInfoSize)).Cast<int>().Where(x => size > x);
            // check only versions that can actually be pak files
            if (values.Any())
            {
                var maxSize = values.Max();
                using var data = PakMemoryPool.Shared.Rent(maxSize);
                stream.ReadWholeBuf(-maxSize, SeekOrigin.End, data.Memory.Span);
                foreach (var value in values)
                {
                    if (value > size)
                        continue;

                    var z = data.Memory[^value..];
                    var pak = new PakInfo(z, _aesProvider);
                    if (pak.IsUnrealPak)
                    {
                        info = pak;
                        return true;
                    }
                }
            }
            info = null;
            return false;
        }
        private void Initialize(PakInfo info) => ProcessIndex(info.ReadIndex(SourceStream));
        private async ValueTask InitializeAsync(PakInfo info, CancellationToken cancellationToken = default) => ProcessIndex(await info.ReadIndexAsync(SourceStream, cancellationToken: cancellationToken).ConfigureAwait(false));
        private void ProcessIndex(FArchive reader)
        {
            reader.Read(out FString mountPoint);
            MountPoint = mountPoint.ToString().Replace("../../../", null);
            reader.Read(out int NumEntries);
            AbsoluteIndex = new Dictionary<string, PakEntry>(NumEntries);

            for (var i = 0; i < NumEntries; i++)
            {
                reader.Read(out FString fileName);
                var filePath = Path.Combine(MountPoint, fileName.ToString());
                var entry = new PakEntry(this);
                entry.Deserialize(reader);
                AbsoluteIndex.Add(filePath, entry);
            }

            // TODO: Change this to something working better
            foreach(var kv in AbsoluteIndex)
            {
                var val = kv.Value;
                if (kv.Value.IsCompressed)
                    val.CompressionBlocks.OffsetBy((int)val.EntryHeaderSize);

                if (kv.Key.EndsWith(".uasset"))
                {
                    var expPath = Path.ChangeExtension(kv.Key, ".uexp");
                    if (AbsoluteIndex.TryGetValue(expPath, out var exports))
                    {
                        val.LinkedEntry = exports;
                        exports.CompressionBlocks.OffsetBy(-(int)(val.Size));
                        var bulkPath = Path.ChangeExtension(kv.Key, ".ubulk");
                        if (AbsoluteIndex.TryGetValue(bulkPath, out var bulk))
                        {
                            exports.LinkedEntry = bulk;
                            bulk.CompressionBlocks.OffsetBy(-(int)(exports.Size + val.Size));
                        }
                    }
                }
            }
        }

        public static PakFile? Open(FileInfo fileInfo, IVersionProvider? versionProvider = null, IAesKeyProvider? keyProvider = null)
        {
            if (fileInfo is null) throw new ArgumentNullException(nameof(fileInfo));
            if (!fileInfo.Exists) throw new FileNotFoundException();
            var aesProvider = keyProvider is null ? null : new AesPakCryptoProvider(keyProvider);

            var fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            if (ReadPakInfo(fileStream, fileStream.Length, aesProvider, out var info))
            {
                var file = new PakFile(fileInfo.Name, fileStream.Length, fileStream, versionProvider, aesProvider);
                file.Initialize(info);
                return file;
            }
            else
            {
                fileStream.Dispose();
                throw new NotPakFileException();
            }
        }
        public async static Task<PakFile?> OpenAsync(FileInfo fileInfo, IVersionProvider? versionProvider = null, IAesKeyProvider? keyProvider = null, CancellationToken cancellationToken = default)
        {
            if (fileInfo is null) throw new ArgumentNullException(nameof(fileInfo));
            if (!fileInfo.Exists) throw new FileNotFoundException();
            var aesProvider = keyProvider is null ? null : new AesPakCryptoProvider(keyProvider);

            var fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            if (ReadPakInfo(fileStream, fileStream.Length, aesProvider, out var info))
            {
                var file = new PakFile(fileInfo.Name, fileStream.Length, fileStream, versionProvider, aesProvider);
                await file.InitializeAsync(info, cancellationToken).ConfigureAwait(false);
                return file;
            }

            return null;
        }

        private IMemoryOwner<byte> ReadStream(long offset, long size)
        {
            var data = PakMemoryPool.Shared.Rent((int)size);
            SourceStream.ReadWholeBuf(offset, data.Memory.Span);
            return data;
        }
        private async ValueTask<IMemoryOwner<byte>> ReadStreamAsync(long offset, long size, CancellationToken cancellationToken)
        {
            var data = PakMemoryPool.Shared.Rent((int)size);
            await SourceStream.ReadWholeBufAsync(offset, data.Memory, cancellationToken).ConfigureAwait(false);
            return data;
        }

        public void Dispose() => SourceStream.Dispose();
        public ValueTask DisposeAsync() => SourceStream.DisposeAsync();
    }
}
