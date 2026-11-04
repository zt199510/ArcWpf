using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

/// <summary>One run of text with CSS letter-spacing and a CSS line box: WPF has neither, and its line height biases the baseline.</summary>
internal sealed class TrackedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TrackedText), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(TrackedText), new FrameworkPropertyMetadata(14d, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(TrackedText), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(TrackedText), new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(TrackedText), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>Letter spacing in em, added after every character as CSS does.</summary>
    public double Tracking { get; init; }
    /// <summary>Height of the line box as a multiple of the font size; NaN uses the font's own height.</summary>
    public double LineRatio { get; init; } = double.NaN;

    public FormattedText Format(string text)
    {
        var typeface = new Typeface((FontFamily)GetValue(FontFamilyProperty), FontStyles.Normal, (FontWeight)GetValue(FontWeightProperty), FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, (double)GetValue(FontSizeProperty), (Brush)GetValue(ForegroundProperty), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    /// <summary>The advance of a space in this font, the gap CSS leaves between inline words.</summary>
    public double SpaceWidth => Format(" ").WidthIncludingTrailingWhitespace + Tracking * (double)GetValue(FontSizeProperty);

    private double Line(FormattedText sample) => double.IsNaN(LineRatio) ? sample.Height : LineRatio * (double)GetValue(FontSizeProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text;
        var whole = Format(text.Length == 0 ? " " : text);
        double width = text.Length == 0 ? 0 : whole.WidthIncludingTrailingWhitespace + new StringInfo(text).LengthInTextElements * Tracking * (double)GetValue(FontSizeProperty);
        return new Size(width, Line(whole));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var text = Text;
        if (text.Length == 0) { return; }
        double size = (double)GetValue(FontSizeProperty);
        var whole = Format(text);
        double top = (Line(whole) - whole.Height) / 2;
        if (Tracking == 0) { dc.DrawText(whole, new Point(0, top)); return; }
        // Each character sits after the kerned width of everything before it, plus the spacing.
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        for (int i = 0; i < starts.Length; i++)
        {
            int end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            double x = (i == 0 ? 0 : Format(text[..starts[i]]).WidthIncludingTrailingWhitespace) + i * Tracking * size;
            dc.DrawText(Format(text[starts[i]..end]), new Point(x, top));
        }
    }
}

/// <summary>Lays words out as inline text: greedy lines, forced breaks, and optionally text-wrap: balance.</summary>
internal sealed class WordFlow : Panel
{
    public static readonly DependencyProperty BreakBeforeProperty = DependencyProperty.RegisterAttached("BreakBefore", typeof(bool), typeof(WordFlow), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
    public static void SetBreakBefore(UIElement element, bool value) => element.SetValue(BreakBeforeProperty, value);

    public double Gap { get; set; }
    public double LineHeight { get; set; }
    public bool Balance { get; init; }
    /// <summary>Upper limit on the line length, in DIPs: the 24ch of a CSS max-width.</summary>
    public double MaxLine { get; set; } = double.PositiveInfinity;

    internal int LineCountForTest { get; private set; }

    private List<(UIElement Child, double X, int Line)> Flow(double width)
    {
        var placed = new List<(UIElement, double, int)>(InternalChildren.Count);
        double x = 0; int line = 0; bool first = true;
        foreach (UIElement child in InternalChildren)
        {
            double w = child.DesiredSize.Width;
            bool forced = (bool)child.GetValue(BreakBeforeProperty);
            if (!first && (forced || x + Gap + w > width + .01)) { line++; x = 0; first = true; }
            if (!first) { x += Gap; }
            placed.Add((child, x, line));
            x += w;
            first = false;
        }
        return placed;
    }

    private double Solve(double width)
    {
        if (!Balance || InternalChildren.Count == 0) { return width; }
        // text-wrap: balance: the narrowest width that does not add a line.
        int lines = Flow(width)[^1].Line;
        double widest = 0;
        foreach (UIElement child in InternalChildren) { widest = Math.Max(widest, child.DesiredSize.Width); }
        double low = widest, high = double.IsInfinity(width) ? Flow(width).Max(p => p.X + p.Child.DesiredSize.Width) : width;
        for (int i = 0; i < 16; i++)
        {
            double middle = (low + high) / 2;
            if (Flow(middle)[^1].Line > lines) { low = middle; } else { high = middle; }
        }
        return high;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren) { child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        if (InternalChildren.Count == 0) { return default; }
        _constraint = availableSize.Width;
        var flow = Flow(Solve(Math.Min(availableSize.Width, MaxLine)));
        LineCountForTest = flow[^1].Line + 1;
        return new Size(flow.Max(p => p.X + p.Child.DesiredSize.Width), LineCountForTest * LineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0) { return finalSize; }
        // The measured width is the widest line, so it would only reproduce the breaks; the constraint is what decides them.
        foreach (var (child, x, line) in Flow(Solve(Math.Min(_constraint, MaxLine))))
        {
            child.Arrange(new Rect(x, line * LineHeight, child.DesiredSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }

    private double _constraint = double.PositiveInfinity;
}

/// <summary>Mixes colors in Oklab, as CSS color-mix(in oklab, ...) does.</summary>
internal static class OkMix
{
    private static double Linear(byte c) { double v = c / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
    private static byte Gamma(double v) { v = Math.Clamp(v, 0, 1); return (byte)Math.Round(255 * (v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - .055)); }

    private static (double L, double A, double B) ToLab(Color c)
    {
        double r = Linear(c.R), g = Linear(c.G), b = Linear(c.B);
        double l = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b), m = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b), s = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        return (.2104542553 * l + .7936177850 * m - .0040720468 * s, 1.9779984951 * l - 2.4285922050 * m + .4505937099 * s, .0259040371 * l + .7827717662 * m - .8086757660 * s);
    }

    /// <summary>The share <paramref name="amount"/> of <paramref name="a"/> mixed into <paramref name="b"/>.</summary>
    public static Color Mix(Color a, double amount, Color b)
    {
        var (la, aa, ba) = ToLab(a);
        var (lb, ab, bb) = ToLab(b);
        double L = la * amount + lb * (1 - amount), A = aa * amount + ab * (1 - amount), B = ba * amount + bb * (1 - amount);
        double l = Math.Pow(L + .3963377774 * A + .2158037573 * B, 3), m = Math.Pow(L - .1055613458 * A - .0638541728 * B, 3), s = Math.Pow(L - .0894841775 * A - 1.2914855480 * B, 3);
        return Color.FromRgb(Gamma(4.0767416621 * l - 3.3077115913 * m + .2309699292 * s), Gamma(-1.2684380046 * l + 2.6097574011 * m - .3413193965 * s), Gamma(-.0041960863 * l - .7034186147 * m + 1.7076147010 * s));
    }
}

internal static class TextMotion
{
    /// <summary>A CSS blur tween. Blurring in, the effect is dropped on landing so text returns to plain, crisp rendering.</summary>
    public static void Blur(UIElement element, double fromCss, double toCss, double seconds, IEasingFunction? ease, double delay = 0)
    {
        var effect = new BlurEffect { Radius = fromCss * 2, RenderingBias = RenderingBias.Performance };
        element.Effect = effect;
        var animation = new DoubleAnimation(toCss * 2, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease, BeginTime = TimeSpan.FromSeconds(delay) };
        if (toCss == 0) { animation.Completed += (_, _) => { if (ReferenceEquals(element.Effect, effect)) { element.Effect = null; } }; }
        effect.BeginAnimation(BlurEffect.RadiusProperty, animation);
    }
}

/// <summary>Exposes a code-built text control to automation as text with its plain string as the name.</summary>
internal sealed class ArcTextPeer(FrameworkElement owner, Func<string> name) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    protected override string GetNameCore() => name();
    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
