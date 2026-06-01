using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace ColorTextBlock.Avalonia.Geometries
{
    public class DecoratorGeometry : CGeometry
    {
        public CGeometry[] Targets { get; }
        public Border Decorate { get; }

        private Action<Control>? _OnMouseEnter;
        private Action<Control>? _OnMouseLeave;
        private Action<Control>? _OnMousePressed;
        private Action<Control>? _OnMouseReleased;
        private Action<Control>? _OnClick;

        // Return null when there is no own handler AND no target has one. The previous
        // implementation always returned a non-null lambda, which made CTextBlock.OnPointer*
        // treat every DecoratorGeometry as interactive — swallowing the press with
        // e.Handled = true and blocking outer selection (e.g. ColorDocument) from starting
        // a drag-select when the user clicks inside a CCode pill.
        public override Action<Control>? OnMouseEnter
        {
            get
            {
                if (_OnMouseEnter is null && !HasAnyTargetHandler(t => t.OnMouseEnter))
                    return null;
                return ctrl =>
                {
                    _OnMouseEnter?.Invoke(ctrl);
                    foreach (var target in Targets)
                        target.OnMouseEnter?.Invoke(ctrl);
                };
            }
            set => _OnMouseEnter = value;
        }
        public override Action<Control>? OnMouseLeave
        {
            get
            {
                if (_OnMouseLeave is null && !HasAnyTargetHandler(t => t.OnMouseLeave))
                    return null;
                return ctrl =>
                {
                    _OnMouseLeave?.Invoke(ctrl);
                    foreach (var target in Targets)
                        target.OnMouseLeave?.Invoke(ctrl);
                };
            }
            set => _OnMouseLeave = value;
        }
        public override Action<Control>? OnMousePressed
        {
            get
            {
                if (_OnMousePressed is null && !HasAnyTargetHandler(t => t.OnMousePressed))
                    return null;
                return ctrl =>
                {
                    _OnMousePressed?.Invoke(ctrl);
                    foreach (var target in Targets)
                        target.OnMousePressed?.Invoke(ctrl);
                };
            }
            set => _OnMousePressed = value;
        }
        public override Action<Control>? OnMouseReleased
        {
            get
            {
                if (_OnMouseReleased is null && !HasAnyTargetHandler(t => t.OnMouseReleased))
                    return null;
                return ctrl =>
                {
                    _OnMouseReleased?.Invoke(ctrl);
                    foreach (var target in Targets)
                        target.OnMouseReleased?.Invoke(ctrl);
                };
            }
            set => _OnMouseReleased = value;
        }
        public override Action<Control>? OnClick
        {
            get
            {
                if (_OnClick is null && !HasAnyTargetHandler(t => t.OnClick))
                    return null;
                return ctrl =>
                {
                    _OnClick?.Invoke(ctrl);
                    foreach (var target in Targets)
                        target.OnClick?.Invoke(ctrl);
                };
            }
            set => _OnClick = value;
        }

        private bool HasAnyTargetHandler(Func<CGeometry, Action<Control>?> selector)
        {
            foreach (var target in Targets)
                if (selector(target) is not null)
                    return true;
            return false;
        }

        internal static DecoratorGeometry New(
            CSpan owner,
            IEnumerable<CGeometry> oneline,
            Border decorate)
        {
            double width = 0;
            double height = 0;

            double descentHeightTop = 0;
            double descentHeightBtm = 0;

            double baseHeight = 0;
            double baseHeight2 = 0;

            void Max(ref double v1, double v2) => v1 = Math.Max(v1, v2);

            foreach (var one in oneline)
            {
                width += one.Width;

                switch (one.TextVerticalAlignment)
                {
                    case TextVerticalAlignment.Base:
                        Max(ref baseHeight, one.BaseHeight);

                        Max(ref descentHeightTop, one.BaseHeight);
                        Max(ref descentHeightBtm, one.Height - one.BaseHeight);
                        break;

                    case TextVerticalAlignment.Top:
                        Max(ref baseHeight, one.BaseHeight);
                        Max(ref height, one.Height);
                        break;

                    case TextVerticalAlignment.Center:
                        Max(ref baseHeight, one.Height / 2);
                        Max(ref height, one.Height);
                        break;

                    case TextVerticalAlignment.Bottom:
                        Max(ref baseHeight2, one.BaseHeight);
                        Max(ref height, one.Height);
                        break;

                    default:
                        throw new InvalidOperationException("sorry library manager forget to modify.");
                }

            }

            Max(ref height, descentHeightTop + descentHeightBtm);

            baseHeight = baseHeight != 0 ? baseHeight : baseHeight2;

            return new DecoratorGeometry(
                width + decorate.DesiredSize.Width,
                height + decorate.DesiredSize.Height,
                baseHeight + decorate.Margin.Top + decorate.BorderThickness.Top + decorate.Padding.Top,
                owner,
                oneline.ToArray(),
                decorate);
        }

        private DecoratorGeometry(
            double w, double h, double lh,
            CSpan owner,
            CGeometry[] targets,
            Border decorate) : base(
                owner,
                w, h, lh,
                owner.TextVerticalAlignment,
                targets[targets.Length - 1].LineBreak)
        {
            this.Targets = targets;
            this.Decorate = decorate;
        }

        public override void Arranged()
        {
            var left = Left + Decorate.BorderThickness.Left + Decorate.Padding.Left + Decorate.Margin.Left;
            var top = Top + Decorate.BorderThickness.Top + Decorate.Padding.Top + Decorate.Margin.Top;
            var btm = Top + Height - Decorate.BorderThickness.Bottom - Decorate.Padding.Bottom - Decorate.Margin.Bottom;

            foreach (var target in Targets)
            {
                target.Left = left;

                target.Top = target.TextVerticalAlignment switch
                {
                    TextVerticalAlignment.Top
                        => top,

                    TextVerticalAlignment.Center
                        => (top + btm - target.Height) / 2,

                    TextVerticalAlignment.Bottom
                        => btm - target.Height,

                    TextVerticalAlignment.Base
                        => Top + BaseHeight - target.BaseHeight,

                    _ => throw new InvalidOperationException("sorry library manager forget to modify.")
                };

                left += target.Width;

                target.Arranged();
            }
        }

        public override void Render(DrawingContext ctx)
        {
            RenderDecoration(ctx);
            RenderTargets(ctx);
        }

        /// <summary>
        /// Paint just the decoration (the CCode pill background, or the non-code
        /// <see cref="Decorate"/> Border) without rendering the inner text <see cref="Targets"/>.
        /// </summary>
        /// <remarks>
        /// Split out from <see cref="Render"/> so <see cref="CTextBlock"/> can paint selection
        /// rectangles between the pill background and the glyphs — selection sits below the
        /// text, the glyphs render last and stay crisp.
        /// </remarks>
        public void RenderDecoration(DrawingContext ctx)
        {
            if (Owner is CCode)
            {
                // Draw the code pill directly so it can be sized/positioned to the glyph band.
                // (Going through Decorate.Arrange/Render ignores the requested position and size.)
                var bounds = GetDecorateBounds();
                var rect = new Rect(
                    Left + Decorate.Margin.Left,
                    Top + Decorate.Margin.Top + bounds.Y,
                    Math.Max(0, bounds.Width - Decorate.Margin.Left - Decorate.Margin.Right),
                    bounds.Height);

                if (Owner.Background is not null && rect.Width > 0 && rect.Height > 0)
                {
                    var pen = (Decorate.BorderThickness != default && Decorate.BorderBrush is not null)
                        ? new Pen(Decorate.BorderBrush, Decorate.BorderThickness.Top)
                        : null;
                    ctx.DrawRectangle(Owner.Background, pen, rect, Decorate.CornerRadius.TopLeft, Decorate.CornerRadius.TopLeft);
                }
            }
            else
            {
                using (ctx.PushTransform(Matrix.CreateTranslation(Left + Decorate.Margin.Left, Top + Decorate.Margin.Top)))
                {
                    Decorate.Background = Owner.Background;
                    Decorate.Arrange(new Rect(0, 0, Width, Height));
                    Decorate.Render(ctx);

                }
            }
        }

        /// <summary>
        /// Paint the inner text geometries without re-painting the decoration.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="CGeometry.RenderForeground"/> instead of <see cref="CGeometry.Render"/>
        /// so a text target doesn't re-fill the pill background (already painted by
        /// <see cref="RenderDecoration"/>). If we used full Render here, the inner re-fill would
        /// cover any selection rectangle that <see cref="CTextBlock"/> painted between the two.
        /// </remarks>
        public void RenderTargets(DrawingContext ctx)
        {
            foreach (var target in Targets)
                target.RenderForeground(ctx);
        }

        // The bounds (in decorator-local space) the background/border is drawn into.
        // Code spans hug the glyph band so the pill is vertically centred on the text;
        // other spans fill the whole box as before.
        private Rect GetDecorateBounds()
        {
            if (Owner is not CCode || Targets.Length == 0)
                return new Rect(0, 0, Width, Height);

            // union the visual text band across the targets, converted into the decorator-local space
            var originY = Top + Decorate.Margin.Top;
            var bandTop = double.PositiveInfinity;
            var bandBottom = double.NegativeInfinity;
            foreach (var target in Targets)
            {
                bandTop = Math.Min(bandTop, target.Top - originY + target.TextBandTop);
                bandBottom = Math.Max(bandBottom, target.Top - originY + target.TextBandBottom);
            }

            // fall back to the whole box if the band could not be resolved
            if (double.IsInfinity(bandTop) || double.IsInfinity(bandBottom) || bandBottom <= bandTop)
                return new Rect(0, 0, Width, Height);

            // pad the ink band symmetrically with the decorator's vertical padding/border, keep full width
            var top = bandTop - Decorate.Padding.Top - Decorate.BorderThickness.Top;
            var bottom = bandBottom + Decorate.Padding.Bottom + Decorate.BorderThickness.Bottom;
            return new Rect(0, top, Width, bottom - top);
        }

        public override TextPointer CalcuatePointerFrom(double x, double y)
        {
            if (x < Left)
            {
                return GetBegin();
            }

            int indexAdd = 0;
            for (int i = 0; i < Targets.Length; i++)
            {
                var target = Targets[i];
                var isLast = i == Targets.Length - 1;
                // Delegate to the target when the click is inside it, or unconditionally
                // for the last target so x beyond all targets still resolves to its end.
                if (isLast || x <= target.Left + target.Width)
                {
                    return target.CalcuatePointerFrom(x, y).Wrap(Owner, indexAdd);
                }
                indexAdd += target.CaretLength;
            }

            return GetEnd();
        }

        public override TextPointer CalcuatePointerFrom(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            int relindex = index;
            for (int i = 0; i < Targets.Length; i++)
            {
                var target = Targets[i];
                var isLast = i == Targets.Length - 1;
                // CTextBlock.Select allows the caret to land exactly on the end of the
                // last metry (begin == caretLength). Mirror that here for the last target
                // so a selection that ends at the trailing edge of a CCode pill on a
                // re-measure (e.g. window resize) resolves to its end pointer instead of
                // throwing — TextLineGeometry.CalcuatePointerFrom accepts index == line length.
                if (relindex < target.CaretLength || (isLast && relindex == target.CaretLength))
                {
                    return target.CalcuatePointerFrom(relindex)
                                 .Wrap(Owner, index - relindex);
                }

                relindex -= target.CaretLength;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }


        public override TextPointer GetBegin()
        {
            var pointer = Targets[0].GetBegin();
            return pointer.Wrap(Owner, 0);
        }

        public override TextPointer GetEnd()
        {
            var pointer = Targets[Targets.Length - 1].GetEnd();

            int indexAdd = Targets.Take(Targets.Length - 1).Sum(t => t.CaretLength);
            return pointer.Wrap(Owner, indexAdd);
        }
    }
}
