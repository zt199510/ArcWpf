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
            var move = (System.Windows.Media.TranslateTransform)value.RenderTransform;
            card.NumericValue = 30;
            Assert.IsTrue(value.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual("30k", value.Text);
            Assert.AreEqual(0d, move.Y, .01);
            Assert.AreEqual(1d, value.Opacity, .01);
            Assert.IsFalse(value.HasAnimatedProperties);
            card.NumericValue = 29;
            var sampleFrame = new System.Windows.Threading.DispatcherFrame();
            var sampleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            sampleTimer.Tick += (_, _) => { sampleTimer.Stop(); sampleFrame.Continue = false; };
            sampleTimer.Start(); System.Windows.Threading.Dispatcher.PushFrame(sampleFrame);
            Assert.IsTrue(move.Y < 0, $"Downward value should enter from above, Y={move.Y}");
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.AreEqual(0d, move.Y, .001);
            Assert.IsFalse(value.HasAnimatedProperties);
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
}
