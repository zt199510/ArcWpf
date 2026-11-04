using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum IconMorphName { Menu, Play, Plus, Copy, Theme, Chevron, Bell, Heart }
public enum IconMorphPointing { Down, Right }

// icon-morph.tsx (frozen 2026-11-03): icons that morph between two states on interruptible springs instead of swapping. Decorative: put one inside a button that carries the name and the pressed state.
public class IconMorph : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconMorphName), typeof(IconMorph), new FrameworkPropertyMetadata(IconMorphName.Menu, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((IconMorph)d).Snap()));
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(bool), typeof(IconMorph), new FrameworkPropertyMetadata(false, (d, _) => ((IconMorph)d).OnState()));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(IconMorph), new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(IconMorph), new FrameworkPropertyMetadata(1.75, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TurnProperty = DependencyProperty.Register(nameof(Turn), typeof(double), typeof(IconMorph), new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PointingProperty = DependencyProperty.Register(nameof(Pointing), typeof(IconMorphPointing), typeof(IconMorph), new FrameworkPropertyMetadata(IconMorphPointing.Down, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(IconMorph), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LikeBrushProperty = DependencyProperty.Register(nameof(LikeBrush), typeof(Brush), typeof(IconMorph), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty FadeProperty = Track("Fade");
    private static readonly DependencyProperty LateFadeProperty = Track("LateFade");
    private static readonly DependencyProperty OneShotProperty = Track("OneShot");

    private static DependencyProperty Track(string name) => DependencyProperty.Register(name, typeof(double), typeof(IconMorph), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public IconMorphName Icon { get => (IconMorphName)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>The second state. Menu: close, Play: pause, Plus and Copy: check, Theme: moon, Chevron: turned, Bell: notifications on, Heart: liked.</summary>
    public bool State { get => (bool)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Stroke width in the 24 unit grid.</summary>
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>Degrees the chevron turns when State is true.</summary>
    public double Turn { get => (double)GetValue(TurnProperty); set => SetValue(TurnProperty, value); }
    /// <summary>Where the chevron points at rest.</summary>
    public IconMorphPointing Pointing { get => (IconMorphPointing)GetValue(PointingProperty); set => SetValue(PointingProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    /// <summary>The liked heart's color; the danger role by default.</summary>
    public Brush? LikeBrush { get => (Brush?)GetValue(LikeBrushProperty); set => SetValue(LikeBrushProperty, value); }

    private static readonly Point[][] PlayShape = [[new(7, 4.5), new(12, 7.42), new(12, 16.58), new(7, 19.5)], [new(12, 7.42), new(19.5, 11.8), new(19.5, 12.2), new(12, 16.58)]];
    private static readonly Point[][] PauseShape = [[new(6.5, 5), new(10, 5), new(10, 19), new(6.5, 19)], [new(14, 5), new(17.5, 5), new(17.5, 19), new(14, 19)]];
    private static readonly double[] RingAngles = [0, 16, -13, 9, -6, 3, 0], ClapperOffsets = [0, -2.2, 2, -1.4, .9, -.4, 0];
    private static readonly Geometry BackSheet = Frozen("M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");
    private static readonly Geometry Bell = Frozen("M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9");
    private static readonly Geometry Clapper = Frozen("M10.3 21a1.94 1.94 0 0 0 3.4 0");
    private static readonly Geometry Heart = Frozen("M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z");
    private static readonly Rect GridBox = new(-8, -8, 40, 40);
    private const double RingSeconds = .9, ClapDelay = .04, BurstSeconds = .6, PopSeconds = .45, CheckDelay = .06;

    private static Geometry Frozen(string data) { var geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }

    private readonly SpringDriver _driver;
    private readonly ControlSpring _shape, _fold;
    private readonly DispatcherTimer _late = new();
    private bool _oneShot;

    public IconMorph()
    {
        IsHitTestVisible = false;
        _driver = new SpringDriver(InvalidateVisual);
        _shape = _driver.Add(0, ArcSpring.Morph);
        _fold = _driver.Add(0, ArcSpring.Snappy);
        _late.Interval = TimeSpan.FromSeconds(CheckDelay);
        _late.Tick += (_, _) => { _late.Stop(); _driver.Animate(_fold, State ? 1 : 0, ArcSpring.Smooth); };
        Unloaded += (_, _) => { _late.Stop(); Snap(); };
        Loaded += (_, _) => Snap();
    }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private double Fade => (double)GetValue(FadeProperty);
    private double LateFade => (double)GetValue(LateFadeProperty);
    private double OneShot => (double)GetValue(OneShotProperty);

    internal double ShapeForTest => _shape.Position;
    internal double FoldForTest => _fold.Position;
    internal double FadeForTest => Fade;
    internal bool IsPlayingOneShotForTest => _oneShot;
    internal double RingAngleForTest(double seconds) => Keyframe(RingAngles, seconds / RingSeconds);
    internal void HoldForTest(double shape, double fold, double fade, double shot)
    {
        _late.Stop();
        _driver.Stop();
        _shape.Snap(shape);
        _fold.Snap(fold);
        Tween.Set(this, FadeProperty, fade);
        Tween.Set(this, LateFadeProperty, fade);
        _oneShot = shot >= 0;
        Tween.Set(this, OneShotProperty, Math.Max(shot, 0));
        InvalidateVisual();
    }
    internal void FinishMotionForTest() { _late.Stop(); _oneShot = false; Snap(); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    private void OnState()
    {
        if (!Moving) { Snap(); return; }
        double target = State ? 1 : 0;
        double visual = Icon is IconMorphName.Copy or IconMorphName.Bell ? ArcSpring.Smooth : ArcSpring.Morph;
        _driver.Animate(_shape, target, visual);
        switch (Icon)
        {
            case IconMorphName.Menu:
                _driver.Animate(_fold, target, ArcSpring.Snappy);
                FadeTo(Tween.Fast, null);
                break;
            case IconMorphName.Copy:
                FadeTo(Tween.Fast, null);
                // The check starts drawing a beat after the sheets begin to clear, and retracts at once.
                _late.Stop();
                if (State) { _late.Start(); Tween.To(this, LateFadeProperty, 1, .08, null, CheckDelay); }
                else { _driver.Animate(_fold, 0, ArcSpring.Smooth); Tween.To(this, LateFadeProperty, 0, .08, null); }
                break;
            case IconMorphName.Theme:
                FadeTo(Tween.Fast, null);
                break;
            case IconMorphName.Bell:
                FadeTo(.08, null);
                if (State) { Fire(RingSeconds + ClapDelay); }
                break;
            case IconMorphName.Heart:
                FadeTo(Tween.Fast, ArcEasing.Standard);
                if (State) { Fire(BurstSeconds); } else { CancelShot(); }
                break;
        }
    }

    private void FadeTo(double seconds, IEasingFunction? ease) => Tween.To(this, FadeProperty, State ? 1 : 0, seconds, ease);

    // Ring and burst run on elapsed seconds, restarting on every turn on.
    private void Fire(double seconds)
    {
        _oneShot = true;
        SetValue(OneShotProperty, 0d);
        var clock = new DoubleAnimation(0, seconds, TimeSpan.FromSeconds(seconds));
        clock.Completed += (_, _) => { _oneShot = false; InvalidateVisual(); };
        BeginAnimation(OneShotProperty, clock);
    }

    private void CancelShot() { _oneShot = false; Tween.Set(this, OneShotProperty, 0); }

    private void Snap()
    {
        double target = State ? 1 : 0;
        _late.Stop();
        _driver.Stop();
        _shape.Snap(target);
        _fold.Snap(target);
        Tween.Set(this, FadeProperty, target);
        Tween.Set(this, LateFadeProperty, target);
        CancelShot();
        InvalidateVisual();
    }

    /// <summary>A value along evenly spaced keyframes, eased out within each segment as motion's keyframes are.</summary>
    private static double Keyframe(double[] values, double t)
    {
        if (t <= 0) { return values[0]; }
        if (t >= 1) { return values[^1]; }
        double at = t * (values.Length - 1);
        int i = (int)at;
        double f = at - i;
        double eased = 1 - (1 - f) * (1 - f);
        return values[i] + (values[i + 1] - values[i]) * eased;
    }

    private Pen Stroke(Brush brush, double? thickness = null) => new(brush, thickness ?? StrokeThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static Point Lerp(Point a, Point b, double t) => new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    private static Geometry Poly(IReadOnlyList<Point> points, bool close)
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(points[0], close, close);
            for (int i = 1; i < points.Count; i++) { c.LineTo(points[i], true, false); }
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>The first share of a polyline, motion's pathLength.</summary>
    private static List<Point> Partial(Point[] line, double share)
    {
        double total = 0;
        for (int i = 1; i < line.Length; i++) { total += (line[i] - line[i - 1]).Length; }
        double left = total * Math.Clamp(share, 0, 1);
        var points = new List<Point> { line[0] };
        for (int i = 1; i < line.Length && left > 0; i++)
        {
            double length = (line[i] - line[i - 1]).Length;
            points.Add(Lerp(line[i - 1], line[i], Math.Min(1, left / length)));
            left -= length;
        }
        return points;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var ink = Foreground ?? Brushes.Black;
        double scale = Size / 24;
        dc.PushTransform(new ScaleTransform(scale, scale));
        switch (Icon)
        {
            case IconMorphName.Menu: DrawMenu(dc, ink); break;
            case IconMorphName.Play: DrawPlay(dc, ink); break;
            case IconMorphName.Plus: DrawPlus(dc, ink); break;
            case IconMorphName.Copy: DrawCopy(dc, ink); break;
            case IconMorphName.Theme: DrawTheme(dc, ink); break;
            case IconMorphName.Chevron: DrawChevron(dc, ink); break;
            case IconMorphName.Bell: DrawBell(dc, ink); break;
            case IconMorphName.Heart: DrawHeart(dc, ink); break;
        }
        dc.Pop();
    }

    private void DrawMenu(DrawingContext dc, Brush ink)
    {
        double p = _shape.Position, radians = 45 * p * Math.PI / 180, dx = 8 * Math.Cos(radians), dy = 8 * Math.Sin(radians);
        var pen = Stroke(ink);
        double top = 6 + 6 * p, bottom = 18 - 6 * p;
        dc.DrawLine(pen, new Point(12 - dx, top - dy), new Point(12 + dx, top + dy));
        dc.PushOpacity(1 - Fade);
        double half = 8 * (1 - .8 * _fold.Position);
        dc.DrawLine(pen, new Point(12 - half, 12), new Point(12 + half, 12));
        dc.Pop();
        dc.DrawLine(pen, new Point(12 - dx, bottom + dy), new Point(12 + dx, bottom - dy));
    }

    private void DrawPlay(DrawingContext dc, Brush ink)
    {
        var pen = Stroke(ink, 1.5);
        for (int shape = 0; shape < 2; shape++)
        {
            var points = new Point[4];
            for (int i = 0; i < 4; i++) { points[i] = Lerp(PlayShape[shape][i], PauseShape[shape][i], _shape.Position); }
            dc.DrawGeometry(ink, pen, Poly(points, true));
        }
    }

    private void DrawPlus(DrawingContext dc, Brush ink)
    {
        double p = _shape.Position;
        var pen = Stroke(ink);
        dc.DrawLine(pen, Lerp(new Point(12, 5), new Point(20, 6.5), p), Lerp(new Point(12, 19), new Point(9.5, 17), p));
        dc.DrawLine(pen, Lerp(new Point(5, 12), new Point(4.5, 12), p), Lerp(new Point(19, 12), new Point(9.5, 17), p));
    }

    private void DrawCopy(DrawingContext dc, Brush ink)
    {
        double p = _shape.Position, fade = 1 - Fade;
        var pen = Stroke(ink);
        dc.PushOpacity(fade);
        dc.PushTransform(new TranslateTransform(4 * p, 4 * p));
        dc.DrawGeometry(null, pen, BackSheet);
        dc.Pop();
        dc.PushTransform(new ScaleTransform(1 - .3 * p, 1 - .3 * p, 15, 15));
        dc.DrawRoundedRectangle(null, pen, new Rect(8, 8, 14, 14), 2, 2);
        dc.Pop();
        dc.Pop();
        if (LateFade <= 0) { return; }
        dc.PushOpacity(LateFade);
        var drawn = Partial([new Point(5, 12.5), new Point(9.5, 17), new Point(19, 7.5)], _fold.Position);
        if (drawn.Count > 1) { dc.DrawGeometry(null, pen, Poly(drawn, false)); }
        dc.Pop();
    }

    private void DrawTheme(DrawingContext dc, Brush ink)
    {
        double p = _shape.Position, disc = Lerp(4.5, 8.5, p);
        var shadow = new Point(Lerp(30, 18, p), Lerp(-6, 6, p));
        const double shadowRadius = 6.5;
        var pen = Stroke(ink);
        // The crescent's outline: the disc's edge outside the shadow, and the shadow's edge inside the disc.
        dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(GridBox), new EllipseGeometry(shadow, shadowRadius, shadowRadius)));
        dc.DrawEllipse(null, pen, new Point(12, 12), disc, disc);
        dc.Pop();
        dc.PushClip(new EllipseGeometry(new Point(12, 12), disc, disc));
        dc.DrawEllipse(null, pen, shadow, shadowRadius, shadowRadius);
        dc.Pop();
        dc.PushOpacity(1 - Fade);
        dc.PushTransform(new RotateTransform(90 * p, 12, 12));
        dc.PushTransform(new ScaleTransform(1 - .6 * p, 1 - .6 * p, 12, 12));
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4, c = Math.Cos(angle), s = Math.Sin(angle);
            dc.DrawLine(pen, new Point(12 + c * 7.5, 12 + s * 7.5), new Point(12 + c * 10, 12 + s * 10));
        }
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void DrawChevron(DrawingContext dc, Brush ink)
    {
        dc.PushTransform(new RotateTransform(Turn * _shape.Position, 12, 12));
        Point[] line = Pointing == IconMorphPointing.Down ? [new(6, 9), new(12, 15), new(18, 9)] : [new(9, 6), new(15, 12), new(9, 18)];
        dc.DrawGeometry(null, Stroke(ink), Poly(line, false));
        dc.Pop();
    }

    private void DrawBell(DrawingContext dc, Brush ink)
    {
        double on = _shape.Position, slashShare = 1 - on, slashFade = 1 - Fade;
        var pen = Stroke(ink);
        double t = _oneShot ? OneShot : -1;
        var slash = Partial([new Point(3, 3), new Point(21, 21)], slashShare);
        bool cut = slash.Count > 1 && slashFade > 0;
        // The slash cuts a gap through the bell wider than itself, as the source's mask does.
        if (cut)
        {
            var gap = Poly(slash, false).GetWidenedPathGeometry(new Pen(Brushes.Black, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
            dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(GridBox), gap));
        }
        dc.PushTransform(new RotateTransform(t < 0 ? 0 : Keyframe(RingAngles, t / RingSeconds), 12, 2));
        dc.DrawGeometry(null, pen, Bell);
        dc.Pop();
        dc.PushTransform(new TranslateTransform(t < 0 ? 0 : Keyframe(ClapperOffsets, (t - ClapDelay) / RingSeconds), 0));
        dc.DrawGeometry(null, pen, Clapper);
        dc.Pop();
        if (cut) { dc.Pop(); }
        if (slash.Count > 1 && slashFade > 0)
        {
            dc.PushOpacity(slashFade);
            dc.DrawGeometry(null, pen, Poly(slash, false));
            dc.Pop();
        }
    }

    private void DrawHeart(DrawingContext dc, Brush ink)
    {
        double fill = Fade;
        var like = LikeBrush as SolidColorBrush ?? TryFindResource("ArcWpf.Brush.ArcDanger") as SolidColorBrush;
        var from = (ink as SolidColorBrush)?.Color ?? Colors.Black;
        var color = like is null ? from : Mix(from, like.Color, fill);
        var brush = new SolidColorBrush(color);
        var pen = Stroke(brush);
        double scale = 1;
        if (_oneShot && State)
        {
            double t = OneShot, ringT = ArcEasing.Enter.Ease(Math.Min(t / .5, 1)), r = Lerp(4, 13, ringT);
            double width = 3 * (1 - ringT);
            if (width > .01) { dc.PushOpacity(.5 * (1 - ringT)); dc.DrawEllipse(null, new Pen(brush, width), new Point(12, 12), r, r); dc.Pop(); }
            double sparkT = ArcEasing.Enter.Ease(Math.Clamp((t - .05) / .55, 0, 1));
            for (int i = 0; i < 7; i++)
            {
                double angle = -Math.PI / 2 + i * 2 * Math.PI / 7, reach = Lerp(7, 14, sparkT), size = (i % 2 == 1 ? 1 : 1.4) * Lerp(1, .3, sparkT);
                dc.PushOpacity(1 - sparkT);
                dc.DrawEllipse(brush, null, new Point(12 + Math.Cos(angle) * reach, 12 + Math.Sin(angle) * reach), size, size);
                dc.Pop();
            }
            double popAt = Math.Clamp(t / PopSeconds, 0, 1) * 2;
            scale = popAt < 1 ? Lerp(.72, 1.14, ArcEasing.Enter.Ease(popAt)) : Lerp(1.14, 1, ArcEasing.Enter.Ease(popAt - 1));
        }
        dc.PushTransform(new ScaleTransform(scale, scale, 12, 12));
        if (fill > 0)
        {
            dc.PushOpacity(fill);
            dc.DrawGeometry(brush, null, Heart);
            dc.Pop();
        }
        dc.DrawGeometry(null, pen, Heart);
        dc.Pop();
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)Lerp(a.R, b.R, t), (byte)Lerp(a.G, b.G, t), (byte)Lerp(a.B, b.B, t));
}
