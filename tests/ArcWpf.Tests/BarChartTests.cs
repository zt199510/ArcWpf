using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class BarChartTests
{
    private static readonly BarChartDatum[] Week = [new("a", "Mon", 46, "M"), new("b", "Tue", 58, "T"), new("c", "Wed", 24, "W"), new("d", "Thu", 86, "T")];
    private static readonly BarChartDatum[] Longer = [new("x", "Sat", 118, "S"), new("y", "Sun", 10, "S"), .. Week];

    private static void Run(Action<BarChart> action)
    {
        Sta.Run(() =>
        {
            var chart = new BarChart { Label = "Active minutes", Period = "This week", Unit = "min", Data = Week };
            var window = new Window { Content = chart, Width = 520, Height = 420, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void HeadlineRestsOnTheAverageAndReadsTheScrubbedBar() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(2);
        Assert.AreEqual("54", chart.HeadlineForTest);
        Assert.AreEqual("Daily average", chart.KindForTest);
        Assert.AreEqual(86, chart.HeightOfForTest("d"), .5);
        Assert.AreEqual(53.5, chart.MeanForTest, .5);
        chart.ScrubForTest(chart.PlotForTest.ActualWidth * .1);
        Assert.AreEqual(0, chart.ActiveForTest);
        Assert.AreEqual("46", chart.HeadlineForTest);
        Assert.AreEqual("Total", chart.KindForTest);
        Assert.AreEqual("Mon", chart.WhenForTest);
        Assert.AreEqual("Mon: 46 min", chart.SpokenForTest);
        chart.LeaveForTest();
        Assert.IsNull(chart.ActiveForTest);
        Assert.AreEqual("54", chart.HeadlineForTest);
    });

    [TestMethod]
    public void BarsWaitToBeSeenThenGrowInTurn() => ChartHost.BelowTheFold(() => new BarChart { Label = "Active minutes", Data = Week }, chart =>
    {
        Assert.AreEqual(0, chart.HeightOfForTest("a"), "out of view");
        Assert.AreEqual(0, chart.MeanForTest);
        chart.ShowForTest();
        chart.AdvanceForTest(.03);
        Assert.IsTrue(chart.HeightOfForTest("a") > 0, "the first bar starts at once");
        Assert.AreEqual(0, chart.HeightOfForTest("d"), "the last waits its stagger");
    });

    [TestMethod]
    public void ANewRangeZoomsAroundTheLatestBarAndRescales() => Run(chart =>
    {
        chart.ShowForTest();
        chart.AdvanceForTest(2);
        chart.Data = Longer;
        Assert.AreEqual(6, chart.BarCountForTest);
        CollectionAssert.AreEqual(new double[] { 0, 30, 60, 90, 120 }, chart.TicksForTest.Order().ToArray());
        chart.AdvanceForTest(3);
        Assert.AreEqual(120, chart.ScaleForTest, .05);
        Assert.AreEqual(1d / 6, chart.UnitForTest, .001);
        Assert.AreEqual(0, chart.PlaceOfForTest("d"), .01, "the latest bar stays pinned right");
        chart.Data = Week;
        Assert.AreEqual(2, chart.LeavingForTest);
        chart.AdvanceForTest(3);
        Assert.AreEqual(0, chart.LeavingForTest);
        Assert.AreEqual(4, chart.BarCountForTest);
    });

    [TestMethod]
    public void KeysWalkTheBarsAndEscapeLets() => Run(chart =>
    {
        chart.ShowForTest();
        chart.KeyForTest(Key.Right);
        Assert.AreEqual(3, chart.ActiveForTest, "the first step lands on the latest");
        chart.KeyForTest(Key.Left);
        Assert.AreEqual(2, chart.ActiveForTest);
        chart.KeyForTest(Key.Home);
        Assert.AreEqual(0, chart.ActiveForTest);
        chart.KeyForTest(Key.PageUp);
        Assert.AreEqual(3, chart.ActiveForTest);
        chart.KeyForTest(Key.Escape);
        Assert.IsNull(chart.ActiveForTest);
    });

    [TestMethod]
    public void AxisLabelsThinOutCountingBackFromTheLatest() => Run(chart =>
    {
        chart.Data = [.. Enumerable.Range(0, 40).Select(index => new BarChartDatum($"k{index}", $"Day {index}", index, $"Label {index}"))];
        var shown = chart.AxisForTest;
        Assert.IsTrue(shown.Length < 40);
        Assert.IsTrue(shown.Contains("Label 39"), "the newest always shows");
    });

    [TestMethod]
    public void WithoutMotionItSettlesAsSoonAsItLoads() => Sta.Run(() =>
    {
        var chart = new BarChart { Label = "Active minutes", Data = Week };
        ArcMotion.SetLevel(chart, ArcMotionLevel.Off);
        var window = new Window { Content = chart, Width = 520, Height = 420, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(86, chart.HeightOfForTest("d"), 1e-6);
            Assert.AreEqual(53.5, chart.MeanForTest, .5);
        }
        finally { window.Close(); }
    });
}
