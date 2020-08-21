﻿using System;
using System.IO;
using UETools.Core;

namespace UETools.Objects.KismetVM.Instructions
{
    internal sealed class BindDelegate : Token
    {
        public override EExprToken Expr => EExprToken.EX_BindDelegate;

        public FName FuncName => _funcName;
        public Token FirstExpr { get; private set; } = null!;
        public Token SecondExpr { get; private set; } = null!;

        public override FArchive Serialize(FArchive reader)
        {
            base.Serialize(reader)
                .Read(ref _funcName);
            FirstExpr = Token.Read(reader);
            SecondExpr = Token.Read(reader);
            return reader;
        }

        public override void ReadTo(TextWriter writer)
        {
            throw new NotImplementedException();
        }

        private FName _funcName = null!;
    }
}
