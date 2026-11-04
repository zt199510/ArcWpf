using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

/// <summary>A detent. A dragged thumb clicks into it and holds for a few pixels before letting go. A label prints under the track.</summary>
public sealed class ElasticSliderMark
{
    public double Value { get; set; }
    public string? Label { get; set; }
}

public sealed class ElasticSliderMarkCollection : Collection<ElasticSliderMark>;

// elastic-slider.tsx (frozen 2026-11-03): the thumb swells, the track stretches past either end, a tag hangs off the thumb and sways with its speed, and marks act as detents.
public class ElasticSlider : RangeBase
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ElasticSlider), new FrameworkPropertyMetadata("", (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ElasticSlider), new FrameworkPropertyMetadata(1d, (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly DependencyProperty LargeStepProperty = DependencyProperty.Register(nameof(LargeStep), typeof(double?), typeof(ElasticSlider), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(nameof(Marks), typeof(IList<ElasticSliderMark>), typeof(ElasticSlider), new FrameworkPropertyMetadata(null, (d, _) => ((ElasticSlider)d).BuildMarks()));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(Func<double, string>), typeof(ElasticSlider), new FrameworkPropertyMetadata(null, (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(ElasticSlider), new FrameworkPropertyMetadata(true, (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly DependencyProperty StartIconProperty = DependencyProperty.Register(nameof(StartIcon), typeof(object), typeof(ElasticSlider), new FrameworkPropertyMetadata(null, (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly DependencyProperty EndIconProperty = DependencyProperty.Register(nameof(EndIcon), typeof(object), typeof(ElasticSlider), new FrameworkPropertyMetadata(null, (d, _) => ((ElasticSlider)d).Copy()));
    public static readonly RoutedEvent ValueCommittedEvent = EventManager.RegisterRoutedEvent(nameof(ValueCommitted), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(ElasticSlider));

    /// <summary>Visible label; it also names the thumb.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    /// <summary>PageUp, PageDown, and Shift with an arrow move this far. Defaults to a tenth of the range.</summary>
    public double? LargeStep { get => (double?)GetValue(LargeStepProperty); set => SetValue(LargeStepProperty, value); }
    /// <summary>Detents. A dragged thumb clicks into each one. Labels print under the track.</summary>
    public IList<ElasticSliderMark>? Marks { get => (IList<ElasticSliderMark>?)GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    /// <summary>Formats the readout, the tag, and the spoken value.</summary>
    public Func<double, string>? Format { get => (Func<double, string>?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    /// <summary>Show the value beside the label. The tag over the thumb still appears while dragging or using keys.</summary>
    public bool ShowValue { get => (bool)GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    /// <summary>Icon before the track. It is pushed outward while the track is pulled past that end.</summary>
    public object? StartIcon { get => GetValue(StartIconProperty); set => SetValue(StartIconProperty, value); }
    public object? EndIcon { get => GetValue(EndIconProperty); set => SetValue(EndIconProperty, value); }
    /// <summary>Runs once a drag is released or a key changes the value.</summary>
    public event RoutedPropertyChangedEventHandler<double> ValueCommitted { add => AddHandler(ValueCommittedEvent, value); remove => RemoveHandler(ValueCommittedEvent, value); }

    private sealed class Drag { public double Grab; public readonly List<(double T, double P)> Samples = []; public double? Detent; public double Edge; public bool Touch; }
    private sealed class Tick { public required double Value; public required Ellipse View; public required SolidColorBrush Brush; public required ScaleTransform Scale; }
    private sealed class MarkView { public required ElasticSliderMark Mark; public required Border Hit; public required TrackedText Text; public required SolidColorBrush Brush; }

    private const double ThumbSize = 24, Half = ThumbSize / 2, TrackHeight = 8, RowHeight = 44, Stretch = 26, DetentPx = 7, BumpPx = 260;
    private static readonly SpringTune ThumbSpring = SpringTune.Snappy;
    // The stretch lets go with more life than anything else here: it is the one thing the user threw.
    private static readonly SpringTune SnapBack = SpringTune.Physical(.5, .42);
    private static readonly SpringTune TrailSpring = new(420, 30, .7);

    private readonly FrameLoop _loop;
    private readonly FocusVisible _focus;
    private readonly GestureSpring _pos = new(0, .002, .02), _lag = new(0, .002, .02), _over = new(0, .05, .5), _press = new(0, .002, .02), _trailed = new(0, .01, .1);
    private readonly Canvas _control = new() { Height = RowHeight, Background = Brushes.Transparent, Cursor = Cursors.Hand };
    private readonly Border _band = new() { Height = TrackHeight, CornerRadius = new CornerRadius(TrackHeight / 2) };
    private readonly Rectangle _fill = new() { Height = TrackHeight, RadiusX = TrackHeight / 2, RadiusY = TrackHeight / 2, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ScaleTransform _bandScale = new(), _thumbScale = new(), _tagScale = new();
    private readonly RotateTransform _tagRotate = new();
    private readonly TranslateTransform _tagLift = new(), _tagAt = new(), _thumbAt = new(), _startShift = new(), _endShift = new();
    private readonly Grid _thumb = new() { Width = ThumbSize, Height = ThumbSize };
    private readonly Ellipse _ring = new() { Width = ThumbSize + 8, Height = ThumbSize + 8, Margin = new Thickness(-4), StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Decorator _tag = new() { IsHitTestVisible = false, Opacity = 0, Visibility = Visibility.Hidden };
    private readonly Border _tagFace = new() { MinHeight = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 0, 12, 0) };
    private readonly ContentPresenter _start = new() { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }, _end = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly Canvas _marksRow = new() { Height = 20, Margin = new Thickness(Half, 0, Half, 0), Visibility = Visibility.Collapsed };
    private readonly List<Tick> _ticks = [];
    private readonly List<MarkView> _marks = [];
    private readonly SolidColorBrush _ink = new(), _secondary = new(), _tagInk = new();
    private readonly System.Windows.Threading.DispatcherTimer _lingerTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private TrackedText? _label, _readout, _tagText;
    private Drag? _drag;
    private double _latest, _goal, _lastOver, _lastOverAt, _overVelocity;
    private bool _keyed, _linger, _tagShown, _touchLift, _built;

    static ElasticSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ElasticSlider), new FrameworkPropertyMetadata(typeof(ElasticSlider)));
        MaximumProperty.OverrideMetadata(typeof(ElasticSlider), new FrameworkPropertyMetadata(100d));
    }

    public ElasticSlider()
    {
        _loop = new FrameLoop(Frame);
        _focus = new FocusVisible(this, ShowRing);
        _lingerTimer.Tick += (_, _) => { _lingerTimer.Stop(); _linger = false; Tag(); };
        _control.MouseLeftButtonDown += (_, e) => { var at = e.GetPosition(_control); Down(at.X, e.Timestamp, e.StylusDevice is not null); _control.CaptureMouse(); e.Handled = true; };
        _control.MouseMove += (_, e) => { if (_drag is not null) { Follow(_drag, RailAt(e.GetPosition(_control).X), e.Timestamp); } };
        _control.MouseLeftButtonUp += (_, e) => { End(e.Timestamp); _control.ReleaseMouseCapture(); };
        _control.LostMouseCapture += (_, e) => End(Environment.TickCount);
        _control.SizeChanged += (_, _) => Layout();
        LostKeyboardFocus += (_, _) => { if (_keyed) { _keyed = false; Tag(); } };
        IsEnabledChanged += (_, _) => { Opacity = IsEnabled ? 1 : .5; _control.Cursor = IsEnabled ? Cursors.Hand : Cursors.No; };
        Loaded += (_, _) => { Inks(); Copy(); };
        Unloaded += (_, _) => { _loop.Stop(); _lingerTimer.Stop(); };
        ThemeWatch.Add(this, () => { Inks(); Copy(); });
    }

    internal double ShownForTest => _pos.Position + _lag.Position;
    internal double OverForTest => _over.Position;
    internal double OverVelocityForTest => _over.Velocity;
    internal double PressForTest => _press.Position;
    internal bool DraggingForTest => _drag is not null;
    internal bool TagForTest => _tagShown;
    internal double RotateForTest => _tagRotate.Angle;
    internal string ReadoutForTest => _readout?.Text ?? "";
    internal double? DetentForTest => _drag?.Detent;
    internal double ThumbLeftForTest => _thumbAt.X;
    internal double TagCenterForTest => _tagAt.X + _tag.ActualWidth / 2;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void DownForTest(double x, double time) => Down(x, time, false);
    internal void MoveForTest(double x, double time) { if (_drag is not null) { Follow(_drag, RailAt(x), time); } }
    internal void UpForTest(double time) => End(time);
    internal void KeyForTest(Key key, bool shift = false) => Press(key, shift);
    internal void SettleForTest() { for (int index = 0; index < 400; index++) { _loop.Advance(1d / 60); } }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcRangePeer(this, AutomationControlType.Slider, () => Label, () => Minimum, () => Maximum, () => Value, () => Text(Value), value => { double next = SnapTo(value); Emit(next); Commit(next); }, () => SafeStep, () => Large);

    private double SafeStep => Step > 0 ? Step : 1;
    private double Span => Maximum - Minimum is var span && span != 0 ? span : 1;
    private int Decimals => Math.Max(GestureMath.DecimalsOf(SafeStep), GestureMath.DecimalsOf(Minimum));
    private double RailWidth => Math.Max(1, _control.ActualWidth - ThumbSize);
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private double Pct(double value) => (value - Minimum) / Span * 100;
    private double SnapTo(double value) => GestureMath.Snap(value, Minimum, Maximum, SafeStep, Decimals);
    private string Text(double value) => Format?.Invoke(value) ?? GestureMath.Grouped(value, Decimals);
    private double Large => LargeStep ?? Math.Max(SafeStep, SnapTo(Minimum + Span / 10) - Minimum);
    private double RailAt(double x) => (x - Half) / RailWidth * 100;
    private IEnumerable<ElasticSliderMark> MarkList => (Marks ?? []).Where(mark => mark.Value >= Minimum && mark.Value <= Maximum);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        Inks();
        _built = true;
        _latest = Value;
        _goal = Pct(Value);
        _pos.Jump(_goal);
        _trailed.Jump(_goal);

        _label = CopyMotion.Text("", 14, 1.4, -.14, FontWeights.Medium, _ink);
        _readout = CopyMotion.Text("", 14, 1.4, -.14, FontWeights.Normal, _secondary);
        _readout.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(_label);
        header.Children.Add(_readout);

        _band.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ControlTrack");
        _fill.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.ControlFill");
        _band.Child = _fill;
        _band.RenderTransform = _bandScale;
        _band.IsHitTestVisible = false;
        Canvas.SetLeft(_band, Half);
        Canvas.SetTop(_band, (RowHeight - TrackHeight) / 2);
        _control.Children.Add(_band);

        _tagFace.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        _tagFace.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Raised");
        _tagText = CopyMotion.Text("", 14, 1.4, -.14, FontWeights.Medium, _tagInk);
        _tagText.VerticalAlignment = VerticalAlignment.Center;
        _tagFace.Child = _tagText;
        _tag.Child = _tagFace;
        // It hangs from its bottom edge, so a swing pivots on the thumb.
        _tag.RenderTransformOrigin = new Point(.5, 1.3);
        _tag.RenderTransform = new TransformGroup { Children = { _tagScale, _tagRotate, _tagLift, _tagAt } };
        _tag.SizeChanged += (_, _) => Paint();
        _control.Children.Add(_tag);

        var dot = new Border { CornerRadius = new CornerRadius(Half), BorderThickness = new Thickness(.5) };
        dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ControlThumb");
        dot.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.SliderThumbBorder");
        dot.SetResourceReference(EffectProperty, "ArcWpf.Shadow.SliderThumb");
        _thumb.Children.Add(dot);
        _thumb.Children.Add(_ring);
        _thumb.RenderTransformOrigin = new Point(.5, .5);
        _thumb.RenderTransform = new TransformGroup { Children = { _thumbScale, _thumbAt } };
        _thumb.IsHitTestVisible = false;
        Canvas.SetTop(_thumb, (RowHeight - ThumbSize) / 2);
        _control.Children.Add(_thumb);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _start.RenderTransform = _startShift;
        _end.RenderTransform = _endShift;
        Grid.SetColumn(_control, 1);
        Grid.SetColumn(_end, 2);
        Grid.SetColumn(_marksRow, 1);
        Grid.SetRow(_marksRow, 1);
        body.Children.Add(_marksRow);
        body.Children.Add(_start);
        body.Children.Add(_end);
        body.Children.Add(_control);
        _marksRow.SizeChanged += (_, _) => PlaceMarks();

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        host.Child = root;
        BuildMarks();
        Copy();
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _tagInk.Color = Tween.Resolve(this, "ArcWpf.Brush.Background");
        _ring.Stroke = FocusVisible.RingBrush(this);
        foreach (var tick in _ticks) { Tween.Color(tick.Brush, TickColor(tick.Value <= Value), 0, false); }
        foreach (var mark in _marks) { Tween.Color(mark.Brush, MarkColor(mark), 0, false); }
    }

    private void ShowRing() => _ring.Visibility = _focus.Shown ? Visibility.Visible : Visibility.Collapsed;

    private void Copy()
    {
        if (!_built) { return; }
        _label!.Text = Label ?? "";
        _readout!.Text = Text(Value);
        _readout.Visibility = ShowValue ? Visibility.Visible : Visibility.Collapsed;
        _tagText!.Text = Text(Value);
        _start.Content = StartIcon;
        _end.Content = EndIcon;
        _start.Visibility = StartIcon is null ? Visibility.Collapsed : Visibility.Visible;
        _end.Visibility = EndIcon is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private Color TickColor(bool on)
    {
        var color = Tween.Resolve(this, on ? "ArcWpf.Brush.ControlGlyph" : "ArcWpf.Brush.Foreground");
        color.A = (byte)Math.Round(255 * (on ? .6 : .32));
        return color;
    }

    private Color MarkColor(MarkView view) => Tween.Resolve(this, view.Mark.Value == Value || view.Hit.IsMouseOver ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted");

    private void BuildMarks()
    {
        if (!_built) { return; }
        foreach (var tick in _ticks) { _control.Children.Remove(tick.View); }
        _ticks.Clear();
        _marks.Clear();
        _marksRow.Children.Clear();
        int at = _control.Children.IndexOf(_band) + 1;
        foreach (var mark in MarkList.Where(mark => mark.Value > Minimum && mark.Value < Maximum))
        {
            var brush = new SolidColorBrush(TickColor(mark.Value <= Value));
            var scale = new ScaleTransform();
            var view = new Ellipse { Width = 4, Height = 4, Fill = brush, IsHitTestVisible = false, RenderTransformOrigin = new Point(.5, .5), RenderTransform = scale };
            Canvas.SetTop(view, RowHeight / 2 - 2);
            _control.Children.Insert(at++, view);
            _ticks.Add(new Tick { Value = mark.Value, View = view, Brush = brush, Scale = scale });
        }
        foreach (var mark in MarkList.Where(mark => !string.IsNullOrEmpty(mark.Label)))
        {
            var brush = new SolidColorBrush();
            var text = CopyMotion.Text(mark.Label!, 12, 1.4, -.14, FontWeights.Normal, brush);
            var hit = new Border { Child = text, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var view = new MarkView { Mark = mark, Hit = hit, Text = text, Brush = brush };
            brush.Color = MarkColor(view);
            hit.MouseEnter += (_, _) => Tween.Color(brush, MarkColor(view), Tween.Fast, !Reduced);
            hit.MouseLeave += (_, _) => Tween.Color(brush, MarkColor(view), Tween.Fast, !Reduced);
            hit.MouseLeftButtonUp += (_, e) => { ClickMark(mark.Value); e.Handled = true; };
            hit.SizeChanged += (_, _) => PlaceMarks();
            _marks.Add(view);
            _marksRow.Children.Add(hit);
        }
        _marksRow.Visibility = _marks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Layout();
    }

    private void ClickMark(double value)
    {
        if (!IsEnabled || value == _latest) { return; }
        Emit(value);
        Pulse(value);
        Commit(value);
    }

    private void PlaceMarks()
    {
        double width = _marksRow.ActualWidth;
        foreach (var view in _marks)
        {
            double own = view.Hit.ActualWidth;
            double left = Pct(view.Mark.Value) / 100 * width;
            // Marks at the ends stay inside the component.
            double shift = view.Mark.Value == Minimum ? Math.Max(-own / 2, -Half) : view.Mark.Value == Maximum ? Math.Min(-own / 2, -own + Half) : -own / 2;
            Canvas.SetLeft(view.Hit, left + shift);
        }
    }

    private void Layout()
    {
        if (!_built) { return; }
        double width = RailWidth;
        _band.Width = width;
        foreach (var tick in _ticks) { Canvas.SetLeft(tick.View, Half + Pct(tick.Value) / 100 * width - 2); }
        PlaceMarks();
        Paint();
    }

    protected override void OnMinimumChanged(double oldMinimum, double newMinimum) { base.OnMinimumChanged(oldMinimum, newMinimum); Rescale(); }
    protected override void OnMaximumChanged(double oldMaximum, double newMaximum) { base.OnMaximumChanged(oldMaximum, newMaximum); Rescale(); }

    private void Rescale()
    {
        if (!_built) { return; }
        _goal = Pct(Value);
        _lag.Jump(0);
        _pos.Jump(_goal);
        _trailed.Jump(_goal);
        BuildMarks();
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (!_built) { return; }
        _latest = newValue;
        Copy();
        bool cross = !Reduced && IsLoaded;
        foreach (var tick in _ticks) { Tween.Color(tick.Brush, TickColor(tick.Value <= newValue), Tween.Fast, cross); }
        foreach (var mark in _marks) { Tween.Color(mark.Brush, MarkColor(mark), Tween.Fast, cross); }
        ArcRangePeer.Notify(this, oldValue, newValue);
        // Values from outside a drag (keys, props) spring the thumb to their place from wherever it is on screen.
        double target = Pct(newValue);
        if (_drag is not null || _goal == target) { return; }
        _goal = target;
        double from = _pos.Position + _lag.Position;
        _lag.Jump(0);
        _pos.Jump(from);
        if (Reduced || !IsLoaded) { _pos.Jump(target); _trailed.Jump(target); Paint(); return; }
        _pos.To(target, ThumbSpring);
        _loop.Start();
    }

    private void Emit(double next)
    {
        if (_latest == next) { return; }
        _latest = next;
        Value = next;
    }

    private void Commit(double next) => RaiseEvent(new RoutedPropertyChangedEventArgs<double>(next, next) { RoutedEvent = ValueCommittedEvent });

    // A detent answers with a small pop of its tick.
    private void Pulse(double mark)
    {
        var tick = _ticks.FirstOrDefault(tick => tick.Value == mark);
        if (tick is null || Reduced) { return; }
        void Half(double to, double seconds, Action? done = null)
        {
            Tween.To(tick.Scale, ScaleTransform.ScaleYProperty, to, seconds, ArcEasing.Enter);
            Tween.To(tick.Scale, ScaleTransform.ScaleXProperty, to, seconds, ArcEasing.Enter, 0, done);
        }
        Half(2.1, .17, () => Half(1, .17));
    }

    // Moves the thumb to a new resting spot through a spring on the catch-up offset, so the jump reads as travel.
    private void Glide(double placed)
    {
        double from = _pos.Position + _lag.Position;
        _pos.Jump(placed);
        if (Reduced) { _lag.Jump(0); return; }
        _lag.Jump(from - placed);
        _lag.To(0, ThumbSpring);
    }

    private double? DetentAt(double percent)
    {
        double? best = null;
        double gap = DetentPx + 1;
        foreach (var mark in MarkList)
        {
            double distance = Math.Abs((percent - Pct(mark.Value)) / 100 * RailWidth);
            if (distance <= DetentPx && distance < gap) { best = mark.Value; gap = distance; }
        }
        return best;
    }

    private void Follow(Drag state, double at, double time, bool first = false)
    {
        double raw = at - state.Grab, edge = Math.Clamp(raw, 0, 100);
        double over = Reduced ? 0 : GestureMath.Rubber((raw - edge) / 100 * RailWidth, Stretch);
        double elapsed = (time - _lastOverAt) / 1000;
        _overVelocity = elapsed > 0 ? (over - _lastOver) / elapsed : 0;
        (_lastOver, _lastOverAt) = (over, time);
        _over.Jump(over);
        double? detent = DetentAt(edge);
        double placed = detent is { } mark ? Pct(mark) : edge;
        if (first || detent != state.Detent)
        {
            if (detent is { } hit && !first) { Pulse(hit); }
            Glide(placed);
        }
        else { _pos.Jump(placed); }
        state.Detent = detent;
        state.Edge = edge;
        state.Samples.Add((time, placed));
        if (state.Samples.Count > 6) { state.Samples.RemoveAt(0); }
        Emit(detent ?? SnapTo(Minimum + edge / 100 * Span));
        Paint();
        _loop.Start();
    }

    private void Down(double x, double time, bool touch)
    {
        if (!IsEnabled || _drag is not null) { return; }
        double at = RailAt(x), from = _pos.Position + _lag.Position;
        double thumbX = Half + from / 100 * RailWidth + _over.Position;
        // A generous invisible hit area for fingertips: 12px beyond the thumb.
        bool grabbed = Math.Abs(x - thumbX) <= Half + 12;
        _lag.Jump(0);
        _pos.Jump(from);
        var state = new Drag { Grab = grabbed ? at - from : 0, Edge = from, Touch = touch };
        _drag = state;
        _touchLift = touch;
        _lingerTimer.Stop();
        _linger = false;
        _focus.Pointer();
        Focus();
        Tag();
        if (Reduced) { _press.Jump(1); Tween.Set(_thumbScale, ScaleTransform.ScaleXProperty, 1.28); Tween.Set(_thumbScale, ScaleTransform.ScaleYProperty, 1.28); }
        else
        {
            _press.To(1, SpringTune.Snappy);
            Tween.Spring(_thumbScale, ScaleTransform.ScaleXProperty, 1.28, Tween.Snappy);
            Tween.Spring(_thumbScale, ScaleTransform.ScaleYProperty, 1.28, Tween.Snappy);
        }
        _lastOverAt = time;
        _lastOver = _over.Position;
        // A grabbed thumb stays put under the finger; a pressed track pulls the thumb over on a spring.
        Follow(state, grabbed ? from + state.Grab : at, time, true);
    }

    private void End(double time)
    {
        if (_drag is not { } state) { return; }
        _drag = null;
        var (first, last) = state.Samples.Count > 0 ? (state.Samples[0], state.Samples[^1]) : ((T: 0d, P: 0d), (T: 0d, P: 0d));
        double elapsed = (last.T - first.T) / 1000;
        double velocity = (elapsed > .008 && time - last.T < 60 ? (last.P - first.P) / elapsed : 0) + _lag.Velocity;
        double next = state.Detent ?? SnapTo(Minimum + state.Edge / 100 * Span);
        double from = _pos.Position + _lag.Position, target = Pct(next);
        _lag.Jump(0);
        _pos.Jump(from);
        _goal = target;
        if (Reduced)
        {
            _pos.Jump(target);
            _over.Jump(0);
            _press.Jump(0);
            Tween.Set(_thumbScale, ScaleTransform.ScaleXProperty, 1);
            Tween.Set(_thumbScale, ScaleTransform.ScaleYProperty, 1);
            Paint();
        }
        else
        {
            _pos.To(target, ThumbSpring, velocity);
            _over.To(0, SnapBack, time - _lastOverAt < 30 ? _overVelocity : 0);
            _press.To(0, SpringTune.Snappy);
            Tween.Spring(_thumbScale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(_thumbScale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            _loop.Start();
        }
        Emit(next);
        _linger = true;
        _lingerTimer.Stop();
        _lingerTimer.Start();
        Tag();
        Commit(next);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && Press(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) { e.Handled = true; }
    }

    private bool Press(Key key, bool shift)
    {
        if (!IsEnabled) { return false; }
        double now = _latest, step = SafeStep, large = Large;
        bool arrow = key is Key.Left or Key.Right or Key.Up or Key.Down;
        double? move = key switch { Key.Right or Key.Up => step, Key.Left or Key.Down => -step, Key.PageUp => large, Key.PageDown => -large, _ => null };
        double wanted;
        if (key == Key.Home) { wanted = Minimum; }
        else if (key == Key.End) { wanted = Maximum; }
        else if (move is { } amount) { wanted = now + (shift && arrow ? Math.Sign(amount) * large : amount); }
        else { return false; }
        _focus.Key();
        _keyed = true;
        Tag();
        double next = SnapTo(wanted);
        // At a limit the track still stretches toward the press and snaps back, so the key visibly lands on something.
        if (next == now)
        {
            int toward = Math.Sign(wanted - now);
            if (toward != 0 && !Reduced) { _over.To(0, SnapBack, toward * BumpPx); _loop.Start(); }
            return true;
        }
        Emit(next);
        if (MarkList.Any(mark => mark.Value == next)) { Pulse(next); }
        Commit(next);
        return true;
    }

    private void Tag()
    {
        bool show = _drag is not null || _keyed || _linger;
        if (show == _tagShown || !_built) { return; }
        _tagShown = show;
        // While the tag rides the thumb it carries the value, so the readout steps back instead of competing with it.
        double quick = Tween.Level(this) == ArcMotionLevel.Off || !IsLoaded ? 0 : Tween.Fast;
        Tween.To(_readout!, OpacityProperty, show ? 0 : 1, quick, ArcEasing.Standard);
        var level = Tween.Level(this);
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            Tween.Set(_tag, OpacityProperty, show ? 1 : 0);
            _tag.Visibility = show ? Visibility.Visible : Visibility.Hidden;
            return;
        }
        if (show)
        {
            _tag.Visibility = Visibility.Visible;
            if (level == ArcMotionLevel.Reduced)
            {
                Tween.To(_tag, OpacityProperty, 1, .15, null);
                return;
            }
            // Re-entering while the tag is on its way out continues from where it is.
            if (_tag.Opacity <= 0)
            {
                _tagScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _tagScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _tagLift.BeginAnimation(TranslateTransform.YProperty, null);
                _tagScale.ScaleX = _tagScale.ScaleY = .7;
                _tagLift.Y = 6;
            }
            Tween.To(_tag, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            TextMotion.Blur(_tag, 2, 0, Tween.Fast, ArcEasing.Enter);
            Tween.Spring(_tagScale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(_tagScale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            Tween.Spring(_tagLift, TranslateTransform.YProperty, 0, Tween.Snappy);
            return;
        }
        void Gone() { if (!_tagShown) { _tag.Visibility = Visibility.Hidden; } }
        if (level == ArcMotionLevel.Reduced) { Tween.To(_tag, OpacityProperty, 0, Tween.Instant, null, 0, Gone); return; }
        Tween.To(_tag, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, 0, Gone);
        TextMotion.Blur(_tag, 0, 2, Tween.Instant, ArcEasing.Standard);
        Tween.To(_tagScale, ScaleTransform.ScaleXProperty, .85, Tween.Instant, ArcEasing.Standard);
        Tween.To(_tagScale, ScaleTransform.ScaleYProperty, .85, Tween.Instant, ArcEasing.Standard);
        Tween.To(_tagLift, TranslateTransform.YProperty, 4, Tween.Instant, ArcEasing.Standard);
    }

    private bool Frame(double seconds)
    {
        _pos.Step(seconds);
        _lag.Step(seconds);
        _over.Step(seconds);
        _press.Step(seconds);
        // The tag trails the thumb on a soft spring, so it lags more the faster the thumb moves.
        _trailed.Chase(_pos.Position + _lag.Position, TrailSpring);
        _trailed.Step(seconds);
        Paint();
        return _pos.Active || _lag.Active || _over.Active || _press.Active || _trailed.Active;
    }

    private void Paint()
    {
        if (!_built) { return; }
        double width = RailWidth, shown = _pos.Position + _lag.Position, over = _over.Position;
        _fill.Width = Math.Clamp(shown, 0, 100) / 100 * width;
        // The band grows away from its anchored end and thins as it does, like a band pulled tight. A press thickens it a little.
        _bandScale.CenterX = over < 0 ? width : 0;
        _bandScale.CenterY = TrackHeight / 2;
        _bandScale.ScaleX = 1 + Math.Abs(over) / width;
        _bandScale.ScaleY = (1 + _press.Position * .45) * (1 - Math.Abs(over) / Stretch * .32);
        _startShift.X = Math.Min(0, over);
        _endShift.X = Math.Max(0, over);
        _thumbAt.X = shown / 100 * width + over;
        double at = Reduced ? shown : _trailed.Position;
        // The tag centers on the thumb until it would pass the component's edge, then holds there.
        double face = _tag.ActualWidth, center = Math.Clamp(at / 100 * width, Math.Min(face / 2 - Half, width + Half - face / 2), Math.Max(face / 2 - Half, width + Half - face / 2)) + over;
        _tagAt.X = Half + center - face / 2;
        _tagAt.Y = RowHeight / 2 - (Half + (_touchLift ? 20 : 10)) - _tag.ActualHeight;
        _tagRotate.Angle = Reduced ? 0 : Math.Clamp(-_trailed.Velocity / 100 * width * .009, -11, 11);
    }
}
