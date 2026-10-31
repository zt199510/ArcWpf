using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class ColorPicker : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(Color?), typeof(ColorPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColorPicker), new PropertyMetadata("Color"));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ColorPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorPicker));
    private Border? _surface; private Popup? _popup; private TextBox? _hex; private UniformGrid? _swatches;
    public Color? Value { get => (Color?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public event RoutedEventHandler ValueChanged { add => AddHandler(ValueChangedEvent, value); remove => RemoveHandler(ValueChangedEvent, value); }
    static ColorPicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker))); }
    public override void OnApplyTemplate() { ReleaseTemplate(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _popup = GetTemplateChild("PART_Popup") as Popup; _hex = GetTemplateChild("PART_Hex") as TextBox; _swatches = GetTemplateChild("PART_Swatches") as UniformGrid; if (_surface is not null) _surface.MouseLeftButtonDown += SurfaceClick; if (_hex is not null) _hex.KeyDown += HexKeyDown; Refresh(); }
    private void ReleaseTemplate() { if (_surface is not null) _surface.MouseLeftButtonDown -= SurfaceClick; if (_hex is not null) _hex.KeyDown -= HexKeyDown; _surface = null; _popup = null; _hex = null; _swatches = null; }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (ColorPicker)d; c.Refresh(); c.RaiseEvent(new RoutedEventArgs(ValueChangedEvent, c)); }
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (ColorPicker)d; if (c._popup is not null) c._popup.IsOpen = (bool)e.NewValue; }
    private void SurfaceClick(object? sender, System.Windows.Input.MouseButtonEventArgs e) { SetCurrentValue(IsOpenProperty, !IsOpen); e.Handled = true; }
    private void Refresh() { if (_hex is not null) _hex.Text = Value is Color c ? c.ToString() : "#"; if (_swatches is not null) { _swatches.Children.Clear(); foreach (var color in Presets) { var b = new Button { Width = 28, Height = 28, Margin = new Thickness(3), Background = new SolidColorBrush(color), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), Tag = color }; b.Click += SwatchClick; _swatches.Children.Add(b); } } }
    private void SwatchClick(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: Color color }) { SetCurrentValue(ValueProperty, color); SetCurrentValue(IsOpenProperty, false); } }
    private void HexKeyDown(object? sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter && _hex is not null && TryParse(_hex.Text, out var color)) { SetCurrentValue(ValueProperty, color); SetCurrentValue(IsOpenProperty, false); e.Handled = true; } }
    private static bool TryParse(string text, out Color color) { try { string value = text.Trim(); if (value.StartsWith("#")) value = value[1..]; if (value.Length == 6) value = "FF" + value; color = (Color)ColorConverter.ConvertFromString("#" + value)!; return true; } catch { color = Colors.Transparent; return false; } }
    private static readonly Color[] Presets = [Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.CornflowerBlue, Colors.Purple, Colors.Black, Colors.White];
    protected override AutomationPeer OnCreateAutomationPeer() => new ColorPickerPeer(this);
    private sealed class ColorPickerPeer(ColorPicker owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button; protected override string GetNameCore() => owner.Label; }
}
