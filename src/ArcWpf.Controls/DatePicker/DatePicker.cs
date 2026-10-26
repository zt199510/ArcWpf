using System.Windows;
using System.Windows.Automation;

namespace ArcWpf.Controls;

/// <summary>Arc-styled WPF DatePicker that keeps the framework date, calendar, binding and validation semantics.</summary>
[TemplatePart(Name = "PART_Today", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
public class DatePicker : System.Windows.Controls.DatePicker
{
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
        if (GetTemplateChild("PART_Today") is System.Windows.Controls.Button oldToday) oldToday.Click -= TodayClick;
        if (GetTemplateChild("PART_Clear") is System.Windows.Controls.Button oldClear) oldClear.Click -= ClearClick;
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Today") is System.Windows.Controls.Button today) today.Click += TodayClick;
        if (GetTemplateChild("PART_Clear") is System.Windows.Controls.Button clear) clear.Click += ClearClick;
    }

    private void TodayClick(object sender, RoutedEventArgs e)
    {
        SelectedDate = DateTime.Today;
        IsDropDownOpen = false;
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        SelectedDate = null;
        IsDropDownOpen = false;
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
