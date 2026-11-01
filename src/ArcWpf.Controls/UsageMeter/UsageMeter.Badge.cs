using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

public partial class UsageMeter
{
    private Border? _badge;
    private TextBlock? _badgeText;
    private DoubleAnimationUsingKeyFrames? _badgeTimeline;
    private double _badgeTargetWidth;

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
        bool moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && _badgeTargetWidth > 0;
        if (!moving)
        {
            StopBadgeMotion();
            _badgeTargetWidth = target;
            return;
        }
        double from = _badgeTimeline is not null ? _badge.Width : _badge.ActualWidth;
        if (!double.IsFinite(from) || from <= 0) from = _badgeTargetWidth;
        if (Math.Abs(target - from) < .5) { _badgeTargetWidth = target; return; }
        _badge.BeginAnimation(WidthProperty, null);
        _badge.ClearValue(WidthProperty);
        var timeline = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.4), FillBehavior = FillBehavior.Stop };
        var samples = OwnedMotion.SpringSamples;
        for (int index = 0; index < samples.Length; index++)
            timeline.KeyFrames.Add(new LinearDoubleKeyFrame(from + (target - from) * samples[index],
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.4 * index / (samples.Length - 1)))));
        _badgeTimeline = timeline;
        timeline.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_badgeTimeline, timeline) || _badge is null) return;
            _badge.BeginAnimation(WidthProperty, null);
            _badge.ClearValue(WidthProperty);
            _badgeTimeline = null;
        };
        _badge.BeginAnimation(WidthProperty, timeline);
        _badgeTargetWidth = target;
    }
    private void StopBadgeMotion()
    {
        if (_badge is not null)
        {
            _badge.BeginAnimation(WidthProperty, null);
            _badge.ClearValue(WidthProperty);
        }
        _badgeTimeline = null;
    }
}
