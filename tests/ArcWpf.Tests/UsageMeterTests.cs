using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class UsageMeterTests
{
    [TestMethod] public void PercentageUsesUsedAndLimit() => Sta.Run(() => { var meter = new UsageMeter { Used = 75, Limit = 100 }; Assert.AreEqual(.75, meter.Percentage, .001); });
    [TestMethod] public void WarningThresholdIsApplied() => Sta.Run(() => { var meter = new UsageMeter { Used = 90, Limit = 100 }; Assert.IsTrue(meter.IsWarning); });
    [TestMethod] public void ZeroLimitProducesZeroPercentage() => Sta.Run(() => { var meter = new UsageMeter { Used = 10, Limit = 0 }; Assert.AreEqual(0d, meter.Percentage); });
    [TestMethod] public void ThresholdAndUnitUpdateDisplayedState() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Used = 75, Limit = 100, WarningThreshold = .8, Unit = "GB" };
        var window = new System.Windows.Window { Content = meter, Width = 360, Height = 120, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.IsFalse(meter.IsWarning);
            meter.WarningThreshold = .7;
            meter.Unit = "MB";
            Assert.IsTrue(meter.IsWarning);
            var value = (System.Windows.Controls.TextBlock)meter.Template.FindName("PART_Value", meter)!;
            Assert.IsTrue(value.Text.EndsWith("MB", StringComparison.Ordinal));
        }
        finally { window.Close(); }
    });
    [TestMethod] public void NonFiniteUsageIsRejected() => Sta.Run(() => Assert.Throws<ArgumentException>(() => new UsageMeter { Used = double.NaN }));
    [TestMethod] public void FillTracksValueAndOffSettlesImmediately() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 300, Used = 25, Limit = 100 };
        ArcMotion.SetLevel(meter, ArcMotionLevel.Off);
        var window = new System.Windows.Window { Content = meter, Width = 360, Height = 120, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var fill = (System.Windows.Controls.Border)meter.Template.FindName("PART_Fill", meter)!;
            var scale = (System.Windows.Media.ScaleTransform)fill.RenderTransform;
            Assert.AreEqual(.25, scale.ScaleX, .001);
            meter.Used = 75;
            Assert.AreEqual(.75, scale.ScaleX, .001);
            Assert.IsFalse(scale.HasAnimatedProperties);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void FullMotionSettlesAndReleasesClock() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 300, Used = 25, Limit = 100 };
        ArcMotion.SetLevel(meter, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = meter, Width = 360, Height = 120, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var fill = (System.Windows.Controls.Border)meter.Template.FindName("PART_Fill", meter)!;
            var scale = (System.Windows.Media.ScaleTransform)fill.RenderTransform;
            meter.Used = 75;
            Assert.IsTrue(scale.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(.75, scale.ScaleX, .01);
            Assert.IsFalse(scale.HasAnimatedProperties);
            meter.WarningThreshold = .7;
            Assert.AreEqual(.75, scale.ScaleX, .01);
        }
        finally { window.Close(); }
    });
}
