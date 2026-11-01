using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class UsageMeter : Control
{
    public static readonly DependencyProperty UsedProperty = DependencyProperty.Register(nameof(Used), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(100d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number >= 0);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(UsageMeter), new PropertyMetadata("Usage"));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(UsageMeter), new PropertyMetadata(string.Empty, OnVisualChanged));
    public static readonly DependencyProperty WarningThresholdProperty = DependencyProperty.Register(nameof(WarningThreshold), typeof(double), typeof(UsageMeter), new PropertyMetadata(.8d, OnVisualChanged), value => value is double number && double.IsFinite(number) && number is >= 0 and <= 1);
    private static readonly DependencyPropertyKey PercentagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Percentage), typeof(double), typeof(UsageMeter), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty PercentageProperty = PercentagePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsWarningPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsWarning), typeof(bool), typeof(UsageMeter), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsWarningProperty = IsWarningPropertyKey.DependencyProperty;
    private Border? _fill;
    private ScaleTransform? _scale;
    private readonly OwnedMotion _motion = new();
    private TextBlock? _value;
    public double Used { get => (double)GetValue(UsedProperty); set => SetValue(UsedProperty, value); }
    public double Limit { get => (double)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double WarningThreshold { get => (double)GetValue(WarningThresholdProperty); set => SetValue(WarningThresholdProperty, value); }
    public double Percentage => (double)GetValue(PercentageProperty);
    public bool IsWarning => (bool)GetValue(IsWarningProperty);
    static UsageMeter() { DefaultStyleKeyProperty.OverrideMetadata(typeof(UsageMeter), new FrameworkPropertyMetadata(typeof(UsageMeter))); }
    public UsageMeter() { Unloaded += (_, _) => _motion.Stop(); }
    public override void OnApplyTemplate()
    {
        _motion.Stop();
        base.OnApplyTemplate();
        _fill = GetTemplateChild("PART_Fill") as Border;
        _value = GetTemplateChild("PART_Value") as TextBlock;
        _scale = _fill is null ? null : new ScaleTransform(0, 1);
        if (_fill is not null) _fill.RenderTransform = _scale;
        Refresh(false);
    }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UsageMeter)d).Refresh();
    private void Refresh() => Refresh(true);
    private void Refresh(bool animate)
    {
        double percent = Limit > 0 ? Math.Clamp(Used / Limit, 0, 1) : 0;
        SetValue(PercentagePropertyKey, percent);
        SetValue(IsWarningPropertyKey, percent >= WarningThreshold);
        if (_scale is not null)
        {
            var level = ArcMotion.GetLevel(this);
            bool moving = animate && IsLoaded && IsVisible && level != ArcMotionLevel.Off;
            _motion.Double(_scale, ScaleTransform.ScaleXProperty, percent, level == ArcMotionLevel.Full ? .35 : .12, moving);
        }
        if (_value is not null) _value.Text = $"{Used.ToString("0.##", CultureInfo.CurrentCulture)} / {Limit.ToString("0.##", CultureInfo.CurrentCulture)} {Unit}".Trim();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _motion is not null) { _motion.Stop(); Refresh(false); }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new UsageMeterPeer(this);
    private sealed class UsageMeterPeer(UsageMeter owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar; protected override string GetNameCore() => owner.Label; protected override string GetItemStatusCore() => $"{owner.Percentage:P0}"; }
}
