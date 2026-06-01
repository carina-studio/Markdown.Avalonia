using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorDocument.Avalonia.DocumentElements
{
    public class PlainCodeBlockElement : DocumentElement
    {
        private readonly string _code;
        private readonly Lazy<Border> _border;
        private TextBlock? _textBlock;
        private Canvas? _selectionLayer;
        private int _selStart = -1;
        private int _selEnd = -1;

        public override Control Control => _border.Value;

        public override IEnumerable<DocumentElement> Children => Array.Empty<DocumentElement>();

        /// <summary>
        /// Override the Helper setter to manage subscription to
        /// <see cref="ISelectionRenderHelper.SelectionBrushChanged"/>. We use the
        /// helper's brush to paint our internal selection layer, so we need to
        /// repaint it when the user changes the viewer's selection brush.
        /// </summary>
        public override ISelectionRenderHelper? Helper
        {
            get => base.Helper;
            set
            {
                if (base.Helper is not null)
                    base.Helper.SelectionBrushChanged -= OnSelectionBrushChanged;
                base.Helper = value;
                if (base.Helper is not null)
                    base.Helper.SelectionBrushChanged += OnSelectionBrushChanged;
            }
        }

        public PlainCodeBlockElement(string code)
        {
            _code = code;
            _border = new Lazy<Border>(CreateBlock);
        }

        public override void Select(Point from, Point to)
        {
            // Ensure the visual tree is built so _textBlock / _selectionLayer exist.
            var _ = _border.Value;
            if (_textBlock is null || _selectionLayer is null) return;

            var layout = _textBlock.TextLayout;
            if (layout is null)
            {
                _selStart = _selEnd = -1;
                _selectionLayer.Children.Clear();
                return;
            }

            int fromIdx = PointToIndex(from, layout);
            int toIdx = PointToIndex(to, layout);

            int start = Math.Min(fromIdx, toIdx);
            int end = Math.Max(fromIdx, toIdx);

            _selStart = start;
            _selEnd = end;

            RenderSelectionRectangles(layout);
        }

        public override void UnSelect()
        {
            _selStart = _selEnd = -1;
            _selectionLayer?.Children.Clear();
        }

        public override void ConstructSelectedText(StringBuilder stringBuilder)
        {
            if (_selStart < 0 || _selEnd <= _selStart)
                return;

            int start = Math.Max(0, Math.Min(_selStart, _code.Length));
            int end = Math.Max(start, Math.Min(_selEnd, _code.Length));
            if (end > start)
                stringBuilder.Append(_code, start, end - start);
        }

        public Border CreateBlock()
        {
            _textBlock = new TextBlock
            {
                Text = _code,
                TextWrapping = TextWrapping.NoWrap
            };
            _textBlock.Classes.Add(ClassNames.CodeBlockClass);

            // Selection layer sits behind the text in the same Grid cell.
            // Rectangles inside this Canvas are positioned in TextLayout-local
            // coordinates (which equal TextBlock-local coords when its Padding is 0).
            _selectionLayer = new Canvas
            {
                IsHitTestVisible = false
            };

            var grid = new Grid();
            grid.Children.Add(_selectionLayer); // index 0 → behind
            grid.Children.Add(_textBlock);      // index 1 → in front

            var scrl = new ScrollViewer();
            scrl.Classes.Add(ClassNames.CodeBlockClass);
            scrl.Content = grid;
            scrl.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

            var result = new Border();
            result.Classes.Add(ClassNames.CodeBlockClass);
            result.Child = scrl;

            return result;
        }

        private void RenderSelectionRectangles(TextLayout layout)
        {
            if (_selectionLayer is null) return;
            _selectionLayer.Children.Clear();

            if (_selEnd <= _selStart) return;

            var brush = Helper?.SelectionBrush;
            foreach (var r in layout.HitTestTextRange(_selStart, _selEnd - _selStart))
            {
                var rectangle = new Rectangle
                {
                    Width = r.Width,
                    Height = r.Height,
                    Fill = brush
                };
                Canvas.SetLeft(rectangle, r.X);
                Canvas.SetTop(rectangle, r.Y);
                _selectionLayer.Children.Add(rectangle);
            }
        }

        private void OnSelectionBrushChanged(object? sender, EventArgs e)
        {
            if (_selectionLayer is null) return;
            var brush = Helper?.SelectionBrush;
            foreach (var child in _selectionLayer.Children)
            {
                if (child is Rectangle r)
                    r.Fill = brush;
            }
        }

        /// <summary>
        /// Convert a point expressed in <see cref="Control"/>'s local coordinate space
        /// to a character index inside <see cref="_code"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="SelectionUtil"/> passes "before the element" as a point whose Y is
        /// negative (workF = (0,0) - rect for non-leading elements) and "past the element"
        /// as <see cref="double.PositiveInfinity"/> (workT for non-trailing elements). Both
        /// are interpreted explicitly so we don't depend on <see cref="TextLayout.HitTestPoint"/>'s
        /// behavior outside the laid-out region.
        /// </remarks>
        private int PointToIndex(Point pntInControl, TextLayout layout)
        {
            // "Before the element" markers used by SelectionUtil.Select.
            if (pntInControl.Y < 0) return 0;
            if (pntInControl.X <= 0 && pntInControl.Y <= 0) return 0;

            // "Past the end" marker.
            if (double.IsPositiveInfinity(pntInControl.X) || double.IsPositiveInfinity(pntInControl.Y))
                return _code.Length;

            // Translate to TextBlock-local coords (handles border thickness, scroll offset, etc.).
            var pntInTb = _border.Value.TranslatePoint(pntInControl, _textBlock!);
            if (!pntInTb.HasValue)
                return 0;

            var pt = pntInTb.Value;
            if (pt.Y < 0) return 0;

            var hit = layout.HitTestPoint(pt);
            int pos = hit.TextPosition + (hit.IsTrailing ? 1 : 0);
            if (pos < 0) pos = 0;
            if (pos > _code.Length) pos = _code.Length;
            return pos;
        }
    }
}
