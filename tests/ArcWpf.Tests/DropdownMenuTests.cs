using System.Windows;
using ArcWpf.Controls;
using ArcMenuItem = ArcWpf.Controls.MenuItem;
namespace ArcWpf.Tests;

[TestClass] public sealed class DropdownMenuTests
{
    private static void With(Action<DropdownMenu> action) => Sta.Run(() =>
    {
        var menu = new DropdownMenu { Label = "Project actions", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        menu.Items.Add(new ArcMenuItem { Label = "Edit project" });
        menu.Items.Add(new ArcMenuItem { Label = "Download brief" });
        menu.Items.Add(new ArcMenuItem { Label = "Archive", SeparatorBefore = true });
        var window = new Window { Content = menu, Width = 600, Height = 500, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(menu); }
        finally { menu.IsOpen = false; window.Close(); }
    });

    [TestMethod] public void IsAMenu() => With(menu => Assert.IsInstanceOfType<Menu>(menu));

    [TestMethod] public void HoverMovesTheHighlightToThatItem() => With(menu =>
    {
        menu.IsOpen = true;
        Sta.Pump();
        menu.HoverItemForTest(1);
        Sta.Pump();
        Assert.AreEqual(1, menu.HighlightedIndexForTest);
    });

    [TestMethod] public void ClosingKeepsTheTrigger() => With(menu =>
    {
        menu.IsOpen = true;
        Sta.Pump();
        menu.IsOpen = false;
        Sta.Pump();
        Assert.IsFalse(menu.IsOpen);
        Assert.IsGreaterThan(0d, menu.TriggerWidthForTest);
    });
}
