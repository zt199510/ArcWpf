using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class PasswordRevealToggle : ToggleButton
{
    private readonly PasswordEye _eye = new() { Width = 18, Height = 18 };
    private readonly OwnedMotion _motion = new();
    private Border? _surface;
    private SolidColorBrush? _background, _foreground;
    private ScaleTransform? _scale;
    internal bool IsMotionRunning => _motion.IsRunning;
    internal PasswordEye Eye => _eye;
    static PasswordRevealToggle() => DefaultStyleKeyProperty.OverrideMetadata(typeof(PasswordRevealToggle), new FrameworkPropertyMetadata(typeof(PasswordRevealToggle)));
    public PasswordRevealToggle()
    {
        Content = _eye; Loaded += (_, _) => Update(false); Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => { if (!IsVisible) Stop(); };
    }
    public override void OnApplyTemplate()
    { Stop(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _background = _foreground = null; if (_surface is not null) { _scale = new(); _surface.RenderTransform = _scale; } Update(false); }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { Stop(); _surface = null; base.OnTemplateChanged(oldTemplate, newTemplate); }
    protected override void OnKeyDown(KeyEventArgs e)
    { if (e.Key == Key.Enter && IsEnabled) { OnClick(); e.Handled = true; return; } base.OnKeyDown(e); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e); if (_motion is null) return;
        if (e.Property == IsCheckedProperty || e.Property == IsPressedProperty || e.Property == BackgroundProperty || e.Property == ForegroundProperty || e.Property == ArcMotion.LevelProperty || e.Property == IsEnabledProperty) Update(IsLoaded);
    }
    internal void Stop() => _motion.Stop();
    private void Update(bool animate)
    {
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        animate &= IsLoaded && IsVisible && full;
        if (_surface is not null && Background is SolidColorBrush bg) { _background ??= new(bg.Color); _surface.Background = _background; _motion.Color(_background, bg.Color, .16, animate); }
        if (Foreground is SolidColorBrush fg) { _foreground ??= new(fg.Color); _eye.Stroke = _foreground; _motion.Color(_foreground, fg.Color, .16, animate); }
        _motion.Double(_eye, PasswordEye.ProgressProperty, IsChecked == true ? 1 : 0, .24, animate);
        _motion.Double(_eye, PasswordEye.SlashOpacityProperty, IsChecked == true ? 1 : 0, .12, animate);
        if (_scale is not null)
        {
            double target = IsPressed && full ? .96 : 1;
            _motion.Double(_scale, ScaleTransform.ScaleXProperty, target, IsPressed ? .1 : .42, animate, spring: !IsPressed);
            _motion.Double(_scale, ScaleTransform.ScaleYProperty, target, IsPressed ? .1 : .42, animate, spring: !IsPressed);
        }
    }
}
