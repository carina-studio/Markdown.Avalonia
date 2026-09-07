
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Rendering.Composition;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia.Geometries;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorTextBlock.Avalonia
{
    /// <summary>
    /// TextBlock to enables character-by-character decoration.
    /// </summary>
    // 文字ごとの装飾を可能とするTextBlock
    public class CTextBlock : Control, ITextPointerHandleable
    {
        /// <summary>
        /// Use for adjusting vertical position between CTextBlocks. e.g. between a list marker and a list item.
        /// </summary>
        // リストマーカーと項目の縦位置の調整といった、CTextBlock間で文字の位置調整に使用します。
        private static readonly StyledProperty<double> BaseHeightProperty =
            AvaloniaProperty.Register<CTextBlock, double>("BaseHeight");

        /// <summary>
        /// Use to indicate the height of each lines. If this value is NaN, the height is calculated by content.
        /// </summary>
        /// <seealso cref="LineHeight"/>
        // 一行の高さ指定の為に使用します。指定がない(NaN)の場合、コンテンツによって行の高さが決まります。
        public static readonly StyledProperty<double> LineHeightProperty =
            AvaloniaProperty.Register<CTextBlock, double>(nameof(LineHeight), defaultValue: Double.NaN);

        /// <summary>
        /// Line to line spacing.
        /// </summary>
        /// <seealso cref="LineSpacing"/>
        // 行間の幅
        public static readonly StyledProperty<double> LineSpacingProperty =
            AvaloniaProperty.Register<CTextBlock, double>(nameof(LineSpacing), defaultValue: 0);

        /// <summary>
        /// The brush of background.
        /// </summary>
        /// <seealso cref="Background"/>
        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            Border.BackgroundProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// The brush of characters.
        /// </summary>
        /// <seealso cref="Foreground"/>
        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextBlock.ForegroundProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// The font family of characters
        /// </summary>
        /// <seealso cref="FontFamily"/>
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// The font weight of characters
        /// </summary>
        /// <seealso cref="FontWeight"/>
        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            TextBlock.FontWeightProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// The font size of characters
        /// </summary>
        /// <seealso cref="FontSize"/>
        public static readonly StyledProperty<double> FontSizeProperty =
            TextBlock.FontSizeProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// The font style of characters
        /// </summary>
        /// <seealso cref="FontStyle"/>
        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            TextBlock.FontStyleProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// Use to indicate the vertical position of text within line.
        /// For example, it is used to align text to the top or to the bottom.
        /// </summary>
        /// <seealso cref="TextVerticalAlignment"/>
        // テキストを上揃えで描画するか下揃えで描画するか指定します。
        public static readonly StyledProperty<TextVerticalAlignment> TextVerticalAlignmentProperty =
            AvaloniaProperty.Register<CTextBlock, TextVerticalAlignment>(
                nameof(TextVerticalAlignment),
                defaultValue: TextVerticalAlignment.Base,
                inherits: true);

        /// <summary>
        /// Use to indicate the mode of text wrapping.
        /// </summary>
        /// <seealso cref="TextWrapping"/>
        public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
            AvaloniaProperty.Register<CTextBlock, TextWrapping>(nameof(TextWrapping), defaultValue: TextWrapping.Wrap);

        /// <summary>
        /// Contents to be displayed.
        /// </summary>
        /// <seealso cref="Content"/>
        public static readonly DirectProperty<CTextBlock, AvaloniaList<CInline>> ContentProperty =
            AvaloniaProperty.RegisterDirect<CTextBlock, AvaloniaList<CInline>>(
                nameof(Content),
                    o => o.Content,
                    (o, v) => o.Content = v);

        public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
            SelectableTextBlock.SelectionBrushProperty.AddOwner<CTextBlock>();

        /// <summary>
        /// Horizontal text alignment.
        /// </summary>
        /// <seealso cref="TextAlignment"/>
        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
            AvaloniaProperty.Register<CTextBlock, TextAlignment>(
                nameof(TextAlignment), defaultValue: TextAlignment.Left);

        static CTextBlock()
        {
            ClipToBoundsProperty.OverrideDefaultValue<CTextBlock>(true);

            AffectsRender<CTextBlock>(
                BackgroundProperty,
                TextBlock.ForegroundProperty,
                TextBlock.FontWeightProperty,
                TextBlock.FontSizeProperty,
                TextBlock.FontStyleProperty);
        }

        private double _computedBaseHeight;
        private AvaloniaList<CInline> _content;
        private Size _constraint;
        private Size _measured;
        private readonly List<CGeometry> _metries;
        private readonly List<LineInfo> _lines;
        private readonly List<CInlineUIContainer> _containers;
        private CGeometry? _entered;
        private CGeometry? _pressed;
        private string? _text;
        private bool _measureRequested;

        private TextPointer? _beginSelect;
        private List<CGeometry> _intermediates = new();
        private TextPointer? _endSelect;


        public Selection? Selection =>
            _beginSelect is not null && _endSelect is not null ?
                new Selection(_beginSelect.Index, _endSelect.Index) :
                null;

        /// <summary>
        /// The brush of background.
        /// </summary>
        public IBrush? Background
        {
            get { return GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        /// <summary>
        /// The brush of characters.
        /// </summary>
        public IBrush? Foreground
        {
            get { return GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); }
        }

        /// <summary>
        /// The font family of characters
        /// </summary>
        public FontFamily FontFamily
        {
            get { return GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        /// <summary>
        /// The font size of characters
        /// </summary>
        public double FontSize
        {
            get { return GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        /// <summary>
        /// The font style of characters
        /// </summary>
        public FontStyle FontStyle
        {
            get { return GetValue(FontStyleProperty); }
            set { SetValue(FontStyleProperty, value); }
        }

        /// <summary>
        /// The font weight of characters
        /// </summary>
        public FontWeight FontWeight
        {
            get { return GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }

        /// <summary>
        /// Use to indicate the mode of text wrapping.
        /// </summary>
        public TextWrapping TextWrapping
        {
            get { return GetValue(TextWrappingProperty); }
            set { SetValue(TextWrappingProperty, value); }
        }

        /// <summary>
        /// Horizontal text alignment.
        /// </summary>
        public TextAlignment TextAlignment
        {
            get { return GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        /// <summary>
        /// Use to indicate the vertical position of text within line.
        /// For example, it is used to align text to the top or to the bottom.
        /// </summary>
        public TextVerticalAlignment TextVerticalAlignment
        {
            get { return GetValue(TextVerticalAlignmentProperty); }
            set { SetValue(TextVerticalAlignmentProperty, value); }
        }

        /// <summary>
        /// Use to indicate the height of each lines. If this value is NaN, the height is calculated by content.
        /// </summary>
        public double LineHeight
        {
            get { return GetValue(LineHeightProperty); }
            set { SetValue(LineHeightProperty, value); }
        }

        /// <summary>
        /// Line to line spacing.
        /// </summary>
        public double LineSpacing
        {
            get { return GetValue(LineSpacingProperty); }
            set { SetValue(LineSpacingProperty, value); }
        }

        /// <summary>
        /// Contents to be displayed.
        /// </summary>
        [Content]
        public AvaloniaList<CInline> Content
        {

            get => _content;
            set
            {
                var olds = _content;

                if (SetAndRaise(ContentProperty, ref _content, value))
                {
                    olds.CollectionChanged -= ContentCollectionChangedd;

                    DetachChildren(olds);
                    AttachChildren(_content);

                    _content.CollectionChanged += ContentCollectionChangedd;
                }
            }
        }

        /// <summary>
        /// Textual presentation of content.
        /// </summary>
        public string Text
        {
            get => _text ??= String.Join("", Content.Select(c => c.AsString()));
        }

        public IBrush? SelectionBrush
        {
            get => GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }


        public CTextBlock()
        {
            _content = new AvaloniaList<CInline>();
            _content.CollectionChanged += ContentCollectionChangedd;

            _metries = new List<CGeometry>();
            _lines = new List<LineInfo>();
            _containers = new List<CInlineUIContainer>();

            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        }

        public CTextBlock(string text) : this()
        {
            _content.Add(new CRun() { Text = text });
        }

        public CTextBlock(params CInline[] inlines) : this((IEnumerable<CInline>)inlines)
        {
        }

        public CTextBlock(IEnumerable<CInline> inlines) : this()
        {
            _content.AddRange(inlines);
        }

        #region pointer event

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (_entered is not null)
            {
                _entered.OnMouseLeave?.Invoke(this);
                _entered = null;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            Point point = e.GetPosition(this);

            if (_entered is not null)
            {
                var relX = point.X - _entered.Left;
                var relY = point.Y - _entered.Top;

                if (!isEntered(_entered))
                {
                    _entered.OnMouseLeave?.Invoke(this);
                    _entered = null;
                }
                else return;
            }

            foreach (CGeometry metry in _metries)
            {
                if (isEntered(metry))
                {
                    metry.OnMouseEnter?.Invoke(this);
                    _entered = metry;
                    break;
                }
            }

            bool isEntered(CGeometry metry)
            {
                var relX = point.X - metry.Left;
                var relY = point.Y - metry.Top;

                return 0 <= relX && relX <= metry.Width
                    && 0 <= relY && relY <= metry.Height;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Point point = e.GetPosition(this);

                foreach (CGeometry metry in _metries)
                {
                    if ((metry.OnMousePressed is not null || metry.OnMouseReleased is not null) && isEntered(metry))
                    {
                        metry.OnMousePressed?.Invoke(this);
                        _pressed = metry;
                        e.Handled = true;
                        return;
                    }
                }

                bool isEntered(CGeometry metry)
                {
                    var relX = point.X - metry.Left;
                    var relY = point.Y - metry.Top;

                    return 0 <= relX && relX <= metry.Width
                        && 0 <= relY && relY <= metry.Height;
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_pressed is not null && e.InitialPressMouseButton == MouseButton.Left)
            {
                e.Handled = true;
                _pressed.OnMouseReleased?.Invoke(this);

                Point point = e.GetPosition(this);
                var relX = point.X - _pressed.Left;
                var relY = point.Y - _pressed.Top;

                if (0 <= relX && relX <= _pressed.Width
                    && 0 <= relY && relY <= _pressed.Height)
                {
                    _pressed.OnClick?.Invoke(this);
                }

                _pressed = null;
            }
        }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            switch (change.Property.Name)
            {
                case nameof(Content):
                case nameof(TextBlock.FontSize):
                case nameof(TextBlock.FontStyle):
                case nameof(TextBlock.FontWeight):
                case nameof(TextWrapping):
                case nameof(Bounds):
                case nameof(TextVerticalAlignment):
                case nameof(LineHeight):
                case nameof(LineSpacing):
                    OnMeasureSourceChanged();
                    break;

                case nameof(BaseHeightProperty):
                    if (_computedBaseHeight != GetValue(BaseHeightProperty))
                    {
                        _measureRequested = true;
                        InvalidateMeasure();
                        InvalidateArrange();
                    }
                    break;
            }
        }

        public void ObserveBaseHeightOf(CTextBlock target)
        {
            if (target is not null)
                this.Bind(BaseHeightProperty, target.GetBindingObservable(BaseHeightProperty));
        }

        private void ContentCollectionChangedd(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is not null)
                        DetachChildren(e.OldItems.Cast<CInline>());
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems is not null)
                        DetachChildren(e.OldItems.Cast<CInline>());

                    if (e.NewItems is not null)
                        AttachChildren(e.NewItems.Cast<CInline>());
                    break;

                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is not null)
                        AttachChildren(e.NewItems.Cast<CInline>());
                    break;
            }
        }

        /// <summary>
        /// Add CInline to LogicalChildren to inherit the value of AvaloniaProperty.
        /// And add Control, which is haved by CInlineUIContainer, to VisualChildren.
        /// </summary>
        private void AttachChildren(IEnumerable<CInline> newItems)
        {
            foreach (CInline item in newItems)
            {
                LogicalChildren.Add(item);
                AttachForVisual(item);
            }

            void AttachForVisual(CInline item)
            {
                if (item is CInlineUIContainer container)
                {
                    var content = container.Content;

                    var visparent = container.Content.GetVisualParent();
                    if (visparent is CTextBlock cblock)
                    {
                        cblock.VisualChildren.Remove(content);
                        cblock.LogicalChildren.Remove(content);
                    }
                    else if (visparent is object)
                    {
                        Debug.Print("Control has another parent");
                        return;
                    }

                    VisualChildren.Add(container.Content);
                    LogicalChildren.Add(container.Content);

                    _containers.Add(container);
                }
                else if (item is CSpan span)
                    foreach (var child in span.Content)
                        AttachForVisual(child);
            }
        }

        /// <summary>
        /// Remove CInline to LogicalChildren to inherit the value of AvaloniaProperty.
        /// And remove Control, which is haved by CInlineUIContainer, to VisualChildren.
        /// </summary>
        private void DetachChildren(IEnumerable<CInline> removeItems)
        {
            foreach (CInline item in removeItems)
            {
                LogicalChildren.Remove(item);
                DetachForVisual(item);
            }

            void DetachForVisual(CInline item)
            {
                if (item is CInlineUIContainer container)
                {
                    VisualChildren.Remove(container.Content);
                    LogicalChildren.Remove(container.Content);

                    _containers.Remove(container);
                }
                else if (item is CSpan span)
                    foreach (var child in span.Content)
                        DetachForVisual(child);
            }
        }

        internal void OnMeasureSourceChanged()
        {
            SetValue(BaseHeightProperty, default);
            _measureRequested = true;
            InvalidateMeasure();
            InvalidateArrange();
        }

        private void RepaintRequested()
        {
            InvalidateVisual();
        }

        /// <summary>
        /// Check to see if the arrangement size is different from the size of measuring.
        /// </summary>
        // 配置領域が寸法計算時に与えられた領域より広すぎるもしくは狭すぎないか確認します。
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_measured.Width > finalSize.Width)
            {
                finalSize = finalSize.WithWidth(Math.Ceiling(_measured.Width));
            }
            foreach (var container in _containers)
            {
                var indicator = container.Indicator;
                if (indicator is null) continue;

                indicator.Control.Arrange(new Rect(indicator.Left, indicator.Top, indicator.Width, indicator.Height));
            }
            if (AreClose(_constraint.Width, finalSize.Width))
            {
                return finalSize;
            }

            _constraint = new Size(finalSize.Width, Double.PositiveInfinity);
            _measured = UpdateGeometry();

            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_measured.Width == 0d || !AreClose(availableSize.Width, _constraint.Width) || _measureRequested)
            {
                _measureRequested = false;
                _constraint = availableSize;
                _measured = UpdateGeometry();
            }

            InvalidateArrange();

            return _measured;
        }

        private Size UpdateGeometry()
        {
            _metries.Clear();
            _lines.Clear();

            double entireWidth = _constraint.Width;
            if (Double.IsInfinity(_constraint.Width) && Bounds.Width != 0)
                entireWidth = Bounds.Width;


            double width = 0;
            double height = 0;

            // measure & split by linebreak
            var reqHeight = GetValue(BaseHeightProperty);
            var entireLineHeight = LineHeight;
            {
                LineInfo? now = null;

                double remainWidth = entireWidth;

                foreach (CInline inline in Content)
                {
                    IEnumerable<CGeometry> inlineGeometry =
                        inline.Measure(
                            (TextWrapping == TextWrapping.NoWrap) ? Double.PositiveInfinity : entireWidth,
                            (TextWrapping == TextWrapping.NoWrap) ? Double.PositiveInfinity : remainWidth);

                    foreach (CGeometry metry in inlineGeometry)
                    {
                        if (now is null)
                        {
                            _lines.Add(now = new LineInfo());
                            if (_lines.Count == 1)
                                now.RequestBaseHeight = reqHeight;
                        }

                        if (now.Add(metry))
                        {
                            if (!Double.IsNaN(entireLineHeight))
                                now.OverwriteHeight(entireLineHeight);

                            width = Math.Max(width, now.Width);
                            height += now.Height;

                            now = null;
                            remainWidth = entireWidth;
                        }
                        else remainWidth -= metry.Width;
                    }
                }

                if (now is not null)
                {
                    if (!Double.IsNaN(entireLineHeight))
                        now.OverwriteHeight(entireLineHeight);

                    width = Math.Max(width, now.Width);
                    height += now.Height;
                }
            }

            if (_lines.Count > 0)
            {
                _computedBaseHeight = _lines[0].BaseHeight;
                SetValue(BaseHeightProperty, _lines[0].BaseHeight);
            }

            var lineSpc = LineSpacing;
            height += lineSpc * (_lines.Count - 1);

            // set position
            {
                var topOffset = 0d;
                var leftOffset = 0d;

                foreach (LineInfo lineInf in _lines)
                {
                    lineInf.Top = topOffset;

                    switch (TextAlignment)
                    {
                        case TextAlignment.Left:
                            leftOffset = 0d;
                            break;
                        case TextAlignment.Center:
                            leftOffset = (entireWidth - lineInf.Width) / 2;
                            break;
                        case TextAlignment.Right:
                            leftOffset = entireWidth - lineInf.Width;
                            break;
                    }

                    foreach (CGeometry metry in lineInf.Metries)
                    {
                        metry.Left = leftOffset;
                        switch (metry.TextVerticalAlignment)
                        {
                            case TextVerticalAlignment.Top:
                                metry.Top = topOffset;
                                break;
                            case TextVerticalAlignment.Center:
                                metry.Top = topOffset + (lineInf.Height - metry.Height) / 2;
                                break;
                            case TextVerticalAlignment.Bottom:
                                metry.Top = topOffset + lineInf.Height - metry.Height;
                                break;
                            case TextVerticalAlignment.Base:
                                metry.Top = topOffset + lineInf.BaseHeight - metry.BaseHeight;
                                break;
                        }

                        leftOffset += metry.Width;

                        _metries.Add(metry);
                        metry.Arranged();
                    }

                    topOffset += lineInf.Height + lineSpc;
                }
            }

            // CCode pills overlay the line band but don't contribute to lineInf.Height (so they
            // don't push surrounding text down — see LineInfo.Add). That leaves the pill drawn
            // past the CTextBlock's measured bounds on the first/last line, which breaks both
            // visual clipping and outer hit-testing (ColorDocument's SelectionUtil routes by
            // control.Bounds.Bottom, so a cursor over the pill overhang lands on the *next*
            // document element). Expand the measured size to cover the pill's actual extent.
            double extraTop = 0d;
            double extraBottom = 0d;
            if (_lines.Count > 0)
            {
                foreach (var metry in _lines[0].Metries)
                {
                    if (metry.Owner is CCode)
                    {
                        var topOverhang = _lines[0].Top - metry.Top;
                        if (topOverhang > extraTop) extraTop = topOverhang;
                    }
                }

                var lastLine = _lines[_lines.Count - 1];
                foreach (var metry in lastLine.Metries)
                {
                    if (metry.Owner is CCode)
                    {
                        var bottomOverhang = (metry.Top + metry.Height) - (lastLine.Top + lastLine.Height);
                        if (bottomOverhang > extraBottom) extraBottom = bottomOverhang;
                    }
                }
            }

            if (extraTop > 0)
            {
                foreach (var line in _lines)
                    line.Top += extraTop;
                foreach (var metry in _metries)
                {
                    metry.Top += extraTop;
                    metry.Arranged();
                }
            }

            height += extraTop + extraBottom;

            foreach (CGeometry metry in _metries) metry.RepaintRequested += RepaintRequested;

            if (_beginSelect is not null && _endSelect is not null)
            {
                Select(_beginSelect.Index, _endSelect.Index);
            }

            return new Size(width, height);
        }

        public override void Render(DrawingContext context)
        {
            if (Background is not null)
            {
                context.FillRectangle(Background, new Rect(0, 0, Bounds.Width, Bounds.Height));
            }

            IBrush select = SelectionBrush ?? Brushes.Cyan;
            // bool flag = the rectangle covers an image and should be painted at reduced opacity
            // so the image stays visible underneath; everything else paints fully opaque.
            List<(Rect Rect, bool OverImage)>? fillAfter = null;

            if (_beginSelect is not null && _endSelect is not null)
            {
                fillAfter = new List<(Rect, bool)>();

                TextPointer bgn, end;
                if (_beginSelect < _endSelect)
                {
                    bgn = _beginSelect;
                    end = _endSelect;
                }
                else
                {
                    bgn = _endSelect;
                    end = _beginSelect;
                }


                if (ReferenceEquals(bgn.Geometry, end.Geometry))
                {
                    var rct = new Rect(
                        bgn.Geometry.Left + bgn.Distance,
                        bgn.Geometry.Top,
                        end.Distance - bgn.Distance,
                        bgn.Geometry.Height);

                    TryRender(bgn.Geometry, rct);
                }
                else
                {
                    TryRender(bgn.Geometry, new Rect(bgn.Geometry.Left + bgn.Distance, bgn.Geometry.Top, bgn.Geometry.Width - bgn.Distance, bgn.Geometry.Height));

                    foreach (var inter in _intermediates)
                    {
                        AddIntermediate(inter);
                    }

                    TryRender(end.Geometry, new Rect(end.Geometry.Left, end.Geometry.Top, end.Distance, end.Geometry.Height));
                }

                void TryRender(CGeometry metry, Rect rct)
                {
                    // A TextGeometry that lives INSIDE a CCode pill must be drawn AFTER the
                    // pill background, otherwise the pill (rendered later in this method)
                    // paints right over it and the selection looks invisible.
                    if (metry is TextGeometry && !IsGeometryInsidePill(metry))
                    {
                        context.FillRectangle(select, rct);
                    }
                    else
                    {
                        // Image selection paints on top of the already-rendered image —
                        // defer it so we can apply reduced opacity below.
                        fillAfter.Add((rct, metry is ImageGeometry));
                    }
                }

                void AddIntermediate(CGeometry inter)
                {
                    // For a whole CCode pill that sits between the selection endpoints,
                    // push selection rectangles for each inner Target instead of the pill's
                    // bounding box. This keeps the pill background visible underneath and
                    // only highlights the glyph band — same as in-pill text selection.
                    if (inter is DecoratorGeometry deco && deco.Owner is CCode)
                    {
                        foreach (var target in deco.Targets)
                            fillAfter.Add((new Rect(target.Left, target.Top, target.Width, target.Height), false));
                    }
                    else
                    {
                        TryRender(inter, new Rect(inter.Left, inter.Top, inter.Width, inter.Height));
                    }
                }
            }

            // Render in two halves so the deferred selection rectangles land BETWEEN
            // the pill background and the inner text — same layering as outside-pill
            // text selection (which is painted before the metries). The glyphs are
            // rendered last and remain crisp on top of the selection.
            foreach (var metry in _metries)
            {
                if (metry is DecoratorGeometry deco)
                    deco.RenderDecoration(context);
                else
                    metry.Render(context);
            }

            if (fillAfter is not null)
            {
                foreach (var (fillRct, overImage) in fillAfter)
                {
                    if (overImage)
                    {
                        using (context.PushOpacity(0.5))
                            context.FillRectangle(select, fillRct);
                    }
                    else
                    {
                        context.FillRectangle(select, fillRct);
                    }
                }
            }

            foreach (var metry in _metries)
            {
                if (metry is DecoratorGeometry deco)
                    deco.RenderTargets(context);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new CTextBlockAutomationPeer(this);
        }

        public void Select(int begin, int end)
        {
            int beginBack = begin;
            int endBack = end;
            for (var i = 0; i < _metries.Count; ++i)
            {
                var metry = _metries[i];
                var caretLength = metry.CaretLength;

                if (begin < caretLength || (i == _metries.Count - 1 && begin == caretLength))
                {
                    _beginSelect = metry.CalcuatePointerFrom(begin).Wrap(this, beginBack - begin);
                    begin = Int32.MaxValue;
                }
                else begin -= caretLength;

                if (end < caretLength || (i == _metries.Count - 1 && end == caretLength))
                {
                    _endSelect = metry.CalcuatePointerFrom(end).Wrap(this, endBack - end);
                    if (endBack != _endSelect.Index)
                        throw new Exception();
                    end = Int32.MaxValue;
                }
                else end -= caretLength;
            }
            ComplementIntermediate();
            InvalidateVisual();
        }

        public void Select(TextPointer begin, TextPointer end)
        {
            _beginSelect = begin;
            _endSelect = end;
            ComplementIntermediate();
            InvalidateVisual();
        }

        private void ComplementIntermediate()
        {
            bool bgn = false;
            bool end = false;

            _intermediates.Clear();
            foreach (var metry in _metries)
            {
                // The selection endpoint's Geometry may be NESTED inside this metry
                // (a TextLineGeometry inside a CCode pill's DecoratorGeometry). A flat
                // ReferenceEquals would miss it, and every metry after `bgn` would get
                // dumped into _intermediates — visually selecting the whole paragraph.
                bool hitB = MetryContainsGeometry(metry, _beginSelect.Geometry);
                bool hitE = MetryContainsGeometry(metry, _endSelect.Geometry);
                bgn |= hitB;
                end |= hitE;

                if (bgn && end) break;

                if (hitB | hitE) continue;

                if (bgn | end)
                {
                    _intermediates.Add(metry);
                }
            }
        }

        private static bool MetryContainsGeometry(CGeometry metry, CGeometry target)
        {
            if (ReferenceEquals(metry, target)) return true;
            if (metry is DecoratorGeometry deco)
            {
                foreach (var t in deco.Targets)
                    if (MetryContainsGeometry(t, target)) return true;
            }
            return false;
        }

        private bool IsGeometryInsidePill(CGeometry target)
        {
            foreach (var metry in _metries)
            {
                if (metry is DecoratorGeometry deco && deco.Owner is CCode)
                {
                    foreach (var t in deco.Targets)
                        if (MetryContainsGeometry(t, target)) return true;
                }
            }
            return false;
        }


        public void ClearSelection()
        {
            _beginSelect = null;
            _endSelect = null;
            _intermediates.Clear();
            InvalidateVisual();
        }

        public TextPointer CalcuatePointerFrom(double x, double y)
        {
            if (y < 0)
            {
                return GetBegin();
            }

            // Inline code pills can extend vertically beyond the surrounding line's text height
            // (they intentionally don't contribute to LineInfo.Height — see LineInfo.Add). Without
            // this direct hit-test, a cursor over the visible pill but past line.Top + line.Height
            // would fall through to the next line and select it.
            {
                int idx = 0;
                foreach (var metry in _metries)
                {
                    if (metry.Owner is CCode
                        && x >= metry.Left && x <= metry.Left + metry.Width
                        && y >= metry.Top && y <= metry.Top + metry.Height)
                    {
                        return metry.CalcuatePointerFrom(x, y).Wrap(this, idx);
                    }
                    idx += metry.CaretLength;
                }
            }

            int indexAdd = 0;
            foreach (var line in _lines)
            {
                // Include any CCode pill overhang in this line's vertical hit range —
                // line.Height excludes CCode (so the pill doesn't push text down), but
                // a cursor on the pill's overhang still belongs to this line for selection.
                var lineBottom = line.Top + line.Height;
                foreach (var m in line.Metries)
                {
                    if (m.Owner is CCode)
                    {
                        var mb = m.Top + m.Height;
                        if (mb > lineBottom) lineBottom = mb;
                    }
                }

                if (y <= lineBottom)
                {
                    // For x past every resolvable metry on this line, delegate to the LAST
                    // resolvable one (its CalcuatePointerFrom returns its end) so we land at
                    // the end of THIS line — not fall through to the next line, which would
                    // accumulate past every following line and return GetEnd() of the whole
                    // block (the "select all paragraph" symptom). LineBreakMarkGeometry is
                    // skipped because its geometric CalcuatePointerFrom throws.
                    int lastResolvable = -1;
                    for (int i = line.Metries.Count - 1; i >= 0; i--)
                    {
                        if (line.Metries[i] is not LineBreakMarkGeometry)
                        {
                            lastResolvable = i;
                            break;
                        }
                    }

                    for (int i = 0; i < line.Metries.Count; i++)
                    {
                        var target = line.Metries[i];
                        var canResolveHere = target is not LineBreakMarkGeometry && x <= target.Left + target.Width;
                        if (canResolveHere || i == lastResolvable)
                        {
                            return target.CalcuatePointerFrom(x, y).Wrap(this, indexAdd);
                        }
                        indexAdd += target.CaretLength;
                    }
                }
                else
                {
                    indexAdd += line.Metries.Sum(t => t.CaretLength);
                }
            }

            return GetEnd();
        }

        public TextPointer CalcuatePointerFrom(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            foreach (var metry in _metries)
            {
                var caretLength = metry.CaretLength;

                if (index < caretLength)
                {
                    return metry.CalcuatePointerFrom(index);
                }
                else index -= caretLength;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public TextPointer GetBegin()
        {
            if (_metries.Count != 0)
            {
                return _metries[0].GetBegin().Wrap(this, 0);
            }
            else
            {
                return new TextPointer(this, 0);
            }
        }

        public TextPointer GetEnd()
        {
            if (_metries.Count != 0)
            {
                var pointer = _metries[_metries.Count - 1].GetEnd();

                int indexAdd = _metries.Take(_metries.Count - 1).Sum(t => t.CaretLength);
                return pointer.Wrap(this, indexAdd);
            }
            else
            {
                return new TextPointer(this, 0);
            }
        }

        public string GetSelectedText()
        {
            if (_beginSelect is null || _endSelect is null)
            {
                return string.Empty;
            }

            TextPointer bgn, end;
            if (_beginSelect < _endSelect)
            {
                bgn = _beginSelect;
                end = _endSelect;
            }
            else
            {
                bgn = _endSelect;
                end = _beginSelect;
            }

            if (ReferenceEquals(bgn.Geometry, end.Geometry))
            {
                if (bgn.Geometry is TextLineGeometry tlg)
                {
                    return tlg.Text.Substring(bgn.InternalIndex, end.InternalIndex - bgn.InternalIndex);
                }
                else return "";
            }
            else
            {
                var buffer = new StringBuilder();

                if (bgn.Geometry is TextLineGeometry btlg)
                    buffer.Append(btlg.Text.Substring(bgn.InternalIndex));

                foreach (var inter in _intermediates)
                {
                    AppendAllText(buffer, inter);
                }

                if (end.Geometry is TextLineGeometry etlg)
                    buffer.Append(etlg.Text.Substring(etlg.Line.FirstTextSourceIndex, end.InternalIndex - etlg.Line.FirstTextSourceIndex));

                return buffer.ToString();
            }
        }

        // An intermediate metry is not always a bare TextLineGeometry: a decorated span
        // (an inline code pill, which CSpan wraps in a Border as soon as it is given a
        // padding, corner radius, border thickness, margin or box shadow) arrives as a
        // DecoratorGeometry holding the real text geometries. ComplementIntermediate
        // already looks through that nesting, so this has to as well - otherwise a
        // selection spanning a pill silently drops everything inside it.
        private static void AppendAllText(StringBuilder buffer, CGeometry metry)
        {
            switch (metry)
            {
                case TextLineGeometry tlg:
                    buffer.Append(tlg.ToString());
                    break;

                case DecoratorGeometry deco:
                    foreach (var target in deco.Targets)
                        AppendAllText(buffer, target);
                    break;
            }
        }

        // Equivalent to the (now internal) Avalonia.Utilities.MathUtilities.AreClose.
        private static bool AreClose(double value1, double value2)
        {
            if (value1 == value2) return true;
            const double doubleEpsilon = 2.2204460492503131e-016;
            double eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * doubleEpsilon;
            double delta = value1 - value2;
            return -eps < delta && eps > delta;
        }
    }




    public class Selection
    {
        public int From { get; }
        public int To { get; }
        public Selection(int f, int t)
        {
            From = f;
            To = t;
        }
    }

    class LineInfo
    {
        public List<CGeometry> Metries = new();

        public double RequestBaseHeight;
        private double BaseHeight1;
        private double BaseHeight2;

        private double _height;
        private double _dheightTop;
        private double _dheightBtm;
        private double _codeBaseHeight;
        private double _codeHeight;

        public double Top { get; internal set; }
        public double Width { private set; get; }
        public double Height
        {
            get
            {
                var textHeight = Math.Max(_height, _dheightTop + _dheightBtm);
                return textHeight != 0 ? textHeight : _codeHeight;
            }
        }
        public double BaseHeight
        {
            get
            {
                var textBase = BaseHeight1 != 0 ? BaseHeight1 : BaseHeight2;
                return Math.Max(RequestBaseHeight, textBase != 0 ? textBase : _codeBaseHeight);
            }
        }

        public bool Add(CGeometry metry)
        {
            Metries.Add(metry);

            Width += metry.Width;

            // Inline code pills overlay the line and must not drive its baseline/height; otherwise
            // their taller font metrics (CJK/emoji/mono) or vertical padding push the line's text
            // down and tighten line spacing. They align to the surrounding text baseline (handled in
            // DecoratorGeometry), and contribute metrics only as a fallback for a line that contains
            // nothing but inline code.
            if (metry.Owner is CCode)
            {
                Max(ref _codeBaseHeight, metry.BaseHeight);
                Max(ref _codeHeight, metry.Height);
                return metry.LineBreak;
            }

            switch (metry.TextVerticalAlignment)
            {
                case TextVerticalAlignment.Base:
                    Max(ref BaseHeight1, metry.BaseHeight);
                    Max(ref _dheightTop, metry.BaseHeight);
                    Max(ref _dheightBtm, metry.Height - metry.BaseHeight);
                    break;

                case TextVerticalAlignment.Top:
                    Max(ref BaseHeight1, metry.BaseHeight);
                    Max(ref _height, metry.Height);
                    break;

                case TextVerticalAlignment.Center:
                    Max(ref BaseHeight1, metry.Height / 2);
                    Max(ref _height, metry.Height);
                    break;

                case TextVerticalAlignment.Bottom:
                    Max(ref BaseHeight2, metry.BaseHeight);
                    Max(ref _height, metry.Height);
                    break;

                default:
                    Throw("sorry library manager forget to modify.");
                    break;
            }

            return metry.LineBreak;
        }

        public void OverwriteHeight(double height)
        {
            _height = height;
            _dheightBtm = _dheightTop = 0;
        }

        private static void Max(ref double v1, double v2) => v1 = Math.Max(v1, v2);
        private static void Throw(string msg) => throw new InvalidOperationException(msg);
    }
}
