using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class Rating : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Rating), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(Rating), new FrameworkPropertyMetadata(5, OnVisualChanged, CoerceMaximum));
    public static readonly DependencyProperty AllowHalfProperty = DependencyProperty.Register(nameof(AllowHalf), typeof(bool), typeof(Rating), new PropertyMetadata(false));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(Rating), new PropertyMetadata(false));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Rating), new PropertyMetadata("Rating"));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Rating));
    private StackPanel? _panel;
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public bool AllowHalf { get => (bool)GetValue(AllowHalfProperty); set => SetValue(AllowHalfProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event RoutedEventHandler ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }
    static Rating() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Rating), new FrameworkPropertyMetadata(typeof(Rating))); }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _panel = GetTemplateChild("PART_Panel") as StackPanel; Refresh(); }
    private static object CoerceMaximum(DependencyObject d, object value) => Math.Clamp((int)value, 1, 10);
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (Rating)d; c.Refresh(); c.RaiseEvent(new RoutedEventArgs(ValueChangedEvent, c)); }
    private void Refresh() { if (_panel is null) return; _panel.Children.Clear(); for (int i = 1; i <= Maximum; i++) { var button = new Button { Content = i <= Value ? "★" : "☆", Tag = i, FontSize = 22, Width = 30, Height = 30, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = FindResource("ArcWpf.Brush.Warning") as System.Windows.Media.Brush }; button.IsEnabled = !IsReadOnly; button.Click += StarClick; _panel.Children.Add(button); } }
    private void StarClick(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: int value }) { SetCurrentValue(ValueProperty, value); e.Handled = true; } }
    protected override AutomationPeer OnCreateAutomationPeer() => new RatingPeer(this);
    private sealed class RatingPeer(Rating owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider; protected override string GetNameCore() => owner.Label; protected override string GetItemStatusCore() => $"{owner.Value:0} of {owner.Maximum}"; }
}
