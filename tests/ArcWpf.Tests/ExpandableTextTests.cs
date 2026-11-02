using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class ExpandableTextTests
{
    private static readonly string Long = string.Concat(Enumerable.Repeat("Long words wrap across several lines of this paragraph. ", 24));
    private static ExpandableText Text(string content, int lines = 3)
    {
        var c = new ExpandableText { Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap }, Lines = lines, Width = 320 };
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        return c;
    }
    private static void Shown(FrameworkElement content, Action body)
    {
        var window = new Window { Width = 500, Height = 900, Content = new StackPanel { Children = { content } }, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }

    [TestMethod]
    public void ExpandableTextTogglesState() => Sta.Run(() =>
    {
        var c = new ExpandableText { Content = new TextBlock { Text = "Long text" }, Lines = 2 };
        c.IsExpanded = true;
        Assert.IsTrue(c.IsExpanded);
        c.IsExpanded = false;
        Assert.IsFalse(c.IsExpanded);
    });

    [TestMethod]
    public void LabelsAreConfigurable() => Sta.Run(() =>
    {
        var c = new ExpandableText { MoreLabel = "展开", LessLabel = "收起" };
        Assert.AreEqual("展开", c.MoreLabel);
        Assert.AreEqual("收起", c.LessLabel);
    });

    [TestMethod]
    public void ClampsToLinesAndShowsToggleWhenOverflowing() => Sta.Run(() =>
    {
        var c = Text(Long);
        Shown(c, () =>
        {
            var content = (FrameworkElement)c.Template.FindName("PART_Content", c);
            var toggle = (FrameworkElement)c.Template.FindName("PART_Toggle", c);
            Assert.IsTrue(c.IsOverflowing);
            Assert.AreEqual(58.8, content.ActualHeight, .6, "3 x 19.6");
            Assert.AreEqual(Visibility.Visible, toggle.Visibility);
            Assert.AreEqual(28, toggle.ActualHeight);
            Assert.AreEqual(8, toggle.TransformToAncestor(c).Transform(new Point()).Y - content.ActualHeight, .6, "gap of 8");
            Assert.AreEqual(-2, toggle.TransformToAncestor(c).Transform(new Point()).X);
            Assert.IsNotNull(content.OpacityMask, "the last line fades");
        });
    });

    [TestMethod]
    public void ShortTextHasNoToggleOrMask() => Sta.Run(() =>
    {
        var c = Text("Well packed.");
        Shown(c, () =>
        {
            var content = (FrameworkElement)c.Template.FindName("PART_Content", c);
            var toggle = (FrameworkElement)c.Template.FindName("PART_Toggle", c);
            Assert.IsFalse(c.IsOverflowing);
            Assert.AreEqual(Visibility.Collapsed, toggle.Visibility);
            Assert.IsNull(content.OpacityMask);
        });
    });

    [TestMethod]
    public void ExpandingShowsFullHeightAndRemovesTheFade() => Sta.Run(() =>
    {
        var c = Text(Long);
        Shown(c, () =>
        {
            var content = (FrameworkElement)c.Template.FindName("PART_Content", c);
            double clamped = content.ActualHeight;
            c.IsExpanded = true;
            c.UpdateLayout();
            Assert.IsTrue(content.ActualHeight > clamped * 2);
            Assert.IsNull(content.OpacityMask);
            var chevron = (FrameworkElement)c.Template.FindName("PART_Chevron", c);
            Assert.AreEqual(180, ((RotateTransform)chevron.RenderTransform).Angle);
            c.IsExpanded = false;
            c.UpdateLayout();
            Assert.AreEqual(clamped, content.ActualHeight, .6);
            Assert.IsNotNull(content.OpacityMask);
        });
    });

    [TestMethod]
    public void WidthChangeUpdatesOverflow() => Sta.Run(() =>
    {
        var c = Text("Some words that fit on three lines when the column is wide enough to hold them.");
        Shown(c, () =>
        {
            Assert.IsFalse(c.IsOverflowing);
            c.Width = 60;
            c.UpdateLayout();
            Assert.IsTrue(c.IsOverflowing);
        });
    });

    [TestMethod]
    public void PlainStringContentWraps() => Sta.Run(() =>
    {
        var c = new ExpandableText { Content = Long, Width = 320 };
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        Shown(c, () => Assert.IsTrue(c.IsOverflowing));
    });
}
