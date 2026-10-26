using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArcWpf.Controls;

internal sealed class SliderMarkButton : System.Windows.Controls.Button
{
    private readonly OwnedMotion _motion = new();
    private TextBlock? _text;
    private SolidColorBrush? _color;
    internal bool IsMotionRunning => _motion.IsRunning;
    internal SliderMarkButton() { Unloaded += (_, _) => Stop(); }
    internal void Stop() => _motion.Stop();
    public override void OnApplyTemplate() { Stop(); base.OnApplyTemplate(); _text = GetTemplateChild("PART_Text") as TextBlock; _color = null; UpdateColor(false); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e); if (_motion is null) return;
        if (e.Property == ForegroundProperty || e.Property == ArcMotion.LevelProperty) UpdateColor(IsLoaded);
    }
    private void UpdateColor(bool animate)
    {
        if (_text is null) return;
        if (Foreground is not SolidColorBrush color) { _text.Foreground = Foreground; return; }
        _color ??= new(color.Color); _text.Foreground = _color;
        _motion.Color(_color, color.Color, .16, animate && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
    }
}
