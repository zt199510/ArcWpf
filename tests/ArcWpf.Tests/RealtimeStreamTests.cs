using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class RealtimeStreamTests
{
    private static readonly DateTime Start = new(2026, 11, 3, 10, 0, 0);

    private static ObservableCollection<StreamPoint> Feed(int count) => [.. Enumerable.Range(0, count).Select(i => new StreamPoint(Start.AddMilliseconds(500 * i), 130 + i % 7, i == count - 10 ? "Spike" : null))];

    private static void Run(Action<RealtimeStream, ObservableCollection<StreamPoint>> action, int count = 80)
    {
        Sta.Run(() =>
        {
            var points = Feed(count);
            var chart = new RealtimeStream { Label = "Checkout API", Unit = "ms", Interval = TimeSpan.FromMilliseconds(500), Window = TimeSpan.FromSeconds(30), Points = points, Width = 473 };
            var window = new Window { Content = chart, Width = 700, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart, points); }
            finally { window.Close(); }
        });
    }

    private static void Add(ObservableCollection<StreamPoint> points, double value = 140) => points.Add(new StreamPoint(points[^1].Time.AddMilliseconds(500), value));

    [TestMethod]
    public void TheViewTrailsTheNewestReadingByOneInterval() => Run((chart, points) =>
    {
        Assert.AreEqual(chart.NewestForTest - 500, chart.HeadForTest, 40);
        Add(points);
        double before = chart.HeadForTest;
        for (int i = 0; i < 15; i++) { chart.AdvanceForTest(1d / 60); }
        // Once more than one and a half intervals behind, the head runs at 1.5x to catch up.
        Assert.IsGreaterThan(before + 240, chart.HeadForTest);
        Assert.IsLessThan(before + 390, chart.HeadForTest);
    });

    [TestMethod]
    public void HoverHoldsTheViewCountsWhatArrivesAndCatchesUpOnLeave() => Run((chart, points) =>
    {
        chart.HoverForTest(200);
        Assert.IsTrue(chart.HeldForTest);
        Assert.IsTrue(chart.TooltipShownForTest);
        double held = chart.HeadForTest;
        Add(points);
        Add(points);
        chart.AdvanceForTest(.5);
        Assert.AreEqual(held, chart.HeadForTest);
        Assert.AreEqual(2, chart.QueuedForTest);
        Assert.AreEqual("Paused, 2 new", chart.StatusForTest);
        chart.LeaveForTest();
        for (int i = 0; i < 120; i++) { chart.AdvanceForTest(1d / 60); }
        Assert.IsFalse(chart.HeldForTest);
        Assert.AreEqual(0, chart.QueuedForTest);
        Assert.AreEqual(chart.NewestForTest, chart.HeadForTest, 520);
        Assert.AreEqual("Live", chart.StatusForTest);
    });

    [TestMethod]
    public void TheStatusButtonPausesThroughAutomation() => Run((chart, points) =>
    {
        bool? raised = null;
        chart.PausedChanged += (_, e) => raised = e.Paused;
        var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(chart.StatusButtonForTest).GetPattern(PatternInterface.Toggle);
        toggle.Toggle();
        Assert.IsTrue(chart.Paused);
        Assert.IsTrue(raised);
        Assert.AreEqual("Paused", chart.StatusForTest);
        toggle.Toggle();
        Assert.IsFalse(chart.Paused);
        Assert.AreEqual("Live", chart.StatusForTest);
    });

    [TestMethod]
    public void TheKeyboardMovesAReadingPointWithinTheWindow() => Run((chart, points) =>
    {
        chart.FocusForTest();
        int last = chart.CursorForTest;
        Assert.IsGreaterThanOrEqualTo(0, last);
        chart.KeyForTest(Key.Left);
        Assert.AreEqual(last - 1, chart.CursorForTest);
        chart.KeyForTest(Key.Home);
        Assert.IsLessThan(last - 1, chart.CursorForTest);
        StringAssert.Contains(chart.TooltipForTest, "ms");
    });

    [TestMethod]
    public void AxisLeavesRoomOverTheDataAndStepsAreRound() => Run((chart, points) =>
    {
        Assert.AreEqual(50, RealtimeStream.NiceStepForTest(90), 1e-9);
        Assert.AreEqual(200, RealtimeStream.NiceStepForTest(456), 1e-9);
        points.Add(new StreamPoint(points[^1].Time.AddMilliseconds(500), 430, "Spike"));
        for (int i = 0; i < 200; i++) { chart.AdvanceForTest(1d / 30); }
        Assert.IsGreaterThan(430d, chart.AxisForTest.High);
        Assert.IsLessThan(130d, chart.AxisForTest.Low);
        CollectionAssert.Contains(chart.TimeLabelsForTest(473).ToList(), "now");
    });

    [TestMethod]
    public void AnEmptyFeedShowsTheWaitingLabel() => Run((chart, points) =>
    {
        Assert.AreEqual(0, points.Count);
        Assert.IsFalse(chart.TooltipShownForTest);
        chart.HoverForTest(100);
        Assert.IsFalse(chart.TooltipShownForTest);
    }, 0);
}
