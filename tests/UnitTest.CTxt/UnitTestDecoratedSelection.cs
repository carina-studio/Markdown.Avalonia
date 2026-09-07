using Avalonia;
using ColorTextBlock.Avalonia;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Text;
using UnitTest.Base;
using UnitTest.Base.Utils;

namespace UnitTest.CTxt
{
    /// <summary>
    /// A decorated span - inline code being the usual one - is only laid out as a
    /// DecoratorGeometry once it is given a padding, corner radius, border thickness,
    /// margin or box shadow (see CSpan.OnBorderPropertyChanged). A theme that draws inline
    /// code as a rounded pill does exactly that, and then the span's text geometries sit
    /// nested inside the decorator rather than directly in the block's own list.
    ///
    /// Selection has to look through that nesting on both sides: the highlight already did,
    /// so a pill in the middle of a selection was painted as selected while its text was
    /// dropped from the copied string, leaving just the characters around it.
    /// </summary>
    public class UnitTestDecoratedSelection : UnitTestBase
    {
        private const string Emoji = "\U0001F60A";

        private static readonly object[] Contents =
        {
            "abc",
            Emoji + Emoji + Emoji,
            "a" + Emoji + "b",
        };

        /// <summary>
        /// "/" + a code span holding <paramref name="inner"/> + "/". The slashes sit outside
        /// the span, so a drag across the whole line puts the span strictly between the two
        /// selection endpoints - the case that used to lose its text.
        /// </summary>
        private static CTextBlock Build(string inner, bool decorated)
        {
            var code = new CCode(new CInline[] { new CRun { Text = inner } });
            if (decorated)
            {
                code.Padding = new Thickness(4, 1);
                code.CornerRadius = new CornerRadius(3);
            }

            var ctxt = new CTextBlock();
            ctxt.Content.Add(new CRun { Text = "/" });
            ctxt.Content.Add(code);
            ctxt.Content.Add(new CRun { Text = "/" });
            ctxt.Measure(new Size(1000, 1000));
            ctxt.Arrange(new Rect(0, 0, 1000, ctxt.DesiredSize.Height));
            return ctxt;
        }

        private static string SelectByX(CTextBlock ctxt, double fromX, double toX)
        {
            var y = ctxt.DesiredSize.Height / 2;
            ctxt.Select(ctxt.CalcuatePointerFrom(fromX, y), ctxt.CalcuatePointerFrom(toX, y));
            return ctxt.GetSelectedText();
        }

        private static string SelectAll(CTextBlock ctxt)
            => SelectByX(ctxt, 0, ctxt.DesiredSize.Width);

        [TestCaseSource(nameof(Contents))]
        [RunOnUI]
        public void SelectionSpanningADecoratedSpan_keepsItsText(string inner)
        {
            var ctxt = Build(inner, decorated: true);

            Assert.That(SelectAll(ctxt), Is.EqualTo("/" + inner + "/"));
        }

        /// <summary>
        /// Decoration is presentation: it must not change what a selection copies.
        /// </summary>
        [TestCaseSource(nameof(Contents))]
        [RunOnUI]
        public void DecoratedAndPlainSpans_copyTheSameText(string inner)
        {
            var decorated = SelectAll(Build(inner, decorated: true));
            var plain = SelectAll(Build(inner, decorated: false));

            Assert.That(decorated, Is.EqualTo(plain), $"decorated='{Describe(decorated)}' plain='{Describe(plain)}'");
        }

        /// <summary>
        /// Sweeping the far end of the drag across the line must never skip the span's
        /// content: once a caret is past the span, everything before it has been copied.
        /// </summary>
        [TestCaseSource(nameof(Contents))]
        [RunOnUI]
        public void DragEndingBeyondADecoratedSpan_neverSkipsIt(string inner)
        {
            var ctxt = Build(inner, decorated: true);
            var width = ctxt.DesiredSize.Width;
            var whole = "/" + inner + "/";

            for (double x = 0; x <= width; x += 0.5)
            {
                var selected = SelectByX(ctxt, 0, x);

                Assert.That(whole, Does.StartWith(selected),
                    $"drag 0..{x:F1} of {width:F1} produced '{Describe(selected)}', "
                    + $"which is not a prefix of '{Describe(whole)}'");
            }
        }

        private static string Describe(string s)
        {
            var b = new StringBuilder();
            foreach (var c in s)
                b.Append(c < 128 ? c.ToString() : "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }
}
