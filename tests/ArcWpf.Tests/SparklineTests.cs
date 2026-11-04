using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SparklineTests
{
    private static readonly double[] Week = [2318, 2402, 2376, 2451, 2433, 2390, 2486];
    private static readonly string[] Days = ["Sep 15", "Sep 16", "Sep 17", "Sep 18", "Sep 19", "Sep 20", "Sep 21"];

    private static void Run(Action<Sparkline> action)
    {
        Sta.Run(() =>
        {
            var chart = new Sparkline { Label = "Daily active users", Value = "2,486", Change = "+7.2%", Tone = ArcTone.Success, Data = Week, Labels = Days, DrawWidth = 360, DrawHeight = 100, Width = 360, FormatValue = (value, _) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) };
            var window = new Window { Content = chart, Width = 500, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(chart); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void CurvePassesThroughEveryPointAndStaysInsideTheData()
    {
        var shape = SparklineMath.Curve(Week);
        double min = Week.Min(), span = Week.Max() - min;
        for (int index = 0; index < Week.Length; index++)
        {
            Assert.AreEqual((Week[index] - min) / span, SparklineMath.HeightAt(shape, index / 6d), 1e-6);
        }
        Assert.IsTrue(shape.All(point => point.Y is >= 0 and <= 1));
        Assert.AreEqual(1, shape[^1].X);
    }

    [TestMethod]
    public void FlatDataSitsInTheMiddle()
    {
        var shape = SparklineMath.Curve([5, 5, 5]);
        Assert.IsTrue(shape.All(point => Math.Abs(point.Y - .5) < 1e-9));
    }

    [TestMethod]
    public void PenStopsPartWayAlongTheLine()
    {
        Point[] line = [new(0, 0), new(10, 0), new(20, 0)];
        var (count, tip) = SparklineMath.PenAt(line, .75);
        Assert.AreEqual(2, count);
        Assert.AreEqual(15, tip.X, 1e-9);
    }

    [TestMethod]
    public void ScrubShowsThePointThenLeavingRestores() => Run(chart =>
    {
        chart.ScrubForTest(120);
        Assert.AreEqual(2, chart.ActiveForTest);
        Assert.AreEqual("2,376", chart.HeadlineForTest);
        Assert.AreEqual("Sep 17", chart.AsideForTest);
        chart.LeaveForTest();
        Assert.IsNull(chart.ActiveForTest);
        Assert.AreEqual("2,486", chart.HeadlineForTest);
        Assert.AreEqual("+7.2%", chart.AsideForTest);
    });

    [TestMethod]
    public void CursorSpringsToThePointAndHome() => Run(chart =>
    {
        chart.FinishForTest();
        chart.ScrubForTest(120);
        for (int index = 0; index < 120; index++) { chart.AdvanceForTest(1d / 60); }
        Assert.AreEqual(2d / 6, chart.CursorForTest, .001);
        Assert.AreEqual(10, chart.DotWidthForTest, .05);
        chart.LeaveForTest();
        for (int index = 0; index < 120; index++) { chart.AdvanceForTest(1d / 60); }
        Assert.AreEqual(1, chart.CursorForTest, .001);
        Assert.AreEqual(7, chart.DotWidthForTest, .05);
    });

    [TestMethod]
    public void KeysWalkThePoints() => Run(chart =>
    {
        chart.KeyForTest(Key.Left);
        Assert.AreEqual(5, chart.ActiveForTest);
        chart.KeyForTest(Key.Home);
        Assert.AreEqual(0, chart.ActiveForTest);
        chart.KeyForTest(Key.Left);
        Assert.AreEqual(0, chart.ActiveForTest);
        chart.KeyForTest(Key.PageUp);
        Assert.AreEqual(1, chart.ActiveForTest);
        chart.KeyForTest(Key.End);
        Assert.AreEqual(6, chart.ActiveForTest);
        chart.KeyForTest(Key.Escape);
        Assert.IsNull(chart.ActiveForTest);
    });

    [TestMethod]
    public void NewDataEndsOnTheNewCurve() => Run(chart =>
    {
        chart.FinishForTest();
        chart.Data = [1, 2, 3, 4];
        chart.FinishForTest();
        CollectionAssert.AreEqual(SparklineMath.Curve([1d, 2, 3, 4]), chart.ShapeForTest);
    });

    [TestMethod]
    public void PlotSpeaksAsASliderOverThePoints() => Run(chart =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(chart.PlotForTest);
        Assert.AreEqual(AutomationControlType.Slider, peer.GetAutomationControlType());
        Assert.AreEqual("Daily active users, explore values", peer.GetName());
        Assert.AreEqual("Sep 21: 2,486", peer.GetItemStatus());
        var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);
        Assert.AreEqual(7, range.Value);
        range.SetValue(3);
        Assert.AreEqual(2, chart.ActiveForTest);
    });

    [TestMethod]
    public void RollTextKeepsUnchangedDigitsStill()
    {
        Sta.Run(() =>
        {
            var roll = new RollText(18, 1.4, -.18, FontWeights.Medium, morph: true);
            var window = new Window { Content = roll, Width = 300, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                Sta.Pump();
                roll.Set("2,486", System.Windows.Media.Brushes.Black, "");
                roll.Set("2,376", System.Windows.Media.Brushes.Black, "");
                CollectionAssert.AreEqual(new[] { "2", ",", "3", "7", "6" }, roll.KeysForTest.ToArray());
                Assert.AreEqual(2, roll.LeavingForTest, "only the changed digits roll");
            }
            finally { window.Close(); }
        });
    }
}
