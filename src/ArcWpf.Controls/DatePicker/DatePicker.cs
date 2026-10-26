using System.Windows;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Input;

namespace ArcWpf.Controls;

/// <summary>Arc-styled WPF DatePicker that keeps the framework date, calendar, binding and validation semantics.</summary>
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
public class DatePicker : System.Windows.Controls.DatePicker
{
    private System.Windows.Controls.Calendar? _calendar;
    private CommandBinding? _todayBinding;
    private System.Windows.Controls.Button? _clear;
    private System.Windows.Controls.ContentControl? _calendarHost;
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(DatePicker), new FrameworkPropertyMetadata(string.Empty, OnLabelChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(DatePicker), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(DatePicker), new FrameworkPropertyMetadata("Select a date"));
    public static readonly DependencyProperty ShowTodayProperty = DependencyProperty.Register(nameof(ShowToday), typeof(bool), typeof(DatePicker), new FrameworkPropertyMetadata(false));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool ShowToday { get => (bool)GetValue(ShowTodayProperty); set => SetValue(ShowTodayProperty, value); }

    static DatePicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DatePicker), new FrameworkPropertyMetadata(typeof(DatePicker)));

    public DatePicker()
    {
        AutomationProperties.SetName(this, Label);
    }

    public override void OnApplyTemplate()
    {
        if (_calendar is not null && _todayBinding is not null) _calendar.CommandBindings.Remove(_todayBinding);
        if (_clear is not null) _clear.Click -= ClearClick;
        if (_calendarHost is not null) _calendarHost.Content = null;
        var popup = GetTemplateChild("PART_Popup") as System.Windows.Controls.Primitives.Popup;
        var surface = popup?.Child;
        _calendarHost = GetTemplateChild("PART_CalendarHost") as System.Windows.Controls.ContentControl;
        base.OnApplyTemplate();
        if (popup?.Child is System.Windows.Controls.Calendar calendar && _calendarHost is not null && surface is not null)
        {
            popup.Child = null;
            _calendarHost.Content = calendar;
            // The framework initializes its private Calendar with HorizontalAlignment.Left.
            // Stretch it inside our padded 320-DIP surface instead of retaining the compact native width.
            calendar.SetCurrentValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            popup.Child = surface;
            _calendar = calendar;
            BindingOperations.SetBinding(calendar, Calendar.ShowTodayProperty, new Binding(nameof(ShowToday)) { Source = this });
            _todayBinding = new CommandBinding(Calendar.SelectTodayCommand,
                (_, e) => { Calendar.SelectToday(calendar); SetCurrentValue(IsDropDownOpenProperty, false); e.Handled = true; },
                (_, e) => { e.CanExecute = Calendar.CanSelectToday(calendar); e.Handled = true; });
            calendar.CommandBindings.Add(_todayBinding);
        }
        _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;
        if (_clear is not null) _clear.Click += ClearClick;
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(SelectedDateProperty, null);
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, Label);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DatePicker picker) AutomationProperties.SetName(picker, e.NewValue as string ?? string.Empty);
    }
}
