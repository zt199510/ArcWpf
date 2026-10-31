using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public class MoneyInput : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(decimal?), typeof(MoneyInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty CurrencySymbolProperty = DependencyProperty.Register(nameof(CurrencySymbol), typeof(string), typeof(MoneyInput), new PropertyMetadata("$"));
    public static readonly DependencyProperty DecimalDigitsProperty = DependencyProperty.Register(nameof(DecimalDigits), typeof(int), typeof(MoneyInput), new PropertyMetadata(2, OnVisualChanged, CoerceDigits));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(decimal?), typeof(MoneyInput), new PropertyMetadata(null));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(decimal?), typeof(MoneyInput), new PropertyMetadata(null));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MoneyInput), new PropertyMetadata("Amount"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(MoneyInput), new PropertyMetadata("0.00"));
    private TextBox? _input;
    public decimal? Value { get => (decimal?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string CurrencySymbol { get => (string)GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }
    public int DecimalDigits { get => (int)GetValue(DecimalDigitsProperty); set => SetValue(DecimalDigitsProperty, value); }
    public decimal? Minimum { get => (decimal?)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public decimal? Maximum { get => (decimal?)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    static MoneyInput() { DefaultStyleKeyProperty.OverrideMetadata(typeof(MoneyInput), new FrameworkPropertyMetadata(typeof(MoneyInput))); }
    public override void OnApplyTemplate() { if (_input is not null) { _input.TextChanged -= InputChanged; _input.LostKeyboardFocus -= InputLostFocus; _input.PreviewKeyDown -= InputKeyDown; } base.OnApplyTemplate(); _input = GetTemplateChild("PART_Input") as TextBox; if (_input is not null) { _input.TextChanged += InputChanged; _input.LostKeyboardFocus += InputLostFocus; _input.PreviewKeyDown += InputKeyDown; } RefreshText(); }
    private static object CoerceDigits(DependencyObject d, object value) => Math.Clamp((int)value, 0, 6);
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MoneyInput)d).RefreshText();
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MoneyInput)d).RefreshText();
    private void RefreshText() { if (_input is null || _input.IsKeyboardFocusWithin) return; _input.Text = Value is decimal value ? value.ToString($"N{DecimalDigits}", CultureInfo.CurrentCulture) : string.Empty; }
    private void InputChanged(object? sender, TextChangedEventArgs e) { if (_input is null || !_input.IsKeyboardFocusWithin) return; if (decimal.TryParse(_input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) SetCurrentValue(ValueProperty, Clamp(value)); }
    private void InputLostFocus(object? sender, KeyboardFocusChangedEventArgs e) { Commit(); }
    private void InputKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } else if (e.Key == Key.Escape) { RefreshText(); e.Handled = true; } }
    private void Commit() { if (_input is null) return; if (decimal.TryParse(_input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)) { SetCurrentValue(ValueProperty, Clamp(value)); RefreshText(); } }
    private decimal Clamp(decimal value) => Math.Min(Maximum ?? decimal.MaxValue, Math.Max(Minimum ?? decimal.MinValue, value));
    protected override AutomationPeer OnCreateAutomationPeer() => new MoneyInputPeer(this);
    private sealed class MoneyInputPeer(MoneyInput owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit; protected override string GetNameCore() => owner.Label; }
}
