﻿using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using AvaloniaEdit;
using Markdown.Avalonia.Plugins;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using System.Collections.ObjectModel;
using Markdown.Avalonia.SyntaxHigh.Extensions;
using Markdown.Avalonia.Parsers;

namespace Markdown.Avalonia.SyntaxHigh
{
    internal class SyntaxOverride : IBlockOverride
    {
        private SyntaxHighlightProvider _provider;


        public string ParserName => "CodeBlocksWithLangEvaluator";


        public SyntaxOverride(ObservableCollection<Alias> aliases)
        {
            _provider = new SyntaxHighlightProvider(aliases);
        }


        public IEnumerable<Control> Convert(
            string text,
            Match match,
            ParseStatus status,
            IMarkdownEngine engine,
            out int parseTextBegin, out int parseTextEnd)
        {
            parseTextBegin = match.Index;
            parseTextEnd = match.Index + match.Length;

            var lang = match.Groups[2].Value;
            var code = match.Groups[3].Value;

            if (String.IsNullOrEmpty(lang))
            {
                var ctxt = new TextBlock()
                {
                    Text = code,
                    TextWrapping = TextWrapping.NoWrap
                };
                ctxt.Classes.Add(Markdown.CodeBlockClass);

                var scrl = new ScrollViewer();
                scrl.Classes.Add(Markdown.CodeBlockClass);
                scrl.Content = ctxt;
                scrl.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

                var result = new Border();
                result.Classes.Add(Markdown.CodeBlockClass);
                result.Child = scrl;

                return new Control[] { result };
            }
            else
            {
                // check wheither style is set
                if (!ThemeDetector.IsAvalonEditSetup)
                {
                    var aeStyle = new StyleInclude(new Uri("avares://Markdown.Avalonia/"))
                    {
                        Source = new Uri("avares://AvaloniaEdit/AvaloniaEdit.xaml")
                    };
                    Application.Current.Styles.Add(aeStyle);
                }

                var txtEdit = new TextEditor();
                txtEdit.Tag = lang;
                txtEdit.SetValue(SyntaxHighlightWrapperExtension.ProviderProperty, _provider);

                txtEdit.Text = code;
                txtEdit.HorizontalAlignment = HorizontalAlignment.Stretch;
                txtEdit.IsReadOnly = true;

                var result = new Border();
                result.Classes.Add(Markdown.CodeBlockClass);
                result.Child = txtEdit;

                return new Control[] { result };
            }
        }




    }
}
