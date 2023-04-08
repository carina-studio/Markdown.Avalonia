﻿using Avalonia.Controls;
using Markdown.Avalonia.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Joins;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Markdown.Avalonia.Plugins
{
    public class SetupInfo
    {
        internal const string BuiltinAsmNm = "Markdown.Avalonia.SyntaxHigh";
        internal const string BuiltinTpNm = "Markdown.Avalonia.SyntaxHigh.SyntaxHiglight";

        private bool _builtinCalled = false;

        internal List<IBlockOverride> BlockOverrides { get; } = new List<IBlockOverride>();
        internal List<IStyleEdit> StyleEdits { get; } = new List<IStyleEdit>();

        public void Register(IBlockOverride overrider)
            => BlockOverrides.Add(overrider);

        public void Register(IStyleEdit editor)
            => StyleEdits.Add(editor);

        internal SetupInfo Builtin()
        {
            if (_builtinCalled)
                return this;

            Assembly asm;
            try
            {
                asm = Assembly.Load(BuiltinAsmNm);
            }
            catch
            {
                return this;
            }

            var type = asm.GetType(BuiltinTpNm);
            if (type != null && Activator.CreateInstance(type) is IMdAvPlugin plugin)
            {
                plugin.Setup(this);
            }

            _builtinCalled = true;
            return this;
        }

        internal Parser<Control> Override(Parser<Control> parser)
        {
            var overrider = BlockOverrides.FirstOrDefault(b => b.ParserName == parser.Name);

            if (overrider is null)
                return parser;

            return new BlockParser(parser.Pattern, parser.Name, overrider);
        }





        class BlockParser : Parser<Control>
        {
            private IBlockOverride _overrider;
            public BlockParser(Regex pattern, string name, IBlockOverride overrider) : base(pattern, name)
            {
                _overrider = overrider;
            }

            public override IEnumerable<Control> Convert(
                string text,
                Match firstMatch,
                ParseStatus status,
                IMarkdownEngine engine,
                out int parseTextBegin, out int parseTextEnd)
            => _overrider.Convert(text, firstMatch, status, engine, out parseTextBegin, out parseTextEnd);
        }
    }
}
