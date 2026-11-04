using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

// The arithmetic of sparkline.tsx.
internal static class SparklineMath
{
    public const double Inset = 5, Cap = 1.25;
    public static readonly double[] Grid = [.. Enumerable.Range(0, 161).Select(index => index / 160d)];

    // A smooth line through every point, sampled densely in unit space (x and y both 0 to 1, y up). Monotone cubic tangents keep it from swinging past the data.
    public static Point[] Curve(IReadOnlyList<double> data)
    {
        double min = data.Min(), span = data.Max() - min;
        double[] ys = [.. data.Select(value => span != 0 ? (value - min) / span : .5)];
        int last = ys.Length - 1;
        double step = 1d / last;
        double[] slopes = [.. Enumerable.Range(0, last).Select(index => (ys[index + 1] - ys[index]) / step)];
        var tangents = new double[ys.Length];
        for (int index = 0; index <= last; index++)
        {
            tangents[index] = index == 0 || index == last ? slopes[0] : (Math.Sign(slopes[index - 1]) + Math.Sign(slopes[index])) * Math.Min(Math.Min(Math.Abs(slopes[index - 1]), Math.Abs(slopes[index])), Math.Abs(slopes[index - 1] + slopes[index]) / 4);
        }
        if (last > 1) { tangents[0] = (3 * slopes[0] - tangents[1]) / 2; tangents[last] = (3 * slopes[last - 1] - tangents[last - 1]) / 2; }
        int samples = Math.Max(1, Math.Min(16, (int)Math.Round(128d / last, MidpointRounding.AwayFromZero)));
        var points = new List<Point>(last * samples + 1);
        for (int index = 0; index < last; index++)
        {
            for (int sample = 0; sample < samples; sample++)
            {
                double t = (double)sample / samples, t2 = t * t, t3 = t2 * t;
                double y = (2 * t3 - 3 * t2 + 1) * ys[index] + (t3 - 2 * t2 + t) * step * tangents[index] + (3 * t2 - 2 * t3) * ys[index + 1] + (t3 - t2) * step * tangents[index + 1];
                points.Add(new Point((index + t) * step, Math.Clamp(y, 0, 1)));
            }
        }
        points.Add(new Point(1, ys[last]));
        return [.. points];
    }

    // Height of the sampled line at x, straight between samples.
    public static double HeightAt(Point[] shape, double x)
    {
        int low = 0, high = shape.Length - 1;
        if (x <= shape[low].X) { return shape[low].Y; }
        if (x >= shape[high].X) { return shape[high].Y; }
        while (high - low > 1) { int middle = (low + high) >> 1; if (shape[middle].X < x) { low = middle; } else { high = middle; } }
        var (a, b) = (shape[low], shape[high]);
        return b.X == a.X ? b.Y : a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
    }

    public static double ToY(double y, double h) => Inset + (1 - y) * (h - Inset * 2);

    // Where the pen is along the line once progress of its length is drawn, as a point count and a final partial point.
    public static (int Count, Point Tip) PenAt(Point[] points, double progress)
    {
        double total = 0;
        for (int index = 1; index < points.Length; index++) { total += (points[index] - points[index - 1]).Length; }
        double left = progress * total;
        for (int index = 1; index < points.Length; index++)
        {
            double step = (points[index] - points[index - 1]).Length;
            if (left <= step)
            {
                double t = step > 0 ? left / step : 1;
                return (index, new Point(points[index - 1].X + (points[index].X - points[index - 1].X) * t, points[index - 1].Y + (points[index].Y - points[index - 1].Y) * t));
            }
            left -= step;
        }
        return (points.Length - 1, points[^1]);
    }

    public static double AmountIn(string text)
    {
        var match = Regex.Match(text.Replace(",", "").Replace('−', '-'), @"-?\d+(?:\.\d+)?");
        return match.Success ? double.Parse(match.Value, CultureInfo.InvariantCulture) : double.NaN;
    }
}

// Text that changes rolls in place, like a numeric content transition; morph springs the wrapper to the new width so nothing beside it jumps.
internal sealed class RollText : Panel
{
    private sealed record Chunk(string Slot, string Key, string Text);
    private sealed class Piece { public required string Slot; public required string Key; public required TrackedText View; public double X; }

    private readonly double _size, _lineRatio, _tracking;
    private readonly FontWeight _weight;
    private readonly bool _whole, _morph;
    private readonly List<Piece> _live = [], _leaving = [];
    private readonly GestureSpring _width = new(0, .05, .5);
    private readonly FrameLoop _loop;
    private double _natural;
    private string _text = "";
    private bool _measured;

    public RollText(double size, double lineRatio, double trackingPx, FontWeight weight, bool whole = false, bool morph = false)
    {
        (_size, _lineRatio, _tracking, _weight, _whole, _morph) = (size, lineRatio, trackingPx, weight, whole, morph);
        IsHitTestVisible = false;
        _loop = new FrameLoop(seconds => { _width.Step(seconds); InvalidateMeasure(); return _width.Active; });
        Unloaded += (_, _) => { _loop.Stop(); _width.Jump(_width.Target); };
    }

    internal string TextForTest => _text;
    internal IReadOnlyList<string> KeysForTest => [.. _live.Select(piece => piece.Key)];
    internal int LeavingForTest => _leaving.Count;
    internal double WidthForTest => _width.Position;
    internal double WidthTargetForTest => _width.Target;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);

    private bool Numeric(string text) => !_whole && !text.Any(char.IsLetter);

    // A number keeps its unchanged digits still, keyed by place from the right. Words key on everything up to them, so only the first changed word and what follows it roll.
    private List<Chunk> Chunks(string text)
    {
        var chunks = new List<Chunk>();
        if (text.Length == 0) { return chunks; }
        if (_whole) { chunks.Add(new Chunk("all", text, text)); return chunks; }
        if (Numeric(text))
        {
            for (int index = 0; index < text.Length; index++) { chunks.Add(new Chunk($"n{text.Length - index}", text[index].ToString(), text[index].ToString())); }
            return chunks;
        }
        string[] words = Regex.Split(text, @"(?<=\s)");
        string prefix = "";
        for (int index = 0; index < words.Length; index++)
        {
            prefix += words[index];
            chunks.Add(new Chunk($"w{index}", prefix, words[index]));
        }
        return chunks;
    }

    public void Set(string text, Brush foreground, string toneKey, int? direction = null)
    {
        if (_measured && text == _text && _live.All(piece => piece.Key.StartsWith(toneKey, StringComparison.Ordinal))) { return; }
        int move = direction ?? (SparklineMath.AmountIn(text) < SparklineMath.AmountIn(_text) ? -1 : 1);
        _text = text;
        var chunks = Chunks(text);
        var next = new List<Piece>(chunks.Count);
        foreach (var chunk in chunks)
        {
            string key = toneKey + chunk.Key;
            var old = _live.FirstOrDefault(piece => piece.Slot == chunk.Slot);
            if (old is not null && old.Key == key) { _live.Remove(old); next.Add(old); continue; }
            var view = CopyMotion.Text(chunk.Text, _size, _lineRatio, _tracking, _weight, foreground);
            Children.Add(view);
            if (_measured) { CopyMotion.In(view, move, this); }
            next.Add(new Piece { Slot = chunk.Slot, Key = key, View = view });
        }
        // Outgoing pieces are popped out of flow, so the wrapper never holds both widths.
        foreach (var gone in _live)
        {
            _leaving.Add(gone);
            CopyMotion.Out(gone.View, move, this, () => { Children.Remove(gone.View); _leaving.Remove(gone); });
        }
        _live.Clear();
        _live.AddRange(next);
        Fit();
    }

    private void Fit()
    {
        double x = 0;
        foreach (var piece in _live)
        {
            piece.View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            piece.X = x;
            x += piece.View.DesiredSize.Width;
        }
        _natural = x;
        if (!_morph || !_measured || !IsLoaded || Tween.Level(this) != ArcMotionLevel.Full) { _width.Jump(x); }
        else if (Math.Abs(x - _width.Target) >= .5) { _width.To(x, SpringTune.Morph); _loop.Start(); }
        else { _width.Jump(x); }
        _measured = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children) { child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        double width = _morph ? _width.Position : _natural;
        if (_whole && double.IsFinite(availableSize.Width)) { width = Math.Min(width, availableSize.Width); }
        return new Size(width, _size * _lineRatio);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Numbers stay anchored to their right edge.
        double shift = Numeric(_text) ? finalSize.Width - _natural : 0;
        foreach (var piece in _live) { piece.View.Arrange(new Rect(piece.X + shift, 0, piece.View.DesiredSize.Width, piece.View.DesiredSize.Height)); }
        foreach (var piece in _leaving) { piece.View.Arrange(new Rect(piece.X + shift, 0, piece.View.DesiredSize.Width, piece.View.DesiredSize.Height)); }
        return finalSize;
    }

    // overflow-x: clip, which leaves the rising copy free above and below.
    protected override Geometry GetLayoutClip(Size layoutSlotSize) => new RectangleGeometry(new Rect(0, -_size, RenderSize.Width, RenderSize.Height + _size * 2));
}

// sparkline.tsx (frozen 2026-11-03): a small line chart with a headline value, scrubbed by pointer or keyboard.
public class Sparkline : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(IReadOnlyList<double>), typeof(Sparkline), new FrameworkPropertyMetadata(null, (d, _) => ((Sparkline)d).OnData()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Sparkline), new FrameworkPropertyMetadata("", (d, _) => ((Sparkline)d).Caption()));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Sparkline), new FrameworkPropertyMetadata(null, (d, _) => ((Sparkline)d).Caption()));
    public static readonly DependencyProperty ChangeProperty = DependencyProperty.Register(nameof(Change), typeof(string), typeof(Sparkline), new FrameworkPropertyMetadata(null, (d, _) => ((Sparkline)d).Caption()));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(ArcTone), typeof(Sparkline), new FrameworkPropertyMetadata(ArcTone.Accent, (d, _) => ((Sparkline)d).OnTone(true)));
    public static readonly DependencyProperty DrawWidthProperty = DependencyProperty.Register(nameof(DrawWidth), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(160d, (d, _) => ((Sparkline)d)._plot.InvalidateMeasure()));
    public static readonly DependencyProperty DrawHeightProperty = DependencyProperty.Register(nameof(DrawHeight), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(52d, (d, _) => ((Sparkline)d)._plot.InvalidateMeasure()));
    public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(nameof(Labels), typeof(IReadOnlyList<string>), typeof(Sparkline), new FrameworkPropertyMetadata(null, (d, _) => ((Sparkline)d).Caption()));
    public static readonly DependencyProperty FormatValueProperty = DependencyProperty.Register(nameof(FormatValue), typeof(Func<double, int, string>), typeof(Sparkline), new FrameworkPropertyMetadata(null, (d, _) => ((Sparkline)d).Caption()));
    public static readonly DependencyProperty ShowAreaProperty = DependencyProperty.Register(nameof(ShowArea), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(true, (d, _) => ((Sparkline)d)._plot.InvalidateVisual()));
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(true, (d, _) => ((Sparkline)d).OnInteractive()));
    private static readonly DependencyProperty DrawnProperty = DependencyProperty.Register("Drawn", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0d, (d, _) => ((Sparkline)d)._plot.InvalidateVisual()));
    private static readonly DependencyProperty MorphProperty = DependencyProperty.Register("Morph", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(1d, (d, _) => ((Sparkline)d).OnMorph()));
    private static readonly DependencyProperty TrailOpacityProperty = DependencyProperty.Register("TrailOpacity", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0d, (d, _) => ((Sparkline)d)._plot.InvalidateVisual()));
    private static readonly DependencyProperty CursorOpacityProperty = DependencyProperty.Register("CursorOpacity", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0d, (d, _) => ((Sparkline)d)._plot.InvalidateVisual()));
    private static readonly DependencyProperty DotOpacityProperty = DependencyProperty.Register("DotOpacity", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0d, (d, _) => ((Sparkline)d)._plot.InvalidateVisual()));

    /// <summary>Points in order, oldest first.</summary>
    public IReadOnlyList<double>? Data { get => (IReadOnlyList<double>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>What is measured. Names the chart.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Headline value at rest. While scrubbing it shows the point under the cursor.</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Aside text at rest, such as "+4.1%".</summary>
    public string? Change { get => (string?)GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }
    /// <summary>Line and change color.</summary>
    public ArcTone Tone { get => (ArcTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    /// <summary>Drawing width that, with DrawHeight, sets the plot's proportions.</summary>
    public double DrawWidth { get => (double)GetValue(DrawWidthProperty); set => SetValue(DrawWidthProperty, value); }
    public double DrawHeight { get => (double)GetValue(DrawHeightProperty); set => SetValue(DrawHeightProperty, value); }
    /// <summary>One label per point, such as a date, shown while scrubbing.</summary>
    public IReadOnlyList<string>? Labels { get => (IReadOnlyList<string>?)GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <summary>Formats a scrubbed point. Defaults to a grouped number.</summary>
    public Func<double, int, string>? FormatValue { get => (Func<double, int, string>?)GetValue(FormatValueProperty); set => SetValue(FormatValueProperty, value); }
    /// <summary>Quiet fill under the line.</summary>
    public bool ShowArea { get => (bool)GetValue(ShowAreaProperty); set => SetValue(ShowAreaProperty, value); }
    /// <summary>Pointer and keyboard scrubbing through the points.</summary>
    public bool IsInteractive { get => (bool)GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    // The plot is the scrubber: hover or drag across it, or focus it and use the arrow keys.
    private sealed class Plot(Sparkline owner) : FrameworkElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? owner.DrawWidth : availableSize.Width;
            return new Size(width, width * owner.DrawHeight / owner.DrawWidth);
        }
        protected override void OnRender(DrawingContext dc) { dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize)); owner.Draw(dc, ActualWidth, ActualHeight); }
        protected override AutomationPeer OnCreateAutomationPeer() => owner.MakePeer(this);
    }

    private readonly Plot _plot;
    private readonly FrameLoop _loop;
    private readonly GestureSpring _cursor = new(1, .0005, .005), _dot = new(0);
    private readonly FocusVisible _focus;
    private readonly InViewOnce _inView;
    private readonly SolidColorBrush _line = new(), _copy = new(), _wash = new(), _ink = new(), _secondary = new();
    private Decorator? _host;
    private RollText? _labelText, _headline, _aside;
    private Border? _headlineBox;
    private Point[] _target = [], _shape = [], _morphFrom = [], _morphTo = [];
    private double[] _morphXs = [];
    private string _dataKey = "";
    private int? _active;
    private int _moved = 1;
    private bool _landed;

    static Sparkline() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Sparkline), new FrameworkPropertyMetadata(typeof(Sparkline)));

    public Sparkline()
    {
        _plot = new Plot(this) { Focusable = true, FocusVisualStyle = null };
        _plot.SizeChanged += (_, _) => _plot.InvalidateVisual();
        _loop = new FrameLoop(Frame);
        _focus = new FocusVisible(_plot, _plot.InvalidateVisual);
        _inView = new InViewOnce(this, StartDraw);
        _plot.MouseDown += (_, e) => { if (Scrubs) { _focus.Pointer(); _plot.Focus(); if (!IsTouch(e)) { SetActive(PointAt(e.GetPosition(_plot).X)); } } };
        _plot.MouseMove += (_, e) => { if (Scrubs && !IsTouch(e)) { SetActive(PointAt(e.GetPosition(_plot).X)); } };
        _plot.MouseLeave += (_, e) => { if (Scrubs && !IsTouch(e)) { SetActive(null); } };
        _plot.TouchDown += (_, e) => { if (Scrubs) { _plot.CaptureTouch(e.TouchDevice); SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; } };
        _plot.TouchMove += (_, e) => { if (Scrubs) { SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; } };
        _plot.TouchUp += (_, e) => { if (Scrubs) { _plot.ReleaseTouchCapture(e.TouchDevice); SetActive(null); e.Handled = true; } };
        _plot.KeyDown += (_, e) => { if (Scrubs) { KeyScrub(e); } };
        _plot.LostKeyboardFocus += (_, _) => SetActive(null);
        Loaded += (_, _) => { OnTone(false); if (Tween.Level(this) != ArcMotionLevel.Full) { StartDraw(); } };
        Unloaded += (_, _) => { _loop.Stop(); _cursor.Jump(_cursor.Target); _dot.Jump(_dot.Target); };
        ThemeWatch.Add(this, () => OnTone(false));
    }

    internal int? ActiveForTest => _active;
    internal double CursorForTest => _cursor.Position;
    internal double DrawnForTest => (double)GetValue(DrawnProperty);
    internal bool LandedForTest => _landed;
    internal double DotWidthForTest => _dot.Position;
    internal string HeadlineForTest => _headline?.TextForTest ?? "";
    internal string AsideForTest => _aside?.TextForTest ?? "";
    internal Point[] ShapeForTest => _shape;
    internal Point[] TargetForTest => _target;
    internal FrameworkElement PlotForTest => _plot;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void FinishForTest()
    {
        foreach (var property in new[] { DrawnProperty, MorphProperty }) { Tween.Set(this, property, 1); }
        Land();
        _cursor.Jump(_cursor.Target);
        _dot.Jump(_dot.Target);
        _shape = _target;
        _plot.InvalidateVisual();
    }
    internal void ScrubForTest(double x) => SetActive(PointAt(x));
    internal void LeaveForTest() => SetActive(null);
    internal void KeyForTest(Key key) { var args = new KeyEventArgs(Keyboard.PrimaryDevice, new Interop(), 0, key) { RoutedEvent = KeyDownEvent }; KeyScrub(args); }
    private sealed class Interop : PresentationSource
    {
        public override Visual? RootVisual { get => null; set { } }
        public override bool IsDisposed => false;
        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }

    private bool Scrubs => IsInteractive && Points > 1;
    private static bool IsTouch(MouseEventArgs e) => e.StylusDevice is not null;
    private IReadOnlyList<double> Safe => Data is { Count: > 1 } data ? data : Data is { Count: 1 } one ? [one[0], one[0]] : [0, 0];
    private int Points => Safe.Count;
    private int Last => Points - 1;
    private int? Index => _active is { } at ? Math.Min(at, Last) : null;
    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    private AutomationPeer MakePeer(FrameworkElement plot) => new ArcRangePeer(plot, AutomationControlType.Slider, () => $"{Label}, explore values", () => 1, () => Points, () => (Index ?? Last) + 1, Spoken, value => SetActive(Math.Clamp((int)value - 1, 0, Last)), () => 1, () => Page);
    private string Format(int at) => FormatValue?.Invoke(Safe[at], at) ?? Safe[at].ToString("#,0.###", CultureInfo.InvariantCulture);
    private string Spoken() { int at = Index ?? Last; return $"{Labels?.ElementAtOrDefault(at) ?? (Index is null ? "Latest" : $"Point {at + 1} of {Points}")}: {Format(at)}"; }
    private int Page => Math.Max(1, (int)Math.Round(Points / 6d, MidpointRounding.AwayFromZero));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }
        var caption = new Grid();
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _labelText = new RollText(14, 1.4, -.16, FontWeights.Normal, whole: true) { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        _headline = new RollText(18, 1.4, -.18, FontWeights.Medium, morph: true);
        _headlineBox = new Border { Child = _headline, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        _aside = new RollText(12, 1.4, -.16, FontWeights.Medium, morph: true) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 7, 0, 0) };
        Grid.SetColumn(_headlineBox, 1);
        Grid.SetColumn(_aside, 2);
        caption.Children.Add(_labelText);
        caption.Children.Add(_headlineBox);
        caption.Children.Add(_aside);
        var figure = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(caption, Dock.Top);
        figure.Children.Add(caption);
        _plot.Margin = new Thickness(0, 12, 0, 0);
        figure.Children.Add(_plot);
        _host.Child = figure;
        OnTone(false);
        OnData();
        Caption();
    }

    private void OnInteractive() { _plot.Focusable = IsInteractive; if (!IsInteractive) { SetActive(null); } }

    private void OnTone(bool animate)
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        var line = Tween.Resolve(this, ToneBrushes.Line(Tone));
        // color-mix(currentColor 9%, transparent), and currentColor is the tone's copy color.
        var wash = Tween.Resolve(this, ToneBrushes.Copy(Tone));
        wash.A = (byte)Math.Round(255 * .09);
        bool cross = animate && Moving;
        Tween.Color(_line, line, Tween.Standard, cross);
        Tween.Color(_copy, Tween.Resolve(this, ToneBrushes.Copy(Tone)), Tween.Standard, cross);
        Tween.Color(_wash, wash, 0, false);
        _plot.InvalidateVisual();
    }

    // New data morphs the line from the shape on screen, sampled on shared x positions so a series that gains or loses points still flows.
    private void OnData()
    {
        string key = string.Join(",", Safe.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        if (key == _dataKey) { Caption(); return; }
        _dataKey = key;
        _target = SparklineMath.Curve(Safe);
        if (_shape.Length == 0 || !Moving) { _shape = _target; Tween.Set(this, MorphProperty, 1); }
        else
        {
            double[] xs = [.. SparklineMath.Grid.Concat(_target.Select(point => point.X)).Distinct().Order()];
            _morphXs = xs;
            _morphFrom = [.. xs.Select(x => new Point(x, SparklineMath.HeightAt(_shape, x)))];
            _morphTo = [.. xs.Select(x => new Point(x, SparklineMath.HeightAt(_target, x)))];
            SetValue(MorphProperty, 0d);
            Tween.Spring(this, MorphProperty, 1, Tween.Smooth, 0, () => { if (_morphXs == xs) { _shape = _target; _plot.InvalidateVisual(); } });
        }
        Aim();
        Caption();
        _plot.InvalidateVisual();
    }

    private void OnMorph()
    {
        double t = (double)GetValue(MorphProperty);
        if (_morphXs.Length > 0 && t < 1)
        {
            var frame = new Point[_morphXs.Length];
            for (int index = 0; index < frame.Length; index++) { frame[index] = new Point(_morphXs[index], _morphFrom[index].Y + (_morphTo[index].Y - _morphFrom[index].Y) * t); }
            _shape = frame;
        }
        _plot.InvalidateVisual();
    }

    // The line draws left to right once it is seen, the fill follows the pen, then the end dot lands.
    private void StartDraw()
    {
        if (!Moving) { Tween.Set(this, DrawnProperty, 1); Land(); return; }
        Tween.To(this, DrawnProperty, 1, Tween.Considered * 1.75, ArcEasing.InOut, 0, Land);
    }

    private void Land()
    {
        if (_landed) { return; }
        _landed = true;
        SetValue(DrawnProperty, 1d);
        Marks();
    }

    private void SetActive(int? at)
    {
        if (_active == at) { return; }
        int? before = Index;
        _active = at;
        int? now = Index;
        _moved = now is null || before is null ? 1 : Math.Sign(now.Value - before.Value) is var sign && sign != 0 ? sign : 1;
        Aim();
        Marks();
        Caption();
        if (UIElementAutomationPeer.FromElement(_plot) is { } peer) { peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)((before ?? Last) + 1), (double)((now ?? Last) + 1)); }
    }

    // The cursor springs to the scrubbed point and rides the line between points; leaving sends it home to the latest value.
    private void Aim()
    {
        double to = Index is { } at ? (double)at / Last : 1;
        if (!Moving) { _cursor.Jump(to); _plot.InvalidateVisual(); return; }
        _cursor.To(to, SpringTune.Snappy);
        _loop.Start();
    }

    private void Marks()
    {
        bool scrubbing = Index is not null;
        double quick = Moving ? Tween.Fast : 0;
        Tween.To(this, TrailOpacityProperty, scrubbing && _landed ? 1 : 0, quick, ArcEasing.Standard);
        Tween.To(this, CursorOpacityProperty, scrubbing ? 1 : 0, quick, ArcEasing.Standard);
        Tween.To(this, DotOpacityProperty, _landed ? 1 : 0, quick, ArcEasing.Standard);
        double dot = _landed ? scrubbing ? 10 : 7 : 0;
        if (Moving) { _dot.To(dot, SpringTune.Snappy); _loop.Start(); } else { _dot.Jump(dot); _plot.InvalidateVisual(); }
    }

    private bool Frame(double seconds)
    {
        _cursor.Step(seconds);
        _dot.Step(seconds);
        _plot.InvalidateVisual();
        return _cursor.Active || _dot.Active;
    }

    private void Caption()
    {
        if (_labelText is null || _headlineBox is null) { return; }
        _labelText.Set(Label ?? "", _secondary, "");
        int? index = Index;
        bool scrubbing = index is not null;
        string point = Format(index ?? Last);
        string? when = Labels?.ElementAtOrDefault(index ?? Last);
        _headlineBox.Visibility = Value is null ? Visibility.Collapsed : Visibility.Visible;
        if (Value is not null) { _headline!.Set(scrubbing ? point : Value, _ink, ""); }
        string aside = index is null ? Change ?? "" : Value is null ? string.Join(" · ", new[] { point, when }.Where(part => !string.IsNullOrEmpty(part))) : when ?? $"{index + 1} of {Last + 1}";
        // The scrubbed date is context, not a change, so it stays neutral whatever the tone.
        _aside!.Set(aside, scrubbing ? _secondary : _copy, scrubbing ? "point:" : "", scrubbing ? _moved : null);
    }

    private SolidColorBrush Brush(string role) => TryFindResource("ArcWpf.Brush." + role) as SolidColorBrush ?? Brushes.Black;

    private int PointAt(double x) => _plot.ActualWidth > 0 ? Math.Clamp((int)Math.Round(x / _plot.ActualWidth * Last, MidpointRounding.AwayFromZero), 0, Last) : Last;

    private void KeyScrub(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Index is not null) { e.Handled = true; SetActive(null); return; }
        int from = Index ?? Last;
        int? next = e.Key switch { Key.Left or Key.Down => from - 1, Key.Right or Key.Up => from + 1, Key.PageDown => from - Page, Key.PageUp => from + Page, Key.Home => 0, Key.End => Last, _ => null };
        if (next is null) { return; }
        e.Handled = true;
        _focus.Key();
        SetActive(Math.Clamp(next.Value, 0, Last));
    }

    private void Draw(DrawingContext dc, double w, double h)
    {
        if (w <= 0 || _shape.Length == 0) { return; }
        double progress = (double)GetValue(DrawnProperty), at = _cursor.Position;
        var points = new Point[_shape.Length];
        for (int index = 0; index < points.Length; index++) { points[index] = new Point(_shape[index].X * w, SparklineMath.ToY(_shape[index].Y, h)); }
        double cx = at * w, cy = SparklineMath.ToY(SparklineMath.HeightAt(_shape, at), h);
        var (count, tip) = SparklineMath.PenAt(points, progress);
        if (ShowArea && progress > 0)
        {
            // The fill follows the pen's tip.
            double reveal = progress >= 1 ? w + 32 : tip.X + 16;
            dc.PushClip(new RectangleGeometry(new Rect(-16, -16, reveal, h + 32)));
            dc.DrawGeometry(_wash, null, Poly(points, points.Length, default, true, w, h));
            dc.Pop();
        }
        dc.DrawLine(new Pen(Brush("Border"), 1), new Point(0, h - .5), new Point(w, h - .5));
        var round = (Brush brush, double thickness) => new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        // While scrubbing, the stretch after the cursor dims, as if the later points have not happened yet.
        double trail = (double)GetValue(TrailOpacityProperty);
        if (trail > 0)
        {
            dc.PushClip(new RectangleGeometry(new Rect(cx + SparklineMath.Cap, -16, w + 32, h + 32)));
            dc.PushOpacity(trail);
            dc.DrawGeometry(null, round(Brush("BorderStrong"), 2.5), Poly(points, points.Length, default, false, w, h));
            dc.Pop();
            dc.Pop();
        }
        if (progress > .001)
        {
            dc.PushClip(new RectangleGeometry(new Rect(-16, -16, cx + SparklineMath.Cap + 16, h + 32)));
            dc.DrawGeometry(null, round(_line, 2.5), Poly(points, count, tip, false, w, h));
            dc.Pop();
        }
        double cursor = (double)GetValue(CursorOpacityProperty);
        if (cursor > 0) { dc.PushOpacity(cursor); dc.DrawLine(new Pen(Brush("BorderStrong"), 1), new Point(cx, 0), new Point(cx, h)); dc.Pop(); }
        // It rests on the latest point and becomes the scrub handle.
        double dot = (double)GetValue(DotOpacityProperty), size = _dot.Position;
        if (dot > 0 && size > 0) { dc.PushOpacity(dot); dc.DrawEllipse(_line, null, new Point(cx, cy), size / 2, size / 2); dc.Pop(); }
        if (_focus.Shown) { dc.DrawRoundedRectangle(null, new Pen(FocusVisible.RingBrush(this), 2), new Rect(-9, -9, w + 18, h + 18), 18, 18); }
    }

    private static StreamGeometry Poly(Point[] points, int count, Point tip, bool area, double w, double h)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], area, area);
            for (int index = 1; index < count; index++) { context.LineTo(points[index], true, true); }
            if (area) { context.LineTo(new Point(w, h), false, false); context.LineTo(new Point(0, h), false, false); }
            else if (tip != default) { context.LineTo(tip, true, true); }
        }
        geometry.Freeze();
        return geometry;
    }
}
