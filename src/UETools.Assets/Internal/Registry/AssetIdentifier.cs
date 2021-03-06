﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UETools.Core;
using UETools.Core.Interfaces;

namespace UETools.Assets.Internal.Registry
{
    [DebuggerDisplay("{PackageName.Name.ToString()}")]
    public partial class AssetIdentifier : IUnrealSerializable
    {
        [DisallowNull]
        public FName? PackageName
        {
            get => _packageName;
            set
            {
                _packageName = value;
                _fieldBits |= IdentifierField.PackageName;
            }
        }
        [DisallowNull]
        public FName? PrimaryAssetType
        {
            get => _primaryAssetType;
            set
            {
                _primaryAssetType = value;
                _fieldBits |= IdentifierField.AssetType;
            }
        }
        [DisallowNull]
        public FName? ObjectName
        {
            get => _objectName;
            set
            {
                _objectName = value;
                _fieldBits |= IdentifierField.ObjectName;
            }
        }
        [DisallowNull]
        public FName? ValueName
        {
            get => _valueName;
            set
            {
                _valueName = value;
                _fieldBits |= IdentifierField.ValueName;
            }
        }

        public FArchive Serialize(FArchive archive)
        {
            archive.ReadUnsafe(ref _fieldBits);

            if ((_fieldBits & IdentifierField.PackageName) != 0)
                archive.Read(ref _packageName);
            if ((_fieldBits & IdentifierField.AssetType) != 0)
                archive.Read(ref _primaryAssetType);
            if ((_fieldBits & IdentifierField.ObjectName) != 0)
                archive.Read(ref _objectName);
            if ((_fieldBits & IdentifierField.ValueName) != 0)
                archive.Read(ref _valueName);

            return archive;
        }

        private IdentifierField _fieldBits;
        private FName? _packageName;
        private FName? _primaryAssetType;
        private FName? _objectName;
        private FName? _valueName;
    }
}
