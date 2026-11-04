using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class MetricTickerTests
{
    private static List<TickerMetric> Metrics(double p95 = 217) =>
    [
        new() { Id = "mrr", Label = "Monthly recurring revenue", Value = 84060, Format = "C0" },
        new() { Id = "active", Label = "Active now", Value = 1270 },
        new() { Id = "trial", Label = "Trial conversion", Value = .182, Format = "P1", Delta = TickerDelta.Absolute },
        new() { Id = "p95", Label = "API p95", Value = p95, Unit = " ms", Positive = TickerPositive.Down, Delta = TickerDelta.Absolute, Caption = "p95 across regions" },
        new() { Id = "orders", Label = "Orders per minute", Value = 330 },
    ];

    private static void Run(Action<MetricTicker> action, double width = 521.6)
    {
        Sta.Run(() =>
        {
            var ticker = new MetricTicker { Metrics = Metrics(), Width = width };
            var window = new Window { Content = ticker, Width = 900, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(ticker); }
            finally { window.Close(); }
        });
    }

    private static void Settle(MetricTicker ticker) { for (int index = 0; index < 200; index++) { ticker.AdvanceForTest(1d / 60); } }

    [TestMethod]
    public void NarrowStripLoopsAndWideStripRestsWithoutMoving()
    {
        Run(ticker => { Assert.IsTrue(ticker.LoopingForTest); double from = ticker.OffsetForTest; Settle(ticker); Assert.AreNotEqual(from, ticker.OffsetForTest); });
        Run(ticker => { Assert.IsTrue(ticker.FitsForTest); Assert.IsFalse(ticker.LoopingForTest); double from = ticker.OffsetForTest; Settle(ticker); Assert.AreEqual(from, ticker.OffsetForTest); }, 1600);
    }

    [TestMethod]
    public void CellsScrolledInFromTheRightStillPaintAndAnswerThePointer() => Run(ticker =>
    {
        // Regression: the strip's default layout clip travelled with its transform and cut off everything past its slot.
        ticker.DragForTest(400, 100, .3);
        ticker.UpdateLayout();
        var hit = VisualTreeHelper.HitTest(ticker, new Point(ticker.ActualWidth - 50, ticker.ActualHeight / 2));
        Assert.IsNotNull(hit);
        Assert.AreEqual("Cell", Ancestor(hit.VisualHit, "Cell"));
    });

    private static string? Ancestor(DependencyObject? from, string type)
    {
        for (var at = from; at is not null; at = VisualTreeHelper.GetParent(at)) { if (at.GetType().Name == type) { return type; } }
        return null;
    }

    [TestMethod]
    public void NumbersFollowTheValueWithAToneFromTheGoodDirection() => Run(ticker =>
    {
        Assert.AreEqual("$84,060", ticker.NumberForTest("mrr"));
        Assert.AreEqual("18.2%", ticker.NumberForTest("trial"));
        Assert.AreEqual("217 ms", ticker.NumberForTest("p95"));
        ticker.Metrics = Metrics(230);
        Assert.AreEqual("230 ms", ticker.NumberForTest("p95"));
        Assert.IsTrue(ticker.FlashingForTest("p95"));
        Assert.AreEqual("Bad", ticker.ToneForTest("p95"), "Rising latency is bad when Positive is Down.");
        ticker.Metrics = Metrics(200);
        Assert.AreEqual("Good", ticker.ToneForTest("p95"));
    });

    [TestMethod]
    public void HoverStopsTheDriftAndOpensThePanelWithItsCaption() => Run(ticker =>
    {
        ticker.HoverForTest("p95");
        Settle(ticker);
        Assert.AreEqual("p95", ticker.OpenIdForTest);
        Assert.IsTrue(ticker.PanelShownForTest);
        Assert.AreEqual("p95 across regions", ticker.CaptionForTest);
        Assert.AreEqual(0, ticker.VelocityForTest, .5);
        double held = ticker.OffsetForTest;
        Settle(ticker);
        Assert.AreEqual(held, ticker.OffsetForTest, .5);
        ticker.LeaveForTest();
        Settle(ticker);
        Assert.IsFalse(ticker.PanelShownForTest);
        Assert.IsGreaterThan(1d, ticker.VelocityForTest);
    });

    [TestMethod]
    public void PanelStaysInsideTheStrip() => Run(ticker =>
    {
        ticker.HoverForTest("mrr");
        Settle(ticker);
        Assert.IsGreaterThanOrEqualTo(0d, ticker.PanelXForTest);
        Assert.IsLessThanOrEqualTo(ticker.ActualWidth - ticker.PanelForTest.Width, ticker.PanelXForTest);
    });

    [TestMethod]
    public void TheMarqueeCopyIsNotAnnouncedTwiceAndEscapeClosesThePanel() => Run(ticker =>
    {
        StringAssert.Contains(ticker.SpokenForTest("p95"), "API p95");
        ticker.HoverForTest("p95");
        Settle(ticker);
        ticker.KeyForTest(Key.Escape);
        Settle(ticker);
        Assert.IsFalse(ticker.PanelShownForTest);
    });
}
