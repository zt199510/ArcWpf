using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A row of slides browsed by dragging, flicking, the arrow keys or the controls below it. The active slide sits in the center and the page indicator stretches into a pill that travels with the track.</summary>
[TemplatePart(Name = "PART_Viewport", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Track", Type = typeof(CarouselTrack))]
[TemplatePart(Name = "PART_Controls", Type = typeof(Grid))]
public class Carousel : ItemsControl
{
    private const double Slop = 6, Flick = 360, Dot = 6, Pill = 22, RestScale = .9, RestOpacity = .5;

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Carousel), new FrameworkPropertyMetadata("Carousel", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(Carousel), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIndexChanged, CoerceIndex));
    public static readonly DependencyProperty SlideWidthProperty = DependencyProperty.Register(nameof(SlideWidth), typeof(double), typeof(Carousel), new FrameworkPropertyMetadata(340d, OnLayoutChanged), value => value is double size && size > 0);
    public static readonly DependencyProperty SlideGapProperty = DependencyProperty.Register(nameof(SlideGap), typeof(double), typeof(Carousel), new FrameworkPropertyMetadata(16d, OnLayoutChanged), value => value is double size && size >= 0);
    public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(nameof(Interval), typeof(TimeSpan), typeof(Carousel), new FrameworkPropertyMetadata(TimeSpan.Zero, (d, _) => ((Carousel)d).RebuildControls()));
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(Carousel), new FrameworkPropertyMetadata(false, (d, _) => ((Carousel)d).PlayingChanged()));
    private static readonly DependencyPropertyKey IsDraggingKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(Carousel), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingKey.DependencyProperty;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double SlideWidth { get => (double)GetValue(SlideWidthProperty); set => SetValue(SlideWidthProperty, value); }
    public double SlideGap { get => (double)GetValue(SlideGapProperty); set => SetValue(SlideGapProperty, value); }
    public TimeSpan Interval { get => (TimeSpan)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool IsDragging => (bool)GetValue(IsDraggingProperty);
    public event EventHandler? SelectedIndexChanged;

    private FrameworkElement? _viewport;
    private CarouselTrack? _track;
    private Grid? _controls;
    private TextBlock? _announcer;
    private ArcChromeButton? _play, _previous, _next;
    private ContentControl? _playGlyph;
    private StackPanel? _tabs;
    private readonly List<(ArcChromeButton Tab, Border Dot, Border Fill, Border Drain)> _dots = [];
    private double[] _drains = [];
    private readonly SpringDriver _driver;
    private readonly ControlSpring _x;
    private double _step = 1;
    private int _target;
    private bool _hovered, _focusPaused, _subscribed;
    private long _lastFrame;
    private double _refillFrom, _refillT = 1;
    private int _refillIndex = -1;
    private DragState? _drag;
    private bool _swallowClick;
    private sealed class DragState { public double StartX, StartY, Origin; public int From; public bool? Horizontal; public bool Caught; public List<(long Time, double X)> Samples = []; }

    static Carousel() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(typeof(Carousel)));

    public Carousel()
    {
        Focusable = false;
        _driver = new SpringDriver(ApplyPosition);
        _x = _driver.Add(0, ArcSpring.Smooth);
        Loaded += (_, _) => { Sync(); UpdateClock(); };
        Unloaded += (_, _) => { _driver.Finish(); StopClock(); };
        SizeChanged += (_, _) => Sync();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private int LastIndex => Math.Max(0, Items.Count - 1);
    private bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
    private bool Animated => IsLoaded && !Reduced;
    private bool CanRotate => Interval > TimeSpan.Zero && Items.Count > 1 && !Reduced;
    private bool Running => CanRotate && IsPlaying && !_hovered && !_focusPaused && !IsDragging && IsVisible;
    internal double Position => _step > 0 ? -_x.Position / _step : 0;
    internal double SlideSize => Math.Min(ActualWidth > 0 ? ActualWidth * .8 : SlideWidth, SlideWidth);
    internal double Drain(int index) => index >= 0 && index < _drains.Length ? _drains[index] : 0;

    private static object CoerceIndex(DependencyObject d, object value)
    {
        var carousel = (Carousel)d;
        return carousel.Items.Count == 0 ? value : Math.Clamp((int)value, 0, carousel.LastIndex);
    }

    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var carousel = (Carousel)d;
        if (carousel._target != (int)e.NewValue) { carousel._target = (int)e.NewValue; carousel.Glide(carousel._target); }
        carousel.UpdateActive();
        carousel.UpdateClock();
        carousel.SelectedIndexChanged?.Invoke(carousel, EventArgs.Empty);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var carousel = (Carousel)d; carousel._track?.InvalidateMeasure(); carousel.Sync(); }

    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        CoerceValue(SelectedIndexProperty);
        RebuildControls();
        Sync();
    }

    public override void OnApplyTemplate()
    {
        if (_viewport is not null)
        {
            _viewport.PreviewMouseLeftButtonDown -= OnPress;
            _viewport.PreviewMouseMove -= OnMove;
            _viewport.PreviewMouseLeftButtonUp -= OnRelease;
            _viewport.LostMouseCapture -= OnLostCapture;
            _viewport.MouseEnter -= OnEnter;
            _viewport.MouseLeave -= OnLeave;
        }
        base.OnApplyTemplate();
        _viewport = GetTemplateChild("PART_Viewport") as FrameworkElement;
        _track = GetTemplateChild("PART_Track") as CarouselTrack;
        _controls = GetTemplateChild("PART_Controls") as Grid;
        _announcer = GetTemplateChild("PART_Announcer") as TextBlock;
        if (_track is not null) { _track.Owner = this; }
        if (_viewport is not null)
        {
            AutomationProperties.SetName(_viewport, "Slides");
            _viewport.PreviewMouseLeftButtonDown += OnPress;
            _viewport.PreviewMouseMove += OnMove;
            _viewport.PreviewMouseLeftButtonUp += OnRelease;
            _viewport.LostMouseCapture += OnLostCapture;
            _viewport.MouseEnter += OnEnter;
            _viewport.MouseLeave += OnLeave;
        }
        RebuildControls();
    }

    private void OnEnter(object sender, MouseEventArgs e) { _hovered = true; UpdateClock(); }
    private void OnLeave(object sender, MouseEventArgs e) { _hovered = false; UpdateClock(); }

    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        _focusPaused = IsKeyboardFocusWithin && !ReferenceEquals(Keyboard.FocusedElement, _play) && (InputManager.Current.MostRecentInputDevice is KeyboardDevice);
        UpdateClock();
    }

    // ---- Position ----
    private void Sync()
    {
        if (_track is null || ActualWidth <= 0) { return; }
        double slide = SlideSize, step = slide + SlideGap;
        bool changed = step != _step;
        _step = step;
        if (changed && _drag is null) { _driver.Stop(); _x.Snap(-_target * step); }
        double inset = Math.Max(0, (ActualWidth - slide) / 2);
        if (_controls is not null)
        {
            bool tight = ActualWidth <= 340;
            _controls.Margin = new Thickness(tight ? Math.Max(0, inset - 8) : inset, 16, tight ? Math.Max(0, inset - 8) : inset, 0);
            foreach (var (tab, _, _, _) in _dots) { tab.Padding = new Thickness(tight ? 4 : 5, 0, tight ? 4 : 5, 0); }
        }
        ApplyPosition();
    }

    /// <summary>Scale, dim and the indicator all read the track position, so they never lag the pointer.</summary>
    private void ApplyPosition()
    {
        if (_track is null) { return; }
        _track.Offset = _x.Position;
        double position = Position;
        _track.ApplyEmphasis(position, RestScale, RestOpacity);
        double clamped = Math.Clamp(position, 0, LastIndex);
        for (int i = 0; i < _dots.Count; i++)
        {
            double emphasis = Math.Max(0, 1 - Math.Abs(clamped - i));
            var (_, dot, fill, drain) = _dots[i];
            dot.Width = Dot + (Pill - Dot) * emphasis;
            fill.Opacity = emphasis;
            if (i != _target && Math.Abs(position - i) >= 1 && i < _drains.Length && _drains[i] != 0) { _drains[i] = 0; }
            ApplyDrain(i, drain, dot.Width);
        }
    }

    private void ApplyDrain(int index, Border drain, double width) => drain.Width = width * (index < _drains.Length ? _drains[index] : 0);

    private void Glide(int to, double velocity = 0)
    {
        double target = -to * _step;
        if (!Animated) { _driver.Stop(); _driver.Snap(_x, target); return; }
        _x.Launch(velocity);
        _driver.Animate(_x, target, ArcSpring.Smooth);
    }

    private void Select(int to, double velocity = 0, bool announce = true)
    {
        int next = Math.Clamp(to, 0, LastIndex);
        _target = next;
        Glide(next, velocity);
        if (next == SelectedIndex) { return; }
        SelectedIndex = next;
        if (announce && _announcer is not null) { _announcer.Text = SlideLabel(next); }
    }

    private string SlideLabel(int index) => $"{index + 1} of {Items.Count}";

    public void Next() => Select(_target + 1);
    public void Previous() => Select(_target - 1);
    public void First() => Select(0);
    public void Last() => Select(LastIndex);

    private void UpdateActive()
    {
        _track?.SetActive(SelectedIndex);
        if (_previous is not null) { _previous.Refresh(true); _previous.Opacity = SelectedIndex == 0 ? .5 : 1; _previous.Cursor = SelectedIndex == 0 ? Cursors.Arrow : Cursors.Hand; }
        if (_next is not null) { _next.Refresh(true); _next.Opacity = SelectedIndex == LastIndex ? .5 : 1; _next.Cursor = SelectedIndex == LastIndex ? Cursors.Arrow : Cursors.Hand; }
        for (int i = 0; i < _dots.Count; i++) { _dots[i].Tab.IsTabStop = i == SelectedIndex; }
    }

    // ---- Dragging ----
    private static double Rubber(double overshoot, double dimension) => overshoot * dimension * .55 / (dimension + .55 * overshoot);
    private static double Unrubber(double distance, double dimension) => distance * dimension / (.55 * Math.Max(1, dimension - distance));
    private double Min => -LastIndex * _step;
    private double Band(double raw) { double size = Math.Max(1, ActualWidth); return raw > 0 ? Rubber(raw, size) : raw < Min ? Min - Rubber(Min - raw, size) : raw; }
    private double Unband(double shown) { double size = Math.Max(1, ActualWidth); return shown > 0 ? Unrubber(shown, size) : shown < Min ? Min - Unrubber(Min - shown, size) : shown; }

    private void OnPress(object sender, MouseButtonEventArgs e)
    {
        if (Items.Count < 2 || _drag is not null) { return; }
        _swallowClick = false;
        // Touching a visibly moving track catches it where it is. A settling tail is left alone so a press still selects.
        bool caught = _driver.IsRunning && Math.Abs(_x.Position + _target * _step) > 12;
        if (caught) { _driver.Stop(); }
        var point = e.GetPosition(this);
        _drag = new DragState { StartX = point.X, StartY = point.Y, From = _target, Caught = caught };
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_drag is null || e.LeftButton != MouseButtonState.Pressed) { return; }
        var point = e.GetPosition(this);
        if (_drag.Horizontal is null)
        {
            double dx = point.X - _drag.StartX, dy = point.Y - _drag.StartY;
            if (Math.Abs(dx) < Slop && Math.Abs(dy) < Slop) { return; }
            if (Math.Abs(dy) > Math.Abs(dx)) { _drag.Horizontal = false; return; }
            _drag.Horizontal = true;
            _driver.Stop();
            _drag.StartX = point.X;
            _drag.Origin = Unband(_x.Position);
            _viewport?.CaptureMouse();
            if (_viewport is not null) { _viewport.Cursor = Cursors.SizeWE; }
            SetValue(IsDraggingKey, true);
            UpdateClock();
        }
        if (_drag.Horizontal != true) { return; }
        _x.Snap(Band(_drag.Origin + point.X - _drag.StartX));
        ApplyPosition();
        _drag.Samples.Add((Environment.TickCount64, point.X));
        if (_drag.Samples.Count > 16) { _drag.Samples.RemoveAt(0); }
    }

    private void OnRelease(object sender, MouseButtonEventArgs e) => Finish(e.GetPosition(this).X, false);
    private void OnLostCapture(object sender, MouseEventArgs e) { if (_drag?.Horizontal == true && Mouse.LeftButton == MouseButtonState.Pressed) { Finish(0, true); } }

    private static double VelocityOf(List<(long Time, double X)> samples, long now)
    {
        var recent = samples.Where(sample => now - sample.Time <= 90).ToList();
        if (recent.Count < 2 || now - recent[^1].Time > 50) { return 0; }
        return recent[^1].Time > recent[0].Time ? (recent[^1].X - recent[0].X) / ((recent[^1].Time - recent[0].Time) / 1000d) : 0;
    }

    private void Finish(double x, bool cancelled)
    {
        var drag = _drag;
        if (drag is null) { return; }
        _drag = null;
        if (drag.Horizontal == true)
        {
            _swallowClick = true;
            SetValue(IsDraggingKey, false);
            if (_viewport is not null) { _viewport.Cursor = Cursors.Hand; if (_viewport.IsMouseCaptured) { _viewport.ReleaseMouseCapture(); } }
            double velocity = cancelled ? 0 : VelocityOf(drag.Samples, Environment.TickCount64);
            // The landing slide comes from where the flick would carry the track, not from where the pointer let go.
            double projected = velocity / 1000 * .995 / .005;
            int to = (int)Math.Round(-(_x.Position + projected) / _step);
            if (Math.Abs(velocity) > Flick) { int direction = velocity < 0 ? 1 : -1; if ((to - drag.From) * direction < 1) { to = drag.From + direction; } }
            Select(to, velocity);
            UpdateClock();
        }
        else if (drag.Caught) { _swallowClick = true; Select((int)Math.Round(-_x.Position / _step)); }
        else if (!cancelled && drag.Horizontal is null && _track is not null)
        {
            int hit = _track.SlideAt(x, this);
            if (hit >= 0 && hit != _target) { _swallowClick = true; Select(hit); }
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_swallowClick) { _swallowClick = false; e.Handled = e.OriginalSource is DependencyObject source && _viewport?.IsAncestorOf(source) == true; }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Items.Count < 2 || (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Windows)) != 0 || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) { return; }
        int? to = e.Key switch { Key.Left => _target - 1, Key.Right => _target + 1, Key.Home => 0, Key.End => LastIndex, _ => null };
        if (to is null) { return; }
        e.Handled = true;
        int next = Math.Clamp(to.Value, 0, LastIndex);
        bool onTab = _dots.Any(dot => ReferenceEquals(dot.Tab, Keyboard.FocusedElement));
        Select(next);
        if (onTab) { _dots[next].Tab.Focus(); }
    }

    // ---- Controls ----
    private void RebuildControls()
    {
        if (_controls is null) { return; }
        _controls.Children.Clear();
        _controls.ColumnDefinitions.Clear();
        _dots.Clear();
        _play = _previous = _next = null;
        _drains = new double[Items.Count];
        _controls.Visibility = Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) { _controls.ColumnDefinitions.Add(new ColumnDefinition { Width = width }); }
        if (Items.Count < 2) { return; }

        if (CanRotate)
        {
            _play = ControlButton(IsPlaying ? "Pause slide rotation" : "Start slide rotation");
            _playGlyph = new ContentControl { Focusable = false, Width = 16, Height = 16, IsHitTestVisible = false };
            _play.Content = _playGlyph;
            _play.Click += (_, _) => IsPlaying = !IsPlaying;
            _play.Margin = new Thickness(0, 0, 8, 0);
            _controls.Children.Add(_play);
            UpdatePlayGlyph(false);
        }

        _tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_tabs, "Choose a slide");
        Grid.SetColumn(_tabs, 1);
        for (int i = 0; i < Items.Count; i++)
        {
            int index = i;
            var fill = new Border { Opacity = 0 };
            fill.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
            var drain = new Border { HorizontalAlignment = HorizontalAlignment.Right, Width = 0 };
            drain.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderStrong");
            var inner = new Grid { Children = { fill, drain } };
            var dot = new Border { Width = Dot, Height = Dot, CornerRadius = new CornerRadius(3), Child = inner, ClipToBounds = true };
            dot.SizeChanged += (_, _) => inner.Clip = new RectangleGeometry(new Rect(0, 0, dot.ActualWidth, Dot), 3, 3);
            dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderStrong");
            var tab = new ArcChromeButton(18) { Content = dot, Height = 36, Padding = new Thickness(5, 0, 5, 0), Roles = _ => (null, null, null) };
            AutomationProperties.SetName(tab, SlideLabel(index));
            tab.Click += (_, _) => Select(index);
            tab.MouseEnter += (_, _) => { if (fill.Opacity < 1) { dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.TextMuted"); } };
            tab.MouseLeave += (_, _) => dot.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderStrong");
            _tabs.Children.Add(tab);
            _dots.Add((tab, dot, fill, drain));
        }
        _controls.Children.Add(_tabs);

        var arrows = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(arrows, 3);
        _previous = ControlButton("Previous slide");
        _previous.Content = ArcText.Icon("chevron-left", 18, _previous.Ink);
        _previous.Click += (_, _) => { if (_target > 0) { Previous(); } };
        _next = ControlButton("Next slide");
        _next.Content = ArcText.Icon("chevron-right", 18, _next.Ink);
        _next.Margin = new Thickness(8, 0, 0, 0);
        _next.Click += (_, _) => { if (_target < LastIndex) { Next(); } };
        arrows.Children.Add(_previous);
        arrows.Children.Add(_next);
        _controls.Children.Add(arrows);
        UpdateActive();
        Sync();
    }

    private ArcChromeButton ControlButton(string name)
    {
        var button = new ArcChromeButton(18, 1) { Width = 36, Height = 36 };
        button.Roles = b => (b.IsMouseOver && b.Opacity == 1 ? "SurfaceMuted" : "Surface", "Border", b.Opacity < 1 ? "TextMuted" : "Foreground");
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void UpdatePlayGlyph(bool animate)
    {
        if (_play is null || _playGlyph is null) { return; }
        AutomationProperties.SetName(_play, IsPlaying ? "Pause slide rotation" : "Start slide rotation");
        var icon = ArcText.Icon(IsPlaying ? "pause" : "play", 15, _play.Ink);
        icon.Fill = _play.Ink;
        _playGlyph.Content = icon;
        if (!animate || Reduced) { return; }
        var scale = new ScaleTransform(.6, .6, 7.5, 7.5);
        icon.RenderTransform = scale;
        icon.Opacity = 0;
        var motion = new OwnedMotion();
        motion.Double(icon, OpacityProperty, 1, .16, true, enter: true);
        motion.Double(scale, ScaleTransform.ScaleXProperty, 1, .24, true, enter: true);
        motion.Double(scale, ScaleTransform.ScaleYProperty, 1, .24, true, enter: true);
    }

    private void PlayingChanged()
    {
        UpdatePlayGlyph(IsLoaded);
        // Pausing refills the pill: this slide stays until the viewer moves on.
        if (!IsPlaying && SelectedIndex < _drains.Length)
        {
            _refillIndex = SelectedIndex;
            _refillFrom = _drains[SelectedIndex];
            _refillT = Reduced ? 1 : 0;
            if (Reduced) { _drains[SelectedIndex] = 0; }
        }
        UpdateClock();
    }

    // ---- Rotation clock ----
    private void UpdateClock()
    {
        bool needed = Running || _refillT < 1;
        if (needed && !_subscribed) { _subscribed = true; _lastFrame = System.Diagnostics.Stopwatch.GetTimestamp(); CompositionTarget.Rendering += OnFrame; }
        else if (!needed) { StopClock(); }
    }

    private void StopClock()
    {
        if (!_subscribed) { return; }
        _subscribed = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        AdvanceClock(System.Diagnostics.Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds);
        _lastFrame = now;
    }

    internal void AdvanceClock(double seconds)
    {
        if (_refillT < 1 && _refillIndex >= 0 && _refillIndex < _drains.Length)
        {
            _refillT = Math.Min(1, _refillT + seconds / .24);
            _drains[_refillIndex] = _refillFrom * (1 - ArcEasing.Standard.Ease(_refillT));
        }
        if (Running && SelectedIndex < _drains.Length)
        {
            _drains[SelectedIndex] += seconds / Interval.TotalSeconds;
            if (_drains[SelectedIndex] >= 1)
            {
                _drains[SelectedIndex] = 1;
                ApplyPosition();
                Select(SelectedIndex >= LastIndex ? 0 : SelectedIndex + 1, 0, false);
            }
        }
        ApplyPosition();
        UpdateClock();
    }
}

/// <summary>Lays slides in a row with side padding that centers the first and last, and applies each slide's distance-based scale and dim.</summary>
public sealed class CarouselTrack : Panel
{
    internal Carousel? Owner { get; set; }
    private readonly TranslateTransform _translate = new();
    public CarouselTrack() => RenderTransform = _translate;
    internal double Offset { get => _translate.X; set => _translate.X = value; }

    protected override Size MeasureOverride(Size availableSize)
    {
        double slide = Owner?.SlideSize ?? availableSize.Width, height = 0;
        foreach (UIElement child in InternalChildren) { child.Measure(new Size(slide, double.PositiveInfinity)); height = Math.Max(height, child.DesiredSize.Height); }
        return new Size(double.IsInfinity(availableSize.Width) ? slide : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double slide = Owner?.SlideSize ?? finalSize.Width, gap = Owner?.SlideGap ?? 0, x = (finalSize.Width - slide) / 2;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, slide, finalSize.Height));
            child.RenderTransformOrigin = new Point(.5, .5);
            x += slide + gap;
        }
        Owner?.Dispatcher.InvokeAsync(() => ApplyEmphasis(Owner.Position, .9, .5), System.Windows.Threading.DispatcherPriority.Render);
        return finalSize;
    }

    internal void ApplyEmphasis(double position, double restScale, double restOpacity)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            double t = Math.Clamp(Math.Abs(position - i), 0, 1);
            var child = InternalChildren[i];
            double scale = 1 + (restScale - 1) * t;
            child.RenderTransform = new ScaleTransform(scale, scale);
            child.Opacity = 1 + (restOpacity - 1) * t;
        }
    }

    internal void SetActive(int index)
    {
        for (int i = 0; i < InternalChildren.Count; i++) { InternalChildren[i].IsEnabled = i == index; }
    }

    internal int SlideAt(double x, UIElement relative)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = (FrameworkElement)InternalChildren[i];
            double left = child.TranslatePoint(new Point(0, 0), relative).X, right = child.TranslatePoint(new Point(child.ActualWidth, 0), relative).X;
            if (x >= left && x <= right) { return i; }
        }
        return -1;
    }
}
