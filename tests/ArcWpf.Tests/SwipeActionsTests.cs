using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SwipeActionsTests
{
    [TestMethod]
    public void FullSwipeRunsTheOutermostTrailingAction() => Sta.Run(() =>
    {
        string? ran = null;
        var row = new SwipeActionsRow { Width = 300 };
        row.Trailing.Add(new SwipeAction { Label = "Snooze", OnSelect = () => ran = "snooze" });
        row.Trailing.Add(new SwipeAction { Label = "Delete", OnSelect = () => ran = "delete" });
        row.CommitForTest(-240);
        Assert.AreEqual("delete", ran, "two actions arm past max(152 + 48, 56% of 300)");
    });

    [TestMethod]
    public void CloseReturnsRowHome() => Sta.Run(() =>
    {
        var row = new SwipeActionsRow();
        row.CommitForTest(-50);
        row.Close();
        Assert.AreEqual(0d, row.OffsetForTest);
    });

    [TestMethod]
    public void ReleaseStopsOpenPastHalfAndReturnsBelowIt() => Sta.Run(() =>
    {
        var row = new SwipeActionsRow { Width = 400 };
        row.Trailing.Add(new SwipeAction { Label = "Snooze" });
        row.Trailing.Add(new SwipeAction { Label = "Delete" });
        row.SwipeForTest(-90, 0);
        Assert.AreEqual(-152d, row.OffsetForTest, .01, "two actions rest 76 wide each");
        row.Close();
        row.SwipeForTest(-60, 0);
        Assert.AreEqual(0d, row.OffsetForTest, .01);
    });

    [TestMethod]
    public void ReleaseVelocityProjectsTheStop() => Sta.Run(() =>
    {
        var row = new SwipeActionsRow { Width = 400 };
        row.Leading.Add(new SwipeAction { Label = "Archive" });
        row.SwipeForTest(20, 400);
        Assert.AreEqual(76d, row.OffsetForTest, .01, "20 plus 40 of projected travel passes half of 76");
    });

    [TestMethod]
    public void FlickBackCancelsAnArmedSwipe() => Sta.Run(() =>
    {
        bool ran = false;
        var row = new SwipeActionsRow { Width = 300 };
        row.Trailing.Add(new SwipeAction { Label = "Delete", OnSelect = () => ran = true });
        row.SwipeForTest(-200, 1200);
        Assert.IsFalse(ran);
    });

    [TestMethod]
    public void WithoutFullSwipeTheRowStopsAtItsActions() => Sta.Run(() =>
    {
        bool ran = false;
        var row = new SwipeActionsRow { Width = 300, FullSwipe = false };
        row.Trailing.Add(new SwipeAction { Label = "Delete", OnSelect = () => ran = true });
        row.CommitForTest(-250);
        Assert.IsFalse(ran);
        Assert.AreEqual(-76d, row.OffsetForTest, .01);
    });

    [TestMethod]
    public void OnlyOneRowStaysOpenAndCollapseHidesTheRowUntilRestore() => Sta.Run(() =>
    {
        var list = new SwipeActions { Width = 440 };
        var first = new SwipeActionsRow { RowLabel = "a", Content = "a" };
        var second = new SwipeActionsRow { RowLabel = "b", Content = "b" };
        first.Trailing.Add(new SwipeAction { Label = "Delete" });
        second.Trailing.Add(new SwipeAction { Label = "Delete", OnSelect = () => second.Collapse() });
        list.Items.Add(first);
        list.Items.Add(second);
        var window = new Window { Content = list, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); list.UpdateLayout();
            first.SwipeForTest(-60, 0);
            Assert.AreSame(first, list.OpenRow);
            second.SwipeForTest(-60, 0);
            Assert.AreSame(second, list.OpenRow);
            for (int i = 0; i < 80; i++) { first.AdvanceMotion(.05); }
            Assert.AreEqual(0d, first.OffsetForTest, .01, "opening a row sends the other home");
            second.Close();
            second.CommitForTest(-400);
            Assert.IsTrue(second.IsLeavingOrGone);
            second.Restore();
            Assert.IsFalse(second.IsLeavingOrGone);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void KeepRowActionRunsAndTheRowComesHome() => Sta.Run(() =>
    {
        int ran = 0;
        var row = new SwipeActionsRow { Width = 400 };
        row.Leading.Add(new SwipeAction { Label = "Mark", KeepRow = true, OnSelect = () => ran++ });
        row.CommitForTest(300);
        Assert.AreEqual(1, ran);
        Assert.IsFalse(row.IsLeavingOrGone);
        Assert.AreEqual(0d, row.OffsetForTest, .01);
    });

    [TestMethod]
    public void VelocityReadsOnlyTheLastEightyMilliseconds() => Sta.Run(() =>
    {
        var samples = new List<(long Time, double X)> { (0, 0), (500, 10), (520, 30), (580, 90) };
        Assert.AreEqual(1000d, SwipeActionsRow.VelocityOf(samples), .01, "the old sample at 0 ms must not dilute the flick");
    });

    [TestMethod]
    public void PastTheLastStopTheRowFollowsWithResistance() => Sta.Run(() =>
    {
        var row = new SwipeActionsRow { Width = 300, FullSwipe = false };
        bool ran = false;
        row.Trailing.Add(new SwipeAction { Label = "Delete", OnSelect = () => ran = true });
        row.CommitForTest(-76);
        Assert.AreEqual(-76d, row.OffsetForTest, .01);
        Assert.IsFalse(ran);
    });

    [TestMethod]
    public void RevealedLayersShareTheSpaceEvenly() => Sta.Run(() =>
    {
        var row = new SwipeActionsRow { RowLabel = "a", Content = "a" };
        row.Trailing.Add(new SwipeAction { Label = "Snooze", Icon = "clock-3" });
        row.Trailing.Add(new SwipeAction { Label = "Delete", Icon = "trash" });
        var list = new SwipeActions { Width = 440 };
        list.Items.Add(row);
        var window = new Window { Content = list, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); list.UpdateLayout();
            row.SwipeForTest(-90, 0);
            for (int i = 0; i < 80; i++) { row.AdvanceMotion(.05); }
            Assert.AreEqual(-152d, row.OffsetForTest, .01);
            Assert.AreEqual(-152d, row.Layers[0].Shift, .01, "rank 0 sits against the content");
            Assert.AreEqual(-76d, row.Layers[1].Shift, .01, "the highest rank holds the outer edge");
            Assert.AreEqual(1d, row.Layers[0].IconReveal, .01);
            Assert.AreEqual(1d, row.Layers[1].LabelReveal, .01);
        }
        finally { window.Close(); }
    });
}
