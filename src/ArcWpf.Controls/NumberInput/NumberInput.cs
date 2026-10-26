using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Input", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Increase", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Decrease", Type = typeof(ButtonBase))]
public class NumberInput : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumberInput), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberInput), new FrameworkPropertyMetadata(0d, OnBoundsChanged, CoerceMinimum));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberInput), new FrameworkPropertyMetadata(100d, OnBoundsChanged, CoerceMaximum));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumberInput), new FrameworkPropertyMetadata(1d, null, CoerceStep));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NumberInput), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(NumberInput), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(NumberInput), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(NumberInput), new FrameworkPropertyMetadata(string.Empty));
    private TextBox? _input;
    private ButtonBase? _increase;
    private ButtonBase? _decrease;
    private bool _syncing;

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Prefix { get => (string)GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    static NumberInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberInput), new FrameworkPropertyMetadata(typeof(NumberInput)));

    public NumberInput()
    {
        AutomationProperties.SetName(this, Label);
        Loaded += (_, _) => SyncText();
        Unloaded += (_, _) => ReleaseParts();
    }

    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _input = GetTemplateChild("PART_Input") as TextBox;
        _increase = GetTemplateChild("PART_Increase") as ButtonBase;
        _decrease = GetTemplateChild("PART_Decrease") as ButtonBase;
        if (_input is not null) { _input.PreviewKeyDown += InputKeyDown; _input.LostKeyboardFocus += InputLostFocus; }
        if (_increase is not null) _increase.Click += IncreaseClick;
        if (_decrease is not null) _decrease.Click += DecreaseClick;
        SyncText();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, Label);
    }

    private static object CoerceValue(DependencyObject d, object value) => Math.Clamp((double)value, ((NumberInput)d).Minimum, ((NumberInput)d).Maximum);
    private static object CoerceMinimum(DependencyObject d, object value) => double.IsFinite((double)value) ? (double)value : 0d;
    private static object CoerceMaximum(DependencyObject d, object value) => double.IsFinite((double)value) ? Math.Max((double)value, ((NumberInput)d).Minimum) : 100d;
    private static object CoerceStep(DependencyObject d, object value) => double.IsFinite((double)value) && (double)value > 0 ? (double)value : 1d;
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumberInput)d).SyncText();
    private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (NumberInput)d; input.CoerceValue(MaximumProperty); input.CoerceValue(ValueProperty); input.SyncText();
    }

    private void IncreaseClick(object sender, RoutedEventArgs e) => Change(Step);
    private void DecreaseClick(object sender, RoutedEventArgs e) => Change(-Step);
    private void InputKeyDown(object sender, KeyEventArgs e)
    {
        var amount = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || e.Key is Key.PageUp or Key.PageDown ? Step * 10 : Step;
        if (e.Key is Key.Up or Key.PageUp) { CommitDraft(); Change(amount); e.Handled = true; }
        else if (e.Key is Key.Down or Key.PageDown) { CommitDraft(); Change(-amount); e.Handled = true; }
        else if (e.Key == Key.Home) { Value = Minimum; e.Handled = true; }
        else if (e.Key == Key.End) { Value = Maximum; e.Handled = true; }
        else if (e.Key == Key.Enter) { CommitDraft(); e.Handled = true; }
        else if (e.Key == Key.Escape) { SyncText(); e.Handled = true; }
    }
    private void InputLostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDraft();
    private void CommitDraft()
    {
        if (_input is null || _syncing) return;
        if (double.TryParse(_input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)) Value = value;
        else SyncText();
    }
    private void Change(double amount) => Value = Math.Clamp(Value + amount, Minimum, Maximum);
    private void SyncText()
    {
        if (_input is null || _syncing) return;
        _syncing = true; try { _input.Text = Value.ToString(CultureInfo.CurrentCulture); _input.SelectAll(); } finally { _syncing = false; }
        if (_increase is not null) _increase.IsEnabled = Value < Maximum;
        if (_decrease is not null) _decrease.IsEnabled = Value > Minimum;
        AutomationProperties.SetHelpText(this, $"{Value.ToString(CultureInfo.CurrentCulture)} {Prefix}{Suffix}".Trim());
    }
    private void ReleaseParts()
    {
        if (_input is not null) { _input.PreviewKeyDown -= InputKeyDown; _input.LostKeyboardFocus -= InputLostFocus; }
        if (_increase is not null) _increase.Click -= IncreaseClick;
        if (_decrease is not null) _decrease.Click -= DecreaseClick;
        _input = null; _increase = null; _decrease = null;
    }
}
