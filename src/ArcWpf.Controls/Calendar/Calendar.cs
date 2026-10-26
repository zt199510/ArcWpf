using System.Windows;
using System.Windows.Input;
using NativeCalendar = System.Windows.Controls.Calendar;

namespace ArcWpf.Controls;

/// <summary>A native WPF calendar with the shared Arc calendar presentation.</summary>
public class Calendar : NativeCalendar
{
    public static readonly DependencyProperty ShowTodayProperty = DependencyProperty.RegisterAttached(
        "ShowToday", typeof(bool), typeof(Calendar), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly RoutedUICommand SelectTodayCommand = new("Select today", nameof(SelectTodayCommand), typeof(Calendar));

    public bool ShowToday { get => GetShowToday(this); set => SetShowToday(this, value); }
    public static bool GetShowToday(DependencyObject element) => (bool)element.GetValue(ShowTodayProperty);
    public static void SetShowToday(DependencyObject element, bool value) => element.SetValue(ShowTodayProperty, value);

    static Calendar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Calendar), new FrameworkPropertyMetadata(typeof(Calendar)));
        // DatePicker owns a framework Calendar; the command and attached option work on that instance too.
        CommandManager.RegisterClassCommandBinding(typeof(NativeCalendar), new CommandBinding(SelectTodayCommand,
            (sender, e) => { SelectToday((NativeCalendar)sender); e.Handled = true; },
            (sender, e) => { e.CanExecute = CanSelectToday((NativeCalendar)sender); e.Handled = true; }));
    }

    internal static bool CanSelectToday(NativeCalendar calendar) => calendar.IsEnabled
        && calendar.SelectionMode == System.Windows.Controls.CalendarSelectionMode.SingleDate
        && !calendar.BlackoutDates.Contains(DateTime.Today)
        && !(DateTime.Today < calendar.DisplayDateStart) && !(DateTime.Today > calendar.DisplayDateEnd);

    internal static void SelectToday(NativeCalendar calendar)
    {
        if (!CanSelectToday(calendar)) return;
        calendar.SetCurrentValue(SelectedDateProperty, DateTime.Today);
        calendar.SetCurrentValue(DisplayDateProperty, DateTime.Today);
        calendar.SetCurrentValue(DisplayModeProperty, System.Windows.Controls.CalendarMode.Month);
    }
}
