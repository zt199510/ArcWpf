namespace ArcWpf.Controls;

// Analytic critically damped spring. Position and velocity survive target changes.
internal sealed class ControlSpring(double position, double visualDuration)
{
    public double Position { get; private set; } = position;
    public double Velocity { get; private set; }
    public double Target { get; set; } = position;
    private readonly double _omega = 3.89 / visualDuration;

    public void Snap(double value) { Target = Position = value; Velocity = 0; }

    public bool Step(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) { return false; }
        double dt = Math.Min(elapsedSeconds, .05);
        double offset = Position - Target;
        double c = Velocity + _omega * offset;
        double decay = Math.Exp(-_omega * dt);
        Position = Target + (offset + c * dt) * decay;
        Velocity = (Velocity - _omega * c * dt) * decay;
        bool settled = Math.Abs(Position - Target) < .01 && Math.Abs(Velocity) < .05;
        if (settled) { Snap(Target); }
        return settled;
    }
}
