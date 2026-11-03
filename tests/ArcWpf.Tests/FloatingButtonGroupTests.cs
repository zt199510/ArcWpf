using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class FloatingButtonGroupTests
{
    private static List<FloatingButtonGroupItem> Items(FloatingButtonGroupItem? share = null) =>
    [
        new() { Id = "undo", Label = "Undo", Icon = "undo-2", IconOnly = true },
        new() { Id = "redo", Label = "Redo", Icon = "redo-2", IconOnly = true, IsDisabled = true },
        new() { IsSeparator = true },
        share ?? new() { Id = "share", Label = "Share", Icon = "link-2", ReserveLabels = ["A much longer word"] },
        new() { Id = "plain", Label = "Plain" },
    ];

    private static void With(Action<FloatingButtonGroup> action, FloatingButtonGroupItem? share = null, Action<FloatingButtonGroup>? setup = null) => Sta.Run(() =>
    {
        var group = new FloatingButtonGroup { Label = "Actions", Items = Items(share), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        setup?.Invoke(group);
        var window = new Window { Content = group, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(group); }
        finally { window.Close(); }
    });

    [TestMethod] public void IconOnlyButtonsAreSquareAndTheTrayFollowsReferenceGeometry() => With(group =>
    {
        var undo = group.ButtonForTest("undo")!; var redo = group.ButtonForTest("redo")!;
        Assert.AreEqual(36d, undo.ActualWidth, .01);
        Assert.AreEqual(36d, undo.ActualHeight, .01);
        Assert.AreEqual(46d, group.ActualHeight, .01);
        Assert.AreEqual(40d, redo.TranslatePoint(new Point(), group).X - undo.TranslatePoint(new Point(), group).X, .01);
        Assert.AreEqual(5d, undo.TranslatePoint(new Point(), group).X, .01);
        // Reference: redo ends at 81, the separator sits at 87 and the next button starts at 94.
        Assert.AreEqual(94d, group.ButtonForTest("share")!.TranslatePoint(new Point(), group).X, .01);
    });

    [TestMethod] public void SmallSizeUsesItsOwnMetrics() => With(group =>
    {
        Assert.AreEqual(30d, group.ButtonForTest("undo")!.ActualHeight, .01);
        Assert.AreEqual(38d, group.ActualHeight, .01);
    }, setup: g => g.Size = FloatingButtonGroupSize.Small);

    [TestMethod] public void HighlightLandsOnHoveredButtonAndFadesWhenThePointerLeaves() => With(group =>
    {
        group.HoverForTest("undo");
        Sta.Pump();
        Assert.AreEqual("undo", group.ActiveId);
        var rect = group.HighlightTargetForTest;
        Assert.AreEqual(36d, rect.Width, .01);
        Assert.AreEqual(0d, rect.X, .01);

        group.HoverForTest("share");
        group.FinishMotionForTest();
        Sta.Pump();
        var share = group.ButtonForTest("share")!;
        Assert.AreEqual(share.ActualWidth, group.HighlightTargetForTest.Width, .01);

        group.HoverForTest(null);
        Assert.IsNull(group.ActiveId);
    });

    [TestMethod] public void DisabledItemUnderThePointerGetsNoHighlightAndDoesNotInvoke() => With(group =>
    {
        string? invoked = null;
        group.ItemInvoked += (_, id) => invoked = id;
        group.HoverForTest("redo");
        Assert.IsNull(group.ActiveId);
        group.ButtonForTest("redo")!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        group.ButtonForTest("undo")!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual("undo", invoked);
    });

    [TestMethod] public void ReservedLabelsHoldTheWidthSoALabelChangeMovesNothing() => Run(share =>
    {
        With(group =>
        {
            var button = group.ButtonForTest("share")!;
            double width = button.ActualWidth, next = group.ButtonForTest("plain")!.TranslatePoint(new Point(), group).X;
            share.Label = "Copied";
            share.Icon = "check";
            Sta.Pump();
            Assert.AreEqual(width, button.ActualWidth, .01);
            Assert.AreEqual(next, group.ButtonForTest("plain")!.TranslatePoint(new Point(), group).X, .01);
        }, share);
    });

    private static void Run(Action<FloatingButtonGroupItem> body) => body(new FloatingButtonGroupItem { Id = "share", Label = "Share", Icon = "link-2", ReserveLabels = ["Copied"] });

    [TestMethod] public void ToolbarKeysSkipDisabledItemsAndWrap()
    {
        bool[] usable = [true, false, true, true];
        Assert.AreEqual(2, ToolbarKeys.Next(usable, 0, Key.Right, false));
        Assert.AreEqual(0, ToolbarKeys.Next(usable, 2, Key.Left, false));
        Assert.AreEqual(0, ToolbarKeys.Next(usable, 3, Key.Right, false));
        Assert.AreEqual(3, ToolbarKeys.Next(usable, 0, Key.End, false));
        Assert.AreEqual(0, ToolbarKeys.Next(usable, 3, Key.Home, false));
        Assert.AreEqual(2, ToolbarKeys.Next(usable, 0, Key.Down, true));
        Assert.AreEqual(-1, ToolbarKeys.Next(usable, 0, Key.Down, false));
    }

    [TestMethod] public void OnlyTheFirstEnabledItemIsATabStop() => With(group =>
    {
        Assert.AreEqual("undo", group.TabStopId);
        Assert.IsTrue(group.ButtonForTest("undo")!.IsTabStop);
        Assert.IsFalse(group.ButtonForTest("share")!.IsTabStop);
    });

    [TestMethod] public void AutomationSeesAnInertItemAsDisabled() => With(group =>
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(group.ButtonForTest("redo")!);
        Assert.IsFalse(peer.IsEnabled());
        Assert.AreEqual("Redo", peer.GetName());
    });
}
