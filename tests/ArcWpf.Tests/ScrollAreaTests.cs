using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ScrollAreaTests
{
    [TestMethod] public void ScrollSettingsAreBindable() => Sta.Run(() => { var area = new ScrollArea { VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false }; Assert.AreEqual(ScrollBarVisibility.Visible, area.VerticalScrollBarVisibility); Assert.IsFalse(area.CanContentScroll); });
    [TestMethod] public void ContentIsPreserved() => Sta.Run(() => { var text = new TextBlock { Text = "content" }; var area = new ScrollArea { Content = text }; Assert.AreSame(text, area.Content); });
    [TestMethod] public void DefaultVerticalScrollingIsAuto() => Sta.Run(() => Assert.AreEqual(ScrollBarVisibility.Auto, new ScrollArea().VerticalScrollBarVisibility));
}
