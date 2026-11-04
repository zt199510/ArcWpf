using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>One bar. A bar whose key survives a data change keeps its place, so switching ranges morphs instead of redrawing.</summary>
public sealed record BarChartDatum(string Key, string Label, double Value, string? AxisLabel = null);

// bar-chart.tsx (frozen 2026-11-04): one measure across a run of days, scrubbed for each value, with a rolling headline and an average line.
public class BarChart : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(IReadOnlyList<BarChartDatum>), typeof(BarChart), new FrameworkPropertyMetadata(null, (d, _) => ((BarChart)d).OnData()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("", (d, _) => ((BarChart)d).Headline()));
    public static readonly DependencyProperty PeriodProperty = DependencyProperty.Register(nameof(Period), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("", (d, _) => ((BarChart)d).Headline()));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("", (d, _) => ((BarChart)d).Headline()));
    public static readonly DependencyProperty AverageLabelProperty = DependencyProperty.Register(nameof(AverageLabel), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("Daily average", (d, _) => ((BarChart)d).Headline()));
    public static readonly DependencyProperty ValueLabelProperty = DependencyProperty.Register(nameof(ValueLabel), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("Total", (d, _) => ((BarChart)d).Headline()));
    public static readonly DependencyProperty CategoryLabelProperty = DependencyProperty.Register(nameof(CategoryLabel), typeof(string), typeof(BarChart), new FrameworkPropertyMetadata("Day"));
    public static readonly DependencyProperty ShowAverageProperty = DependencyProperty.Register(nameof(ShowAverage), typeof(bool), typeof(BarChart), new FrameworkPropertyMetadata(true, (d, _) => ((BarChart)d).OnShowAverage()));
    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(nameof(PlotHeight), typeof(double), typeof(BarChart), new FrameworkPropertyMetadata(176d, (d, _) => ((BarChart)d).OnPlotHeight()));
    public static readonly DependencyProperty FormatValueProperty = DependencyProperty.Register(nameof(FormatValue), typeof(Func<double, string>), typeof(BarChart), new FrameworkPropertyMetadata(null, (d, _) => ((BarChart)d).OnFormat()));

    /// <summary>The bars, oldest first.</summary>
    public IReadOnlyList<BarChartDatum>? Data { get => (IReadOnlyList<BarChartDatum>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>What is measured. Names the chart for assistive technology.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The range on show. It rests under the headline.</summary>
    public string Period { get => (string)GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    /// <summary>Unit after each value, such as "min".</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Headline label at rest, above the average.</summary>
    public string AverageLabel { get => (string)GetValue(AverageLabelProperty); set => SetValue(AverageLabelProperty, value); }
    /// <summary>Headline label while a bar is scrubbed.</summary>
    public string ValueLabel { get => (string)GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }
    /// <summary>What one bar is, for the slider's spoken name.</summary>
    public string CategoryLabel { get => (string)GetValue(CategoryLabelProperty); set => SetValue(CategoryLabelProperty, value); }
    /// <summary>Draws the average as a reference line that springs to each new range.</summary>
    public bool ShowAverage { get => (bool)GetValue(ShowAverageProperty); set => SetValue(ShowAverageProperty, value); }
    /// <summary>Plot height in pixels.</summary>
    public double PlotHeight { get => (double)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    /// <summary>Formats values. Defaults to a grouped whole number.</summary>
    public Func<double, string>? FormatValue { get => (Func<double, string>?)GetValue(FormatValueProperty); set => SetValue(FormatValueProperty, value); }

    /// <summary>Room above the top gridline, the share of each slot a bar fills, its widest size, and the radius of its data end.</summary>
    private const double Top = 12, Fill = .58, MaxBar = 28, Radius = 4;
    private static readonly SpringTune Settle = SpringTune.Smooth, Grow = SpringTune.Morph, Glide = SpringTune.Snappy, Collapse = SpringTune.Physical(Tween.Standard);

    private sealed class Bar(string key, double place)
    {
        public string Key { get; } = key;
        public GestureSpring Place { get; } = new(place);
        public GestureSpring Amount { get; } = new(0);
        public TimedColor Color { get; } = new(Colors.Transparent);
        public double Value, Delay;
        public bool Grown, Leaving, Waiting;
    }

    private readonly ChartSurface _plot, _scrubber;
    private readonly Canvas _gutter = new() { IsHitTestVisible = false }, _axis = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly FrameLoop _loop;
    private readonly FocusVisible _focus;
    private readonly InViewOnce _inView;
    private readonly List<Bar> _bars = [];
    private readonly List<ChartRow> _rows = [];
    private readonly List<ChartAxisLabel> _labels = [];
    private readonly GestureSpring _unit = new(1, .00001, .00002), _scale = new(1), _mean = new(0), _cursor = new(0);
    private readonly Timed _cursorFade = new(0), _meanFade = new(0);
    private readonly SolidColorBrush _ink = new(), _secondary = new(), _muted = new();
    private Decorator? _host;
    private Grid? _chart;
    private RollText? _kind, _value, _when;
    private TrackedText? _unitText, _meanLabel;
    private FrameworkElement? _meanHost;
    private Color _rest, _dim, _accent, _gridColor, _strongColor, _meanColor;
    private BarChartDatum[] _data = [];
    private string _signature = "";
    private int? _active, _previousIndex;
    private double _top = 1, _shownValue, _meanDelay;
    private int _valueWay = 1, _indexWay = 1, _changes;
    private bool _meanStarted, _scrubbedOnce, _shown;

    static BarChart() => DefaultStyleKeyProperty.OverrideMetadata(typeof(BarChart), new FrameworkPropertyMetadata(typeof(BarChart)));

    public BarChart()
    {
        _plot = new ChartSurface(Paint) { ClipToBounds = true, IsHitTestVisible = false };
        _scrubber = new ChartSurface((_, _) => { }, surface => new ArcRangePeer(surface, AutomationControlType.Slider, () => $"{Label}, explore by {CategoryLabel.ToLowerInvariant()}", () => 1, () => Math.Max(1, _data.Length), () => (Index ?? Last) + 1, Spoken)) { Focusable = true, FocusVisualStyle = null };
        _loop = new FrameLoop(Frame);
        _focus = new FocusVisible(_scrubber, () => { if (_focus is { Shown: true } && Last >= 0 && _active is null) { SetActive(Last); } });
        _inView = new InViewOnce(this, () => Show());
        _scrubber.MouseDown += (_, e) => { _focus.Pointer(); _scrubber.Focus(); if (e.StylusDevice is null) { SetActive(PointAt(e.GetPosition(_plot).X)); } };
        _scrubber.MouseMove += (_, e) => { if (e.StylusDevice is null) { SetActive(PointAt(e.GetPosition(_plot).X)); } };
        _scrubber.MouseLeave += (_, e) => { if (e.StylusDevice is null) { SetActive(null); } };
        _scrubber.TouchDown += (_, e) => { _scrubber.CaptureTouch(e.TouchDevice); SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; };
        _scrubber.TouchMove += (_, e) => { SetActive(PointAt(e.GetTouchPoint(_plot).Position.X)); e.Handled = true; };
        _scrubber.TouchUp += (_, e) => { _scrubber.ReleaseTouchCapture(e.TouchDevice); SetActive(null); e.Handled = true; };
        _scrubber.KeyDown += (_, e) => KeyScrub(e);
        _scrubber.LostKeyboardFocus += (_, _) => SetActive(null);
        _plot.SizeChanged += (_, _) => { FitLabels(false); Place(); _plot.InvalidateVisual(); };
        Loaded += (_, _) => { Colorize(false); if (!Moving) { Show(); } };
        Unloaded += (_, _) => _loop.Stop();
        ThemeWatch.Add(this, () => Colorize(false));
    }

    internal int? ActiveForTest => _active;
    internal int BarCountForTest => _bars.Count(bar => !bar.Leaving);
    internal int LeavingForTest => _bars.Count(bar => bar.Leaving);
    internal double HeightOfForTest(string key) => _bars.First(bar => bar.Key == key).Amount.Position;
    internal double PlaceOfForTest(string key) => _bars.First(bar => bar.Key == key).Place.Position;
    internal double[] TicksForTest => [.. _rows.Where(row => !row.Leaving).Select(row => row.Value)];
    internal string[] AxisForTest => [.. _labels.Where(label => !label.Leaving).Select(label => label.View.Text)];
    internal double ScaleForTest => _scale.Position;
    internal double MeanForTest => _mean.Position;
    internal double CursorForTest => _cursor.Position;
    internal double UnitForTest => _unit.Position;
    internal string HeadlineForTest => _value?.TextForTest ?? "";
    internal string KindForTest => _kind?.TextForTest ?? "";
    internal string WhenForTest => _when?.TextForTest ?? "";
    internal string SpokenForTest => Spoken();
    internal FrameworkElement ScrubberForTest => _scrubber;
    internal FrameworkElement PlotForTest => _plot;
    internal void ShowForTest() => Show();
    internal void ScrubForTest(double x) => SetActive(PointAt(x));
    internal void LeaveForTest() => SetActive(null);
    internal void KeyForTest(Key key) => Press(key);
    internal void AdvanceForTest(double seconds) { for (double left = seconds; left > 0; left -= .05) { _loop.Advance(Math.Min(left, .05)); } }

    private bool Moving => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private int Count => Math.Max(_data.Length, 1);
    private int Last => _data.Length - 1;
    private int? Index => _active is { } at && Last >= 0 ? Math.Min(at, Last) : null;
    private double Average => _data.Length > 0 ? _data.Average(item => item.Value) : 0;
    private string Format(double value) => FormatValue?.Invoke(value) ?? ChartMath.Grouped(value);
    private string Suffix => string.IsNullOrEmpty(Unit) ? "" : $" {Unit}";
    private double H => PlotHeight;

    private string Spoken()
    {
        var reading = Index is { } at ? _data[at] : _data.LastOrDefault();
        return reading is null ? "No data" : $"{reading.Label}: {Format(reading.Value)}{Suffix}";
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        if (_host is null) { return; }
        _kind = new RollText(14, 1.4, -.16, FontWeights.Normal, whole: true) { HorizontalAlignment = HorizontalAlignment.Left };
        _value = new RollText(36, 1.1, -1.08, FontWeights.Medium, morph: true);
        _when = new RollText(14, 1.4, -.16, FontWeights.Normal, whole: true) { HorizontalAlignment = HorizontalAlignment.Left };
        _unitText = CopyMotion.Text("", 16, 1.1, -.16, FontWeights.Normal, _secondary);
        _unitText.Margin = new Thickness(6, 18, 0, 0);
        var valueRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2), Height = 39.6, IsHitTestVisible = false };
        valueRow.Children.Add(_value);
        valueRow.Children.Add(_unitText);
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20), IsHitTestVisible = false };
        header.Children.Add(_kind);
        header.Children.Add(valueRow);
        header.Children.Add(_when);

        _chart = new Grid();
        _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(H) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        Grid.SetColumn(_gutter, 1);
        Grid.SetRow(_axis, 1);
        Grid.SetColumnSpan(_scrubber, 2);
        Grid.SetRowSpan(_scrubber, 2);
        _chart.Children.Add(_plot);
        _chart.Children.Add(_gutter);
        _chart.Children.Add(_axis);
        _chart.Children.Add(_scrubber);
        _meanLabel = CopyMotion.Text("", 12, 1, -.16, FontWeights.Medium, _ink);
        _meanHost = _meanLabel;
        _gutter.Children.Add(_meanLabel);

        var figure = new StackPanel();
        figure.Children.Add(header);
        figure.Children.Add(_chart);
        _host.Child = figure;
        Colorize(false);
        OnData();
    }

    private void OnPlotHeight()
    {
        if (_chart is not null) { _chart.RowDefinitions[0].Height = new GridLength(H); }
        Place();
        _plot.InvalidateVisual();
    }

    private void OnFormat()
    {
        foreach (var row in _rows) { row.View.Text = Format(row.Value); }
        Headline();
        Place();
    }

    private void OnShowAverage()
    {
        _meanFade.Jump(ShowAverage && _shown ? 1 : 0);
        Place();
        _plot.InvalidateVisual();
    }

    // New bars grow outward from the ones that stayed, a new range zooms around the latest bar, and the scale and average spring to fit.
    private void OnData()
    {
        _data = [.. Data ?? []];
        string signature = string.Join("|", _data.Select(item => item.Key));
        bool newShape = signature != _signature, first = _bars.Count == 0 && _signature.Length == 0;
        if (newShape && !first) { _changes++; _active = null; }
        _signature = signature;
        int last = Last;
        var incoming = _data.Select((item, index) => (item.Key, Place: last - index)).Where(item => _bars.All(bar => bar.Key != item.Key || bar.Leaving)).OrderBy(item => item.Place).ToList();
        double freshStep = Math.Min(.04, .32 / Math.Max(1, incoming.Count)), revealStep = Math.Min(.04, .36 / Count);
        var fresh = incoming.Select((item, rank) => (item.Key, Delay: rank * freshStep)).ToDictionary(item => item.Key, item => item.Delay);
        foreach (var gone in _bars.Where(bar => _data.All(item => item.Key != bar.Key))) { Leave(gone); }
        for (int at = 0; at < _data.Length; at++)
        {
            var item = _data[at];
            int place = last - at;
            var bar = _bars.FirstOrDefault(entry => entry.Key == item.Key);
            if (bar is null)
            {
                bar = new Bar(item.Key, place) { Delay = first ? at * revealStep : fresh.GetValueOrDefault(item.Key, at * revealStep) };
                bar.Color.Jump(_rest);
                _bars.Add(bar);
            }
            bar.Leaving = false;
            bar.Value = item.Value;
            Aim(bar.Place, place, Settle);
            Grower(bar);
        }

        var scale = ChartMath.ScaleFor(_data.Length > 0 ? _data.Max(item => item.Value) : 0);
        _top = scale.Max;
        if (first) { _scale.Jump(scale.Max); _unit.Jump(1d / Count); }
        else { Aim(_scale, scale.Max, Settle); Aim(_unit, 1d / Count, Settle); }
        Rows(scale.Ticks, first);
        Mean();
        FitLabels(!first);
        Paints();
        Headline();
        if (!first && newShape) { Announcer.Say(this, $"{Period}. {AverageLabel} {Format(Math.Round(Average, MidpointRounding.AwayFromZero))}{Suffix}."); }
        Wake();
    }

    private void Aim(GestureSpring spring, double to, SpringTune tune)
    {
        if (!Moving) { spring.Jump(to); return; }
        if (spring.Target != to || spring.Active) { spring.To(to, tune); }
    }

    private void Grower(Bar bar)
    {
        if (!Moving) { if (_shown) { bar.Amount.Jump(bar.Value); bar.Grown = true; } return; }
        if (!_shown) { return; }
        if (!bar.Grown) { bar.Grown = true; bar.Waiting = true; return; }
        bar.Waiting = false;
        if (bar.Amount.Target != bar.Value || bar.Amount.Active) { bar.Amount.To(bar.Value, Settle); }
    }

    private void Leave(Bar bar)
    {
        if (bar.Leaving) { return; }
        bar.Leaving = true;
        bar.Waiting = false;
        if (!Moving) { bar.Amount.Jump(0); return; }
        bar.Amount.To(0, Collapse);
    }

    private void Rows(double[] ticks, bool first)
    {
        foreach (var row in _rows.Where(row => !row.Leaving && !ticks.Contains(row.Value)))
        {
            row.Leaving = true;
            row.Presence.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        foreach (double value in ticks)
        {
            if (_rows.Find(entry => entry.Value == value) is { } kept)
            {
                kept.Leaving = false;
                kept.Presence.Go(1, Moving ? Tween.Standard : 0, ArcEasing.Standard);
                continue;
            }
            var view = CopyMotion.Text(Format(value), 12, 1, -.16, FontWeights.Normal, _muted);
            _gutter.Children.Insert(0, view);
            var row = new ChartRow(value, view);
            if (first || !Moving) { row.Presence.Jump(1); } else { row.Presence.Go(1, Tween.Standard, ArcEasing.Standard); }
            _rows.Add(row);
        }
    }

    // The average rises with the first bars, then springs to each new range.
    private void Mean()
    {
        if (!Moving) { if (_shown) { _mean.Jump(Average); } return; }
        if (!_shown) { return; }
        if (!_meanStarted) { _meanStarted = true; _meanDelay = .12; return; }
        if (_mean.Target != Average || _mean.Active) { _mean.To(Average, Settle); }
    }

    private void Show()
    {
        if (_shown) { return; }
        _shown = true;
        foreach (var bar in _bars.Where(bar => !bar.Leaving)) { Grower(bar); }
        Mean();
        if (Moving && ShowAverage) { _meanFade.Go(1, Tween.Standard, ArcEasing.Standard, .12); } else { _meanFade.Jump(ShowAverage ? 1 : 0); }
        Wake();
        Place();
        _plot.InvalidateVisual();
    }

    // Axis labels that fit once the range has settled: every label, else every second, third, and so on, counted back from the latest so the newest date always shows and spacing stays even.
    private void FitLabels(bool animate)
    {
        double width = _plot.ActualWidth;
        if (width <= 0) { return; }
        var measured = new List<(BarChartDatum Item, double Size, double Natural, double Left, int Place)>();
        for (int index = _data.Length - 1; index >= 0; index--)
        {
            var item = _data[index];
            if (string.IsNullOrEmpty(item.AxisLabel)) { continue; }
            var probe = CopyMotion.Text(item.AxisLabel, 12, 1, -.16, FontWeights.Normal, _muted);
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double size = probe.DesiredSize.Width, natural = width - (_data.Length - 1 - index + .5) * (width / Count) - size / 2;
            measured.Add((item, size, natural, Math.Clamp(natural, 0, Math.Max(0, width - size)), _data.Length - 1 - index));
        }
        var picks = new List<(string Key, string Text, double Shift, int Place)>();
        for (int stride = 1; stride <= measured.Count; stride++)
        {
            var picked = measured.Where((_, rank) => rank % stride == 0).ToList();
            bool fits = true;
            for (int rank = 1; rank < picked.Count && fits; rank++) { fits = picked[rank].Left + picked[rank].Size + 10 <= picked[rank - 1].Left; }
            if (stride != measured.Count && !fits) { continue; }
            picks = [.. picked.Select(label => ($"{label.Item.Key}:{label.Item.AxisLabel}", label.Item.AxisLabel!, label.Left - label.Natural, label.Place))];
            break;
        }
        foreach (var label in _labels.Where(label => !label.Leaving && picks.All(pick => pick.Key != label.Key)))
        {
            label.Leaving = true;
            label.Presence.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        foreach (var pick in picks)
        {
            var label = _labels.Find(entry => entry.Key == pick.Key);
            if (label is null)
            {
                var view = CopyMotion.Text(pick.Text, 12, 1, -.16, FontWeights.Normal, _muted);
                _axis.Children.Add(view);
                Canvas.SetTop(view, 8);
                label = new ChartAxisLabel(pick.Key, view, pick.Place);
                _labels.Add(label);
                if (animate && Moving) { label.Presence.Go(1, Tween.Standard, ArcEasing.Standard); } else { label.Presence.Jump(1); }
            }
            label.Leaving = false;
            label.Shift = pick.Shift;
            Aim(label.Place, pick.Place, Settle);
            if (!label.Presence.Active && label.Presence.Value < 1) { label.Presence.Go(1, Moving ? Tween.Standard : 0, ArcEasing.Standard); }
        }
        Wake();
    }

    private double RowY(double value) => Math.Round(H - value / _scale.Position * (H - Top), MidpointRounding.AwayFromZero) + .5;
    private double CenterOf(double place) { double width = _plot.ActualWidth; return width - (place + .5) * width * _unit.Position; }

    // Moves every label to where the springs say it is.
    private void Place()
    {
        double meanY = RowY(_mean.Position);
        bool avoid = ShowAverage;
        foreach (var row in _rows)
        {
            row.Y = RowY(row.Value);
            row.View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(row.View, 10);
            Canvas.SetTop(row.View, row.Y - 6);
            // A tick label makes way where the average label sits beside it.
            row.TextOpacity = avoid ? Math.Clamp((Math.Abs(row.Y - meanY) - 10) / 6, 0, 1) : 1;
            row.View.Opacity = row.Presence.Value * row.TextOpacity;
        }
        if (_meanLabel is not null && _meanHost is not null)
        {
            _meanLabel.Text = $"Avg {Format(Math.Round(_mean.Position, MidpointRounding.AwayFromZero))}";
            Canvas.SetLeft(_meanHost, 10);
            Canvas.SetTop(_meanHost, meanY - 6);
            _meanHost.Opacity = ShowAverage ? _meanFade.Value : 0;
        }
        foreach (var label in _labels)
        {
            label.View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label.View, CenterOf(label.Place.Position) - label.View.DesiredSize.Width / 2 + label.Shift);
            label.View.Opacity = label.Presence.Value;
        }
    }

    private int PointAt(double x)
    {
        double width = _plot.ActualWidth;
        if (width <= 0) { return Math.Max(0, Last); }
        int place = (int)Math.Floor((width - x) / width * Count);
        return Math.Clamp(Last - place, 0, Math.Max(0, Last));
    }

    private void KeyScrub(KeyEventArgs e) { if (Press(e.Key)) { e.Handled = true; _focus.Key(); } }

    private bool Press(Key key)
    {
        if (key == Key.Escape && Index is not null) { SetActive(null); return true; }
        int last = Last, from = Index ?? last + 1, page = Math.Min(7, Count);
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
        if (next is null || last < 0) { return false; }
        SetActive(Math.Clamp(next.Value, 0, last));
        return true;
    }

    private void SetActive(int? at)
    {
        if (Last < 0) { at = null; }
        if (_active == at) { return; }
        int? before = Index;
        _active = at;
        int? now = Index;
        if (now is { } index)
        {
            double place = Last - index;
            // The cursor appears on the first scrubbed bar and glides between the ones after it.
            if (!Moving || !_scrubbedOnce) { _cursor.Jump(place); }
            _scrubbedOnce = true;
            if (Moving) { _cursor.To(place, Glide); }
            _cursorFade.Go(1, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        else
        {
            _scrubbedOnce = false;
            _cursorFade.Go(0, Moving ? Tween.Instant : 0, ArcEasing.Standard);
        }
        Paints();
        Headline();
        if (UIElementAutomationPeer.FromElement(_scrubber) is { } peer) { peer.RaisePropertyChangedEvent(System.Windows.Automation.RangeValuePatternIdentifiers.ValueProperty, (double)((before ?? Last) + 1), (double)((now ?? Last) + 1)); }
        Wake();
    }

    // The series is drawn in a soft step of the accent; the scrubbed bar takes the full accent while its neighbours step back.
    private void Paints()
    {
        int? index = Index;
        foreach (var bar in _bars)
        {
            int at = Array.FindIndex(_data, item => item.Key == bar.Key);
            var to = index is null ? _rest : at == index ? _accent : _dim;
            bar.Color.Go(to, Moving ? Tween.Fast : 0, ArcEasing.Standard);
        }
    }

    // Headline copy moves the way the data did: a larger value rises from below, a later day arrives from below too.
    private void Headline()
    {
        if (_kind is null || _value is null || _when is null || _unitText is null) { return; }
        int? index = Index;
        double shown = index is { } at ? _data[at].Value : Average;
        if (shown != _shownValue) { _valueWay = shown > _shownValue ? 1 : -1; _shownValue = shown; }
        if (index != _previousIndex) { _indexWay = index is null || _previousIndex is null ? 1 : Math.Sign(index.Value - _previousIndex.Value) is var sign && sign != 0 ? sign : 1; _previousIndex = index; }
        _kind.Set(index is null ? AverageLabel : ValueLabel, _secondary, "", 1);
        _value.Set(Format(index is { } now ? _data[now].Value : Math.Round(Average, MidpointRounding.AwayFromZero)), _ink, "", _valueWay);
        _when.Set(index is { } day ? _data[day].Label : Period, _muted, "", index is null ? 1 : _indexWay);
        _unitText.Text = Unit ?? "";
    }

    private void Colorize(bool animate)
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _accent = Tween.Resolve(this, "ArcWpf.Brush.Accent");
        var surface = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        bool dark = ChartMath.IsDark(this);
        // On a dark surface a lighter mix keeps the hue clear instead of turning muddy.
        _rest = OkColor.Mix(_accent, surface, dark ? .76 : .62);
        _dim = OkColor.Mix(_accent, surface, dark ? .34 : .24);
        _gridColor = Tween.Resolve(this, "ArcWpf.Brush.Border");
        _strongColor = Tween.Resolve(this, "ArcWpf.Brush.BorderStrong");
        _meanColor = _secondary.Color;
        foreach (var bar in _bars)
        {
            int at = Array.FindIndex(_data, item => item.Key == bar.Key);
            bar.Color.Jump(Index is null ? _rest : at == Index ? _accent : _dim);
        }
        _plot.InvalidateVisual();
    }

    private void Wake() { if (Moving) { _loop.Start(); } else { Settled(); _plot.InvalidateVisual(); } }

    private void Settled()
    {
        foreach (var bar in _bars.Where(bar => bar.Leaving).ToList()) { _bars.Remove(bar); }
        foreach (var row in _rows.Where(row => row.Leaving).ToList()) { _gutter.Children.Remove(row.View); _rows.Remove(row); }
        foreach (var label in _labels.Where(label => label.Leaving).ToList()) { _axis.Children.Remove(label.View); _labels.Remove(label); }
        Place();
    }

    private bool Frame(double seconds)
    {
        bool busy = false;
        foreach (var bar in _bars)
        {
            bar.Place.Step(seconds);
            if (bar.Waiting)
            {
                bar.Delay -= seconds;
                if (bar.Delay <= 0) { bar.Waiting = false; bar.Amount.To(bar.Value, Grow); }
                busy = true;
            }
            bar.Amount.Step(seconds);
            bar.Color.Step(seconds);
            busy |= bar.Place.Active || bar.Amount.Active || bar.Color.Active;
        }
        foreach (var gone in _bars.Where(bar => bar.Leaving && !bar.Amount.Active).ToList()) { _bars.Remove(gone); }
        foreach (var spring in new[] { _unit, _scale, _mean, _cursor }) { spring.Step(seconds); busy |= spring.Active; }
        if (_meanDelay > 0)
        {
            _meanDelay -= seconds;
            busy = true;
            if (_meanDelay <= 0) { _mean.To(Average, Settle); }
        }
        foreach (var timed in new[] { _cursorFade, _meanFade }) { timed.Step(seconds); busy |= timed.Active; }
        foreach (var row in _rows.ToList())
        {
            row.Presence.Step(seconds);
            busy |= row.Presence.Active;
            if (row.Leaving && !row.Presence.Active) { _gutter.Children.Remove(row.View); _rows.Remove(row); }
        }
        foreach (var label in _labels.ToList())
        {
            label.Presence.Step(seconds);
            label.Place.Step(seconds);
            busy |= label.Presence.Active || label.Place.Active;
            if (label.Leaving && !label.Presence.Active) { _axis.Children.Remove(label.View); _labels.Remove(label); }
        }
        Place();
        _plot.InvalidateVisual();
        return busy;
    }

    // A column that grows from a square baseline to a softly rounded data end.
    private static void DrawBar(DrawingContext dc, Brush brush, double x, double w, double h, double bottom)
    {
        if (w <= 0 || h <= .01) { return; }
        double r = Math.Min(Radius, Math.Min(w / 2, h)), top = bottom - h;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, bottom), true, true);
            ctx.LineTo(new Point(x, top + r), false, false);
            ctx.ArcTo(new Point(x + r, top), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            ctx.LineTo(new Point(x + w - r, top), false, false);
            ctx.ArcTo(new Point(x + w, top + r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            ctx.LineTo(new Point(x + w, bottom), false, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    private void Paint(DrawingContext dc, Size size)
    {
        double width = size.Width;
        if (width <= 0) { return; }
        foreach (var row in _rows.Where(row => row.Value != 0))
        {
            double y = RowY(row.Value);
            dc.DrawLine(ChartMath.Crisp(ChartMath.Faded(_gridColor, row.Presence.Value)), new Point(0, y), new Point(width, y));
        }
        if (_cursorFade.Value > 0 && _data.Length > 0)
        {
            double x = Math.Round(CenterOf(_cursor.Position), MidpointRounding.AwayFromZero) + .5;
            dc.DrawLine(ChartMath.Crisp(ChartMath.Faded(_strongColor, _cursorFade.Value)), new Point(x, Top), new Point(x, H));
        }
        double slot = width * _unit.Position, barWidth = Math.Min(slot * Fill, MaxBar);
        foreach (var bar in _bars)
        {
            double center = CenterOf(bar.Place.Position);
            DrawBar(dc, ChartMath.Solid(bar.Color.Value), center - barWidth / 2, barWidth, bar.Amount.Position / _scale.Position * (H - Top), H);
        }
        dc.DrawLine(ChartMath.Crisp(_strongColor), new Point(0, H - .5), new Point(width, H - .5));
        if (ShowAverage && _meanFade.Value > 0)
        {
            double y = RowY(_mean.Position);
            var pen = new Pen(ChartMath.Solid(ChartMath.Faded(_meanColor, _meanFade.Value)), 1) { DashStyle = new DashStyle([3, 3], 0) };
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, y), new Point(width, y));
        }
    }
}
