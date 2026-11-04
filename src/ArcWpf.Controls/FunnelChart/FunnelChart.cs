using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>One step of the funnel. Use Value without segments, Values keyed by segment with them.</summary>
public sealed record FunnelStage(string Key, string Label, double? Value = null, IReadOnlyDictionary<string, double>? Values = null);

/// <summary>A part of every step, such as a device.</summary>
public sealed record FunnelSegment(string Key, string Label);

public enum FunnelOrientation { Auto, Horizontal, Vertical }

/// <summary>Reports the step in focus, or null when the reading clears.</summary>
public sealed class FunnelActiveEventArgs(int? index) : EventArgs
{
    public int? Index { get; } = index;
}

// The geometry the band is drawn from: flat across each step, eased between them, stacked around a center line.
internal static class FunnelMath
{
    public const double EaseHalf = .24, GapPx = 2, Inset = 8;

    // The eased level at position u, where step i spans [i, i + 1).
    public static double LevelAt(IReadOnlyList<double> levels, double u)
    {
        int count = levels.Count;
        if (count == 0) { return 0; }
        for (int boundary = 1; boundary < count; boundary++)
        {
            if (u < boundary - EaseHalf) { return levels[boundary - 1]; }
            if (u <= boundary + EaseHalf) { return levels[boundary - 1] + (levels[boundary] - levels[boundary - 1]) * ChartMath.Smoother((u - boundary + EaseHalf) / (2 * EaseHalf)); }
        }
        return levels[count - 1];
    }

    // 100K, 61.2K, 8.4K.
    public static string Short(double value) => ChartMath.Compact(value);

    // 61% from ten up, 8.4% below that.
    public static string Rate(double share)
    {
        double percent = share * 100;
        return (percent >= 10 ? Math.Round(percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) : Math.Round(percent, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture)) + "%";
    }
}

// Independent WPF implementation from uiarc.dev's public documentation and displayed geometry (see THIRD-PARTY-NOTICES.md): an ordered conversion funnel drawn as one tapered band with drop-off at every step.
public class FunnelChart : Control
{
    public static readonly DependencyProperty StagesProperty = DependencyProperty.Register(nameof(Stages), typeof(IReadOnlyList<FunnelStage>), typeof(FunnelChart), new FrameworkPropertyMetadata(null, (d, _) => ((FunnelChart)d).Rebuild()));
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(nameof(Segments), typeof(IReadOnlyList<FunnelSegment>), typeof(FunnelChart), new FrameworkPropertyMetadata(null, (d, _) => ((FunnelChart)d).Rebuild()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(FunnelChart), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(FunnelChart), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(nameof(View), typeof(string), typeof(FunnelChart), new FrameworkPropertyMetadata("total", (d, _) => ((FunnelChart)d).Retarget()));
    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(nameof(Flow), typeof(bool), typeof(FunnelChart), new FrameworkPropertyMetadata(true, (d, _) => ((FunnelChart)d).OnFlow()));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(FunnelOrientation), typeof(FunnelChart), new FrameworkPropertyMetadata(FunnelOrientation.Auto, (d, _) => ((FunnelChart)d).Rebuild()));
    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(nameof(PlotHeight), typeof(double), typeof(FunnelChart), new FrameworkPropertyMetadata(200d, (d, _) => ((FunnelChart)d).Rebuild()));
    public static readonly DependencyProperty FormatValueProperty = DependencyProperty.Register(nameof(FormatValue), typeof(Func<double, string>), typeof(FunnelChart), new FrameworkPropertyMetadata(null, (d, _) => ((FunnelChart)d).Retarget()));
    public static readonly DependencyProperty ActiveIndexProperty = DependencyProperty.Register(nameof(ActiveIndex), typeof(int?), typeof(FunnelChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((FunnelChart)d).OnActive((int?)e.OldValue)));
    public static readonly DependencyProperty EmptyLabelProperty = DependencyProperty.Register(nameof(EmptyLabel), typeof(string), typeof(FunnelChart), new FrameworkPropertyMetadata("No data", (d, _) => ((FunnelChart)d).Retarget()));

    /// <summary>Ordered steps. Use Value without segments, Values keyed by segment with them.</summary>
    public IReadOnlyList<FunnelStage>? Stages { get => (IReadOnlyList<FunnelStage>?)GetValue(StagesProperty); set => SetValue(StagesProperty, value); }
    /// <summary>Parts of every step, such as devices. Leave it out for one total per step.</summary>
    public IReadOnlyList<FunnelSegment>? Segments { get => (IReadOnlyList<FunnelSegment>?)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    /// <summary>What is counted. Names the chart for assistive technology.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Noun after counts in spoken readings, such as "sessions".</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>"total" draws one band, "split" stacks every segment with a gap, and a segment key isolates it and rescales the band to fill the height.</summary>
    public string View { get => (string)GetValue(ViewProperty); set => SetValue(ViewProperty, value); }
    /// <summary>Dots that stream through the band. Each decides up front where it leaves, so the falling share matches the real drop-off.</summary>
    public bool Flow { get => (bool)GetValue(FlowProperty); set => SetValue(FlowProperty, value); }
    /// <summary>Auto turns the funnel vertical below 420 pixels of width, with steps listed down the side.</summary>
    public FunnelOrientation Orientation { get => (FunnelOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Band area thickness in pixels when horizontal.</summary>
    public double PlotHeight { get => (double)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    /// <summary>Formats counts. Defaults to compact numbers in headings and full numbers in the tooltip.</summary>
    public Func<double, string>? FormatValue { get => (Func<double, string>?)GetValue(FormatValueProperty); set => SetValue(FormatValueProperty, value); }
    /// <summary>The step in focus.</summary>
    public int? ActiveIndex { get => (int?)GetValue(ActiveIndexProperty); set => SetValue(ActiveIndexProperty, value); }
    /// <summary>Message when the first step is zero.</summary>
    public string EmptyLabel { get => (string)GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }

    /// <summary>Raised as the step in focus changes.</summary>
    public event EventHandler<FunnelActiveEventArgs>? ActiveChanged;

    private const int DotCount = 40;
    private const double VerticalStep = 84, VerticalPlot = 120, SideGap = 20, TipGap = 6, DropRow = 26, AutoWidth = 420;
    private static readonly SpringTune Settle = SpringTune.Smooth, Grow = SpringTune.Morph, Glide = SpringTune.Snappy;

    private sealed class Layer(string key, string label, int stages, int index)
    {
        public string Key { get; } = key;
        public string Label { get; } = label;
        public int Index { get; } = index;
        public GestureSpring[] Near { get; } = [.. Enumerable.Range(0, stages).Select(_ => new GestureSpring(0, .02, .2))];
        public GestureSpring[] Far { get; } = [.. Enumerable.Range(0, stages).Select(_ => new GestureSpring(0, .02, .2))];
        public TimedColor Fill { get; } = new(Colors.Transparent);
        public TimedColor Glow { get; } = new(Colors.Transparent);
        public double[] Values = new double[stages];
    }

    private sealed class Dot
    {
        public double U, Lane, Speed, Fall, Age;
        public int Leave, Layer;
        public bool Falling, Live;
    }

    private sealed class StageView
    {
        public required FrameworkElement Root;
        public required TextBlock Label;
        public required RollText Value, Rate;
        public required Timed Dim;
    }

    private sealed class DropView
    {
        public required FrameworkElement Root;
        public required RollText Text;
    }

    private readonly ChartSurface _plot, _dots;
    private readonly Border _column = new() { CornerRadius = new CornerRadius(14), Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _area = new();
    private readonly Canvas _stageLayer = new() { IsHitTestVisible = false }, _dropLayer = new() { IsHitTestVisible = false }, _tipLayer = new() { IsHitTestVisible = false };
    private readonly Border _tip = new() { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(12, 10, 12, 10), MinWidth = 140, Opacity = 0, IsHitTestVisible = false };
    private readonly StackPanel _tipLines = new();
    private readonly TextBlock _empty = new() { FontSize = 14, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly FrameLoop _loop, _flow;
    private readonly FocusVisible _focus;
    private readonly InViewOnce _inView;
    private readonly List<Layer> _layers = [];
    private readonly List<StageView> _stageViews = [];
    private readonly List<DropView> _dropViews = [];
    private readonly Dot[] _dotPool = [.. Enumerable.Range(0, DotCount).Select(_ => new Dot())];
    private readonly Random _random = new(7);
    private readonly GestureSpring _growth = new(0, .001, .01), _columnAt = new(0), _tipX = new(0), _tipY = new(0);
    private readonly Timed _columnFade = new(0), _glowFade = new(0), _tipFade = new(0);
    private readonly SolidColorBrush _ink = new(), _secondary = new(), _muted = new(), _raised = new(), _edge = new(), _columnFill = new();
    private Decorator? _host;
    private FunnelStage[] _stages = [];
    private FunnelSegment[] _segments = [];
    private double[] _totals = [];
    private double _width, _spawnBudget, _screenCheck;
    private bool _vertical, _seen, _built, _flowVisible = true;
    private int _shownStep = -1;
    private Color _inkColor;

    static FunnelChart() => DefaultStyleKeyProperty.OverrideMetadata(typeof(FunnelChart), new FrameworkPropertyMetadata(typeof(FunnelChart)));

    public FunnelChart()
    {
        _plot = new ChartSurface(PaintBand);
        _dots = new ChartSurface(PaintDots, surface => new FunnelPeer(surface, this)) { Focusable = true, FocusVisualStyle = null };
        _tip.Child = _tipLines;
        _tip.RenderTransform = new TranslateTransform();
        _tipLayer.Children.Add(_tip);
        _loop = new FrameLoop(Frame);
        _flow = new FrameLoop(FlowFrame);
        _focus = new FocusVisible(_dots, () => { if (_focus is { Shown: true } && StepCount > 0 && ActiveIndex is null) { SetActive(0); } });
        _inView = new InViewOnce(this, Seen);
        _dots.MouseDown += (_, _) => { _focus.Pointer(); _dots.Focus(); };
        _dots.MouseMove += (_, e) => { if (e.StylusDevice is null) { SetActive(PointAt(e.GetPosition(_dots))); } };
        _dots.MouseLeave += (_, e) => { if (e.StylusDevice is null) { SetActive(null); } };
        _dots.TouchDown += (_, e) => { _dots.CaptureTouch(e.TouchDevice); SetActive(PointAt(e.GetTouchPoint(_dots).Position)); e.Handled = true; };
        _dots.TouchMove += (_, e) => { SetActive(PointAt(e.GetTouchPoint(_dots).Position)); e.Handled = true; };
        _dots.TouchUp += (_, e) => { _dots.ReleaseTouchCapture(e.TouchDevice); SetActive(null); e.Handled = true; };
        _dots.KeyDown += (_, e) => { if (Press(e.Key)) { e.Handled = true; _focus.Key(); } };
        _dots.LostKeyboardFocus += (_, _) => SetActive(null);
        SizeChanged += (_, e) => { if (e.WidthChanged) { _width = e.NewSize.Width; Rebuild(); } };
        Loaded += (_, _) => { Colorize(); if (!Moving) { Seen(); } OnFlow(); };
        Unloaded += (_, _) => { _loop.Stop(); _flow.Stop(); };
        ThemeWatch.Add(this, Colorize);
    }

    internal int LayerCountForTest => _layers.Count;
    internal double TopOfForTest(int layer, int stage) => _layers[layer].Near[stage].Target;
    internal double BottomOfForTest(int layer, int stage) => _layers[layer].Far[stage].Target;
    internal double NowTopForTest(int layer, int stage) => _layers[layer].Near[stage].Position;
    internal Color FillTargetForTest(int layer) => _layers[layer].Fill.Target;
    internal bool VerticalForTest => _vertical;
    internal double GrowthForTest => _growth.Position;
    internal string ValueForTest(int stage) => _stageViews[stage].Value.TextForTest;
    internal string RateForTest(int stage) => _stageViews[stage].Rate.TextForTest;
    internal string DropForTest(int boundary) => _dropViews[boundary].Text.TextForTest;
    internal string[] TipForTest => [.. _tipLines.Children.OfType<FrameworkElement>().SelectMany(TextsIn)];
    internal double TipXForTest => _tipX.Target;
    internal double TipWidthForTest => _tip.DesiredSize.Width;
    internal double TipYForTest => _tipY.Target;
    internal double TipHeightForTest => _tip.DesiredSize.Height;
    internal double ColumnForTest => _columnAt.Target;
    internal double DimForTest(int stage) => _stageViews[stage].Dim.Target;
    internal int LiveDotsForTest => _dotPool.Count(dot => dot.Live);
    internal bool FlowRunningForTest => _flow.IsRunning;
    internal FrameworkElement PlotForTest => _dots;
    internal bool EmptyShownForTest => _empty.Visibility == Visibility.Visible;
    internal void SeenForTest() => Seen();
    internal void HoverForTest(Point point) => SetActive(PointAt(point));
    internal void KeyForTest(Key key) => Press(key);
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { _loop.Advance(Math.Min(left, .05)); } }
    internal void AdvanceFlowForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { Spawn(Math.Min(left, .05)); } }

    private static IEnumerable<string> TextsIn(FrameworkElement element) => element switch
    {
        TrackedText text => [text.Text],
        Panel panel => panel.Children.OfType<FrameworkElement>().SelectMany(TextsIn),
        _ => [],
    };

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private int StepCount => _stages.Length;
    private bool Vertical => Orientation == FunnelOrientation.Vertical || (Orientation == FunnelOrientation.Auto && _width > 0 && _width < AutoWidth);
    private double PlotW => _vertical ? Math.Max(VerticalPlot, (_width - SideGap) / 2) : _width;
    private double PlotH => _vertical ? VerticalStep * Math.Max(1, StepCount) : PlotHeight;
    private double Across => _vertical ? PlotW : PlotH;
    private double Along => _vertical ? PlotH : PlotW;
    private double StepSize => StepCount > 0 ? Along / StepCount : 0;
    private double HeaderHeight => _vertical ? 0 : 84.2;
    private string Short(double value) => FormatValue?.Invoke(value) ?? FunnelMath.Short(value);
    private string Full(double value) => FormatValue?.Invoke(value) ?? ChartMath.Grouped(value);
    private string Suffix => string.IsNullOrEmpty(Unit) ? "" : $" {Unit}";

    private double ValueOf(FunnelStage stage, string? key)
    {
        if (key is null) { return stage.Value ?? stage.Values?.Values.Sum() ?? 0; }
        return stage.Values is { } values && values.TryGetValue(key, out double value) ? value : 0;
    }

    private bool IsSegmentView => _segments.Any(segment => segment.Key == View);
    private double ShownOf(int stage) => IsSegmentView ? ValueOf(_stages[stage], View) : _totals.ElementAtOrDefault(stage);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }
        _column.SetValue(Panel.ZIndexProperty, 0);
        _area.Children.Add(_column);
        _area.Children.Add(_stageLayer);
        _area.Children.Add(_plot);
        _area.Children.Add(_dots);
        _area.Children.Add(_dropLayer);
        _area.Children.Add(_empty);
        _area.Children.Add(_tipLayer);
        _tip.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _host.Child = _area;
        _built = true;
        Colorize();
        Rebuild();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (double.IsFinite(constraint.Width) && constraint.Width != _width) { _width = constraint.Width; Rebuild(); }
        return base.MeasureOverride(constraint);
    }

    // Everything sized by the data or the width.
    private void Rebuild()
    {
        if (!_built) { return; }
        _stages = [.. Stages ?? []];
        _segments = [.. Segments ?? []];
        _vertical = Vertical;
        _totals = [.. _stages.Select(stage => _segments.Length > 0 ? _segments.Sum(segment => ValueOf(stage, segment.Key)) : ValueOf(stage, null))];
        string[] keys = _segments.Length > 0 ? [.. _segments.Select(segment => segment.Key)] : ["__all"];
        bool sameShape = _layers.Count == keys.Length && _layers.All(layer => keys.Contains(layer.Key) && layer.Near.Length == StepCount);
        if (!sameShape)
        {
            _layers.Clear();
            for (int index = 0; index < keys.Length; index++) { _layers.Add(new Layer(keys[index], _segments.Length > 0 ? _segments[index].Label : "Total", StepCount, index)); }
        }
        foreach (var layer in _layers)
        {
            for (int stage = 0; stage < StepCount; stage++) { layer.Values[stage] = ValueOf(_stages[stage], _segments.Length > 0 ? layer.Key : null); }
        }
        BuildViews(!sameShape);
        PlaceAll();
        Retarget(!sameShape);
    }

    private void BuildViews(bool rebuild)
    {
        if (!rebuild && _stageViews.Count == StepCount) { return; }
        _stageLayer.Children.Clear();
        _dropLayer.Children.Clear();
        _stageViews.Clear();
        _dropViews.Clear();
        for (int stage = 0; stage < StepCount; stage++)
        {
            var label = new TextBlock { FontSize = 12, LineHeight = 15.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap, Foreground = _secondary, Text = _stages[stage].Label };
            var value = new RollText(18, 1.3, -.16, FontWeights.Medium, morph: true) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 1) };
            var rate = new RollText(12, 1.3, -.16, FontWeights.Normal, whole: true) { HorizontalAlignment = HorizontalAlignment.Left };
            var panel = new StackPanel();
            panel.Children.Add(label);
            panel.Children.Add(value);
            panel.Children.Add(rate);
            _stageLayer.Children.Add(panel);
            _stageViews.Add(new StageView { Root = panel, Label = label, Value = value, Rate = rate, Dim = new Timed(1) });
        }
        for (int boundary = 0; boundary + 1 < StepCount; boundary++)
        {
            var arrow = new Path { Data = Geometry.Parse("M4 1v6M1.5 4.5 4 7l2.5-2.5"), Stroke = _muted, StrokeThickness = 1.3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 3, 0) };
            var text = new RollText(12, 1, -.16, FontWeights.Normal, morph: true) { VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            row.Children.Add(arrow);
            row.Children.Add(text);
            _dropLayer.Children.Add(row);
            _dropViews.Add(new DropView { Root = row, Text = text });
        }
    }

    // Lays out the header, plot, drop row and side list for the orientation in force.
    private void PlaceAll()
    {
        double plotW = PlotW, plotH = PlotH, top = HeaderHeight;
        _plot.Width = _dots.Width = plotW;
        _plot.Height = _dots.Height = plotH;
        Canvas.SetLeft(_plot, 0);
        Canvas.SetTop(_plot, top);
        Canvas.SetLeft(_dots, 0);
        Canvas.SetTop(_dots, top);
        _area.Width = _width;
        _area.Height = top + plotH + (_vertical ? 0 : DropRow);
        double step = StepSize;
        for (int stage = 0; stage < _stageViews.Count; stage++)
        {
            var view = _stageViews[stage];
            view.Rate.Visibility = _vertical ? Visibility.Collapsed : Visibility.Visible;
            double width = _vertical ? Math.Max(0, _width - plotW - SideGap) : Math.Max(0, step - 8);
            view.Root.Width = width;
            view.Root.Measure(new Size(width, double.PositiveInfinity));
            double height = view.Root.DesiredSize.Height;
            if (_vertical)
            {
                Canvas.SetLeft(view.Root, plotW + SideGap);
                Canvas.SetTop(view.Root, stage * step + (step - height) / 2);
            }
            else
            {
                // Labels wrap upward, so the numbers line up however long a step's name is.
                Canvas.SetLeft(view.Root, stage * step);
                Canvas.SetTop(view.Root, 72.2 - height);
            }
        }
        for (int boundary = 0; boundary < _dropViews.Count; boundary++)
        {
            var view = _dropViews[boundary];
            view.Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (_vertical)
            {
                Canvas.SetLeft(view.Root, plotW + SideGap);
                Canvas.SetTop(view.Root, (boundary + 1) * step - 6);
            }
            else
            {
                Canvas.SetLeft(view.Root, (boundary + 1) * step - 38.5 / 2);
                Canvas.SetTop(view.Root, top + plotH + 8);
            }
        }
        _column.Width = _vertical ? _width : step;
        _column.Height = _vertical ? step : _area.Height;
        _empty.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_empty, (plotW - _empty.DesiredSize.Width) / 2);
        Canvas.SetTop(_empty, top + (plotH - _empty.DesiredSize.Height) / 2);
        _tipLayer.Width = _width;
        _tipLayer.Height = _area.Height;
    }

    private void Retarget() => Retarget(false);

    // Works out where every layer should sit, and lets the band morph there from the shape on screen.
    private void Retarget(bool jump)
    {
        if (!_built) { return; }
        double across = Across, center = across / 2, full = Math.Max(0, across - 2 * FunnelMath.Inset);
        string view = View;
        bool split = view == "split" && _layers.Count > 1, isolate = IsSegmentView;
        double first = isolate ? ValueOf(_stages.FirstOrDefault() ?? new FunnelStage("", ""), view) : _totals.FirstOrDefault();
        double scale = first > 0 ? full / first : 0;
        for (int stage = 0; stage < StepCount; stage++)
        {
            double total = 0;
            foreach (var layer in _layers) { total += !isolate || layer.Key == view ? layer.Values[stage] : 0; }
            double cursor = center - total * scale / 2;
            for (int index = 0; index < _layers.Count; index++)
            {
                var layer = _layers[index];
                double thick = (!isolate || layer.Key == view ? layer.Values[stage] : 0) * scale;
                double shift = split ? (index - (_layers.Count - 1) / 2d) * FunnelMath.GapPx : 0;
                Aim(layer.Near[stage], cursor + shift, jump);
                Aim(layer.Far[stage], cursor + thick + shift, jump);
                cursor += thick;
            }
        }
        Tint(jump);
        Readouts();
        Wake();
        _plot.InvalidateVisual();
        Refresh();
    }

    private void Aim(GestureSpring spring, double to, bool jump)
    {
        if (jump || !Moving) { spring.Jump(to); return; }
        if (spring.Target != to || spring.Active) { spring.To(to, Settle); }
    }

    private Color LayerBase(Layer layer)
    {
        int series = View == "total" || _layers.Count == 1 ? 0 : layer.Index;
        return Tween.Resolve(this, $"ArcWpf.Brush.Series{(series % 4) + 1}");
    }

    private void Tint(bool jump)
    {
        double share = View == "split" && _layers.Count > 1 ? .42 : .24;
        var surface = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        foreach (var layer in _layers)
        {
            var color = LayerBase(layer);
            var fill = OkColor.Mix(color, surface, share);
            if (jump || !Moving) { layer.Fill.Jump(fill); layer.Glow.Jump(color); }
            else { layer.Fill.Go(fill, Tween.Standard, ArcEasing.Standard); layer.Glow.Go(color, Tween.Standard, ArcEasing.Standard); }
        }
    }

    // Counts and rates roll to their new values.
    private void Readouts()
    {
        bool hasData = StepCount > 0 && ShownOf(0) > 0;
        _empty.Text = EmptyLabel;
        _empty.Visibility = hasData || StepCount == 0 ? Visibility.Collapsed : Visibility.Visible;
        for (int stage = 0; stage < _stageViews.Count; stage++)
        {
            double value = ShownOf(stage), start = ShownOf(0);
            var view = _stageViews[stage];
            view.Label.Text = _stages[stage].Label;
            view.Value.Set(Short(value), _ink, "");
            view.Rate.Set(stage == 0 ? "Start" : start > 0 ? $"{FunnelMath.Rate(value / start)} of start" : "", _muted, "", value >= 0 ? 1 : -1);
        }
        for (int boundary = 0; boundary < _dropViews.Count; boundary++)
        {
            double before = ShownOf(boundary), after = ShownOf(boundary + 1);
            _dropViews[boundary].Text.Set(before > 0 ? $"{Math.Round((1 - after / before) * 100, MidpointRounding.AwayFromZero):0}%" : "0%", _muted, "");
        }
    }

    // The band grows out of its centre line the first time it is seen.
    private void Seen()
    {
        if (_seen) { return; }
        _seen = true;
        if (Moving) { _growth.To(1, Grow); } else { _growth.Jump(1); }
        Wake();
        OnFlow();
        _plot.InvalidateVisual();
    }

    private void OnFlow()
    {
        bool run = Flow && _seen && Moving && StepCount > 1;
        if (run) { _flow.Start(); }
        else
        {
            _flow.Stop();
            foreach (var dot in _dotPool) { dot.Live = false; }
            _dots.InvalidateVisual();
        }
    }

    private int? PointAt(Point point)
    {
        if (StepCount == 0) { return null; }
        double along = _vertical ? point.Y : point.X;
        return Math.Clamp((int)Math.Floor(along / StepSize), 0, StepCount - 1);
    }

    private bool Press(Key key)
    {
        if (key == Key.Escape && ActiveIndex is not null) { SetActive(null); return true; }
        if (StepCount == 0) { return false; }
        int from = ActiveIndex ?? -1;
        int? next = key switch
        {
            Key.Right or Key.Down => from + 1,
            Key.Left or Key.Up => from < 0 ? 0 : from - 1,
            Key.Home => 0,
            Key.End => StepCount - 1,
            _ => null,
        };
        if (next is null) { return false; }
        SetActive(Math.Clamp(next.Value, 0, StepCount - 1));
        return true;
    }

    private void SetActive(int? at) { if (ActiveIndex != at) { SetCurrentValue(ActiveIndexProperty, at); } }

    private void OnActive(int? before)
    {
        Refresh();
        ActiveChanged?.Invoke(this, new FunnelActiveEventArgs(ActiveIndex));
        if (ActiveIndex is { } at && at < StepCount && before != at && _dots.IsKeyboardFocused) { Announcer.Say(this, Reading(at)); }
    }

    private string Kept(int stage)
    {
        if (stage == 0) { return "Entry step"; }
        double before = ShownOf(stage - 1);
        return before > 0 ? $"{FunnelMath.Rate(ShownOf(stage) / before)} kept from {_stages[stage - 1].Label.ToLowerInvariant()}" : "";
    }

    private string Reading(int stage)
    {
        double first = ShownOf(0);
        return $"{_stages[stage].Label}, {Full(ShownOf(stage))}{Suffix}. {Kept(stage)}. {(first > 0 ? FunnelMath.Rate(ShownOf(stage) / first) : "0%")} of first step.";
    }

    // A soft column and an accent clip glide to the step in focus; the tooltip follows on its own spring and stays inside the chart.
    private void Refresh()
    {
        int? active = ActiveIndex is { } at && at < StepCount ? at : null;
        double step = StepSize;
        bool moving = Moving;
        for (int stage = 0; stage < _stageViews.Count; stage++) { _stageViews[stage].Dim.Go(active is null || active == stage ? 1 : .5, moving ? Tween.Fast : 0, ArcEasing.Standard); }
        if (active is { } index)
        {
            double to = index * step;
            bool first = _shownStep < 0;
            if (first || !moving) { _columnAt.Jump(to); } else if (_columnAt.Target != to) { _columnAt.To(to, Glide); }
            TipContent(index);
            // The rows just changed, and measuring only re-runs when the bubble itself is marked dirty.
            _tip.InvalidateMeasure();
            _tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tw = _tip.DesiredSize.Width, th = _tip.DesiredSize.Height;
            double x, y;
            if (_vertical)
            {
                x = Math.Clamp((PlotW - tw) / 2, 0, Math.Max(0, _width - tw));
                y = (index + 1) * step - TipGap;
                if (y + th > _area.Height) { y = index * step + TipGap - th; }
                y = Math.Clamp(y, 0, Math.Max(0, _area.Height - th));
            }
            else
            {
                x = (index + 1) * step - TipGap;
                if (x + tw > _width) { x = index * step + TipGap - tw; }
                x = Math.Clamp(x, 0, Math.Max(0, _width - tw));
                y = Math.Clamp(HeaderHeight + (PlotH - th) / 2, 0, Math.Max(0, _area.Height - th));
            }
            if (first || !moving) { _tipX.Jump(x); _tipY.Jump(y); }
            else
            {
                if (_tipX.Target != x) { _tipX.To(x, Glide); }
                if (_tipY.Target != y) { _tipY.To(y, Glide); }
            }
            _shownStep = index;
        }
        else { _shownStep = -1; }
        bool on = active is not null;
        _columnFade.Go(on ? .035 : 0, moving ? Tween.Fast : 0, ArcEasing.Standard);
        _glowFade.Go(on ? 1 : 0, moving ? Tween.Fast : 0, ArcEasing.Standard);
        _tipFade.Go(on ? 1 : 0, moving ? Tween.Fast : 0, ArcEasing.Standard);
        Wake();
    }

    private void TipContent(int index)
    {
        _tipLines.Children.Clear();
        var title = CopyMotion.Text(_stages[index].Label, 12, 1.4, -.16, FontWeights.Normal, _secondary);
        title.Margin = new Thickness(0, 0, 0, 6);
        _tipLines.Children.Add(title);
        bool split = View == "split" && _layers.Count > 1;
        IEnumerable<(string Name, double Value, Color? Key)> rows = split
            ? _layers.Select(layer => (layer.Label, layer.Values[index], (Color?)layer.Glow.Target))
            : [(IsSegmentView ? _layers.First(layer => layer.Key == View).Label : "Total", ShownOf(index), null)];
        bool firstRow = true;
        foreach (var (name, value, key) in rows)
        {
            var row = new DockPanel { Margin = new Thickness(0, firstRow ? 0 : 4, 0, 0) };
            firstRow = false;
            if (key is { } color)
            {
                var swatch = new Rectangle { Width = 8, Height = 8, RadiusX = 2, RadiusY = 2, Fill = ChartMath.Solid(color), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(swatch, Dock.Left);
                row.Children.Add(swatch);
            }
            var number = CopyMotion.Text(Full(value), 14, 1.3, -.16, FontWeights.Medium, _ink);
            number.Margin = new Thickness(16, 0, 0, 0);
            DockPanel.SetDock(number, Dock.Right);
            row.Children.Add(number);
            row.Children.Add(CopyMotion.Text(name, 14, 1.3, -.16, FontWeights.Normal, _secondary));
            _tipLines.Children.Add(row);
        }
        string meta = index == 0 ? string.IsNullOrEmpty(Unit) ? "Entry step" : $"Entry step, {Unit}" : Kept(index);
        var note = CopyMotion.Text(meta, 12, 1.4, -.16, FontWeights.Normal, _muted);
        note.Margin = new Thickness(0, 6, 0, 0);
        _tipLines.Children.Add(note);
    }

    private void Colorize()
    {
        _inkColor = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _ink.Color = _inkColor;
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _raised.Color = Tween.Resolve(this, "ArcWpf.Brush.SurfaceRaised");
        _edge.Color = Tween.Resolve(this, "ArcWpf.Brush.Border");
        _columnFill.Color = _inkColor;
        _column.Background = _columnFill;
        _tip.Background = _raised;
        _tip.BorderBrush = _edge;
        _empty.Foreground = _muted;
        Tint(true);
        _plot.InvalidateVisual();
    }

    private void Wake() { if (Moving) { _loop.Start(); } else { Paint(); } }

    private void Paint()
    {
        foreach (var view in _stageViews) { view.Root.Opacity = view.Dim.Value; }
        _column.Opacity = _columnFade.Value;
        Canvas.SetLeft(_column, _vertical ? 0 : _columnAt.Position);
        Canvas.SetTop(_column, _vertical ? _columnAt.Position : 0);
        ((TranslateTransform)_tip.RenderTransform).X = _tipX.Position;
        ((TranslateTransform)_tip.RenderTransform).Y = _tipY.Position;
        _tip.Opacity = _tipFade.Value;
        _plot.InvalidateVisual();
    }

    private bool Frame(double seconds)
    {
        bool busy = false;
        foreach (var layer in _layers)
        {
            foreach (var spring in layer.Near) { spring.Step(seconds); busy |= spring.Active; }
            foreach (var spring in layer.Far) { spring.Step(seconds); busy |= spring.Active; }
            layer.Fill.Step(seconds);
            layer.Glow.Step(seconds);
            busy |= layer.Fill.Active || layer.Glow.Active;
        }
        foreach (var spring in new[] { _growth, _columnAt, _tipX, _tipY }) { spring.Step(seconds); busy |= spring.Active; }
        foreach (var timed in new[] { _columnFade, _glowFade, _tipFade }) { timed.Step(seconds); busy |= timed.Active; }
        foreach (var view in _stageViews) { view.Dim.Step(seconds); busy |= view.Dim.Active; }
        Paint();
        return busy;
    }

    // Dots run at the display rate and pause off screen.
    private bool FlowFrame(double seconds)
    {
        _screenCheck -= seconds;
        if (_screenCheck <= 0) { _screenCheck = .4; _flowVisible = ChartMath.OnScreen(_dots); }
        if (!_flowVisible) { return true; }
        Spawn(Math.Min(seconds, .05));
        _dots.InvalidateVisual();
        return true;
    }

    private void Spawn(double seconds)
    {
        const double crossing = 6.5;
        // Spawned so about DotCount are alive: a dot lives for the share of the band its step count keeps it in.
        double start = Math.Max(ShownOf(0), 1), stay = Enumerable.Range(0, StepCount).Sum(stage => ShownOf(stage) / start) / Math.Max(1, StepCount);
        double rate = DotCount / (crossing * Math.Max(stay, .1)), along = Math.Max(1, StepCount);
        _spawnBudget += seconds * rate;
        foreach (var dot in _dotPool)
        {
            if (!dot.Live)
            {
                if (_spawnBudget < 1) { continue; }
                _spawnBudget -= 1;
                Launch(dot);
                continue;
            }
            if (dot.Falling)
            {
                dot.Fall += seconds * (18 + dot.Age * 40);
                dot.Age += seconds;
                if (dot.Age > .9) { dot.Live = false; }
                continue;
            }
            dot.U += seconds * dot.Speed * along / crossing;
            if (dot.U >= dot.Leave + 1 - (dot.Leave + 1 >= StepCount ? 0 : FunnelMath.EaseHalf))
            {
                if (dot.Leave + 1 >= StepCount) { dot.Live = false; continue; }
                dot.Falling = true;
                dot.Age = 0;
            }
        }
        _spawnBudget = Math.Min(_spawnBudget, 2);
    }

    // Each dot decides up front where it leaves, so the falling share matches the real drop-off.
    private void Launch(Dot dot)
    {
        double start = Math.Max(ShownOf(0), 1), pick = _random.NextDouble() * start;
        int leave = StepCount - 1;
        for (int stage = 0; stage + 1 < StepCount; stage++)
        {
            if (pick >= ShownOf(stage + 1)) { leave = stage; break; }
        }
        double layerPick = _random.NextDouble() * start;
        int layer = 0;
        double running = 0;
        foreach (var entry in _layers)
        {
            running += ThicknessShare(entry);
            if (layerPick <= running * start) { layer = entry.Index; break; }
        }
        dot.U = 0;
        dot.Lane = _random.NextDouble() * .86 + .07;
        dot.Speed = .85 + _random.NextDouble() * .3;
        dot.Leave = leave;
        dot.Layer = layer;
        dot.Falling = false;
        dot.Fall = 0;
        dot.Age = 0;
        dot.Live = true;
    }

    private double ThicknessShare(Layer layer)
    {
        if (StepCount == 0) { return 0; }
        double thick = layer.Far[0].Target - layer.Near[0].Target, whole = _layers.Sum(entry => entry.Far[0].Target - entry.Near[0].Target);
        return whole > 0 ? thick / whole : 0;
    }

    private double[] Levels(GestureSpring[] springs, double center)
    {
        double growth = _growth.Position;
        var levels = new double[springs.Length];
        for (int index = 0; index < springs.Length; index++) { levels[index] = center + (springs[index].Position - center) * growth; }
        return levels;
    }

    private Geometry? BandOf(Layer layer)
    {
        if (StepCount == 0) { return null; }
        double along = Along, center = Across / 2, step = StepSize;
        if (along <= 0 || step <= 0) { return null; }
        double[] near = Levels(layer.Near, center), far = Levels(layer.Far, center);
        if (near.Zip(far).All(pair => pair.Second - pair.First < .01)) { return null; }
        int samples = (int)Math.Ceiling(along / 3);
        Point At(double t, double cross) => _vertical ? new Point(cross, t) : new Point(t, cross);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(0, near[0]), true, true);
            for (int sample = 1; sample <= samples; sample++) { double t = along * sample / samples; ctx.LineTo(At(t, FunnelMath.LevelAt(near, t / step)), false, false); }
            for (int sample = samples; sample >= 0; sample--) { double t = along * sample / samples; ctx.LineTo(At(t, FunnelMath.LevelAt(far, t / step)), false, false); }
        }
        geometry.Freeze();
        return geometry;
    }

    private void PaintBand(DrawingContext dc, Size size)
    {
        if (StepCount == 0 || _totals.FirstOrDefault() <= 0) { return; }
        var shapes = _layers.Select(layer => (layer, Shape: BandOf(layer))).Where(item => item.Shape is not null).ToList();
        foreach (var (layer, shape) in shapes) { dc.DrawGeometry(ChartMath.Solid(layer.Fill.Value), null, shape); }
        if (_glowFade.Value <= 0) { return; }
        double at = _columnAt.Position, step = StepSize;
        dc.PushClip(new RectangleGeometry(_vertical ? new Rect(0, at, size.Width, step) : new Rect(at, 0, step, size.Height)));
        dc.PushOpacity(_glowFade.Value);
        foreach (var (layer, shape) in shapes) { dc.DrawGeometry(ChartMath.Solid(layer.Glow.Value), null, shape); }
        dc.Pop();
        dc.Pop();
    }

    private void PaintDots(DrawingContext dc, Size size)
    {
        if (_layers.Count == 0 || StepCount == 0) { return; }
        double center = Across / 2, step = StepSize;
        foreach (var dot in _dotPool)
        {
            if (!dot.Live || step <= 0) { continue; }
            var layer = _layers[Math.Min(dot.Layer, _layers.Count - 1)];
            double[] near = Levels(layer.Near, center), far = Levels(layer.Far, center);
            double a = FunnelMath.LevelAt(near, dot.U), b = FunnelMath.LevelAt(far, dot.U);
            double cross = a + (b - a) * dot.Lane, t = dot.U * step;
            double alpha = .72;
            if (dot.Falling)
            {
                t += dot.Age * 12;
                cross += (cross >= center ? 1 : -1) * dot.Fall;
                alpha *= Math.Clamp(1 - dot.Age / .9, 0, 1);
            }
            if (dot.U < .03) { alpha *= dot.U / .03; }
            var point = _vertical ? new Point(cross, t) : new Point(t, cross);
            dc.DrawEllipse(ChartMath.Solid(ChartMath.Faded(_inkColor, alpha)), null, point, 1.9, 1.9);
        }
    }

    private sealed class FunnelPeer(FrameworkElement owner, FunnelChart chart) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetLocalizedControlTypeCore() => "funnel chart";
        protected override string GetNameCore() { string own = base.GetNameCore(); return string.IsNullOrEmpty(own) ? chart.Label : own; }
        protected override string GetItemStatusCore() => chart.ActiveIndex is { } at && at < chart.StepCount ? chart.Reading(at) : string.Join(" ", Enumerable.Range(0, chart.StepCount).Select(chart.Reading));
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
