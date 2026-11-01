using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

public partial class UsageMeter
{
    private Border? _badge;
    private TextBlock? _badgeText;
    private readonly AutoWidthMotion _badgeWidthMotion = new();

    private void UpdateBadge(string text, bool animate)
    {
        if (_badge is null || _badgeText is null) return;
        var measure = new TextBlock
        {
            Text = text, FontFamily = _badgeText.FontFamily, FontSize = _badgeText.FontSize,
            FontWeight = _badgeText.FontWeight
        };
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double target = measure.DesiredSize.Width + 24 + (Status == UsageMeterStatus.Normal ? 0 : 20);
        bool moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        _badgeWidthMotion.SetTarget(target, moving);
    }
    private void StopBadgeMotion() => _badgeWidthMotion.Stop();
}
