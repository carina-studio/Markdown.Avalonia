using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace ColorTextBlock.Avalonia.Geometries
{
    internal class TextLineGeometry : TextGeometry
    {
        public SimpleTextSource Text { get; private set; }
        public TextLine Line { get; private set; }
        public IBrush? LayoutForeground { get; private set; }

        internal TextLineGeometry(
            CRun owner,
            SimpleTextSource text,
            TextLine tline,
            bool linebreak) :
            base(owner, tline.WidthIncludingTrailingWhitespace, tline.Height, tline.Baseline, owner.TextVerticalAlignment, linebreak)
        {
            Text = text;
            Line = tline;
            LayoutForeground = owner.Foreground;
        }

        public override double TextBandTop
            => TryGetTextBand(out var top, out _) ? top : base.TextBandTop;

        public override double TextBandBottom
            => TryGetTextBand(out _, out var bottom) ? bottom : base.TextBandBottom;

        // The em-box of the run's *nominal* font (e.g. the monospace family for inline code), placed
        // at the line's leading-adjusted baseline. Using the nominal font keeps the band height
        // consistent regardless of content (Latin vs CJK vs emoji fallback glyphs), so code pills are
        // a uniform height; the (shorter) fallback glyphs sit within this box.
        private bool TryGetTextBand(out double top, out double bottom)
        {
            top = 0;
            bottom = 0;

            var metrics = Owner.Typeface.GlyphTypeface.Metrics;
            if (metrics.DesignEmHeight <= 0)
                return false;

            var scale = Owner.FontSize / metrics.DesignEmHeight;
            top = Line.Baseline - Math.Abs(metrics.Ascent * scale);
            bottom = Line.Baseline + Math.Abs(metrics.Descent * scale);
            return bottom > top;
        }

        public override void Render(DrawingContext ctx)
        {
            var background = TemporaryBackground ?? Background;
            if (background != null)
            {
                // fill the background over the glyph band so it matches the centred code pill
                ctx.FillRectangle(background, new Rect(Left, Top + TextBandTop, Width, TextBandBottom - TextBandTop));
            }

            RenderForeground(ctx);
        }

        public override void RenderForeground(DrawingContext ctx)
        {
            var foreground = TemporaryForeground ?? Foreground;

            if (LayoutForeground != foreground)
            {
                LayoutForeground = foreground;
                Text = Text.ChangeForeground(foreground);

                var owner = (CRun)Owner;
                var parPrps = owner.CreateTextParagraphProperties(Text.RunProperties);

                Line = TextFormatter.Current.FormatLine(
                            Text,
                            Line.FirstTextSourceIndex,
                            Width,
                            parPrps)!;
            }

            Line.Draw(ctx, new Point(Left, Top));

            if (IsUnderline)
            {
                var ypos = Math.Round(Top + Height);
                ctx.DrawLine(new Pen(foreground, 1),
                    new Point(Left, ypos),
                    new Point(Left + Width, ypos));
            }

            if (IsStrikethrough)
            {
                var ypos = Math.Round(Top + Height / 2);
                ctx.DrawLine(new Pen(foreground, 1),
                    new Point(Left, ypos),
                    new Point(Left + Width, ypos));
            }
        }

        public override TextPointer CalcuatePointerFrom(double x, double y)
        {
            var relX = x - Left;

            if (relX < 0) return GetBegin();
            if (relX >= Width) return GetEnd();

            var hit = Line.GetCharacterHitFromDistance(relX);
            var dst = Line.GetDistanceFromCharacterHit(hit);

            return new TextPointer((CRun)Owner, this, hit, dst, false);
        }
        public override TextPointer CalcuatePointerFrom(int index)
        {
            var hit = new CharacterHit(Line.FirstTextSourceIndex + index);
            var dst = Line.GetDistanceFromCharacterHit(hit);

            return new TextPointer((CRun)Owner, this, hit, dst, false);
        }

        public override TextPointer GetBegin()
        {
            var hit = Line.GetCharacterHitFromDistance(0);

            return new TextPointer((CRun)Owner, this, hit, false);
        }

        public override TextPointer GetEnd()
        {
            var hit = Line.GetCharacterHitFromDistance(Double.MaxValue);

            return new TextPointer((CRun)Owner, this, hit, Width, true);
        }

        public override string ToString()
            => Text.Substring(Line.FirstTextSourceIndex, Line.Length);
    }
}
