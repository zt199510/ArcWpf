using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ArcCalendar = ArcWpf.Controls.Calendar;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CalendarMotionTests
{
    [TestMethod]
    public void SelectionGlidesAndRetargetsFromCurrentPositionThenStops() => Sta.Run(() =>
    {
        var calendar = Create();
        var window = new Window { Content = calendar, Width = 400, Height = 460, ShowActivated = false };
        try
        {
            window.Show(); Sta.Pump();
            var indicator = Indicator(calendar);
            var start = indicator.DiscBounds.Location;
            Capture(calendar, "start.png");
            calendar.SelectedDate = new DateTime(2026, 6, 19); Sta.Pump();
            Assert.IsTrue(indicator.IsAnimating);
            PumpFor(100);
            var midway = indicator.DiscBounds.Location;
            Capture(calendar, "moving.png");
            Assert.IsGreaterThan(start.X + 1, midway.X);
            Assert.IsLessThan(Target(calendar).X, midway.X);
            var velocity = indicator.Velocity;
            calendar.SelectedDate = new DateTime(2026, 6, 15);
            // Native data updates synchronously; the visual is retargeted after native layout.
            Assert.AreEqual(midway, indicator.DiscBounds.Location);
            Assert.AreEqual(velocity, indicator.Velocity);
            Sta.Pump();
            PumpUntilStopped(indicator);
            Assert.AreEqual(Target(calendar).X, indicator.DiscBounds.X, .1);
            Assert.AreEqual(Target(calendar).Y, indicator.DiscBounds.Y, .1);
            Assert.AreEqual(1d, indicator.DiscOpacity, .001);
            var settled = indicator.DiscBounds;
            Capture(calendar, "settled.png");
            var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
            theme.Mode = ArcThemeMode.Dark; calendar.UpdateLayout(); PumpFor(80);
            Assert.AreEqual(settled, indicator.DiscBounds);
            Assert.IsFalse(indicator.IsAnimating);
            calendar.SelectedDate = null; Sta.Pump();
            Assert.IsTrue(indicator.IsAnimating);
            PumpFor(250);
            Assert.AreEqual(0d, indicator.DiscOpacity, .001);
            Assert.IsFalse(indicator.IsAnimating);
            calendar.SelectedDate = new DateTime(2026, 6, 12); Sta.Pump();
            PumpUntilStopped(indicator);
            Assert.AreEqual(1d, indicator.DiscOpacity, .001);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ReducedOffAndUnloadSnapWithoutRetainingFrames() => Sta.Run(() =>
    {
        var calendar = Create();
        var window = new Window { Content = calendar, Width = 400, Height = 460, ShowActivated = false };
        try
        {
            window.Show(); Sta.Pump();
            var indicator = Indicator(calendar);
            calendar.SelectedDate = new DateTime(2026, 6, 19); Sta.Pump(); PumpFor(50);
            ArcMotion.SetLevel(calendar, ArcMotionLevel.Reduced); Sta.Pump();
            Assert.IsFalse(indicator.IsAnimating);
            Assert.AreEqual(Target(calendar).X, indicator.DiscBounds.X, .1);
            ArcMotion.SetLevel(calendar, ArcMotionLevel.Off);
            calendar.SelectedDate = new DateTime(2026, 6, 16); Sta.Pump();
            Assert.IsFalse(indicator.IsAnimating);
            Assert.AreEqual(Target(calendar).X, indicator.DiscBounds.X, .1);
            ArcMotion.SetLevel(calendar, ArcMotionLevel.Full);
            calendar.SelectedDate = new DateTime(2026, 6, 19); Sta.Pump();
            Assert.IsTrue(indicator.IsAnimating);
            calendar.Visibility = Visibility.Hidden; Sta.Pump();
            Assert.IsFalse(indicator.IsAnimating);
            calendar.Visibility = Visibility.Visible; Sta.Pump();
            Assert.AreEqual(Target(calendar).X, indicator.DiscBounds.X, .1);
            calendar.SelectedDate = new DateTime(2026, 6, 16); Sta.Pump();
            Assert.IsTrue(indicator.IsAnimating);
            window.Content = null; Sta.Pump();
            Assert.IsFalse(indicator.IsAnimating);
            window.Content = calendar; Sta.Pump();
            indicator = Indicator(calendar);
            Assert.IsFalse(indicator.IsAnimating);
            Assert.AreEqual(Target(calendar).X, indicator.DiscBounds.X, .1);
            calendar.DisplayMode = CalendarMode.Year; Sta.Pump();
            Assert.AreEqual(0d, indicator.DiscOpacity, .001);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void NativeDatePickerCalendarSharesMotionAndCleansUpOnPopupClose() => Sta.Run(() =>
    {
        var picker = new ArcWpf.Controls.DatePicker { SelectedDate = new DateTime(2026, 6, 15) };
        var window = new Window { Content = picker, Width = 400, Height = 200, ShowActivated = false };
        try
        {
            window.Show(); picker.IsDropDownOpen = true; Sta.Pump();
            var calendar = (Calendar)((ContentControl)picker.Template.FindName("PART_CalendarHost", picker)).Content;
            var indicator = Indicator(calendar);
            picker.SelectedDate = new DateTime(2026, 6, 19); Sta.Pump(); PumpFor(50);
            Assert.IsTrue(indicator.IsAnimating);
            Assert.AreSame(picker.BlackoutDates, calendar.BlackoutDates);
            picker.IsDropDownOpen = false; Sta.Pump();
            Assert.IsFalse(indicator.IsAnimating);
            picker.IsDropDownOpen = true; Sta.Pump();
            Assert.AreEqual(Target(calendar).X, Indicator(calendar).DiscBounds.X, .1);
        }
        finally { picker.IsDropDownOpen = false; window.Close(); }
    });

    private static ArcCalendar Create() => new() { Width = 328, VerticalAlignment = VerticalAlignment.Top, DisplayDate = new DateTime(2026, 6, 15), SelectedDate = new DateTime(2026, 6, 15) };
    private static ArcWpf.Controls.CalendarSelectionIndicator Indicator(Calendar calendar)
    {
        var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
        return (ArcWpf.Controls.CalendarSelectionIndicator)item.Template.FindName("PART_SelectionIndicator", item);
    }
    private static Point Target(Calendar calendar)
    {
        var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
        var grid = (Grid)item.Template.FindName("PART_MonthView", item);
        var day = grid.Children.OfType<CalendarDayButton>().Single(d => d.DataContext is DateTime date && date == calendar.SelectedDate);
        return day.TranslatePoint(new Point(), Indicator(calendar));
    }
    private static void PumpUntilStopped(ArcWpf.Controls.CalendarSelectionIndicator indicator)
    {
        var limit = DateTime.UtcNow.AddSeconds(4);
        while (indicator.IsAnimating && DateTime.UtcNow < limit) PumpFor(30);
        Assert.IsFalse(indicator.IsAnimating, "Selection spring must settle and release its render subscription.");
    }
    private static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    private static void Capture(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("ARCWPF_CALENDAR_MOTION_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            drawing.DrawRectangle(System.Windows.Media.Brushes.White, null, bounds);
            drawing.DrawRectangle(new System.Windows.Media.VisualBrush(element), null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(directory, name));
        encoder.Save(file);
    }
}
