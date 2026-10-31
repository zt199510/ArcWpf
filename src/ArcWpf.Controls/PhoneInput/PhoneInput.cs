using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public class PhoneInput : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(PhoneInput), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty CountryCodeProperty = DependencyProperty.Register(nameof(CountryCode), typeof(string), typeof(PhoneInput), new FrameworkPropertyMetadata("+1", OnVisualChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PhoneInput), new PropertyMetadata("Phone number"));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(PhoneInput), new PropertyMetadata("(555) 123-4567"));
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(PhoneInput), new PropertyMetadata(string.Empty, OnVisualChanged));
    private static readonly DependencyPropertyKey IsValidPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsValid), typeof(bool), typeof(PhoneInput), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsValidProperty = IsValidPropertyKey.DependencyProperty;
    private TextBox? _input;
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string CountryCode { get => (string)GetValue(CountryCodeProperty); set => SetValue(CountryCodeProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool IsValid => (bool)GetValue(IsValidProperty);
    static PhoneInput() { DefaultStyleKeyProperty.OverrideMetadata(typeof(PhoneInput), new FrameworkPropertyMetadata(typeof(PhoneInput))); }
    public override void OnApplyTemplate() { if (_input is not null) { _input.TextChanged -= InputChanged; _input.PreviewKeyDown -= InputKeyDown; _input.LostKeyboardFocus -= InputLostFocus; } base.OnApplyTemplate(); _input = GetTemplateChild("PART_Input") as TextBox; if (_input is not null) { _input.TextChanged += InputChanged; _input.PreviewKeyDown += InputKeyDown; _input.LostKeyboardFocus += InputLostFocus; } RefreshText(); }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PhoneInput)d).RefreshText();
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (PhoneInput)d; c.SetValue(IsValidPropertyKey, Digits((string)e.NewValue).Length >= 7); c.RefreshText(); }
    private void InputChanged(object? sender, TextChangedEventArgs e) { if (_input is null || !_input.IsKeyboardFocusWithin) return; string digits = Digits(_input.Text); SetCurrentValue(ValueProperty, digits); SetValue(IsValidPropertyKey, digits.Length >= 7); }
    private void InputKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) { InputLostFocus(sender, e); e.Handled = true; } else if (e.Key == Key.Escape) { RefreshText(); e.Handled = true; } }
    private void InputLostFocus(object? sender, RoutedEventArgs e) { RefreshText(); }
    private void RefreshText() { if (_input is null || _input.IsKeyboardFocusWithin) return; _input.Text = Format(Value); }
    private string Format(string value) { string d = Digits(value); if (d.Length == 10) return $"({d[..3]}) {d.Substring(3, 3)}-{d[6..]}"; return d; }
    private static string Digits(string value) { var b = new StringBuilder(); foreach (char c in value ?? "") if (char.IsDigit(c)) b.Append(c); return b.ToString(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new PhoneInputPeer(this);
    private sealed class PhoneInputPeer(PhoneInput owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit; protected override string GetNameCore() => owner.Label; }
}
