using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

// The small sums the registry's gesture controls share.
internal static class GestureMath
{
    // iOS style resistance: each pixel past the limit gives less, and never more than limit.
    public static double Rubber(double distance, double limit) => Math.Sign(distance) * (1 - 1 / (Math.Abs(distance) * .55 / limit + 1)) * limit;
    public static int DecimalsOf(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
        int exponent = text.IndexOf("e-", StringComparison.Ordinal);
        if (exponent >= 0) { return int.Parse(text[(exponent + 2)..], CultureInfo.InvariantCulture); }
        int dot = text.IndexOf('.');
        return dot < 0 ? 0 : text.Length - dot - 1;
    }
    // Math.round: halves go up, also below zero.
    public static double RoundTo(double value, int places) { double factor = Math.Pow(10, places); return Math.Floor(value * factor + .5) / factor; }
    // The range controls' snap: the nearest step from min, written at the step's own precision.
    public static double Snap(double input, double min, double max, double step, int decimals) => Math.Clamp(RoundTo(min + Math.Floor((input - min) / step + .5) * step, decimals), min, max);
    // Number.toLocaleString("en-US", { minimumFractionDigits, maximumFractionDigits }).
    public static string Grouped(double value, int decimals) => value.ToString("N" + decimals, CultureInfo.InvariantCulture);
}

internal readonly record struct SpringTune(double Stiffness, double Damping, double Mass = 1)
{
    // motion's visualDuration/bounce written as stiffness and damping, the form that keeps velocity.
    public static SpringTune Physical(double visualDuration, double bounce = 0)
    {
        double root = 2 * Math.PI / (visualDuration * 1.2);
        return new SpringTune(root * root, 2 * (1 - bounce) * root);
    }
    public static readonly SpringTune Snappy = Physical(.26), Smooth = Physical(.4), Morph = Physical(.42);
}

// A damped spring of any bounce. Position and velocity survive every retarget.
internal sealed class GestureSpring(double position, double restDelta = .01, double restSpeed = .1)
{
    private SpringTune _tune = SpringTune.Snappy;
    public double Position { get; private set; } = position;
    public double Velocity { get; private set; }
    public double Target { get; private set; } = position;
    public bool Active { get; private set; }
    public double RestDelta { get; set; } = restDelta;
    public double RestSpeed { get; set; } = restSpeed;

    public void Jump(double value) { Position = Target = value; Velocity = 0; Active = false; }
    public void To(double target, SpringTune tune, double? velocity = null)
    {
        Target = target;
        _tune = tune;
        if (velocity is { } launch) { Velocity = launch; }
        Active = true;
    }
    // Follows a moving target without restarting the settle test's state.
    public void Chase(double target, SpringTune tune) { if (target != Target || Active) { To(target, tune); } }

    public void Step(double seconds)
    {
        if (!Active || !double.IsFinite(seconds) || seconds <= 0) { return; }
        double left = Math.Min(seconds, .05);
        while (left > 0)
        {
            double dt = Math.Min(left, 1d / 480);
            double force = -_tune.Stiffness * (Position - Target) - _tune.Damping * Velocity;
            Velocity += force / _tune.Mass * dt;
            Position += Velocity * dt;
            left -= dt;
        }
        if (Math.Abs(Position - Target) < RestDelta && Math.Abs(Velocity) < RestSpeed) { Jump(Target); }
    }
}

// CompositionTarget.Rendering while frame() asks for more; idle controls cost nothing.
internal sealed class FrameLoop(Func<double, bool> frame)
{
    private long _last;
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning) { return; }
        IsRunning = true;
        _last = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += Tick;
    }

    public void Stop()
    {
        if (!IsRunning) { return; }
        CompositionTarget.Rendering -= Tick;
        IsRunning = false;
    }

    private void Tick(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = Stopwatch.GetElapsedTime(_last, now).TotalSeconds;
        _last = now;
        Advance(seconds);
    }

    internal void Advance(double seconds) { if (!frame(seconds)) { Stop(); } }
}

// useInView({ once: true, amount: .5 }): fires once when half the element is on screen.
internal sealed class InViewOnce
{
    private readonly FrameworkElement _owner;
    private readonly Action _seen;
    private ScrollViewer? _scroller;
    public bool Seen { get; private set; }

    public InViewOnce(FrameworkElement owner, Action seen)
    {
        (_owner, _seen) = (owner, seen);
        owner.Loaded += (_, _) => { Attach(); Later(); };
        owner.Unloaded += (_, _) => Detach();
        owner.IsVisibleChanged += (_, _) => Later();
        owner.SizeChanged += (_, _) => Later();
    }

    private void Attach()
    {
        Detach();
        if (Seen) { return; }
        for (DependencyObject? at = _owner; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is ScrollViewer scroller) { _scroller = scroller; scroller.ScrollChanged += OnScroll; return; }
        }
    }

    private void Detach() { if (_scroller is not null) { _scroller.ScrollChanged -= OnScroll; _scroller = null; } }
    private void OnScroll(object sender, ScrollChangedEventArgs e) => Evaluate();
    private void Later() { if (!Seen) { _owner.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Evaluate)); } }

    private void Evaluate()
    {
        if (Seen || !_owner.IsLoaded || !_owner.IsVisible || _owner.ActualWidth <= 0 || _owner.ActualHeight <= 0) { return; }
        if (_scroller is not null)
        {
            var box = _owner.TransformToAncestor(_scroller).TransformBounds(new Rect(0, 0, _owner.ActualWidth, _owner.ActualHeight));
            var view = new Rect(0, 0, _scroller.ViewportWidth, _scroller.ViewportHeight);
            var shared = Rect.Intersect(box, view);
            if (shared.IsEmpty || shared.Width * shared.Height < box.Width * box.Height * .5) { return; }
        }
        Seen = true;
        Detach();
        _seen();
    }
}

// Exposes a code-built value as a range: a meter, a slider or a spinner, with its spoken text as the item status.
internal sealed class ArcRangePeer(FrameworkElement owner, AutomationControlType type, Func<string> name, Func<double> minimum, Func<double> maximum, Func<double> value, Func<string> text, Action<double>? set = null, Func<double>? smallChange = null, Func<double>? largeChange = null) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => type;
    protected override string GetNameCore() { string own = base.GetNameCore(); return string.IsNullOrEmpty(own) ? name() : own; }
    protected override string GetItemStatusCore() => text();
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

    public double Value => value();
    public double Minimum => minimum();
    public double Maximum => maximum();
    public double SmallChange => smallChange?.Invoke() ?? 0;
    public double LargeChange => largeChange?.Invoke() ?? 0;
    public bool IsReadOnly => set is null || !owner.IsEnabled;
    public void SetValue(double newValue)
    {
        if (IsReadOnly) { throw new ElementNotEnabledException(); }
        set!(newValue);
    }

    public static void Notify(FrameworkElement element, double from, double to)
    {
        if (UIElementAutomationPeer.FromElement(element) is ArcRangePeer peer) { peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, from, to); }
    }
}

// :focus-visible as the registry draws it: a 2px outline of accent-strong at 72%. Mouse focus stays quiet until a key is used.
internal sealed class FocusVisible
{
    private readonly UIElement _owner;
    private readonly Action _changed;
    private bool _byPointer;
    public bool Shown { get; private set; }

    public FocusVisible(UIElement owner, Action changed)
    {
        (_owner, _changed) = (owner, changed);
        owner.GotKeyboardFocus += (_, _) => Update(!_byPointer);
        owner.LostKeyboardFocus += (_, _) => { _byPointer = false; Update(false); };
    }

    public void Pointer() { _byPointer = true; Update(false); }
    public void Key() { _byPointer = false; if (_owner.IsKeyboardFocused) { Update(true); } }
    private void Update(bool shown) { if (Shown != shown) { Shown = shown; _changed(); } }

    public static SolidColorBrush RingBrush(FrameworkElement owner)
    {
        var color = Tween.Resolve(owner, "ArcWpf.Brush.AccentStrong");
        color.A = (byte)Math.Round(255 * .72);
        return new SolidColorBrush(color);
    }
}

// One line of copy with CSS letter spacing, for the registry's text that rolls or swaps in place.
internal static class CopyMotion
{
    public static TrackedText Text(string text, double size, double lineRatio, double trackingPx, FontWeight weight, Brush foreground, FontFamily? family = null)
    {
        var view = new TrackedText { Text = text, LineRatio = lineRatio, Tracking = trackingPx / size, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
        view.SetValue(TrackedText.FontSizeProperty, size);
        view.SetValue(TrackedText.FontWeightProperty, weight);
        view.SetValue(TrackedText.ForegroundProperty, foreground);
        if (family is not null) { view.SetValue(TrackedText.FontFamilyProperty, family); }
        return view;
    }

    private static double SizeOf(TrackedText view) => (double)view.GetValue(TrackedText.FontSizeProperty);

    // Copy enters from the side the value moved toward: a rise comes up from below, a fall drops from above.
    public static void In(TrackedText view, int direction, FrameworkElement scope)
    {
        var level = Tween.Level(scope);
        if (level == ArcMotionLevel.Off || !scope.IsLoaded) { return; }
        view.Opacity = 0;
        if (level == ArcMotionLevel.Reduced) { Tween.To(view, UIElement.OpacityProperty, 1, Tween.Instant, null); return; }
        var move = (TranslateTransform)view.RenderTransform;
        move.Y = .3 * direction * SizeOf(view);
        Tween.To(view, UIElement.OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        Tween.To(move, TranslateTransform.YProperty, 0, Tween.Standard, ArcEasing.Enter);
        TextMotion.Blur(view, 4, 0, Tween.Standard, ArcEasing.Enter);
    }

    public static void Out(TrackedText view, int direction, FrameworkElement scope, Action done)
    {
        var level = Tween.Level(scope);
        if (level == ArcMotionLevel.Off || !scope.IsLoaded) { done(); return; }
        if (level == ArcMotionLevel.Reduced) { Tween.To(view, UIElement.OpacityProperty, 0, Tween.Instant, null, 0, done); return; }
        Tween.To(view, UIElement.OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, done);
        Tween.To((TranslateTransform)view.RenderTransform, TranslateTransform.YProperty, -.3 * direction * SizeOf(view), Tween.Fast, ArcEasing.Standard);
        TextMotion.Blur(view, 0, 2, Tween.Fast, ArcEasing.Standard);
    }
}

// Label, status and detail copy swap in place: the outgoing line is popped out of flow while the new one rises in.
internal class SwapText : Panel
{
    private readonly double _size, _lineRatio, _tracking;
    private readonly FontWeight _weight;
    private readonly Brush _foreground;
    private TrackedText? _current;

    public SwapText(double size, double lineRatio, double trackingPx, FontWeight weight, Brush foreground)
    {
        (_size, _lineRatio, _tracking, _weight, _foreground) = (size, lineRatio, trackingPx, weight, foreground);
        IsHitTestVisible = false;
    }

    public string Text => _current?.Text ?? "";

    public void Set(string text, int direction = 1)
    {
        if (_current is not null && _current.Text == text) { return; }
        var old = _current;
        var view = CopyMotion.Text(text, _size, _lineRatio, _tracking, _weight, _foreground);
        _current = view;
        Children.Add(view);
        if (old is null) { return; }
        Enter(view, direction);
        Leave(old, direction, () => Children.Remove(old));
        InvalidateMeasure();
    }

    protected virtual void Enter(TrackedText view, int direction) => CopyMotion.In(view, direction, this);
    protected virtual void Leave(TrackedText view, int direction, Action done) => CopyMotion.Out(view, direction, this, done);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children) { child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        return _current?.DesiredSize ?? new Size(0, _size * _lineRatio);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in Children) { child.Arrange(new Rect((finalSize.Width - child.DesiredSize.Width) / 2, 0, child.DesiredSize.Width, child.DesiredSize.Height)); }
        return finalSize;
    }
}
