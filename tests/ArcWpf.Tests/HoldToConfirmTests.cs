using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class HoldToConfirmTests
{
    [TestMethod]
    public void TemplateUsesHoldPartsAndControlledConfirmation()
    {
        Sta.Run(() => InWindow(control =>
        {
            control.Label = "Hold"; control.ConfirmedLabel = "Done"; control.Confirmed = true;
            Assert.IsTrue(control.IsConfirmed);
            Assert.AreEqual("Hold", control.Label);
            Assert.IsNotNull(control.Template.FindName("PART_Fill", control));
            Assert.IsNotNull(control.Template.FindName("PART_Check", control));
        }));
    }

    [TestMethod]
    public void EarlyPointerReleaseRewindsWithoutConfirming()
    {
        Sta.Run(() => InWindow(control =>
        {
            control.Duration = 180;
            RaiseMouse(control, Mouse.PreviewMouseDownEvent);
            Pump(45);
            RaiseMouse(control, Mouse.PreviewMouseUpEvent);
            Pump(320);
            Assert.IsFalse(control.IsConfirmed);
            Assert.IsFalse(control.IsHolding);
            Assert.AreEqual(0d, control.Progress, .05);
        }));
    }

    [TestMethod]
    public void HoldingToDurationRaisesConfirmationOnce()
    {
        Sta.Run(() => InWindow(control =>
        {
            control.Duration = 90;
            var calls = 0;
            control.ConfirmedRaised += (_, _) => calls++;
            RaiseMouse(control, Mouse.PreviewMouseDownEvent);
            Pump(180);
            Assert.IsTrue(control.IsConfirmed);
            Assert.AreEqual(1, calls);
            RaiseMouse(control, Mouse.PreviewMouseUpEvent);
            Assert.IsTrue(control.IsConfirmed);
        }));
    }

    private static void InWindow(Action<HoldToConfirm> action)
    {
        var control = new HoldToConfirm { Label = "Hold to delete", Width = 190, Height = 44 };
        var window = new Window { Content = control, Width = 280, Height = 120, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(control); }
        finally { window.Close(); Sta.Pump(); }
    }

    private static void RaiseMouse(HoldToConfirm control, RoutedEvent routedEvent)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routedEvent, Source = control };
        control.RaiseEvent(args);
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}

