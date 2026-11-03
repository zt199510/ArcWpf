using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TextColoringTests
{
    [TestMethod] public void LaterSegmentsWinAndRangesAreClamped()
    {
        var brushes = TextColoring.ResolveBrushes(6, [new(1, 3, Brushes.Red), new(2, 1, Brushes.Blue), new(4, 100, Brushes.Green), new(-5, 2, Brushes.Gray), new(50, 2, Brushes.Pink)]);
        CollectionAssert.AreEqual(new Brush?[] { null, Brushes.Red, Brushes.Blue, Brushes.Red, Brushes.Green, Brushes.Green }, brushes);
    }

    [TestMethod] public void EmojiPresentationFollowsTheCatalog()
    {
        Assert.IsTrue(TextColoring.PrefersEmoji("🚀"));
        Assert.IsTrue(TextColoring.PrefersEmoji("✅"));
        Assert.IsTrue(TextColoring.PrefersEmoji("↔️"));
        Assert.IsTrue(TextColoring.PrefersEmoji("1️⃣"));
        Assert.IsFalse(TextColoring.PrefersEmoji("↔"));
        Assert.IsFalse(TextColoring.PrefersEmoji("中"));
    }

    [TestMethod] public void RecognizesRightToLeftText()
    {
        Assert.IsTrue(TextColoring.ContainsRightToLeft("Hello שלום"));
        Assert.IsFalse(TextColoring.ContainsRightToLeft("Hello 中文 🚀"));
    }

    [TestMethod] public void DrawsUntilDetachedAndKeepsTheForegroundUntouched() => Sta.Run(() =>
    {
        var box = new TextBox { Text = "plain", Foreground = Brushes.Teal };
        int calls = 0;
        TextColoring.SetSegmenter(box, _ => { calls++; return []; });
        var window = Show(box);
        Assert.IsTrue(TextColoring.IsDrawing(box));
        Assert.AreEqual(Brushes.Teal, box.Foreground);
        int before = calls;
        box.Text = "changed";
        Assert.AreEqual(before + 1, calls);
        TextColoring.Refresh(box);
        Assert.AreEqual(before + 2, calls);
        var view = (FrameworkElement)((ScrollViewer)box.Template.FindName("PART_ContentHost", box)).Content;
        Assert.AreEqual(0d, view.Opacity);
        TextColoring.SetSegmenter(box, null);
        Assert.IsFalse(TextColoring.IsDrawing(box));
        Assert.AreEqual(1d, view.Opacity);
        box.Text = "again";
        Assert.AreEqual(before + 2, calls);
        window.Close();
    });

    [TestMethod] public void RightToLeftTextStaysNative() => Sta.Run(() =>
    {
        var box = new TextBox { Text = "plain" };
        TextColoring.SetColorEmoji(box, true);
        var window = Show(box);
        Assert.IsTrue(TextColoring.IsDrawing(box));
        box.Text = "שלום";
        Assert.IsFalse(TextColoring.IsDrawing(box));
        box.Text = "plain";
        Assert.IsTrue(TextColoring.IsDrawing(box));
        window.Close();
    });

    [TestMethod] public void TemplateWithoutAScrollHostStaysNative() => Sta.Run(() =>
    {
        var box = new TextBox { Text = "plain", Template = new ControlTemplate(typeof(TextBox)) { VisualTree = new FrameworkElementFactory(typeof(Grid)) } };
        TextColoring.SetColorEmoji(box, true);
        var window = Show(box);
        Assert.IsFalse(TextColoring.IsDrawing(box));
        window.Close();
    });

    [TestMethod] public void WorksInTheLibraryTextControls() => Sta.Run(() =>
    {
        foreach (var box in new TextBox[] { new Input { Text = "plain" }, new Textarea { Text = "plain" }, new MentionInput { Text = "plain" } })
        {
            TextColoring.SetColorEmoji(box, true);
            var window = Show(box);
            Assert.IsTrue(TextColoring.IsDrawing(box), box.GetType().Name);
            window.Close();
        }
    });

    private static Window Show(UIElement content)
    {
        var window = new Window { Content = content, Width = 300, Height = 200, Left = -4000, Top = -4000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        return window;
    }
}
