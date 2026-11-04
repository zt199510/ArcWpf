using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

// Digits keyed by place value from the right, so a column keeps its identity as the number grows and each one rolls on its own.
// A column that appears or leaves opens or closes its width on a spring, so the centered readout never jumps a digit in one frame.
internal sealed class RulerDigits : FrameworkElement
{
    private sealed class Column
    {
        public required string Key;
        public required char Glyph;
        public required double Natural;
        public required (int Place, int Run) Order;
        public GestureSpring Width = new(0, .05, .5), Fade = new(0, .005, .05);
        public bool Digit => Glyph is >= '0' and <= '9';
        public bool Leaving;
        public double From, To, Pos, Elapsed = Tween.Standard;
    }

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(RulerDigits), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    private readonly double _size, _tracking, _line;
    private readonly List<Column> _columns = [];
    private readonly FrameLoop _loop;
    private readonly LinearGradientBrush _edge;
    private string _text = "";

    public RulerDigits(double size, double trackingPx, double lineRatio)
    {
        (_size, _tracking, _line) = (size, trackingPx, size * lineRatio);
        IsHitTestVisible = false;
        _loop = new FrameLoop(Frame);
        Unloaded += (_, _) => { _loop.Stop(); FinishAll(); };
        // Digits fade through the column's top and bottom tenths as they roll.
        _edge = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, 0), EndPoint = new Point(0, _line), GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, .1), new GradientStop(Colors.Black, .9), new GradientStop(Colors.Transparent, 1) } };
        _edge.Freeze();
    }

    internal string TextForTest => _text;
    internal IReadOnlyList<string> KeysForTest => [.. _columns.Where(column => !column.Leaving).Select(column => column.Key)];
    internal int LeavingForTest => _columns.Count(column => column.Leaving);
    internal double? RollForTest(string key) => _columns.FirstOrDefault(column => column.Key == key)?.Pos;
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    public double Baseline => (_line - Face("0").Height) / 2 + Face("0").Baseline;

    private FormattedText Face(string text) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), _size, (Brush)GetValue(ForegroundProperty), VisualTreeHelper.GetDpi(this).PixelsPerDip);

    public void Set(string text)
    {
        if (text == _text && _columns.Count > 0) { return; }
        bool first = _columns.Count == 0, moving = !first && IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        _text = text;
        var wanted = new HashSet<string>();
        int place = text.Count(char.IsAsciiDigit), run = 0;
        foreach (char glyph in text)
        {
            bool digit = char.IsAsciiDigit(glyph);
            if (digit) { place--; }
            string key = digit ? $"d{place}" : $"s{place}-{run}";
            (int, int) order = (-place, digit ? 0 : 1 + run);
            run = digit ? 0 : run + 1;
            wanted.Add(key);
            var column = _columns.FirstOrDefault(item => item.Key == key);
            if (column is null)
            {
                string shape = digit ? "0" : glyph.ToString();
                column = new Column { Key = key, Glyph = glyph, Natural = Face(shape).WidthIncludingTrailingWhitespace + _tracking, Order = order };
                column.Pos = column.To = column.From = digit ? glyph - '0' : 0;
                _columns.Add(column);
                if (!moving) { column.Width.Jump(column.Natural); column.Fade.Jump(1); }
            }
            column.Leaving = false;
            column.Glyph = glyph;
            if (digit && column.To != glyph - '0')
            {
                column.From = column.Pos;
                column.To = glyph - '0';
                column.Elapsed = moving ? 0 : Tween.Standard;
                if (!moving) { column.Pos = column.To; }
            }
            if (moving) { column.Width.To(column.Natural, SpringTune.Morph); column.Fade.To(1, SpringTune.Morph); }
        }
        foreach (var column in _columns.Where(column => !wanted.Contains(column.Key)).ToList())
        {
            if (!moving) { _columns.Remove(column); continue; }
            column.Leaving = true;
            column.Width.To(0, SpringTune.Morph);
            column.Fade.To(0, SpringTune.Morph);
        }
        _columns.Sort((a, b) => a.Order.CompareTo(b.Order));
        if (moving) { _loop.Start(); }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void FinishAll()
    {
        _columns.RemoveAll(column => column.Leaving);
        foreach (var column in _columns) { column.Width.Jump(column.Natural); column.Fade.Jump(1); column.Pos = column.To; column.Elapsed = Tween.Standard; }
    }

    // A transition on the strip: a new digit retargets from wherever the strip is rather than restarting.
    private bool Frame(double seconds)
    {
        bool running = false;
        foreach (var column in _columns)
        {
            column.Width.Step(seconds);
            column.Fade.Step(seconds);
            if (column.Elapsed < Tween.Standard)
            {
                column.Elapsed = Math.Min(Tween.Standard, column.Elapsed + seconds);
                column.Pos = column.From + (column.To - column.From) * ArcEasing.Standard.Ease(column.Elapsed / Tween.Standard);
                running = true;
            }
            running |= column.Width.Active || column.Fade.Active;
        }
        _columns.RemoveAll(column => column.Leaving && !column.Width.Active);
        InvalidateMeasure();
        InvalidateVisual();
        return running;
    }

    protected override Size MeasureOverride(Size availableSize) => new(_columns.Sum(column => Math.Max(0, column.Width.Position)), _line);

    protected override void OnRender(DrawingContext dc)
    {
        double x = 0;
        foreach (var column in _columns)
        {
            double width = Math.Max(0, column.Width.Position);
            if (width > 0)
            {
                dc.PushClip(new RectangleGeometry(new Rect(x, 0, width, _line)));
                dc.PushOpacity(Math.Clamp(column.Fade.Position, 0, 1));
                if (column.Digit)
                {
                    dc.PushOpacityMask(_edge);
                    for (int digit = (int)Math.Floor(column.Pos); digit <= (int)Math.Ceiling(column.Pos); digit++)
                    {
                        if (digit is < 0 or > 9) { continue; }
                        var face = Face(digit.ToString(CultureInfo.InvariantCulture));
                        dc.DrawText(face, new Point(x + (column.Natural - face.WidthIncludingTrailingWhitespace - _tracking) / 2, (digit - column.Pos) * _line + (_line - face.Height) / 2));
                    }
                    dc.Pop();
                }
                else
                {
                    var face = Face(column.Glyph.ToString());
                    dc.DrawText(face, new Point(x, (_line - face.Height) / 2));
                }
                dc.Pop();
                dc.Pop();
            }
            x += width;
        }
    }
}

// ruler-slider.tsx (frozen 2026-11-03): a ruler of ticks under a fixed needle. Drag it 1:1, flick it to coast onto a tick, pull it past an end and it rubber-bands back, or tap a spot to glide there.
public class RulerSlider : RangeBase
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(RulerSlider), new FrameworkPropertyMetadata("", (d, _) => ((RulerSlider)d).Copy()));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(RulerSlider), new FrameworkPropertyMetadata(1d, (d, _) => ((RulerSlider)d).Rescale()));
    public static readonly DependencyProperty LargeStepProperty = DependencyProperty.Register(nameof(LargeStep), typeof(double?), typeof(RulerSlider), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty MajorEveryProperty = DependencyProperty.Register(nameof(MajorEvery), typeof(int), typeof(RulerSlider), new FrameworkPropertyMetadata(10, (d, _) => ((RulerSlider)d).Rescale()));
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(RulerSlider), new FrameworkPropertyMetadata(10d, (d, _) => ((RulerSlider)d).Rescale()));
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(Func<double, string>), typeof(RulerSlider), new FrameworkPropertyMetadata(null, (d, _) => ((RulerSlider)d).Rescale()));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(RulerSlider), new FrameworkPropertyMetadata(null, (d, _) => ((RulerSlider)d).Copy()));
    public static readonly RoutedEvent ValueCommittedEvent = EventManager.RegisterRoutedEvent(nameof(ValueCommitted), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(RulerSlider));

    /// <summary>Visible label; it also names the control.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The value of one small tick. The tape always rests on a tick.</summary>
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    /// <summary>PageUp, PageDown, and Shift with an arrow move this far. Defaults to one labelled tick.</summary>
    public double? LargeStep { get => (double?)GetValue(LargeStepProperty); set => SetValue(LargeStepProperty, value); }
    /// <summary>Small ticks between labelled ticks.</summary>
    public int MajorEvery { get => (int)GetValue(MajorEveryProperty); set => SetValue(MajorEveryProperty, value); }
    /// <summary>Pixels between small ticks. Wider spacing gives finer control.</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    /// <summary>Formats the readout, the tick labels, and the spoken value.</summary>
    public Func<double, string>? Format { get => (Func<double, string>?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    /// <summary>Unit shown after the readout and spoken with the value, such as "cm".</summary>
    public string? Unit { get => (string?)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    /// <summary>Runs once the tape comes to rest after a drag, a flick, a tap, or a key.</summary>
    public event RoutedPropertyChangedEventHandler<double> ValueCommitted { add => AddHandler(ValueCommittedEvent, value); remove => RemoveHandler(ValueCommittedEvent, value); }

    private sealed class Tape(RulerSlider owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            // Clipped, so the edge fade maps onto the viewport and not onto ticks that spill past it.
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            owner.DrawTape(dc, ActualWidth);
            dc.Pop();
        }
    }

    private enum Flight { None, Spring, Inertia }
    private sealed class Drag { public double X; public double From; public bool Moved; public readonly List<(double T, double V)> Samples = []; }

    private const double HeightOfRuler = 72, Stretch = 44, TapSlop = 4, Power = .42, TimeConstant = .26;
    private static readonly SpringTune GlideSpring = SpringTune.Snappy, Home = SpringTune.Physical(.5, .1);

    private readonly FrameLoop _loop;
    private readonly FocusVisible _focus;
    private readonly GestureSpring _spring = new(0);
    private readonly Tape _tape;
    private readonly Border _viewport = new() { Height = HeightOfRuler, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Cursor = Cursors.Hand };
    private readonly Border _ring = new() { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(22), Margin = new Thickness(-4), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Grid _inner = new();
    private readonly RulerDigits _rolling = new(28, -.14, 1.1);
    private readonly DispatcherTimer _wheelTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly SolidColorBrush _secondary = new(), _muted = new();
    private TrackedText? _label, _unit;
    private HwndSource? _source;
    private Drag? _drag;
    private Flight _flight;
    private double _pos, _latest, _inertiaOrigin, _inertiaAmp, _inertiaTarget, _inertiaT;
    private bool _coasting, _built, _dragging;

    static RulerSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RulerSlider), new FrameworkPropertyMetadata(typeof(RulerSlider)));
        MaximumProperty.OverrideMetadata(typeof(RulerSlider), new FrameworkPropertyMetadata(100d));
    }

    public RulerSlider()
    {
        _loop = new FrameLoop(Frame);
        _tape = new Tape(this);
        _focus = new FocusVisible(this, () => _ring.Visibility = _focus!.Shown ? Visibility.Visible : Visibility.Collapsed);
        _viewport.MouseLeftButtonDown += (_, e) => { Down(e.GetPosition(_viewport).X, e.Timestamp); _viewport.CaptureMouse(); e.Handled = true; };
        _viewport.MouseMove += (_, e) => Move(e.GetPosition(_viewport).X, e.Timestamp);
        _viewport.MouseLeftButtonUp += (_, e) => { End(e.GetPosition(_viewport).X, e.Timestamp, true); _viewport.ReleaseMouseCapture(); };
        _viewport.LostMouseCapture += (_, _) => End(0, Environment.TickCount, false);
        _wheelTimer.Tick += (_, _) => { _wheelTimer.Stop(); GlideTo(SnapTo(_pos)); };
        IsEnabledChanged += (_, _) => { Opacity = IsEnabled ? 1 : .5; _viewport.Cursor = IsEnabled ? Cursors.Hand : Cursors.No; };
        Loaded += (_, _) => { Inks(); Hook(); };
        Unloaded += (_, _) => { Unhook(); StopFlight(); _wheelTimer.Stop(); };
        ThemeWatch.Add(this, () => { Inks(); _tape.InvalidateVisual(); });
    }

    internal double PositionForTest => _pos;
    internal bool CoastingForTest => _coasting;
    internal bool FlyingForTest => _flight != Flight.None;
    internal bool DraggingForTest => _dragging;
    internal RulerDigits RollingForTest => _rolling;
    internal string SpokenForTest => Spoken;
    internal double TapeOffsetForTest => TapeOffset(_tape.ActualWidth);
    internal void AdvanceForTest(double seconds) => _loop.Advance(seconds);
    internal void SettleForTest() { for (int index = 0; index < 600; index++) { _loop.Advance(1d / 60); } }
    internal void DownForTest(double x, double time) => Down(x, time);
    internal void MoveForTest(double x, double time) => Move(x, time);
    internal void UpForTest(double x, double time) => End(x, time, true);
    internal void CancelForTest(double time) => End(0, time, false);
    internal void KeyForTest(Key key, bool shift = false) => Press(key, shift);
    internal void WheelForTest(double deltaX) => Wheel(deltaX);
    internal void WheelSettleForTest() { _wheelTimer.Stop(); GlideTo(SnapTo(_pos)); }
    internal FrameworkElement ViewportForTest => _viewport;

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcRangePeer(this, AutomationControlType.Slider, () => Label, () => Minimum, () => Maximum, () => SnapTo(Value), () => Spoken, value => GlideTo(SnapTo(value)), () => SafeStep, () => Large);

    private double SafeStep => Step > 0 ? Step : 1;
    private int Decimals => Math.Max(GestureMath.DecimalsOf(SafeStep), GestureMath.DecimalsOf(Minimum));
    private double PerUnit => Spacing / SafeStep;
    private double Large => LargeStep ?? SafeStep * MajorEvery;
    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private double SnapTo(double value) => GestureMath.Snap(value, Minimum, Maximum, SafeStep, Decimals);
    private string Text(double value) => Format?.Invoke(value) ?? GestureMath.Grouped(value, Decimals);
    private string Spoken => string.IsNullOrEmpty(Unit) ? Text(SnapTo(Value)) : $"{Text(SnapTo(Value))} {Unit}";

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        Inks();
        _built = true;
        _latest = SnapTo(Value);
        _pos = _latest;
        _label = CopyMotion.Text("", 14, 1.4, -.14, FontWeights.Normal, _secondary);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.Margin = new Thickness(0, 0, 0, 4);
        _rolling.SetResourceReference(RulerDigits.ForegroundProperty, "ArcWpf.Brush.Foreground");
        _unit = CopyMotion.Text("", 16, 1.1, -.14, FontWeights.Medium, _secondary);
        _unit.VerticalAlignment = VerticalAlignment.Top;
        var readout = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
        readout.Children.Add(_rolling);
        readout.Children.Add(_unit);

        _viewport.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _viewport.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        // Ticks fade toward the edges, so the eye stays on the needle and the tape reads as continuing past view.
        _tape.OpacityMask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, .22), new GradientStop(Colors.Black, .78), new GradientStop(Colors.Transparent, 1) } };
        var needle = new Border { Width = 2, Height = 36, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(0, 0, 1, 1), IsHitTestVisible = false };
        needle.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Accent");
        _inner.Children.Add(_tape);
        _inner.Children.Add(needle);
        _inner.SizeChanged += (_, e) => _inner.Clip = new RectangleGeometry(new Rect(e.NewSize), 17, 17);
        _viewport.Child = _inner;
        _ring.BorderBrush = FocusVisible.RingBrush(this);
        var frame = new Grid();
        frame.Children.Add(_viewport);
        frame.Children.Add(_ring);

        var root = new StackPanel();
        root.Children.Add(_label);
        root.Children.Add(readout);
        root.Children.Add(frame);
        host.Child = root;
        _rolling.Loaded += (_, _) => PlaceUnit();
        Copy();
    }

    private void Inks()
    {
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
        _ring.BorderBrush = FocusVisible.RingBrush(this);
    }

    // The unit shares the readout's baseline, .3em after it.
    private void PlaceUnit()
    {
        if (_unit is null) { return; }
        var sample = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), 16, Brushes.Black, 1);
        double own = (17.6 - sample.Height) / 2 + sample.Baseline;
        _unit.Margin = new Thickness(8.4, _rolling.Baseline - own, 0, 0);
    }

    private void Copy()
    {
        if (!_built) { return; }
        _label!.Text = Label ?? "";
        _rolling.Set(Text(SnapTo(Value)));
        _unit!.Text = Unit ?? "";
        _unit.Visibility = string.IsNullOrEmpty(Unit) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Rescale()
    {
        if (!_built) { return; }
        _latest = SnapTo(Value);
        StopFlight();
        _pos = _latest;
        Copy();
        _tape.InvalidateVisual();
    }

    protected override void OnMinimumChanged(double oldMinimum, double newMinimum) { base.OnMinimumChanged(oldMinimum, newMinimum); Rescale(); }
    protected override void OnMaximumChanged(double oldMaximum, double newMaximum) { base.OnMaximumChanged(oldMaximum, newMaximum); Rescale(); }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (!_built) { return; }
        Copy();
        ArcRangePeer.Notify(this, oldValue, newValue);
        // A value from outside (a reset, a linked field) moves the tape there. Values this control emitted are already under the needle.
        double current = SnapTo(newValue);
        if (current == _latest) { return; }
        _latest = current;
        StopFlight();
        if (Reduced || !IsLoaded) { _pos = current; _tape.InvalidateVisual(); return; }
        Launch(current, GlideSpring, 0);
    }

    private void Emit(double next)
    {
        if (_latest == next) { return; }
        _latest = next;
        Value = next;
    }

    private void Commit(double next) => RaiseEvent(new RoutedPropertyChangedEventArgs<double>(next, next) { RoutedEvent = ValueCommittedEvent });

    private double Speed => _flight switch { Flight.Spring => _spring.Velocity, Flight.Inertia => _inertiaAmp / TimeConstant * Math.Exp(-_inertiaT / TimeConstant), _ => 0 };

    private void StopFlight()
    {
        _flight = Flight.None;
        _coasting = false;
        _loop.Stop();
    }

    private void Launch(double target, SpringTune tune, double velocity)
    {
        _spring.Jump(_pos);
        _spring.RestDelta = SafeStep / 200;
        _spring.RestSpeed = SafeStep / 20;
        _spring.To(target, tune, velocity);
        _flight = Flight.Spring;
        _loop.Start();
    }

    // Lands the tape on a value. Keys and taps glide on a spring that keeps any speed already in the tape.
    private void GlideTo(double next, bool commit = true)
    {
        double speed = Speed;
        StopFlight();
        Emit(next);
        if (Reduced) { _pos = next; _tape.InvalidateVisual(); }
        else { Launch(next, GlideSpring, speed); }
        if (commit) { Commit(next); }
    }

    private bool Frame(double seconds)
    {
        switch (_flight)
        {
            case Flight.Spring:
                _spring.Step(seconds);
                _pos = _spring.Position;
                if (!_spring.Active) { Landed(); }
                break;
            case Flight.Inertia:
                _inertiaT += seconds;
                double delta = -_inertiaAmp * Math.Exp(-_inertiaT / TimeConstant);
                if (Math.Abs(delta) <= SafeStep / 200) { _pos = _inertiaTarget; Landed(); }
                else { _pos = _inertiaTarget + delta; }
                break;
            default:
                return false;
        }
        // While the tape coasts, every tick it lands under the needle is the new value, so the readout rolls with it.
        if (_coasting && _flight != Flight.None) { Emit(SnapTo(_pos)); }
        _tape.InvalidateVisual();
        return _flight != Flight.None;
    }

    private void Landed()
    {
        bool coasted = _coasting;
        _flight = Flight.None;
        _coasting = false;
        if (!coasted) { return; }
        double next = SnapTo(_pos);
        Emit(next);
        Commit(next);
    }

    private void Down(double x, double time)
    {
        if (!IsEnabled) { return; }
        _focus.Pointer();
        Focus();
        // Grabbing a coasting tape catches it where it is, like a finger on a spinning wheel.
        StopFlight();
        _drag = new Drag { X = x, From = _pos };
        _drag.Samples.Add((time, _pos));
    }

    private void Move(double x, double time)
    {
        if (_drag is not { } state) { return; }
        double dx = x - state.X;
        if (!state.Moved)
        {
            if (Math.Abs(dx) < TapSlop) { return; }
            state.Moved = true;
            _dragging = true;
        }
        double raw = state.From - dx / PerUnit, edge = Math.Clamp(raw, Minimum, Maximum);
        double placed = Reduced ? edge : edge + GestureMath.Rubber((raw - edge) * PerUnit, Stretch) / PerUnit;
        _pos = placed;
        state.Samples.Add((time, placed));
        if (state.Samples.Count > 6) { state.Samples.RemoveAt(0); }
        Emit(SnapTo(edge));
        _tape.InvalidateVisual();
    }

    private void End(double x, double time, bool up)
    {
        if (_drag is not { } state) { return; }
        _drag = null;
        _dragging = false;
        if (!state.Moved)
        {
            // A tap glides the tape so the tapped tick comes to the needle.
            if (up) { GlideTo(SnapTo(_pos + (x - _viewport.ActualWidth / 2) / PerUnit)); }
            return;
        }
        var (first, last) = (state.Samples[0], state.Samples[^1]);
        double elapsed = (last.T - first.T) / 1000;
        double velocity = up && elapsed > .008 && time - last.T < 60 ? (last.V - first.V) / elapsed : 0;
        if (Reduced)
        {
            double next = SnapTo(_pos);
            _pos = next;
            Emit(next);
            Commit(next);
            _tape.InvalidateVisual();
            return;
        }
        _coasting = true;
        if (_pos < Minimum || _pos > Maximum)
        {
            // Past an end the band pulls the tape home with the speed it was thrown at.
            Launch(Math.Clamp(_pos, Minimum, Maximum), Home, velocity);
            return;
        }
        // Where the throw lands: a scroll-like projection that eases onto a tick.
        _inertiaOrigin = _pos;
        _inertiaTarget = SnapTo(_pos + Power * velocity);
        _inertiaAmp = _inertiaTarget - _pos;
        _inertiaT = 0;
        if (Math.Abs(_inertiaAmp) <= SafeStep / 200) { _pos = _inertiaTarget; _tape.InvalidateVisual(); Landed(); return; }
        _flight = Flight.Inertia;
        _loop.Start();
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
        double next = SnapTo(wanted);
        if (next != now) { GlideTo(next); }
        return true;
    }

    // Horizontal scrolling moves the tape; vertical scrolling is left to the page. WPF has no routed event for it, so the window's message is read directly.
    private void Hook()
    {
        Unhook();
        if (PresentationSource.FromVisual(this) is HwndSource source) { _source = source; source.AddHook(OnMessage); }
    }

    private void Unhook()
    {
        _source?.RemoveHook(OnMessage);
        _source = null;
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg == WM_MOUSEHWHEEL && IsEnabled && _viewport.IsMouseOver)
        {
            short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            Wheel(delta / 120d * 100);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Wheel(double deltaX)
    {
        if (!IsEnabled) { return; }
        StopFlight();
        double next = Math.Clamp(_pos + deltaX / PerUnit, Minimum, Maximum);
        _pos = next;
        Emit(SnapTo(next));
        _tape.InvalidateVisual();
        _wheelTimer.Stop();
        _wheelTimer.Start();
    }

    // The tape starts at the needle, offset half a tick so a one pixel tick centers exactly under it.
    private double TapeOffset(double width) => width / 2 - .5 - (_pos - Minimum) * PerUnit;

    private void DrawTape(DrawingContext dc, double width)
    {
        if (width <= 0) { return; }
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip, spacing = Math.Max(Spacing, .01), length = (Maximum - Minimum) * PerUnit;
        double x0 = Math.Floor(TapeOffset(width) * dip + .5) / dip;
        var major = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        var minor = major;
        major.A = (byte)Math.Round(255 * .48);
        minor.A = (byte)Math.Round(255 * .2);
        var majorBrush = new SolidColorBrush(major);
        var minorBrush = new SolidColorBrush(minor);
        majorBrush.Freeze();
        minorBrush.Freeze();
        void Layer(double every, double height, Brush brush)
        {
            int from = Math.Max(0, (int)Math.Floor(-x0 / every)), to = (int)Math.Min(Math.Floor(length / every), Math.Ceiling((width - x0) / every));
            for (int index = from; index <= to; index++) { dc.DrawRectangle(brush, null, new Rect(Math.Floor((x0 + index * every) * dip + .5) / dip, 0, 1 / dip, height)); }
        }
        // Three repeating layers that stack where they coincide, as the source's background images do.
        double majorEvery = spacing * Math.Max(1, MajorEvery);
        Layer(spacing, 12, minorBrush);
        if (MajorEvery % 2 == 0) { Layer(majorEvery / 2, 20, minorBrush); }
        Layer(majorEvery, 28, majorBrush);
        double majorValue = SafeStep * Math.Max(1, MajorEvery);
        int labelDecimals = GestureMath.DecimalsOf(GestureMath.RoundTo(majorValue, Decimals));
        int firstLabel = Math.Max(0, (int)Math.Floor((-x0 - 40) / majorEvery)), lastLabel = (int)Math.Min(Math.Floor((Maximum - Minimum) / majorValue + 1e-9), Math.Ceiling((width - x0 + 40) / majorEvery));
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        for (int index = firstLabel; index <= lastLabel; index++)
        {
            double mark = GestureMath.RoundTo(Minimum + index * majorValue, Decimals);
            string label = Format?.Invoke(mark) ?? mark.ToString("#,0." + new string('#', labelDecimals), CultureInfo.InvariantCulture);
            var face = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, _muted, dip);
            dc.DrawText(face, new Point(x0 + index * majorEvery - face.WidthIncludingTrailingWhitespace / 2, 36 + (16.8 - face.Height) / 2));
        }
    }
}
