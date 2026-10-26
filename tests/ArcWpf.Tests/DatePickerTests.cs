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
        var host = (System.Windows.Controls.ContentControl)picker.Template.FindName("PART_CalendarHost", picker)!;
        var calendar = (System.Windows.Controls.Calendar)host.Content;
        Assert.AreSame(picker.BlackoutDates, calendar.BlackoutDates);
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
        var calendar = (System.Windows.Controls.Calendar)((System.Windows.Controls.ContentControl)picker.Template.FindName("PART_CalendarHost", picker)!).Content;
        var clear = (System.Windows.Controls.Button)picker.Template.FindName("PART_Clear", picker)!;
        ArcWpf.Controls.Calendar.SelectTodayCommand.Execute(null, calendar);
        Assert.AreEqual(DateTime.Today, picker.SelectedDate);
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.IsNull(picker.SelectedDate);
    });

    [TestMethod]
    public void DropDownButtonOpensNativePopup() => Sta.Run(() =>
    {
        var picker = Create();
        var window = new Window { Content = picker, Width = 400, Height = 240, ShowActivated = false };
        window.Show();
        Layout(picker);
        var button = (System.Windows.Controls.Button)picker.Template.FindName("PART_Button", picker)!;
        var popup = (System.Windows.Controls.Primitives.Popup)picker.Template.FindName("PART_Popup", picker)!;
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(picker.IsDropDownOpen);
        Assert.IsTrue(popup.IsOpen);
        popup.IsOpen = false;
        Sta.Pump();
        var textBox = (System.Windows.Controls.Primitives.DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker)!;
        textBox.Focus();
        textBox.Text = "12/15/2026";
        var source = System.Windows.PresentationSource.FromVisual(textBox)!;
        textBox.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
        Assert.IsNotNull(picker.SelectedDate);
        picker.SetCurrentValue(ArcDatePicker.IsDropDownOpenProperty, true);
        Sta.Pump();
        Assert.IsTrue(popup.IsOpen);
        picker.SetCurrentValue(ArcDatePicker.IsDropDownOpenProperty, false);
        window.Close();
    });

    private static ArcDatePicker Create()
    {
        var picker = new ArcDatePicker();
        picker.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/DatePicker/Themes/DatePickerTheme.xaml", UriKind.Relative) });
        return picker;
    }

    [TestMethod]
    public void ClearKeepsBindingAndTodayRespectsBlackout() => Sta.Run(() =>
    {
        var source = new ArcDatePicker { SelectedDate = DateTime.Today.AddDays(-1) };
        var picker = Create();
        picker.SetBinding(ArcDatePicker.SelectedDateProperty, new System.Windows.Data.Binding("SelectedDate") { Source = source, Mode = System.Windows.Data.BindingMode.TwoWay });
        picker.BlackoutDates.Add(new System.Windows.Controls.CalendarDateRange(DateTime.Today));
        Layout(picker);
        var calendar = (System.Windows.Controls.Calendar)((System.Windows.Controls.ContentControl)picker.Template.FindName("PART_CalendarHost", picker)!).Content;
        Assert.IsFalse(ArcWpf.Controls.Calendar.SelectTodayCommand.CanExecute(null, calendar));
        ArcWpf.Controls.Calendar.SelectTodayCommand.Execute(null, calendar);
        Assert.AreEqual(source.SelectedDate, picker.SelectedDate);
        var clear = (System.Windows.Controls.Button)picker.Template.FindName("PART_Clear", picker)!;
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.IsNotNull(picker.GetBindingExpression(ArcDatePicker.SelectedDateProperty));
        Assert.IsNull(source.SelectedDate);
    });

    [TestMethod]
    public void CultureParsingAndInvalidInputKeepNativeValidation() => Sta.Run(() =>
    {
        var picker = Create();
        picker.Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB");
        Layout(picker);
        picker.Text = "31/12/2026";
        Assert.AreEqual(new DateTime(2026, 12, 31), picker.SelectedDate);
        var errors = 0;
        picker.DateValidationError += (_, e) => { errors++; e.ThrowException = false; };
        picker.Text = "not a date";
        Assert.AreEqual(1, errors);
        Assert.AreEqual(new DateTime(2026, 12, 31), picker.SelectedDate);
    });

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(400, 220));
        element.Arrange(new Rect(0, 0, 400, 220));
        element.UpdateLayout();
        Sta.Pump();
    }
}
