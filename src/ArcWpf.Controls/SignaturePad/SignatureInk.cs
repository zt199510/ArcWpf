using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArcWpf.Controls;

public enum SignatureInkColor { Black, Blue, Violet }
public enum SignatureInkWidth { Fine, Medium, Bold }

/// <summary>A sampled point: position in pad units, pen pressure from 0 to 1 (-1 when the device has none), and ms since the stroke began.</summary>
public readonly record struct SignatureInkPoint(double X, double Y, double P, double T);

/// <summary>One stroke of a signature. <see cref="At"/> is when it began, in ms, so replay keeps the real rhythm between strokes.</summary>
public sealed class SignatureInkStroke
{
    public required string Id { get; init; }
    public required List<SignatureInkPoint> Points { get; init; }
    public SignatureInkColor Color { get; init; }
    public SignatureInkWidth Width { get; init; }
    public double At { get; init; }
}

public sealed record SignatureOutlineOptions(double Size, double Thinning = .62, double Streamline = .42, bool SimulatePressure = true, double TaperStart = 0, double TaperEnd = 0, bool Last = true);

// The stroke geometry of signature-pad.tsx, in the spirit of perfect-freehand: same steps, same constants, same rounding.
public static class SignatureInk
{
    public const double PadWidth = 600, PadHeight = 260;

    private readonly record struct Vec(double X, double Y)
    {
        public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec operator *(Vec a, double n) => new(a.X * n, a.Y * n);
    }
    private sealed record Sample(Vec At, double Pressure, double Length, Vec Vector) { public Vec Vector { get; set; } = Vector; }

    private static Vec Lerp(Vec a, Vec b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static double Dist(Vec a, Vec b) => Hypot(a.X - b.X, a.Y - b.Y);
    private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
    private static Vec Norm(Vec a) { double l = Hypot(a.X, a.Y); return l != 0 ? new Vec(a.X / l, a.Y / l) : new Vec(0, 0); }
    private static Vec Perp(Vec a) => new(a.Y, -a.X);
    private static double Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y;
    private static Vec RotAround(Vec p, Vec c, double r)
    {
        double s = Math.Sin(r), co = Math.Cos(r), px = p.X - c.X, py = p.Y - c.Y;
        return new Vec(px * co - py * s + c.X, px * s + py * co + c.Y);
    }
    private static double EaseOut(double t) => t * (2 - t);

    public static double SizeOf(SignatureInkWidth width) => width switch { SignatureInkWidth.Fine => 3.4, SignatureInkWidth.Bold => 8.4, _ => 5.6 };
    public static string ExportColor(SignatureInkColor color) => color switch { SignatureInkColor.Blue => "#1d3fae", SignatureInkColor.Violet => "#5b35c8", _ => "#17171a" };

    /// <summary>Turns sampled points into the outline polygon of a variable width stroke.</summary>
    public static IReadOnlyList<Point> Outline(IReadOnlyList<SignatureInkPoint> input, SignatureOutlineOptions options)
    {
        if (input.Count == 0) { return []; }
        var (size, thinning, streamline, simulate, taperStart, taperEnd, last) = options;
        var head = input[0];
        bool usePen = !simulate && input.Any(point => point.P > 0);

        // Streamline: each point eases toward the raw input, which removes jitter without a smoothing pass.
        var pts = new List<Sample>();
        var prev = new Vec(head.X, head.Y);
        double length = 0;
        var start = new Sample(prev, usePen ? head.P : .5, 0, new Vec(1, 0));
        pts.Add(start);
        for (int i = 1; i < input.Count; i++)
        {
            var raw = new Vec(input[i].X, input[i].Y);
            var at = i == input.Count - 1 && last ? raw : Lerp(prev, raw, 1 - streamline);
            double d = Dist(at, prev);
            if (d < .4) { continue; }
            length += d;
            pts.Add(new Sample(at, usePen ? input[i].P : .5, length, Norm(prev - at)));
            prev = at;
        }
        if (pts.Count > 1) { start.Vector = pts[1].Vector; }
        double total = length;

        // A dot: a round mark sized by the first pressure.
        if (pts.Count == 1)
        {
            double r = Math.Max(.6, size * (.5 - thinning * (.5 - start.Pressure)) * .9);
            return Enumerable.Range(0, 18).Select(i => { var v = start.At + new Vec(Math.Cos(i / 18d * Math.PI * 2), Math.Sin(i / 18d * Math.PI * 2)) * r; return new Point(v.X, v.Y); }).ToList();
        }

        var left = new List<Vec>();
        var right = new List<Vec>();
        double pressure = start.Pressure;
        var prevVector = start.Vector;
        for (int i = 0; i < pts.Count; i++)
        {
            var point = pts[i];
            if (!usePen)
            {
                // Speed thins the line: a long step between samples reads as a fast stroke.
                double step = i == 0 ? 0 : Dist(point.At, pts[i - 1].At);
                double speed = Math.Min(1, step / size);
                double target = Math.Min(1, 1 - speed);
                pressure = Math.Min(1, pressure + (target - pressure) * speed * .3);
            }
            else { pressure = point.Pressure; }
            double radius = Math.Max(.25, size * (.5 - thinning * (.5 - pressure)));
            double ts = taperStart != 0 ? EaseOut(Math.Min(1, point.Length / taperStart)) : 1;
            double te = taperEnd != 0 && last ? EaseOut(Math.Min(1, (total - point.Length) / taperEnd)) : 1;
            radius = Math.Max(.12 * size, radius * Math.Min(ts, te));

            var next = i + 1 < pts.Count ? pts[i + 1].Vector : point.Vector;
            var offset = Perp(Lerp(next, point.Vector, .5)) * radius;
            // A sharp turn: sweep round the corner so the outline does not pinch.
            if (i > 0 && i < pts.Count - 1 && Dot(point.Vector, next) < -.2)
            {
                var corner = Perp(prevVector) * radius;
                for (double t = 0; t <= 1; t += 1d / 12)
                {
                    left.Add(RotAround(point.At - corner, point.At, Math.PI * t));
                    right.Add(RotAround(point.At + corner, point.At, -Math.PI * t));
                }
            }
            else
            {
                left.Add(point.At - offset);
                right.Add(point.At + offset);
            }
            prevVector = point.Vector;
        }

        // Round caps: half circles swept around the first and last points, on the side facing away from the stroke.
        static List<Vec> Cap(Vec center, Vec from, Vec to, Vec away)
        {
            var result = new List<Vec>();
            double r = Dist(center, from), begin = Math.Atan2(from.Y - center.Y, from.X - center.X);
            double mid = begin + Math.PI / 2;
            int sign = Dot(new Vec(Math.Cos(mid), Math.Sin(mid)), away) >= 0 ? 1 : -1;
            for (int k = 1; k < 12; k++)
            {
                double a = begin + sign * Math.PI * k / 12;
                result.Add(new Vec(center.X + Math.Cos(a) * r, center.Y + Math.Sin(a) * r));
            }
            result.Add(to);
            return result;
        }
        var end = pts[^1];
        var endCap = Cap(end.At, left[^1], right[^1], end.Vector * -1);
        var startCap = Cap(start.At, right[0], left[0], start.Vector);
        right.Reverse();
        return left.Concat(endCap).Concat(right).Concat(startCap).Select(v => new Point(v.X, v.Y)).ToList();
    }

    // Math.round(value * 100) / 100, printed the way JavaScript prints a number.
    private static string Round(double value)
    {
        double rounded = Math.Floor(value * 100 + .5) / 100;
        return (rounded == 0 ? 0 : rounded).ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>Draws an outline as one smooth closed path, with quadratic curves through the midpoints of its edges.</summary>
    public static string OutlineToPath(IReadOnlyList<Point> points)
    {
        if (points.Count < 3) { return ""; }
        var d = new StringBuilder($"M{Round(points[0].X)} {Round(points[0].Y)} Q");
        for (int i = 0; i < points.Count; i++)
        {
            Point a = points[i], b = points[(i + 1) % points.Count];
            d.Append(Round(a.X)).Append(' ').Append(Round(a.Y)).Append(' ').Append(Round((a.X + b.X) / 2)).Append(' ').Append(Round((a.Y + b.Y) / 2)).Append(' ');
        }
        return d.Append('Z').ToString();
    }

    internal static SignatureOutlineOptions OptionsFor(SignatureInkWidth width, IReadOnlyList<SignatureInkPoint> points, bool last)
    {
        double size = SizeOf(width);
        bool pen = points.Any(point => point.P > 0);
        return new SignatureOutlineOptions(size, SimulatePressure: !pen, TaperStart: size * 1.5, TaperEnd: pen ? 0 : size * 5, Last: last);
    }

    /// <summary>The SVG path of a stroke, or of its first <paramref name="upTo"/> ms while it is replayed.</summary>
    public static string StrokePath(SignatureInkStroke stroke, double upTo = double.PositiveInfinity)
    {
        var all = stroke.Points;
        bool done = upTo >= all[^1].T;
        var points = done ? all : all.Where(point => point.T <= upTo).ToList();
        return OutlineToPath(Outline(points, OptionsFor(stroke.Width, stroke.Points, done)));
    }

    // SVG fills with the nonzero rule; WPF path markup asks for it with F1.
    internal static Geometry ToGeometry(string path)
    {
        if (path.Length == 0) { return Geometry.Empty; }
        var geometry = Geometry.Parse("F1 " + path);
        geometry.Freeze();
        return geometry;
    }

    private static (int X, int Y, int Width, int Height) InkBounds(IReadOnlyList<SignatureInkStroke> strokes)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var stroke in strokes)
        {
            foreach (var point in stroke.Points)
            {
                minX = Math.Min(minX, point.X); minY = Math.Min(minY, point.Y); maxX = Math.Max(maxX, point.X); maxY = Math.Max(maxY, point.Y);
            }
        }
        const double PAD = 16;
        return ((int)Math.Floor(minX - PAD), (int)Math.Floor(minY - PAD), (int)Math.Ceiling(maxX - minX + PAD * 2), (int)Math.Ceiling(maxY - minY + PAD * 2));
    }

    /// <summary>A standalone SVG of the signature, cropped to the ink, with print ink colors.</summary>
    public static string ToSvg(IReadOnlyList<SignatureInkStroke> strokes)
    {
        if (strokes.Count == 0) { return ""; }
        var box = InkBounds(strokes);
        string paths = string.Concat(strokes.Select(stroke => $"<path d=\"{StrokePath(stroke)}\" fill=\"{ExportColor(stroke.Color)}\"/>"));
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{box.X} {box.Y} {box.Width} {box.Height}\" width=\"{box.Width}\" height=\"{box.Height}\">{paths}</svg>";
    }

    /// <summary>A transparent PNG of the signature, cropped to the ink, at <paramref name="scale"/> times pad resolution.</summary>
    public static byte[] ToPng(IReadOnlyList<SignatureInkStroke> strokes, int scale = 3)
    {
        if (strokes.Count == 0) { throw new InvalidOperationException("Nothing to export"); }
        var box = InkBounds(strokes);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(scale, scale));
            context.PushTransform(new TranslateTransform(-box.X, -box.Y));
            foreach (var stroke in strokes)
            {
                context.DrawGeometry(new SolidColorBrush((Color)ColorConverter.ConvertFromString(ExportColor(stroke.Color))), null, ToGeometry(StrokePath(stroke)));
            }
        }
        var bitmap = new RenderTargetBitmap(box.Width * scale, box.Height * scale, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
