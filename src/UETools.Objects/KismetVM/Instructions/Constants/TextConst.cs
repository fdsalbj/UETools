﻿using UETools.Core;
using UETools.Objects.KismetVM.Enums;

namespace UETools.Objects.KismetVM.Instructions
{
    internal sealed partial class TextConst : ConstToken<FText>
    {
        public override EExprToken Expr => EExprToken.EX_TextConst;

        internal EBlueprintTextLiteralType TextLiteralType { get => _textLiteralType; set => _textLiteralType = value; }

        public override FArchive Serialize(FArchive archive)
        {
            base.Serialize(archive)
                .ReadUnsafe(ref _textLiteralType);
            switch (TextLiteralType)
            {
                case EBlueprintTextLiteralType.Empty:
                    _value = FText.GetEmpty();
                    break;
                case EBlueprintTextLiteralType.LocalizedText:
                    break;
                case EBlueprintTextLiteralType.InvariantText:
                    break;
                case EBlueprintTextLiteralType.LiteralString:
                    break;
                case EBlueprintTextLiteralType.StringTableEntry:
                    break;
                default:
                    break;
            }
            // TODO: Implement
            //reader.Read(ref _value);
            return archive;
        }

        private EBlueprintTextLiteralType _textLiteralType;
    }
}
