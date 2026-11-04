using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>One part of the whole. A segment whose key survives a data change keeps its place and color, so switching datasets morphs the arcs.</summary>
public sealed record DonutChartDatum(string Key, string Label, double Value, Color? Color = null);

public enum DonutLegendAction { Toggle, Select }

/// <summary>Where the legend sits. Auto puts it beside the ring from 460 pixels of width and below it under that.</summary>
public enum DonutLegendPlacement { Auto, Below, Beside }

// The geometry of donut-chart.tsx.
internal static class DonutMath
{
    private const double Tau = Math.PI * 2;

    public sealed record Slice(string Key, string Label, double Value, Color? Color, DonutChartDatum[]? Members);

    // Small parts join one "Other" segment, but only when at least two would; grouping a single part hides it for nothing.
    public static Slice[] Slices(IReadOnlyList<DonutChartDatum> data, double groupBelow, int maxSegments, string otherLabel)
    {
        var parts = data.Where(item => item.Value > 0).ToList();
        double total = parts.Sum(item => item.Value);
        if (total == 0) { return []; }
        var keep = parts.OrderByDescending(item => item.Value).Where((item, rank) => item.Value / total >= groupBelow && rank < maxSegments - 1).Select(item => item.Key).ToHashSet();
        var rest = parts.Where(item => !keep.Contains(item.Key)).ToArray();
        if (rest.Length < 2) { return [.. parts.Select(item => new Slice(item.Key, item.Label, item.Value, item.Color, null))]; }
        return [.. parts.Where(item => keep.Contains(item.Key)).Select(item => new Slice(item.Key, item.Label, item.Value, item.Color, null)), new Slice(DonutChart.OtherKey, otherLabel, rest.Sum(item => item.Value), null, rest)];
    }

    // Half the width, in pixels, of a sector's outer edge once the gap is cut; below a couple of pixels a segment fades rather than drawing a hairline.
    public static double Breadth(double outer, double t0, double t1, double gap)
    {
        double turns = t1 - t0;
        if (turns >= 1 - 1e-6) { return double.PositiveInfinity; }
        return Math.Sin(Math.Min(turns * Math.PI, Math.PI / 2)) * outer - Math.Min(gap / 2, (1 - turns) * Tau * outer / 2);
    }

    // An annular sector between two turns of the ring, measured clockwise from the top, with rounded corners. Its sides run parallel to the radius at a fixed distance, so the gap between neighbours is the same number of pixels from the inner edge to the outer one. A thin sector narrows to a wedge and then to nothing, so a closing segment never pops.
    public static StreamGeometry? Sector(double c, double R, double r, double t0, double t1, double gap, double corner)
    {
        double turns = t1 - t0;
        if (turns <= 1e-6) { return null; }
        Point At(double radius, double angle) => new(c + radius * Math.Cos(angle), c + radius * Math.Sin(angle));
        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using var ctx = geometry.Open();
        if (turns >= 1 - 1e-6)
        {
            ctx.BeginFigure(At(R, -Math.PI / 2), true, true);
            ctx.ArcTo(At(R, Math.PI / 2), new Size(R, R), 0, true, SweepDirection.Clockwise, false, false);
            ctx.ArcTo(At(R, -Math.PI / 2), new Size(R, R), 0, true, SweepDirection.Clockwise, false, false);
            ctx.BeginFigure(At(r, -Math.PI / 2), true, true);
            ctx.ArcTo(At(r, Math.PI / 2), new Size(r, r), 0, true, SweepDirection.Counterclockwise, false, false);
            ctx.ArcTo(At(r, -Math.PI / 2), new Size(r, r), 0, true, SweepDirection.Counterclockwise, false, false);
            ctx.Close();
            geometry.Freeze();
            return geometry;
        }
        // Half the gap on each side; a lone segment closes its gap as the rest of the ring empties, so it becomes a whole ring without a jump.
        double p = Math.Min(gap / 2, (1 - turns) * Tau * R / 2);
        double a0 = t0 * Tau - Math.PI / 2, a1 = t1 * Tau - Math.PI / 2, half = (a1 - a0) / 2, s = Math.Sin(Math.Min(half, Math.PI / 2));
        if (s * R <= p + 1e-6) { ctx.Close(); return null; }
        Point Side(double t, double angle, int sign) => new(c + t * Math.Cos(angle) - sign * p * Math.Sin(angle), c + t * Math.Sin(angle) + sign * p * Math.Cos(angle));
        double band = (R - r) / 2;
        double ro = Math.Max(0, Math.Min(Math.Min(corner, band), s >= .9999 ? double.PositiveInfinity : (s * R - p) / (1 + s)));
        double dO = Math.Asin(Math.Min(1, (p + ro) / (R - ro))), tO = Math.Sqrt(Math.Max(0, (R - ro) * (R - ro) - (p + ro) * (p + ro)));
        bool bigO = a1 - a0 - 2 * dO > Math.PI;
        ctx.BeginFigure(Side(tO, a0, 1), true, true);
        if (ro > .01) { ctx.ArcTo(At(R, a0 + dO), new Size(ro, ro), 0, false, SweepDirection.Clockwise, false, false); }
        ctx.ArcTo(At(R, a1 - dO), new Size(R, R), 0, bigO, SweepDirection.Clockwise, false, false);
        if (ro > .01) { ctx.ArcTo(Side(tO, a1, -1), new Size(ro, ro), 0, false, SweepDirection.Clockwise, false, false); }
        if (s * r > p + 1e-6 || s >= .9999)
        {
            double ri = Math.Max(0, Math.Min(Math.Min(corner, band), s >= .9999 ? double.PositiveInfinity : (s * r - p) / (1 - s)));
            double dI = Math.Asin(Math.Min(1, (p + ri) / (r + ri))), tI = Math.Sqrt(Math.Max(0, (r + ri) * (r + ri) - (p + ri) * (p + ri)));
            bool bigI = a1 - a0 - 2 * dI > Math.PI;
            ctx.LineTo(Side(tI, a1, -1), false, false);
            if (ri > .01) { ctx.ArcTo(At(r, a1 - dI), new Size(ri, ri), 0, false, SweepDirection.Clockwise, false, false); }
            ctx.ArcTo(At(r, a0 + dI), new Size(r, r), 0, bigI, SweepDirection.Counterclockwise, false, false);
            if (ri > .01) { ctx.ArcTo(Side(tI, a0, 1), new Size(ri, ri), 0, false, SweepDirection.Clockwise, false, false); }
        }
        else { ctx.LineTo(At(p / s, (a0 + a1) / 2), false, false); }
        ctx.Close();
        geometry.Freeze();
        return geometry;
    }

    // Intl percent with no decimals, with "<1%" for a sliver.
    public static string Share(double share) => share > 0 && share < .01 ? "<1%" : Math.Round(share * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
}

// donut-chart.tsx (frozen 2026-11-04): a few parts of one whole, with a center readout and a synced legend.
public class DonutChart : Control
{
    internal const string OtherKey = "__other";

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(IReadOnlyList<DonutChartDatum>), typeof(DonutChart), new FrameworkPropertyMetadata(null, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata("", (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty FormatValueProperty = DependencyProperty.Register(nameof(FormatValue), typeof(Func<double, string>), typeof(DonutChart), new FrameworkPropertyMetadata(null, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty TotalLabelProperty = DependencyProperty.Register(nameof(TotalLabel), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata("Total", (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty RingSizeProperty = DependencyProperty.Register(nameof(RingSize), typeof(double), typeof(DonutChart), new FrameworkPropertyMetadata(208d, (d, _) => ((DonutChart)d).OnGeometry()));
    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(nameof(RingThickness), typeof(double), typeof(DonutChart), new FrameworkPropertyMetadata(24d, (d, _) => ((DonutChart)d).OnGeometry()));
    public static readonly DependencyProperty GroupBelowProperty = DependencyProperty.Register(nameof(GroupBelow), typeof(double), typeof(DonutChart), new FrameworkPropertyMetadata(.04, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty MaxSegmentsProperty = DependencyProperty.Register(nameof(MaxSegments), typeof(int), typeof(DonutChart), new FrameworkPropertyMetadata(6, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty OtherLabelProperty = DependencyProperty.Register(nameof(OtherLabel), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata("Other", (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty ActiveKeyProperty = DependencyProperty.Register(nameof(ActiveKey), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DonutChart)d).OnActive()));
    public static readonly DependencyProperty HiddenKeysProperty = DependencyProperty.Register(nameof(HiddenKeys), typeof(IReadOnlyList<string>), typeof(DonutChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty LegendActionProperty = DependencyProperty.Register(nameof(LegendAction), typeof(DonutLegendAction), typeof(DonutChart), new FrameworkPropertyMetadata(DonutLegendAction.Toggle, (d, _) => ((DonutChart)d).OnData()));
    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(nameof(ShowLegend), typeof(bool), typeof(DonutChart), new FrameworkPropertyMetadata(true, (d, _) => ((DonutChart)d).OnGeometry()));
    public static readonly DependencyProperty LegendPlacementProperty = DependencyProperty.Register(nameof(LegendPlacement), typeof(DonutLegendPlacement), typeof(DonutChart), new FrameworkPropertyMetadata(DonutLegendPlacement.Auto, (d, _) => ((DonutChart)d).OnGeometry()));
    public static readonly DependencyProperty EmptyLabelProperty = DependencyProperty.Register(nameof(EmptyLabel), typeof(string), typeof(DonutChart), new FrameworkPropertyMetadata("No data yet", (d, _) => ((DonutChart)d).OnData()));

    /// <summary>The parts of the whole.</summary>
    public IReadOnlyList<DonutChartDatum>? Data { get => (IReadOnlyList<DonutChartDatum>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>What the whole is. Names the chart for assistive technology.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Unit after values, such as "visits".</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Formats values. Defaults to a grouped number with up to one decimal.</summary>
    public Func<double, string>? FormatValue { get => (Func<double, string>?)GetValue(FormatValueProperty); set => SetValue(FormatValueProperty, value); }
    /// <summary>Center label at rest, above the total.</summary>
    public string TotalLabel { get => (string)GetValue(TotalLabelProperty); set => SetValue(TotalLabelProperty, value); }
    /// <summary>Diameter in pixels. The chart scales down to fit narrower containers.</summary>
    public double RingSize { get => (double)GetValue(RingSizeProperty); set => SetValue(RingSizeProperty, value); }
    /// <summary>Ring thickness in pixels.</summary>
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }
    /// <summary>Segments below this share of the total join "Other".</summary>
    public double GroupBelow { get => (double)GetValue(GroupBelowProperty); set => SetValue(GroupBelowProperty, value); }
    /// <summary>The most segments drawn, counting "Other". The smallest parts beyond it are grouped.</summary>
    public int MaxSegments { get => (int)GetValue(MaxSegmentsProperty); set => SetValue(MaxSegmentsProperty, value); }
    public string OtherLabel { get => (string)GetValue(OtherLabelProperty); set => SetValue(OtherLabelProperty, value); }
    /// <summary>The selected segment key. Hover and focus preview other segments without changing it.</summary>
    public string? ActiveKey { get => (string?)GetValue(ActiveKeyProperty); set => SetValue(ActiveKeyProperty, value); }
    /// <summary>Hidden segment keys. A hidden segment closes and the rest of the ring redistributes.</summary>
    public IReadOnlyList<string>? HiddenKeys { get => (IReadOnlyList<string>?)GetValue(HiddenKeysProperty); set => SetValue(HiddenKeysProperty, value); }
    /// <summary>What clicking a legend row does: show or hide its segment, or pin it as the selected segment.</summary>
    public DonutLegendAction LegendAction { get => (DonutLegendAction)GetValue(LegendActionProperty); set => SetValue(LegendActionProperty, value); }
    /// <summary>The synced legend beside or below the ring.</summary>
    public bool ShowLegend { get => (bool)GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public DonutLegendPlacement LegendPlacement { get => (DonutLegendPlacement)GetValue(LegendPlacementProperty); set => SetValue(LegendPlacementProperty, value); }
    public string EmptyLabel { get => (string)GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }

    /// <summary>Room left outside the ring for the lift; the gap between segments and their corner radius, in pixels.</summary>
    private const double Lift = 4, Thicken = 2, RingMargin = 7, SegmentGap = 3, Corner = 4, Tau = Math.PI * 2;
    private const string TotalKey = "__total";
    private static readonly SpringTune Reveal = SpringTune.Physical(.72), Settle = SpringTune.Physical(.5), Pop = SpringTune.Snappy, CountTune = SpringTune.Physical(.4);
    private static readonly double[] Neutral = [.56, .42, .32];

    private sealed class Segment
    {
        public GestureSpring Start = new(0, .0005, .001), End = new(0, .0005, .001), LiftBy = new(0, .002, .004);
        public TimedColor Color = new(Colors.Transparent);
        public Timed Dim = new(1);
        public double EndDelay, EndTarget;
        public bool Leaving;
        public DonutMath.Slice Slice = null!;
    }

    // A number that counts to each new value. Whole targets count in whole steps.
    private sealed class Count(TrackedText view, Func<double, string> format, double value)
    {
        public GestureSpring Spring = new(value, Math.Max(Math.Abs(value) * .001, 1e-6));
        public TrackedText View = view;
        public Func<double, string> Format = format;
        public double Target = value;
        public void Write() => View.Text = Format(IsWhole(Target) ? Math.Round(Spring.Position, MidpointRounding.AwayFromZero) : Spring.Position);
        private static bool IsWhole(double value) => value == Math.Floor(value);
    }

    // The readouts in the middle of the ring stay mounted in one cell and roll like a drum.
    private sealed class Readout(string key)
    {
        public string Key { get; } = key;
        public required StackPanel View;
        public required TrackedText Label, Meta, Value;
        public required TranslateTransform Move;
        public required Count? ValueCount, ShareCount;
        public required TrackedText? ShareText;
        public required StackPanel? MetaRow;
        public required TrackedText? MetaTail;
    }

    private sealed class Row
    {
        public required string Key;
        public required Border Button;
        public required ScaleTransform Press;
        public required Ellipse Dot;
        public required SolidColorBrush DotFill, DotStroke, Wash, LabelBrush, ValueBrush, ShareBrush;
        public required TrackedText Label;
        public required TextBlock Members;
        public required Count ValueCount, ShareCount;
        public required FocusVisible FocusRing;
        public required Presence Holder;
    }

    // A row that joins or leaves opens or closes its height, so the rows below and the card around them glide instead of jumping.
    private sealed class Presence : Decorator
    {
        public readonly GestureSpring Share = new(0, .002, .02);
        public bool Leaving;
        protected override Size MeasureOverride(Size constraint)
        {
            Child?.Measure(new Size(constraint.Width, double.PositiveInfinity));
            double height = Child?.DesiredSize.Height ?? 0;
            return new Size(Child?.DesiredSize.Width ?? 0, height * Math.Clamp(Share.Position, 0, 1));
        }
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
            return arrangeSize;
        }
        protected override Geometry GetLayoutClip(Size layoutSlotSize) => new RectangleGeometry(new Rect(RenderSize));
    }

    private sealed class FigureLayout(DonutChart owner) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
            owner.UpdatePlacement(width);
            var (ring, legend) = (owner._ringBox, owner._legendHost);
            bool beside = owner._beside && owner.ShowLegend;
            double ringSize = Math.Min(owner.RingSize, width);
            ring.Measure(new Size(ringSize, ringSize));
            if (!owner.ShowLegend || owner._slices.Length == 0) { return new Size(width, ring.DesiredSize.Height); }
            if (beside)
            {
                legend.Measure(new Size(Math.Max(0, width - ringSize - 32), double.PositiveInfinity));
                return new Size(width, Math.Max(ring.DesiredSize.Height, legend.DesiredSize.Height));
            }
            legend.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, ring.DesiredSize.Height + 24 + legend.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (ring, legend) = (owner._ringBox, owner._legendHost);
            double ringWidth = ring.DesiredSize.Width, ringHeight = ring.DesiredSize.Height;
            if (!owner.ShowLegend || owner._slices.Length == 0)
            {
                ring.Arrange(new Rect((finalSize.Width - ringWidth) / 2, 0, ringWidth, ringHeight));
                legend.Arrange(new Rect(0, 0, 0, 0));
            }
            else if (owner._beside)
            {
                ring.Arrange(new Rect(0, (finalSize.Height - ringHeight) / 2, ringWidth, ringHeight));
                double x = ringWidth + 32;
                legend.Arrange(new Rect(x, (finalSize.Height - legend.DesiredSize.Height) / 2, Math.Max(0, finalSize.Width - x), legend.DesiredSize.Height));
            }
            else
            {
                ring.Arrange(new Rect((finalSize.Width - ringWidth) / 2, 0, ringWidth, ringHeight));
                legend.Arrange(new Rect(0, ringHeight + 24, finalSize.Width, legend.DesiredSize.Height));
            }
            return finalSize;
        }
    }

    private readonly ChartSurface _ring;
    private readonly Grid _ringBox = new();
    private readonly Grid _center = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _rows = new() { Margin = new Thickness(0, 0, 0, -2) };
    private readonly Decorator _legendHost = new();
    private readonly FigureLayout _figure;
    private readonly FrameLoop _loop;
    private readonly FocusVisible _ringFocus;
    private readonly InViewOnce _inView;
    private readonly Dictionary<string, Segment> _segments = [];
    private readonly List<string> _order = [];
    private readonly Dictionary<string, Readout> _readouts = [];
    private readonly Dictionary<string, Row> _rowMap = [];
    private readonly Dictionary<string, int> _palette = [];
    private readonly Dictionary<string, (double Start, double End)> _targets = [];
    private readonly Timed _trackFade = new(1);
    private readonly List<Count> _counts = [];
    private Decorator? _host;
    private DonutMath.Slice[] _slices = [];
    private DonutMath.Slice[] _visible = [];
    private double _total;
    private string _signature = "", _identity = TotalKey, _current = "";
    private string? _preview;
    private bool _shown, _started, _beside;
    private Color _track;

    static DonutChart() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DonutChart), new FrameworkPropertyMetadata(typeof(DonutChart)));

    public DonutChart()
    {
        _ring = new ChartSurface(Paint, surface => new DonutRingPeer(surface, this)) { FocusVisualStyle = null };
        _ringBox.Children.Add(_ring);
        _ringBox.Children.Add(_center);
        _figure = new FigureLayout(this);
        _figure.Children.Add(_ringBox);
        _figure.Children.Add(_legendHost);
        _legendHost.Child = _rows;
        _rows.MouseLeave += (_, _) => SetPreview(null);
        _loop = new FrameLoop(Frame);
        _ringFocus = new FocusVisible(_ring, () => { });
        _inView = new InViewOnce(this, Show);
        _ring.MouseMove += (_, e) => { if (e.StylusDevice is null && Hit(e.GetPosition(_ring)) is var hit && hit.Known && hit.Key != _preview) { SetPreview(hit.Key); } };
        _ring.MouseLeave += (_, _) => SetPreview(null);
        _ring.MouseLeftButtonUp += (_, e) => { if (Hit(e.GetPosition(_ring)) is { Key: { } key }) { Select(ActiveKey == key ? null : key); } };
        _ring.KeyDown += (_, e) => { if (RingKey(e.Key)) { e.Handled = true; _ringFocus.Key(); } };
        _ring.LostKeyboardFocus += (_, _) => { if (!ShowLegend) { SetPreview(null); } };
        Loaded += (_, _) => { Colorize(); if (!Moving) { Show(); } };
        Unloaded += (_, _) => _loop.Stop();
        ThemeWatch.Add(this, Colorize);
    }

    internal string[] SlicesForTest => [.. _slices.Select(slice => slice.Key)];
    internal string[] LegendForTest => [.. _rowMap.Keys];
    internal string? CurrentForTest => _current.Length == 0 ? null : _current;
    internal string CenterForTest => _readouts.TryGetValue(_identity, out var readout) ? readout.Label.Text : "";
    internal string CenterValueForTest => _readouts.TryGetValue(_identity, out var readout) ? readout.Value.Text : "";
    internal (double Start, double End) ArcForTest(string key) => (_segments[key].Start.Position, _segments[key].End.Position);
    internal double LiftForTest(string key) => _segments[key].LiftBy.Position;
    internal bool BesideForTest => _beside;
    internal FrameworkElement RingForTest => _ring;
    internal string? HitForTest(Point point) => Hit(point).Key;
    internal void PreviewForTest(string? key) => SetPreview(key);
    internal void RowClickForTest(string key) => RowClick(key);
    internal void ShowForTest() => Show();
    internal bool RingKeyForTest(Key key) => RingKey(key);
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { _loop.Advance(Math.Min(left, .05)); } }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private string Format(double value) => FormatValue?.Invoke(value) ?? ChartMath.Grouped(value, 1);
    private string Suffix => string.IsNullOrEmpty(Unit) ? "" : $" {Unit}";
    private string[] HiddenList => [.. HiddenKeys ?? []];
    private double C => RingSize / 2;
    private double Outer => C - RingMargin;
    private double Inner => Math.Max(8, Outer - RingThickness);
    private double Scale => _ring.ActualWidth > 0 ? _ring.ActualWidth / RingSize : 1;

    private void UpdatePlacement(double width) => _beside = LegendPlacement == DonutLegendPlacement.Beside || (LegendPlacement == DonutLegendPlacement.Auto && width >= 460);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }
        _host.Child = _figure;
        Colorize();
        OnGeometry();
        OnData();
    }

    private void OnGeometry()
    {
        _ringBox.Width = _ringBox.Height = RingSize;
        _center.Width = Math.Max(0, Inner * 2 - 16);
        _ring.Focusable = !ShowLegend;
        _legendHost.Visibility = ShowLegend ? Visibility.Visible : Visibility.Collapsed;
        _figure.InvalidateMeasure();
        _ring.InvalidateVisual();
    }

    private Color SeriesColor(int index)
    {
        if (index < 4) { return Tween.Resolve(this, $"ArcWpf.Brush.Series{index + 1}"); }
        return OkColor.Mix(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), Tween.Resolve(this, "ArcWpf.Brush.Surface"), Neutral[(index - 4) % Neutral.Length]);
    }

    // Colors follow a key from the first time it is seen, so a segment keeps its color through every dataset.
    private Color ColorFor(DonutMath.Slice item)
    {
        if (item.Color is { } own) { return own; }
        if (item.Key == OtherKey) { return OkColor.Mix(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), Tween.Resolve(this, "ArcWpf.Brush.Surface"), .26); }
        return SeriesColor(_palette.GetValueOrDefault(item.Key));
    }

    private void OnData()
    {
        if (_host is null) { return; }
        foreach (var item in Data ?? []) { _palette.TryAdd(item.Key, _palette.Count); }
        _slices = DonutMath.Slices(Data ?? [], GroupBelow, MaxSegments, OtherLabel);
        var hidden = HiddenList.Where(key => _slices.Any(item => item.Key == key)).ToHashSet();
        _visible = [.. _slices.Where(item => !hidden.Contains(item.Key))];
        _total = _visible.Sum(item => item.Value);
        string signature = string.Join("|", _slices.Select(item => $"{item.Key}:{item.Value}{(hidden.Contains(item.Key) ? "h" : "")}"));

        // Leaving segments stay drawn in their old place while they close, then drop out once they have.
        foreach (var item in _slices) { if (!_order.Contains(item.Key)) { Insert(item.Key); } }

        Readouts(hidden);
        Rows(hidden);
        if (signature != _signature)
        {
            _signature = signature;
            Layout(hidden);
        }
        Refocus();
        _trackFade.Go(!_shown || _total == 0 ? 1 : 0, Moving ? Tween.Considered : 0, ArcEasing.Standard);
        _figure.InvalidateMeasure();
        Wake();
    }

    private void Insert(string key)
    {
        var slices = _slices.Select(item => item.Key).ToList();
        int at = slices.IndexOf(key);
        for (int back = at - 1; back >= 0; back--)
        {
            int place = _order.IndexOf(slices[back]);
            if (place >= 0) { _order.Insert(place + 1, key); return; }
        }
        _order.Insert(0, key);
    }

    // New data, or a segment shown or hidden, moves every arc from wherever it is on screen, keeping its velocity if it was already moving.
    private void Layout(HashSet<string> hidden)
    {
        if (!_shown) { return; }
        double total = _slices.Sum(item => hidden.Contains(item.Key) ? 0 : item.Value);
        if (total == 0) { total = 1; }
        _targets.Clear();
        double at = 0;
        foreach (var item in _slices)
        {
            double end = at + (hidden.Contains(item.Key) ? 0 : item.Value / total);
            _targets[item.Key] = (at, end);
            at = end;
        }
        bool first = _segments.Count == 0;
        for (int index = 0; index < _order.Count; index++)
        {
            string key = _order[index];
            bool leaving = !_targets.ContainsKey(key);
            double Place() { for (int back = index - 1; back >= 0; back--) { if (_targets.TryGetValue(_order[back], out var before)) { return before.End; } } return 0; }
            var to = _targets.TryGetValue(key, out var goal) ? goal : (Place(), Place());
            if (!_segments.TryGetValue(key, out var segment))
            {
                // A new segment opens from the edge of the one before it, as it is right now.
                double from = 0;
                if (!first) { for (int back = index - 1; back >= 0; back--) { if (_segments.TryGetValue(_order[back], out var prior)) { from = prior.End.Position; break; } } }
                segment = new Segment();
                segment.Start.Jump(from);
                segment.End.Jump(from);
                segment.Slice = _slices.First(item => item.Key == key);
                segment.Color.Jump(ColorFor(segment.Slice));
                _segments[key] = segment;
            }
            segment.Leaving = leaving;
            if (_slices.FirstOrDefault(item => item.Key == key) is { } slice) { segment.Slice = slice; }
            segment.EndTarget = to.Item2;
            if (!Moving)
            {
                segment.Start.Jump(to.Item1);
                segment.End.Jump(to.Item2);
                continue;
            }
            if (first)
            {
                // One sweep: every segment leaves the top together, and each trailing edge follows a beat behind the one before.
                segment.Start.To(to.Item1, Reveal);
                segment.EndDelay = .04 + index * .04;
                continue;
            }
            segment.EndDelay = 0;
            segment.Start.To(to.Item1, Settle);
            segment.End.To(to.Item2, Settle);
        }
        if (!Moving) { foreach (string key in _segments.Keys.Where(key => _segments[key].Leaving).ToList()) { Drop(key); } }
    }

    private void Drop(string key)
    {
        _segments.Remove(key);
        _order.Remove(key);
    }

    private void Show()
    {
        if (_shown) { return; }
        _shown = true;
        var hidden = HiddenList.Where(key => _slices.Any(item => item.Key == key)).ToHashSet();
        Layout(hidden);
        Refocus();
        _trackFade.Go(_total == 0 ? 1 : 0, Moving ? Tween.Considered : 0, ArcEasing.Standard);
        Wake();
    }

    private TrackedText CenterText(double size, double line, double tracking, FontWeight weight, Color color) => CopyMotion.Text("", size, line, tracking, weight, ChartMath.Solid(color));

    private void Readouts(HashSet<string> hidden)
    {
        var secondary = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        var muted = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        var ink = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        string[] keys = [TotalKey, .. _slices.Select(item => item.Key)];
        foreach (string key in _readouts.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            _center.Children.Remove(_readouts[key].View);
            if (_readouts[key].ValueCount is { } a) { _counts.Remove(a); }
            if (_readouts[key].ShareCount is { } b) { _counts.Remove(b); }
            _readouts.Remove(key);
        }
        foreach (string key in keys)
        {
            var slice = _slices.FirstOrDefault(item => item.Key == key);
            if (!_readouts.TryGetValue(key, out var readout))
            {
                var label = CenterText(12, 1.4, -.16, FontWeights.Normal, secondary);
                var value = CenterText(28, 1.1, -.84, FontWeights.Medium, ink);
                var meta = CenterText(12, 1.4, -.16, FontWeights.Normal, muted);
                label.HorizontalAlignment = value.HorizontalAlignment = meta.HorizontalAlignment = HorizontalAlignment.Center;
                value.Margin = new Thickness(0, 2, 0, 2);
                var move = new TranslateTransform(0, 7.2);
                var panel = new StackPanel { RenderTransform = move, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(label);
                panel.Children.Add(value);
                StackPanel? metaRow = null;
                TrackedText? shareText = null, tail = null;
                Count? shareCount = null;
                if (slice is null) { panel.Children.Add(meta); }
                else
                {
                    shareText = CenterText(12, 1.4, -.16, FontWeights.Normal, muted);
                    tail = CenterText(12, 1.4, -.16, FontWeights.Normal, muted);
                    metaRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    metaRow.Children.Add(shareText);
                    metaRow.Children.Add(tail);
                    panel.Children.Add(metaRow);
                    shareCount = new Count(shareText, DonutMath.Share, 0);
                    _counts.Add(shareCount);
                }
                var valueCount = new Count(value, Format, slice?.Value ?? _total);
                _counts.Add(valueCount);
                readout = new Readout(key) { View = panel, Label = label, Value = value, Meta = meta, Move = move, ValueCount = valueCount, ShareCount = shareCount, ShareText = shareText, MetaRow = metaRow, MetaTail = tail };
                _center.Children.Add(panel);
                _readouts[key] = readout;
            }
            if (slice is null)
            {
                readout.Label.Text = _total > 0 ? TotalLabel : EmptyLabel;
                readout.Meta.Text = string.IsNullOrEmpty(Unit) ? " " : Unit;
                readout.Value.Visibility = Visibility.Visible;
                readout.Meta.MinHeight = 16.8;
                Aim(readout.ValueCount!, _total, _total > 0);
                if (_total == 0) { readout.Value.Text = " "; }
            }
            else
            {
                readout.Label.Text = slice.Label;
                readout.MetaTail!.Text = $" of {TotalLabel.ToLowerInvariant()}";
                Aim(readout.ValueCount!, slice.Value, true);
                Aim(readout.ShareCount!, _total > 0 && !hidden.Contains(key) ? slice.Value / _total : 0, true);
            }
            readout.ValueCount!.Format = Format;
        }
    }

    private void Aim(Count count, double value, bool write)
    {
        count.Target = value;
        count.Spring.RestDelta = Math.Max(Math.Abs(value - count.Spring.Position) * .001, 1e-6);
        count.Spring.RestSpeed = count.Spring.RestDelta * 2;
        if (!Moving) { count.Spring.Jump(value); }
        else if (count.Spring.Target != value) { count.Spring.To(value, CountTune); }
        if (write) { count.Write(); }
    }

    private void Rows(HashSet<string> hidden)
    {
        string[] keys = [.. _slices.Select(item => item.Key)];
        foreach (var (key, row) in _rowMap.Where(pair => !keys.Contains(pair.Key)).ToList())
        {
            _rowMap.Remove(key);
            _counts.Remove(row.ValueCount);
            _counts.Remove(row.ShareCount);
            row.Holder.Leaving = true;
            row.Button.IsHitTestVisible = false;
            if (Moving) { row.Holder.Share.To(0, SpringTune.Smooth); }
            else { _rows.Children.Remove(row.Holder); }
        }
        for (int index = 0; index < _slices.Length; index++)
        {
            var slice = _slices[index];
            if (!_rowMap.TryGetValue(slice.Key, out var row))
            {
                row = MakeRow(slice);
                _rowMap[slice.Key] = row;
                _rows.Children.Add(row.Holder);
                if (Moving && _rowMap.Count > 1 && _started) { row.Holder.Share.To(1, SpringTune.Smooth); }
                else { row.Holder.Share.Jump(1); }
            }
            // Keep the rows in slice order, leaving ones wherever they were.
            int visual = _rows.Children.IndexOf(row.Holder);
            int wanted = index;
            for (int scan = 0; scan <= wanted && scan < _rows.Children.Count; scan++) { if (_rows.Children[scan] is Presence { Leaving: true }) { wanted++; } }
            if (visual != wanted && wanted < _rows.Children.Count) { _rows.Children.RemoveAt(visual); _rows.Children.Insert(wanted, row.Holder); }
            bool off = hidden.Contains(slice.Key);
            row.Label.Text = slice.Label;
            string members = MembersText(slice);
            row.Members.Text = members;
            row.Members.Visibility = members.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            row.ValueCount.Format = Format;
            Aim(row.ValueCount, slice.Value, true);
            Aim(row.ShareCount, off || _total == 0 ? 0 : slice.Value / _total, true);
            Dress(row, slice, off);
        }
        _started = true;
    }

    private static string MembersText(DonutMath.Slice slice)
    {
        if (slice.Members is not { } members) { return ""; }
        return members.Length > 2 ? $"{string.Join(", ", members.Take(2).Select(member => member.Label))} and {members.Length - 2} more" : string.Join(" and ", members.Select(member => member.Label));
    }

    private string Describe(DonutMath.Slice item) => $"{item.Label}, {Format(item.Value)}{Suffix}, {DonutMath.Share(item.Value / (_total > 0 ? _total : 1))}{(item.Members is { } members ? $". Includes {string.Join(", ", members.Select(member => member.Label))}" : "")}";

    private Row MakeRow(DonutMath.Slice slice)
    {
        var fill = new SolidColorBrush(Colors.Transparent);
        var stroke = new SolidColorBrush(Colors.Transparent);
        var wash = new SolidColorBrush(Colors.Transparent);
        var labelBrush = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.Foreground"));
        var valueBrush = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.TextSecondary"));
        var shareBrush = new SolidColorBrush(labelBrush.Color);
        var dot = new Ellipse { Width = 10, Height = 10, Fill = fill, Stroke = stroke, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center };
        var label = CopyMotion.Text(slice.Label, 14, 1.4, -.16, FontWeights.Normal, labelBrush);
        var members = new TextBlock { FontSize = 12, LineHeight = 16.8, TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false };
        members.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(label);
        text.Children.Add(members);
        var value = CopyMotion.Text("", 14, 1.4, -.16, FontWeights.Normal, valueBrush);
        var share = CopyMotion.Text("", 14, 1.4, -.16, FontWeights.Medium, shareBrush);
        value.HorizontalAlignment = share.HorizontalAlignment = HorizontalAlignment.Right;
        value.VerticalAlignment = share.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45.5) });
        text.Margin = new Thickness(10, 0, 10, 0);
        share.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(value, 2);
        Grid.SetColumn(share, 3);
        grid.Children.Add(dot);
        grid.Children.Add(text);
        grid.Children.Add(value);
        grid.Children.Add(share);
        var press = new ScaleTransform();
        var button = new Border { MinHeight = 40, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 2), CornerRadius = new CornerRadius(12), Background = wash, Child = grid, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null, RenderTransform = press, RenderTransformOrigin = new Point(.5, .5) };
        var holder = new Presence { Child = button };
        Row row = null!;
        var ring = new FocusVisible(button, () => { button.BorderBrush = row.FocusRing.Shown ? FocusVisible.RingBrush(this) : null; button.BorderThickness = new Thickness(row.FocusRing.Shown ? 2 : 0); button.Padding = row.FocusRing.Shown ? new Thickness(8, 4, 8, 4) : new Thickness(10, 6, 10, 6); if (row.FocusRing.Shown) { SetPreview(IsOff(row.Key) ? null : row.Key); } });
        row = new Row { Key = slice.Key, Button = button, Press = press, Dot = dot, DotFill = fill, DotStroke = stroke, Wash = wash, LabelBrush = labelBrush, ValueBrush = valueBrush, ShareBrush = shareBrush, Label = label, Members = members, ValueCount = new Count(value, Format, slice.Value), ShareCount = new Count(share, DonutMath.Share, 0), FocusRing = ring, Holder = holder };
        _counts.Add(row.ValueCount);
        _counts.Add(row.ShareCount);
        string key = slice.Key;
        button.MouseEnter += (_, e) => { if (e.StylusDevice is null) { SetPreview(IsOff(key) ? null : key); } RowHover(key); };
        button.MouseLeave += (_, _) => { Tween.Press(press, false, .985, Moving); RowHover(key); };
        button.MouseLeftButtonDown += (_, e) => { row.FocusRing.Pointer(); button.Focus(); button.CaptureMouse(); Tween.Press(press, true, .985, Moving, Tween.Instant); e.Handled = true; };
        button.MouseLeftButtonUp += (_, e) =>
        {
            bool over = new Rect(button.RenderSize).Contains(e.GetPosition(button));
            button.ReleaseMouseCapture();
            Tween.Press(press, false, .985, Moving);
            if (over) { RowClick(key); }
            e.Handled = true;
        };
        button.LostKeyboardFocus += (_, e) => { if (e.NewFocus is not DependencyObject target || !IsRow(target)) { SetPreview(null); } };
        button.KeyDown += (_, e) => { if (RowKey(key, e.Key)) { e.Handled = true; row.FocusRing.Key(); } };
        return row;
    }

    private bool IsRow(DependencyObject target) => _rowMap.Values.Any(row => ReferenceEquals(row.Button, target));
    private bool IsOff(string key) => HiddenList.Contains(key);

    private void RowHover(string key) { if (_rowMap.TryGetValue(key, out var row) && _slices.FirstOrDefault(item => item.Key == key) is { } slice) { Dress(row, slice, IsOff(key)); } }

    private void Dress(Row row, DonutMath.Slice slice, bool off)
    {
        bool cross = Moving;
        var color = ColorFor(slice);
        Tween.Color(row.DotFill, off ? OkColor.Alpha(color, 0) : color, Tween.Standard, cross);
        Tween.Color(row.DotStroke, color, Tween.Standard, cross);
        var ink = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        var muted = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        var secondary = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        Tween.Color(row.LabelBrush, off ? muted : ink, Tween.Standard, cross);
        Tween.Color(row.ValueBrush, off ? muted : secondary, Tween.Standard, cross);
        Tween.Color(row.ShareBrush, off ? muted : ink, Tween.Standard, cross);
        bool lit = row.Button.IsMouseOver || row.Key == _current || ActiveKey == row.Key;
        Tween.Color(row.Wash, lit ? OkColor.Alpha(ink, .05) : OkColor.Alpha(ink, 0), Tween.Fast, cross);
        AutomationProperties.SetName(row.Button, off ? $"{slice.Label}, hidden" : Describe(slice));
    }

    private void RowClick(string key)
    {
        if (LegendAction == DonutLegendAction.Toggle) { ToggleHidden(key); }
        else { Select(ActiveKey == key ? null : key); }
    }

    private void ToggleHidden(string key)
    {
        string[] list = HiddenList;
        bool isHidden = list.Contains(key);
        if (!isHidden && _visible.Length <= 1) { return; }
        string[] next = isHidden ? [.. list.Where(item => item != key)] : [.. list, key];
        SetCurrentValue(HiddenKeysProperty, next);
        var item = _slices.FirstOrDefault(slice => slice.Key == key);
        double nextTotal = _slices.Sum(slice => next.Contains(slice.Key) ? 0 : slice.Value);
        if (item is not null) { Announcer.Say(this, $"{item.Label} {(isHidden ? "shown" : "hidden")}. {TotalLabel} {Format(nextTotal)}{Suffix}."); }
    }

    private void Select(string? key) => SetCurrentValue(ActiveKeyProperty, key);
    private void OnActive() { Refocus(); foreach (var slice in _slices) { RowHover(slice.Key); } }
    private void SetPreview(string? key)
    {
        if (_preview == key) { return; }
        _preview = key;
        Refocus();
        foreach (var slice in _slices) { RowHover(slice.Key); }
    }

    // The active segment slides out along its middle and thickens a little; the one it replaces settles back at the same time. The rest dim, and the center rolls like a drum to the one that is active.
    private void Refocus()
    {
        string current = new[] { _preview, ActiveKey }.FirstOrDefault(key => key is not null && _visible.Any(item => item.Key == key)) ?? "";
        _current = current;
        _ring.Cursor = current.Length > 0 ? Cursors.Hand : null;
        foreach (var (key, segment) in _segments)
        {
            double to = key == current ? 1 : 0;
            if (!Moving) { segment.LiftBy.Jump(to); }
            else if (segment.LiftBy.Target != to || segment.LiftBy.Active) { segment.LiftBy.To(to, Pop); }
            segment.Dim.Go(current.Length > 0 && key != current ? .45 : 1, Moving ? Tween.Standard : 0, ArcEasing.Standard);
        }
        string identity = current.Length > 0 ? current : TotalKey;
        _identity = identity;
        string[] order = [TotalKey, .. _slices.Select(item => item.Key)];
        int active = Math.Max(0, Array.IndexOf(order, identity));
        for (int index = 0; index < order.Length; index++)
        {
            if (!_readouts.TryGetValue(order[index], out var readout)) { continue; }
            int place = index - active;
            double opacity = place == 0 ? 1 : 0, y = 7.2 * Math.Sign(place);
            double seconds = !Moving ? 0 : place == 0 ? Tween.Standard : Tween.Fast;
            var ease = place == 0 ? ArcEasing.Enter : ArcEasing.Standard;
            if (seconds == 0) { readout.View.BeginAnimation(OpacityProperty, null); readout.Move.BeginAnimation(TranslateTransform.YProperty, null); readout.View.Opacity = opacity; readout.Move.Y = y; continue; }
            Tween.To(readout.View, OpacityProperty, opacity, seconds, ease);
            Tween.To(readout.Move, TranslateTransform.YProperty, y, seconds, ease);
        }
        Wake();
    }

    // One pointer handler for the whole ring: the angle picks the segment, so crossing a gap never drops the hover.
    private (bool Known, string? Key) Hit(Point point)
    {
        double scale = Scale;
        if (scale <= 0) { return (false, null); }
        double x = point.X / scale - C, y = point.Y / scale - C, radius = Math.Sqrt(x * x + y * y);
        if (radius < Inner - 6 || radius > C) { return (true, null); }
        double turn = (Math.Atan2(y, x) + Math.PI / 2) / Tau;
        if (turn < 0) { turn += 1; }
        foreach (var (key, arc) in _targets)
        {
            if (arc.End > arc.Start && turn >= arc.Start && turn < arc.End && _visible.Any(item => item.Key == key)) { return (true, key); }
        }
        return (false, null);
    }

    // Arrow keys walk the segments, in the legend or on the ring itself when there is no legend.
    private static int? Step(Key key, int index, int count) => key switch
    {
        Key.Down or Key.Right => index + 1,
        Key.Up or Key.Left => index - 1,
        Key.Home => 0,
        Key.End => count - 1,
        _ => null,
    };

    private bool RowKey(string key, Key pressed)
    {
        if (pressed == Key.Escape && ActiveKey is not null) { Select(null); return true; }
        if (pressed is Key.Enter or Key.Space) { RowClick(key); return true; }
        int index = Array.FindIndex(_slices, item => item.Key == key);
        if (Step(pressed, index, _slices.Length) is not { } next) { return false; }
        string target = _slices[(next + _slices.Length) % _slices.Length].Key;
        _rowMap[target].Button.Focus();
        return true;
    }

    private bool RingKey(Key pressed)
    {
        if (_visible.Length == 0) { return false; }
        if (pressed == Key.Escape) { SetPreview(null); if (ActiveKey is not null) { Select(null); } return true; }
        if (pressed is Key.Enter or Key.Space && _current.Length > 0) { Select(ActiveKey == _current ? null : _current); return true; }
        int index = Array.FindIndex(_visible, item => item.Key == _current);
        int from = index < 0 ? (pressed is Key.Up or Key.Left ? 0 : -1) : index;
        if (Step(pressed, from, _visible.Length) is not { } next) { return false; }
        var item = _visible[(next + _visible.Length) % _visible.Length];
        SetPreview(item.Key);
        Announcer.Say(this, Describe(item));
        return true;
    }

    private void Colorize()
    {
        _track = Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted");
        foreach (var (key, segment) in _segments) { if (_slices.FirstOrDefault(item => item.Key == key) is { } slice) { segment.Color.Jump(ColorFor(slice)); } else { segment.Color.Jump(ColorFor(segment.Slice)); } }
        var secondary = ChartMath.Solid(Tween.Resolve(this, "ArcWpf.Brush.TextSecondary"));
        var muted = ChartMath.Solid(Tween.Resolve(this, "ArcWpf.Brush.TextMuted"));
        var ink = ChartMath.Solid(Tween.Resolve(this, "ArcWpf.Brush.Foreground"));
        foreach (var readout in _readouts.Values)
        {
            readout.Label.SetValue(TrackedText.ForegroundProperty, secondary);
            readout.Value.SetValue(TrackedText.ForegroundProperty, ink);
            readout.Meta.SetValue(TrackedText.ForegroundProperty, muted);
            readout.ShareText?.SetValue(TrackedText.ForegroundProperty, muted);
            readout.MetaTail?.SetValue(TrackedText.ForegroundProperty, muted);
        }
        foreach (var slice in _slices) { RowHover(slice.Key); }
        _ring.InvalidateVisual();
    }

    private void Wake() { if (Moving) { _loop.Start(); } else { foreach (var count in _counts) { count.Spring.Jump(count.Target); count.Write(); } foreach (var row in _rows.Children.OfType<Presence>().Where(row => row.Leaving).ToList()) { _rows.Children.Remove(row); } _ring.InvalidateVisual(); } }

    private bool Frame(double seconds)
    {
        bool busy = false;
        foreach (var (key, segment) in _segments.ToList())
        {
            segment.Start.Step(seconds);
            if (segment.EndDelay > 0)
            {
                segment.EndDelay -= seconds;
                if (segment.EndDelay <= 0) { segment.EndDelay = 0; segment.End.To(segment.EndTarget, Reveal); }
                busy = true;
            }
            segment.End.Step(seconds);
            segment.LiftBy.Step(seconds);
            segment.Dim.Step(seconds);
            segment.Color.Step(seconds);
            busy |= segment.Start.Active || segment.End.Active || segment.LiftBy.Active || segment.Dim.Active || segment.Color.Active;
            if (segment.Leaving && !segment.End.Active && !segment.Start.Active) { Drop(key); }
        }
        foreach (var count in _counts)
        {
            if (!count.Spring.Active) { continue; }
            count.Spring.Step(seconds);
            count.Write();
            busy = true;
        }
        foreach (var row in _rows.Children.OfType<Presence>().ToList())
        {
            if (!row.Share.Active) { continue; }
            row.Share.Step(seconds);
            row.Opacity = Math.Clamp(row.Share.Position, 0, 1);
            row.InvalidateMeasure();
            busy = true;
            if (row.Leaving && !row.Share.Active) { _rows.Children.Remove(row); }
        }
        _trackFade.Step(seconds);
        busy |= _trackFade.Active;
        _ring.InvalidateVisual();
        return busy;
    }

    private void Paint(DrawingContext dc, Size size)
    {
        double scale = size.Width / RingSize;
        if (scale <= 0) { return; }
        dc.PushTransform(new ScaleTransform(scale, scale));
        double c = C, outer = Outer, inner = Inner, corner = Math.Min(Corner, RingThickness / 4);
        // The track shows only before the first sweep and when there is nothing to draw, so the gaps between segments stay the page colour.
        if (_trackFade.Value > 0)
        {
            var pen = new Pen(ChartMath.Solid(ChartMath.Faded(_track, _trackFade.Value)), outer - inner);
            pen.Freeze();
            dc.DrawEllipse(null, pen, new Point(c, c), (outer + inner) / 2, (outer + inner) / 2);
        }
        foreach (string key in _order)
        {
            if (!_segments.TryGetValue(key, out var segment)) { continue; }
            double start = segment.Start.Position, end = segment.End.Position, lift = Math.Max(0, segment.LiftBy.Position), R = outer + lift * Thicken;
            var geometry = DonutMath.Sector(c, R, inner - lift * Thicken * .5, start, end, SegmentGap, corner);
            if (geometry is null) { continue; }
            double fill = Math.Clamp((DonutMath.Breadth(R, start, end, SegmentGap) - 1) / 3, 0, 1);
            double middle = (start + end) / 2 * Tau - Math.PI / 2, distance = lift * Lift;
            dc.PushTransform(new TranslateTransform(Math.Cos(middle) * distance, Math.Sin(middle) * distance));
            dc.DrawGeometry(ChartMath.Solid(ChartMath.Faded(segment.Color.Value, fill * segment.Dim.Value)), null, geometry);
            dc.Pop();
        }
        dc.Pop();
    }

    private sealed class DonutRingPeer(FrameworkElement owner, DonutChart chart) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() { string own = base.GetNameCore(); return string.IsNullOrEmpty(own) ? $"{chart.Label}. Use the arrow keys to read each segment." : own; }
        protected override string GetItemStatusCore() => chart._total > 0 ? $"{chart.TotalLabel} {chart.Format(chart._total)}{chart.Suffix}. {string.Join(". ", chart._visible.Select(chart.Describe))}." : chart.EmptyLabel;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
