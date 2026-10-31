using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_List", Type = typeof(ListBox))]
public class TimePicker : Control
{
    // 时间列表按 MinuteIncrement 生成，Popup 选择后保持原生 TimeSpan 绑定语义。
    public static readonly DependencyProperty SelectedTimeProperty = DependencyProperty.Register(nameof(SelectedTime), typeof(TimeSpan?), typeof(TimePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TimePicker), new FrameworkPropertyMetadata("Time"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TimePicker), new FrameworkPropertyMetadata("Select time"));
    public static readonly DependencyProperty MinuteIncrementProperty = DependencyProperty.Register(nameof(MinuteIncrement), typeof(int), typeof(TimePicker), new FrameworkPropertyMetadata(30), v => v is int n && n > 0 && n <= 60);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(TimePicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    public static readonly RoutedEvent SelectedTimeChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedTimeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TimePicker));
    private Border? _surface; private Popup? _popup; private ListBox? _list;
    public TimeSpan? SelectedTime { get => (TimeSpan?)GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public int MinuteIncrement { get => (int)GetValue(MinuteIncrementProperty); set => SetValue(MinuteIncrementProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public event RoutedEventHandler SelectedTimeChanged { add => AddHandler(SelectedTimeChangedEvent, value); remove => RemoveHandler(SelectedTimeChangedEvent, value); }
    static TimePicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(typeof(TimePicker))); }
    public TimePicker() { Loaded += (_, _) => RefreshItems(); }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _popup = GetTemplateChild("PART_Popup") as Popup; _list = GetTemplateChild("PART_List") as ListBox;
        if (_surface is not null) _surface.MouseLeftButtonDown += SurfaceClick; if (_list is not null) _list.SelectionChanged += ListSelectionChanged; RefreshItems();
    }
    private void ReleaseTemplate() { if (_surface is not null) _surface.MouseLeftButtonDown -= SurfaceClick; if (_list is not null) _list.SelectionChanged -= ListSelectionChanged; _surface = null; _popup = null; _list = null; }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (TimePicker)d; c.RefreshItems(); c.RaiseEvent(new RoutedEventArgs(SelectedTimeChangedEvent, c)); }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (TimePicker)d; if (c._popup is not null) c._popup.IsOpen = (bool)e.NewValue; }
    private void SurfaceClick(object? sender, MouseButtonEventArgs e) { if (IsEnabled) { SetCurrentValue(IsOpenProperty, !IsOpen); e.Handled = true; } }
    private void RefreshItems()
    {
        if (_list is null) return; var values = new List<TimeSpan>(); for (int minutes = 0; minutes < 24 * 60; minutes += MinuteIncrement) values.Add(TimeSpan.FromMinutes(minutes)); _list.ItemsSource = values; _list.SelectedItem = SelectedTime;
    }
    private void ListSelectionChanged(object? sender, SelectionChangedEventArgs e) { if (_list?.SelectedItem is TimeSpan time) { SetCurrentValue(SelectedTimeProperty, time); SetCurrentValue(IsOpenProperty, false); } }
    protected override AutomationPeer OnCreateAutomationPeer() => new TimePickerPeer(this);
    private sealed class TimePickerPeer(TimePicker owner) : FrameworkElementAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(TimePicker); protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox; protected override string GetNameCore() => owner.Label; }
}
