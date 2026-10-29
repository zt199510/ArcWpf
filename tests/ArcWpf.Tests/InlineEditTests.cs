using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
