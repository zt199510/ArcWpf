using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A reading in an append-only feed, in ascending time. A non-empty Anomaly becomes the marker tag.</summary>
public sealed record StreamPoint(DateTime Time, double Value, string? Anomaly = null);

public sealed class StreamPausedChangedEventArgs(bool paused) : EventArgs
{
    public bool Paused { get; } = paused;
}

// Arc Pro: no source. Built from the public docs and preview geometry; see docs/references/components/RealtimeStream.md.
public class RealtimeStream : Control
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IList<StreamPoint>), typeof(RealtimeStream), new FrameworkPropertyMetadata(null, (d, e) => ((RealtimeStream)d).OnPoints(e)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(RealtimeStream), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(RealtimeStream), new FrameworkPropertyMetadata("", (d, _) => ((RealtimeStream)d).SyncHead()));
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(nameof(Window), typeof(TimeSpan), typeof(RealtimeStream), new FrameworkPropertyMetadata(TimeSpan.FromSeconds(30), (d, _) => ((RealtimeStream)d).Redraw()));
    public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(nameof(Interval), typeof(TimeSpan), typeof(RealtimeStream), new FrameworkPropertyMetadata(TimeSpan.FromSeconds(1)));
    public static readonly DependencyProperty PlotHeightProperty = DependencyProperty.Register(nameof(PlotHeight), typeof(double), typeof(RealtimeStream), new FrameworkPropertyMetadata(200d, (d, _) => ((RealtimeStream)d).ApplyHeight()));
    public static readonly DependencyProperty PausedProperty = DependencyProperty.Register(nameof(Paused), typeof(bool), typeof(RealtimeStream), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((RealtimeStream)d).OnPaused()));
    public static readonly DependencyProperty PauseOnHoverProperty = DependencyProperty.Register(nameof(PauseOnHover), typeof(bool), typeof(RealtimeStream), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(nameof(Min), typeof(double?), typeof(RealtimeStream), new FrameworkPropertyMetadata(null, (d, _) => ((RealtimeStream)d).Redraw()));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(double?), typeof(RealtimeStream), new FrameworkPropertyMetadata(null, (d, _) => ((RealtimeStream)d).Redraw()));
    public static readonly DependencyProperty EmptyLabelProperty = DependencyProperty.Register(nameof(EmptyLabel), typeof(string), typeof(RealtimeStream), new FrameworkPropertyMetadata("Waiting for data", (d, _) => ((RealtimeStream)d).Redraw()));

    /// <summary>The feed. An ObservableCollection is followed as it grows; trim it to a few windows.</summary>
    public IList<StreamPoint>? Points { get => (IList<StreamPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    /// <summary>What is measured. Names the chart and the current value.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Visible span of time.</summary>
    public TimeSpan Window { get => (TimeSpan)GetValue(WindowProperty); set => SetValue(WindowProperty, value); }
    /// <summary>Typical time between points. The view trails the newest point by this much.</summary>
    public TimeSpan Interval { get => (TimeSpan)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public double PlotHeight { get => (double)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    public bool Paused { get => (bool)GetValue(PausedProperty); set => SetValue(PausedProperty, value); }
    /// <summary>Holds the view while the pointer is over the plot.</summary>
    public bool PauseOnHover { get => (bool)GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }
    public double? Min { get => (double?)GetValue(MinProperty); set => SetValue(MinProperty, value); }
    public double? Max { get => (double?)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public string EmptyLabel { get => (string)GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }
    public Func<double, string>? FormatValue { get; set; }
    public Func<DateTime, string>? FormatTime { get; set; }
    public event EventHandler<StreamPausedChangedEventArgs>? PausedChanged;

    private sealed class PlotSurface(RealtimeStream owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => owner.DrawPlot(dc, ActualWidth, ActualHeight);
    }

    private sealed class LiveToggle : ToggleButton
    {
        private static readonly ControlTemplate Bare = Make();
        private static ControlTemplate Make()
        {
            var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
            template.Seal();
            return template;
        }
        public LiveToggle() { Template = Bare; FocusVisualStyle = null; Cursor = Cursors.Hand; }
    }

    private const double GutterRight = 44.6, GutterBottom = 23, GridLabelGap = 8;
    private readonly SolidColorBrush _ink = new(), _muted = new(), _border = new(), _line = new(), _lineWash = new(), _anomaly = new(), _anomalySoft = new(), _surface = new(), _statusInk = new();
    private readonly OdometerText _number = new(52, 1, -1.56, FontWeights.Normal);
    private readonly TrackedText _unit;
    private readonly PlotSurface _plot;
    private readonly Canvas _overlay = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly Border _tooltip = new() { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8), Visibility = Visibility.Collapsed };
    private readonly TextBlock _tipValue = ArcText.Block("", 14, 18.2, "Foreground", true), _tipMeta = ArcText.Block("", 12, 15.6, "TextMuted");
    private readonly Grid _plotBox = new();
    private readonly Border _plotRing = new() { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly LiveToggle _status = new();
    private readonly Border _dot = new() { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcDataGridIcon _pauseGlyph = new() { Icon = "pause", Size = 9, StrokeThickness = 3.4, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private SwapText? _statusText;
    private Border? _statusBox;
    private readonly Border _statusRing = new() { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly FrameLoop _loop;
    private readonly FocusVisible _plotFocus;
    private readonly HashSet<StreamPoint> _announced = [];
    private Decorator? _host;
    private INotifyCollectionChanged? _watched;
    private double _head, _axisLow = double.NaN, _axisHigh = double.NaN, _pointerX = double.NaN;
    private bool _hover, _focused, _gliding, _seeded;
    private int _queued, _cursor = -1;
    private DateTime _epoch;

    static RealtimeStream() => DefaultStyleKeyProperty.OverrideMetadata(typeof(RealtimeStream), new FrameworkPropertyMetadata(typeof(RealtimeStream)));

    public RealtimeStream()
    {
        _unit = CopyMotion.Text("", 16, 1.4, -.16, FontWeights.Normal, _muted);
        _plot = new PlotSurface(this) { Focusable = true, FocusVisualStyle = null, Cursor = Cursors.Cross };
        _plotFocus = new FocusVisible(_plot, () => _plotRing.Visibility = _plotFocus!.Shown ? Visibility.Visible : Visibility.Collapsed);
        _loop = new FrameLoop(Frame);
        Loaded += (_, _) => { Inks(); Rewatch(); Sync(); Kick(); };
        Unloaded += (_, _) => { _loop.Stop(); Unwatch(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) { Kick(); } else { _loop.Stop(); } };
        ThemeWatch.Add(this, () => { Inks(); Redraw(); });
    }

    internal double HeadForTest => _head;
    internal double NewestForTest => Newest?.Time.Subtract(Epoch).TotalMilliseconds ?? 0;
    internal (double Low, double High) AxisForTest => (_axisLow, _axisHigh);
    internal int QueuedForTest => _queued;
    internal int CursorForTest => _cursor;
    internal bool HeldForTest => Held;
    internal bool RunningForTest => _loop.IsRunning;
    internal string StatusForTest => _statusText?.Text ?? "";
    internal string ValueForTest => _number.Text;
    internal bool TooltipShownForTest => _tooltip.Visibility == Visibility.Visible;
    internal string TooltipForTest => _tipValue.Text + " | " + _tipMeta.Text;
    internal FrameworkElement PlotForTest => _plot;
    internal ToggleButton StatusButtonForTest => _status;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void HoverForTest(double x) { _hover = true; _pointerX = x; SyncHead(); UpdateReading(); Redraw(); }
    internal void LeaveForTest() { _hover = false; _pointerX = double.NaN; Resume(); UpdateReading(); Redraw(); }
    internal void KeyForTest(Key key) => HandleKey(key);
    internal void FocusForTest() { _focused = true; _cursor = VisibleRange().Last; UpdateReading(); }
    internal static double NiceStepForTest(double span) => NiceStep(span);
    internal IReadOnlyList<string> AxisLabelsForTest => [.. Ticks().Select(tick => tick.ToString("0", CultureInfo.InvariantCulture))];
    internal IReadOnlyList<string> TimeLabelsForTest(double width) => [.. TimeTicks(width).Select(tick => tick.Label)];
    internal double XOfForTest(int index, double width) => XOf(Time(index), width - GutterRight);

    protected override AutomationPeer OnCreateAutomationPeer() => new StreamPeer(this);

    private sealed class StreamPeer(RealtimeStream owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => $"{owner.Label}, {owner.Spoken(owner.Newest)}";
        // The hidden table of the latest twenty readings, read as the item status.
        protected override string GetItemStatusCore() => owner.LatestReadings();
        protected override string GetLocalizedControlTypeCore() => "live chart";
    }

    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private bool Held => Paused || (_hover && PauseOnHover) || _focused;
    private IList<StreamPoint> Feed => Points ?? (IList<StreamPoint>)[];
    private StreamPoint? Newest => Feed.Count > 0 ? Feed[^1] : null;
    private DateTime Epoch => _seeded ? _epoch : _epoch = Feed.Count > 0 ? Feed[0].Time : DateTime.UnixEpoch;
    private double Time(int index) => Feed[index].Time.Subtract(Epoch).TotalMilliseconds;
    private double WindowMs => Math.Max(1, Window.TotalMilliseconds);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Build()
    {
        if (_host is null) { return; }
        Inks();
        _number.SetValue(OdometerText.ForegroundProperty, _ink);
        var now = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        now.Children.Add(_number);
        _unit.Margin = new Thickness(6, 0, 0, 3);
        _unit.VerticalAlignment = VerticalAlignment.Bottom;
        now.Children.Add(_unit);
        _statusText = new SwapText(14, 1.4, -.16, FontWeights.Normal, new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.TextSecondary"))) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        _dot.Background = _statusInk;
        _pauseGlyph.Stroke = _statusInk;
        var slot = new Grid { Width = 8, VerticalAlignment = VerticalAlignment.Center };
        slot.Children.Add(_dot);
        slot.Children.Add(_pauseGlyph);
        _pauseGlyph.Margin = new Thickness(-.5, 0, -.5, 0);
        var inner = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 12, 0) };
        inner.Children.Add(slot);
        inner.Children.Add(_statusText);
        _statusBox = new Border { Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Child = inner };
        _statusBox.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _statusBox.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        var statusFace = new Grid();
        statusFace.Children.Add(_statusBox);
        _statusRing.BorderBrush = FocusVisible.RingBrush(this);
        statusFace.Children.Add(_statusRing);
        _status.Content = statusFace;
        _status.Checked += (_, _) => SetPaused(true);
        _status.Unchecked += (_, _) => SetPaused(false);
        _status.GotKeyboardFocus += (_, _) => { if (InputManager.Current.MostRecentInputDevice is KeyboardDevice) { _statusRing.Visibility = Visibility.Visible; } };
        _status.LostKeyboardFocus += (_, _) => _statusRing.Visibility = Visibility.Collapsed;
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        _status.VerticalAlignment = VerticalAlignment.Bottom;
        var head = new Grid { Height = 52 };
        head.Children.Add(now);
        head.Children.Add(_status);
        var tip = new StackPanel();
        tip.Children.Add(_tipValue);
        _tipMeta.Margin = new Thickness(0, 1, 0, 0);
        tip.Children.Add(_tipMeta);
        _tooltip.Child = tip;
        _tooltip.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _tooltip.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _tooltip.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _overlay.Children.Clear();
        _overlay.Children.Add(_tooltip);
        _plotRing.BorderBrush = FocusVisible.RingBrush(this);
        _plotBox.Children.Clear();
        _plotBox.Children.Add(_plot);
        _plotBox.Children.Add(_plotRing);
        _plotBox.Children.Add(_overlay);
        _plotBox.Margin = new Thickness(0, 12, 0, 0);
        ApplyHeight();
        _plot.MouseMove += (_, e) => { _hover = true; _pointerX = e.GetPosition(_plot).X; SyncHead(); UpdateReading(); Redraw(); };
        _plot.MouseLeave += (_, _) => { _hover = false; _pointerX = double.NaN; if (!Held) { Resume(); } SyncHead(); UpdateReading(); Redraw(); Kick(); };
        _plot.MouseLeftButtonDown += (_, e) => { _plotFocus.Pointer(); _plot.Focus(); _plot.CaptureMouse(); e.Handled = true; };
        _plot.MouseLeftButtonUp += (_, _) => _plot.ReleaseMouseCapture();
        _plot.GotKeyboardFocus += (_, _) => { _focused = true; if (_plotFocus.Shown) { _cursor = VisibleRange().Last; UpdateReading(); AnnounceReading(); } SyncHead(); Redraw(); };
        _plot.LostKeyboardFocus += (_, _) => { _focused = false; _cursor = -1; if (!Held) { Resume(); } SyncHead(); UpdateReading(); Redraw(); Kick(); };
        _plot.PreviewKeyDown += (_, e) => { if (HandleKey(e.Key)) { e.Handled = true; } };
        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(_plotBox);
        _host.Child = stack;
        Sync();
    }

    private void ApplyHeight() => _plot.Height = PlotHeight;

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _border.Color = Tween.Resolve(this, "ArcWpf.Brush.Border");
        _surface.Color = Tween.Resolve(this, "ArcWpf.Brush.Surface");
        _statusInk.Color = Tween.Resolve(this, "ArcWpf.Brush.Accent");
        var line = Tween.Resolve(this, "ArcWpf.Brush.ChartLine");
        _line.Color = line;
        _lineWash.Color = OkColor.Alpha(line, .09);
        var anomaly = Tween.Resolve(this, "ArcWpf.Brush.ChartAnomaly");
        _anomaly.Color = anomaly;
        _anomalySoft.Color = OkColor.Alpha(anomaly, .4);
    }

    private void OnPoints(DependencyPropertyChangedEventArgs e)
    {
        Unwatch();
        Rewatch();
        Sync();
    }

    private void Rewatch()
    {
        if (_watched is not null) { return; }
        if (Points is INotifyCollectionChanged watched) { _watched = watched; watched.CollectionChanged += OnFeed; }
    }

    private void Unwatch() { if (_watched is not null) { _watched.CollectionChanged -= OnFeed; _watched = null; } }
    private void OnFeed(object? sender, NotifyCollectionChangedEventArgs e) => Sync(e.Action == NotifyCollectionChangedAction.Add ? e.NewItems?.Count ?? 0 : 0);

    /// <summary>Call after changing a Points list that does not raise change notifications.</summary>
    public void Refresh() => Sync();

    private void Sync(int added = 0)
    {
        if (_host is null) { return; }
        var feed = Feed;
        if (feed.Count == 0) { _seeded = false; }
        else if (!_seeded)
        {
            _epoch = feed[0].Time;
            _seeded = true;
            // The view starts one interval behind the newest reading, so the first arrival slides in.
            _head = Math.Max(0, Time(feed.Count - 1) - Interval.TotalMilliseconds);
            if (Reduced) { _head = Time(feed.Count - 1); }
        }
        if (Held) { _queued += added; }
        foreach (var point in feed.Skip(Math.Max(0, feed.Count - added)))
        {
            // Anomalies are announced once as they arrive.
            if (!string.IsNullOrEmpty(point.Anomaly) && _announced.Add(point) && IsLoaded) { Announcer.Say(this, $"{point.Anomaly} at {TimeText(point.Time)}, {ValueText(point.Value)}"); }
        }
        if (Reduced && !Held && feed.Count > 0) { _head = Time(feed.Count - 1); }
        SyncHead();
        Redraw();
        Kick();
    }

    private string ValueText(double value)
    {
        string text = FormatValue?.Invoke(value) ?? value.ToString("0", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}";
    }

    private string TimeText(DateTime time) => FormatTime?.Invoke(time) ?? time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    private string Spoken(StreamPoint? point) => point is null ? EmptyLabel : ValueText(point.Value);

    private string LatestReadings() => string.Join("; ", Feed.Skip(Math.Max(0, Feed.Count - 20)).Select(point => $"{TimeText(point.Time)}, {ValueText(point.Value)}" + (string.IsNullOrEmpty(point.Anomaly) ? "" : $", {point.Anomaly}")));

    private double _lastShown = double.NaN;

    private void SyncHead()
    {
        if (_host is null || _statusText is null) { return; }
        _unit.Text = Unit ?? "";
        var newest = Newest;
        if (newest is null) { _number.Set("–"); }
        else
        {
            int direction = double.IsNaN(_lastShown) || newest.Value >= _lastShown ? 1 : -1;
            _lastShown = newest.Value;
            _number.Set(FormatValue?.Invoke(newest.Value) ?? newest.Value.ToString("0", CultureInfo.InvariantCulture), direction);
        }
        // Holding on hover or focus reads the same as pausing; only the button keeps the explicit state.
        _statusText.Set(Held ? (_queued > 0 ? $"Paused, {_queued} new" : "Paused") : "Live");
        _dot.Visibility = Held ? Visibility.Collapsed : Visibility.Visible;
        _pauseGlyph.Visibility = Held ? Visibility.Visible : Visibility.Collapsed;
        _status.IsChecked = Paused;
        System.Windows.Automation.AutomationProperties.SetName(_status, (Paused ? "Paused" : "Live") + (Paused ? ", resume" : ", pause"));
    }

    private void SetPaused(bool paused) { if (Paused != paused) { SetCurrentValue(PausedProperty, paused); } }

    private void OnPaused()
    {
        if (Paused) { _queued = 0; } else { Resume(); }
        SyncHead();
        PausedChanged?.Invoke(this, new StreamPausedChangedEventArgs(Paused));
        Kick();
    }

    private void Resume()
    {
        if (Paused) { return; }
        _queued = 0;
        if (Reduced) { _head = Newest is null ? _head : Time(Feed.Count - 1); }
        else { _gliding = true; }
        SyncHead();
        Kick();
    }

    private void Kick() { if (IsLoaded && IsVisible && Feed.Count > 0 && !Reduced) { _loop.Start(); } }
    private void Redraw() => _plot.InvalidateVisual();

    private bool Frame(double seconds)
    {
        if (!IsVisible || Feed.Count == 0) { return false; }
        double newest = Time(Feed.Count - 1);
        double step = Math.Max(0, Math.Min(seconds, .1));
        if (!Held)
        {
            if (_gliding)
            {
                // Resuming glides from the held view back to live.
                _head += (newest - _head) * (1 - Math.Exp(-step / .14));
                if (newest - _head < Interval.TotalMilliseconds) { _gliding = false; }
            }
            else
            {
                // A feed that runs ahead pulls the head along, so it never falls further behind than one interval.
                double behind = newest - _head, catchUp = behind > Interval.TotalMilliseconds * 1.5 ? 1.5 : 1;
                _head = Math.Min(newest, _head + step * 1000 * catchUp);
            }
        }
        EaseAxis(step);
        Redraw();
        // Keep running while anything can still change without new data.
        bool settled = Math.Abs(_axisLow - AxisGoal().Low) < .01 && Math.Abs(_axisHigh - AxisGoal().High) < .01;
        return !Held && _head < newest - .5 || !settled || PopsRunning();
    }

    private bool PopsRunning()
    {
        var (first, last) = VisibleRange();
        for (int i = Math.Max(first, 0); i <= last && i < Feed.Count; i++)
        {
            if (string.IsNullOrEmpty(Feed[i].Anomaly)) { continue; }
            double age = (_head - Time(i)) / 1000;
            if (age >= 0 && age < .6) { return true; }
        }
        return false;
    }

    // Only points inside the window are drawn, found by binary search.
    private (int First, int Last) VisibleRange()
    {
        var feed = Feed;
        if (feed.Count == 0) { return (0, -1); }
        double from = _head - WindowMs, to = _head;
        int Lower(double t) { int lo = 0, hi = feed.Count; while (lo < hi) { int mid = (lo + hi) / 2; if (Time(mid) < t) { lo = mid + 1; } else { hi = mid; } } return lo; }
        int first = Math.Max(0, Lower(from) - 1);
        int last = Math.Min(feed.Count - 1, Lower(to));
        if (Reduced || Time(last) <= to) { return (first, last); }
        return (first, last);
    }

    // The axis leaves room over the data for the anomaly tags: more above than below.
    private (double Low, double High) AxisGoal()
    {
        var (first, last) = VisibleRange();
        double low = double.PositiveInfinity, high = double.NegativeInfinity;
        for (int i = first; i <= last && i < Feed.Count; i++) { low = Math.Min(low, Feed[i].Value); high = Math.Max(high, Feed[i].Value); }
        if (double.IsInfinity(low)) { low = 0; high = 1; }
        double span = high > low ? high - low : Math.Max(1, Math.Abs(high) * .1);
        return (Min ?? low - span * .18, Max ?? high + span * .3);
    }

    private void EaseAxis(double seconds)
    {
        var goal = AxisGoal();
        if (double.IsNaN(_axisLow) || Reduced) { (_axisLow, _axisHigh) = goal; return; }
        double share = 1 - Math.Exp(-seconds / .18);
        _axisLow += (goal.Low - _axisLow) * share;
        _axisHigh += (goal.High - _axisHigh) * share;
    }

    internal static double NiceStep(double span)
    {
        double rough = Math.Max(span, 1e-9) / 2.5, power = Math.Pow(10, Math.Floor(Math.Log10(rough))), unit = rough / power;
        return (unit <= 1 ? 1 : unit <= 2 ? 2 : unit <= 5 ? 5 : 10) * power;
    }

    private IEnumerable<double> Ticks()
    {
        if (double.IsNaN(_axisLow) || _axisHigh <= _axisLow) { yield break; }
        double step = NiceStep(_axisHigh - _axisLow);
        for (double tick = Math.Ceiling(_axisLow / step) * step; tick < _axisHigh; tick += step) { yield return tick; }
    }

    // Time labels thin out as the plot narrows.
    private IEnumerable<(double Share, string Label)> TimeTicks(double width)
    {
        double seconds = Window.TotalSeconds;
        int gaps = width < 220 ? 1 : 3;
        for (int i = 0; i <= gaps; i++)
        {
            double back = seconds * (gaps - i) / gaps;
            yield return ((double)i / gaps, back <= 0 ? "now" : back >= 60 && back % 60 == 0 ? $"−{back / 60:0}m" : $"−{back:0}s");
        }
    }

    private double XOf(double time, double plotWidth) => plotWidth * (1 - (_head - time) / WindowMs);
    private double YOf(double value, double plotHeight) => plotHeight * (1 - (value - _axisLow) / Math.Max(1e-9, _axisHigh - _axisLow));

    private double ValueAtHead(int last)
    {
        var feed = Feed;
        if (last <= 0) { return feed.Count > 0 ? feed[Math.Max(0, last)].Value : 0; }
        double t0 = Time(last - 1), t1 = Time(last);
        if (_head >= t1 || t1 <= t0) { return feed[last].Value; }
        return feed[last - 1].Value + (feed[last].Value - feed[last - 1].Value) * Math.Clamp((_head - t0) / (t1 - t0), 0, 1);
    }

    private FormattedText Label12(string text, Brush brush, double size = 12) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void DrawPlot(DrawingContext dc, double width, double height)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        double plotWidth = Math.Max(1, width - GutterRight), plotHeight = Math.Max(1, height - GutterBottom);
        if (Feed.Count == 0)
        {
            var empty = Label12(EmptyLabel, _muted, 14);
            dc.DrawText(empty, new Point((plotWidth - empty.Width) / 2, (plotHeight - empty.Height) / 2));
            return;
        }
        if (double.IsNaN(_axisLow)) { EaseAxis(0); }
        var gridPen = new Pen(_border, 1);
        foreach (double tick in Ticks())
        {
            double y = Math.Round(YOf(tick, plotHeight)) + .5;
            if (y < 0 || y > plotHeight) { continue; }
            dc.DrawLine(gridPen, new Point(0, y), new Point(plotWidth, y));
            var label = Label12(FormatValue?.Invoke(tick) ?? tick.ToString("0", CultureInfo.InvariantCulture), _muted);
            dc.DrawText(label, new Point(plotWidth + GridLabelGap, y - label.Height / 2));
        }
        foreach (var (share, text) in TimeTicks(plotWidth))
        {
            var label = Label12(text, _muted);
            double x = share <= 0 ? 0 : share >= 1 ? plotWidth - label.Width : plotWidth * share - label.Width / 2;
            dc.DrawText(label, new Point(x, plotHeight + 11 - label.Height / 2 + 1));
        }
        var (first, last) = VisibleRange();
        if (last < first) { return; }
        var points = new List<Point>();
        for (int i = first; i <= last; i++)
        {
            double t = Time(i);
            if (t > _head) { break; }
            points.Add(new Point(XOf(t, plotWidth), YOf(Feed[i].Value, plotHeight)));
        }
        // The head interpolates between readings, so new points slide in rather than pop.
        double headValue = ValueAtHead(last);
        var tip = new Point(plotWidth, YOf(headValue, plotHeight));
        points.Add(tip);
        dc.PushClip(new RectangleGeometry(new Rect(0, -8, plotWidth + 8, plotHeight + 8)));
        if (points.Count > 1)
        {
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(points[0].X, plotHeight), true, true);
                foreach (var p in points) { context.LineTo(p, false, false); }
                context.LineTo(new Point(points[^1].X, plotHeight), false, false);
            }
            area.Freeze();
            dc.DrawGeometry(_lineWash, null, area);
            var line = new StreamGeometry();
            using (var context = line.Open())
            {
                context.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Count; i++) { context.LineTo(points[i], true, true); }
            }
            line.Freeze();
            dc.DrawGeometry(null, new Pen(_line, 1.75) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);
        }
        DrawAnomalies(dc, first, last, plotWidth, plotHeight);
        dc.DrawEllipse(_line, null, tip, 3.5, 3.5);
        DrawReading(dc, plotWidth, plotHeight);
        dc.Pop();
    }

    // Markers and tags pop in with one overshoot as the head reaches them. A run of readings shares one tag.
    private void DrawAnomalies(DrawingContext dc, int first, int last, double plotWidth, double plotHeight)
    {
        double previousTime = double.NegativeInfinity;
        string? runTag = null;
        for (int i = first; i <= last; i++)
        {
            var point = Feed[i];
            double t = Time(i);
            if (t > _head || string.IsNullOrEmpty(point.Anomaly)) { runTag = null; continue; }
            bool lead = runTag != point.Anomaly || t - previousTime > Interval.TotalMilliseconds * 1.6;
            runTag = point.Anomaly;
            previousTime = t;
            double x = XOf(t, plotWidth), y = YOf(point.Value, plotHeight), pop = Pop((_head - t) / 1000);
            const double TagY = 14;
            dc.DrawLine(new Pen(_anomalySoft, 1), new Point(x, TagY), new Point(x, y));
            dc.DrawEllipse(_anomaly, null, new Point(x, y), 3.5 * pop, 3.5 * pop);
            if (!lead) { continue; }
            var label = Label12(point.Anomaly!, _ink);
            double w = label.Width + 16, h = 18;
            var box = new Rect(Math.Clamp(x - w / 2, 0, plotWidth - w), TagY - h / 2, w, h);
            dc.PushTransform(new ScaleTransform(pop, pop, box.X + w / 2, box.Y + h / 2));
            dc.DrawRoundedRectangle(_surface, new Pen(_border, 1), box, 9, 9);
            dc.DrawText(label, new Point(box.X + 8, box.Y + (h - label.Height) / 2));
            dc.Pop();
        }
    }

    private double Pop(double age)
    {
        if (Reduced || age >= .6) { return 1; }
        if (age <= 0) { return 0; }
        // A spring released from nothing that passes its rest once.
        return 1 - Math.Exp(-9 * age) * Math.Cos(13 * age);
    }

    private int ReadingIndex(double plotWidth)
    {
        if (_cursor >= 0 && _cursor < Feed.Count) { return _cursor; }
        if (double.IsNaN(_pointerX)) { return -1; }
        var (first, last) = VisibleRange();
        double wanted = _head - WindowMs * (1 - Math.Clamp(_pointerX, 0, plotWidth) / plotWidth);
        int best = -1;
        double nearest = double.MaxValue;
        for (int i = first; i <= last && i < Feed.Count; i++)
        {
            double t = Time(i);
            if (t > _head) { continue; }
            if (Math.Abs(t - wanted) < nearest) { nearest = Math.Abs(t - wanted); best = i; }
        }
        return best;
    }

    private void DrawReading(DrawingContext dc, double plotWidth, double plotHeight)
    {
        int index = ReadingIndex(plotWidth);
        if (index < 0) { return; }
        double x = XOf(Time(index), plotWidth), y = YOf(Feed[index].Value, plotHeight);
        dc.DrawLine(new Pen(_ink, 1), new Point(Math.Round(x) + .5, 0), new Point(Math.Round(x) + .5, plotHeight));
        dc.DrawEllipse(_line, new Pen(_surface, 1.5), new Point(x, y), 4, 4);
    }

    private void UpdateReading()
    {
        double plotWidth = Math.Max(1, _plot.ActualWidth - GutterRight), plotHeight = Math.Max(1, _plot.ActualHeight - GutterBottom);
        int index = ReadingIndex(plotWidth);
        if (index < 0) { _tooltip.Visibility = Visibility.Collapsed; return; }
        var point = Feed[index];
        _tipValue.Text = ValueText(point.Value);
        _tipMeta.Text = string.IsNullOrEmpty(point.Anomaly) ? TimeText(point.Time) : $"{point.Anomaly}, {TimeText(point.Time)}";
        _tooltip.Visibility = Visibility.Visible;
        _tooltip.UpdateLayout();
        double x = XOf(Time(index), plotWidth), y = YOf(point.Value, plotHeight);
        double w = _tooltip.ActualWidth, h = _tooltip.ActualHeight, left = x + 14;
        if (left + w > _plot.ActualWidth) { left = x - 14 - w; }
        Canvas.SetLeft(_tooltip, Math.Max(0, left));
        Canvas.SetTop(_tooltip, Math.Clamp(y - h - 8, 0, Math.Max(0, plotHeight - h)));
    }

    private void AnnounceReading()
    {
        if (_cursor >= 0 && _cursor < Feed.Count) { Announcer.Say(this, $"{TimeText(Feed[_cursor].Time)}, {ValueText(Feed[_cursor].Value)}"); }
    }

    private bool HandleKey(Key key)
    {
        var (first, last) = VisibleRange();
        if (last >= Feed.Count) { last = Feed.Count - 1; }
        while (last > first && Time(last) > _head) { last--; }
        int current = _cursor < 0 ? last : _cursor;
        int? next = key switch
        {
            Key.Left => current - 1,
            Key.Right => current + 1,
            Key.PageUp => current - 10,
            Key.PageDown => current + 10,
            Key.Home => first,
            Key.End => last,
            _ => null,
        };
        if (next is { } target && last >= first)
        {
            _plotFocus.Key();
            _cursor = Math.Clamp(target, first, last);
            UpdateReading();
            AnnounceReading();
            Redraw();
            return true;
        }
        switch (key)
        {
            case Key.Space or Key.Enter:
                SetPaused(!Paused);
                return true;
            case Key.Escape:
                _cursor = -1;
                SetPaused(false);
                _focused = false;
                Keyboard.ClearFocus();
                Resume();
                UpdateReading();
                Redraw();
                return true;
            default:
                return false;
        }
    }
}
