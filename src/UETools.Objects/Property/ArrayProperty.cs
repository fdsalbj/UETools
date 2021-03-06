﻿#if NETSTANDARD2_0
using CoreExtensions;
#endif
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UETools.Core;
using UETools.Core.Interfaces;
using UETools.Objects.Interfaces;
using UETools.TypeFactory;

namespace UETools.Objects.Property
{
    internal sealed class ArrayProperty : PropertyCollectionBase<IList>
    {
        public override FArchive Serialize(FArchive reader, PropertyTag tag)
        {
            base.Serialize(reader, tag);
            var info = tag;
            if (tag.InnerTypeEnum == PropertyTag.PropertyType.StructProperty)
                reader.Read(ref info);

            if ((tag.InnerTypeEnum == PropertyTag.PropertyType.ByteProperty && tag.EnumName is null) || tag.InnerTypeEnum == PropertyTag.PropertyType.BoolProperty)
            {
                byte[]? bytes = default;
                reader.Read(ref bytes, Count);
                _value = bytes.ToArray();
            }
            else
            {
                if (tag.InnerTypeEnum.TryGetAttribute(out LinkedTypeAttribute? attrib))
                {
                    var array = new List<IProperty>(Count);
                    var func = PropertyFactory.Get(attrib.LinkedType);
                    for (var i = 0; i < Count; i++)
                    {
                        var prop = func();
                        prop.Serialize(reader, info);
                        array.Add(prop);
                    }
                    _value = array;
                }
            }
            return reader;
        }

        public override void ReadTo(IndentedTextWriter writer)
        {
            if (_value is byte[] bytes)
                writer.WriteLine(new StringBuilder().Append("[ ").AppendJoin(", ", bytes).Append(" ]"));
            else
                base.ReadTo(writer);
        }
    }
}