using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class AnimatedCounter : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnimatedCounter), new FrameworkPropertyMetadata(0d, OnValueChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(AnimatedCounter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(AnimatedCounter), new PropertyMetadata(0, OnValueChanged));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(AnimatedCounter), new PropertyMetadata("en-US", OnValueChanged));
    public static readonly DependencyProperty AnimateOnViewProperty = DependencyProperty.Register(nameof(AnimateOnView), typeof(bool), typeof(AnimatedCounter), new PropertyMetadata(false));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Prefix { get => (string)GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    public bool AnimateOnView { get => (bool)GetValue(AnimateOnViewProperty); set => SetValue(AnimateOnViewProperty, value); }
    private StackPanel? _digitsPanel; private string _displayText = string.Empty; private bool _viewAnimated;
    static AnimatedCounter() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AnimatedCounter), new FrameworkPropertyMetadata(typeof(AnimatedCounter)));
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _digitsPanel = GetTemplateChild("PART_Digits") as StackPanel; UpdateText(false); if (AnimateOnView && IsVisible && !_viewAnimated) { _viewAnimated = true; AnimateIn(); } }
    protected override void OnInitialized(EventArgs e) { base.OnInitialized(e); IsVisibleChanged += (_, _) => { if (AnimateOnView && IsVisible && !_viewAnimated) { _viewAnimated = true; AnimateIn(); } }; }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AnimatedCounter)d).UpdateText(true);
    private void UpdateText(bool animate)
    {
        if (_digitsPanel is null) return;
        string text; try { text = Value.ToString($"N{Math.Clamp(Decimals, 0, 6)}", CultureInfo.GetCultureInfo(Locale)); } catch { text = Value.ToString($"N{Math.Clamp(Decimals, 0, 6)}", CultureInfo.InvariantCulture); }
        var next = Prefix + text + Suffix; var previous = _displayText; _displayText = next; _digitsPanel.Children.Clear();
        for (var index = 0; index < next.Length; index++)
        {
            var glyph = new TextBlock { Text = next[index].ToString(), Foreground = (Brush)FindResource("ArcWpf.Brush.Foreground"), FontSize = 32, FontWeight = FontWeights.Medium, FontFamily = (FontFamily)FindResource("ArcWpf.Font.Mono") };
            if (char.IsDigit(next[index])) glyph.MinWidth = 18;
            _digitsPanel.Children.Add(glyph);
            if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && index < previous.Length && previous[index] != next[index])
            {
                var move = new TranslateTransform { Y = string.CompareOrdinal(previous, next) > 0 ? 6 : -6 }; glyph.RenderTransform = move; glyph.Opacity = .35;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(move.Y, 0, TimeSpan.FromSeconds(.22)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                glyph.BeginAnimation(OpacityProperty, new DoubleAnimation(.35, 1, TimeSpan.FromSeconds(.22)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }
    }
    private void AnimateIn()
    {
        if (_digitsPanel is null) return;
        _digitsPanel.Opacity = ArcMotion.GetLevel(this) == ArcMotionLevel.Off ? 1 : 0;
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Off) _digitsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
