using System.Windows;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

internal sealed class AutoWidthMotion
{
    private FrameworkElement? _element;
    private DoubleAnimationUsingKeyFrames? _timeline;
    private double _targetWidth;
    internal bool IsRunning => _timeline is not null;

    internal void Attach(FrameworkElement? element)
    {
        Stop();
        _element = element;
        _targetWidth = 0;
    }
    internal void SetTarget(double target, bool animate)
    {
        if (_element is null || !double.IsFinite(target) || target < 0) return;
        if (!animate || _targetWidth <= 0)
        {
            Stop();
            _targetWidth = target;
            return;
        }
        double from = _timeline is not null ? _element.Width : _element.ActualWidth;
        if (!double.IsFinite(from) || from <= 0) from = _targetWidth;
        if (Math.Abs(target - from) < .5) { _targetWidth = target; return; }
        _element.BeginAnimation(FrameworkElement.WidthProperty, null);
        _element.ClearValue(FrameworkElement.WidthProperty);
        var timeline = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.4), FillBehavior = FillBehavior.Stop };
        var samples = OwnedMotion.SpringSamples;
        for (int index = 0; index < samples.Length; index++)
            timeline.KeyFrames.Add(new LinearDoubleKeyFrame(from + (target - from) * samples[index],
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.4 * index / (samples.Length - 1)))));
        _timeline = timeline;
        var element = _element;
        timeline.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_timeline, timeline)) return;
            element.BeginAnimation(FrameworkElement.WidthProperty, null);
            element.ClearValue(FrameworkElement.WidthProperty);
            _timeline = null;
        };
        element.BeginAnimation(FrameworkElement.WidthProperty, timeline);
        _targetWidth = target;
    }
    internal void Stop()
    {
        if (_element is not null)
        {
            _element.BeginAnimation(FrameworkElement.WidthProperty, null);
            _element.ClearValue(FrameworkElement.WidthProperty);
        }
        _timeline = null;
    }
}
