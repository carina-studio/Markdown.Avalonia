using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorDocument.Avalonia
{
    public abstract class DocumentElement
    {
        private ISelectionRenderHelper? _helper;

        public abstract Control Control { get; }
        public abstract IEnumerable<DocumentElement> Children { get; }

        public virtual ISelectionRenderHelper? Helper
        {
            get => _helper;
            set
            {
                _helper = value;
                foreach (var child in Children)
                    child.Helper = value;
            }
        }

        public Rect GetRect(Layoutable anchor) => Control.GetRectInDoc(anchor).GetValueOrDefault();
        public abstract void Select(Point from, Point to);
        public abstract void UnSelect();

        public virtual string GetSelectedText()
        {
            var builder = new StringBuilder();
            ConstructSelectedText(builder);
            return builder.ToString();
        }

        public abstract void ConstructSelectedText(StringBuilder stringBuilder);

    }

    public interface ISelectionRenderHelper
    {
        void Register(Control control);

        /// <summary>
        /// Register a partial highlight for <paramref name="control"/>.
        /// </summary>
        /// <param name="control">The visual whose bounds anchor the rectangles in the document.</param>
        /// <param name="rectsInControlSpace">
        /// Rectangles in <paramref name="control"/>'s own coordinate space (origin = control top-left).
        /// </param>
        void Register(Control control, IEnumerable<Rect> rectsInControlSpace);

        void Unregister(Control control);

        /// <summary>
        /// The current selection brush. Elements that paint their own selection
        /// (e.g. inside a code block) should read this and subscribe to
        /// <see cref="SelectionBrushChanged"/> for live updates.
        /// </summary>
        IBrush SelectionBrush { get; }

        /// <summary>
        /// Raised whenever <see cref="SelectionBrush"/> changes.
        /// </summary>
        event EventHandler? SelectionBrushChanged;
    }
}
