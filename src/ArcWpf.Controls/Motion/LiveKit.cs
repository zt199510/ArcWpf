using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcWpf.Controls;

// Number text as a mechanical counter: each digit is a wheel that turns the way the value moved and wraps 9 to 0.
// The ones settle first and each higher place follows a beat later. A new column opens on a spring.
internal sealed class OdometerText : FrameworkElement
{
    private sealed class Column
    {
        public required string Key;
        public required char Glyph;
        public required double Natural;
        public required (int Place, int Run) Order;
        public required int Place;
        public GestureSpring Width = new(0, .05, .5), Wheel = new(0, .004, .04), Fade = new(0, .005, .05);
        public double Delay;
        public double Target;
        public bool Leaving;
        public bool Digit => char.IsAsciiDigit(Glyph);
    }

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(OdometerText), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    private const double Stagger = .025;
    private readonly double _size, _tracking, _line;
    private readonly FontWeight _weight;
    private readonly List<Column> _columns = [];
    private readonly FrameLoop _loop;
    private string _text = "";

    public OdometerText(double size, double lineRatio, double trackingPx, FontWeight weight)
    {
        (_size, _tracking, _line, _weight) = (size, trackingPx, size * lineRatio, weight);
        IsHitTestVisible = false;
        _loop = new FrameLoop(Frame);
        Unloaded += (_, _) => { _loop.Stop(); Finish(); };
    }

    public string Text => _text;
    public double Line => _line;
    public bool Running => _loop.IsRunning;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal IReadOnlyList<(string Key, double Wheel, double Delay)> ColumnsForTest => [.. _columns.Where(column => column.Digit && !column.Leaving).Select(column => (column.Key, column.Wheel.Position, column.Delay))];

    private FormattedText Face(string text) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, _weight, FontStretches.Normal), _size, (Brush)GetValue(ForegroundProperty), VisualTreeHelper.GetDpi(this).PixelsPerDip);

    public double Baseline { get { var face = Face("0"); return (_line - face.Height) / 2 + face.Baseline; } }

    // direction: 1 when the value rose, -1 when it fell; wheels turn that way around.
    public void Set(string text, int direction = 1)
    {
        if (text == _text && _columns.Count > 0) { return; }
        bool first = _columns.Count == 0, moving = !first && IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        _text = text;
        var wanted = new HashSet<string>();
        int place = text.Count(char.IsAsciiDigit), run = 0;
        foreach (char glyph in text)
        {
            bool digit = char.IsAsciiDigit(glyph);
            if (digit) { place--; }
            string key = digit ? $"d{place}" : $"s{place}-{run}-{glyph}";
            (int, int) order = (-place, digit ? 0 : 1 + run);
            run = digit ? 0 : run + 1;
            wanted.Add(key);
            var column = _columns.FirstOrDefault(item => item.Key == key);
            if (column is null)
            {
                column = new Column { Key = key, Glyph = glyph, Natural = Face(digit ? "0" : glyph.ToString()).WidthIncludingTrailingWhitespace + _tracking, Order = order, Place = place };
                if (digit) { column.Wheel.Jump(glyph - '0'); column.Target = glyph - '0'; }
                _columns.Add(column);
                if (!moving) { column.Width.Jump(column.Natural); column.Fade.Jump(1); }
            }
            column.Leaving = false;
            column.Glyph = glyph;
            if (digit)
            {
                int current = Mod10(column.Target), goal = glyph - '0';
                if (current != goal)
                {
                    int steps = direction >= 0 ? Mod10(goal - current) : -Mod10(current - goal);
                    column.Target += steps;
                    if (moving) { column.Delay = Stagger * Math.Max(0, place); column.Wheel.To(column.Target, SpringTune.Smooth); }
                    else { column.Wheel.Jump(column.Target); }
                }
            }
            if (moving) { column.Width.To(column.Natural, SpringTune.Morph); column.Fade.To(1, SpringTune.Morph); }
        }
        foreach (var column in _columns.Where(column => !wanted.Contains(column.Key)).ToList())
        {
            if (!moving) { _columns.Remove(column); continue; }
            column.Leaving = true;
            column.Width.To(0, SpringTune.Morph);
            column.Fade.To(0, SpringTune.Morph);
        }
        _columns.Sort((a, b) => a.Order.CompareTo(b.Order));
        if (moving) { _loop.Start(); }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static int Mod10(double value) => (int)(((Math.Round(value) % 10) + 10) % 10);

    private void Finish()
    {
        _columns.RemoveAll(column => column.Leaving);
        foreach (var column in _columns) { column.Width.Jump(column.Natural); column.Fade.Jump(1); column.Wheel.Jump(column.Target); column.Delay = 0; }
    }

    private bool Frame(double seconds)
    {
        bool running = false;
        foreach (var column in _columns)
        {
            column.Width.Step(seconds);
            column.Fade.Step(seconds);
            if (column.Delay > 0) { column.Delay = Math.Max(0, column.Delay - seconds); running = true; }
            else { column.Wheel.Step(seconds); }
            running |= column.Width.Active || column.Fade.Active || column.Wheel.Active;
        }
        _columns.RemoveAll(column => column.Leaving && !column.Width.Active);
        InvalidateMeasure();
        InvalidateVisual();
        return running;
    }

    protected override Size MeasureOverride(Size availableSize) => new(_columns.Sum(column => Math.Max(0, column.Width.Position)), _line);

    protected override void OnRender(DrawingContext dc)
    {
        double x = 0, pad = _size * .18;
        foreach (var column in _columns)
        {
            double width = Math.Max(0, column.Width.Position);
            if (width > .01)
            {
                dc.PushClip(new RectangleGeometry(new Rect(x, -pad, width, _line + pad * 2)));
                dc.PushOpacity(Math.Clamp(column.Fade.Position, 0, 1));
                if (column.Digit) { DrawWheel(dc, column, x); }
                else { var face = Face(column.Glyph.ToString()); dc.DrawText(face, new Point(x, (_line - face.Height) / 2)); }
                dc.Pop();
                dc.Pop();
            }
            x += width;
        }
    }

    private void DrawWheel(DrawingContext dc, Column column, double x)
    {
        double at = column.Wheel.Position;
        int near = (int)Math.Floor(at);
        for (int offset = near - 1; offset <= near + 2; offset++)
        {
            double gap = offset - at, opacity = Math.Clamp(1 - Math.Abs(gap) * 1.2, 0, 1);
            if (opacity <= 0) { continue; }
            var face = Face(Mod10(offset).ToString(CultureInfo.InvariantCulture));
            dc.PushOpacity(opacity);
            // A higher digit waits below and rolls up into place, like the wheel it is.
            dc.DrawText(face, new Point(x + (column.Natural - _tracking - face.WidthIncludingTrailingWhitespace) / 2, (_line - face.Height) / 2 + gap * _size));
            dc.Pop();
        }
    }
}

internal static class OkColor
{
    private static double ToLinear(byte channel) { double c = channel / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
    private static byte FromLinear(double c) { c = Math.Clamp(c, 0, 1); return (byte)Math.Round(255 * (c <= .0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - .055)); }

    internal static (double L, double A, double B) Lab(Color color)
    {
        double r = ToLinear(color.R), g = ToLinear(color.G), b = ToLinear(color.B);
        double l = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b), m = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b), s = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        return (.2104542553 * l + .793617785 * m - .0040720468 * s, 1.9779984951 * l - 2.428592205 * m + .4505937099 * s, .0259040371 * l + .7827717662 * m - .808675766 * s);
    }

    public static Color FromLab(double l, double a, double b, byte alpha = 255)
    {
        double p = l + .3963377774 * a + .2158037573 * b, q = l - .1055613458 * a - .0638541728 * b, s = l - .0894841775 * a - 1.291485548 * b;
        p = p * p * p; q = q * q * q; s = s * s * s;
        return Color.FromArgb(alpha, FromLinear(4.0767416621 * p - 3.3077115913 * q + .2309699292 * s), FromLinear(-1.2684380046 * p + 2.6097574011 * q - .3413193965 * s), FromLinear(-.0041960863 * p - .7034186147 * q + 1.707614701 * s));
    }

    public static Color FromLch(double l, double c, double h, byte alpha = 255) => FromLab(l, c * Math.Cos(h * Math.PI / 180), c * Math.Sin(h * Math.PI / 180), alpha);

    // CSS color-mix(in oklab, first share%, second).
    public static Color Mix(Color first, Color second, double share)
    {
        var (l1, a1, b1) = Lab(first);
        var (l2, a2, b2) = Lab(second);
        return FromLab(l1 * share + l2 * (1 - share), a1 * share + a2 * (1 - share), b1 * share + b2 * (1 - share));
    }

    public static Color Alpha(Color color, double alpha) { color.A = (byte)Math.Round(255 * alpha); return color; }
}

// A code-built button that opens something: invoked by automation, and reports whether it is open.
internal sealed class DisclosurePeer(FrameworkElement owner, Func<string> name, Func<bool> expanded, Action toggle, AutomationControlType type = AutomationControlType.Button) : FrameworkElementAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => type;
    protected override string GetNameCore() => name();
    protected override bool IsKeyboardFocusableCore() => owner.Focusable;
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface is PatternInterface.Invoke or PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
    public void Invoke() => toggle();
    public ExpandCollapseState ExpandCollapseState => expanded() ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    public void Expand() { if (!expanded()) { toggle(); } }
    public void Collapse() { if (expanded()) { toggle(); } }
}

// A focusable surface that is a link or a button in the automation tree.
internal sealed class InvokePeer(FrameworkElement owner, AutomationControlType type, Func<string> name, Action invoke, Func<string>? status = null) : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => type;
    protected override string GetNameCore() => name();
    protected override string GetItemStatusCore() => status?.Invoke() ?? "";
    protected override bool IsKeyboardFocusableCore() => owner.Focusable;
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
    public void Invoke() => invoke();
}
