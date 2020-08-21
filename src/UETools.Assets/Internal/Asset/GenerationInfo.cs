﻿using UETools.Core;
using UETools.Core.Interfaces;

namespace UETools.Assets.Internal.Asset
{
    internal struct GenerationInfo : IUnrealSerializable
    {
        public FArchive Serialize(FArchive reader) 
            => reader.Read(ref _exportCount)
                     .Read(ref _nameCount);

        private int _exportCount;
        private int _nameCount;
    }
}
