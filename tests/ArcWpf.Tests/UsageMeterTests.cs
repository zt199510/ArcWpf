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
    [TestMethod] public void SegmentsDriveTotalNearAndOverStates() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Used = 99, Limit = 50, Unit = "GB" };
        var files = new UsageMeterSegment { Id = "files", Label = "Files", Value = 21.4 };
        var media = new UsageMeterSegment { Id = "media", Label = "Media", Value = 14.2 };
        var backups = new UsageMeterSegment { Id = "backups", Label = "Backups", Value = 6.8 };
        meter.Segments.Add(files);
        meter.Segments.Add(media);
        meter.Segments.Add(backups);
        Assert.AreEqual(42.4, meter.Total, .001);
        Assert.AreEqual(7.6, meter.Free, .001);
        Assert.AreEqual(UsageMeterStatus.Normal, meter.Status);
        media.Value = 18.2;
        Assert.AreEqual(UsageMeterStatus.Near, meter.Status);
        Assert.IsTrue(meter.IsWarning);
        meter.Limit = 40;
        Assert.AreEqual(UsageMeterStatus.Over, meter.Status);
        Assert.AreEqual(6.4, meter.Over, .001);
        Assert.IsFalse(meter.IsWarning);
        Assert.Throws<ArgumentOutOfRangeException>(() => backups.Value = double.NaN);
        meter.Segments.Clear();
        Assert.AreEqual(99d, meter.Total);
    });
    [TestMethod] public void WindowRendersSegmentsLegendAndOverage() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 350, Limit = 50, Unit = "GB" };
        ArcMotion.SetLevel(meter, ArcMotionLevel.Off);
        meter.Segments.Add(new UsageMeterSegment { Id = "files", Label = "Files", Value = 21.4 });
        meter.Segments.Add(new UsageMeterSegment { Id = "media", Label = "Media", Value = 14.2 });
        meter.Segments.Add(new UsageMeterSegment { Id = "backups", Label = "Backups", Value = 6.8 });
        var window = new System.Windows.Window { Content = meter, Width = 440, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); meter.UpdateLayout();
            var canvas = (System.Windows.Controls.Canvas)meter.Template.FindName("PART_Segments", meter)!;
            var legend = (System.Windows.Controls.WrapPanel)meter.Template.FindName("PART_Legend", meter)!;
            var value = (System.Windows.Controls.TextBlock)meter.Template.FindName("PART_Value", meter)!;
            Assert.IsGreaterThan(0d, canvas.ActualWidth);
            Assert.HasCount(4, legend.Children);
            var first = (System.Windows.Controls.Border)canvas.Children[0];
            var second = (System.Windows.Controls.Border)canvas.Children[1];
            Assert.IsGreaterThan(0d, first.Width);
            Assert.IsTrue(System.Windows.Controls.Canvas.GetLeft(second) >= first.Width + 1.5);
            Assert.IsTrue(value.Text.StartsWith("42.4", StringComparison.Ordinal));
            var media = (System.Windows.Controls.Primitives.ToggleButton)legend.Children[1];
            media.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, media));
            Assert.IsTrue(value.Text.StartsWith("14.2", StringComparison.Ordinal));
            Assert.IsTrue(media.IsChecked);
            meter.Limit = 40; meter.UpdateLayout();
            Assert.AreEqual(UsageMeterStatus.Over, meter.Status);
            var hatch = (System.Windows.Shapes.Rectangle)meter.Template.FindName("PART_OverHatch", meter)!;
            var marker = (System.Windows.Controls.Border)meter.Template.FindName("PART_LimitMarker", meter)!;
            Assert.IsGreaterThan(0d, hatch.Width);
            Assert.IsTrue(System.Windows.Controls.Canvas.GetLeft(marker) < canvas.ActualWidth);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void LegendKeyboardMovesFocusAndEscapeClearsPinnedCategory() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 330, Limit = 50, Unit = "GB" };
        meter.Segments.Add(new UsageMeterSegment { Id = "files", Label = "Files", Value = 20 });
        meter.Segments.Add(new UsageMeterSegment { Id = "media", Label = "Media", Value = 10 });
        var window = new System.Windows.Window { Content = meter, Width = 420, Height = 270, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var legend = (System.Windows.Controls.WrapPanel)meter.Template.FindName("PART_Legend", meter)!;
            var first = (System.Windows.Controls.Primitives.ToggleButton)legend.Children[0];
            var second = (System.Windows.Controls.Primitives.ToggleButton)legend.Children[1];
            first.Focus();
            var right = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(first), 0, System.Windows.Input.Key.Right) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            first.RaiseEvent(right);
            Assert.IsTrue(second.IsKeyboardFocused);
            second.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, second));
            Assert.IsTrue(second.IsChecked);
            var escape = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(second), 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            second.RaiseEvent(escape);
            Assert.IsFalse(second.IsChecked);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void SegmentMotionSettlesAndKeepsVisualAfterValueUpdate() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 350, Limit = 50 };
        meter.Segments.Add(new UsageMeterSegment { Id = "files", Label = "Files", Value = 20 });
        meter.Segments.Add(new UsageMeterSegment { Id = "media", Label = "Media", Value = 10 });
        ArcMotion.SetLevel(meter, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = meter, Width = 440, Height = 270, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var canvas = (System.Windows.Controls.Canvas)meter.Template.FindName("PART_Segments", meter)!;
            var first = (System.Windows.Controls.Border)canvas.Children[0];
            double before = first.Width;
            meter.Segments[0].Value = 30;
            Assert.IsTrue(first.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreSame(first, canvas.Children[0]);
            Assert.IsGreaterThan(before, first.Width);
            Assert.IsFalse(first.HasAnimatedProperties);
            meter.Limit = 45;
            Assert.IsTrue(first.Width > 0);
            ArcMotion.SetLevel(meter, ArcMotionLevel.Off);
            Assert.IsFalse(first.HasAnimatedProperties);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void SegmentBrushTracksThemeChangesInWindow() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 320, Limit = 50 };
        var theme = new ArcWpf.ArcThemeResources();
        meter.Resources.MergedDictionaries.Add(theme);
        meter.Segments.Add(new UsageMeterSegment { Id = "files", Label = "Files", Value = 20 });
        var window = new System.Windows.Window { Content = meter, Width = 400, Height = 270, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var canvas = (System.Windows.Controls.Canvas)meter.Template.FindName("PART_Segments", meter)!;
            var visual = (System.Windows.Controls.Border)canvas.Children[0];
            var light = ((System.Windows.Media.SolidColorBrush)visual.Background).Color;
            var numberHost = (System.Windows.Controls.Grid)meter.Template.FindName("PART_NumberHost", meter)!;
            var ticker = numberHost.Children.OfType<OdometerNumber>().Single();
            var digitStrip = (System.Windows.Controls.Canvas)System.Windows.Media.VisualTreeHelper.GetChild(ticker.DigitColumns["i0"].Root, 0);
            var digit = (System.Windows.Controls.TextBlock)digitStrip.Children[10];
            var lightDigit = ((System.Windows.Media.SolidColorBrush)digit.Foreground).Color;
            theme.Mode = ArcWpf.ArcThemeMode.Dark;
            Sta.Pump();
            Assert.AreNotEqual(light, ((System.Windows.Media.SolidColorBrush)visual.Background).Color);
            Assert.AreNotEqual(lightDigit, ((System.Windows.Media.SolidColorBrush)digit.Foreground).Color);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ReadoutDigitColumnsRollThroughCarryAndReleaseClocks() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 320, Used = 99, Limit = 100, Decimals = 0 };
        ArcMotion.SetLevel(meter, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = meter, Width = 400, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var host = (System.Windows.Controls.Grid)meter.Template.FindName("PART_NumberHost", meter)!;
            var ticker = host.Children.OfType<OdometerNumber>().Single();
            var readout = (System.Windows.Controls.TextBlock)meter.Template.FindName("PART_Value", meter)!;
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            meter.Used = 100;
            Assert.AreEqual("100", readout.Text);
            Assert.AreEqual(0, ticker.DigitColumns["i0"].Digit);
            Assert.AreEqual(0, ticker.DigitColumns["i1"].Digit);
            Assert.IsTrue(ticker.IsMotionRunning);
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(ticker.IsMotionRunning);
            meter.Used = 99;
            Assert.AreEqual(9, ticker.DigitColumns["i0"].Digit);
            Assert.AreEqual(9, ticker.DigitColumns["i1"].Digit);
            ArcMotion.SetLevel(meter, ArcMotionLevel.Off);
            Assert.IsFalse(ticker.IsMotionRunning);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void LegendCategoryValueRollsWithoutRecreatingItsButton() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 340, Limit = 50, Unit = "GB", Decimals = 0 };
        meter.Segments.Add(new UsageMeterSegment { Id = "files", Label = "Files", Value = 20 });
        meter.Segments.Add(new UsageMeterSegment { Id = "media", Label = "Media", Value = 10 });
        ArcMotion.SetLevel(meter, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = meter, Width = 430, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var legend = (System.Windows.Controls.WrapPanel)meter.Template.FindName("PART_Legend", meter)!;
            var first = (System.Windows.Controls.Primitives.ToggleButton)legend.Children[0];
            var content = (System.Windows.Controls.StackPanel)first.Content;
            var amount = (System.Windows.Controls.StackPanel)content.Children[2];
            var ticker = (OdometerNumber)amount.Children[0];
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            meter.Segments[0].Value = 21;
            Assert.AreSame(first, legend.Children[0]);
            Assert.AreEqual(1, ticker.DigitColumns["i0"].Digit);
            Assert.IsTrue(ticker.IsMotionRunning);
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(ticker.IsMotionRunning);
            Assert.AreEqual("GB", ((System.Windows.Controls.TextBlock)amount.Children[1]).Text);
            meter.Segments[0].Value = 22;
            Assert.IsTrue(ticker.IsMotionRunning);
            ArcMotion.SetLevel(meter, ArcMotionLevel.Off);
            Assert.IsFalse(ticker.IsMotionRunning);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void NarrowCaptionUsesRemainingWidthWithoutOverflow() => Sta.Run(() =>
    {
        var meter = new UsageMeter { Width = 220, Limit = 50, Unit = "GB" };
        meter.Segments.Add(new UsageMeterSegment { Id = "archives", Label = "Long-term media archives and imported files", Value = 21.4 });
        var window = new System.Windows.Window { Content = meter, Width = 310, Height = 320, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var legend = (System.Windows.Controls.WrapPanel)meter.Template.FindName("PART_Legend", meter)!;
            var first = (System.Windows.Controls.Primitives.ToggleButton)legend.Children[0];
            first.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, first));
            meter.UpdateLayout();
            var caption = (System.Windows.Controls.TextBlock)meter.Template.FindName("PART_Caption", meter)!;
            Assert.IsTrue(caption.Text.Contains("Long-term media archives", StringComparison.Ordinal));
            Assert.IsTrue(caption.ActualWidth > 0);
            Assert.IsTrue(caption.TranslatePoint(new System.Windows.Point(), meter).X + caption.ActualWidth <= meter.ActualWidth + 1);
        }
        finally { window.Close(); }
    });
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
