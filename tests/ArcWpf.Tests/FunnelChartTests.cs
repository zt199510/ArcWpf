using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class FunnelChartTests
{
    private static FunnelStage S(string label, double mobile, double desktop) => new(label, label, Values: new Dictionary<string, double> { ["mobile"] = mobile, ["desktop"] = desktop });
    private static readonly FunnelStage[] Stages = [S("Visited store", 68420, 31580), S("Viewed product", 39710, 21460), S("Added to cart", 9140, 7280), S("Started checkout", 4310, 4090), S("Purchased", 1540, 2050)];

    private static void Run(Action<FunnelChart> action, double width = 500)
    {
        Sta.Run(() =>
        {
            var chart = new FunnelChart { Label = "Checkout funnel", Unit = "sessions", Segments = [new("mobile", "Mobile"), new("desktop", "Desktop")], Stages = Stages, Flow = false };
            var window = new Window { Content = chart, Width = width, Height = 720, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void TheBandIsFlatAcrossAStepAndEasedBetween()
    {
        double[] levels = [8, 44];
        Assert.AreEqual(8, FunnelMath.LevelAt(levels, .5));
        Assert.AreEqual(8, FunnelMath.LevelAt(levels, .76));
        Assert.AreEqual(26, FunnelMath.LevelAt(levels, 1), 1e-9);
        Assert.AreEqual(44, FunnelMath.LevelAt(levels, 1.24), 1e-9);
        Assert.AreEqual(44, FunnelMath.LevelAt(levels, 1.8));
    }

    [TestMethod]
    public void RatesKeepOneDecimalUnderTen()
    {
        Assert.AreEqual("61%", FunnelMath.Rate(.6117));
        Assert.AreEqual("8.4%", FunnelMath.Rate(.084));
        Assert.AreEqual("3.6%", FunnelMath.Rate(.0359));
    }

    [TestMethod]
    public void TotalStacksSegmentsAroundTheCenter() => Run(chart =>
    {
        Assert.AreEqual(2, chart.LayerCountForTest);
        Assert.AreEqual(8, chart.TopOfForTest(0, 0), 1e-6);
        Assert.AreEqual(192, chart.BottomOfForTest(1, 0), 1e-6);
        Assert.AreEqual(chart.BottomOfForTest(0, 0), chart.TopOfForTest(1, 0), 1e-6);
        Assert.AreEqual(100 - 92 * .6117, chart.TopOfForTest(0, 1), 1e-6);
        Assert.AreEqual("100K", chart.ValueForTest(0));
        Assert.AreEqual("Start", chart.RateForTest(0));
        Assert.AreEqual("8.4% of start", chart.RateForTest(3));
        Assert.AreEqual("39%", chart.DropForTest(0));
    });

    [TestMethod]
    public void SplitOpensATwoPixelGapAndMorphs() => Run(chart =>
    {
        double before = chart.NowTopForTest(0, 0);
        chart.View = "split";
        Assert.AreEqual(7, chart.TopOfForTest(0, 0), 1e-6);
        Assert.AreEqual(2, chart.TopOfForTest(1, 0) - chart.BottomOfForTest(0, 0), 1e-6);
        Assert.AreEqual(before, chart.NowTopForTest(0, 0), 1e-6, "morphs from the shape on screen");
        chart.AdvanceForTest(2);
        Assert.AreEqual(7, chart.NowTopForTest(0, 0), .05);
    });

    [TestMethod]
    public void ASegmentRescalesToFillTheHeight() => Run(chart =>
    {
        chart.View = "desktop";
        Assert.AreEqual(8, chart.TopOfForTest(1, 0), 1e-6);
        Assert.AreEqual(192, chart.BottomOfForTest(1, 0), 1e-6);
        Assert.AreEqual(chart.TopOfForTest(0, 0), chart.BottomOfForTest(0, 0), 1e-6, "the other layer closes");
        Assert.AreEqual("31.6K", chart.ValueForTest(0));
        Assert.AreEqual("6.5% of start", chart.RateForTest(4));
        Assert.AreEqual("50%", chart.DropForTest(3));
    });

    [TestMethod]
    public void HoverFocusesAStepAndTheTooltipStaysInside() => Run(chart =>
    {
        var plot = chart.PlotForTest;
        chart.HoverForTest(new Point(plot.ActualWidth * .5, 100));
        Assert.AreEqual(2, chart.ActiveIndex);
        Assert.AreEqual(.5, chart.DimForTest(0));
        Assert.AreEqual(1, chart.DimForTest(2));
        Assert.AreEqual(2 * plot.ActualWidth / 5, chart.ColumnForTest, 1e-6);
        CollectionAssert.AreEqual(new[] { "Added to cart", "Total", "16,420", "27% kept from viewed product" }, chart.TipForTest.OrderBy(text => Array.IndexOf(new[] { "Added to cart", "Total", "16,420", "27% kept from viewed product" }, text)).ToArray());
        Assert.AreEqual(84.2 + (200 - chart.TipHeightForTest) / 2, chart.TipYForTest, 1e-6, "centered on the plot once the new rows are measured");
        Assert.IsTrue(chart.TipHeightForTest > 60);
        chart.HoverForTest(new Point(plot.ActualWidth - 2, 100));
        chart.AdvanceForTest(.6);
        Assert.IsTrue(chart.TipXForTest + chart.TipWidthForTest <= chart.ActualWidth + .5);
    });

    [TestMethod]
    public void KeysWalkTheSteps() => Run(chart =>
    {
        int? seen = -1;
        chart.ActiveChanged += (_, e) => seen = e.Index;
        chart.KeyForTest(Key.Right);
        Assert.AreEqual(0, chart.ActiveIndex);
        chart.KeyForTest(Key.End);
        Assert.AreEqual(4, chart.ActiveIndex);
        Assert.AreEqual(4, seen);
        chart.KeyForTest(Key.Escape);
        Assert.IsNull(chart.ActiveIndex);
    });

    [TestMethod]
    public void AutoTurnsVerticalWhenNarrow()
    {
        Run(chart => Assert.IsFalse(chart.VerticalForTest));
        Run(chart => Assert.IsTrue(chart.VerticalForTest), 360);
    }

    [TestMethod]
    public void FlowStreamsDotsAndStopsWhenOff() => Run(chart =>
    {
        chart.SeenForTest();
        chart.Flow = true;
        Assert.IsTrue(chart.FlowRunningForTest);
        chart.AdvanceFlowForTest(4);
        Assert.IsTrue(chart.LiveDotsForTest > 20, $"got {chart.LiveDotsForTest}");
        chart.Flow = false;
        Assert.IsFalse(chart.FlowRunningForTest);
        Assert.AreEqual(0, chart.LiveDotsForTest);
    });

    [TestMethod]
    public void AFirstStepOfZeroShowsTheMessage() => Run(chart =>
    {
        chart.Stages = [S("Visit", 0, 0), S("Cart", 0, 0)];
        Assert.IsTrue(chart.EmptyShownForTest);
    });

    [TestMethod]
    public void TheBandGrowsFromItsCenterOnceSeen() => ChartHost.BelowTheFold(() => new FunnelChart { Label = "Checkout funnel", Stages = Stages, Segments = [new("mobile", "Mobile"), new("desktop", "Desktop")], Flow = false }, chart =>
    {
        Assert.AreEqual(0, chart.GrowthForTest);
        chart.SeenForTest();
        chart.AdvanceForTest(.05);
        Assert.IsTrue(chart.GrowthForTest is > 0 and < 1, $"got {chart.GrowthForTest}");
        chart.AdvanceForTest(2);
        Assert.AreEqual(1, chart.GrowthForTest, .01);
    });

    [TestMethod]
    public void WithoutMotionItSettlesAsSoonAsItLoads() => Sta.Run(() =>
    {
        var chart = new FunnelChart { Label = "Checkout funnel", Stages = Stages, Segments = [new("mobile", "Mobile"), new("desktop", "Desktop")] };
        ArcMotion.SetLevel(chart, ArcMotionLevel.Off);
        var window = new Window { Content = chart, Width = 500, Height = 720, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual(1, chart.GrowthForTest);
            Assert.IsFalse(chart.FlowRunningForTest, "dots do not stream without motion");
        }
        finally { window.Close(); }
    });
}
