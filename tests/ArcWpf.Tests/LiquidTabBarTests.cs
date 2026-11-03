using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class LiquidTabBarTests
{
    private static void With(Action<LiquidTabBar> action) => Sta.Run(() =>
    {
        var bar = new LiquidTabBar
        {
            Label = "Sections", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Tabs =
            [
                new() { Id = "home", Text = "Home", Icon = "house" },
                new() { Id = "saved", Text = "Saved", Icon = "bookmark" },
                new() { Id = "inbox", Text = "Inbox", Icon = "message-circle", Count = 2 },
                new() { Id = "profile", Text = "Profile", Icon = "user" },
            ],
        };
        var window = new Window { Content = bar, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(bar); }
        finally { window.Close(); }
    });

    [TestMethod] public void WidthsRoundAndMakeRoomForACount()
    {
        Assert.AreEqual(91d, LiquidTabMath.TabWidth(37.45, false));
        Assert.AreEqual(96d, LiquidTabMath.TabWidth(36.31, true));
        Assert.AreEqual(237d, LiquidTabMath.BarWidth([91, 44, 44, 44]));
    }

    [TestMethod] public void FirstTabIsSelectedAndTheLensCoversIt() => With(bar =>
    {
        Assert.AreEqual("home", bar.SelectedId);
        double wide = bar.TabWidthForTest("home");
        Assert.IsGreaterThan(44d, wide);
        Assert.AreEqual(44d, bar.TabWidthForTest("saved"), .01);
        var lens = bar.LensForTest;
        Assert.AreEqual(4d, lens.X, .01);
        Assert.AreEqual(4d, lens.Y, .01);
        Assert.AreEqual(40d, lens.Height, .01);
        Assert.AreEqual(wide, lens.Width, .01);
        Assert.AreEqual(1d, bar.LensLabelOpacityForTest("home"), .01);
        Assert.AreEqual(0d, bar.LensLabelOpacityForTest("saved"), .01);
    });

    [TestMethod] public void SelectingMovesTheLensToTheNewTabAndRaisesTheEvent() => With(bar =>
    {
        string? raised = null;
        bar.SelectionChanged += (_, id) => raised = id;
        bar.SelectedId = "inbox";
        bar.FinishMotionForTest();
        Sta.Pump();
        Assert.AreEqual("inbox", raised);
        Assert.AreEqual(44d, bar.TabWidthForTest("home"), .01);
        // Reference: with Inbox selected, the tabs before it are 44 wide with a gap of 2, so it starts at 96.
        Assert.AreEqual(96d, bar.TabLeftForTest("inbox"), .01);
        Assert.AreEqual(96d, bar.LensForTest.X, .01);
        Assert.AreEqual(bar.TabWidthForTest("inbox"), bar.LensForTest.Width, .01);
        Assert.IsTrue(bar.HitForTest("inbox").IsChecked);
        Assert.IsFalse(bar.HitForTest("home").IsChecked);
    });

    [TestMethod] public void CheckingATabTheWayAutomationDoesSelectsIt() => With(bar =>
    {
        bar.HitForTest("saved").IsChecked = true;
        Assert.AreEqual("saved", bar.SelectedId);
    });

    [TestMethod] public void TheLensLeansTowardTheHoveredSideOnly() => With(bar =>
    {
        bar.SelectedId = "inbox";
        bar.FinishMotionForTest();
        var rest = bar.LensForTest;
        bar.HoverForTest("home");
        bar.FinishMotionForTest();
        Assert.AreEqual(rest.Left - 6.44, bar.LensForTest.Left, .01);
        Assert.AreEqual(rest.Right, bar.LensForTest.Right, .01);
        bar.HoverForTest("profile");
        bar.FinishMotionForTest();
        Assert.AreEqual(rest.Left, bar.LensForTest.Left, .01);
        Assert.AreEqual(rest.Right + 6.44, bar.LensForTest.Right, .01);
        bar.HoverForTest("inbox");
        bar.FinishMotionForTest();
        Assert.AreEqual(rest.Width, bar.LensForTest.Width, .01);
    });

    [TestMethod] public void LabelModesChangeTheRow() => With(bar =>
    {
        bar.ShowLabels = LiquidTabLabels.None;
        bar.FinishMotionForTest();
        Sta.Pump();
        foreach (var id in new[] { "home", "saved", "inbox", "profile" }) { Assert.AreEqual(44d, bar.TabWidthForTest(id), .01); }
        Assert.AreEqual(44d, bar.LensForTest.Width, .01);
        Assert.AreEqual(190d, bar.BarWidthForTest, .01);

        bar.ShowLabels = LiquidTabLabels.All;
        bar.FinishMotionForTest();
        Sta.Pump();
        foreach (var id in new[] { "home", "saved", "inbox", "profile" }) { Assert.IsGreaterThan(44d, bar.TabWidthForTest(id)); }
        Assert.AreEqual(1d, bar.LensLabelOpacityForTest("saved"), .01);
    });

    [TestMethod] public void TheCountIsPartOfTheAccessibleName() => With(bar => Assert.AreEqual("Inbox, 2 unread", System.Windows.Automation.AutomationProperties.GetName(bar.HitForTest("inbox"))));
}
