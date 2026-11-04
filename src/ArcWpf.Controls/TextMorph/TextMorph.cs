using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// text-morph.tsx (frozen 2026-11-03): one short label morphs into the next. Shared letters glide, new ones sharpen in, removed ones blur away, and the width springs to fit.
public class TextMorph : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextMorph), new FrameworkPropertyMetadata("", (d, _) => ((TextMorph)d).Sync(true)));
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(nameof(Tracking), typeof(double), typeof(TextMorph), new FrameworkPropertyMetadata(0d, (d, _) => ((TextMorph)d).Rebuild()));
    public static readonly DependencyProperty LineRatioProperty = DependencyProperty.Register(nameof(LineRatio), typeof(double), typeof(TextMorph), new FrameworkPropertyMetadata(double.NaN, (d, _) => ((TextMorph)d).Rebuild()));

    /// <summary>The current label; keep it to one short line, since it never wraps.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>Letter spacing in em.</summary>
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    /// <summary>Line box height as a multiple of the font size; NaN uses the font's own height.</summary>
    public double LineRatio { get => (double)GetValue(LineRatioProperty); set => SetValue(LineRatioProperty, value); }

    private const double Handoff = .05, Step = .032, EnterSeconds = .28, SoftBlur = 4;

    private sealed class Glyph(string key, string text, TrackedText view, ControlSpring x)
    {
        public string Key { get; } = key;
        public string Text { get; } = text;
        public TrackedText View { get; } = view;
        public ControlSpring X { get; } = x;
        public ScaleTransform Scale { get; } = new();
        public TranslateTransform Move { get; } = new();
        public double Advance { get; set; }
    }

    private readonly SpringDriver _driver;
    private readonly ControlSpring _width;
    private readonly List<Glyph> _glyphs = [];
    private readonly List<Glyph> _leaving = [];
    private readonly LinearGradientBrush _mask = new() { MappingMode = BrushMappingMode.Absolute, GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Transparent, 1) } };
    private Decorator? _host;
    private Canvas? _frame, _masked, _track;
    private double _lineHeight;

    static TextMorph() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TextMorph), new FrameworkPropertyMetadata(typeof(TextMorph)));

    public TextMorph()
    {
        _driver = new SpringDriver(Apply) { Settled = Rest };
        _width = _driver.Add(0, ArcSpring.Morph);
        Unloaded += (_, _) => FinishMotionForTest();
    }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    internal double FrameWidthForTest => _width.Position;
    internal double FrameTargetForTest => _width.Target;
    internal IReadOnlyList<string> GlyphKeysForTest => _glyphs.Select(g => g.Key).ToList();
    internal IReadOnlyList<string> LeavingKeysForTest => _leaving.Select(g => g.Key).ToList();
    internal double GlyphXForTest(string key) => _glyphs.First(g => g.Key == key).X.Position;
    internal double GlyphXTargetForTest(string key) => _glyphs.First(g => g.Key == key).X.Target;
    internal double GlyphOpacityForTest(string key) => _glyphs.First(g => g.Key == key).View.Opacity;
    internal bool IsMaskedForTest => _masked?.OpacityMask is not null;
    internal void FinishMotionForTest()
    {
        foreach (var glyph in _leaving.ToList()) { Discard(glyph); }
        foreach (var glyph in _glyphs) { Settle(glyph); }
        _driver.Finish();
        Rest();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcTextPeer(this, () => Text);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Rebuild();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // Font loading or a new size changes the width without a new label: follow it immediately.
        if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == FontWeightProperty) { Rebuild(); }
    }

    private void Rebuild()
    {
        if (_host is null) { return; }
        _driver.Stop();
        foreach (var glyph in _glyphs.Concat(_leaving)) { _driver.Remove(glyph.X); }
        _glyphs.Clear();
        _leaving.Clear();
        double em = FontSize;
        _lineHeight = double.IsNaN(LineRatio) ? FontFamily.LineSpacing * em : LineRatio * em;
        _track = new Canvas();
        // Room for blur and descenders around the frame; the mask's soft edge sits entirely in the room on the right, so resting text is never dimmed.
        _masked = new Canvas { Children = { _track }, Background = Brushes.Transparent, IsHitTestVisible = false };
        Canvas.SetLeft(_masked, -.2 * em);
        Canvas.SetTop(_masked, -.5 * em);
        Canvas.SetLeft(_track, .2 * em);
        Canvas.SetTop(_track, .5 * em);
        _frame = new Canvas { Children = { _masked }, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _host.Child = _frame;
        Sync(false);
    }

    private static List<(string Key, string Text)> ToGlyphs(string text)
    {
        var seen = new Dictionary<string, int>();
        var glyphs = new List<(string, string)>();
        var cursor = StringInfo.GetTextElementEnumerator(text);
        while (cursor.MoveNext())
        {
            string item = cursor.GetTextElement();
            seen[item] = seen.GetValueOrDefault(item) + 1;
            glyphs.Add(($"{item}-{seen[item] - 1}", item));
        }
        return glyphs;
    }

    private Glyph Create(string key, string text, double x)
    {
        var view = new TrackedText { Text = text == " " ? " " : text, Tracking = Tracking, LineRatio = _lineHeight / FontSize, IsHitTestVisible = false };
        var glyph = new Glyph(key, text, view, _driver.Add(x, ArcSpring.Morph));
        view.RenderTransform = new TransformGroup { Children = { glyph.Scale, glyph.Move } };
        _track!.Children.Add(view);
        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        glyph.Advance = view.DesiredSize.Width;
        glyph.Scale.CenterX = glyph.Advance / 2;
        glyph.Scale.CenterY = _lineHeight * .6;
        return glyph;
    }

    private void Sync(bool animate)
    {
        if (_track is null) { return; }
        bool moving = animate && Moving;
        var next = ToGlyphs(Text ?? "");
        var old = _glyphs.ToDictionary(g => g.Key);
        var wanted = next.Select(n => n.Key).ToHashSet();
        foreach (var glyph in _glyphs.Where(g => !wanted.Contains(g.Key)).ToList())
        {
            _glyphs.Remove(glyph);
            if (moving) { Leave(glyph); } else { Remove(glyph); }
        }
        _glyphs.Clear();
        double x = 0;
        int entering = 0;
        foreach (var (key, text) in next)
        {
            if (old.TryGetValue(key, out var kept))
            {
                // Only the letters that were already there glide.
                if (moving) { _driver.Animate(kept.X, x, ArcSpring.Morph); } else { kept.X.Snap(x); }
            }
            else
            {
                var back = _leaving.FirstOrDefault(g => g.Key == key);
                if (back is not null) { _leaving.Remove(back); kept = back; if (moving) { _driver.Animate(kept.X, x, ArcSpring.Morph); } else { kept.X.Snap(x); } }
                else { kept = Create(key, text, x); }
                if (moving) { Enter(kept, entering++); } else { Settle(kept); }
            }
            _glyphs.Add(kept);
            x += kept.Advance;
        }
        if (moving && _width.Position > 0 && Math.Abs(x - _width.Target) > .5)
        {
            _driver.Animate(_width, x, ArcSpring.Morph);
        }
        else { _width.Snap(x); }
        if (moving) { _masked!.OpacityMask = _mask; }
        else { _driver.Finish(); }
        Apply();
        if (!moving) { Rest(); }
        InvalidateMeasure();
    }

    private void Enter(Glyph glyph, int order)
    {
        double delay = Handoff + Math.Min(order, 8) * Step;
        var view = glyph.View;
        view.Opacity = 0;
        Tween.Set(glyph.Scale, ScaleTransform.ScaleXProperty, .8);
        Tween.Set(glyph.Scale, ScaleTransform.ScaleYProperty, .8);
        Tween.Set(glyph.Move, TranslateTransform.YProperty, .14 * FontSize);
        Tween.To(view, OpacityProperty, 1, EnterSeconds, ArcEasing.Enter, delay);
        Tween.To(glyph.Scale, ScaleTransform.ScaleXProperty, 1, EnterSeconds, ArcEasing.Enter, delay);
        Tween.To(glyph.Scale, ScaleTransform.ScaleYProperty, 1, EnterSeconds, ArcEasing.Enter, delay);
        Tween.To(glyph.Move, TranslateTransform.YProperty, 0, EnterSeconds, ArcEasing.Enter, delay);
        TextMotion.Blur(view, SoftBlur, 0, EnterSeconds, ArcEasing.Enter, delay);
    }

    private void Leave(Glyph glyph)
    {
        _leaving.Add(glyph);
        // It keeps its place: leaving letters come out of flow and do not follow the ones that glide.
        glyph.X.Snap(glyph.X.Position);
        var view = glyph.View;
        Tween.To(view, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard, 0, () => { if (_leaving.Contains(glyph)) { Discard(glyph); } });
        Tween.To(glyph.Scale, ScaleTransform.ScaleXProperty, .86, Tween.Exit, ArcEasing.Standard);
        Tween.To(glyph.Scale, ScaleTransform.ScaleYProperty, .86, Tween.Exit, ArcEasing.Standard);
        Tween.To(glyph.Move, TranslateTransform.YProperty, -.1 * FontSize, Tween.Exit, ArcEasing.Standard);
        TextMotion.Blur(view, 0, SoftBlur, Tween.Exit, ArcEasing.Standard);
    }

    private static void Settle(Glyph glyph)
    {
        Tween.Set(glyph.View, OpacityProperty, 1);
        Tween.Set(glyph.Scale, ScaleTransform.ScaleXProperty, 1);
        Tween.Set(glyph.Scale, ScaleTransform.ScaleYProperty, 1);
        Tween.Set(glyph.Move, TranslateTransform.YProperty, 0);
        glyph.View.Effect = null;
    }

    private void Discard(Glyph glyph) { _leaving.Remove(glyph); Remove(glyph); Rest(); }

    private void Remove(Glyph glyph) { _driver.Remove(glyph.X); _track?.Children.Remove(glyph.View); }

    private void Rest()
    {
        if (_masked is not null && _leaving.Count == 0 && !_driver.IsRunning) { _masked.OpacityMask = null; }
    }

    private void Apply()
    {
        if (_frame is null || _masked is null) { return; }
        double em = FontSize, width = _width.Position;
        _frame.Width = width;
        _frame.Height = _lineHeight;
        _masked.Width = width + .7 * em;
        _masked.Height = _lineHeight + em;
        _mask.StartPoint = new Point(width + .2 * em, 0);
        _mask.EndPoint = new Point(width + .7 * em, 0);
        foreach (var glyph in _glyphs.Concat(_leaving)) { Canvas.SetLeft(glyph.View, glyph.X.Position); }
    }
}
