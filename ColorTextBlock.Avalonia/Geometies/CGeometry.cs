using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ColorTextBlock.Avalonia.Geometries
{
    public abstract class CGeometry : ITextPointerHandleable
    {
        public CInline Owner { get; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; }
        public double Height { get; }
        public double BaseHeight { get; }
        public bool LineBreak { get; }
        public TextVerticalAlignment TextVerticalAlignment { get; }

        /// <summary>
        /// Top edge of the visual text band, measured from this geometry's top.
        /// Defaults to the whole box; text geometries narrow it to the glyph metrics.
        /// </summary>
        public virtual double TextBandTop => 0;

        /// <summary>
        /// Bottom edge of the visual text band, measured from this geometry's top.
        /// Defaults to the whole box; text geometries narrow it to the glyph metrics.
        /// </summary>
        public virtual double TextBandBottom => Height;

        public event Action? RepaintRequested;

        public virtual Action<Control>? OnMouseEnter { get; set; }
        public virtual Action<Control>? OnMouseLeave { get; set; }
        public virtual Action<Control>? OnMousePressed { get; set; }
        public virtual Action<Control>? OnMouseReleased { get; set; }
        public virtual Action<Control>? OnClick { get; set; }

        private int? _caretLength;

        public CGeometry(
            CInline owner,
            double width, double height, double baseHeight,
            TextVerticalAlignment textVerticalAlignment,
            bool linebreak)
        {
            this.Owner = owner;
            this.Width = width;
            this.Height = height;
            this.BaseHeight = baseHeight;
            this.TextVerticalAlignment = textVerticalAlignment;
            this.LineBreak = linebreak;
        }

        public abstract void Render(DrawingContext ctx);

        /// <summary>
        /// Paint the foreground part of this geometry (glyphs, image, etc.) without re-painting
        /// any background fill that has already been drawn by an outer geometry — e.g. a
        /// <see cref="DecoratorGeometry"/> that has already painted the CCode pill background.
        /// </summary>
        /// <remarks>
        /// Used by <see cref="DecoratorGeometry.RenderTargets"/> so an inside-pill selection
        /// rectangle painted between the pill background and the glyphs is not erased by the
        /// inner geometry re-filling the same background. Defaults to <see cref="Render"/>;
        /// override in text geometries to skip the background fill.
        /// </remarks>
        public virtual void RenderForeground(DrawingContext ctx) => Render(ctx);

        internal void RequestRepaint() => RepaintRequested?.Invoke();

        public abstract TextPointer CalcuatePointerFrom(int index);
        public abstract TextPointer CalcuatePointerFrom(double x, double y);
        public abstract TextPointer GetBegin();
        public abstract TextPointer GetEnd();

        public virtual void Arranged() { }


        public virtual int CaretLength
        {
            get
            {
                if (!_caretLength.HasValue)
                    _caretLength = GetEnd().Index - GetBegin().Index;

                return _caretLength.Value;
            }
        }
    }
}
