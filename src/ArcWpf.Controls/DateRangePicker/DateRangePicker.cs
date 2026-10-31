using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_StartCalendar", Type = typeof(Calendar))]
[TemplatePart(Name = "PART_EndCalendar", Type = typeof(Calendar))]
[TemplatePart(Name = "PART_Clear", Type = typeof(System.Windows.Controls.Button))]
public class DateRangePicker : Control
{
    // 两个原生 Calendar 共用同一组边界和禁用日期，避免重新实现日期网格。
    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(nameof(StartDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));
    public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(nameof(EndDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(DateRangePicker), new FrameworkPropertyMetadata("Date range"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(DateRangePicker), new FrameworkPropertyMetadata("Select dates"));
    public static readonly DependencyProperty MinimumDateProperty = DependencyProperty.Register(nameof(MinimumDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, OnBoundaryChanged));
    public static readonly DependencyProperty MaximumDateProperty = DependencyProperty.Register(nameof(MaximumDate), typeof(DateTime?), typeof(DateRangePicker), new FrameworkPropertyMetadata(null, OnBoundaryChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DateRangePicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    public static readonly RoutedEvent RangeChangedEvent = EventManager.RegisterRoutedEvent(nameof(RangeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DateRangePicker));

    private Border? _surface; private Popup? _popup; private Calendar? _startCalendar, _endCalendar; private System.Windows.Controls.Button? _clear; private bool _selectingEnd;
    public DateTime? StartDate { get => (DateTime?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DateTime? EndDate { get => (DateTime?)GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public ObservableCollection<DateTime> BlackoutDates { get; } = [];
    public event RoutedEventHandler RangeChanged { add => AddHandler(RangeChangedEvent, value); remove => RemoveHandler(RangeChangedEvent, value); }

    static DateRangePicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(DateRangePicker), new FrameworkPropertyMetadata(typeof(DateRangePicker))); }
    public DateRangePicker() { BlackoutDates.CollectionChanged += (_, _) => ApplyBounds(); Loaded += (_, _) => ApplyBounds(); }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _popup = GetTemplateChild("PART_Popup") as Popup; _startCalendar = GetTemplateChild("PART_StartCalendar") as Calendar; _endCalendar = GetTemplateChild("PART_EndCalendar") as Calendar; _clear = GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;
        if (_surface is not null) _surface.MouseLeftButtonDown += SurfaceClick; if (_popup is not null) { _popup.Opened += (_, _) => ApplyBounds(); _popup.Closed += (_, _) => { if (IsOpen) SetCurrentValue(IsOpenProperty, false); }; } if (_startCalendar is not null) _startCalendar.SelectedDatesChanged += CalendarSelected; if (_endCalendar is not null) _endCalendar.SelectedDatesChanged += CalendarSelected; if (_clear is not null) _clear.Click += ClearClick; ApplyBounds();
    }
    private void ReleaseTemplate() { if (_surface is not null) _surface.MouseLeftButtonDown -= SurfaceClick; if (_startCalendar is not null) _startCalendar.SelectedDatesChanged -= CalendarSelected; if (_endCalendar is not null) _endCalendar.SelectedDatesChanged -= CalendarSelected; if (_clear is not null) _clear.Click -= ClearClick; _surface = null; _popup = null; _startCalendar = _endCalendar = null; _clear = null; }
    private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (DateRangePicker)d; c.ApplyBounds(); c.RaiseEvent(new RoutedEventArgs(RangeChangedEvent, c)); }
    private static void OnBoundaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DateRangePicker)d).ApplyBounds();
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (DateRangePicker)d; if (c._popup is not null) c._popup.IsOpen = (bool)e.NewValue; }
    private void SurfaceClick(object? sender, MouseButtonEventArgs e) { if (IsEnabled) { SetCurrentValue(IsOpenProperty, !_popup?.IsOpen); e.Handled = true; } }
    // 第一次选择开始日期，第二次选择结束日期；反向选择时自动交换两端。
    private void CalendarSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not DateTime date) return;
        if (!_selectingEnd || StartDate is null) { SetCurrentValue(StartDateProperty, date.Date); SetCurrentValue(EndDateProperty, null); _selectingEnd = true; _endCalendar?.Focus(); }
        else { var start = StartDate.Value; if (date.Date < start) { SetCurrentValue(StartDateProperty, date.Date); SetCurrentValue(EndDateProperty, start); } else SetCurrentValue(EndDateProperty, date.Date); _selectingEnd = false; SetCurrentValue(IsOpenProperty, false); }
    }
    private void ClearClick(object? sender, RoutedEventArgs e) { SetCurrentValue(StartDateProperty, null); SetCurrentValue(EndDateProperty, null); _selectingEnd = false; SetCurrentValue(IsOpenProperty, false); e.Handled = true; }
    private void ApplyBounds()
    {
        foreach (var calendar in new[] { _startCalendar, _endCalendar }.Where(c => c is not null))
        {
            calendar!.DisplayDateStart = MinimumDate; calendar.DisplayDateEnd = MaximumDate; calendar.BlackoutDates.Clear(); foreach (var date in BlackoutDates) calendar.BlackoutDates.Add(new CalendarDateRange(date)); calendar.SelectedDate = ReferenceEquals(calendar, _startCalendar) ? StartDate : EndDate; if (StartDate is not null && ReferenceEquals(calendar, _endCalendar)) calendar.DisplayDate = StartDate.Value.AddMonths(1);
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new DateRangePickerPeer(this);
    private sealed class DateRangePickerPeer(DateRangePicker owner) : FrameworkElementAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(DateRangePicker); protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox; protected override string GetNameCore() => owner.Label; }
}
