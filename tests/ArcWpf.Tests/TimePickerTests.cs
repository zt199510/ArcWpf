using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class TimePickerTests
{
    [TestMethod]
    public void DisplayMatchesArcTwelveAndTwentyFourHourFormats()
    {
        Assert.AreEqual("9:30 AM", TimePicker.Display(new TimeSpan(9, 30, 0), TimePickerFormat.TwelveHour));
        Assert.AreEqual("12:00 AM", TimePicker.Display(TimeSpan.Zero, TimePickerFormat.TwelveHour));
        Assert.AreEqual("12:15 PM", TimePicker.Display(new TimeSpan(12, 15, 0), TimePickerFormat.TwelveHour));
        Assert.AreEqual("14:30", TimePicker.Display(new TimeSpan(14, 30, 0), TimePickerFormat.TwentyFourHour));
    }

    [TestMethod]
    public void DefaultsFollowTheSource() => Sta.Run(() =>
    {
        var picker = new TimePicker();
        Assert.AreEqual(new TimeSpan(9, 0, 0), picker.SelectedTime);
        Assert.AreEqual(15, picker.MinuteStep);
        Assert.AreEqual(TimePickerFormat.TwelveHour, picker.Format);
        Assert.AreEqual("Select a time", picker.Placeholder);
    });

    [TestMethod]
    public void OnlyArcMinuteStepsAreAccepted() => Sta.Run(() =>
    {
        Assert.Throws<ArgumentException>(() => new TimePicker { MinuteStep = 7 });
        Assert.AreEqual(30, new TimePicker { MinuteStep = 30 }.MinuteStep);
    });

    [TestMethod]
    public void KeyboardOpensOnSelectionMovesWithWrapAndChooses() => Sta.Run(() =>
    {
        var picker = new TimePicker { SelectedTime = new TimeSpan(23, 30, 0), MinuteStep = 30 };
        var window = new Window { Content = picker, Width = 360, Height = 240, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); picker.Focus();
            Key(picker, System.Windows.Input.Key.Down); Sta.Pump(); Assert.IsTrue(picker.IsOpen);
            Key(picker, System.Windows.Input.Key.Down); // 23:30 wraps to 00:00
            Key(picker, System.Windows.Input.Key.Enter); Sta.Pump();
            Assert.IsFalse(picker.IsOpen);
            Assert.AreEqual(TimeSpan.Zero, picker.SelectedTime);
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static void Key(UIElement element, Key key) => element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent, Source = element });
}
