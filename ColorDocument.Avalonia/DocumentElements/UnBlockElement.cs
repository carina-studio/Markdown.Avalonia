using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorDocument.Avalonia.DocumentElements
{
    public class UnBlockElement : DocumentElement
    {
        private Control _control;

        public override Control Control => _control;

        public override IEnumerable<DocumentElement> Children => Array.Empty<DocumentElement>();

        public UnBlockElement(Control control)
        {
            _control = control;
        }

        // UnBlockElement wraps an arbitrary Control produced by an old-style parser (the HTML
        // bridge, horizontal-rule renderer, container plugins). The default no-op Select makes
        // any text inside silently unselectable — see e.g. the demo's `< notetext >` line which
        // HtmlBlockParser swallows along with everything after it (including the trailing
        // blockquote, which then contains multiple CTextBlocks: H2 heading + paragraph + list
        // items). Walk the visual tree for every CTextBlock and dispatch selection vertically,
        // the same shape as SelectionUtil.SelectVertical does at the document root.
        private struct Located
        {
            public CTextBlock Block;
            public double Left;
            public double Top;
            public double Bottom => Top + Block.Bounds.Height;
        }

        private List<Located> Collect()
        {
            var list = new List<Located>();
            Visit(_control, 0, 0, list);
            list.Sort((a, b) => a.Top.CompareTo(b.Top));
            return list;
        }

        private static void Visit(Visual v, double offX, double offY, List<Located> outto)
        {
            if (v is CTextBlock t)
            {
                outto.Add(new Located { Block = t, Left = offX, Top = offY });
                return;
            }
            foreach (var raw in v.GetVisualChildren())
            {
                if (raw is Visual vc)
                    Visit(vc, offX + vc.Bounds.X, offY + vc.Bounds.Y, outto);
            }
        }

        private static int IndexAtY(List<Located> blocks, double y)
        {
            for (int i = 0; i < blocks.Count; i++)
                if (y < blocks[i].Bottom) return i;
            return blocks.Count - 1;
        }

        public override void Select(Point from, Point to)
        {
            var blocks = Collect();
            if (blocks.Count == 0) return;

            int fp = IndexAtY(blocks, from.Y);
            int tp = IndexAtY(blocks, to.Y);
            int lo = Math.Min(fp, tp);
            int hi = Math.Max(fp, tp);

            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (i < lo || i > hi)
                {
                    b.Block.ClearSelection();
                    continue;
                }

                // Same sentinel convention SelectionUtil.Select uses for intermediates:
                // negative Y → CTextBlock.CalcuatePointerFrom returns GetBegin();
                // positive-infinity Y → it returns GetEnd().
                Point localFrom = i == lo
                    ? new Point((lo == fp ? from.X : to.X) - b.Left, (lo == fp ? from.Y : to.Y) - b.Top)
                    : new Point(0, -1);
                Point localTo = i == hi
                    ? new Point((hi == tp ? to.X : from.X) - b.Left, (hi == tp ? to.Y : from.Y) - b.Top)
                    : new Point(double.PositiveInfinity, double.PositiveInfinity);

                var fromPt = b.Block.CalcuatePointerFrom(localFrom.X, localFrom.Y);
                var toPt = b.Block.CalcuatePointerFrom(localTo.X, localTo.Y);
                b.Block.Select(fromPt, toPt);
            }
        }

        public override void UnSelect()
        {
            foreach (var b in Collect())
                b.Block.ClearSelection();
        }

        public override void ConstructSelectedText(StringBuilder stringBuilder)
        {
            foreach (var b in Collect())
            {
                var t = b.Block.GetSelectedText();
                if (string.IsNullOrEmpty(t)) continue;
                stringBuilder.Append(t);
                if (stringBuilder[stringBuilder.Length - 1] != '\n')
                    stringBuilder.Append('\n');
            }
        }
    }
}

