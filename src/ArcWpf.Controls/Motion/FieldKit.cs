using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

// lucide-react glyphs used by the 2026-10-31 input batch, on the 24-unit grid with explicit separators for the WPF parser.
internal static class Lucide
{
    public const string X = "M18,6 L6,18 M6,6 L18,18";
    public const string Check = "M20,6 L9,17 L4,12";
    public const string ChevronDown = "M6,9 L12,15 L18,9";
    public const string Search = "M19,11 A8,8 0 1 1 3,11 A8,8 0 1 1 19,11 Z M21,21 L16.7,16.7";
    public const string RotateCcw = "M3,12 A9,9 0 1 0 12,3 A9.75,9.75 0 0 0 5.26,5.74 L3,8 M3,3 V8 H8";
    public const string Pipette = "M2,22 L3,21 H6 L15,12 M3,21 V18 L12,9 M15,6 L18.4,2.6 A2.1,2.1 0 1 1 21.4,5.6 L18,9 L18.4,9.4 A2.1,2.1 0 1 1 15.4,12.4 L11.6,8.6 A2.1,2.1 0 1 1 14.6,5.6 L15,6 Z";
    public const string Plus = "M5,12 H19 M12,5 V19";
    public const string Columns2 = "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z M12 3v18";
    public const string Rows2 = "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z M3 12h18";
    public const string UnfoldVertical = "M12 22v-6 M12 8V2 M4 12H2 M10 12H8 M16 12h-2 M22 12h-2 M15 19l-3 3-3-3 M15 5l-3-3-3 3";
    public const string Sparkles = "M9.94,15.5 A2,2 0 0 0 8.5,14.06 L2.37,12.48 A0.5,0.5 0 0 1 2.37,11.52 L8.5,9.94 A2,2 0 0 0 9.94,8.5 L11.52,2.37 A0.5,0.5 0 0 1 12.48,2.37 L14.06,8.5 A2,2 0 0 0 15.5,9.94 L21.63,11.52 A0.5,0.5 0 0 1 21.63,12.48 L15.5,14.06 A2,2 0 0 0 14.06,15.5 L12.48,21.63 A0.5,0.5 0 0 1 11.52,21.63 Z M20,3 V7 M22,5 H18 M4,17 V19 M5,18 H3";
    public const string Star = "M11.525,2.295 A0.53,0.53 0 0 1 12.475,2.295 L14.785,6.974 A2.123,2.123 0 0 0 16.38,8.134 L21.546,8.89 A0.53,0.53 0 0 1 21.84,9.794 L18.104,13.432 A2.123,2.123 0 0 0 17.493,15.31 L18.375,20.45 A0.53,0.53 0 0 1 17.604,21.01 L12.986,18.582 A2.122,2.122 0 0 0 11.013,18.582 L6.396,21.01 A0.53,0.53 0 0 1 5.626,20.45 L6.507,15.311 A2.122,2.122 0 0 0 5.896,13.432 L2.16,9.795 A0.53,0.53 0 0 1 2.454,8.889 L7.619,8.134 A2.122,2.122 0 0 0 9.216,6.974 Z";
    // 2026-11-02 theme and action controls.
    public const string Sun = "M12,8 A4,4,0,1,1,12,16 A4,4,0,1,1,12,8 Z M12,2 v2 M12,20 v2 M4.93,4.93 l1.41,1.41 M17.66,17.66 l1.41,1.41 M2,12 h2 M20,12 h2 M6.34,17.66 l-1.41,1.41 M19.07,4.93 l-1.41,1.41";
    public const string Moon = "M20.985,12.486 a9,9,0,1,1,-9.473,-9.472 c.405,-.022,.617,.46,.402,.803 a6,6,0,0,0,8.268,8.268 c.344,-.215,.825,-.004,.803,.401";
    public const string Bold = "M6,12 h9 a4,4,0,0,1,0,8 H7 a1,1,0,0,1,-1,-1 V5 a1,1,0,0,1,1,-1 h7 a4,4,0,0,1,0,8";
    public const string Italic = "M19,4 H10 M14,20 H5 M15,4 L9,20";
    public const string Underline = "M6,4 v6 a6,6,0,0,0,12,0 V4 M4,20 H20";
    public const string AlignStart = "M21,5 H3 M15,12 H3 M17,19 H3";
    public const string AlignCenter = "M21,5 H3 M17,12 H7 M19,19 H5";
    public const string AlignEnd = "M21,5 H3 M21,12 H9 M21,19 H7";
    public const string AlignJustify = "M3,5 h18 M3,12 h18 M3,19 h18";
    public const string UserPlus = "M16,21 v-2 a4,4,0,0,0,-4,-4 H6 a4,4,0,0,0,-4,4 v2 M9,3 A4,4,0,1,1,9,11 A4,4,0,1,1,9,3 Z M19,8 V14 M22,11 H16";
    public const string Send = "M14.536,21.686 a.5,.5,0,0,0,.937,-.024 l6.5,-19 a.496,.496,0,0,0,-.635,-.635 l-19,6.5 a.5,.5,0,0,0,-.024,.937 l7.93,3.18 a2,2,0,0,1,1.112,1.11 z M21.854,2.147 l-10.94,10.939";
    public const string CircleAlert = "M12,2 A10,10,0,1,1,12,22 A10,10,0,1,1,12,2 Z M12,8 V12 M12,16 H12.01";
    public const string Archive = "M3,3 h18 a1,1,0,0,1,1,1 v3 a1,1,0,0,1,-1,1 H3 a1,1,0,0,1,-1,-1 V4 a1,1,0,0,1,1,-1 z M4,8 v11 a2,2,0,0,0,2,2 h12 a2,2,0,0,0,2,-2 V8 M10,12 h4";
    public const string ChevronUp = "M6,15 L12,9 L18,15";
    public const string ArrowRight = "M5,12 H19 M12,5 L19,12 L12,19";
    public const string ArrowUp = "M12,19 V5 M5,12 L12,5 L19,12";
    public const string Pause = "M6,4 H9 V20 H6 Z M15,4 H18 V20 H15 Z";
    public const string Play = "M6,3 L20,12 L6,21 Z";
    public const string Trash = "M10,11 v6 M14,11 v6 M19,6 v14 a2,2,0,0,1,-2,2 H7 a2,2,0,0,1,-2,-2 V6 M3,6 h18 M8,6 V4 a2,2,0,0,1,2,-2 h4 a2,2,0,0,1,2,2 v2";

    // 2026-11-04 AI surfaces (lucide-static 0.460.0, ISC).
    public const string Globe = "M2,12.0 A10,10 0 1 1 22,12.0 A10,10 0 1 1 2,12.0 Z M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20 M2 12h20";
    public const string Brain = "M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z M12 5a3 3 0 1 1 5.997.125 4 4 0 0 1 2.526 5.77 4 4 0 0 1-.556 6.588A4 4 0 1 1 12 18Z M15 13a4.5 4.5 0 0 1-3-4 4.5 4.5 0 0 1-3 4 M17.599 6.5a3 3 0 0 0 .399-1.375 M6.003 5.125A3 3 0 0 0 6.401 6.5 M3.477 10.896a4 4 0 0 1 .585-.396 M19.938 10.5a4 4 0 0 1 .585.396 M6 18a4 4 0 0 1-1.967-.516 M19.967 17.484A4 4 0 0 1 18 18";
    public const string Mic = "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z M19 10v2a7 7 0 0 1-14 0v-2 M12,19 L12,22";
    public const string Paperclip = "m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48";
    public const string FileText = "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M14 2v4a2 2 0 0 0 2 2h4 M10 9H8 M16 13H8 M16 17H8";
    public const string Languages = "m5 8 6 6 M4 14l6-6 2-3 M2 5h12 M7 2h1 M22 22l-5-10-5 10 M14 18h6";
    public const string Lightbulb = "M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4";
    public const string Wrench = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z";
    public const string Shield = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z";
    public const string Ban = "M2,12.0 A10,10 0 1 1 22,12.0 A10,10 0 1 1 2,12.0 Z M4.9 4.9l14.2 14.2";
    public const string Copy = "M10,8.0 h10 a2,2 0 0 1 2,2 v10 a2,2 0 0 1 -2,2 h-10 a2,2 0 0 1 -2,-2 v-10 a2,2 0 0 1 2,-2 Z M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2";
    public const string AtSign = "M8,12.0 A4,4 0 1 1 16,12.0 A4,4 0 1 1 8,12.0 Z M16 8v5a3 3 0 0 0 6 0v-1a10 10 0 1 0-4 8";
    public const string Camera = "M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z M9,13.0 A3,3 0 1 1 15,13.0 A3,3 0 1 1 9,13.0 Z";
    public const string Layers = "m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z M22 17.65l-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65 M22 12.65l-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65";
    public const string Gauge = "m12 14 4-4 M3.34 19a10 10 0 1 1 17.32 0";
    public const string Pencil = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z M15 5l4 4";
    public const string Bug = "m8 2 1.88 1.88 M14.12 3.88 16 2 M9 7.13v-1a3.003 3.003 0 1 1 6 0v1 M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v3c0 3.3-2.7 6-6 6 M12 20v-9 M6.53 9C4.6 8.8 3 7.1 3 5 M6 13H2 M3 21c0-2.1 1.7-3.9 3.8-4 M20.97 5c0 2.1-1.6 3.8-3.5 4 M22 13h-4 M17.2 17c2.1.1 3.8 1.9 3.8 4";
    public const string SearchX = "m13.5 8.5-5 5 M8.5 8.5l5 5 M3,11.0 A8,8 0 1 1 19,11.0 A8,8 0 1 1 3,11.0 Z M21 21l-4.3-4.3";
    public const string ChevronRight = "m9 18 6-6-6-6";
    public const string ChevronLeft = "m15 18-6-6 6-6";
    public const string ThumbsUp = "M7 10v12 M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z";
    public const string ThumbsDown = "M17 14V2 M9 18.12 10 14H4.17a2 2 0 0 1-1.92-2.56l2.33-8A2 2 0 0 1 6.5 2H20a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2h-2.76a2 2 0 0 0-1.79 1.11L12 22a3.13 3.13 0 0 1-3-3.88Z";
    public const string Square = "M5,3.0 h14 a2,2 0 0 1 2,2 v14 a2,2 0 0 1 -2,2 h-14 a2,2 0 0 1 -2,-2 v-14 a2,2 0 0 1 2,-2 Z";
    public const string ExternalLink = "M15 3h6v6 M10 14 21 3 M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6";
    public const string ArrowDown = "M12 5v14 M19 12l-7 7-7-7";
    public const string Hammer = "m15 12-8.373 8.373a1 1 0 1 1-3-3L12 9 M18 15l4-4 M21.5 11.5l-1.914-1.914A2 2 0 0 1 19 8.172V7l-2.26-2.26a6 6 0 0 0-4.202-1.756L9 2.96l.92.82A6.18 6.18 0 0 1 12 8.4V10l2 2h1.172a2 2 0 0 1 1.414.586L18.5 14.5";
    public const string Image = "M5,3.0 h14 a2,2 0 0 1 2,2 v14 a2,2 0 0 1 -2,2 h-14 a2,2 0 0 1 -2,-2 v-14 a2,2 0 0 1 2,-2 Z M7,9.0 A2,2 0 1 1 11,9.0 A2,2 0 1 1 7,9.0 Z M21 15l-3.086-3.086a2 2 0 0 0-2.828 0L6 21";
    public const string CircleCheck = "M2,12.0 A10,10 0 1 1 22,12.0 A10,10 0 1 1 2,12.0 Z M9 12l2 2 4-4";
    public const string ShieldCheck = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M9 12l2 2 4-4";
    public const string ShieldAlert = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M12 8v4 M12 16h.01";
    public const string ShieldX = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M14.5 9.5l-5 5 M9.5 9.5l5 5";
    public const string User = "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2 M8,7 A4,4 0 1 1 16,7 A4,4 0 1 1 8,7 Z";
    public const string TextQuote = "M17 6H3 M21 12H8 M21 18H8 M3 12v6";
    public const string Download = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3";
    public const string SquarePen = "M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7 M18.375 2.625a1 1 0 0 1 3 3l-9.013 9.014a2 2 0 0 1-.853.505l-2.873.84a.5.5 0 0 1-.62-.62l.84-2.873a2 2 0 0 1 .506-.852z";

    /// <summary>A stroked glyph whose color follows the inherited TextElement.Foreground of its container.</summary>
    public static Viewbox Glyph(string data, double size, double stroke = 1.75)
    {
        var path = new Path { Data = Geometry.Parse(data), Width = 24, Height = 24, StrokeThickness = stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetBinding(Shape.StrokeProperty, new Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = RelativeSource.Self });
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }
}

internal static class Tween
{
    // CSS token durations (motion-tokens.ts).
    public const double Instant = .12, Fast = .16, Exit = .18, Standard = .24, Considered = .48;
    public const double Snappy = .26, Smooth = .4, Morph = .42;

    public static ArcMotionLevel Level(DependencyObject element) => ArcMotion.GetLevel(element);

    /// <summary>--ease-spring over --duration-spring (580 ms): the foundation's linear() samples, used by CSS press releases.</summary>
    public static readonly IEasingFunction SpringToken = Frozen(new SampledEase());
    public const double SpringTokenSeconds = .58;

    private sealed class SampledEase : EasingFunctionBase
    {
        private static readonly double[] Samples = [0, .0213, .0739, .1443, .2235, .305, .3849, .4606, .5305, .594, .6508, .7011, .7452, .7836, .8168, .8454, .8699, .8907, .9084, .9234, .9361, .9467, .9557, .9632, .9694, .9747, .979, .9826, .9857, .9882, .9902, .992, .9934, .9946, .9955, .9963, 1];
        public SampledEase() => EasingMode = EasingMode.EaseIn;
        protected override double EaseInCore(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            double at = t * (Samples.Length - 1);
            int i = (int)at;
            return Samples[i] + (Samples[i + 1] - Samples[i]) * (at - i);
        }
        protected override Freezable CreateInstanceCore() => new SampledEase();
    }

    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }

    /// <summary>CSS press: quick scale down (.1 s standard), spring release on the token curve.</summary>
    public static void Press(ScaleTransform scale, bool pressed, double down, bool animate, double downSeconds = .1)
    {
        double to = pressed ? down : 1;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            if (!animate) Set(scale, property, to);
            else if (pressed) To(scale, property, to, downSeconds, ArcEasing.Standard);
            else To(scale, property, to, SpringTokenSeconds, SpringToken);
        }
    }

    /// <summary>Tweens an internal property from its current (possibly animated) value; the end value is held by the clock.</summary>
    public static void To(IAnimatable target, DependencyProperty property, double to, double seconds, IEasingFunction? ease, double delay = 0, Action? done = null)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromSeconds(Math.Max(seconds, .001))) { EasingFunction = ease, BeginTime = TimeSpan.FromSeconds(delay) };
        if (done is not null) animation.Completed += (_, _) => done();
        target.BeginAnimation(property, animation);
    }

    /// <summary>A critically damped spring (bounce 0) with the given visual duration, sampled over twice that window.</summary>
    public static void Spring(IAnimatable target, DependencyProperty property, double to, double visualDuration, double delay = 0, Action? done = null)
        => To(target, property, to, visualDuration * 2, ArcEasing.Spring(visualDuration, visualDuration * 2), delay, done);

    public static void Set(IAnimatable target, DependencyProperty property, double value)
    {
        target.BeginAnimation(property, null);
        ((DependencyObject)target).SetValue(property, value);
    }

    /// <summary>Enter/exit with opacity, a vertical offset in DIPs and a blur, as the registry's text and glyph presence variants do.</summary>
    public static void Present(FrameworkElement element, bool show, double y, double blur, double seconds, IEasingFunction ease, double delay = 0, Action? done = null, double opacitySeconds = -1)
    {
        if (element.RenderTransform is not TranslateTransform move) element.RenderTransform = move = new TranslateTransform();
        double fromY = show ? y : 0, toY = show ? 0 : y;
        if (show) { element.Opacity = 0; move.Y = fromY; }
        To(element, UIElement.OpacityProperty, show ? 1 : 0, opacitySeconds > 0 ? opacitySeconds : seconds, ease, delay, done);
        if (y != 0) To(move, TranslateTransform.YProperty, toY, seconds, ease, delay);
        if (blur > 0)
        {
            var effect = new BlurEffect { Radius = show ? blur : 0 };
            element.Effect = effect;
            var animation = new DoubleAnimation(show ? 0 : blur, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease, BeginTime = TimeSpan.FromSeconds(delay) };
            if (show) animation.Completed += (_, _) => { if (ReferenceEquals(element.Effect, effect)) element.Effect = null; };
            effect.BeginAnimation(BlurEffect.RadiusProperty, animation);
        }
    }

    public static void Color(SolidColorBrush brush, System.Windows.Media.Color to, double seconds, bool animate)
    {
        if (!animate) { brush.BeginAnimation(SolidColorBrush.ColorProperty, null); brush.Color = to; return; }
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = ArcEasing.Standard });
    }

    public static System.Windows.Media.Color Resolve(FrameworkElement owner, string key) => (owner.TryFindResource(key) as SolidColorBrush)?.Color ?? Colors.Transparent;
}

/// <summary>
/// The registry's message row: the row opens its height from a true zero on the smooth spring while the copy rises in from .35em with
/// a soft blur; a new message replaces the old one in place (popLayout) and the old one fades out over .12 s. Closing springs the height to zero.
/// </summary>
internal sealed class FieldMessage : Grid
{
    private readonly Grid _content = new() { VerticalAlignment = VerticalAlignment.Top };
    private FrameworkElement? _current;
    private string? _key;
    private bool _open;

    public FieldMessage()
    {
        ClipToBounds = true;
        Height = 0;
        Opacity = 0;
        Children.Add(_content);
        SizeChanged += (_, e) => { if (e.WidthChanged && _open && !HasAnimatedProperties) Height = double.NaN; };
    }

    public string? Key => _key;

    public void Show(string key, FrameworkElement content, bool animate)
    {
        var level = Tween.Level(this);
        animate &= IsLoaded && level != ArcMotionLevel.Off;
        bool full = animate && level == ArcMotionLevel.Full;
        if (_open && key == _key) return;
        double from = _open ? (double.IsNaN(Height) ? ActualHeight : Height) : 0;
        if (_current is not null && _key != key)
        {
            var old = _current;
            old.IsHitTestVisible = false;
            if (animate) Tween.To(old, OpacityProperty, 0, .12, ArcEasing.Standard, done: () => _content.Children.Remove(old));
            else _content.Children.Remove(old);
        }
        _current = content;
        _key = key;
        _content.Children.Add(content);
        if (animate && _open)
        {
            if (full) Tween.Present(content, true, TextElement.GetFontSize(this) * .35, 4, Tween.Standard, ArcEasing.Enter);
            else Tween.Present(content, true, 0, 0, Tween.Instant, ArcEasing.Enter);
        }
        _open = true;
        Resize(from, animate, full, Tween.Fast);
    }

    public void Hide(bool animate)
    {
        if (!_open) return;
        var level = Tween.Level(this);
        animate &= IsLoaded && level != ArcMotionLevel.Off;
        _open = false;
        _key = null;
        var leaving = _current;
        _current = null;
        if (!animate || level != ArcMotionLevel.Full)
        {
            Tween.Set(this, HeightProperty, 0);
            Tween.Set(this, OpacityProperty, 0);
            _content.Children.Clear();
            return;
        }
        double from = double.IsNaN(Height) ? ActualHeight : Height;
        Height = from;
        Tween.To(this, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard);
        Tween.Spring(this, HeightProperty, 0, Tween.Smooth, done: () => { if (!_open && leaving is not null) _content.Children.Remove(leaving); });
    }

    private void Resize(double from, bool animate, bool full, double fadeSeconds)
    {
        _content.Measure(new Size(ActualWidth > 0 ? ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
        double to = _current?.DesiredSize.Height ?? 0;
        if (!full)
        {
            Tween.Set(this, HeightProperty, double.NaN);
            if (animate && Opacity < 1) Tween.To(this, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
            else Tween.Set(this, OpacityProperty, 1);
            return;
        }
        if (Opacity < 1) Tween.To(this, OpacityProperty, 1, fadeSeconds, ArcEasing.Standard);
        if (Math.Abs(to - from) < .5) { Tween.Set(this, HeightProperty, double.NaN); return; }
        BeginAnimation(HeightProperty, null);
        Height = from;
        Tween.Spring(this, HeightProperty, to, Tween.Smooth, done: () => { if (_open) Tween.Set(this, HeightProperty, double.NaN); });
    }

    /// <summary>Plain message copy: text-xs, leading-body, 8 DIP above.</summary>
    public static TextBlock Line(string text, string brush)
    {
        var line = new TextBlock { Text = text, FontSize = 12, LineHeight = 12 * 1.4, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(0, 8, 0, 0) };
        line.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return line;
    }
}

/// <summary>layout="position": capture where children sit, change the layout, then let each moved child glide from its old place.</summary>
internal static class Flip
{
    public static Dictionary<UIElement, Point> Capture(Panel panel)
    {
        var map = new Dictionary<UIElement, Point>();
        if (!panel.IsLoaded) return map;
        foreach (UIElement child in panel.Children) map[child] = child.TranslatePoint(Offset(child), panel);
        return map;
    }

    /// <summary>Children need a TranslateTransform as their RenderTransform; the offset springs back to zero.</summary>
    public static void Play(Panel panel, Dictionary<UIElement, Point> before, double visualDuration)
    {
        if (before.Count == 0) return;
        panel.UpdateLayout();
        foreach (UIElement child in panel.Children)
        {
            if (child.RenderTransform is not TranslateTransform move || !before.TryGetValue(child, out var was)) continue;
            var now = child.TranslatePoint(new Point(), panel);
            double x = was.X - now.X, y = was.Y - now.Y;
            if (Math.Abs(x) < .5 && Math.Abs(y) < .5) continue;
            Tween.Set(move, TranslateTransform.XProperty, x);
            Tween.Set(move, TranslateTransform.YProperty, y);
            Tween.Spring(move, TranslateTransform.XProperty, 0, visualDuration);
            Tween.Spring(move, TranslateTransform.YProperty, 0, visualDuration);
        }
    }

    private static Point Offset(UIElement child) => child.RenderTransform is TranslateTransform move ? new Point(move.X, move.Y) : new Point();
}

/// <summary>Outside-press detection for window-level handlers. A Popup's content routes its mouse events through its owner window as well,
/// so a press inside the popup must be told apart from a real outside press by walking up from the event source.</summary>
internal static class Outside
{
    public static bool IsWithin(object? source, params DependencyObject?[] roots)
    {
        for (var node = source as DependencyObject; node is not null; node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (Array.IndexOf(roots, node) >= 0) return true;
        return false;
    }
}

internal static class Announcer
{
    /// <summary>The registry's polite live region: hands the message to UI Automation clients.</summary>
    public static void Say(UIElement owner, string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        var peer = UIElementAutomationPeer.FromElement(owner) ?? UIElementAutomationPeer.CreatePeerForElement(owner);
        peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, message, "ArcWpf.Status");
    }
}

/// <summary>
/// The registry's small icon button (tag remove, recorder actions, picker head buttons, password reveal): a rounded hit area whose fill
/// and ink cross-fade over .16 s on hover or keyboard focus, with an optional press scale released on the spring token.
/// </summary>
internal sealed class FieldIconButton : System.Windows.Controls.Button
{
    private readonly Border _surface = new();
    private readonly SolidColorBrush _fill = new(Colors.Transparent), _ink = new(Colors.Gray);
    private readonly ScaleTransform _scale = new(1, 1);
    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register("ThemeProbe", typeof(object), typeof(FieldIconButton), new PropertyMetadata(null, (d, _) => ((FieldIconButton)d).Refresh(false)));

    public FieldIconButton(string glyph, double glyphSize, double size, double radius, double stroke = 1.75)
    {
        Width = Height = size;
        Cursor = System.Windows.Input.Cursors.Hand;
        FocusVisualStyle = null;
        Template = Bare;
        _surface.CornerRadius = new CornerRadius(radius);
        _surface.Background = _fill;
        _surface.Child = Lucide.Glyph(glyph, glyphSize, stroke);
        _surface.RenderTransform = _scale;
        _surface.RenderTransformOrigin = new Point(.5, .5);
        Content = _surface;
        SetValue(TextElement.ForegroundProperty, _ink);
        Loaded += (_, _) => Refresh(false);
        SetResourceReference(ThemeProbeProperty, "ArcWpf.Brush.Border");
    }

    private static readonly ControlTemplate Bare = CreateBare();
    private static ControlTemplate CreateBare()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        template.Seal();
        return template;
    }

    public string RestInk { get; set; } = "TextSecondary";
    public string HoverInk { get; set; } = "Foreground";
    public string? RestFill { get; set; }
    public string? HoverFill { get; set; } = "SurfaceMuted";
    /// <summary>0 keeps the button still when pressed.</summary>
    public double PressScale { get; set; }
    /// <summary>Pressed (or open) buttons keep the hover fill, as `:active` does.</summary>
    public bool Latched { get; set; }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsKeyboardFocusedProperty || e.Property == IsEnabledProperty) Refresh(true);
        else if (e.Property == IsPressedProperty)
        {
            Refresh(true);
            if (PressScale > 0) Tween.Press(_scale, IsPressed, PressScale, IsLoaded && Tween.Level(this) == ArcMotionLevel.Full, Tween.Instant);
        }
    }

    public void Refresh(bool animate)
    {
        animate &= IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        bool hot = IsEnabled && (IsMouseOver || IsKeyboardFocused || IsPressed || Latched);
        string? fill = hot ? HoverFill : RestFill;
        var color = fill is null ? WithAlpha(_fill.Color, 0) : Tween.Resolve(this, $"ArcWpf.Brush.{fill}");
        Tween.Color(_fill, color, Tween.Fast, animate);
        Tween.Color(_ink, Tween.Resolve(this, $"ArcWpf.Brush.{(hot ? HoverInk : RestInk)}"), Tween.Fast, animate);
    }

    private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color color, byte alpha) { color.A = alpha; return color; }
}
