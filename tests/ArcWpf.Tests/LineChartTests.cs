using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class LineChartTests
{
    private static LineChartDatum P(string key, double now, double before, string? axis = null) => new(key, key, new Dictionary<string, double> { ["now"] = now, ["before"] = before }, axis);
    private static readonly LineChartDatum[] Week = [P("Mon", 128, 121, "Mon"), P("Tue", 437, 200), P("Wed", 192, 198, "Wed"), P("Thu", 268, 211), P("Fri", 248, 183, "Fri")];
    private static readonly LineChartDatum[] Short = [P("Mon", 10, 20, "Mon"), P("Tue", 30, 40, "Tue")];

    private static void Run(Action<LineChart> action)
    {
        Sta.Run(() =>
        {
            var chart = new LineChart { Label = "Signups", Series = [new("now", "This period"), new("before", "Previous period", Dashed: true, Area: false)], Data = Week };
            var window = new Window { Content = chart, Width = 520, Height = 420, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void ScrubbingReadsEverySeriesInTheTooltip() => Run(chart =>
    {
        LineChartDatum? reported = null;
        chart.ActiveChanged += (_, e) => reported = e.Datum;
        chart.ScrubForTest(chart.PlotForTest.ActualWidth / 4);
        Assert.AreEqual(1, chart.ActiveForTest);
        Assert.AreEqual("Tue", chart.TipTitleForTest);
        CollectionAssert.AreEqual(new[] { "437", "200" }, chart.TipValuesForTest);
        Assert.AreEqual("Tue: This period 437, Previous period 200", chart.SpokenForTest);
        Assert.AreEqual("Tue", reported?.Key);
        chart.LeaveForTest();
        Assert.IsNull(chart.ActiveForTest);
        Assert.IsNull(reported);
    });

    [TestMethod]
    public void TooltipFlipsBeforeItLeavesThePlot() => Run(chart =>
    {
        double width = chart.PlotForTest.ActualWidth;
        chart.ScrubForTest(0);
        chart.AdvanceForTest(.6);
        Assert.IsTrue(chart.TipXForTest >= 14, "right of the crosshair at the start");
        chart.ScrubForTest(width);
        chart.AdvanceForTest(.6);
        Assert.IsTrue(chart.TipXForTest + chart.TipWidthForTest <= width + .5);
        Assert.IsTrue(chart.TipXForTest < width - 14 - chart.TipWidthForTest + 1, "left of it at the end");
    });

    [TestMethod]
    public void HidingASeriesFlattensItAndRescalesTheRest() => Run(chart =>
    {
        Assert.AreEqual(600, chart.MaxTargetForTest);
        chart.ToggleForTest("now");
        CollectionAssert.AreEqual(new[] { "now" }, chart.HiddenSeries!.ToArray());
        Assert.AreEqual(300, chart.MaxTargetForTest);
        chart.AdvanceForTest(2);
        Assert.AreEqual(0, chart.PresenceForTest("now"), .01);
        Assert.AreEqual(300, chart.MaxForTest, .01);
        chart.ToggleForTest("before");
        chart.AdvanceForTest(.5);
        Assert.AreEqual("Choose a series to show", chart.MessageForTest);
    });

    [TestMethod]
    public void NewDataMorphsFromTheShapeOnScreen() => Run(chart =>
    {
        chart.Data = Short;
        Assert.IsNull(chart.ActiveForTest);
        chart.AdvanceForTest(.1);
        var mid = chart.ShapeForTest("now");
        double peak = mid.Max(point => point.Y);
        Assert.IsTrue(peak > 30 && peak < 437, $"between shapes, got {peak}");
        chart.AdvanceForTest(3);
        Assert.AreEqual(30, chart.ShapeForTest("now").Max(point => point.Y), 1e-6);
        CollectionAssert.AreEquivalent(new[] { "Mon", "Tue" }, chart.AxisForTest);
    });

    [TestMethod]
    public void DrawsOnceSeenAndShowsTheEmptyMessage() => Run(chart =>
    {
        chart.DrawForTest();
        chart.AdvanceForTest(1.2);
        Assert.AreEqual(1, chart.DrawnForTest, 1e-6);
        chart.Data = [];
        chart.AdvanceForTest(.5);
        Assert.AreEqual("No data for this range", chart.MessageForTest);
        chart.KeyForTest(Key.Right);
        Assert.IsNull(chart.ActiveForTest);
    });

    [TestMethod]
    public void NothingDrawsBeforeItIsSeen() => ChartHost.BelowTheFold(() => new LineChart { Label = "Signups", Series = [new("now", "This period")], Data = Week }, chart =>
    {
        Assert.AreEqual(0, chart.DrawnForTest);
        chart.DrawForTest();
        chart.AdvanceForTest(.1);
        Assert.IsTrue(chart.DrawnForTest is > 0 and < 1, $"got {chart.DrawnForTest}");
    });

    [TestMethod]
    public void WithoutMotionItSettlesAsSoonAsItLoads() => Sta.Run(() =>
    {
        var chart = new LineChart { Label = "Signups", Series = [new("now", "This period")], Data = Week };
        ArcMotion.SetLevel(chart, ArcMotionLevel.Off);
        var window = new Window { Content = chart, Width = 520, Height = 420, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(1, chart.DrawnForTest);
        }
        finally { window.Close(); }
    });
}
