using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcCalendar = ArcWpf.Controls.Calendar;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CalendarTests
{
    [TestMethod]
    public void MonthGridHasSixWeeksAndSquareDaysAtSupportedWidths() => Sta.Run(() =>
    {
        foreach (var width in new[] { 328d, 286d, 240d })
        {
            var calendar = Create(width);
            Layout(calendar);
            var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
            var grid = (Grid)item.Template.FindName("PART_MonthView", item);
            var days = grid.Children.OfType<CalendarDayButton>().ToArray();
            Assert.HasCount(42, days);
            Assert.AreEqual(width, calendar.ActualWidth, .5);
            foreach (var day in days)
            {
                Assert.IsGreaterThan(25d, day.ActualWidth);
                Assert.AreEqual(day.ActualWidth, day.ActualHeight, .5);
                var point = day.TranslatePoint(new Point(), calendar);
                Assert.IsTrue(point.X >= 0 && point.X + day.ActualWidth <= width + .5);
            }
            Assert.IsGreaterThan(200d, grid.ActualHeight);
        }
    });

    [TestMethod]
    public void HeaderNavigationAndNativeDisplayModesRemainFunctional() => Sta.Run(() =>
    {
        var calendar = Create();
        Layout(calendar);
        var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
        ((Button)item.Template.FindName("PART_NextButton", item)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(7, calendar.DisplayDate.Month);
        ((Button)item.Template.FindName("PART_PreviousButton", item)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(6, calendar.DisplayDate.Month);
        var header = (Button)item.Template.FindName("PART_HeaderButton", item);
        var titleWidth = header.ActualWidth;
        calendar.Width = 286;
        Layout(calendar);
        Assert.AreEqual(titleWidth, header.ActualWidth, .5, "The title hit area must fit its content instead of filling spare header width.");
        header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(CalendarMode.Year, calendar.DisplayMode);
        header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(CalendarMode.Decade, calendar.DisplayMode);
        Layout(calendar);
        var year = (Grid)item.Template.FindName("PART_YearView", item);
        Assert.AreEqual(Visibility.Visible, year.Visibility);
        Assert.HasCount(12, year.Children.OfType<CalendarButton>().ToArray());
        Assert.IsGreaterThan(200d, year.ActualHeight);
    });

    [TestMethod]
    public void TodayPreservesBindingAndHonorsRangeBlackoutAndSelectionMode() => Sta.Run(() =>
    {
        var source = new ArcCalendar();
        var calendar = Create();
        calendar.SetBinding(Calendar.SelectedDateProperty, new Binding("SelectedDate") { Source = source, Mode = BindingMode.TwoWay });
        Layout(calendar);
        Assert.IsTrue(ArcCalendar.SelectTodayCommand.CanExecute(null, calendar));
        ArcCalendar.SelectTodayCommand.Execute(null, calendar);
        Assert.AreEqual(DateTime.Today, source.SelectedDate);
        Assert.IsNotNull(calendar.GetBindingExpression(Calendar.SelectedDateProperty));
        calendar.SetCurrentValue(Calendar.SelectedDateProperty, null);
        calendar.BlackoutDates.Add(new CalendarDateRange(DateTime.Today));
        Assert.IsFalse(ArcCalendar.SelectTodayCommand.CanExecute(null, calendar));
        calendar.BlackoutDates.Clear();
        calendar.DisplayDateEnd = DateTime.Today.AddDays(-1);
        Assert.IsFalse(ArcCalendar.SelectTodayCommand.CanExecute(null, calendar));
        calendar.DisplayDateEnd = null;
        calendar.SelectionMode = CalendarSelectionMode.None;
        Assert.IsFalse(ArcCalendar.SelectTodayCommand.CanExecute(null, calendar));
    });

    [TestMethod]
    public void DatePickerUsesSharedCalendarTemplateAndDynamicTheme() => Sta.Run(() =>
    {
        var picker = new ArcWpf.Controls.DatePicker { ShowToday = true, SelectedDate = new DateTime(2026, 6, 15) };
        var window = new Window { Content = picker, Width = 420, Height = 250, ShowActivated = false };
        try
        {
            window.Show();
            picker.IsDropDownOpen = true;
            Sta.Pump();
            var host = (ContentControl)picker.Template.FindName("PART_CalendarHost", picker);
            var calendar = (Calendar)host.Content;
            Assert.AreSame(picker.BlackoutDates, calendar.BlackoutDates);
            Assert.AreSame(picker.CalendarStyle, calendar.Style);
            Assert.IsNotNull(calendar.CalendarDayButtonStyle);
            Assert.IsTrue(ArcCalendar.GetShowToday(calendar), "Attached ShowToday must follow the picker");
            var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
            var today = (Button)item.Template.FindName("PART_Today", item);
            Assert.AreEqual(Visibility.Visible, today.Visibility);
            Assert.AreSame(calendar, today.CommandTarget);
            var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
            Assert.AreEqual(320d, ((FrameworkElement)popup.Child).ActualWidth, .5);
            Assert.AreEqual(286d, calendar.ActualWidth, .5);
            Capture((FrameworkElement)popup.Child, "datepicker-light.png");
            var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
            var previous = calendar.Foreground;
            theme.Mode = ArcThemeMode.Dark;
            Sta.Pump();
            Assert.AreNotSame(previous, calendar.Foreground);
            Capture((FrameworkElement)popup.Child, "datepicker-dark.png");
            picker.ShowToday = false;
            Sta.Pump();
            Assert.AreEqual(Visibility.Collapsed, today.Visibility);
        }
        finally { picker.IsDropDownOpen = false; window.Close(); }
    });

    [TestMethod]
    public void StandaloneStylesSelectedTodayAndBlackoutDates() => Sta.Run(() =>
    {
        var calendar = Create();
        calendar.SelectedDate = new DateTime(2026, 6, 15);
        calendar.BlackoutDates.Add(new CalendarDateRange(new DateTime(2026, 6, 20)));
        Layout(calendar);
        var item = (CalendarItem)calendar.Template.FindName("PART_CalendarItem", calendar);
        var grid = (Grid)item.Template.FindName("PART_MonthView", item);
        var selected = grid.Children.OfType<CalendarDayButton>().Single(d => d.IsSelected);
        var disc = (System.Windows.Shapes.Ellipse)selected.Template.FindName("Disc", selected);
        Assert.AreEqual(calendar.FindResource("ArcWpf.Brush.ControlOn"), disc.Fill);
        Assert.IsTrue(grid.Children.OfType<CalendarDayButton>().Any(d => d.IsBlackedOut));
        Capture(calendar, "calendar-light.png");
        calendar.SelectedDate = DateTime.Today;
        calendar.DisplayDate = DateTime.Today;
        Layout(calendar);
        var today = grid.Children.OfType<CalendarDayButton>().Single(d => d.IsToday);
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)today.Template.FindName("TodayDot", today)).Visibility);
    });

    private static ArcCalendar Create(double width = 328)
    {
        var calendar = new ArcCalendar { Width = width, ShowToday = true, DisplayDate = new DateTime(2026, 6, 15) };
        calendar.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Calendar/Themes/CalendarTheme.xaml", UriKind.Relative) });
        return calendar;
    }
    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(400, double.PositiveInfinity));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.UpdateLayout();
        Sta.Pump();
        element.UpdateLayout();
    }
    private static void Capture(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("ARCWPF_VERIFICATION_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name));
        encoder.Save(file);
    }
}
