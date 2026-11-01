using ArcWpf.Controls;
using System.Windows;
namespace ArcWpf.Tests;
[TestClass] public sealed class CardTests
{
    [TestMethod] public void CardContentPropertiesAreBindable() => Sta.Run(() => { var card = new Card { Title = "Title", Description = "Description", Content = "Body" }; Assert.AreEqual("Title", card.Title); Assert.AreEqual("Body", card.Content); });
    [TestMethod] public void MetricValuesAreBindable() => Sta.Run(() => { var card = new MetricCard { Value = "42", Trend = "+5%" }; Assert.AreEqual("42", card.Value); Assert.AreEqual("+5%", card.Trend); });
    [TestMethod] public void MetricTrendDefaultsPositive() => Sta.Run(() => Assert.IsTrue(new MetricCard().TrendIsPositive));
    [TestMethod] public void NegativeTrendUpdatesVisibleBrush() => Sta.Run(() =>
    {
        var card = new MetricCard { Title = "Revenue", Value = "42", Trend = "-5%", TrendIsPositive = false };
        var window = new System.Windows.Window { Content = card, Width = 300, Height = 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var trend = (System.Windows.Controls.TextBlock)card.Template.FindName("TrendText", card)!;
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.Danger"), trend.Foreground);
            Assert.AreEqual("-5%", trend.Text);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricSourceLayoutFormatsNumberAndWrapsChangeOnNarrowWidth() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 300, Label = "Uptime", NumericValue = 99.9, Suffix = "%", Context = "Last 30 days", Change = "-0.2%" };
        var window = new System.Windows.Window { Content = card, Width = 380, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.UpdateLayout();
            Assert.AreEqual("Uptime", card.DisplayLabel);
            Assert.AreEqual("99.9%", card.DisplayValue);
            Assert.IsTrue(card.IsNegativeChange);
            Assert.IsTrue(card.IsCompact);
            var surface = (System.Windows.Controls.Border)card.Template.FindName("PART_Surface", card)!;
            var top = (System.Windows.Controls.Grid)card.Template.FindName("PART_Top", card)!;
            var chip = (System.Windows.Controls.Border)card.Template.FindName("ChangeChip", card)!;
            Assert.AreEqual(new Thickness(16), surface.Padding);
            Assert.AreEqual(1, System.Windows.Controls.Grid.GetRow(chip));
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.BadgeDangerBackground"), chip.Background);
            Assert.IsGreaterThan(20d, top.ActualHeight);
            card.Change = "+0.2%";
            Assert.IsFalse(card.IsNegativeChange);
            Assert.IsTrue(card.IsPositiveChange);
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.BadgeSuccessBackground"), chip.Background);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricValueMotionSettlesAndOffStopsIt() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Revenue", NumericValue = 24, Suffix = "k" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = card, Width = 500, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var value = (System.Windows.Controls.TextBlock)card.Template.FindName("PART_Value", card)!;
            var host = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            var ticker = host.Children.OfType<OdometerNumber>().Single();
            Assert.AreEqual(Visibility.Collapsed, value.Visibility);
            card.NumericValue = 30;
            Assert.IsTrue(ticker.IsMotionRunning);
            Assert.AreEqual(3, ticker.DigitColumns["i1"].Digit);
            Assert.AreEqual(0, ticker.DigitColumns["i0"].Digit);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual("30k", value.Text);
            Assert.IsFalse(ticker.IsMotionRunning);
            ticker.UpdateLayout();
            Assert.IsTrue(ticker.ActualWidth > 0 && ticker.ActualHeight > 0);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ticker.ActualWidth), (int)Math.Ceiling(ticker.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(ticker);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            Assert.IsTrue(pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0), "Digit canvas must render visible glyph pixels.");
            double before = ticker.DigitColumns["i0"].Position;
            card.NumericValue = 29;
            var sampleFrame = new System.Windows.Threading.DispatcherFrame();
            var sampleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            sampleTimer.Tick += (_, _) => { sampleTimer.Stop(); sampleFrame.Continue = false; };
            sampleTimer.Start(); System.Windows.Threading.Dispatcher.PushFrame(sampleFrame);
            Assert.IsTrue(ticker.DigitColumns["i0"].Position > before, $"before={before}, now={ticker.DigitColumns["i0"].Position}, running={ticker.IsMotionRunning}");
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.IsFalse(ticker.IsMotionRunning);
            Assert.AreEqual(9, ticker.DigitColumns["i0"].Digit);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricCarryColumnGrowsAndRetiresWithoutResidualVisual() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Requests", NumericValue = 99 };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 500, Height = 250, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var host = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            var ticker = host.Children.OfType<OdometerNumber>().Single();
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            card.NumericValue = 100;
            var hundreds = ticker.DigitColumns["i2"];
            Assert.IsTrue(hundreds.Root.HasAnimatedProperties);
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(hundreds.NaturalWidth, hundreds.Root.Width, .01);
            Assert.IsFalse(ticker.IsMotionRunning);
            card.NumericValue = 99;
            Assert.IsTrue(hundreds.Root.HasAnimatedProperties);
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(ticker.IsMotionRunning);
            Assert.IsNull(hundreds.Root.Parent);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void RapidMetricRetargetKeepsDigitColumnsInCycle() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Requests", NumericValue = 98 };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 500, Height = 250, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var host = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            var ticker = host.Children.OfType<OdometerNumber>().Single();
            for (int value = 99; value <= 120; value++) card.NumericValue = value;
            Assert.AreEqual(0, ticker.DigitColumns["i0"].Digit);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(ticker.IsMotionRunning);
            Assert.AreEqual("120", ((System.Windows.Controls.TextBlock)card.Template.FindName("PART_Value", card)!).Text);
            Assert.IsTrue(ticker.DigitColumns["i0"].Position < 0);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void NumericValueCanReturnToLegacyStringWithoutLosingTemplate() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 320, Label = "Count", Value = "42", NumericValue = 42 };
        ArcMotion.SetLevel(card, ArcMotionLevel.Off);
        var window = new Window { Content = card, Width = 400, Height = 230, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var text = (System.Windows.Controls.TextBlock)card.Template.FindName("PART_Value", card)!;
            var host = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            var ticker = host.Children.OfType<OdometerNumber>().Single();
            Assert.AreEqual(Visibility.Collapsed, text.Visibility);
            card.NumericValue = double.NaN;
            Assert.AreEqual("42", text.Text);
            Assert.AreEqual(Visibility.Visible, text.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ticker.Visibility);
            card.NumericValue = 42;
            Assert.AreEqual(Visibility.Collapsed, text.Visibility);
            Assert.AreEqual(Visibility.Visible, ticker.Visibility);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void MetricTemplateReplacementReleasesOldDigitVisual() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 320, Label = "Count", NumericValue = 24 };
        var window = new Window { Content = card, Width = 400, Height = 230, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var original = card.Template;
            var oldHost = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            var oldTicker = oldHost.Children.OfType<OdometerNumber>().Single();
            card.Template = new System.Windows.Controls.ControlTemplate(typeof(MetricCard)) { VisualTree = new FrameworkElementFactory(typeof(System.Windows.Controls.Border)) };
            card.ApplyTemplate();
            card.NumericValue = 30;
            Assert.IsFalse(oldTicker.IsMotionRunning);
            card.Template = original;
            card.ApplyTemplate(); Sta.Pump();
            var host = (System.Windows.Controls.Grid)card.Template.FindName("PART_NumberHost", card)!;
            Assert.AreEqual("30", ((System.Windows.Controls.TextBlock)card.Template.FindName("PART_Value", card)!).Text);
            Assert.AreNotSame(oldTicker, host.Children.OfType<OdometerNumber>().Single());
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ChangeChipMorphsWidthAndSettlesOnOff() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Revenue", NumericValue = 24, Change = "+2%" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 500, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.UpdateLayout();
            var chip = (System.Windows.Controls.Border)card.Template.FindName("ChangeChip", card)!;
            var text = (System.Windows.Controls.TextBlock)card.Template.FindName("TrendText", card)!;
            double shortWidth = chip.ActualWidth;
            card.Change = "+123.45%";
            Assert.IsTrue(chip.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(chip.HasAnimatedProperties);
            Assert.IsTrue(double.IsNaN(chip.Width));
            Assert.IsTrue(chip.ActualWidth > shortWidth);
            card.Change = "-1%";
            Assert.IsTrue(chip.HasAnimatedProperties);
            Assert.IsTrue(text.HasAnimatedProperties);
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.IsFalse(chip.HasAnimatedProperties);
            Assert.IsFalse(text.HasAnimatedProperties);
            Assert.IsTrue(double.IsNaN(chip.Width));
            Assert.AreEqual(card.TryFindResource("ArcWpf.Brush.Danger"), text.Foreground);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ChangeChipExitKeepsCopyUntilFadeAndReentryCancelsExit() => Sta.Run(() =>
    {
        var card = new MetricCard { Width = 420, Label = "Revenue", NumericValue = 24, Change = "+8%" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 500, Height = 250, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var chip = (System.Windows.Controls.Border)card.Template.FindName("ChangeChip", card)!;
            var text = (System.Windows.Controls.TextBlock)card.Template.FindName("TrendText", card)!;
            card.Change = string.Empty;
            Assert.AreEqual(Visibility.Visible, chip.Visibility);
            Assert.AreEqual("+8%", text.Text);
            Assert.IsTrue(chip.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(Visibility.Collapsed, chip.Visibility);
            Assert.IsFalse(chip.HasAnimatedProperties);
            card.Change = "+4%";
            card.Change = string.Empty;
            card.Change = "-2%";
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(Visibility.Visible, chip.Visibility);
            Assert.AreEqual("-2%", text.Text);
            Assert.AreEqual(1d, chip.Opacity, .01);
            Assert.IsFalse(chip.HasAnimatedProperties);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void QuickLookMorphsFromCardAndReturnsBeforeFocusRestore() => Sta.Run(() =>
    {
        var card = new Card { Width = 320, Title = "Harbour redesign", Description = "New booking flow", Details = "Scope and milestones" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var host = new DialogHost { Content = card };
        var window = new Window { Content = host, Width = 800, Height = 600, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var title = (System.Windows.Controls.Button)card.Template.FindName("PART_TitleButton", card)!;
            title.Focus();
            title.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, title)); Sta.Pump();
            var dialog = host.Dialog!;
            var surface = (FrameworkElement)dialog.Template.FindName("PART_Surface", dialog)!;
            var transform = (System.Windows.Media.TransformGroup)surface.RenderTransform;
            var scale = (System.Windows.Media.ScaleTransform)transform.Children[0];
            var move = (System.Windows.Media.TranslateTransform)transform.Children[1];
            Assert.IsTrue(scale.HasAnimatedProperties);
            Assert.IsTrue(scale.ScaleX < 1, $"The quick look should start near the card width, scale={scale.ScaleX}");
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsTrue(dialog.IsPresented);
            Assert.AreEqual(1d, scale.ScaleX, .01);
            Assert.AreEqual(0d, move.X, .01);
            Assert.IsFalse(scale.HasAnimatedProperties);
            dialog.Close();
            Assert.IsTrue(scale.HasAnimatedProperties);
            frame = new System.Windows.Threading.DispatcherFrame();
            timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(dialog.IsPresented);
            Assert.IsFalse(card.IsOpen);
            Assert.IsTrue(title.IsKeyboardFocused);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void QuickLookReopenDuringExitAndOffSettleWithoutStaleClose() => Sta.Run(() =>
    {
        var card = new Card { Width = 320, Title = "Harbour redesign", Details = "Scope and milestones" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var host = new DialogHost { Content = card };
        var window = new Window { Content = host, Width = 800, Height = 600, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            card.IsOpen = true; Sta.Pump();
            var dialog = host.Dialog!;
            var surface = (FrameworkElement)dialog.Template.FindName("PART_Surface", dialog)!;
            var scale = (System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)surface.RenderTransform).Children[0];
            card.IsOpen = false;
            Assert.IsTrue(dialog.IsPresented);
            card.IsOpen = true; Sta.Pump();
            Assert.IsTrue(dialog.IsOpen);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsTrue(dialog.IsPresented);
            Assert.IsTrue(card.IsOpen);
            Assert.AreEqual(1d, scale.ScaleX, .01);
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            dialog.Close(); Sta.Pump();
            Assert.IsFalse(dialog.IsPresented);
            Assert.IsFalse(scale.HasAnimatedProperties);
            Assert.IsFalse(card.IsOpen);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void QuickLookReducedOpensWithoutSpatialMorph() => Sta.Run(() =>
    {
        var card = new Card { Width = 320, Title = "Harbour redesign", Details = "Scope and milestones" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Reduced);
        var host = new DialogHost { Content = card };
        var window = new Window { Content = host, Width = 800, Height = 600, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            card.IsOpen = true; Sta.Pump();
            var dialog = host.Dialog!;
            var surface = (FrameworkElement)dialog.Template.FindName("PART_Surface", dialog)!;
            var transform = (System.Windows.Media.TransformGroup)surface.RenderTransform;
            var scale = (System.Windows.Media.ScaleTransform)transform.Children[0];
            var move = (System.Windows.Media.TranslateTransform)transform.Children[1];
            Assert.IsTrue(dialog.IsPresented);
            Assert.IsFalse(scale.HasAnimatedProperties);
            Assert.IsFalse(move.HasAnimatedProperties);
            Assert.AreEqual(1d, scale.ScaleX, .01);
            Assert.AreEqual(0d, move.X, .01);
            card.IsOpen = false;
            ArcMotion.SetLevel(card, ArcMotionLevel.Off); Sta.Pump();
            Assert.IsFalse(dialog.IsPresented);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void CardMediaBylineAndDetailsUseDialogHost() => Sta.Run(() =>
    {
        var card = new Card
        {
            Width = 320,
            Title = "Harbour redesign",
            Description = "New booking flow and room pages.",
            Media = new System.Windows.Controls.Border { Height = 120 },
            Avatar = new System.Windows.Controls.TextBlock { Text = "MC" },
            Meta = "Maya Chen",
            Status = "Updated 2 hours ago",
            Action = new System.Windows.Controls.Button { Content = "Open" },
            Details = new System.Windows.Controls.TextBlock { Text = "Scope, milestones, and open questions." }
        };
        var host = new DialogHost { Content = card };
        var window = new System.Windows.Window { Content = host, Width = 550, Height = 550, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.UpdateLayout();
            var media = (System.Windows.FrameworkElement)card.Template.FindName("PART_MediaHost", card)!;
            var byline = (System.Windows.Controls.Grid)card.Template.FindName("CardByline", card)!;
            var title = (System.Windows.Controls.Button)card.Template.FindName("PART_TitleButton", card)!;
            Assert.IsTrue(media.ActualHeight >= 112);
            Assert.AreEqual(Visibility.Visible, byline.Visibility);
            Assert.AreEqual(Visibility.Visible, title.Visibility);
            title.Focus();
            title.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, title));
            Assert.IsTrue(card.IsOpen);
            Assert.IsNotNull(host.Dialog);
            Assert.IsTrue(host.Dialog.IsPresented);
            host.Dialog.Close();
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsFalse(card.IsOpen);
            Assert.IsTrue(title.IsKeyboardFocused);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void CardWithoutDetailsKeepsStaticTitleAndNoEmptyFooter() => Sta.Run(() =>
    {
        var card = new Card { Width = 280, Title = "Project Atlas" };
        var window = new System.Windows.Window { Content = card, Width = 360, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.AreEqual(Visibility.Collapsed, ((System.Windows.Controls.Button)card.Template.FindName("PART_TitleButton", card)!).Visibility);
            Assert.AreEqual(Visibility.Collapsed, ((System.Windows.Controls.Grid)card.Template.FindName("CardByline", card)!).Visibility);
            Assert.AreEqual(Visibility.Collapsed, ((System.Windows.FrameworkElement)card.Template.FindName("PART_MediaHost", card)!).Visibility);
            Assert.IsFalse(card.HasDetails);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void CardHoverLiftsMediaAndStatusUpdatesInWindow() => Sta.Run(() =>
    {
        var card = new Card { Width = 300, Title = "Project", Media = new System.Windows.Controls.Border { Height = 112 }, Meta = "Maya Chen", Status = "Updated yesterday" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new System.Windows.Window { Content = card, Width = 380, Height = 330, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var surface = (System.Windows.Controls.Border)card.Template.FindName("PART_Surface", card)!;
            var media = (System.Windows.FrameworkElement)card.Template.FindName("PART_MediaHost", card)!;
            var status = (System.Windows.Controls.TextBlock)card.Template.FindName("PART_StatusText", card)!;
            card.Status = "Updated 2 hours ago";
            Assert.AreEqual("Updated 2 hours ago", status.Text);
            Assert.IsTrue(status.HasAnimatedProperties);
            surface.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
            Assert.IsTrue(card.IsPointerHover);
            var lift = (System.Windows.Media.TranslateTransform)surface.RenderTransform;
            var zoom = (System.Windows.Media.ScaleTransform)media.RenderTransform;
            Assert.IsTrue(lift.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(-2d, lift.Y, .01);
            Assert.AreEqual(1.04, zoom.ScaleX, .01);
            Assert.IsFalse(lift.HasAnimatedProperties);
            Assert.AreEqual(1d, status.Opacity, .01);
            Assert.IsFalse(status.HasAnimatedProperties);
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.AreEqual(0d, lift.Y, .001);
            Assert.AreEqual(1d, zoom.ScaleX, .001);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void CardSurfaceClickOpensDetailsButNestedActionDoesNot() => Sta.Run(() =>
    {
        var action = new System.Windows.Controls.Button { Content = "Open action" };
        var template = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(System.Windows.Controls.Border)) };
        var card = new Card { Width = 320, Title = "Harbour redesign", Media = "preview", MediaTemplate = template, Action = action, Details = "Scope and milestones" };
        var host = new DialogHost { Content = card };
        var window = new Window { Content = host, Width = 500, Height = 450, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var surface = (System.Windows.Controls.Border)card.Template.FindName("PART_Surface", card)!;
            action.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.UIElement.MouseLeftButtonUpEvent });
            Assert.IsFalse(card.IsOpen);
            surface.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.UIElement.MouseLeftButtonUpEvent });
            Assert.IsTrue(card.IsOpen);
            var body = (System.Windows.Controls.StackPanel)host.Dialog!.Content;
            Assert.HasCount(2, body.Children);
            host.Dialog.Close();
        }
        finally { window.Close(); }
    });

    // card.module.css has no resting shadow; metric-card.module.css uses radius-surface and the counter's text-3xl.
    [TestMethod] public void SurfacesFollowTheirOwnSourceTokens() => Sta.Run(() =>
    {
        var card = new Card { Title = "Harbour redesign" };
        var metric = new MetricCard { Label = "Uptime", NumericValue = 99.9, Context = "Last 30 days" };
        var window = new Window { Content = new System.Windows.Controls.StackPanel { Children = { card, metric } }, Width = 600, Height = 500, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.IsNull(((System.Windows.Controls.Border)card.Template.FindName("PART_Surface", card)!).Effect);
            Assert.AreEqual(34d, ((System.Windows.Controls.Border)metric.Template.FindName("PART_Surface", metric)!).CornerRadius.TopLeft);
            Assert.AreEqual(36d, ((System.Windows.Controls.TextBlock)metric.Template.FindName("PART_Value", metric)!).FontSize);
        }
        finally { window.Close(); }
    });
}
