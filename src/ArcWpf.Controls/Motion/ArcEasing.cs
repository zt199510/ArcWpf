using System.Windows;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

// CSS cubic-bezier(x1, y1, x2, y2) solved for x with Newton steps and a bisection fallback.
internal sealed class BezierEase : EasingFunctionBase
{
    private readonly double x1, y1, x2, y2;
    public BezierEase() : this(0, 0, 1, 1) { }
    public BezierEase(double x1, double y1, double x2, double y2)
    {
        (this.x1, this.y1, this.x2, this.y2) = (x1, y1, x2, y2);
        EasingMode = EasingMode.EaseIn;
    }

    protected override double EaseInCore(double t)
    {
        if (t <= 0 || t >= 1) { return t; }
        double u = t;
        for (int i = 0; i < 8; i++)
        {
            double x = Curve(u, x1, x2) - t;
            double dx = Slope(u, x1, x2);
            if (Math.Abs(x) < 1e-6) { return Curve(u, y1, y2); }
            if (Math.Abs(dx) < 1e-6) { break; }
            u -= x / dx;
        }
        double lo = 0, hi = 1;
        u = t;
        for (int i = 0; i < 30; i++)
        {
            double x = Curve(u, x1, x2);
            if (Math.Abs(x - t) < 1e-6) { break; }
            if (x < t) { lo = u; } else { hi = u; }
            u = (lo + hi) / 2;
        }
        return Curve(u, y1, y2);
    }

    private static double Curve(double u, double a, double b) => 3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u;
    private static double Slope(double u, double a, double b) => 3 * a * (1 - u) * (1 - u) + 6 * (b - a) * u * (1 - u) + 3 * (1 - b) * u * u;
    protected override Freezable CreateInstanceCore() => new BezierEase(x1, y1, x2, y2);
}

// Analytic critically damped spring normalised to land exactly on 1 at the end of the window.
internal sealed class CriticalSpringEase : EasingFunctionBase
{
    private readonly double _omega;
    public CriticalSpringEase() : this(10) { }
    public CriticalSpringEase(double omega) { _omega = omega; EasingMode = EasingMode.EaseIn; }
    protected override double EaseInCore(double t) => (1 - (1 + _omega * t) * Math.Exp(-_omega * t)) / (1 - (1 + _omega) * Math.Exp(-_omega));
    protected override Freezable CreateInstanceCore() => new CriticalSpringEase(_omega);
}

internal static class ArcEasing
{
    // Arc motion tokens: ease.enter / ease.standard.
    public static readonly IEasingFunction Enter = Frozen(new BezierEase(.16, 1, .3, 1));
    public static readonly IEasingFunction Standard = Frozen(new BezierEase(.22, 1, .36, 1));
    // ease.inOut (--ease-in-out): for elements that move while already on screen.
    public static readonly IEasingFunction InOut = Frozen(new BezierEase(.65, 0, .35, 1));

    // Critically damped spring sampled over a fixed window; 3.89/visualDuration matches ControlSpring.
    public static IEasingFunction Spring(double visualDuration, double windowSeconds) => Frozen(new CriticalSpringEase(3.89 / visualDuration * windowSeconds));

    private static T Frozen<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
