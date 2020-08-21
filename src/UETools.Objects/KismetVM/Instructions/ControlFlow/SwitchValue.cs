﻿using System;
using System.IO;
using UETools.Core;

namespace UETools.Objects.KismetVM.Instructions
{
    internal sealed class SwitchValue : Token
    {
        public override EExprToken Expr => EExprToken.EX_SwitchValue;
        public ushort NumCases { get => _numCases; set => _numCases = value; }
        public CodeSkipSize AfterSkip { get => _afterSkip; set => _afterSkip = value; }
        public Token IndexExpression { get; private set; } = null!;

        public override FArchive Serialize(FArchive reader)
        {
            base.Serialize(reader)
                .Read(ref _numCases)
                .Read(ref _afterSkip);
            IndexExpression = Token.Read(reader);
            for (int i = 0; i < NumCases; i++)
            {
                var label = Token.Read(reader);
                CodeSkipSize _nextCaseOffset = default;
                reader.Read(ref _nextCaseOffset);
                var term = Token.Read(reader);
            }
            var defaultCase = Token.Read(reader);
            return reader;
        }

        public override void ReadTo(TextWriter writer)
        {
            throw new NotImplementedException();
        }

        private ushort _numCases;
        private CodeSkipSize _afterSkip;
    }
}
