using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>Template layout host; measures content naturally while its visible width follows a spring.</summary>
public sealed class BadgeBody : Decorator
{
    private readonly ControlSpring _spring = new(0, .42);
    private double _natural = double.NaN;
    private bool _requested, _running;
    private long _lastFrame;
    internal bool IsRunning => _running;
    internal void RequestMorph() { _requested = true; InvalidateMeasure(); }
    public BadgeBody() { Unloaded += (_, _) => Stop(); }
    internal void Stop()
    {
        if (_running) CompositionTarget.Rendering -= Frame;
        _running = _requested = false;
        if (double.IsFinite(_natural)) _spring.Snap(_natural);
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return default;
        Child.Measure(new Size(double.PositiveInfinity, constraint.Height));
        double next = Child.DesiredSize.Width;
        if (!double.IsFinite(_natural)) _spring.Snap(next);
        else if (Math.Abs(next - _natural) > .001)
        {
            if (_requested)
            {
                _spring.Target = next;
                if (!_running) { _running = true; _lastFrame = Stopwatch.GetTimestamp(); CompositionTarget.Rendering += Frame; }
            }
            else { Stop(); _spring.Snap(next); }
        }
        _natural = next; _requested = false;
        return new Size(Math.Max(0, _running ? _spring.Position : next), Child.DesiredSize.Height);
    }
    protected override Size ArrangeOverride(Size size)
    {
        Child?.Arrange(new Rect(0, 0, Math.Max(0, _natural), size.Height));
        return size;
    }
    private void Frame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        bool done = _spring.Step((now - _lastFrame) / (double)Stopwatch.Frequency); _lastFrame = now;
        if (done) Stop();
        InvalidateMeasure();
    }
}
