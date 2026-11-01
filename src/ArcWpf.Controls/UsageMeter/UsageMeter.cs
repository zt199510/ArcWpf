using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace ArcWpf.Controls;

public class UsageMeter : Control
{
    public static readonly DependencyProperty UsedProperty = DependencyProperty.Register(nameof(Used), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d, OnVisualChanged));
    public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(100d, OnVisualChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UsageMeter), new PropertyMetadata("Usage"));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(UsageMeter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty WarningThresholdProperty = DependencyProperty.Register(nameof(WarningThreshold), typeof(double), typeof(UsageMeter), new PropertyMetadata(.8d));
    private static readonly DependencyPropertyKey PercentagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Percentage), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty PercentageProperty = PercentagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsWarningPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsWarning), typeof(bool), typeof(UsageMeter), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsWarningProperty = IsWarningPropertyKey.DependencyProperty;
    private ProgressBar? _bar; private TextBlock? _value;
    public double Used { get => (double)GetValue(UsedProperty); set => SetValue(UsedProperty, value); }
    public double Limit { get => (double)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double WarningThreshold { get => (double)GetValue(WarningThresholdProperty); set => SetValue(WarningThresholdProperty, value); }
    public double Percentage => (double)GetValue(PercentageProperty);
    public bool IsWarning => (bool)GetValue(IsWarningProperty);
    static UsageMeter() { DefaultStyleKeyProperty.OverrideMetadata(typeof(UsageMeter), new FrameworkPropertyMetadata(typeof(UsageMeter))); }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _bar = GetTemplateChild("PART_Bar") as ProgressBar; _value = GetTemplateChild("PART_Value") as TextBlock; Refresh(); }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UsageMeter)d).Refresh();
    private void Refresh() { double percent = Limit > 0 ? Math.Clamp(Used / Limit, 0, 1) : 0; SetValue(PercentagePropertyKey, percent); SetValue(IsWarningPropertyKey, percent >= WarningThreshold); if (_bar is not null) _bar.Value = percent * 100; if (_value is not null) _value.Text = $"{Used.ToString("0.##", CultureInfo.CurrentCulture)} / {Limit.ToString("0.##", CultureInfo.CurrentCulture)} {Unit}".Trim(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new UsageMeterPeer(this);
    private sealed class UsageMeterPeer(UsageMeter owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar; protected override string GetNameCore() => owner.Label; protected override string GetItemStatusCore() => $"{owner.Percentage:P0}"; }
}
