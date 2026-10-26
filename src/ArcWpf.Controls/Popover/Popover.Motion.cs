using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ArcWpf.Controls;

public partial class Popover
{
    private ScaleTransform? _scale;
    private TranslateTransform? _translate;
    private Transform? _previousSurfaceTransform;
    private int _animationVersion;
    private readonly List<Action> _animationStops = new();
    private static readonly double[] SpringSamples = [0, .0213, .0739, .1443, .2235, .305, .3849, .4606, .5305, .594, .6508, .7011,
        .7452, .7836, .8168, .8454, .8699, .8907, .9084, .9234, .9361, .9467, .9557, .9632, .9694, .9747, .979, .9826, .9857, .9882,
        .9902, .992, .9934, .9946, .9955, .9963, 1];

    private void InitializeAnimation()
    {
        if (_surface is null) { return; }
        _surfaceMinWidth = _surface.MinWidth;
        _surfaceMaxWidth = _surface.MaxWidth;
        _previousSurfaceTransform = _surface.RenderTransform;
        _scale = new ScaleTransform(1, 1);
        _translate = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(_previousSurfaceTransform);
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        _surface.SetCurrentValue(RenderTransformProperty, group);
    }

    private Vector DirectionOffset(double length) => ActualSide switch
    {
        PopoverSide.Top => new Vector(0, length),
        PopoverSide.Bottom => new Vector(0, -length),
        PopoverSide.Left => new Vector(length, 0),
        _ => new Vector(-length, 0)
    };

    private void PrepareAnimation()
    {
        StopAnimation();
        if (_surface is null) { return; }
        var level = ArcMotion.GetLevel(this);
        _surface.SetCurrentValue(OpacityProperty, level == ArcMotionLevel.Off ? 1d : 0d);
        if (_scale is not null) { _scale.ScaleX = _scale.ScaleY = level == ArcMotionLevel.Full ? .97 : 1; }
        var offset = level == ArcMotionLevel.Full ? DirectionOffset(5) : new Vector();
        if (_translate is not null) { _translate.X = offset.X; _translate.Y = offset.Y; }
        double aligned = Alignment switch { PopoverAlignment.Start => 0, PopoverAlignment.End => 1, _ => .5 };
        if (FlowDirection == FlowDirection.RightToLeft && ActualSide is PopoverSide.Top or PopoverSide.Bottom) { aligned = 1 - aligned; }
        _surface.RenderTransformOrigin = ActualSide switch
        {
            PopoverSide.Top => new Point(aligned, 1), PopoverSide.Bottom => new Point(aligned, 0),
            PopoverSide.Left => new Point(1, aligned), _ => new Point(0, aligned)
        };
    }

    private void RunAnimation(bool opening, int cycleVersion)
    {
        double opacity = _surface?.Opacity ?? 1;
        double scale = _scale?.ScaleX ?? 1;
        double x = _translate?.X ?? 0, y = _translate?.Y ?? 0;
        StopAnimation();
        var level = ArcMotion.GetLevel(this);
        if (_surface is null || level == ArcMotionLevel.Off)
        {
            if (_surface is not null) { _surface.SetCurrentValue(OpacityProperty, opening ? 1d : 0d); }
            if (_scale is not null) { _scale.ScaleX = _scale.ScaleY = 1; }
            if (_translate is not null) { _translate.X = _translate.Y = 0; }
            if (opening) { _cycle.CompleteOpen(cycleVersion); } else { FinishClose(cycleVersion); }
            return;
        }
        bool full = level == ArcMotionLevel.Full;
        double fadeDuration = full ? .16 : .12;
        double motionDuration = opening ? .58 : .16;
        var targetOffset = !opening && full ? DirectionOffset(2.5) : new Vector();
        double targetScale = !opening && full ? .98 : 1;
        if (!full)
        {
            if (_scale is not null) { _scale.ScaleX = _scale.ScaleY = 1; }
            if (_translate is not null) { _translate.X = _translate.Y = 0; }
        }
        if (full && _scale is not null && _translate is not null)
        {
            AnimateValue(_translate, TranslateTransform.XProperty, x, targetOffset.X, motionDuration, opening);
            AnimateValue(_translate, TranslateTransform.YProperty, y, targetOffset.Y, motionDuration, opening);
            AnimateValue(_scale, ScaleTransform.ScaleXProperty, scale, targetScale, motionDuration, opening);
            AnimateValue(_scale, ScaleTransform.ScaleYProperty, scale, targetScale, motionDuration, opening,
                opening ? () => _cycle.CompleteOpen(cycleVersion) : null);
        }
        AnimateValue(_surface, OpacityProperty, opacity, opening ? 1 : 0, fadeDuration, false,
            !opening ? () => FinishClose(cycleVersion) : !full || _scale is null ? () => _cycle.CompleteOpen(cycleVersion) : null,
            opening && full);
    }

    private void AnimateValue(DependencyObject target, DependencyProperty property, double from, double to, double duration, bool spring, Action? finished = null, bool enter = false)
    {
        int token = _animationVersion;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(duration), FillBehavior = FillBehavior.Stop };
        if (spring)
        {
            for (int i = 0; i < SpringSamples.Length; i++)
            {
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(from + (to - from) * SpringSamples[i],
                    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration * i / (SpringSamples.Length - 1)))));
            }
        }
        else
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(duration)),
                enter ? new KeySpline(.16, 1, .3, 1) : new KeySpline(.22, 1, .36, 1)));
        }
        Action stop = () =>
        {
            if (target is UIElement ui) { ui.BeginAnimation(property, null); }
            else if (target is Animatable value) { value.BeginAnimation(property, null); }
        };
        _animationStops.Add(stop);
        target.SetCurrentValue(property, to);
        animation.Completed += (_, _) =>
        {
            if (token != _animationVersion) { return; }
            stop();
            _animationStops.Remove(stop);
            finished?.Invoke();
        };
        if (target is UIElement element) { element.BeginAnimation(property, animation); }
        else if (target is Animatable animatable) { animatable.BeginAnimation(property, animation); }
    }

    private void StopAnimation()
    {
        ++_animationVersion;
        foreach (var stop in _animationStops) { stop(); }
        _animationStops.Clear();
    }
}
