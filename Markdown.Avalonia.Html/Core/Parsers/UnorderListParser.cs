﻿using HtmlAgilityPack;
using System.Collections.Generic;
using Markdonw.Avalonia.Html.Core.Utils;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ColorTextBlock.Avalonia;

namespace Markdonw.Avalonia.Html.Core.Parsers
{
    public class UnorderListParser : IBlockTagParser
    {
        public IEnumerable<string> SupportTag => new[] { "ul" };

        bool ITagParser.TryReplace(HtmlNode node, ReplaceManager manager, out IEnumerable<StyledElement> generated)
        {
            var rtn = TryReplace(node, manager, out var list);
            generated = list;
            return rtn;
        }

        public bool TryReplace(HtmlNode node, ReplaceManager manager, out IEnumerable<Control> generated)
        {
            var list = new Grid();
            list.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            list.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            int order = 0;

            foreach (var listItemTag in node.ChildNodes.CollectTag("li"))
            {
                var itemContent = manager.ParseChildrenAndGroup(listItemTag);

                var markerTxt = new CTextBlock("・");
                markerTxt.TextAlignment = TextAlignment.Right;
                markerTxt.TextWrapping = TextWrapping.NoWrap;
                markerTxt.Classes.Add(global::Markdown.Avalonia.Markdown.ListMarkerClass);

                var item = CreateItem(itemContent);

                list.RowDefinitions.Add(new RowDefinition());
                list.Children.Add(markerTxt);
                list.Children.Add(item);

                Grid.SetRow(markerTxt, order);
                Grid.SetColumn(markerTxt, 0);

                Grid.SetRow(item, order);
                Grid.SetColumn(item, 1);

                ++order;
            }

            generated = new[] { list };
            return true;
        }

        private StackPanel CreateItem(IEnumerable<Control> children)
        {
            var panel = new StackPanel() { Orientation = Orientation.Vertical };
            foreach (var child in children)
                panel.Children.Add(child);

            return panel;
        }
    }
}
