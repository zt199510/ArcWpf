using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>One line. A series keeps its line across data changes, so switching ranges morphs the path instead of redrawing it.</summary>
public sealed record LineChartSeries(string Key, string Label, Color? Color = null, bool Dashed = false, bool? Area = null);

/// <summary>One point on the x axis, with one value per series key. A missing value counts as zero.</summary>
public sealed record LineChartDatum(string Key, string Label, IReadOnlyDictionary<string, double> Values, string? AxisLabel = null);

public enum LineChartCurve { Smooth, Linear }

/// <summary>Reports the point under the crosshair, or null when it leaves.</summary>
public sealed class LineChartActiveEventArgs(int? index, LineChartDatum? datum) : EventArgs
{
    public int? Index { get; } = index;
    public LineChartDatum? Datum { get; } = datum;
}

// line-chart.tsx (frozen 2026-11-04): one or more measures over time; ranges morph, series toggle, and a crosshair reads each point.
public class LineChart : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(IReadOnlyList<LineChartDatum>), typeof(LineChart), new FrameworkPropertyMetadata(null, (d, _) => ((LineChart)d).OnData()));
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(IReadOnlyList<LineChartSeries>), typeof(LineChart), new FrameworkPropertyMetadata(null, (d, _) => ((LineChart)d).OnSeries()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(LineChart), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(LineChart), new FrameworkPropertyMetadata("", (d, _) => ((LineChart)d).Tip()));
    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(nameof(PlotHeight), typeof(double), typeof(LineChart), new FrameworkPropertyMetadata(220d, (d, _) => ((LineChart)d).OnPlotHeight()));
    public static readonly DependencyProperty FormatValueProperty = DependencyProperty.Register(nameof(FormatValue), typeof(Func<double, LineChartSeries, string>), typeof(LineChart), new FrameworkPropertyMetadata(null, (d, _) => ((LineChart)d).Tip()));
    public static readonly DependencyProperty FormatTickProperty = DependencyProperty.Register(nameof(FormatTick), typeof(Func<double, string>), typeof(LineChart), new FrameworkPropertyMetadata(null, (d, _) => ((LineChart)d).OnFormatTick()));
    public static readonly DependencyProperty HiddenSeriesProperty = DependencyProperty.Register(nameof(HiddenSeries), typeof(IReadOnlyList<string>), typeof(LineChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((LineChart)d).OnHidden()));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LineChart), new FrameworkPropertyMetadata(false, (d, _) => ((LineChart)d).OnLoading()));
    public static readonly DependencyProperty EmptyLabelProperty = DependencyProperty.Register(nameof(EmptyLabel), typeof(string), typeof(LineChart), new FrameworkPropertyMetadata("No data for this range", (d, _) => ((LineChart)d).Message()));
    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(nameof(ShowLegend), typeof(bool?), typeof(LineChart), new FrameworkPropertyMetadata(null, (d, _) => ((LineChart)d).Legend()));
    public static readonly DependencyProperty CategoryLabelProperty = DependencyProperty.Register(nameof(CategoryLabel), typeof(string), typeof(LineChart), new FrameworkPropertyMetadata("Date"));
    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(nameof(Curve), typeof(LineChartCurve), typeof(LineChart), new FrameworkPropertyMetadata(LineChartCurve.Smooth, (d, _) => ((LineChart)d).OnData()));

    /// <summary>The points, oldest first.</summary>
    public IReadOnlyList<LineChartDatum>? Data { get => (IReadOnlyList<LineChartDatum>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>The lines. The first defaults to the accent with a faint area, the rest to quieter neutral steps.</summary>
    public IReadOnlyList<LineChartSeries>? Series { get => (IReadOnlyList<LineChartSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    /// <summary>What is measured. Names the chart for assistive technology.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Unit after each value in the tooltip, such as "ms".</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Plot height in pixels. The width follows the container.</summary>
    public double PlotHeight { get => (double)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    /// <summary>Formats values in the tooltip.</summary>
    public Func<double, LineChartSeries, string>? FormatValue { get => (Func<double, LineChartSeries, string>?)GetValue(FormatValueProperty); set => SetValue(FormatValueProperty, value); }
    /// <summary>Formats the value axis. Defaults to a compact number, such as 1.2K.</summary>
    public Func<double, string>? FormatTick { get => (Func<double, string>?)GetValue(FormatTickProperty); set => SetValue(FormatTickProperty, value); }
    /// <summary>Keys of the hidden series. The legend writes it back.</summary>
    public IReadOnlyList<string>? HiddenSeries { get => (IReadOnlyList<string>?)GetValue(HiddenSeriesProperty); set => SetValue(HiddenSeriesProperty, value); }
    /// <summary>Keeps the current lines on screen, dimmed, while the next range loads.</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    /// <summary>Shown when there are no points.</summary>
    public string EmptyLabel { get => (string)GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }
    /// <summary>Series toggles above the plot. Shown by default when there is more than one series.</summary>
    public bool? ShowLegend { get => (bool?)GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <summary>What one point is, for the slider's spoken name.</summary>
    public string CategoryLabel { get => (string)GetValue(CategoryLabelProperty); set => SetValue(CategoryLabelProperty, value); }
    /// <summary>Smooth monotone curves that never swing past the data, or straight segments.</summary>
    public LineChartCurve Curve { get => (LineChartCurve)GetValue(CurveProperty); set => SetValue(CurveProperty, value); }

    /// <summary>Raised as the crosshair moves, and with null when it leaves. Use it to drive a headline readout.</summary>
    public event EventHandler<LineChartActiveEventArgs>? ActiveChanged;

    /// <summary>Room above the top gridline, and the gap between the crosshair and its tooltip.</summary>
    private const double Top = 12, Gap = 14;
    private static readonly double[] Grid241 = [.. Enumerable.Range(0, 241).Select(index => index / 240d)];
    private static readonly SpringTune Settle = SpringTune.Smooth, Glide = SpringTune.Snappy;

    private sealed class Track
    {
        public Point[] Shape = [];
        public GestureSpring Presence = new(1, .001, .002);
        public double[] Xs = [], From = [], To = [];
        public Point[] Target = [];
        public Color Color;
    }

    private sealed class Toggle : Border
    {
        public required string Key;
        public required SeriesSwatch Swatch;
        public required TrackedText Text;
        public required Border Strike;
        public required SolidColorBrush Copy;
        public required SolidColorBrush Wash;
        public required ScaleTransform Press;
        public required FocusVisible FocusRing;
    }

    // The legend's little line, solid or dashed, shortening when its series hides.
    private sealed class SeriesSwatch : FrameworkElement
    {
        public Color Color;
        public bool Dashed;
        public double Length = 1, Fade = 1;
        protected override Size MeasureOverride(Size availableSize) => new(14, 10);
        protected override void OnRender(DrawingContext dc)
        {
            var brush = ChartMath.Solid(ChartMath.Faded(Color, Fade));
            double width = 14 * Length;
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, 10)));
            if (Dashed) { for (double x = 0; x < 14; x += 7) { dc.DrawRoundedRectangle(brush, null, new Rect(x, 3.75, 4, 2.5), 2, 2); } }
            else { dc.DrawRoundedRectangle(brush, null, new Rect(0, 3.75, 14, 2.5), 2, 2); }
            dc.Pop();
        }
    }

    private readonly ChartSurface _plot;
    private readonly Canvas _overlay = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly Canvas _gutter = new() { IsHitTestVisible = false }, _axis = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly WrapPanel _legend = new() { Margin = new Thickness(-8, 0, -8, 16) };
    private readonly Dictionary<string, Track> _tracks = [];
    private readonly List<ChartRow> _rows = [];
    private readonly List<ChartAxisLabel> _labels = [];
    private readonly GestureSpring _min = new(0, .001, .002), _max = new(100, .001, .002), _cursor = new(1, .0001, .0002), _morph = new(1, .0005, .001), _tipX = new(0), _tipY = new(0);
    private readonly Timed _draw = new(0), _crossFade = new(0), _dotFade = new(0), _tipFade = new(0), _messageFade = new(0);
    private readonly FrameLoop _loop;
    private readonly FocusVisible _focus;
    private readonly InViewOnce _inView;
    private readonly SolidColorBrush _secondary = new(), _muted = new(), _ink = new(), _raised = new(), _edge = new();
    private readonly Border _tip = new() { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(12, 10, 12, 10), MinWidth = 140, IsHitTestVisible = false, Opacity = 0, RenderTransformOrigin = new Point(.5, .5) };
    private readonly StackPanel _tipLines = new();
    private readonly TextBlock _message = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14, IsHitTestVisible = false, Opacity = 0 };
    private readonly Dictionary<string, (TrackedText Name, TrackedText Value)> _tipRows = [];
    private TrackedText? _tipTitle;
    private Decorator? _host;
    private Grid? _chart;
    private LineChartDatum[] _data = [];
    private LineChartSeries[] _series = [];
    private string[] _hiddenList = [];
    private string _signature = "";
    private int? _active, _reported;
    private double[] _ticks = [0, 25, 50, 75, 100];
    private double _breath;
    private bool _scrubbedOnce, _first = true, _seen;
    private Color _gridColor, _strongColor, _surface;

    static LineChart() => DefaultStyleKeyProperty.OverrideMetadata(typeof(LineChart), new FrameworkPropertyMetadata(typeof(LineChart)));

    public LineChart()
    {
        _plot = new ChartSurface(Paint, surface => new ArcRangePeer(surface, AutomationControlType.Slider, () => $"{Label}, explore by {CategoryLabel.ToLowerInvariant()}", () => 1, () => Math.Max(1, _data.Length), () => (Index ?? Last) + 1, Spoken)) { Focusable = true, FocusVisualStyle = null };
        _tip.Child = _tipLines;
        _tip.RenderTransform = new TransformGroup { Children = { new ScaleTransform(.96, .96), new TranslateTransform() } };
        _overlay.Children.Add(_tip);
        _loop = new FrameLoop(Frame);
        _focus = new FocusVisible(_plot, () => { if (_focus is { Shown: true } && !Empty && _active is null) { SetActive(Last); } });
        _inView = new InViewOnce(this, StartDraw);
        _plot.MouseDown += (_, e) => { _focus.Pointer(); _plot.Focus(); if (!Empty && e.StylusDevice is null) { SetActive(PointAt(e.GetPosition(_plot).X)); } };
        _plot.MouseMove += (_, e) => { if (!Empty && e.StylusDevice is null) { SetActive(PointAt(e.GetPosition(_plot).X)); } };
        _plot.MouseLeave += (_, e) => { if (e.StylusDevice is null) { SetActive(null); } };
        _plot.TouchDown += (_, e) => { if (!Empty) { _plot.CaptureTouch(e.TouchDevice); SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; } };
        _plot.TouchMove += (_, e) => { if (!Empty) { SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; } };
        _plot.TouchUp += (_, e) => { _plot.ReleaseTouchCapture(e.TouchDevice); SetActive(null); e.Handled = true; };
        _plot.KeyDown += (_, e) => { if (Press(e.Key)) { e.Handled = true; _focus.Key(); } };
        _plot.LostKeyboardFocus += (_, _) => SetActive(null);
        _plot.SizeChanged += (_, _) => { Picks(false); Place(); _plot.InvalidateVisual(); };
        Loaded += (_, _) => { Colorize(); if (!Moving) { StartDraw(); } if (IsLoading) { Wake(); } };
        Unloaded += (_, _) => _loop.Stop();
        ThemeWatch.Add(this, Colorize);
    }

    internal int? ActiveForTest => _active;
    internal double[] TicksForTest => [.. _rows.Where(row => !row.Leaving).Select(row => row.Value)];
    internal string[] AxisForTest => [.. _labels.Where(label => !label.Leaving).Select(label => label.View.Text)];
    internal double MaxForTest => _max.Position;
    internal double MaxTargetForTest => _max.Target;
    internal double CursorForTest => _cursor.Position;
    internal double DrawnForTest => _draw.Value;
    internal double PresenceForTest(string key) => _tracks[key].Presence.Position;
    internal Point[] ShapeForTest(string key) => _tracks[key].Shape;
    internal string TipTitleForTest => _tipTitle?.Text ?? "";
    internal string[] TipValuesForTest => [.. _tipRows.Values.Select(row => row.Value.Text)];
    internal double TipXForTest => _tipX.Target;
    internal double TipWidthForTest => TipWidth;
    internal double TipOpacityForTest => _tip.Opacity;
    internal string MessageForTest => _message.Opacity > 0 || _messageFade.Target > 0 ? _message.Text : "";
    internal string SpokenForTest => Spoken();
    internal FrameworkElement PlotForTest => _plot;
    internal IReadOnlyList<UIElement> TogglesForTest => [.. _legend.Children.Cast<UIElement>()];
    internal void ToggleForTest(string key) => ToggleSeries(key);
    internal void ScrubForTest(double x) => SetActive(PointAt(x));
    internal void LeaveForTest() => SetActive(null);
    internal void KeyForTest(Key key) => Press(key);
    internal void DrawForTest() => StartDraw();
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { _loop.Advance(Math.Min(left, .05)); } }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Empty => _data.Length == 0;
    private int Last => _data.Length - 1;
    private int? Index => _active is { } at && !Empty ? Math.Min(at, Last) : null;
    private double H => PlotHeight;
    private LineChartSeries[] Visible => [.. _series.Where(line => !_hiddenList.Contains(line.Key))];
    private static double ValueOf(LineChartDatum item, string key) => item.Values.TryGetValue(key, out double value) ? value : 0;
    private string Format(double value, LineChartSeries line) => (FormatValue?.Invoke(value, line) ?? ChartMath.Grouped(value, 1)) + (string.IsNullOrEmpty(Unit) ? "" : $" {Unit}");
    private string TickText(double value) => FormatTick?.Invoke(value) ?? ChartMath.Compact(value);

    private Color ColorOf(LineChartSeries line, int index)
    {
        if (line.Color is { } own) { return own; }
        var surface = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        var ink = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        return index switch
        {
            0 => Tween.Resolve(this, "ArcWpf.Brush.Accent"),
            1 => OkColor.Mix(ink, surface, .46),
            _ => OkColor.Mix(ink, surface, .26),
        };
    }

    private string Spoken()
    {
        var visible = Visible;
        if (Index is { } at) { return $"{_data[at].Label}: {string.Join(", ", visible.Select(line => $"{line.Label} {Format(ValueOf(_data[at], line.Key), line)}"))}"; }
        return Empty ? EmptyLabel : $"{visible.Length} of {_series.Length} series shown";
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }
        _tip.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _chart = new Grid();
        _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(H) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        Grid.SetColumn(_gutter, 1);
        Grid.SetRow(_axis, 1);
        _chart.Children.Add(_plot);
        _chart.Children.Add(_message);
        _chart.Children.Add(_gutter);
        _chart.Children.Add(_axis);
        _chart.Children.Add(_overlay);
        Grid.SetColumnSpan(_overlay, 1);
        var figure = new StackPanel();
        figure.Children.Add(_legend);
        figure.Children.Add(_chart);
        _host.Child = figure;
        Colorize();
        OnSeries();
    }

    private void OnPlotHeight()
    {
        if (_chart is not null) { _chart.RowDefinitions[0].Height = new GridLength(H); }
        Place();
        _plot.InvalidateVisual();
    }

    private void OnFormatTick()
    {
        foreach (var row in _rows) { row.View.Text = TickText(row.Value); }
        Place();
    }

    private void OnSeries()
    {
        _series = [.. Series ?? []];
        foreach (string key in _tracks.Keys.Where(key => _series.All(line => line.Key != key)).ToList()) { _tracks.Remove(key); }
        Legend();
        Tip();
        OnData();
    }

    // New data morphs every line from the shape on screen, sampled on shared x positions so a range that gains or loses points still flows.
    private void OnData()
    {
        _data = [.. Data ?? []];
        string signature = $"{Curve}|{string.Join(",", _series.Select(line => line.Key))}|{string.Join(";", _data.Select(item => $"{item.Key}:{string.Join(",", _series.Select(line => ValueOf(item, line.Key)))}"))}";
        if (signature == _signature) { return; }
        _signature = signature;
        _active = null;
        Report();
        _scrubbedOnce = false;
        _crossFade.Go(0, 0, null);
        _dotFade.Go(0, 0, null);
        _tipFade.Go(0, 0, null);
        bool any = false;
        foreach (var (line, at) in _series.Select((line, at) => (line, at)))
        {
            var target = ChartMath.Curve(_data.Select(item => ValueOf(item, line.Key)).ToList(), Curve == LineChartCurve.Smooth);
            if (!_tracks.TryGetValue(line.Key, out var track))
            {
                track = new Track { Shape = target, Target = target };
                track.Presence.Jump(_hiddenList.Contains(line.Key) ? 0 : 1);
                _tracks[line.Key] = track;
                continue;
            }
            track.Target = target;
            if (track.Shape.Length == 0 || target.Length == 0 || !Moving) { track.Shape = target; track.Xs = []; continue; }
            double[] xs = [.. Grid241.Concat(target.Select(point => point.X)).Distinct().Order()];
            track.Xs = xs;
            track.From = [.. xs.Select(x => ChartMath.ValueAt(track.Shape, x))];
            track.To = [.. xs.Select(x => ChartMath.ValueAt(target, x))];
            any = true;
        }
        if (any) { _morph.Jump(0); _morph.To(1, Settle); }
        Rescale();
        Picks(!_first);
        Tip();
        Message();
        if (_seen && !Moving) { _draw.Jump(1); }
        else if (_seen && !Empty && _draw.Target < 1) { _draw.Go(1, Tween.Considered * 1.75, ArcEasing.InOut); }
        _first = false;
        Place();
        Wake();
        _plot.InvalidateVisual();
    }

    // The scale fits only the series on show, and rides its own springs.
    private void Rescale()
    {
        double low = double.PositiveInfinity, high = double.NegativeInfinity;
        foreach (var line in Visible)
        {
            foreach (var item in _data)
            {
                double value = ValueOf(item, line.Key);
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }
        if (double.IsFinite(low))
        {
            var range = ChartMath.NiceScale(low, high);
            _ticks = range.Ticks;
            if (_first || !Moving) { _min.Jump(range.Min); _max.Jump(range.Max); }
            else
            {
                if (_min.Target != range.Min) { _min.To(range.Min, Settle); }
                if (_max.Target != range.Max) { _max.To(range.Max, Settle); }
            }
        }
        else if (_first)
        {
            var range = ChartMath.NiceScale(0, 100);
            _ticks = range.Ticks;
            _min.Jump(range.Min);
            _max.Jump(range.Max);
        }
        Rows(_first);
    }

    private void Rows(bool first)
    {
        foreach (var row in _rows.Where(row => !row.Leaving && !_ticks.Contains(row.Value)))
        {
            row.Leaving = true;
            row.Presence.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        foreach (double value in _ticks)
        {
            if (_rows.Find(entry => entry.Value == value) is { } kept)
            {
                kept.Leaving = false;
                kept.Presence.Go(1, Moving ? Tween.Standard : 0, ArcEasing.Standard);
                continue;
            }
            var view = CopyMotion.Text(TickText(value), 12, 1, -.16, FontWeights.Normal, _muted);
            _gutter.Children.Add(view);
            var row = new ChartRow(value, view);
            if (first || !Moving) { row.Presence.Jump(1); } else { row.Presence.Go(1, Tween.Standard, ArcEasing.Standard); }
            _rows.Add(row);
        }
    }

    // Axis labels that fit: at most one per 76 pixels, counted back from the latest so the newest date always shows.
    private void Picks(bool animate)
    {
        double width = _plot.ActualWidth;
        var labeled = _data.Select((item, index) => (item, index)).Where(entry => !string.IsNullOrEmpty(entry.item.AxisLabel)).Select(entry => entry.index).ToList();
        var picks = new List<int>();
        if (width > 0 && labeled.Count > 0)
        {
            int room = Math.Max(2, (int)Math.Floor(width / 76)), stride = (int)Math.Ceiling((double)labeled.Count / room);
            labeled.Reverse();
            picks = [.. labeled.Where((_, rank) => rank % stride == 0).Reverse()];
        }
        var keys = picks.Select(at => ($"{_data[at].Key}:{_data[at].AxisLabel}", at)).ToList();
        foreach (var label in _labels.Where(label => !label.Leaving && keys.All(key => key.Item1 != label.Key)))
        {
            label.Leaving = true;
            label.Presence.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        foreach (var (key, at) in keys)
        {
            var label = _labels.Find(entry => entry.Key == key);
            if (label is null)
            {
                var view = CopyMotion.Text(_data[at].AxisLabel!, 12, 1, -.16, FontWeights.Normal, _muted);
                _axis.Children.Add(view);
                Canvas.SetTop(view, 8);
                label = new ChartAxisLabel(key, view, 0);
                _labels.Add(label);
                if (animate && Moving) { label.Presence.Go(1, Tween.Standard, ArcEasing.Standard); } else { label.Presence.Jump(1); }
            }
            label.Leaving = false;
            label.Shift = Last > 0 ? (double)at / Last : .5;
            if (!label.Presence.Active && label.Presence.Value < 1) { label.Presence.Go(1, Moving ? Tween.Standard : 0, ArcEasing.Standard); }
        }
    }

    private double RowY(double value)
    {
        double lo = _min.Position, span = _max.Position - lo;
        if (span == 0) { span = 1; }
        return Math.Round(Top + (1 - (value - lo) / span) * (H - Top), MidpointRounding.AwayFromZero) + .5;
    }

    private double ToY(double value)
    {
        double lo = _min.Position, span = _max.Position - lo;
        if (span == 0) { span = 1; }
        return Top + (1 - (value - lo) / span) * (H - Top);
    }

    private double TipWidth => Math.Max(_tip.DesiredSize.Width, 0);

    // One writer for the labels and the tooltip's target, whatever moved.
    private void Place()
    {
        double width = _plot.ActualWidth;
        foreach (var row in _rows)
        {
            Canvas.SetLeft(row.View, 10);
            Canvas.SetTop(row.View, RowY(row.Value) - 6);
            row.View.Opacity = row.Presence.Value;
        }
        foreach (var label in _labels)
        {
            label.View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label.View, label.Shift * width - label.Shift * label.View.DesiredSize.Width);
            label.View.Opacity = label.Presence.Value;
        }
        if (width <= 0) { return; }
        // The tooltip sits beside the crosshair, flips sides before it would leave the plot, and centres on the points it describes.
        double at = _cursor.Position, cx = Math.Round(at * width, MidpointRounding.AwayFromZero) + .5;
        _tip.MaxWidth = Math.Min(220, width);
        _tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tw = TipWidth, th = _tip.DesiredSize.Height;
        double x = cx + Gap;
        if (x + tw > width) { x = cx - Gap - tw; }
        double targetX = Math.Clamp(x, 0, Math.Max(0, width - tw));
        var ys = new List<double>();
        foreach (var line in _series)
        {
            if (!_tracks.TryGetValue(line.Key, out var track) || track.Shape.Length == 0 || track.Presence.Position <= .5) { continue; }
            ys.Add(DotY(track, at));
        }
        double middle = ys.Count > 0 ? ys.Average() : H / 2;
        double targetY = Math.Clamp(middle - th / 2, 0, Math.Max(0, H - th));
        if (!Moving) { _tipX.Jump(targetX); _tipY.Jump(targetY); }
        else
        {
            if (_tipX.Target != targetX) { _tipX.To(targetX, Glide); }
            if (_tipY.Target != targetY) { _tipY.To(targetY, Glide); }
        }
    }

    private double DotY(Track track, double at) { double lo = _min.Position; return ToY(lo + (ChartMath.ValueAt(track.Shape, at) - lo) * track.Presence.Position); }

    private void MoveTip()
    {
        var group = (TransformGroup)_tip.RenderTransform;
        var scale = (ScaleTransform)group.Children[0];
        var move = (TranslateTransform)group.Children[1];
        move.X = _tipX.Position;
        move.Y = _tipY.Position;
        scale.ScaleX = scale.ScaleY = .96 + .04 * _tipFade.Value;
        _tip.Opacity = _tipFade.Value;
    }

    private int PointAt(double x)
    {
        double width = _plot.ActualWidth;
        if (width <= 0 || Last < 1) { return Math.Max(0, Last); }
        return Math.Clamp((int)Math.Round(x / width * Last, MidpointRounding.AwayFromZero), 0, Last);
    }

    private bool Press(Key key)
    {
        if (key == Key.Escape && Index is not null) { SetActive(null); return true; }
        int last = Last, from = Index ?? last + 1, page = Math.Max(1, (int)Math.Round(_data.Length / 6d, MidpointRounding.AwayFromZero));
        int? next = key switch
        {
            Key.Left or Key.Down => from - 1,
            Key.Right or Key.Up => Index is null ? last : from + 1,
            Key.PageDown => from - page,
            Key.PageUp => from + page,
            Key.Home => 0,
            Key.End => last,
            _ => null,
        };
        if (next is null || Empty) { return false; }
        SetActive(Math.Clamp(next.Value, 0, last));
        return true;
    }

    private void SetActive(int? at)
    {
        if (Empty) { at = null; }
        if (_active == at) { return; }
        int? before = Index;
        _active = at;
        int? now = Index;
        if (now is { } index)
        {
            double to = Last > 0 ? (double)index / Last : .5;
            // The crosshair appears on the first scrubbed point and glides between the ones after it; the tooltip follows on its own spring.
            if (!Moving || !_scrubbedOnce)
            {
                _cursor.Jump(to);
                Tip();
                _tip.UpdateLayout();
                Place();
                _tipX.Jump(_tipX.Target);
                _tipY.Jump(_tipY.Target);
            }
            _scrubbedOnce = true;
            if (Moving) { _cursor.To(to, Glide); }
        }
        else { _scrubbedOnce = false; }
        bool scrubbing = now is not null;
        _crossFade.Go(scrubbing ? 1 : 0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        _dotFade.Go(scrubbing ? 1 : 0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        _tipFade.Go(scrubbing ? 1 : 0, Moving ? Tween.Fast : 0, ArcEasing.Standard);
        Tip();
        Report();
        if (UIElementAutomationPeer.FromElement(_plot) is { } peer) { peer.RaisePropertyChangedEvent(System.Windows.Automation.RangeValuePatternIdentifiers.ValueProperty, (double)((before ?? Last) + 1), (double)((now ?? Last) + 1)); }
        Wake();
    }

    private void Report()
    {
        int? index = Index;
        if (index == _reported) { return; }
        _reported = index;
        ActiveChanged?.Invoke(this, new LineChartActiveEventArgs(index, index is { } at ? _data[at] : null));
    }

    private void StartDraw()
    {
        _seen = true;
        if (Empty) { return; }
        if (!Moving) { _draw.Jump(1); _plot.InvalidateVisual(); return; }
        if (_draw.Target < 1) { _draw.Go(1, Tween.Considered * 1.75, ArcEasing.InOut); Wake(); }
    }

    private void OnHidden()
    {
        _hiddenList = [.. HiddenSeries ?? []];
        // A hidden series flattens into the baseline as it fades; the ones that stay rescale around it.
        foreach (var (key, track) in _tracks)
        {
            double to = _hiddenList.Contains(key) ? 0 : 1;
            if (!Moving) { track.Presence.Jump(to); }
            else if (track.Presence.Target != to) { track.Presence.To(to, Settle); }
        }
        Rescale();
        Legend();
        Tip();
        Message();
        Place();
        Wake();
        _plot.InvalidateVisual();
    }

    private void ToggleSeries(string key)
    {
        string[] next = _hiddenList.Contains(key) ? [.. _hiddenList.Where(item => item != key)] : [.. _hiddenList, key];
        SetCurrentValue(HiddenSeriesProperty, next);
    }

    private void OnLoading()
    {
        if (IsLoading) { Announcer.Say(this, "Loading"); _breath = 0; }
        Message();
        Wake();
        _plot.InvalidateVisual();
    }

    private void Message()
    {
        bool allHidden = !Empty && Visible.Length == 0;
        bool show = (Empty || allHidden) && !IsLoading;
        if (show) { _message.Text = Empty ? EmptyLabel : "Choose a series to show"; }
        _messageFade.Go(show ? 1 : 0, Moving ? show ? Tween.Standard : Tween.Instant : 0, ArcEasing.Standard);
        _message.Opacity = _messageFade.Value;
        _plot.Focusable = !Empty;
        Wake();
    }

    private void Legend()
    {
        bool show = ShowLegend ?? _series.Length > 1;
        _legend.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_legend.Children.Count != _series.Length || _legend.Children.OfType<Toggle>().Select(toggle => toggle.Key).SequenceEqual(_series.Select(line => line.Key)) is false)
        {
            _legend.Children.Clear();
            foreach (var (line, at) in _series.Select((line, at) => (line, at))) { _legend.Children.Add(MakeToggle(line, at)); }
        }
        foreach (var toggle in _legend.Children.OfType<Toggle>()) { DressToggle(toggle, true); }
    }

    private Toggle MakeToggle(LineChartSeries line, int at)
    {
        var copy = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.TextSecondary"));
        var swatch = new SeriesSwatch { Dashed = line.Dashed, Color = ColorOf(line, at), VerticalAlignment = VerticalAlignment.Center };
        var text = CopyMotion.Text(line.Label, 14, 1.4, -.16, FontWeights.Normal, copy);
        var strike = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        var words = new Grid { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(text);
        words.Children.Add(strike);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(swatch);
        row.Children.Add(words);
        var wash = new SolidColorBrush(Colors.Transparent);
        var press = new ScaleTransform();
        Toggle toggle = null!;
        toggle = new Toggle
        {
            Key = line.Key, Swatch = swatch, Text = text, Strike = strike, Copy = copy, Wash = wash, Press = press,
            FocusRing = null!,
            MinHeight = 32, Padding = new Thickness(8, 0, 10, 0), Margin = new Thickness(0, 0, 4, 2), CornerRadius = new CornerRadius(16), Background = wash, Child = row, Cursor = Cursors.Hand,
            Focusable = true, FocusVisualStyle = null, RenderTransform = press, RenderTransformOrigin = new Point(.5, .5),
        };
        toggle.BorderThickness = new Thickness(0);
        toggle.FocusRing = new FocusVisible(toggle, () => toggle.BorderBrush = toggle.FocusRing is { Shown: true } ? FocusVisible.RingBrush(this) : null);
        toggle.MouseEnter += (_, _) => DressToggle(toggle, true);
        toggle.MouseLeave += (_, _) => { Tween.Press(press, false, .97, Moving); DressToggle(toggle, true); };
        toggle.MouseLeftButtonDown += (_, e) => { toggle.FocusRing.Pointer(); toggle.Focus(); toggle.CaptureMouse(); Tween.Press(press, true, .97, Moving); e.Handled = true; };
        toggle.MouseLeftButtonUp += (_, e) =>
        {
            bool over = toggle.IsMouseOver;
            toggle.ReleaseMouseCapture();
            Tween.Press(press, false, .97, Moving);
            if (over) { ToggleSeries(line.Key); }
            e.Handled = true;
        };
        toggle.KeyDown += (_, e) =>
        {
            toggle.FocusRing.Key();
            if (e.Key is Key.Enter or Key.Space) { ToggleSeries(line.Key); e.Handled = true; }
        };
        System.Windows.Automation.AutomationProperties.SetName(toggle, line.Label);
        return toggle;
    }

    private void DressToggle(Toggle toggle, bool animate)
    {
        bool shown = !_hiddenList.Contains(toggle.Key);
        bool hover = toggle.IsMouseOver;
        var ink = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        var to = hover ? ink : Tween.Resolve(this, shown ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.TextMuted");
        bool cross = animate && Moving;
        Tween.Color(toggle.Copy, to, Tween.Fast, cross);
        Tween.Color(toggle.Wash, hover ? OkColor.Alpha(ink, .05) : OkColor.Alpha(ink, 0), Tween.Fast, cross);
        toggle.Text.Opacity = shown ? 1 : .72;
        toggle.Strike.Visibility = shown ? Visibility.Collapsed : Visibility.Visible;
        toggle.Strike.Background = ChartMath.Solid(ChartMath.Faded(to, .55));
        int at = Array.FindIndex(_series, line => line.Key == toggle.Key);
        if (at >= 0) { toggle.Swatch.Color = ColorOf(_series[at], at); }
        toggle.Swatch.Length = shown ? 1 : .35;
        toggle.Swatch.Fade = shown ? 1 : .45;
        toggle.Swatch.InvalidateVisual();
    }

    // The tooltip: one title and a row per series on show.
    private void Tip()
    {
        if (_host is null) { return; }
        var visible = Visible;
        var reading = Index is { } at ? _data[at] : _data.LastOrDefault();
        if (_tipTitle is null || _tipRows.Count != visible.Length || visible.Any(line => !_tipRows.ContainsKey(line.Key)))
        {
            _tipLines.Children.Clear();
            _tipRows.Clear();
            _tipTitle = CopyMotion.Text("", 12, 1.4, -.16, FontWeights.Normal, _secondary);
            _tipTitle.Margin = new Thickness(0, 0, 0, 6);
            _tipLines.Children.Add(_tipTitle);
            for (int index = 0; index < visible.Length; index++)
            {
                var line = visible[index];
                var row = new DockPanel { Margin = new Thickness(0, index == 0 ? 0 : 4, 0, 0) };
                var key = new SeriesSwatch { Color = ColorOf(line, Array.IndexOf(_series, line)), Dashed = line.Dashed, Width = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Clip = new RectangleGeometry(new Rect(0, 0, 10, 10)) };
                var name = CopyMotion.Text(line.Label, 14, 1.4, -.16, FontWeights.Normal, _secondary);
                var value = CopyMotion.Text("", 14, 1.4, -.16, FontWeights.Medium, _ink);
                value.Margin = new Thickness(16, 0, 0, 0);
                DockPanel.SetDock(key, Dock.Left);
                DockPanel.SetDock(value, Dock.Right);
                row.Children.Add(key);
                row.Children.Add(value);
                row.Children.Add(name);
                _tipLines.Children.Add(row);
                _tipRows[line.Key] = (name, value);
            }
        }
        _tipTitle.Text = reading?.Label ?? "";
        foreach (var line in visible) { _tipRows[line.Key].Value.Text = Format(reading is null ? 0 : ValueOf(reading, line.Key), line); }
        foreach (var row in _tipLines.Children.OfType<DockPanel>()) { foreach (var key in row.Children.OfType<SeriesSwatch>()) { key.InvalidateVisual(); } }
    }

    private void Colorize()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _raised.Color = Tween.Resolve(this, "ArcWpf.Brush.SurfaceRaised");
        _edge.Color = Tween.Resolve(this, "ArcWpf.Brush.Border");
        _gridColor = _edge.Color;
        _strongColor = Tween.Resolve(this, "ArcWpf.Brush.BorderStrong");
        _surface = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        _tip.Background = _raised;
        _tip.BorderBrush = _edge;
        _message.Foreground = _muted;
        foreach (var (line, at) in _series.Select((line, at) => (line, at))) { if (_tracks.TryGetValue(line.Key, out var track)) { track.Color = ColorOf(line, at); } }
        foreach (var toggle in _legend.Children.OfType<Toggle>()) { DressToggle(toggle, false); }
        _plot.InvalidateVisual();
    }

    private void Wake() { if (Moving) { _loop.Start(); } else { Settled(); } }

    private void Settled()
    {
        foreach (var row in _rows.Where(row => row.Leaving).ToList()) { _gutter.Children.Remove(row.View); _rows.Remove(row); }
        foreach (var label in _labels.Where(label => label.Leaving).ToList()) { _axis.Children.Remove(label.View); _labels.Remove(label); }
        Place();
        MoveTip();
        _plot.InvalidateVisual();
    }

    private bool Frame(double seconds)
    {
        bool busy = false;
        foreach (var spring in new[] { _min, _max, _cursor }) { spring.Step(seconds); busy |= spring.Active; }
        if (_morph.Active)
        {
            _morph.Step(seconds);
            busy = true;
            double progress = _morph.Position;
            foreach (var track in _tracks.Values)
            {
                if (track.Xs.Length == 0) { continue; }
                if (!_morph.Active) { track.Shape = track.Target; track.Xs = []; continue; }
                var frame = new Point[track.Xs.Length];
                for (int at = 0; at < frame.Length; at++) { frame[at] = new Point(track.Xs[at], track.From[at] + (track.To[at] - track.From[at]) * progress); }
                track.Shape = frame;
            }
        }
        foreach (var track in _tracks.Values) { track.Presence.Step(seconds); busy |= track.Presence.Active; }
        foreach (var timed in new[] { _draw, _crossFade, _dotFade, _tipFade, _messageFade }) { timed.Step(seconds); busy |= timed.Active; }
        _message.Opacity = _messageFade.Value;
        foreach (var row in _rows.ToList())
        {
            row.Presence.Step(seconds);
            busy |= row.Presence.Active;
            if (row.Leaving && !row.Presence.Active) { _gutter.Children.Remove(row.View); _rows.Remove(row); }
        }
        foreach (var label in _labels.ToList())
        {
            label.Presence.Step(seconds);
            busy |= label.Presence.Active;
            if (label.Leaving && !label.Presence.Active) { _axis.Children.Remove(label.View); _labels.Remove(label); }
        }
        Place();
        _tipX.Step(seconds);
        _tipY.Step(seconds);
        busy |= _tipX.Active || _tipY.Active;
        MoveTip();
        if (IsLoading) { _breath += seconds; busy = true; }
        _plot.InvalidateVisual();
        return busy;
    }

    // breathe: from .22 to .45 over 1.4 s, there and back.
    private double Breathing
    {
        get
        {
            double phase = _breath / 1.4 % 2;
            double t = phase <= 1 ? phase : 2 - phase;
            return .22 + (.45 - .22) * ArcEasing.InOut.Ease(t);
        }
    }

    private void Paint(DrawingContext dc, Size size)
    {
        double width = size.Width;
        if (width <= 0) { return; }
        foreach (var row in _rows.Where(row => row.Value != _ticks[0]))
        {
            double y = RowY(row.Value);
            dc.DrawLine(ChartMath.Crisp(ChartMath.Faded(_gridColor, row.Presence.Value)), new Point(0, y), new Point(width, y));
        }
        dc.DrawLine(ChartMath.Crisp(_strongColor), new Point(0, H - .5), new Point(width, H - .5));
        double at = _cursor.Position, cx = Math.Round(at * width, MidpointRounding.AwayFromZero) + .5;
        if (_crossFade.Value > 0) { dc.DrawLine(ChartMath.Crisp(ChartMath.Faded(_strongColor, _crossFade.Value)), new Point(cx, Top - 4), new Point(cx, H)); }

        if (IsLoading && Empty)
        {
            var band = new LinearGradientBrush(Colors.Transparent, OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), .05), 90) { Opacity = Breathing / .45 };
            dc.DrawRoundedRectangle(band, null, new Rect(0, H * .3, width, H * .45), 12, 12);
        }

        // The lines draw left to right the first time the chart is seen with data.
        dc.PushClip(new RectangleGeometry(new Rect(-8, -16, Math.Max(0, _draw.Value * (width + 16)), H + 32)));
        dc.PushOpacity(IsLoading ? Breathing : 1);
        double lo = _min.Position;
        foreach (var (line, index) in _series.Select((line, index) => (line, index)))
        {
            if (!_tracks.TryGetValue(line.Key, out var track) || track.Shape.Length == 0) { continue; }
            double p = Math.Clamp(track.Presence.Position, 0, 1);
            if (p <= 0) { continue; }
            var color = track.Color != default ? track.Color : ColorOf(line, index);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(track.Shape[0].X * width, ToY(lo + (track.Shape[0].Y - lo) * track.Presence.Position)), false, false);
                for (int point = 1; point < track.Shape.Length; point++) { ctx.LineTo(new Point(track.Shape[point].X * width, ToY(lo + (track.Shape[point].Y - lo) * track.Presence.Position)), true, true); }
            }
            geometry.Freeze();
            dc.PushOpacity(p);
            if (line.Area ?? index == 0)
            {
                var area = new StreamGeometry();
                using (var ctx = area.Open())
                {
                    ctx.BeginFigure(new Point(track.Shape[0].X * width, ToY(lo + (track.Shape[0].Y - lo) * track.Presence.Position)), true, true);
                    for (int point = 1; point < track.Shape.Length; point++) { ctx.LineTo(new Point(track.Shape[point].X * width, ToY(lo + (track.Shape[point].Y - lo) * track.Presence.Position)), false, false); }
                    ctx.LineTo(new Point(width, H), false, false);
                    ctx.LineTo(new Point(0, H), false, false);
                }
                area.Freeze();
                dc.DrawGeometry(ChartMath.Solid(ChartMath.Faded(color, .09)), null, area);
            }
            double thick = line.Dashed ? 1.75 : 2;
            var pen = new Pen(ChartMath.Solid(color), thick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (line.Dashed) { pen.DashStyle = new DashStyle([4 / thick, 5 / thick], 0); pen.DashCap = PenLineCap.Round; }
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
            dc.Pop();
        }
        dc.Pop();
        dc.Pop();

        if (_dotFade.Value > 0)
        {
            var ring = new Pen(ChartMath.Solid(_surface), 2);
            ring.Freeze();
            foreach (var (line, index) in _series.Select((line, index) => (line, index)))
            {
                if (_hiddenList.Contains(line.Key) || !_tracks.TryGetValue(line.Key, out var track) || track.Shape.Length == 0) { continue; }
                dc.PushOpacity(_dotFade.Value);
                dc.DrawEllipse(ChartMath.Solid(track.Color != default ? track.Color : ColorOf(line, index)), ring, new Point(at * width, DotY(track, at)), 4, 4);
                dc.Pop();
            }
        }
    }
}
