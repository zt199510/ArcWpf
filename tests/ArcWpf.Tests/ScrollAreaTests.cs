using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class ScrollAreaTests
{
    [TestMethod]
    public void DefaultsFollowTheSource() => Sta.Run(() =>
    {
        var area = new ScrollArea();
        Assert.AreEqual(ScrollAreaOrientation.Vertical, area.Orientation);
        Assert.AreEqual(28, area.Fade);
        Assert.AreEqual(ScrollAreaScrollbars.Auto, area.Scrollbars);
        Assert.AreEqual(TimeSpan.FromMilliseconds(900), area.HideDelay);
        Assert.IsTrue(area.WheelToHorizontal);
    });

    [TestMethod]
    public void EdgesTrackOverflowAndTheThumbKeepsItsMinimumLength() => Sta.Run(() =>
    {
        var area = new ScrollArea { Height = 100, Content = new Border { Height = 5000 } };
        var edges = new List<ScrollAreaEdges>(); area.EdgesChanged += (_, e) => edges.Add(e.Edges);
        var window = new Window { Content = area, Width = 200, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.AreEqual(new ScrollAreaEdges(false, true, false, false), area.Edges);
            var thumb = (FrameworkElement)area.Template.FindName("PART_ThumbY", area)!;
            Assert.AreEqual(28, thumb.Height, .01); // 94 * 100 / 5000 is below MIN_THUMB
            area.Viewport!.ScrollToEnd(); Sta.Pump();
            Assert.AreEqual(new ScrollAreaEdges(true, false, false, false), area.Edges);
            Assert.HasCount(2, edges);
        }
        finally { window.Close(); Sta.Pump(); }
    });

    [TestMethod]
    public void HorizontalOrientationDisablesVerticalScrolling() => Sta.Run(() =>
    {
        var area = new ScrollArea { Orientation = ScrollAreaOrientation.Horizontal };
        area.ApplyTemplate();
        Assert.AreEqual(ScrollBarVisibility.Disabled, area.Viewport!.VerticalScrollBarVisibility);
        Assert.AreEqual(ScrollBarVisibility.Hidden, area.Viewport.HorizontalScrollBarVisibility);
    });
}
