using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum TickerDelta { Percent, Absolute }
public enum TickerPositive { Up, Down }
public enum TickerOverflow { Marquee, Scroll }

/// <summary>One live reading. Replace the Metrics list to update; changes are matched by Id.</summary>
public sealed class TickerMetric
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double Value { get; set; }
    /// <summary>A .NET numeric format such as C0, P1 or N0. Defaults to whole numbers.</summary>
    public string? Format { get; set; }
    /// <summary>Text after the number, such as " ms".</summary>
    public string? Unit { get; set; }
    public TickerDelta Delta { get; set; } = TickerDelta.Percent;
    public TickerPositive Positive { get; set; } = TickerPositive.Up;
    /// <summary>Recent values, oldest first. Left out, the ticker keeps its own history of the values it has seen.</summary>
    public IReadOnlyList<double>? History { get; set; }
    /// <summary>Context at the top of the panel. Defaults to Last n updates.</summary>
    public string? Caption { get; set; }
}

public sealed class TickerMetricCollection : Collection<TickerMetric>;

// Arc Pro: no source. Built from the public docs and preview geometry; see docs/references/components/MetricTicker.md.
public class MetricTicker : Control
{
    public static readonly DependencyProperty MetricsProperty = DependencyProperty.Register(nameof(Metrics), typeof(IList<TickerMetric>), typeof(MetricTicker), new FrameworkPropertyMetadata(null, (d, _) => ((MetricTicker)d).Sync()));
    public static readonly DependencyProperty OverflowProperty = DependencyProperty.Register(nameof(Overflow), typeof(TickerOverflow), typeof(MetricTicker), new FrameworkPropertyMetadata(TickerOverflow.Marquee, (d, _) => ((MetricTicker)d).Reflow()));
    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(nameof(Speed), typeof(double), typeof(MetricTicker), new FrameworkPropertyMetadata(32d));
    public static readonly DependencyProperty FlashDurationProperty = DependencyProperty.Register(nameof(FlashDuration), typeof(TimeSpan), typeof(MetricTicker), new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(1600)));
    public static readonly DependencyProperty HistoryLengthProperty = DependencyProperty.Register(nameof(HistoryLength), typeof(int), typeof(MetricTicker), new FrameworkPropertyMetadata(24));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(MetricTicker), new FrameworkPropertyMetadata("en-US", (d, _) => ((MetricTicker)d).Sync()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricTicker), new FrameworkPropertyMetadata("Live metrics"));

    public IList<TickerMetric>? Metrics { get => (IList<TickerMetric>?)GetValue(MetricsProperty); set => SetValue(MetricsProperty, value); }
    public TickerOverflow Overflow { get => (TickerOverflow)GetValue(OverflowProperty); set => SetValue(OverflowProperty, value); }
    /// <summary>Marquee drift in DIPs per second.</summary>
    public double Speed { get => (double)GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }
    /// <summary>How long a change stays emphasised before the delta settles.</summary>
    public TimeSpan FlashDuration { get => (TimeSpan)GetValue(FlashDurationProperty); set => SetValue(FlashDurationProperty, value); }
    public int HistoryLength { get => (int)GetValue(HistoryLengthProperty); set => SetValue(HistoryLengthProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    /// <summary>Accessible name of the strip.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    private sealed class Live
    {
        public required TickerMetric Metric;
        public double? Previous;
        public readonly List<double> Seen = [];
        public required Cell Primary, Copy;
        public DateTime FlashUntil;
        public IEnumerable<Cell> Cells { get { yield return Primary; yield return Copy; } }
    }

    private enum Tone { None, Good, Bad }

    // One metric in the strip. The repeated copy that makes the marquee seamless is the same cell, out of the tab order and hidden from automation.
    private sealed class Cell : Decorator
    {
        public readonly MetricTicker Owner;
        public readonly bool IsCopy;
        public readonly Border Face = new() { CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 8, 14, 9) };
        public readonly Border Separator = new() { Width = 1, Height = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        public readonly TrackedText Title;
        public readonly OdometerText Number = new(18, 1.3, -.16, FontWeights.Medium);
        public readonly OdometerText DeltaNumber = new(12, 1.4, -.16, FontWeights.Medium);
        public readonly Border Pill = new() { CornerRadius = new CornerRadius(999), Padding = new Thickness(4, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        public readonly Border Ring = new() { CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), IsHitTestVisible = false, Opacity = 0, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
        public readonly ArcDataGridIcon Arrow = new() { Size = 12, StrokeThickness = 2.25, RenderTransform = new TranslateTransform() };
        public readonly SolidColorBrush Ink = new(), PillFill = new(Colors.Transparent), Wash = new(Colors.Transparent), RingInk = new();
        public readonly Border FocusRing = new() { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        public readonly FocusVisible Focused;
        public string Id = "";
        public int Index;

        public Cell(MetricTicker owner, bool copy)
        {
            (Owner, IsCopy) = (owner, copy);
            Focusable = !copy;
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
            Title = CopyMotion.Text("", 12, 1.4, -.16, FontWeights.Normal, owner._muted);
            Title.HorizontalAlignment = HorizontalAlignment.Left;
            Number.SetValue(OdometerText.ForegroundProperty, owner._ink);
            DeltaNumber.SetValue(OdometerText.ForegroundProperty, Ink);
            Arrow.Stroke = Ink;
            Arrow.Margin = new Thickness(0, 0, 2, 0);
            Arrow.VerticalAlignment = VerticalAlignment.Center;
            Pill.Background = PillFill;
            Ring.BorderBrush = RingInk;
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(Arrow);
            line.Children.Add(DeltaNumber);
            var holder = new Grid();
            holder.Children.Add(line);
            Pill.Child = holder;
            var pillSlot = new Grid { VerticalAlignment = VerticalAlignment.Center };
            pillSlot.Children.Add(Pill);
            pillSlot.Children.Add(Ring);
            Ring.Margin = Pill.Margin;
            var reading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            reading.Children.Add(Number);
            reading.Children.Add(pillSlot);
            var stack = new StackPanel();
            stack.Children.Add(Title);
            stack.Children.Add(reading);
            Face.Child = stack;
            Face.Background = Wash;
            var body = new Grid { Margin = new Thickness(4) };
            body.Children.Add(Face);
            FocusRing.BorderBrush = FocusVisible.RingBrush(owner);
            body.Children.Add(FocusRing);
            // The whole cell answers the pointer, not only its text.
            var all = new Grid { Background = Brushes.Transparent };
            all.Children.Add(Separator);
            all.Children.Add(body);
            Child = all;
            Separator.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
            Focused = new FocusVisible(this, () => FocusRing.Visibility = Focused!.Shown ? Visibility.Visible : Visibility.Collapsed);
            MouseEnter += (_, _) => owner.OnCellEnter(this);
            MouseLeave += (_, _) => owner.OnCellLeave();
            GotKeyboardFocus += (_, _) => owner.OnCellFocus(this);
            LostKeyboardFocus += (_, _) => owner.OnCellBlur();
            if (copy) { AutomationProperties.SetIsOffscreenBehavior(this, IsOffscreenBehavior.Offscreen); }
        }

        protected override AutomationPeer OnCreateAutomationPeer() => IsCopy ? new SilentPeer(this) : new DisclosurePeer(this, () => Owner.Spoken(Id), () => Owner._openId == Id, () => Owner.Toggle(this));
    }

    private sealed class SilentPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }

    // Lays the group, and its copy when the strip loops, out in one row. Metrics that fit share the width.
    private sealed class Strip(MetricTicker owner) : Panel
    {
        public double GroupWidth { get; private set; }
        public double[] Natural = [];
        // The strip is wider than its slot and scrolls by transform; the default clip would travel with it and cut off the cells it brings in.
        protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;
        protected override Size MeasureOverride(Size availableSize)
        {
            var primary = owner._live.Select(live => live.Primary).ToList();
            foreach (UIElement child in Children) { child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
            Natural = [.. primary.Select(cell => cell.DesiredSize.Width)];
            double sum = Natural.Sum();
            double height = primary.Count == 0 ? 0 : primary.Max(cell => cell.DesiredSize.Height);
            double width = owner._viewportWidth;
            bool fits = sum <= width + .5;
            if (fits != owner._fits) { owner._fits = fits; Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(owner.Reflow)); }
            GroupWidth = fits ? width : sum;
            return new Size(owner.Looping ? GroupWidth * 2 : GroupWidth, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = owner._live.Count;
            if (count == 0) { return finalSize; }
            double[] widths = Natural;
            if (owner._fits)
            {
                // Equal shares when every metric fits in one; otherwise each takes the same extra.
                double share = owner._viewportWidth / count;
                double extra = (owner._viewportWidth - Natural.Sum()) / count;
                widths = Natural.Max() <= share ? [.. Natural.Select(_ => share)] : [.. Natural.Select(width => width + extra)];
            }
            double x = 0;
            for (int i = 0; i < count; i++) { owner._live[i].Primary.Arrange(new Rect(x, 0, widths[i], finalSize.Height)); x += widths[i]; }
            for (int i = 0; i < count; i++)
            {
                var copy = owner._live[i].Copy;
                if (copy.Visibility == Visibility.Visible) { copy.Arrange(new Rect(x, 0, widths[i], finalSize.Height)); x += widths[i]; }
            }
            return finalSize;
        }
    }

    // The sparkline in the panel draws itself in; the area fades up under it and the last point pops once the line arrives.
    private sealed class Spark : FrameworkElement
    {
        public IReadOnlyList<double> Points = [];
        public Brush Line = Brushes.Black, Area = Brushes.Transparent, Surface = Brushes.White;
        public double Draw = 1;
        public Spark() { Height = 56; IsHitTestVisible = false; }

        internal IReadOnlyList<Point> Layout(double width)
        {
            if (Points.Count == 0) { return []; }
            double low = Points.Min(), high = Points.Max(), span = high > low ? high - low : 1;
            int last = Math.Max(1, Points.Count - 1);
            return [.. Points.Select((value, i) => new Point(width * i / last, 4 + (1 - (value - low) / span) * 48))];
        }

        protected override void OnRender(DrawingContext dc)
        {
            var at = Layout(ActualWidth);
            if (at.Count == 0) { return; }
            double reach = ActualWidth * Math.Clamp(Draw, 0, 1);
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(at[0].X, 56), true, true);
                foreach (var p in at) { context.LineTo(p, false, false); }
                context.LineTo(new Point(at[^1].X, 56), false, false);
            }
            area.Freeze();
            dc.PushOpacity(Math.Clamp(Draw, 0, 1));
            dc.DrawGeometry(Area, null, area);
            dc.Pop();
            dc.PushClip(new RectangleGeometry(new Rect(-1, -2, reach + 2, 60)));
            var line = new StreamGeometry();
            using (var context = line.Open())
            {
                context.BeginFigure(at[0], false, false);
                foreach (var p in at.Skip(1)) { context.LineTo(p, true, true); }
            }
            line.Freeze();
            dc.DrawGeometry(null, new Pen(Line, 1.75) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);
            dc.Pop();
            if (Draw >= .98) { dc.DrawEllipse(Surface, new Pen(Line, 2), at[^1], 2.67, 2.67); }
        }
    }

    private const double PanelWidth = 248, PanelGap = 9, EdgeFade = 32;
    private readonly List<Live> _live = [];
    private readonly SolidColorBrush _ink = new(), _muted = new(), _secondary = new();
    private readonly Border _viewport = new() { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), ClipToBounds = false };
    private readonly Grid _fader = new() { ClipToBounds = true };
    private readonly Strip _strip;
    private readonly TranslateTransform _trackMove = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _panel = new() { Width = PanelWidth, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(14, 12, 14, 12), Opacity = 0, Visibility = Visibility.Collapsed };
    private readonly ScaleTransform _panelScale = new(1, 1);
    private readonly TranslateTransform _panelMove = new();
    private readonly Spark _spark = new();
    private readonly TextBlock _panelCaption = ArcText.Block("", 12, 16.8, "TextMuted"), _panelTrend = ArcText.Block("", 12, 16.8, "TextSecondary"), _low = ArcText.Block("", 12, 16.8), _high = ArcText.Block("", 12, 16.8);
    private readonly StackPanel _panelBody = new();
    private readonly FrameLoop _loop;
    private readonly GestureSpring _panelX = new(0, .05, .5), _panelGrow = new(0, .002, .02), _pull = new(0, .05, .5), _settle = new(0, .05, .5);
    // Normal priority: these answer the pointer, and must not wait behind rendering.
    private readonly DispatcherTimer _openTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(120) }, _closeTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(160) }, _flashTimer = new(DispatcherPriority.Normal);
    private Decorator? _host;
    private double _viewportWidth, _offset, _velocity, _drawTime, _dragStart, _dragOffset, _dragLast, _flick;
    private bool _fits = true, _hovering, _focused, _dragging, _pressed, _pinned, _panelShown;
    private long _dragStamp;
    private string? _openId;
    private Cell? _pending, _anchor;
    private Window? _window;

    static MetricTicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MetricTicker), new FrameworkPropertyMetadata(typeof(MetricTicker)));

    public MetricTicker()
    {
        _strip = new Strip(this) { RenderTransform = _trackMove, HorizontalAlignment = HorizontalAlignment.Left };
        _loop = new FrameLoop(Frame);
        _openTimer.Tick += (_, _) => { _openTimer.Stop(); if (_pending is { } cell) { Open(cell); } };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); if (!_pinned && !_focused && !_hovering) { Close(); } };
        _flashTimer.Tick += (_, _) => Settle();
        Loaded += (_, _) => { Attach(); Inks(); Reflow(); Kick(); };
        Unloaded += (_, _) => { _loop.Stop(); _openTimer.Stop(); _closeTimer.Stop(); _flashTimer.Stop(); Detach(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) { Kick(); } else { _loop.Stop(); } };
        ThemeWatch.Add(this, () => { Inks(); foreach (var live in _live) { foreach (var cell in live.Cells) { Paint(live, cell, false); } } PaintPanel(); });
    }

    internal bool LoopingForTest => Looping;
    internal bool FitsForTest => _fits;
    internal double OffsetForTest => _offset;
    internal double VelocityForTest => _velocity;
    internal string? OpenIdForTest => _openId;
    internal bool PinnedForTest => _pinned;
    internal bool PanelShownForTest => _panelShown;
    internal string CaptionForTest => _panelCaption.Text;
    internal string TrendForTest => _panelTrend.Text;
    internal string LowForTest => _low.Text;
    internal string HighForTest => _high.Text;
    internal double PanelXForTest => _panelX.Position;
    internal string SpokenForTest(string id) => Spoken(id);
    internal string NumberForTest(string id) => _live.First(live => live.Metric.Id == id).Primary.Number.Text;
    internal string DeltaForTest(string id) => _live.First(live => live.Metric.Id == id).Primary.DeltaNumber.Text;
    internal bool FlashingForTest(string id) => _live.First(live => live.Metric.Id == id).FlashUntil > DateTime.UtcNow;
    internal string ToneForTest(string id) { var live = _live.First(item => item.Metric.Id == id); return ToneOf(live).ToString(); }
    internal FrameworkElement CellForTest(string id) => _live.First(live => live.Metric.Id == id).Primary;
    internal FrameworkElement PanelForTest => _panel;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void HoverForTest(string id) { var cell = _live.First(live => live.Metric.Id == id).Primary; _hovering = true; Open(cell); Kick(); }
    internal void LeaveForTest() { _hovering = false; if (!_pinned && !_focused) { Close(); } Kick(); }
    internal void KeyForTest(Key key) => HandleKey(key);
    internal void DragForTest(double fromX, double toX, double seconds) { BeginDrag(fromX); MoveDrag(toX, seconds); EndDrag(); }
    internal void SettleForTest() => Settle(true);

    protected override AutomationPeer OnCreateAutomationPeer() => new StripPeer(this);

    private sealed class StripPeer(MetricTicker owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
        protected override string GetNameCore() => owner.Label;
    }

    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private bool Looping => !_fits && Overflow == TickerOverflow.Marquee && !Reduced;

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
        _viewport.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _viewport.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _viewport.ClipToBounds = false;
        _viewport.Clip = null;
        var strip = new Grid();
        strip.Children.Add(_strip);
        _fader.Children.Clear();
        _fader.Children.Add(strip);
        _viewport.Child = _fader;
        _fader.SizeChanged += (_, e) => { _viewportWidth = e.NewSize.Width; ClipFader(); _strip.InvalidateMeasure(); };
        _viewport.MouseEnter += (_, _) => { _hovering = true; Kick(); };
        _viewport.MouseLeave += (_, _) => { _hovering = false; Kick(); if (!_pinned && !_focused) { _closeTimer.Start(); } };
        _viewport.PreviewMouseLeftButtonDown += (_, e) => { _pressed = true; BeginDrag(e.GetPosition(_viewport).X); };
        _viewport.PreviewMouseMove += (_, e) =>
        {
            if (!_pressed) { return; }
            double x = e.GetPosition(_viewport).X;
            if (!_dragging && Math.Abs(x - _dragStart) > 4 && !_fits) { _dragging = true; _viewport.CaptureMouse(); Keyboard.ClearFocus(); }
            if (_dragging) { MoveDrag(x, Stopwatch()); }
        };
        _viewport.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            bool dragged = _dragging;
            _pressed = false;
            if (dragged) { EndDrag(); _viewport.ReleaseMouseCapture(); e.Handled = true; return; }
            if (FindCell(e.OriginalSource as DependencyObject) is { } cell) { cell.Focused.Pointer(); Target(cell).Focus(); Toggle(cell); }
        };
        _viewport.LostMouseCapture += (_, _) => { if (_dragging) { EndDrag(); } _pressed = false; };
        _viewport.PreviewMouseWheel += (_, e) =>
        {
            if (Looping || _fits) { return; }
            _offset = ClampScroll(_offset - e.Delta / 3d);
            Place();
            e.Handled = true;
        };
        _viewport.PreviewKeyDown += (_, e) => { if (HandleKey(e.Key)) { e.Handled = true; } };

        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        row.Children.Add(_panelCaption);
        Grid.SetColumn(_panelTrend, 1);
        row.Children.Add(_panelTrend);
        var range = new Grid { Margin = new Thickness(0, 8, 0, 0), ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var lowSet = new StackPanel { Orientation = Orientation.Horizontal };
        lowSet.Children.Add(new ContentControl { Content = ArcText.Block("Low", 12, 16.8, "TextMuted"), Margin = new Thickness(0, 0, 6, 0), Focusable = false });
        lowSet.Children.Add(_low);
        var highSet = new StackPanel { Orientation = Orientation.Horizontal };
        highSet.Children.Add(new ContentControl { Content = ArcText.Block("High", 12, 16.8, "TextMuted"), Margin = new Thickness(0, 0, 6, 0), Focusable = false });
        highSet.Children.Add(_high);
        Grid.SetColumn(highSet, 1);
        range.Children.Add(lowSet);
        range.Children.Add(highSet);
        _spark.Margin = new Thickness(0, 8, 0, 0);
        _panelBody.Children.Clear();
        _panelBody.Children.Add(row);
        _panelBody.Children.Add(_spark);
        _panelBody.Children.Add(range);
        _panel.Child = _panelBody;
        _panel.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _panel.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _panel.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        var transform = new TransformGroup();
        transform.Children.Add(_panelScale);
        transform.Children.Add(_panelMove);
        _panel.RenderTransform = transform;
        _overlay.Children.Clear();
        _overlay.Children.Add(_panel);
        Panel.SetZIndex(_overlay, 10);
        var layers = new Grid();
        layers.Children.Add(_viewport);
        layers.Children.Add(_overlay);
        _host.Child = layers;
        Sync();
    }

    private static double Stopwatch() => double.NaN;

    private void Attach()
    {
        Detach();
        _window = Window.GetWindow(this);
        if (_window is not null) { _window.PreviewMouseDown += OnWindowDown; }
    }

    private void Detach() { if (_window is not null) { _window.PreviewMouseDown -= OnWindowDown; _window = null; } }

    private void OnWindowDown(object sender, MouseButtonEventArgs e)
    {
        if (_pinned && !Outside.IsWithin(e.OriginalSource, _viewport)) { _pinned = false; Close(); }
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
    }

    private CultureInfo Culture { get { try { return CultureInfo.GetCultureInfo(Locale); } catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; } } }

    internal string FormatValue(TickerMetric metric, double value) => value.ToString(metric.Format ?? "N0", Culture) + metric.Unit;

    private static int DecimalsIn(string? format) => format is { Length: > 1 } && int.TryParse(format[1..], out int places) ? places : 0;

    private string DeltaText(TickerMetric metric, double previous, double value)
    {
        double change = Math.Abs(value - previous);
        if (metric.Delta == TickerDelta.Percent) { return (previous == 0 ? 0 : change / Math.Abs(previous) * 100).ToString("0.0", Culture) + "%"; }
        // A percent metric's raw difference reads in points, such as 0.3 pts.
        if (metric.Format is { Length: > 0 } format && char.ToUpperInvariant(format[0]) == 'P') { return (change * 100).ToString("N" + DecimalsIn(format), Culture) + " pts"; }
        return FormatValue(metric, change);
    }

    private static string Word(double previous, double value) => value > previous ? "up" : "down";

    private string Spoken(string id)
    {
        var live = _live.FirstOrDefault(item => item.Metric.Id == id);
        if (live is null) { return id; }
        string text = $"{live.Metric.Label}, {FormatValue(live.Metric, live.Metric.Value)}";
        return live.Previous is { } previous && previous != live.Metric.Value ? $"{text}, {Word(previous, live.Metric.Value)} {DeltaText(live.Metric, previous, live.Metric.Value)}" : text;
    }

    private static Tone ToneOf(Live live)
    {
        if (live.Previous is not { } previous || previous == live.Metric.Value) { return Tone.None; }
        bool rose = live.Metric.Value > previous;
        return rose == (live.Metric.Positive == TickerPositive.Up) ? Tone.Good : Tone.Bad;
    }

    // Changes are matched by id, so a new list keeps each metric's wheels and history.
    private void Sync()
    {
        if (_host is null) { return; }
        var list = Metrics ?? [];
        var kept = new List<Live>();
        bool structure = false;
        for (int i = 0; i < list.Count; i++)
        {
            var metric = list[i];
            var live = _live.FirstOrDefault(item => item.Metric.Id == metric.Id);
            if (live is null)
            {
                live = new Live { Metric = metric, Primary = new Cell(this, false), Copy = new Cell(this, true) };
                live.Primary.Id = live.Copy.Id = metric.Id;
                _strip.Children.Add(live.Primary);
                _strip.Children.Add(live.Copy);
                structure = true;
                live.Seen.Add(metric.Value);
                foreach (var cell in live.Cells) { cell.Title.Text = metric.Label; Render(live, cell, false, 1); }
            }
            else
            {
                double before = live.Metric.Value;
                live.Metric = metric;
                if (metric.Value != before)
                {
                    live.Previous = before;
                    live.Seen.Add(metric.Value);
                    int keep = Math.Max(2, HistoryLength);
                    if (live.Seen.Count > keep) { live.Seen.RemoveRange(0, live.Seen.Count - keep); }
                    live.FlashUntil = DateTime.UtcNow + FlashDuration;
                    foreach (var cell in live.Cells) { cell.Title.Text = metric.Label; Render(live, cell, true, metric.Value > before ? 1 : -1); }
                    ScheduleSettle();
                }
                else { foreach (var cell in live.Cells) { cell.Title.Text = metric.Label; Render(live, cell, false, 1); } }
            }
            live.Primary.Index = live.Copy.Index = i;
            kept.Add(live);
        }
        foreach (var gone in _live.Where(item => !kept.Contains(item)).ToList())
        {
            foreach (var cell in gone.Cells) { _strip.Children.Remove(cell); }
            structure = true;
        }
        _live.Clear();
        _live.AddRange(kept);
        for (int i = 0; i < _live.Count; i++)
        {
            foreach (var cell in _live[i].Cells) { cell.Separator.Visibility = i == 0 && !cell.IsCopy ? Visibility.Collapsed : Visibility.Visible; }
        }
        if (structure) { Roving(_live.FirstOrDefault()?.Primary); Reflow(); }
        if (_openId is not null && _live.All(item => item.Metric.Id != _openId)) { Close(); }
        if (_openId is not null) { PaintPanel(); }
        _strip.InvalidateMeasure();
    }

    private void Render(Live live, Cell cell, bool changed, int direction)
    {
        cell.Number.Set(FormatValue(live.Metric, live.Metric.Value), direction);
        bool hasDelta = live.Previous is { } previous && previous != live.Metric.Value;
        cell.Pill.Visibility = hasDelta ? Visibility.Visible : Visibility.Collapsed;
        if (hasDelta)
        {
            cell.Arrow.Icon = live.Metric.Value > live.Previous ? "arrow-up" : "arrow-down";
            cell.DeltaNumber.Set(DeltaText(live.Metric, live.Previous!.Value, live.Metric.Value), direction);
        }
        Paint(live, cell, changed);
        if (changed && Tween.Level(this) == ArcMotionLevel.Full && IsLoaded && hasDelta) { Flash(cell, direction); }
    }

    // The delta's tint deepens, its arrow slides in from the direction of travel and a thin ring expands and fades once.
    private static void Flash(Cell cell, int direction)
    {
        var move = (TranslateTransform)cell.Arrow.RenderTransform;
        move.Y = direction * 6;
        cell.Arrow.Opacity = 0;
        Tween.To(cell.Arrow, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard);
        Tween.Spring(move, TranslateTransform.YProperty, 0, .26);
        var scale = (ScaleTransform)cell.Ring.RenderTransform;
        cell.Ring.Opacity = .5;
        scale.ScaleX = scale.ScaleY = 1;
        Tween.To(scale, ScaleTransform.ScaleXProperty, 1.12, Tween.Considered, ArcEasing.Standard);
        Tween.To(scale, ScaleTransform.ScaleYProperty, 1.35, Tween.Considered, ArcEasing.Standard);
        Tween.To(cell.Ring, OpacityProperty, 0, Tween.Considered, ArcEasing.Standard);
    }

    private void Paint(Live live, Cell cell, bool animate)
    {
        var tone = ToneOf(live);
        if (tone == Tone.None) { return; }
        var full = Tween.Resolve(this, tone == Tone.Good ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.ArcDanger");
        bool flashing = live.FlashUntil > DateTime.UtcNow;
        bool cross = animate && IsLoaded && !Reduced;
        // At rest the delta sinks 30% toward the muted ink and loses its tint.
        var rest = OkColor.Mix(full, Tween.Resolve(this, "ArcWpf.Brush.TextMuted"), .7);
        Tween.Color(cell.Ink, flashing ? full : rest, Tween.Considered, cross);
        Tween.Color(cell.PillFill, flashing ? OkColor.Alpha(full, .14) : OkColor.Alpha(full, 0), Tween.Considered, cross);
        cell.RingInk.Color = full;
    }

    private void ScheduleSettle()
    {
        _flashTimer.Stop();
        _flashTimer.Interval = FlashDuration;
        _flashTimer.Start();
    }

    private void Settle(bool force = false)
    {
        _flashTimer.Stop();
        var now = DateTime.UtcNow;
        DateTime? next = null;
        foreach (var live in _live)
        {
            if (force) { live.FlashUntil = default; }
            if (live.FlashUntil == default) { foreach (var cell in live.Cells) { Paint(live, cell, !force); } continue; }
            if (live.FlashUntil > now) { next = next is null || live.FlashUntil < next ? live.FlashUntil : next; continue; }
            live.FlashUntil = default;
            foreach (var cell in live.Cells) { Paint(live, cell, true); }
        }
        if (next is { } at) { _flashTimer.Interval = at - now + TimeSpan.FromMilliseconds(10); _flashTimer.Start(); }
    }

    private void Reflow()
    {
        foreach (var live in _live)
        {
            live.Copy.Visibility = Looping ? Visibility.Visible : Visibility.Collapsed;
        }
        if (!Looping) { _offset = _fits ? 0 : ClampScroll(_offset); }
        ClipFader();
        _strip.InvalidateMeasure();
        Place();
        Kick();
    }

    // The marquee feathers its ends while the border stays crisp.
    private void ClipFader()
    {
        double width = _fader.ActualWidth, height = _fader.ActualHeight;
        if (width <= 0) { return; }
        _fader.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 17, 17);
        if (_fits) { _fader.OpacityMask = null; return; }
        double edge = Math.Min(EdgeFade / width, .5);
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, edge), new GradientStop(Colors.Black, 1 - edge), new GradientStop(Colors.Transparent, 1) } };
        mask.Freeze();
        _fader.OpacityMask = mask;
    }

    private double ClampScroll(double offset) => Math.Clamp(offset, 0, Math.Max(0, _strip.GroupWidth - _viewportWidth));

    private void Place()
    {
        double group = _strip.GroupWidth;
        if (Looping && group > 0) { _offset = (_offset % group + group) % group; }
        _trackMove.X = -_offset;
    }

    private void Kick()
    {
        if (IsLoaded && IsVisible && (Looping || _panelShown || _pull.Active || _settle.Active)) { _loop.Start(); }
    }

    private double DriftTarget => _hovering || _focused || _openId is not null || _dragging ? 0 : Speed;

    private bool Frame(double seconds)
    {
        if (!IsVisible) { return false; }
        bool running = false;
        if (Looping && !_dragging)
        {
            // Speed eases rather than stopping dead: resting on the strip glides it to a halt in about half a second.
            _velocity += (DriftTarget - _velocity) * (1 - Math.Exp(-Math.Max(0, Math.Min(seconds, .05)) / .16));
            if (_pull.Active) { _pull.Step(seconds); _offset = _pull.Position; _velocity = 0; }
            else { _offset += _velocity * seconds; }
            running = true;
        }
        if (!Looping && _settle.Active) { _settle.Step(seconds); _offset = _settle.Position; running |= _settle.Active; }
        if (!Looping && _pull.Active) { _pull.Step(seconds); _offset = ClampScroll(_pull.Position); running |= _pull.Active; }
        Place();
        running |= StepPanel(seconds);
        return running;
    }

    private void BeginDrag(double x) { _dragStart = x; _dragLast = x; _dragOffset = _offset; _flick = 0; _dragStamp = System.Diagnostics.Stopwatch.GetTimestamp(); _pull.Jump(_offset); _settle.Jump(_offset); }

    private void MoveDrag(double x, double seconds)
    {
        _dragging = true;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double dt = double.IsNaN(seconds) ? System.Diagnostics.Stopwatch.GetElapsedTime(_dragStamp, now).TotalSeconds : seconds;
        _dragStamp = now;
        if (dt > .0005) { _flick = -(x - _dragLast) / dt; }
        _dragLast = x;
        double next = _dragOffset - (x - _dragStart);
        _offset = Looping ? next : ClampScroll(next);
        Place();
    }

    private void EndDrag()
    {
        _dragging = false;
        _pressed = false;
        if (Looping) { _velocity = Math.Clamp(_flick, -1600, 1600); }
        else if (!_fits) { SnapScroll(); }
        Kick();
    }

    // Plain scrolling rests on a metric's edge.
    private void SnapScroll()
    {
        double x = 0, best = 0, nearest = double.MaxValue;
        foreach (double width in _strip.Natural)
        {
            if (Math.Abs(x - _offset) < nearest) { nearest = Math.Abs(x - _offset); best = x; }
            x += width;
        }
        _settle.Jump(_offset);
        _settle.To(ClampScroll(best), SpringTune.Smooth);
    }

    private static Cell? FindCell(DependencyObject? from)
    {
        for (var at = from; at is not null; at = at is Visual ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at)) { if (at is Cell cell) { return cell; } }
        return null;
    }

    private Cell Target(Cell cell) => cell.IsCopy ? _live.First(live => live.Metric.Id == cell.Id).Primary : cell;

    private void Roving(Cell? active)
    {
        foreach (var live in _live) { KeyboardNavigation.SetIsTabStop(live.Primary, ReferenceEquals(live.Primary, active)); live.Copy.IsHitTestVisible = true; KeyboardNavigation.SetIsTabStop(live.Copy, false); }
    }

    private void OnCellEnter(Cell cell)
    {
        _hovering = true;
        _closeTimer.Stop();
        _pending = cell;
        if (_panelShown) { Open(cell); } else { _openTimer.Start(); }
        Kick();
    }

    private void OnCellLeave()
    {
        _openTimer.Stop();
        _pending = null;
    }

    private void OnCellFocus(Cell cell)
    {
        _focused = true;
        _closeTimer.Stop();
        Roving(cell);
        Open(cell);
        if (cell.Focused.Shown) { PullIntoView(cell); }
        Kick();
    }

    private void OnCellBlur()
    {
        _focused = IsKeyboardFocusWithin && Keyboard.FocusedElement is Cell;
        if (!_focused && !_pinned && !_hovering) { _closeTimer.Start(); }
        Kick();
    }

    // Focusing a metric inside a moving marquee pulls it into view on the shortest path around the loop.
    private void PullIntoView(Cell cell)
    {
        if (_fits) { return; }
        double x = _live.Take(cell.Index).Sum(live => live.Primary.ActualWidth);
        double wanted = x - (_viewportWidth - cell.ActualWidth) / 2;
        double delta = wanted - _offset, group = _strip.GroupWidth;
        if (Looping && group > 0) { delta = ((delta % group) + group * 1.5) % group - group / 2; }
        if (Reduced) { _offset = Looping ? _offset + delta : ClampScroll(_offset + delta); Place(); return; }
        _pull.Jump(_offset);
        _pull.To(_offset + delta, SpringTune.Morph);
    }

    private void Toggle(Cell cell)
    {
        if (_pinned && _openId == cell.Id) { _pinned = false; Close(); return; }
        _pinned = true;
        Open(cell);
    }

    internal bool HandleKey(Key key)
    {
        var current = Keyboard.FocusedElement as Cell;
        if (current is null || current.IsCopy) { current = _live.FirstOrDefault(live => live.Metric.Id == _openId)?.Primary ?? _live.FirstOrDefault()?.Primary; }
        if (current is null || _live.Count == 0) { return false; }
        Cell At(int index) => _live[(index + _live.Count) % _live.Count].Primary;
        switch (key)
        {
            case Key.Left: Move(At(current.Index - 1)); return true;
            case Key.Right: Move(At(current.Index + 1)); return true;
            case Key.Home: Move(At(0)); return true;
            case Key.End: Move(At(_live.Count - 1)); return true;
            case Key.Enter or Key.Space: current.Focused.Key(); Toggle(current); return true;
            case Key.Escape: if (_openId is null) { return false; } _pinned = false; Close(); return true;
            default: return false;
        }
    }

    private void Move(Cell cell)
    {
        cell.Focused.Key();
        if (cell.IsLoaded) { cell.Focus(); }
        Roving(cell);
        Open(cell);
        PullIntoView(cell);
        cell.Focused.Key();
        Kick();
    }

    private void Open(Cell cell)
    {
        bool switching = _panelShown && _openId != cell.Id;
        bool fresh = !_panelShown;
        _openId = cell.Id;
        _anchor = cell;
        _pending = null;
        _openTimer.Stop();
        if (fresh)
        {
            _panelShown = true;
            _panel.Visibility = Visibility.Visible;
            _panelX.Jump(PanelTargetX());
            if (Reduced) { _panelGrow.Jump(1); } else { _panelGrow.Jump(0); _panelGrow.To(1, SpringTune.Morph); }
            ApplyPanelTransform();
        }
        if (fresh || switching) { Redraw(switching); }
        PaintPanel();
        foreach (var live in _live) { foreach (var item in live.Cells) { PaintWash(item); } }
        Peer(cell)?.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded);
        Kick();
    }

    private static AutomationPeer? Peer(UIElement element) => UIElementAutomationPeer.FromElement(element);

    private void Close()
    {
        if (!_panelShown) { _openId = null; return; }
        _openId = null;
        _anchor = null;
        _pinned = false;
        _panelShown = false;
        if (Reduced) { _panelGrow.Jump(0); ApplyPanelTransform(); _panel.Visibility = Visibility.Collapsed; }
        else { _panelGrow.To(0, SpringTune.Snappy); _loop.Start(); }
        foreach (var live in _live) { foreach (var item in live.Cells) { PaintWash(item); } }
    }

    private void PaintWash(Cell cell)
    {
        var fill = _openId == cell.Id ? Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted") : OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted"), 0);
        Tween.Color(cell.Wash, fill, Tween.Fast, IsLoaded && !Reduced);
    }

    private void Redraw(bool switching)
    {
        _drawTime = 0;
        _spark.Draw = Reduced ? 1 : 0;
        if (switching && !Reduced)
        {
            _panelBody.Opacity = 0;
            Tween.To(_panelBody, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard);
        }
    }

    private double PanelTargetX()
    {
        if (_anchor is null || !_anchor.IsVisible || _overlay.ActualWidth <= 0) { return _panelX.Position; }
        double center = _anchor.TransformToVisual(_overlay).Transform(new Point(_anchor.ActualWidth / 2, 0)).X;
        return Math.Clamp(center - PanelWidth / 2, 0, Math.Max(0, _overlay.ActualWidth - PanelWidth));
    }

    private void ApplyPanelTransform()
    {
        double grow = Math.Clamp(_panelGrow.Position, 0, 1.02);
        _panel.Opacity = Math.Clamp(grow, 0, 1);
        double scale = Reduced ? 1 : .86 + .14 * grow;
        _panelScale.ScaleX = _panelScale.ScaleY = scale;
        _panelScale.CenterX = PanelWidth / 2;
        _panelScale.CenterY = 0;
        Canvas.SetLeft(_panel, _panelX.Position);
        Canvas.SetTop(_panel, _viewport.ActualHeight + PanelGap);
    }

    // The panel follows its metric every frame; moving to another metric glides it across.
    private bool StepPanel(double seconds)
    {
        if (!_panelShown && !_panelGrow.Active && _panel.Visibility != Visibility.Visible) { return false; }
        double wanted = PanelTargetX();
        if (Reduced) { _panelX.Jump(wanted); } else { _panelX.Chase(wanted, SpringTune.Morph); }
        _panelX.Step(seconds);
        _panelGrow.Step(seconds);
        if (_panelShown && _spark.Draw < 1)
        {
            _drawTime += seconds;
            _spark.Draw = Math.Min(1, ArcEasing.Standard.Ease(Math.Min(1, _drawTime / Tween.Considered)));
            _spark.InvalidateVisual();
        }
        ApplyPanelTransform();
        if (!_panelShown && !_panelGrow.Active) { _panel.Visibility = Visibility.Collapsed; return false; }
        // Open and following: a frame loop that runs only while a panel is open.
        return _panelShown || _panelGrow.Active;
    }

    private void PaintPanel()
    {
        var live = _live.FirstOrDefault(item => item.Metric.Id == _openId);
        if (live is null) { return; }
        var points = live.Metric.History is { Count: > 0 } given ? given : live.Seen;
        _panelCaption.Text = live.Metric.Caption ?? $"Last {points.Count} updates";
        double first = points[0], last = points[^1];
        bool rising = last > first, falling = last < first;
        _panelTrend.Text = rising ? "Trending up" : falling ? "Trending down" : "Holding steady";
        bool good = (rising && live.Metric.Positive == TickerPositive.Up) || (falling && live.Metric.Positive == TickerPositive.Down);
        var tone = !rising && !falling ? Tween.Resolve(this, "ArcWpf.Brush.TextSecondary") : Tween.Resolve(this, good ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.ArcDanger");
        _spark.Line = new SolidColorBrush(tone);
        _spark.Area = new SolidColorBrush(OkColor.Alpha(tone, .1));
        _spark.Surface = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.Surface"));
        _spark.Points = [.. points];
        _low.Text = FormatValue(live.Metric, points.Min());
        _high.Text = FormatValue(live.Metric, points.Max());
        _spark.InvalidateVisual();
    }
}
