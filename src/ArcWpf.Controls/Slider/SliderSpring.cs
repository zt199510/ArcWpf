namespace ArcWpf.Controls;

// Slider's physical, critically damped spring. The frozen Arc source converts duration to this omega.
internal sealed class SliderSpring(double position = 0)
{
    internal double Position { get; private set; } = position;
    internal double Velocity { get; private set; }
    internal double Target { get; private set; } = position;
    private double _omega = 2 * Math.PI / (.26 * 1.2);
    internal bool IsRunning { get; private set; }
    internal void Snap(double value) { Position = Target = value; Velocity = 0; IsRunning = false; }
    internal void To(double target, double? velocity = null, double duration = .26)
    {
        Target = target; if (velocity is { } v) Velocity = v;
        _omega = 2 * Math.PI / (duration * 1.2);
        IsRunning = Math.Abs(Position - Target) > .002 || Math.Abs(Velocity) > .02;
    }
    internal void Step(double elapsed)
    {
        if (!IsRunning || !double.IsFinite(elapsed) || elapsed <= 0) return;
        double dt = Math.Min(elapsed, .05), offset = Position - Target, c = Velocity + _omega * offset, decay = Math.Exp(-_omega * dt);
        Position = Target + (offset + c * dt) * decay;
        Velocity = (Velocity - _omega * c * dt) * decay;
        if (Math.Abs(Position - Target) < .002 && Math.Abs(Velocity) < .02) Snap(Target);
    }
}
