using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

public enum ThemeSwitchVariant { Reveal, Eclipse, Split, Rise }
public enum ThemeSwitchTheme { Light, Dark }

public sealed class ThemeChangedEventArgs(RoutedEvent routedEvent, object source, ThemeSwitchTheme theme, ThemeSwitchVariant variant) : RoutedEventArgs(routedEvent, source)
{
    public ThemeSwitchTheme Theme { get; } = theme;
    public ThemeSwitchVariant Variant { get; } = variant;
}

/// <summary>
/// theme-switch.tsx on the secondary small Button: the sun and moon trade places with one rotation, and each variant nudges the icon the
/// way its page transition travels. The host applies the theme; the control only reports the requested one.
/// </summary>
[TemplatePart(Name = "PART_Surface", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_IconWrap", Type = typeof(Panel))]
public class ThemeSwitch : System.Windows.Controls.Button
{
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(nameof(Theme), typeof(ThemeSwitchTheme), typeof(ThemeSwitch), new FrameworkPropertyMetadata(ThemeSwitchTheme.Light, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnThemeChanged));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ThemeSwitchVariant), typeof(ThemeSwitch), new FrameworkPropertyMetadata(ThemeSwitchVariant.Reveal, (d, _) => ((ThemeSwitch)d).UpdateNudge(true)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ThemeSwitch), new FrameworkPropertyMetadata(null, (d, _) => ((ThemeSwitch)d).UpdateName()));
    public static readonly DependencyProperty IconOnlyProperty = DependencyProperty.Register(nameof(IconOnly), typeof(bool), typeof(ThemeSwitch), new FrameworkPropertyMetadata(false));
    public static readonly RoutedEvent ThemeChangedEvent = EventManager.RegisterRoutedEvent(nameof(ThemeChanged), RoutingStrategy.Bubble, typeof(EventHandler<ThemeChangedEventArgs>), typeof(ThemeSwitch));

    public ThemeSwitchTheme Theme { get => (ThemeSwitchTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
    public ThemeSwitchVariant Variant { get => (ThemeSwitchVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The accessible name. The visible text is always "Switch theme", as in the source.</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IconOnly { get => (bool)GetValue(IconOnlyProperty); set => SetValue(IconOnlyProperty, value); }
    public event EventHandler<ThemeChangedEventArgs> ThemeChanged { add => AddHandler(ThemeChangedEvent, value); remove => RemoveHandler(ThemeChangedEvent, value); }

    private const double BlurSubtle = 2;
    private FrameworkElement? _surface;
    private Panel? _wrap;
    private FrameworkElement? _icon;
    private readonly ScaleTransform _press = new(1, 1), _nudgeScale = new(1, 1);
    private readonly TranslateTransform _nudgeMove = new();

    static ThemeSwitch() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ThemeSwitch), new FrameworkPropertyMetadata(typeof(ThemeSwitch)));
    public ThemeSwitch()
    {
        Click += (_, _) => Toggle();
        UpdateName();
    }

    public void Toggle()
    {
        if (!IsEnabled) return;
        var next = Theme == ThemeSwitchTheme.Light ? ThemeSwitchTheme.Dark : ThemeSwitchTheme.Light;
        SetCurrentValue(ThemeProperty, next);
        RaiseEvent(new ThemeChangedEventArgs(ThemeChangedEvent, this, next, Variant));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        _wrap = GetTemplateChild("PART_IconWrap") as Panel;
        if (_surface is not null) _surface.RenderTransform = _press;
        if (_wrap is not null)
        {
            var nudge = new TransformGroup();
            nudge.Children.Add(_nudgeScale);
            nudge.Children.Add(_nudgeMove);
            _wrap.RenderTransform = nudge;
            _wrap.Children.Clear();
            _icon = null;
        }
        SwapIcon(false);
        UpdateNudge(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty) Tween.Press(_press, IsPressed, .97, IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ThemeSwitch)d;
        control.UpdateName();
        // A theme that arrives before the first paint swaps in place, so a page loading in dark mode never spins its switches.
        control.SwapIcon(control.IsLoaded);
        control.UpdateNudge(control.IsLoaded);
    }

    private void UpdateName() => AutomationProperties.SetName(this, Label ?? $"Switch to {(Theme == ThemeSwitchTheme.Light ? "dark" : "light")} mode");

    private void SwapIcon(bool animate)
    {
        if (_wrap is null) return;
        var level = ArcMotion.GetLevel(this);
        var previous = _icon;
        double angle = Theme == ThemeSwitchTheme.Light ? 30 : -30;
        var icon = MakeIcon(Theme == ThemeSwitchTheme.Light ? Lucide.Sun : Lucide.Moon);
        _icon = icon;
        _wrap.Children.Add(icon);
        if (!animate || level == ArcMotionLevel.Off)
        {
            if (previous is not null) _wrap.Children.Remove(previous);
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            icon.Opacity = 0;
            Tween.To(icon, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
            if (previous is not null) Tween.To(previous, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, done: () => _wrap?.Children.Remove(previous));
            return;
        }
        // Both icons turn the same way: the entering one starts at this theme's angle and the leaving one ends at the same angle.
        var (scale, rotate) = Parts(icon);
        scale.ScaleX = scale.ScaleY = .7;
        rotate.Angle = angle;
        icon.Opacity = 0;
        Tween.To(icon, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
        Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
        Tween.Spring(rotate, RotateTransform.AngleProperty, 0, Tween.Snappy);
        Blur(icon, BlurSubtle, 0, Tween.Fast, ArcEasing.Enter);
        if (previous is null) return;
        var (oldScale, oldRotate) = Parts(previous);
        Tween.To(previous, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => _wrap?.Children.Remove(previous));
        Tween.To(oldScale, ScaleTransform.ScaleXProperty, .7, Tween.Fast, ArcEasing.Standard);
        Tween.To(oldScale, ScaleTransform.ScaleYProperty, .7, Tween.Fast, ArcEasing.Standard);
        Tween.To(oldRotate, RotateTransform.AngleProperty, angle, Tween.Fast, ArcEasing.Standard);
        Blur(previous, 0, BlurSubtle, Tween.Fast, ArcEasing.Standard);
    }

    private static Grid MakeIcon(string data)
    {
        var scale = new ScaleTransform(1, 1);
        var rotate = new RotateTransform();
        var transform = new TransformGroup();
        transform.Children.Add(scale);
        transform.Children.Add(rotate);
        return new Grid { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5), RenderTransform = transform, Children = { Lucide.Glyph(data, 16) } };
    }

    private static (ScaleTransform, RotateTransform) Parts(FrameworkElement icon)
    {
        var group = (TransformGroup)icon.RenderTransform;
        return ((ScaleTransform)group.Children[0], (RotateTransform)group.Children[1]);
    }

    private static void Blur(FrameworkElement element, double from, double to, double seconds, IEasingFunction ease)
    {
        var effect = new BlurEffect { Radius = from };
        element.Effect = effect;
        var animation = new DoubleAnimation(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease };
        if (to == 0) animation.Completed += (_, _) => { if (ReferenceEquals(element.Effect, effect)) element.Effect = null; };
        effect.BeginAnimation(BlurEffect.RadiusProperty, animation);
    }

    private void UpdateNudge(bool animate)
    {
        var (scale, x, y) = Theme == ThemeSwitchTheme.Light ? (1d, 0d, 0d) : Variant switch
        {
            ThemeSwitchVariant.Reveal => (1.08, 0d, 0d),
            ThemeSwitchVariant.Eclipse => (1d, -2d, 0d),
            ThemeSwitchVariant.Split => (1d, 1d, 0d),
            _ => (1d, 0d, -2d)
        };
        if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            Tween.To(_nudgeScale, ScaleTransform.ScaleXProperty, scale, Tween.SpringTokenSeconds, Tween.SpringToken);
            Tween.To(_nudgeScale, ScaleTransform.ScaleYProperty, scale, Tween.SpringTokenSeconds, Tween.SpringToken);
            Tween.To(_nudgeMove, TranslateTransform.XProperty, x, Tween.SpringTokenSeconds, Tween.SpringToken);
            Tween.To(_nudgeMove, TranslateTransform.YProperty, y, Tween.SpringTokenSeconds, Tween.SpringToken);
            return;
        }
        Tween.Set(_nudgeScale, ScaleTransform.ScaleXProperty, scale);
        Tween.Set(_nudgeScale, ScaleTransform.ScaleYProperty, scale);
        Tween.Set(_nudgeMove, TranslateTransform.XProperty, x);
        Tween.Set(_nudgeMove, TranslateTransform.YProperty, y);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ThemeSwitchPeer(this);

    /// <summary>aria-pressed: pressed while the theme is dark.</summary>
    private sealed class ThemeSwitchPeer(ThemeSwitch owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.Theme == ThemeSwitchTheme.Dark ? ToggleState.On : ToggleState.Off;
        public void Toggle() => owner.Toggle();
        public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);
    }
}
