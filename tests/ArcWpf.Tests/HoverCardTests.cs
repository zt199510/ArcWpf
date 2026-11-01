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
}
