using System;
using System.Windows;
using ArcDatePicker = ArcWpf.Controls.DatePicker;

namespace ArcWpf.Tests;

[TestClass]
public sealed class DatePickerTests
{
    [TestMethod]
    public void ArcPropertiesAndNativeDatePropertiesAreBindable() => Sta.Run(() =>
    {
        var picker = Create();
        picker.Label = "Start date";
        picker.Description = "Choose a day";
        picker.Placeholder = "Select a date";
        picker.SelectedDate = new DateTime(2026, 6, 15);
        picker.DisplayDateStart = new DateTime(2026, 1, 1);
        picker.DisplayDateEnd = new DateTime(2026, 12, 31);
        Assert.AreEqual("Start date", picker.Label);
        Assert.AreEqual(new DateTime(2026, 6, 15), picker.SelectedDate);
        Assert.AreEqual(new DateTime(2026, 1, 1), picker.DisplayDateStart);
    });

    [TestMethod]
    public void TemplateLoadsNativeDatePickerParts() => Sta.Run(() =>
    {
        var picker = Create();
        Layout(picker);
        Assert.IsNotNull(picker.Template.FindName("PART_TextBox", picker));
        Assert.IsNotNull(picker.Template.FindName("PART_Button", picker));
        Assert.IsNotNull(picker.Template.FindName("PART_Popup", picker));
        Assert.IsNotNull(picker.Template.FindName("PART_Calendar", picker));
    });

    [TestMethod]
    public void BlackoutDatesRemainNative() => Sta.Run(() =>
    {
        var picker = Create();
        var blocked = new DateTime(2026, 6, 20);
        picker.BlackoutDates.Add(new System.Windows.Controls.CalendarDateRange(blocked));
        Assert.AreEqual(1, picker.BlackoutDates.Count);
        Assert.AreEqual(blocked, picker.BlackoutDates[0].Start);
    });

    [TestMethod]
    public void TodayAndClearButtonsUpdateSelectedDate() => Sta.Run(() =>
    {
        var picker = Create();
        picker.ShowToday = true;
        picker.SelectedDate = new DateTime(2026, 6, 15);
        Layout(picker);
        var today = (System.Windows.Controls.Button)picker.Template.FindName("PART_Today", picker)!;
        var clear = (System.Windows.Controls.Button)picker.Template.FindName("PART_Clear", picker)!;
        today.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(DateTime.Today, picker.SelectedDate);
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.IsNull(picker.SelectedDate);
    });

    private static ArcDatePicker Create()
    {
        var picker = new ArcDatePicker();
        picker.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/DatePicker/Themes/DatePickerTheme.xaml", UriKind.Relative) });
        return picker;
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(400, 220));
        element.Arrange(new Rect(0, 0, 400, 220));
        element.UpdateLayout();
        Sta.Pump();
    }
}
