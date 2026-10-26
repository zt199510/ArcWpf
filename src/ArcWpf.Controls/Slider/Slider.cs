using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

public sealed record SliderMark(double Value, string? Label = null);

/// <summary>A horizontal, single-value slider with an independently moving visual thumb.</summary>
[TemplatePart(Name = "PART_Surface", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Track", Type = typeof(Track))]
public partial class Slider : System.Windows.Controls.Slider
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Slider), new PropertyMetadata(""));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(Slider), new PropertyMetadata(1d, StepChanged), v => v is double n && double.IsFinite(n) && n > 0);
    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(Slider), new PropertyMetadata(true));
    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(Slider), new PropertyMetadata("0.##", DisplayChanged));
    public static readonly DependencyProperty StartContentProperty = DependencyProperty.Register(nameof(StartContent), typeof(object), typeof(Slider));
    public static readonly DependencyProperty EndContentProperty = DependencyProperty.Register(nameof(EndContent), typeof(object), typeof(Slider));
    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(nameof(Marks), typeof(IEnumerable<SliderMark>), typeof(Slider), new PropertyMetadata(null, MarksChanged));
    public static readonly DependencyProperty ThumbShadowProperty = DependencyProperty.Register(nameof(ThumbShadow), typeof(DropShadowEffect), typeof(Slider), new PropertyMetadata(null));
    private static readonly DependencyPropertyKey ValueTextPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ValueText), typeof(string), typeof(Slider), new PropertyMetadata("0"));
    public static readonly DependencyProperty ValueTextProperty = ValueTextPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(Slider), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ValueCommittedEvent = EventManager.RegisterRoutedEvent(nameof(ValueCommitted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Slider));
    public event RoutedEventHandler ValueCommitted { add => AddHandler(ValueCommittedEvent, value); remove => RemoveHandler(ValueCommittedEvent, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool ShowValue { get => (bool)GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    public string ValueFormat { get => (string)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public object? StartContent { get => GetValue(StartContentProperty); set => SetValue(StartContentProperty, value); }
    public object? EndContent { get => GetValue(EndContentProperty); set => SetValue(EndContentProperty, value); }
    public IEnumerable<SliderMark>? Marks { get => (IEnumerable<SliderMark>?)GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    public DropShadowEffect? ThumbShadow { get => (DropShadowEffect?)GetValue(ThumbShadowProperty); set => SetValue(ThumbShadowProperty, value); }
    public string ValueText => (string)GetValue(ValueTextProperty);
    public bool IsDragging => (bool)GetValue(IsDraggingProperty);

    private bool _pointerFocus, _keyFocus;
    private double _grab, _raw, _lastTime;
    private readonly Queue<(double Time, double Position)> _samples = new();
    private readonly SliderSpring _position = new(), _lag = new();
    internal double ShownPosition => _position.Position + _lag.Position;
    internal bool FullMotion => ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded && IsVisible;
    private double Span => Maximum - Minimum;
    private bool Reversed => IsDirectionReversed;
    // WPF FlowDirection mirrors the visual subtree and mouse coordinates; don't reverse it twice here.
    private double Travel => Math.Max(0, (_surface?.ActualWidth ?? 22) - 22);
    private double Percent(double value) => Span > 0 ? Math.Clamp(double.IsFinite(Span) ? (value - Minimum) / Span : (value / 2 - Minimum / 2) / (Maximum / 2 - Minimum / 2), 0, 1) * 100 : 0;
    private double ValueAt(double percent) => Minimum * (1 - Math.Clamp(percent / 100, 0, 1)) + Maximum * Math.Clamp(percent / 100, 0, 1);
    private double PointerPercent(double x) => Travel > 0 ? (Reversed ? 100 - (x - 11) / Travel * 100 : (x - 11) / Travel * 100) : Percent(Value);

    static Slider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(typeof(Slider)));
        MaximumProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(100d));
        LargeChangeProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(10d));
        SmallChangeProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(1d, null, (d, _) => ((Slider)d).Step));
        OrientationProperty.OverrideMetadata(typeof(Slider), new FrameworkPropertyMetadata(Orientation.Horizontal, null, (_, _) => Orientation.Horizontal));
    }
    public Slider()
    {
        Loaded += (_, _) => { RefreshText(false); RebuildMarks(); SnapVisuals(); };
        Unloaded += (_, _) => StopInteraction();
        IsVisibleChanged += (_, _) => { if (!IsVisible) StopInteraction(); };
    }
    private static void StepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var slider = (Slider)d; slider.CoerceValue(SmallChangeProperty); slider.CancelGesture(); }
    private static void DisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Slider)d).RefreshText(false);
    private static void MarksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = (Slider)d;
        if (e.OldValue is INotifyCollectionChanged old) CollectionChangedEventManager.RemoveHandler(old, slider.CollectionChanged);
        if (e.NewValue is INotifyCollectionChanged next) CollectionChangedEventManager.AddHandler(next, slider.CollectionChanged);
        slider.RebuildMarks();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildMarks();
    protected override void OnMinimumChanged(double oldMinimum, double newMinimum) { base.OnMinimumChanged(oldMinimum, newMinimum); BoundsChanged(); }
    protected override void OnMaximumChanged(double oldMaximum, double newMaximum) { base.OnMaximumChanged(oldMaximum, newMaximum); BoundsChanged(); }
    private void BoundsChanged()
    { if (_position is null) return; CancelGesture(); SnapVisuals(); RebuildMarks(); RefreshText(false); }
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (_position is null) return;
        // A consumer may replace the value during a drag: the gesture remains anchored to the pointer.
        if (!IsDragging) MoveToValue();
        RefreshText(IsLoaded); RefreshMarks();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e); if (_position is null) return;
        if (e.Property == LanguageProperty) RefreshText(false);
        if (e.Property == ThumbShadowProperty) UpdateShadow();
        if (e.Property == IsEnabledProperty && !IsEnabled) StopInteraction();
        if (e.Property == ArcMotion.LevelProperty) { CancelGesture(); SnapVisuals(); UpdateState(false); }
        if (e.Property == IsDirectionReversedProperty || e.Property == FlowDirectionProperty) { CancelGesture(); PositionParts(); LayoutMarks(); }
    }
    internal double SnapValue(double value)
    {
        value = Math.Clamp(value, Minimum, Maximum);
        if (value == Minimum || value == Maximum) return value;
        double steps = (value - Minimum) / Step;
        if (!double.IsFinite(steps)) return value;
        double result = Minimum + Math.Floor(steps + .5) * Step;
        // Decimal arithmetic avoids common .1/.3 grid noise without rounding tiny valid steps to zero.
        try
        {
            decimal step = (decimal)Step;
            if (step != 0) result = (double)((decimal)Minimum + Math.Floor(((decimal)value - (decimal)Minimum) / step + .5m) * step);
        }
        catch (OverflowException) { /* Extreme finite doubles retain the floating-point grid. */ }
        return Math.Clamp(double.IsFinite(result) ? result : value, Minimum, Maximum);
    }
    private void SetInputValue(double value)
    {
        SetCurrentValue(ValueProperty, SnapValue(value));
    }
    private void Commit() => RaiseEvent(new RoutedEventArgs(ValueCommittedEvent, this));
    private void RefreshText(bool animate)
    {
        string text;
        CultureInfo culture;
        try { culture = Language.GetSpecificCulture(); } catch (InvalidOperationException) { culture = CultureInfo.CurrentCulture; }
        try { text = Value.ToString(ValueFormat, culture); } catch (FormatException) { text = Value.ToString("0.##", culture); }
        SetValue(ValueTextPropertyKey, text);
        _readout?.SetNumber(text, Value, animate, ArcMotion.GetLevel(this));
        _bubbleNumber?.SetNumber(text, Value, animate && _bubble?.Visibility == Visibility.Visible, ArcMotion.GetLevel(this));
        PositionParts(); EnsureRendering();
    }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (IsEnabled && _surface is not null && Popover.IsWithin(e.OriginalSource as DependencyObject, _surface))
        {
            _pointerFocus = true; try { Focus(); } finally { _pointerFocus = false; }
            var point = e.GetPosition(_surface);
            bool grabbed = Math.Abs(point.X - (11 + Travel * (Reversed ? 100 - ShownPosition : ShownPosition) / 100)) <= 22;
            if (_surface.CaptureMouse()) BeginGesture(point.X, grabbed, e.Timestamp);
            e.Handled = true; return;
        }
        base.OnPreviewMouseLeftButtonDown(e);
    }
    private void SurfaceMouseMove(object sender, MouseEventArgs e)
    { if (IsDragging && _surface?.IsMouseCaptured == true) FollowGesture(e.GetPosition(_surface).X, e.Timestamp); }
    private void SurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsDragging || e.ChangedButton != MouseButton.Left) return;
        EndGesture(e.Timestamp, false); _surface?.ReleaseMouseCapture(); e.Handled = true;
    }
    private void SurfaceLostCapture(object sender, MouseEventArgs e) { if (IsDragging) EndGesture(e.Timestamp, true); }

    // Shared by actual WPF pointer routing and the bounded STA gesture tests (coordinates are Surface DIPs).
    internal void BeginGesture(double x, bool grabbed, double timestamp)
    {
        if (!IsEnabled || !IsLoaded || !IsVisible || Travel <= 0) return;
        _linger.Stop(); _lingering = false; _keyFocus = false;
        double from = ShownPosition; _position.Snap(from); _lag.Snap(0);
        double at = PointerPercent(x); _grab = grabbed ? at - from : 0; _raw = at - _grab;
        _samples.Clear(); SetValue(IsDraggingPropertyKey, true);
        if (!grabbed) FollowGesture(x, timestamp, true);
        _samples.Enqueue((timestamp, _position.Position)); UpdateState(true);
    }
    internal void FollowGesture(double x, double timestamp, bool press = false)
    {
        if (!IsDragging || !IsEnabled) return;
        double raw = PointerPercent(x) - _grab, edge = Math.Clamp(raw, 0, 100);
        double outside = (raw - edge) / 100 * Travel;
        double rubber = Math.Sign(outside) * (1 - 1 / (Math.Abs(outside) * .55 / 9 + 1)) * 9;
        double placed = edge + (FullMotion && Travel > 0 ? rubber / Travel * 100 : 0);
        double from = ShownPosition; _position.Snap(placed);
        if (press && FullMotion) { _lag.Snap(from - placed); _lag.To(0); }
        _raw = raw; _samples.Enqueue((timestamp, placed)); while (_samples.Count > 6) _samples.Dequeue();
        SetInputValue(ValueAt(raw)); PositionParts(); EnsureRendering();
    }
    internal void EndGesture(double timestamp, bool canceled)
    {
        if (!IsDragging) return;
        double velocity = 0;
        if (_samples.Count >= 2)
        {
            var first = _samples.Peek(); var last = _samples.Last(); double elapsed = (last.Time - first.Time) / 1000;
            if (elapsed > .008 && timestamp - last.Time is >= 0 and < 60) velocity = (last.Position - first.Position) / elapsed;
        }
        velocity += _lag.Velocity;
        double stepPercent = Span > 0 ? Step / Span * 100 : 0;
        double carry = !canceled && FullMotion && stepPercent / 100 * Travel >= 12 ? Math.Clamp(velocity * .099, -stepPercent, stepPercent) : 0;
        if (!canceled) SetInputValue(ValueAt(_raw + carry));
        double from = ShownPosition; _position.Snap(from); _lag.Snap(0);
        SetValue(IsDraggingPropertyKey, false);
        if (FullMotion && !canceled) _position.To(Percent(Value), velocity); else _position.Snap(Percent(Value));
        if (!canceled) { _lingering = true; _linger.Start(); Commit(); }
        else { _lingering = false; _linger.Stop(); }
        UpdateState(!canceled); PositionParts(); EnsureRendering();
    }
    private void CancelGesture()
    { if (IsDragging) EndGesture(Environment.TickCount, true); if (_surface?.IsMouseCaptured == true) _surface.ReleaseMouseCapture(); }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    { base.OnGotKeyboardFocus(e); if (ReferenceEquals(e.NewFocus, this)) { _keyFocus = !_pointerFocus && InputManager.Current.MostRecentInputDevice is KeyboardDevice; UpdateState(true); } }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    { base.OnLostKeyboardFocus(e); _keyFocus = false; UpdateState(true); }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && ReferenceEquals(e.OriginalSource, this))
        {
            bool flipped = IsDirectionReversed ^ (FlowDirection == FlowDirection.RightToLeft);
            double small = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? LargeChange : Step;
            double? target = e.Key switch
            {
                Key.Right => Value + (flipped ? -small : small), Key.Left => Value + (flipped ? small : -small),
                Key.Up => Value + small, Key.Down => Value - small,
                Key.PageUp => Value + LargeChange, Key.PageDown => Value - LargeChange,
                Key.Home => Minimum, Key.End => Maximum, _ => null
            };
            if (target is { } value) { _keyFocus = true; ChangeFromInput(value); e.Handled = true; }
        }
        base.OnPreviewKeyDown(e);
    }
    private void ChangeFromInput(double wanted)
    {
        if (!IsEnabled) return;
        CancelGesture(); double old = Value; SetInputValue(wanted);
        if (Value != old) Commit();
        else if (FullMotion && wanted != old && Travel > 0) { _position.To(Percent(Value), Math.Sign(wanted - old) * 150 / Travel * 100, .42); EnsureRendering(); }
        UpdateState(true);
    }
    protected override void OnIncreaseSmall() => ChangeFromInput(Value + Step);
    protected override void OnDecreaseSmall() => ChangeFromInput(Value - Step);
    protected override void OnIncreaseLarge() => ChangeFromInput(Value + LargeChange);
    protected override void OnDecreaseLarge() => ChangeFromInput(Value - LargeChange);
    protected override void OnMaximizeValue() => ChangeFromInput(Maximum);
    protected override void OnMinimizeValue() => ChangeFromInput(Minimum);
    protected override AutomationPeer OnCreateAutomationPeer() => new SliderPeer(this);
    private sealed class SliderPeer(Slider owner) : SliderAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Slider);
        protected override string GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Label : base.GetNameCore();
        protected override string GetItemStatusCore() => owner.ValueText;
    }
}
