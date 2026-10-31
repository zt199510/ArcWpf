namespace ArcWpf.Controls;

// Analytic critically damped spring. Position and velocity survive target changes.
internal sealed class ControlSpring(double position, double visualDuration)
{
    public double Position { get; private set; } = position;
    public double Velocity { get; private set; }
    public double Target { get; set; } = position;
    private double _omega = 3.89 / visualDuration;

    // Retargets may switch springs (morph to open, smooth to fold) while keeping velocity.
    public void Tune(double visualDuration) => _omega = 3.89 / visualDuration;

    // motion's physical(visualDuration, 0): stiffness (2pi / 1.2vd)^2, critically damped.
    public static double Physical(double visualDuration) => visualDuration * 1.2 * 3.89 / (2 * Math.PI);

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
