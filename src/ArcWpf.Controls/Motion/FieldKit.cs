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
    public const string Sparkles = "M9.94,15.5 A2,2 0 0 0 8.5,14.06 L2.37,12.48 A0.5,0.5 0 0 1 2.37,11.52 L8.5,9.94 A2,2 0 0 0 9.94,8.5 L11.52,2.37 A0.5,0.5 0 0 1 12.48,2.37 L14.06,8.5 A2,2 0 0 0 15.5,9.94 L21.63,11.52 A0.5,0.5 0 0 1 21.63,12.48 L15.5,14.06 A2,2 0 0 0 14.06,15.5 L12.48,21.63 A0.5,0.5 0 0 1 11.52,21.63 Z M20,3 V7 M22,5 H18 M4,17 V19 M5,18 H3";
    public const string Star = "M11.525,2.295 A0.53,0.53 0 0 1 12.475,2.295 L14.785,6.974 A2.123,2.123 0 0 0 16.38,8.134 L21.546,8.89 A0.53,0.53 0 0 1 21.84,9.794 L18.104,13.432 A2.123,2.123 0 0 0 17.493,15.31 L18.375,20.45 A0.53,0.53 0 0 1 17.604,21.01 L12.986,18.582 A2.122,2.122 0 0 0 11.013,18.582 L6.396,21.01 A0.53,0.53 0 0 1 5.626,20.45 L6.507,15.311 A2.122,2.122 0 0 0 5.896,13.432 L2.16,9.795 A0.53,0.53 0 0 1 2.454,8.889 L7.619,8.134 A2.122,2.122 0 0 0 9.216,6.974 Z";

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
