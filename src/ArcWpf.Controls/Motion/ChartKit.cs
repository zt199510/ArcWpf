using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

// The sums bar-chart.tsx and line-chart.tsx share.
internal static class ChartMath
{
    public readonly record struct Scale(double Min, double Max, double[] Ticks);

    // Clean gridlines: the smallest step of 1, 2, 3, or 5 that covers the data in four rows or fewer, so shared steps survive a range change.
    public static Scale ScaleFor(double max)
    {
        double safe = Math.Max(max, 1), magnitude = Math.Pow(10, Math.Floor(Math.Log10(safe / 4)));
        foreach (double factor in new[] { 1d, 2, 3, 5, 10 })
        {
            double step = factor * magnitude, rows = Math.Ceiling(safe / step);
            if (rows <= 4) { return new Scale(0, rows * step, [.. Enumerable.Range(0, (int)rows + 1).Select(row => row * step)]); }
        }
        return new Scale(0, safe, [0, safe]);
    }

    private static double Clean(double value) => double.Parse(value.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    // Clean gridlines: the smallest step of 1, 2, 2.5, or 5 that covers the data in four rows or fewer, from zero unless the data dips below it.
    public static Scale NiceScale(double low, double high)
    {
        double lo = Math.Min(0, low), hi = Math.Max(high, lo + 1), span = hi - lo, magnitude = Math.Pow(10, Math.Floor(Math.Log10(span / 4)));
        foreach (double factor in new[] { 1d, 2, 2.5, 5, 10 })
        {
            double step = factor * magnitude, bottom = Math.Floor(lo / step) * step, top = Math.Ceiling(hi / step) * step;
            if ((top - bottom) / step > 4 + 1e-9) { continue; }
            var ticks = new List<double>();
            for (double value = bottom; value <= top + step / 2; value += step) { ticks.Add(Clean(value)); }
            return new Scale(Clean(bottom), Clean(top), [.. ticks]);
        }
        return new Scale(lo, hi, [lo, hi]);
    }

    // The line through every value, sampled densely on x from 0 to 1 in data units. Monotone cubic tangents keep a smooth curve from swinging past the data.
    public static Point[] Curve(IReadOnlyList<double> values, bool smooth)
    {
        if (values.Count == 0) { return []; }
        if (values.Count == 1) { return [new Point(0, values[0]), new Point(1, values[0])]; }
        int last = values.Count - 1;
        double step = 1d / last;
        if (!smooth) { return [.. values.Select((value, index) => new Point(index * step, value))]; }
        double[] slopes = [.. Enumerable.Range(0, last).Select(index => (values[index + 1] - values[index]) / step)];
        var tangents = new double[values.Count];
        for (int index = 1; index < last; index++)
        {
            tangents[index] = (Math.Sign(slopes[index - 1]) + Math.Sign(slopes[index])) * Math.Min(Math.Min(Math.Abs(slopes[index - 1]), Math.Abs(slopes[index])), Math.Abs(slopes[index - 1] + slopes[index]) / 4);
        }
        if (last > 1) { tangents[0] = (3 * slopes[0] - tangents[1]) / 2; tangents[last] = (3 * slopes[last - 1] - tangents[last - 1]) / 2; }
        else { tangents[0] = slopes[0]; tangents[1] = slopes[0]; }
        int samples = Math.Max(1, Math.Min(16, (int)Math.Round(192d / last, MidpointRounding.AwayFromZero)));
        var points = new List<Point>(last * samples + 1);
        for (int index = 0; index < last; index++)
        {
            double low = Math.Min(values[index], values[index + 1]), high = Math.Max(values[index], values[index + 1]);
            for (int sample = 0; sample < samples; sample++)
            {
                double t = (double)sample / samples, t2 = t * t, t3 = t2 * t;
                double value = (2 * t3 - 3 * t2 + 1) * values[index] + (t3 - 2 * t2 + t) * step * tangents[index] + (3 * t2 - 2 * t3) * values[index + 1] + (t3 - t2) * step * tangents[index + 1];
                points.Add(new Point((index + t) * step, Math.Clamp(value, low, high)));
            }
        }
        points.Add(new Point(1, values[last]));
        return [.. points];
    }

    // Value of a sampled line at x, straight between samples.
    public static double ValueAt(Point[] shape, double x)
    {
        if (shape.Length == 0) { return 0; }
        int low = 0, high = shape.Length - 1;
        if (x <= shape[low].X) { return shape[low].Y; }
        if (x >= shape[high].X) { return shape[high].Y; }
        while (high - low > 1) { int middle = (low + high) >> 1; if (shape[middle].X < x) { low = middle; } else { high = middle; } }
        var (a, b) = (shape[low], shape[high]);
        return b.X == a.X ? b.Y : a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
    }

    // Intl.NumberFormat("en-US", { maximumFractionDigits }).
    public static string Grouped(double value, int decimals = 0) => value.ToString("#,0" + (decimals > 0 ? "." + new string('#', decimals) : ""), CultureInfo.InvariantCulture);

    // Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 1 }).
    public static string Compact(double value)
    {
        double size = Math.Abs(value);
        (double scale, string suffix) = size >= 1e12 ? (1e12, "T") : size >= 1e9 ? (1e9, "B") : size >= 1e6 ? (1e6, "M") : size >= 1e3 ? (1e3, "K") : (1d, "");
        return (value / scale).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
    }

    // A cubic-free quintic ease: flat at both ends, so a band reads as level across a step.
    public static double Smoother(double t) { t = Math.Clamp(t, 0, 1); return t * t * t * (t * (t * 6 - 15) + 10); }

    public static bool IsDark(FrameworkElement owner)
    {
        var surface = Tween.Resolve(owner, "ArcWpf.Brush.Surface");
        return .299 * surface.R + .587 * surface.G + .114 * surface.B < 128;
    }

    public static Color Faded(Color color, double opacity) => Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    // A rounded-corner pen stroke that lands on whole pixels.
    public static Pen Crisp(Color color, double thickness = 1) { var pen = new Pen(new SolidColorBrush(color), thickness); pen.Freeze(); return pen; }
    public static SolidColorBrush Solid(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    // Whether any of the element is inside the scroller around it, for work that should stop off screen.
    public static bool OnScreen(FrameworkElement element)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null) { return false; }
        DependencyObject? parent = element;
        while (parent is not null && parent is not ScrollViewer) { parent = VisualTreeHelper.GetParent(parent); }
        if (parent is not ScrollViewer scroller) { return true; }
        var box = element.TransformToVisual(scroller).TransformBounds(new Rect(element.RenderSize));
        return box.IntersectsWith(new Rect(0, 0, scroller.ViewportWidth, scroller.ViewportHeight));
    }
}

// A value on a clock: waits out a delay, then eases to its target. CSS transitions and fades run on these inside a chart's frame loop.
internal sealed class Timed(double value)
{
    private double _from = value, _elapsed, _duration, _delay;
    private IEasingFunction? _ease;
    public double Value { get; private set; } = value;
    public double Target { get; private set; } = value;
    public bool Active { get; private set; }

    public void Jump(double to) { Value = _from = Target = to; Active = false; }
    public void Go(double to, double duration, IEasingFunction? ease, double delay = 0)
    {
        if (to == Target && (Active || to == Value)) { return; }
        Target = to;
        if (duration + delay <= 0) { Jump(to); return; }
        (_from, _elapsed, _duration, _delay, _ease, Active) = (Value, 0, duration, delay, ease, true);
    }
    public void Step(double seconds)
    {
        if (!Active) { return; }
        _elapsed += seconds;
        double running = _elapsed - _delay;
        if (running <= 0) { return; }
        if (running >= _duration) { Value = Target; Active = false; return; }
        double t = running / _duration;
        Value = _from + (Target - _from) * (_ease?.Ease(t) ?? t);
    }
}

// A color on the same clock, mixed in oklab as CSS interpolates it.
internal sealed class TimedColor(Color value)
{
    private readonly Timed _mix = new(1);
    private Color _from = value;
    public Color Target { get; private set; } = value;
    public Color Value { get; private set; } = value;
    public bool Active => _mix.Active;

    public void Jump(Color to) { (_from, Target, Value) = (to, to, to); _mix.Jump(1); }
    public void Go(Color to, double duration, IEasingFunction? ease, double delay = 0)
    {
        if (to == Target) { return; }
        (_from, Target) = (Value, to);
        _mix.Jump(0);
        _mix.Go(1, duration, ease, delay);
        if (duration + delay <= 0) { Value = to; }
    }
    public void Step(double seconds)
    {
        if (!_mix.Active) { return; }
        _mix.Step(seconds);
        Value = _mix.Value >= 1 ? Target : OkColor.Mix(Target, _from, _mix.Value);
    }
}

// One element a chart paints by hand. It always hit-tests, and carries the peer the chart hands it.
internal sealed class ChartSurface(Action<DrawingContext, Size> paint, Func<ChartSurface, AutomationPeer>? peer = null) : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        paint(dc, RenderSize);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => peer?.Invoke(this) ?? new FrameworkElementAutomationPeer(this);
}

// A gridline value and its label, fading as they join or leave the axis.
internal sealed class ChartRow(double value, TrackedText view)
{
    public double Value { get; } = value;
    public TrackedText View { get; } = view;
    public Timed Presence { get; } = new(0);
    public bool Leaving { get; set; }
    public double TextOpacity { get; set; } = 1;
    public double Y { get; set; }
}

// An axis label under the plot, fading the same way.
internal sealed class ChartAxisLabel(string key, TrackedText view, double place)
{
    public string Key { get; } = key;
    public TrackedText View { get; } = view;
    public Timed Presence { get; } = new(0);
    public GestureSpring Place { get; } = new(place);
    public bool Leaving { get; set; }
    public double Shift { get; set; }
}

// A panel that keeps its two children on one line with the space between them, and stacks them when they do not fit.
internal sealed class SpreadRow(double gapX, double gapY) : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0, stacked = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            stacked += child.DesiredSize.Height;
        }
        int count = InternalChildren.Count;
        width += gapX * Math.Max(0, count - 1);
        if (double.IsFinite(availableSize.Width) && width > availableSize.Width) { return new Size(availableSize.Width, stacked + gapY * Math.Max(0, count - 1)); }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double total = gapX * Math.Max(0, InternalChildren.Count - 1), y = 0, height = 0;
        foreach (UIElement child in InternalChildren) { total += child.DesiredSize.Width; height = Math.Max(height, child.DesiredSize.Height); }
        bool stack = total > finalSize.Width + .01;
        for (int index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            if (stack) { child.Arrange(new Rect(0, y, child.DesiredSize.Width, child.DesiredSize.Height)); y += child.DesiredSize.Height + gapY; continue; }
            double x = index == 0 ? 0 : finalSize.Width - child.DesiredSize.Width;
            child.Arrange(new Rect(x, (height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}
