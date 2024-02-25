﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorDocument.Avalonia.DocumentElements
{
    /// <summary>
    /// The document element for expression of blockquote.
    /// </summary>
    // 引用を表現するためのドキュメント要素
    public class BlockquoteElement : DocumentElement
    {
        private Lazy<Border> _block;
        private EnumerableEx<DocumentElement> _children;
        private List<DocumentElement>? _prevSelection;

        public override Control Control => _block.Value;
        public override IEnumerable<DocumentElement> Children => _children;

        public BlockquoteElement(IEnumerable<DocumentElement> child)
        {
            _block = new Lazy<Border>(Create);
            _children = child.ToEnumerable();
        }

        private Border Create()
        {
            var panel = new StackPanel();
            panel.Orientation = Orientation.Vertical;
            panel.Classes.Add(ClassNames.BlockquoteClass);
            foreach (var child in Children)
                panel.Children.Add(child.Control);

            var border = new Border();
            border.Classes.Add(ClassNames.BlockquoteClass);
            border.Child = panel;

            return border;
        }

        public override void Select(Point from, Point to)
        {
            var selection = SelectionUtil.SelectVertical(Control, _children, from, to);

            if (_prevSelection is not null)
            {
                foreach (var ps in _prevSelection)
                {
                    if (!selection.Any(cs => ReferenceEquals(cs, ps)))
                    {
                        ps.UnSelect();
                    }
                }
            }

            _prevSelection = selection;
        }

        public override void UnSelect()
        {
            foreach (var child in _children)
                child.UnSelect();
        }
    }
}
