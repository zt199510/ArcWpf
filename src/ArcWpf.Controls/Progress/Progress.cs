using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Fill", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Label", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Value", Type = typeof(TextBlock))]
public class Progress : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Progress), new FrameworkPropertyMetadata(0d, OnProgressPropertyChanged, CoerceValue));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(double), typeof(Progress), new FrameworkPropertyMetadata(100d, OnProgressPropertyChanged, CoerceMax));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Progress), new FrameworkPropertyMetadata(string.Empty, OnProgressPropertyChanged));
    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(Progress), new FrameworkPropertyMetadata(false, OnProgressPropertyChanged));
    private FrameworkElement? _fill;
    private TextBlock? _label;
    private TextBlock? _value;
    private ScaleTransform? _scale;

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool ShowValue { get => (bool)GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    public double Percentage => Max <= 0 ? 0 : Math.Clamp(Value / Max, 0, 1);

    static Progress() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Progress), new FrameworkPropertyMetadata(typeof(Progress)));
    public Progress() => AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Progress" : Label);

    public override void OnApplyTemplate()
    {
        if (_fill is not null) _fill.BeginAnimation(OpacityProperty, null);
        base.OnApplyTemplate();
        _fill = GetTemplateChild("PART_Fill") as FrameworkElement;
        _label = GetTemplateChild("PART_Label") as TextBlock;
        _value = GetTemplateChild("PART_Value") as TextBlock;
        _scale = new ScaleTransform(0, 1);
        if (_fill is not null) { _fill.RenderTransformOrigin = new Point(0, .5); _fill.RenderTransform = _scale; }
        UpdateVisual(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Progress" : Label);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var progress = (Progress)d;
        var value = double.IsFinite((double)baseValue) ? Math.Max(0, (double)baseValue) : 0d;
        return Math.Min(value, progress.Max);
    }
    private static object CoerceMax(DependencyObject d, object baseValue) => double.IsFinite((double)baseValue) ? Math.Max(0, (double)baseValue) : 100d;
    private static void OnProgressPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var progress = (Progress)d;
        if (e.Property == MaxProperty) progress.CoerceValue(ValueProperty);
        progress.UpdateVisual(true);
    }

    private void UpdateVisual(bool animate)
    {
        if (_fill is null || _scale is null) return;
        var level = ArcMotion.GetLevel(this);
        var target = Percentage;
        var current = _scale.ScaleX;
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        // Keep the logical target as the base value. Without this, clearing the
        // previous clock restores the initial 0 value and every update animates
        // from the beginning instead of continuing from the current fill.
        _scale.ScaleX = target;
        if (animate && IsLoaded && level != ArcMotionLevel.Off)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation(current, target, level == ArcMotionLevel.Full ? TimeSpan.FromSeconds(.35) : TimeSpan.FromSeconds(.12)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
        }
        if (_label is not null) _label.Text = Label;
        if (_value is not null) _value.Text = ShowValue ? (target >= 1 ? "✓" : target.ToString("P0", CultureInfo.InvariantCulture)) : string.Empty;
        AutomationProperties.SetHelpText(this, $"{target:P0}");
    }
}
