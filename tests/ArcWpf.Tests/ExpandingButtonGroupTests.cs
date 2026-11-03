using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class ExpandingButtonGroupTests
{
    private static void With(Action<ExpandingButtonGroup> action, Func<Task<bool>>? run = null, string? rest = null) => Sta.Run(() =>
    {
        var group = new ExpandingButtonGroup
        {
            Label = "Actions", ConfirmDuration = TimeSpan.FromMilliseconds(60), DefaultExpanded = rest, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Items =
            [
                new() { Id = "off", Label = "Off", Icon = "pin", IsDisabled = true },
                new() { Id = "reply", Label = "Reply", DoneLabel = "Replied", Icon = "reply", Run = run },
                new() { Id = "forward", Label = "Forward", Icon = "forward" },
                new() { Id = "delete", Label = "Delete", Icon = "trash-2", Tone = ExpandingButtonGroupTone.Danger },
            ],
        };
        var window = new Window { Content = group, Width = 700, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(group); }
        finally { window.Close(); }
    });

    private static void Wait(Func<bool> done, int milliseconds = 1500)
    {
        var until = Environment.TickCount64 + milliseconds;
        while (!done() && Environment.TickCount64 < until) { Sta.Pump(); }
    }

    [TestMethod] public void FirstEnabledItemRestsExpandedAtTheWidthOfItsLongerWord() => With(group =>
    {
        Assert.AreEqual("reply", group.ExpandedId);
        Assert.AreEqual(36d, group.ItemWidthForTest("off"), .01);
        // Reference: start 10 + icon 16 + gap 7 + word + end 14; the longer of Reply and Replied holds the width.
        double width = group.ItemWidthForTest("reply");
        Assert.IsGreaterThan(36d + 30, width);
        Assert.AreEqual(44d, group.ActualHeight, .01);
    });

    [TestMethod] public void DefaultExpandedWins() => With(group => Assert.AreEqual("forward", group.ExpandedId), rest: "forward");

    [TestMethod] public void PointingMovesTheExpansionAndLeavingReturnsIt() => With(group =>
    {
        group.PointerAtForTest("forward", 18);
        Assert.AreEqual("forward", group.ExpandedId);
        group.FinishMotionForTest();
        Assert.IsGreaterThan(36d, group.ItemWidthForTest("forward"));
        Assert.AreEqual(36d, group.ItemWidthForTest("reply"), .01);
        group.PointerLeaveForTest();
        Assert.AreEqual("reply", group.ExpandedId);
    });

    [TestMethod] public void HysteresisHoldsAtTheBoundaryBetweenActions() => With(group =>
    {
        group.PointerAtForTest("forward", 18);
        group.PointerAtForTest("delete", 2);
        Assert.AreEqual("forward", group.ExpandedId);
        group.PointerAtForTest("delete", 18);
        Assert.AreEqual("delete", group.ExpandedId);
    });

    [TestMethod] public void ActionConfirmsInPlaceWithoutChangingTheWidthThenCollapsesBack() => With(group =>
    {
        double width = group.ItemWidthTargetForTest("reply");
        group.ButtonForTest("reply").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Wait(() => group.DoneIdForTest == "reply");
        Assert.AreEqual("reply", group.DoneIdForTest);
        Assert.AreEqual("Replied", group.WordForTest("reply"));
        Assert.AreEqual(width, group.ItemWidthTargetForTest("reply"), .01);
        Wait(() => group.DoneIdForTest is null);
        Assert.IsNull(group.DoneIdForTest);
        Assert.AreEqual("Reply", group.WordForTest("reply"));
    });

    [TestMethod] public void ReturningFalseSkipsTheConfirmation() => With(group =>
    {
        group.ButtonForTest("reply").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsNull(group.DoneIdForTest);
    }, () => Task.FromResult(false));

    [TestMethod] public void AFaultedActionSkipsTheConfirmation() => With(group =>
    {
        group.ButtonForTest("reply").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsNull(group.DoneIdForTest);
        Assert.IsNull(group.BusyIdForTest);
    }, () => Task.FromException<bool>(new InvalidOperationException()));

    [TestMethod] public void APendingActionKeepsTheButtonBusyUntilItEnds()
    {
        var gate = new TaskCompletionSource<bool>();
        With(group =>
        {
            group.ButtonForTest("reply").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.AreEqual("reply", group.BusyIdForTest);
            gate.SetResult(true);
            Wait(() => group.DoneIdForTest == "reply");
            Assert.IsNull(group.BusyIdForTest);
            Assert.AreEqual("reply", group.DoneIdForTest);
        }, () => gate.Task);
    }

    [TestMethod] public void OnTouchTheFirstTapRevealsAndTheSecondRuns() => With(group =>
    {
        string? ran = null;
        group.ItemInvoked += (_, id) => ran = id;
        group.TapForTest("forward");
        Assert.AreEqual("forward", group.ExpandedId);
        Assert.IsNull(ran);
        group.TapForTest("forward");
        Assert.AreEqual("forward", ran);
    });

    [TestMethod] public void ADisabledItemRevealsButDoesNotRun() => With(group =>
    {
        string? ran = null;
        group.ItemInvoked += (_, id) => ran = id;
        group.PointerAtForTest("off", 18);
        Assert.AreEqual("off", group.ExpandedId);
        group.ButtonForTest("off").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.IsNull(ran);
    });
}
