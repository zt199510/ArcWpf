using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class InlineEditTests
{
    [TestMethod]
    public void DisplayEntersEditAndEscapeRestoresValue()
    {
        Sta.Run(() => InWindow(edit =>
        {
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump();
            Assert.IsTrue(edit.IsEditing); editor.Text = "Changed"; RaiseKey(editor, Key.Escape); Sta.Pump();
            Assert.IsFalse(edit.IsEditing); Assert.AreEqual("Original", edit.Value);
        }));
    }

    [TestMethod]
    public void EnterSavesAndReportsSavedPhase()
    {
        Sta.Run(() => InWindow(edit =>
        {
            edit.SaveAsync = value => Task.CompletedTask;
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump(); editor.Text = "Renamed"; RaiseKey(editor, Key.Enter); Pump(80);
            Assert.AreEqual("Renamed", edit.Value); Assert.AreEqual(InlineEditPhase.Saved, edit.Phase);
        }));
    }

    [TestMethod]
    public void ValidationBlocksCommit()
    {
        Sta.Run(() => InWindow(edit =>
        {
            edit.Validate = value => string.IsNullOrWhiteSpace(value) ? "Required" : null;
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump(); editor.Text = " "; RaiseKey(editor, Key.Enter); Sta.Pump();
            Assert.IsTrue(edit.IsEditing); Assert.AreEqual("Required", edit.Error); Assert.AreEqual("Original", edit.Value);
        }));
    }

    [TestMethod]
    public void FrameHugsTextAndFieldCoversDisplay()
    {
        Sta.Run(() => InWindow(edit =>
        {
            var box = (InlineEditBox)edit.Template.FindName("PART_Box", edit)!;
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            var frame = (Border)edit.Template.FindName("PART_Frame", edit)!;
            Assert.AreEqual(display.ActualWidth, frame.ActualWidth, .5);
            Assert.IsTrue(display.ActualWidth < edit.ActualWidth - 60, "the frame hugs the text instead of filling the row");
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump(1000);
            Assert.AreEqual(display.ActualWidth, editor.ActualWidth, .5);
            Assert.AreEqual(display.ActualHeight, editor.ActualHeight, .5);
            Assert.AreEqual(display.ActualWidth, box.FrameWidth, .5, "the frame settles on the field width");
        }));
    }

    [TestMethod]
    public void FailedSaveRollsBackAndOffersRetry()
    {
        Sta.Run(() => InWindow(edit =>
        {
            edit.SaveAsync = value => Task.FromException(new InvalidOperationException("offline"));
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            var host = (Panel)edit.Template.FindName("PART_MessageHost", edit)!;
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump(); editor.Text = "Renamed"; RaiseKey(editor, Key.Enter); Pump(120);
            Assert.AreEqual(InlineEditPhase.Failed, edit.Phase);
            Assert.AreEqual("Original", edit.Value);
            Assert.IsFalse(edit.IsEditing);
            var message = host.Children.OfType<FrameworkElement>().First(child => child is not Canvas);
            var text = message is Panel panel ? panel.Children.OfType<TextBlock>().First() : (TextBlock)message;
            var retry = text.Inlines.OfType<Hyperlink>().Single();
            StringAssert.Contains(new TextRange(text.ContentStart, text.ContentEnd).Text, "Couldn\u2019t save \u201cRenamed\u201d, so the last saved name is back.");
            retry.DoClick(); Sta.Pump();
            Assert.IsTrue(edit.IsEditing);
            Assert.AreEqual("Renamed", editor.Text);
        }));
    }

    [TestMethod]
    public void ExternalValueIsIgnoredWhileEditing()
    {
        Sta.Run(() => InWindow(edit =>
        {
            var display = (System.Windows.Controls.Button)edit.Template.FindName("PART_Display", edit)!;
            var editor = (System.Windows.Controls.TextBox)edit.Template.FindName("PART_Editor", edit)!;
            display.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Sta.Pump(); editor.Text = "Typing";
            edit.Value = "From server"; Sta.Pump();
            Assert.AreEqual("Typing", editor.Text);
            RaiseKey(editor, Key.Escape); Sta.Pump();
            Assert.AreEqual("From server", edit.Value);
        }));
    }

    private static void InWindow(Action<InlineEdit> action)
    {
        var edit = new InlineEdit { Value = "Original", Label = "Name", Width = 220, Height = 48 };
        var window = new Window { Content = edit, Width = 320, Height = 140, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(edit); }
        finally { window.Close(); Sta.Pump(); }
    }
    private static void RaiseKey(UIElement element, Key key) { var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = element }; element.RaiseEvent(args); }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
}
