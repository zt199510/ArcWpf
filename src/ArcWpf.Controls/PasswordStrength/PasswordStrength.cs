using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class PasswordStrength : Control
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string), typeof(PasswordStrength), new FrameworkPropertyMetadata(string.Empty, OnPasswordChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(PasswordStrength), new PropertyMetadata("Password strength"));
    public static readonly DependencyProperty ShowRulesProperty = DependencyProperty.Register(nameof(ShowRules), typeof(bool), typeof(PasswordStrength), new PropertyMetadata(true));
    private static readonly DependencyPropertyKey ScorePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Score), typeof(int), typeof(PasswordStrength), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty ScoreProperty = ScorePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey StrengthLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(StrengthLabel), typeof(string), typeof(PasswordStrength), new FrameworkPropertyMetadata("Weak"));
    public static readonly DependencyProperty StrengthLabelProperty = StrengthLabelPropertyKey.DependencyProperty;
    private TextBlock? _label; private ProgressBar? _bar;
    public string Password { get => (string)GetValue(PasswordProperty); set => SetValue(PasswordProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool ShowRules { get => (bool)GetValue(ShowRulesProperty); set => SetValue(ShowRulesProperty, value); }
    public int Score => (int)GetValue(ScoreProperty);
    public string StrengthLabel => (string)GetValue(StrengthLabelProperty);
    static PasswordStrength() { DefaultStyleKeyProperty.OverrideMetadata(typeof(PasswordStrength), new FrameworkPropertyMetadata(typeof(PasswordStrength))); }
    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (PasswordStrength)d; int score = Calculate(c.Password); c.SetValue(ScorePropertyKey, score); c.SetValue(StrengthLabelPropertyKey, score switch { 0 or 1 => "Weak", 2 => "Fair", 3 => "Good", _ => "Strong" }); c.RefreshVisual(); }
    private static int Calculate(string value) { if (string.IsNullOrEmpty(value)) return 0; int score = 0; if (value.Length >= 8) score++; if (value.Any(char.IsUpper) && value.Any(char.IsLower)) score++; if (value.Any(char.IsDigit)) score++; if (value.Any(ch => !char.IsLetterOrDigit(ch))) score++; return score; }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _label = GetTemplateChild("PART_Label") as TextBlock; _bar = GetTemplateChild("PART_Bar") as ProgressBar; RefreshVisual(); }
    private void RefreshVisual() { if (_label is not null) _label.Text = $"{Label}: {StrengthLabel}"; if (_bar is not null) _bar.Value = Score; }
    protected override AutomationPeer OnCreateAutomationPeer() => new PasswordStrengthPeer(this);
    private sealed class PasswordStrengthPeer(PasswordStrength owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar; protected override string GetNameCore() => owner.Label; protected override string GetHelpTextCore() => owner.StrengthLabel; }
}
