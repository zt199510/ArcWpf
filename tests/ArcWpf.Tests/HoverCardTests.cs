using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace ArcWpf.Tests;
[TestClass] public sealed class HoverCardTests
{
    [TestMethod] public void DelayPropertiesAreBindable() => Sta.Run(() => { var card = new HoverCard { OpenDelay = 200, CloseDelay = 100 }; Assert.AreEqual(200, card.OpenDelay); Assert.AreEqual(100, card.CloseDelay); });
    [TestMethod] public void DefaultSideIsBottom() => Sta.Run(() => Assert.AreEqual(HoverCardSide.Bottom, new HoverCard().Side));
    [TestMethod] public void IsOpenDefaultsFalse() => Sta.Run(() => Assert.IsFalse(new HoverCard().IsOpen));
    [TestMethod] public void PopupStaysOpenWhilePointerCrossesToCard() => Sta.Run(() =>
    {
        var card = new HoverCard { Content = new Border { Width = 80, Height = 30 }, CardContent = "Details", CloseDelay = 60 };
        var window = new Window { Content = card, Width = 400, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.IsOpen = true; Sta.Pump();
            var trigger = (FrameworkElement)card.Template.FindName("PART_Trigger", card)!;
            var popup = (Popup)card.Template.FindName("PART_Popup", card)!;
            Assert.IsTrue(popup.IsOpen);
            card.Side = HoverCardSide.Top;
            Assert.IsTrue(card.IsOpen);
            Assert.IsTrue(popup.IsOpen);
            trigger.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            popup.Child.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.IsTrue(card.IsOpen);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void NearScreenEdgeFlipsAndConstrainsLongCard() => Sta.Run(() =>
    {
        var work = SystemParameters.WorkArea;
        var trigger = new System.Windows.Controls.Button { Content = "Profile", Width = 80, Height = 30 };
        var card = new HoverCard { Content = trigger, CardContent = new Border { Height = 2000 }, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        var window = new Window { Content = card, Width = 420, Height = 160, Left = work.Left + 80, Top = work.Bottom - 180, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.IsOpen = true; Sta.Pump();
            var popup = (Popup)card.Template.FindName("PART_Popup", card)!;
            var frame = (Border)popup.Child;
            var surface = (Border)frame.Child;
            Assert.IsTrue(popup.IsOpen);
            Assert.AreEqual(HoverCardSide.Top, card.ActualSide);
            var scroll = (ScrollViewer)surface.Child;
            Assert.IsTrue(scroll.ScrollableHeight > 0);
            var bounds = ArcWpf.Controls.Popover.ScreenBounds(surface);
            var monitor = ArcWpf.Controls.Popover.MonitorWorkArea(ArcWpf.Controls.Popover.ScreenBounds(trigger));
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(trigger);
            Assert.IsTrue(bounds.Left >= monitor.Left + 12 * dpi.DpiScaleX - 2);
            Assert.IsTrue(bounds.Right <= monitor.Right - 12 * dpi.DpiScaleX + 2);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void OutsideWindowClickClosesPopupWithoutStealingTriggerFocus() => Sta.Run(() =>
    {
        var trigger = new System.Windows.Controls.Button { Content = "Profile" };
        var card = new HoverCard { Content = trigger, CardContent = "Details" };
        var outside = new System.Windows.Controls.Button { Content = "Outside" };
        var root = new StackPanel(); root.Children.Add(card); root.Children.Add(outside);
        var window = new Window { Content = root, Width = 430, Height = 280, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); trigger.Focus(); card.IsOpen = true; Sta.Pump();
            var popup = (Popup)card.Template.FindName("PART_Popup", card)!;
            Assert.IsTrue(popup.IsOpen);
            outside.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
            Assert.IsFalse(card.IsOpen);
            Assert.IsFalse(popup.IsOpen);
            Assert.IsTrue(trigger.IsKeyboardFocused);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void OffSettlesOpenMotionAndEscapeSuppressesReopenUntilLeave() => Sta.Run(() =>
    {
        var trigger = new System.Windows.Controls.Button { Content = "Profile" };
        var card = new HoverCard { Content = trigger, CardContent = "Details", OpenDelay = 200 };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 400, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); trigger.Focus(); card.IsOpen = true; Sta.Pump();
            var popup = (Popup)card.Template.FindName("PART_Popup", card)!;
            var frame = (Border)popup.Child;
            var surface = (Border)frame.Child;
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.AreEqual(1d, surface.Opacity, .001);
            Assert.IsFalse(surface.HasAnimatedProperties);
            var source = PresentationSource.FromVisual(trigger)!;
            trigger.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.IsFalse(card.IsOpen);
            Assert.IsFalse(popup.IsOpen);
            Assert.IsTrue(trigger.IsKeyboardFocused);
            var presenter = (FrameworkElement)card.Template.FindName("PART_Trigger", card)!;
            presenter.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert.IsFalse(card.HasPendingOpen);
            presenter.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            presenter.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert.IsTrue(card.HasPendingOpen);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void ProfileRowsStaggerThenReleaseClocksAndInheritMotionLevel() => Sta.Run(() =>
    {
        var profile = new HoverCardProfile { DisplayName = "Maya Chen", Role = "Designer", Bio = "Designing calmer tools.", Meta = "Hong Kong" };
        profile.Stats.Add(new HoverCardStat { Label = "Projects", Value = "12" });
        var context = new object();
        var card = new HoverCard { Content = new System.Windows.Controls.Button { Content = "@maya" }, CardContent = profile, DataContext = context };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 420, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.IsOpen = true; Sta.Pump();
            var head = (FrameworkElement)profile.Template.FindName("PART_Head", profile)!;
            var bio = (FrameworkElement)profile.Template.FindName("Bio", profile)!;
            Assert.AreSame(context, profile.DataContext);
            Assert.IsTrue(head.HasAnimatedProperties);
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Assert.AreEqual(1d, head.Opacity, .01);
            Assert.AreEqual(0d, ((System.Windows.Media.TranslateTransform)bio.RenderTransform).Y, .01);
            Assert.IsFalse(head.HasAnimatedProperties);
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            Assert.AreEqual(ArcMotionLevel.Off, ArcMotion.GetLevel(profile));
            card.IsOpen = false; Sta.Pump(); card.IsOpen = true; Sta.Pump();
            Assert.IsFalse(head.HasAnimatedProperties);
            Assert.AreEqual(1d, head.Opacity, .001);
        }
        finally { window.Close(); }
    });
    [TestMethod] public void OwnerMoveRepositionsAndReducedStopsSpatialMotion() => Sta.Run(() =>
    {
        var work = SystemParameters.WorkArea;
        var card = new HoverCard { Width = 80, Height = 30, Content = new System.Windows.Controls.Button { Content = "Profile" }, CardContent = new Border { Height = 90 }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ArcMotion.SetLevel(card, ArcMotionLevel.Full);
        var window = new Window { Content = card, Width = 420, Height = 220, Left = work.Left + Math.Max(0, (work.Width - 420) / 2), Top = work.Top + 80, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); card.IsOpen = true; Sta.Pump();
            Assert.AreEqual(HoverCardSide.Bottom, card.ActualSide);
            var popup = (Popup)card.Template.FindName("PART_Popup", card)!;
            var surface = (Border)((Border)popup.Child).Child;
            var transform = (System.Windows.Media.TransformGroup)surface.RenderTransform;
            var scale = (System.Windows.Media.ScaleTransform)transform.Children[0];
            var move = (System.Windows.Media.TranslateTransform)transform.Children[1];
            ArcMotion.SetLevel(card, ArcMotionLevel.Reduced);
            Assert.AreEqual(1d, scale.ScaleX, .001);
            Assert.AreEqual(0d, move.Y, .001);
            Assert.IsFalse(scale.HasAnimatedProperties);
            window.Top = work.Bottom - 180; Sta.Pump();
            Assert.AreEqual(HoverCardSide.Top, card.ActualSide);
            window.Top = work.Top + 80; Sta.Pump();
            Assert.AreEqual(HoverCardSide.Bottom, card.ActualSide);
        }
        finally { window.Close(); }
    });
}
