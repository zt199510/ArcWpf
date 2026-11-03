using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A number on digit wheels: each place value keeps its column, turns in the direction the whole number moved and wraps like an odometer.</summary>
[TemplatePart(Name = "PART_Digits", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_Label", Type = typeof(Grid))]
public class AnimatedCounter : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnimatedCounter), new FrameworkPropertyMetadata(0d, (d, _) => ((AnimatedCounter)d).UpdateText()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty, (d, _) => ((AnimatedCounter)d).UpdateLabel(true)));
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty, (d, _) => ((AnimatedCounter)d).UpdateText()));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty, (d, _) => ((AnimatedCounter)d).UpdateText()));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(AnimatedCounter), new PropertyMetadata(0, (d, _) => ((AnimatedCounter)d).UpdateText()));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(AnimatedCounter), new PropertyMetadata("en-US", (d, _) => ((AnimatedCounter)d).UpdateText()));
    public static readonly DependencyProperty AnimateOnViewProperty = DependencyProperty.Register(nameof(AnimateOnView), typeof(bool), typeof(AnimatedCounter), new PropertyMetadata(false));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Prefix { get => (string)GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    public bool AnimateOnView { get => (bool)GetValue(AnimateOnViewProperty); set => SetValue(AnimateOnViewProperty, value); }

    private const double LetterSpacing = -.03;
    private readonly OdometerNumber _number = new() { RevealOnLoad = false, Tracking = LetterSpacing };
    private Grid? _label;
    private TextBlock? _labelText;
    private bool _applied;

    static AnimatedCounter() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AnimatedCounter), new FrameworkPropertyMetadata(typeof(AnimatedCounter)));

    public AnimatedCounter()
    {
        Loaded += (_, _) => { if (AnimateOnView && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) _number.Reveal(); else RevealNow(); };
        IsVisibleChanged += (_, _) => { if (IsVisible && IsLoaded) _number.Reveal(); };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Digits") is ContentPresenter host)
        {
            _number.SetBinding(FontFamilyProperty, new System.Windows.Data.Binding(nameof(FontFamily)) { Source = this });
            _number.SetBinding(FontSizeProperty, new System.Windows.Data.Binding(nameof(FontSize)) { Source = this });
            _number.SetBinding(FontWeightProperty, new System.Windows.Data.Binding(nameof(FontWeight)) { Source = this });
            _number.SetBinding(ForegroundProperty, new System.Windows.Data.Binding(nameof(Foreground)) { Source = this });
            host.Content = _number;
            CentreDigits();
            _applied = true;
        }
        _label = GetTemplateChild("PART_Label") as Grid;
        _labelText = null;
        UpdateLabel(false);
        UpdateText();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_applied && (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == FontWeightProperty)) CentreDigits();
    }

    // The digit row is one em tall (line-height 1), while a font's natural line is taller, so the row overflows evenly above and below.
    private void CentreDigits()
    {
        var line = new TextBlock { Text = "0", FontFamily = FontFamily, FontSize = FontSize, FontWeight = FontWeight };
        line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double overflow = (FontSize - line.DesiredSize.Height) / 2;
        _number.Margin = new Thickness(0, overflow, 0, overflow);
    }

    private string FormatText()
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(Locale); } catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        return Prefix + Value.ToString($"N{Math.Clamp(Decimals, 0, 6)}", culture) + Suffix;
    }

    private void UpdateName(string text) => AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? text : $"{Label}: {text}");

    private void RevealNow()
    {
        _number.Set(Value, FormatText(), false);
        _number.Reveal();
        _number.Stop();
    }

    private void UpdateText()
    {
        string text = FormatText();
        UpdateName(text);
        if (_applied) _number.Set(Value, text, IsLoaded);
    }

    // A changed label rises in from a soft blur while the old one lifts away, in the same slot.
    private void UpdateLabel(bool animate)
    {
        UpdateName(FormatText());
        if (_label is null) return;
        _label.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;
        var next = new TextBlock { Text = Label, FontSize = 12, FontWeight = FontWeights.Normal, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        next.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var old = _labelText;
        _labelText = next;
        _label.Children.Add(next);
        if (old is null) return;
        if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            Tween.Present(next, true, 12 * .3, 6, Tween.Standard, ArcEasing.Enter);
            Tween.Present(old, false, -12 * .3, 3, Tween.Fast, ArcEasing.Standard, 0, () => _label.Children.Remove(old));
        }
        else _label.Children.Remove(old);
    }
}
