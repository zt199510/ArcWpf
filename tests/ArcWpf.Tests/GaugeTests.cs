using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class GaugeTests
{
    private static GaugeThresholdCollection Bands() => [new GaugeThreshold { From = 0, Tone = ArcTone.Danger, Label = "At risk" }, new GaugeThreshold { From = 30, Tone = ArcTone.Warning, Label = "Needs attention" }, new GaugeThreshold { From = 70, Tone = ArcTone.Success, Label = "Healthy" }];

    private static void Run(Action<Gauge> action, double value = 72)
    {
        Sta.Run(() =>
        {
            var gauge = new Gauge { Label = "Workspace health", Value = value, Thresholds = Bands(), Width = 240 };
            var window = new Window { Content = gauge, Width = 400, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(gauge); }
            finally { window.Close(); }
        });
    }

    private static void Settle(Gauge gauge) { for (int index = 0; index < 400 && gauge.RunningForTest; index++) { gauge.AdvanceForTest(1d / 60); } }

    [TestMethod]
    public void RingAndCountFillToTheValueOnceSeen() => Run(gauge =>
    {
        Sta.Pump();
        Settle(gauge);
        Assert.AreEqual(.72, gauge.SweepForTest, .001);
        Assert.AreEqual("72", gauge.DigitsForTest);
        Assert.IsTrue(gauge.FilledForTest);
    });

    [TestMethod]
    public void StateAppearsAfterTheFirstFillAndFollowsTheBand() => Run(gauge =>
    {
        Sta.Pump();
        Settle(gauge);
        Assert.AreEqual("Healthy", gauge.StatusForTest);
        Assert.AreEqual(ArcTone.Success, gauge.ActiveToneForTest);
        gauge.Value = 40;
        Settle(gauge);
        Assert.AreEqual("Needs attention", gauge.StatusForTest);
        Assert.AreEqual(ArcTone.Warning, gauge.ActiveToneForTest);
        gauge.Value = 4;
        Settle(gauge);
        Assert.AreEqual("At risk", gauge.StatusForTest);
        Assert.AreEqual(ArcTone.Danger, gauge.ActiveToneForTest);
    });

    [TestMethod]
    public void ValueIsClampedAndPercentIsOfTheRange() => Run(gauge =>
    {
        gauge.Minimum = 50;
        gauge.Maximum = 150;
        gauge.Value = 100;
        Settle(gauge);
        Assert.AreEqual(.5, gauge.SweepForTest, .001);
        gauge.Value = 900;
        Settle(gauge);
        Assert.AreEqual(1, gauge.SweepForTest, .001);
        Assert.AreEqual("100", gauge.DigitsForTest);
    });

    [TestMethod]
    public void SpeaksThePercentAndBandAsAMeter() => Run(gauge =>
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(gauge);
        Assert.AreEqual(AutomationControlType.ProgressBar, peer.GetAutomationControlType());
        Assert.AreEqual("Workspace health", peer.GetName());
        Assert.AreEqual("72%, Healthy", peer.GetItemStatus());
        var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);
        Assert.AreEqual(72, range.Value);
        Assert.IsTrue(range.IsReadOnly);
    });

    [TestMethod]
    public void ArcEndsWhereTheSweepSays()
    {
        Sta.Run(() =>
        {
            var half = Gauge.ArcFor(.5, 1).Bounds;
            Assert.AreEqual(8, half.Top, .01, "half way is the top of the ring");
            var full = Gauge.ArcFor(1, 1).Bounds;
            Assert.AreEqual(8, full.Left, .01);
            Assert.AreEqual(92, full.Right, .01);
        });
    }
}
