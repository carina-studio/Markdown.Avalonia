using Avalonia;
using ColorTextBlock.Avalonia;
using ColorTextBlock.Avalonia.Geometries;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnitTest.Base;
using UnitTest.Base.Utils;

namespace UnitTest.CTxt
{
    /// <summary>
    /// Selection driven by mouse coordinates must respect shaping clusters. An emoji is a
    /// surrogate pair (or a longer ZWJ/flag sequence) that the formatter shapes as one
    /// cluster, usually in a fallback font, so hit-testing inside it returns a *trailing*
    /// CharacterHit whose caret sits at FirstCharacterIndex + TrailingLength.
    ///
    /// The tests below pin the caret's pixel position and its character index together:
    /// the highlight is painted from the pixel position and the copied text is cut with the
    /// index, so whenever the two disagree the user sees an emoji selected but copies the
    /// character in front of it. No pixel values are hard-coded, so the tests hold whatever
    /// font the platform falls back to.
    /// </summary>
    public class UnitTestSelection : UnitTestBase
    {
        private const string Emoji = "\U0001F600";                        // grinning face
        private const string Family = "\U0001F468‍\U0001F469‍\U0001F466"; // ZWJ sequence
        private const string Flag = "\U0001F1EF\U0001F1F5";               // regional indicator pair

        private static readonly object[] Texts =
        {
            "ABCDEF",
            "AB" + Emoji + "CD",
            Emoji,
            Emoji + Emoji,
            "a" + Family + "b",
            "a" + Flag + "b",
            "あい" + Emoji + "う",
        };

        // TextPointer.Distance and .Geometry are internal: a caret's x is Geometry.Left +
        // Distance, which is exactly what CTextBlock.Render uses to paint the highlight.
        private static readonly PropertyInfo DistanceProperty =
            typeof(TextPointer).GetProperty("Distance", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo GeometryProperty =
            typeof(TextPointer).GetProperty("Geometry", BindingFlags.NonPublic | BindingFlags.Instance);

        private static double CaretX(TextPointer pointer)
        {
            var geometry = (CGeometry)GeometryProperty.GetValue(pointer);
            return geometry.Left + (double)DistanceProperty.GetValue(pointer);
        }

        private static CTextBlock Build(string text)
        {
            var ctxt = new CTextBlock();
            ctxt.Content.Add(new CRun() { Text = text });
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

        // CalcuatePointerFrom(int) only accepts indices strictly inside the content; the
        // caret one past the last character is GetEnd().
        private static TextPointer CaretAt(CTextBlock ctxt, string text, int index)
            => index == text.Length ? ctxt.GetEnd() : ctxt.CalcuatePointerFrom(index);

        /// <summary>
        /// An index that shares its caret x with the previous one sits inside a shaping
        /// cluster, where hit-testing legitimately snaps to the cluster's edge.
        /// </summary>
        private static bool IsClusterBoundary(CTextBlock ctxt, string text, int index)
            => index == 0
            || CaretX(CaretAt(ctxt, text, index)) != CaretX(CaretAt(ctxt, text, index - 1));

        /// <summary>
        /// Dragging over the whole line must yield the whole text.
        /// </summary>
        [TestCaseSource(nameof(Texts))]
        [RunOnUI]
        public void WholeLine_selectsEveryCharacter(string text)
        {
            var ctxt = Build(text);
            Assert.That(SelectByX(ctxt, 0, ctxt.DesiredSize.Width), Is.EqualTo(text));
        }

        /// <summary>
        /// Hit-testing at a caret's own x must return that same caret. This is the property
        /// the emoji bug broke: the caret after an emoji reported the right pixel position
        /// but an index two characters short, so the emoji was highlighted and not copied.
        /// </summary>
        [TestCaseSource(nameof(Texts))]
        [RunOnUI]
        public void MouseCaret_agreesWithCharacterCaret(string text)
        {
            var ctxt = Build(text);
            var y = ctxt.DesiredSize.Height / 2;

            for (var index = 0; index <= text.Length; ++index)
            {
                if (!IsClusterBoundary(ctxt, text, index)) continue;

                var x = CaretX(CaretAt(ctxt, text, index));
                var hit = ctxt.CalcuatePointerFrom(x, y);

                Assert.That(hit.Index, Is.EqualTo(index),
                    $"caret for index {index} of '{Describe(text)}' sits at x={x:F1}, "
                    + $"but hit-testing there resolves to index {hit.Index}");
            }
        }

        /// <summary>
        /// Dragging between two carets must copy exactly the text between them, including
        /// when one of them is the far side of an emoji.
        /// </summary>
        [TestCaseSource(nameof(Texts))]
        [RunOnUI]
        public void DragBetweenCarets_copiesExactlyThatRange(string text)
        {
            var ctxt = Build(text);

            for (var from = 0; from <= text.Length; ++from)
            {
                if (!IsClusterBoundary(ctxt, text, from)) continue;
                var fromX = CaretX(CaretAt(ctxt, text, from));

                for (var to = from; to <= text.Length; ++to)
                {
                    if (!IsClusterBoundary(ctxt, text, to)) continue;
                    var toX = CaretX(CaretAt(ctxt, text, to));
                    var expected = text.Substring(from, to - from);

                    Assert.That(SelectByX(ctxt, fromX, toX), Is.EqualTo(expected),
                        $"dragging {from}->{to} over '{Describe(text)}'");
                    Assert.That(SelectByX(ctxt, toX, fromX), Is.EqualTo(expected),
                        $"dragging {to}->{from} over '{Describe(text)}'");
                }
            }
        }

        /// <summary>
        /// No drag may end inside a surrogate pair, which would copy half an emoji as an
        /// unpaired surrogate.
        /// </summary>
        [TestCaseSource(nameof(Texts))]
        [RunOnUI]
        public void Selection_neverSplitsASurrogatePair(string text)
        {
            var ctxt = Build(text);
            var width = ctxt.DesiredSize.Width;

            for (double from = 0; from <= width; from += 1)
            {
                for (double to = from; to <= width; to += 1)
                {
                    var selected = SelectByX(ctxt, from, to);
                    if (selected.Length == 0) continue;

                    Assert.That(Char.IsLowSurrogate(selected[0]), Is.False,
                        $"drag x={from:F0}..{to:F0} starts with a low surrogate: '{Describe(selected)}'");
                    Assert.That(Char.IsHighSurrogate(selected[selected.Length - 1]), Is.False,
                        $"drag x={from:F0}..{to:F0} ends with a high surrogate: '{Describe(selected)}'");
                }
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
