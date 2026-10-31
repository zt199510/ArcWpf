using System.Diagnostics;
using System.Windows.Media;

namespace ArcWpf.Controls;

// Steps a set of ControlSprings on CompositionTarget.Rendering and stops once all settle.
internal sealed class SpringDriver(Action apply)
{
    private readonly List<ControlSpring> _springs = [];
    private bool _running;
    private long _lastFrame;
    internal bool IsRunning => _running;
    /// <summary>Runs once all springs settle after an animated run.</summary>
    public Action? Settled { get; set; }

    public ControlSpring Add(double position, double visualDuration)
    {
        var spring = new ControlSpring(position, visualDuration);
        _springs.Add(spring);
        return spring;
    }

    public void Animate(ControlSpring spring, double target, double visualDuration)
    {
        spring.Tune(visualDuration);
        spring.Target = target;
        if (_running) { return; }
        _running = true;
        _lastFrame = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += Frame;
    }

    public void Snap(ControlSpring spring, double value) { spring.Snap(value); apply(); }

    public void Stop()
    {
        if (!_running) { return; }
        CompositionTarget.Rendering -= Frame;
        _running = false;
    }

    // Lands every spring on its target, for unload or motion Off.
    public void Finish()
    {
        Stop();
        foreach (var spring in _springs) { spring.Snap(spring.Target); }
        apply();
    }

    private void Frame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        Advance(Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds);
        _lastFrame = now;
    }

    internal void Advance(double seconds)
    {
        bool settled = true;
        foreach (var spring in _springs) { settled &= spring.Step(seconds) || spring.Position == spring.Target && spring.Velocity == 0; }
        apply();
        if (settled) { Stop(); Settled?.Invoke(); }
    }
}
