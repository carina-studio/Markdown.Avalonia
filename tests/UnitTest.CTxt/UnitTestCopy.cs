using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Markdown.Avalonia;
using NUnit.Framework;
using System;
using UnitTest.Base;
using UnitTest.Base.Utils;

namespace UnitTest.CTxt
{
    /// <summary>
    /// Copying a selection with the platform shortcut only works while the viewer can take
    /// keyboard focus: the pointer press calls Focus(), and OnKeyDown is what turns
    /// Ctrl/Cmd+C into a clipboard write. SelectionEnabled defaults to true but only its
    /// setter keeps Focusable in step, so a viewer that never assigns the property used to
    /// select text happily and then ignore the copy shortcut.
    /// </summary>
    public class UnitTestCopy : UnitTestBase
    {
        private const string Markdown = "hello \U0001F600 world";

        private static RawInputModifiers CopyModifier
            => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        private static (Window Window, MarkdownScrollViewer Viewer) Show(bool? selectionEnabled)
        {
            var viewer = new MarkdownScrollViewer { Markdown = Markdown };
            if (selectionEnabled.HasValue)
                viewer.SelectionEnabled = selectionEnabled.Value;

            var window = new Window { Width = 400, Height = 200, Content = viewer };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return (window, viewer);
        }

        private static void DragAcrossFirstLine(Window window)
        {
            window.MouseDown(new Point(5, 20), MouseButton.Left);
            window.MouseMove(new Point(300, 20));
            window.MouseUp(new Point(300, 20), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        private static string ClipboardText(Window window)
        {
            var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
            if (clipboard is null) return null;

            var task = clipboard.TryGetTextAsync();
            task.Wait(TimeSpan.FromSeconds(5));
            return task.Result;
        }

        /// <summary>
        /// SelectionEnabled is true by default, so the viewer must be focusable out of the
        /// box - otherwise Focus() on pointer-press is a no-op and no key ever arrives.
        /// </summary>
        [Test]
        [RunOnUI]
        public void ViewerIsFocusable_whenSelectionEnabledIsLeftAtItsDefault()
        {
            var (_, viewer) = Show(selectionEnabled: null);

            Assert.That(viewer.SelectionEnabled, Is.True);
            Assert.That(viewer.Focusable, Is.True);
        }

        [Test]
        [RunOnUI]
        public void ViewerIsNotFocusable_whenSelectionIsDisabled()
        {
            var (_, viewer) = Show(selectionEnabled: false);

            Assert.That(viewer.Focusable, Is.False);
        }

        /// <summary>
        /// The end-to-end path a user takes: drag to select, then press the platform's copy
        /// shortcut. Run with SelectionEnabled left at its default, which is how a viewer
        /// declared in XAML without the attribute behaves.
        /// </summary>
        [Test]
        [RunOnUI]
        public void CopyShortcut_putsTheSelectionOnTheClipboard()
        {
            var (window, viewer) = Show(selectionEnabled: null);

            DragAcrossFirstLine(window);
            Assert.That(viewer.SelectedText, Is.Not.Null.And.Not.Empty, "drag did not select anything");
            Assert.That(viewer.IsFocused, Is.True, "pointer press did not move focus to the viewer");

            window.KeyPressQwerty(PhysicalKey.C, CopyModifier);
            Dispatcher.UIThread.RunJobs();

            Assert.That(ClipboardText(window), Is.EqualTo(viewer.SelectedText));
        }

        /// <summary>
        /// An emoji in the selection must reach the clipboard whole.
        /// </summary>
        [Test]
        [RunOnUI]
        public void CopyShortcut_carriesEmojiThrough()
        {
            var (window, viewer) = Show(selectionEnabled: null);

            DragAcrossFirstLine(window);
            window.KeyPressQwerty(PhysicalKey.C, CopyModifier);
            Dispatcher.UIThread.RunJobs();

            Assert.That(ClipboardText(window), Does.Contain("\U0001F600"));
        }

        [Test]
        [RunOnUI]
        public void CopyShortcut_isIgnored_whenSelectionIsDisabled()
        {
            var (window, viewer) = Show(selectionEnabled: false);

            DragAcrossFirstLine(window);
            window.KeyPressQwerty(PhysicalKey.C, CopyModifier);
            Dispatcher.UIThread.RunJobs();

            Assert.That(viewer.SelectedText, Is.Null.Or.Empty);
            Assert.That(ClipboardText(window), Is.Null.Or.Empty);
        }
    }
}
