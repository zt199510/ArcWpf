using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

// The arithmetic of image-compare.tsx.
internal static class ImageCompareMath
{
    // Rubber-band travel past an edge, the handle's inset from the frame, how far it gives inside that inset, and a key press at a limit.
    public const double Stretch = 9, Margin = 42, Give = 12, BumpPx = 150, Inset = 12;
    public static double Clamp(double value, double low, double high) => Math.Min(high, Math.Max(low, value));
    public static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    // iOS style resistance: travel past a limit gives less and less, and never more than limit pixels.
    public static double Rubber(double distance, double limit = Stretch) => Math.Sign(distance) * (1 - 1 / (Math.Abs(distance) * .55 / limit + 1)) * limit;
    // The handle keeps the line's pace in the middle and slows smoothly near an edge.
    public static double Soften(double at, double size)
    {
        static double GiveAt(double distance) => Give * (1 - 1 / (distance / Give + 1));
        return at < Margin ? Margin - GiveAt(Margin - at) : at > size - Margin ? size - Margin + GiveAt(at - size + Margin) : at;
    }
    public static double Fade(double room) => Clamp(room / 28, 0, 1);

    // The before side of a line through center at angle, cut from the frame: the clip while the divider turns.
    public static List<Point> HalfPlane(double w, double h, Point center, double angle)
    {
        double nx = -Math.Cos(angle), ny = -Math.Sin(angle);
        double Side(Point p) => (p.X - center.X) * nx + (p.Y - center.Y) * ny;
        Point[] corners = [new(0, 0), new(w, 0), new(w, h), new(0, h)];
        var points = new List<Point>();
        for (int i = 0; i < 4; i++)
        {
            Point corner = corners[i], next = corners[(i + 1) % 4];
            double a = Side(corner), b = Side(next);
            if (a >= 0) { points.Add(corner); }
            if ((a >= 0) != (b >= 0)) { double t = a / (a - b); points.Add(new Point(corner.X + (next.X - corner.X) * t, corner.Y + (next.Y - corner.Y) * t)); }
        }
        return points;
    }

    // Keys move the divider the way they point along its axis; the other two follow the value.
    public static double? KeyStep(Key key, bool shift, bool vertical)
    {
        double large = shift ? 10 : 1;
        return key switch
        {
            Key.PageUp => -10,
            Key.PageDown => 10,
            Key.Up => -large,
            Key.Down => large,
            Key.Left => vertical ? large : -large,
            Key.Right => vertical ? -large : large,
            _ => null,
        };
    }
}

/// <summary>The divider's handle. A RangeBase, so the native range automation peer reports and sets the after share.</summary>
internal sealed class ImageCompareHandle : RangeBase
{
    private static readonly ControlTemplate HandleTemplate = Build();
    private static ControlTemplate Build()
    {
        var host = new FrameworkElementFactory(typeof(ContentPresenter), "Host");
        var template = new ControlTemplate(typeof(ImageCompareHandle)) { VisualTree = host };
        template.Seal();
        return template;
    }
    public ImageCompareHandle(UIElement content)
    {
        OverridesDefaultStyle = true;
        Template = HandleTemplate;
        Minimum = 0;
        Maximum = 100;
        SmallChange = 1;
        LargeChange = 10;
        Focusable = true;
        IsTabStop = true;
        FocusVisualStyle = null;
        _content = content;
    }
    private readonly UIElement _content;
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("Host") is ContentPresenter host) { host.Content = _content; }
    }
}

/// <summary>
/// Before and after, split by a divider. Drag anywhere to move it: the handle follows 1:1, rubber-bands past the edges and stays where it is released.
/// A press on the image springs the divider there; on touch the press waits for a drag along the axis so the page still scrolls.
/// Arrow keys step 1% (Shift for 10%), Home and End reveal one side fully, and a double-click on the handle springs it back to the middle.
/// </summary>
public class ImageCompare : Control
{
    public static readonly DependencyProperty BeforeProperty = DependencyProperty.Register(nameof(Before), typeof(object), typeof(ImageCompare), new PropertyMetadata(null, (d, e) => ((ImageCompare)d)._beforeHost.Content = e.NewValue));
    public static readonly DependencyProperty AfterProperty = DependencyProperty.Register(nameof(After), typeof(object), typeof(ImageCompare), new PropertyMetadata(null, (d, e) => ((ImageCompare)d)._afterHost.Content = e.NewValue));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(ImageCompare), new FrameworkPropertyMetadata(50d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ImageCompare)d).OnPosition((double)e.NewValue)));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ImageCompare), new PropertyMetadata(Orientation.Horizontal, (d, _) => ((ImageCompare)d).OnOrientation()));
    public static readonly DependencyProperty BeforeLabelProperty = DependencyProperty.Register(nameof(BeforeLabel), typeof(string), typeof(ImageCompare), new PropertyMetadata("Before", (d, _) => ((ImageCompare)d).OnLabels()));
    public static readonly DependencyProperty AfterLabelProperty = DependencyProperty.Register(nameof(AfterLabel), typeof(string), typeof(ImageCompare), new PropertyMetadata("After", (d, _) => ((ImageCompare)d).OnLabels()));
    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(ImageCompare), new PropertyMetadata(true, (d, _) => ((ImageCompare)d).OnLabels()));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ImageCompare), new PropertyMetadata("Before and after", (d, e) => AutomationProperties.SetName(((ImageCompare)d)._handle, (string)e.NewValue)));
    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(nameof(AspectRatio), typeof(double), typeof(ImageCompare), new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly RoutedEvent PositionChangedEvent = EventManager.RegisterRoutedEvent(nameof(PositionChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double>), typeof(ImageCompare));

    private sealed class DragState { public double Grab; public bool Press; public bool Started; public Point Start; public double Raw; public bool Touch; }

    private readonly ContentControl _afterHost = new() { Focusable = false, IsTabStop = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly ContentControl _beforeHost = new() { Focusable = false, IsTabStop = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Canvas _pivot = new() { IsHitTestVisible = true, Background = null };
    private readonly Rectangle _line = new() { Width = 2 };
    private readonly Rectangle _lineEdge = new() { Width = 3 };
    private readonly RotateTransform _rotor = new();
    private readonly Canvas _rotorHost = new() { IsHitTestVisible = false };
    private readonly Border _handleFace = new() { BorderThickness = new Thickness(1) };
    private readonly Border _ring = new() { BorderThickness = new Thickness(2), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly ImageCompareHandle _handle;
    private readonly TranslateTransform _handleMove = new();
    private readonly RotateTransform _chevronTurn = new();
    private readonly TranslateTransform _chevronLeft = new(), _chevronRight = new();
    private readonly TextBlock _chipBeforeText = new(), _chipAfterText = new(), _chipBottomText = new();
    private readonly Border _chipBefore, _chipAfter, _chipBottom;
    private readonly ThemedBrush _handleEdge;
    private readonly OwnedMotion _motion = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _pos, _lag, _theta, _capW, _capH, _spread;
    private Decorator? _root;
    private Grid? _frame;
    private DragState? _drag;
    private bool _pressedHandle, _quiet, _dragging, _committing;
    private double _latest = 50, _goal = 50;
    private double _lastAt; private long _lastTick; private double _posVelocity;

    static ImageCompare()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageCompare), new FrameworkPropertyMetadata(typeof(ImageCompare)));
        HorizontalAlignmentProperty.OverrideMetadata(typeof(ImageCompare), new FrameworkPropertyMetadata(HorizontalAlignment.Stretch));
    }

    public ImageCompare()
    {
        _driver = new SpringDriver(Apply);
        _pos = _driver.Add(50, ArcSpring.Snappy);
        _lag = _driver.Add(0, ArcSpring.Snappy);
        _theta = _driver.Add(0, ArcSpring.Morph);
        _capW = _driver.Add(40, Tween.Morph);
        _capH = _driver.Add(40, Tween.Morph);
        _spread = _driver.Add(0, Tween.Morph);
        _chipBefore = Chip(_chipBeforeText);
        _chipAfter = Chip(_chipAfterText);
        _chipBottom = Chip(_chipBottomText);
        _handleEdge = new ThemedBrush(_handleFace, _motion);
        _handleFace.BorderBrush = _handleEdge.Brush;
        _handle = new ImageCompareHandle(BuildHandle());
        AutomationProperties.SetName(_handle, "Before and after");
        _handle.PreviewKeyDown += OnHandleKey;
        _handle.GotKeyboardFocus += (_, _) => RefreshRing();
        _handle.LostKeyboardFocus += (_, _) => { _quiet = false; RefreshRing(); };
        _handle.ValueChanged += (_, e) => { if (!_committing && Math.Abs(e.NewValue - Math.Round(100 - _latest)) > .001) { Commit(ImageCompareMath.Clamp(100 - e.NewValue, 0, 100)); } };
        Cursor = Cursors.SizeWE;
        Loaded += (_, _) => { _handleEdge.To(IsMouseOver ? "BorderStrong" : "Border", false); SyncValue(); Apply(); };
        Unloaded += (_, _) => { _driver.Finish(); _motion.Stop(); };
        MouseEnter += (_, _) => _handleEdge.To("BorderStrong", true);
        MouseLeave += (_, _) => _handleEdge.To("Border", true);
        ThemeWatch.Add(this, () => _handleEdge.To(IsMouseOver ? "BorderStrong" : "Border", false));
    }

    public object? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }
    public object? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }
    /// <summary>Where the divider sits, in percent from the left (or top).</summary>
    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    /// <summary>Vertical stacks the images. Changing it swings the divider a quarter turn instead of swapping layouts.</summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public string BeforeLabel { get => (string)GetValue(BeforeLabelProperty); set => SetValue(BeforeLabelProperty, value); }
    public string AfterLabel { get => (string)GetValue(AfterLabelProperty); set => SetValue(AfterLabelProperty, value); }
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <summary>Accessible name of the divider.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Frame width divided by height.</summary>
    public double AspectRatio { get => (double)GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }
    public event RoutedPropertyChangedEventHandler<double> PositionChanged { add => AddHandler(PositionChangedEvent, value); remove => RemoveHandler(PositionChangedEvent, value); }

    private bool Vertical => Orientation == Orientation.Vertical;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private double FrameW => _frame?.ActualWidth ?? 0;
    private double FrameH => _frame?.ActualHeight ?? 0;

    internal ImageCompareHandle HandleForTest => _handle;
    internal double ShownForTest => _pos.Position + _lag.Position;
    internal double ThetaForTest => _theta.Position;
    internal bool DraggingForTest => _dragging;
    internal Size CapsuleForTest => new(_capW.Target, _capH.Target);
    internal Geometry? BeforeClipForTest => _beforeHost.Clip;
    internal double ChipOpacityForTest(int index) => (index == 0 ? _chipBefore : index == 1 ? _chipAfter : _chipBottom).Opacity;
    internal void FinishForTest() => _driver.Finish();
    internal void PressForTest(Point at, bool onHandle, bool touch = false) => Press(at, onHandle, touch);
    internal void MoveForTest(Point at) => Move(at);
    internal void ReleaseForTest(Point at) => Release(at, true);

    private static Border Chip(TextBlock text)
    {
        text.FontSize = 12;
        text.FontWeight = FontWeights.Medium;
        text.LineHeight = 16.8;
        text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        text.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        var chip = new Border { Child = text, Padding = new Thickness(10, 3, 10, 3), CornerRadius = new CornerRadius(11.4), IsHitTestVisible = false };
        chip.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.CompareChip");
        return chip;
    }

    private UIElement BuildHandle()
    {
        _handleFace.CornerRadius = new CornerRadius(999);
        _handleFace.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _handleFace.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
        _handleFace.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Raised");
        var ink = new SolidColorBrush();
        var chevrons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5), RenderTransform = _chevronTurn };
        var left = new ArcDataGridIcon { Icon = "chevron-left", Size = 16, StrokeThickness = 1.75, Margin = new Thickness(-2.5, 0, -2.5, 0), RenderTransform = _chevronLeft };
        var right = new ArcDataGridIcon { Icon = "chevron-right", Size = 16, StrokeThickness = 1.75, Margin = new Thickness(-2.5, 0, -2.5, 0), RenderTransform = _chevronRight };
        left.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Foreground");
        right.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Foreground");
        chevrons.Children.Add(left);
        chevrons.Children.Add(right);
        _handleFace.Child = chevrons;
        _ring.Margin = new Thickness(-4);
        _ring.CornerRadius = new CornerRadius(999);
        _ring.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.AccentStrong");
        return new Grid { Children = { _handleFace, _ring } };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _frame = new Grid { ClipToBounds = true };
        _frame.SetResourceReference(Panel.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _frame.SizeChanged += (_, e) => { _frame.Clip = new RectangleGeometry(new Rect(e.NewSize), 26, 26); Apply(); };
        _frame.Children.Add(_afterHost);
        _frame.Children.Add(_beforeHost);
        _chipBefore.HorizontalAlignment = HorizontalAlignment.Left; _chipBefore.VerticalAlignment = VerticalAlignment.Top; _chipBefore.Margin = new Thickness(12);
        _chipAfter.HorizontalAlignment = HorizontalAlignment.Right; _chipAfter.VerticalAlignment = VerticalAlignment.Top; _chipAfter.Margin = new Thickness(12);
        _chipBottom.HorizontalAlignment = HorizontalAlignment.Left; _chipBottom.VerticalAlignment = VerticalAlignment.Bottom; _chipBottom.Margin = new Thickness(12);
        _frame.Children.Add(_chipBefore);
        _frame.Children.Add(_chipAfter);
        _frame.Children.Add(_chipBottom);
        _line.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.CompareLine");
        _lineEdge.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.CompareLineEdge");
        _rotorHost.RenderTransform = _rotor;
        _rotorHost.Children.Add(_lineEdge);
        _rotorHost.Children.Add(_line);
        _pivot.Children.Add(_rotorHost);
        _pivot.Children.Add(_handle);
        _frame.Children.Add(_pivot);
        var hairline = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), IsHitTestVisible = false };
        hairline.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.CompareHairline");
        _frame.Children.Add(hairline);
        _handle.RenderTransform = _handleMove;
        _root.Child = _frame;
        double start = ImageCompareMath.Clamp(Position, 0, 100);
        _latest = _goal = start;
        _pos.Snap(start);
        _theta.Snap(Vertical ? 90 : 0);
        Cursor = Vertical ? Cursors.SizeNS : Cursors.SizeWE;
        OnLabels();
        SyncValue();
        Apply();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double width = double.IsInfinity(constraint.Width) ? 480 : constraint.Width;
        double height = width / Math.Max(.01, AspectRatio);
        if (!double.IsInfinity(constraint.Height) && !double.IsNaN(Height)) { height = Math.Min(height, constraint.Height); }
        var size = new Size(width, height);
        base.MeasureOverride(size);
        return size;
    }

    private void OnLabels()
    {
        _chipBeforeText.Text = BeforeLabel;
        _chipAfterText.Text = AfterLabel;
        _chipBottomText.Text = AfterLabel;
        var show = ShowLabels ? Visibility.Visible : Visibility.Collapsed;
        _chipBefore.Visibility = _chipAfter.Visibility = _chipBottom.Visibility = show;
        Apply();
    }

    private void OnOrientation()
    {
        Cursor = Vertical ? Cursors.SizeNS : Cursors.SizeWE;
        double target = Vertical ? 90 : 0;
        if (Full) { _driver.Animate(_theta, target, ArcSpring.Morph); } else { _theta.Snap(target); }
        RefreshCapsule();
        Apply();
    }

    // Positions that arrive from outside a drag (props, keys, a double-click) spring the divider there.
    private void OnPosition(double value)
    {
        double current = ImageCompareMath.Clamp(value, 0, 100);
        _latest = current;
        SyncValue();
        if (_drag?.Started == true || _goal == current) { return; }
        _goal = current;
        double from = _pos.Position + _lag.Position;
        _lag.Snap(0);
        _pos.Snap(from);
        if (Full) { _driver.Animate(_pos, current, ArcSpring.Snappy); } else { _pos.Snap(current); }
        Apply();
    }

    private void SyncValue()
    {
        _committing = true;
        _handle.Value = Math.Round(100 - _latest);
        AutomationProperties.SetItemStatus(_handle, $"{Math.Round(100 - _latest)}% after");
        _committing = false;
    }

    private void Commit(double next, bool live = false)
    {
        if (live && Math.Round(next, MidpointRounding.AwayFromZero) == Math.Round(_latest, MidpointRounding.AwayFromZero)) { return; }
        double value = Math.Round(next, 2);
        if (value == _latest) { return; }
        double old = _latest;
        _latest = value;
        if (_drag?.Started != true && !live) { /* the callback springs it */ } else { _goal = _drag?.Started == true ? _goal : value; }
        SetCurrentValue(PositionProperty, value);
        RaiseEvent(new RoutedPropertyChangedEventArgs<double>(old, value, PositionChangedEvent));
    }

    private double PercentAt(Point at) => Vertical ? at.Y / Math.Max(1, FrameH) * 100 : at.X / Math.Max(1, FrameW) * 100;
    private double AxisSize => Math.Max(1, Vertical ? FrameH : FrameW);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_frame is null) { return; }
        bool onHandle = IsOn(e.OriginalSource as DependencyObject, _handle);
        if (e.ClickCount == 2)
        {
            // Pointer capture sends the double-click to the frame, so it checks where the presses began.
            if (onHandle && _latest != 50) { Commit(50); }
            e.Handled = true;
            return;
        }
        Press(e.GetPosition(_frame), onHandle, e.StylusDevice is not null);
        _frame.CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_frame is not null && _drag is not null) { Move(e.GetPosition(_frame)); }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_frame is null || _drag is null) { return; }
        Release(e.GetPosition(_frame), true);
        _frame.ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_frame is not null && _drag is not null) { Release(e.GetPosition(_frame), false); }
    }

    private static bool IsOn(DependencyObject? source, DependencyObject target)
    {
        for (var node = source; node is not null; node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node)) { if (ReferenceEquals(node, target)) { return true; } }
        return false;
    }

    private void Press(Point at, bool onHandle, bool touch)
    {
        _pressedHandle = onHandle;
        var state = new DragState { Press = !onHandle, Start = at, Touch = touch };
        _drag = state;
        // A finger on the photo might be scrolling the page, so it waits for a drag along the axis; a mouse or the handle answers at once.
        if (!touch || onHandle) { Start(state, PercentAt(at)); }
    }

    private void Move(Point at)
    {
        if (_drag is not { } state) { return; }
        if (!state.Started)
        {
            double dx = Math.Abs(at.X - state.Start.X), dy = Math.Abs(at.Y - state.Start.Y);
            double along = Vertical ? dy : dx, across = Vertical ? dx : dy;
            if (along < 6 && across < 6) { return; }
            if (across > along) { _drag = null; _frame?.ReleaseMouseCapture(); return; }
            Start(state, PercentAt(at));
            return;
        }
        Follow(state, PercentAt(at));
    }

    private void Start(DragState state, double at)
    {
        double from = _pos.Position + _lag.Position;
        state.Started = true;
        state.Grab = state.Press ? 0 : at - from;
        _driver.Stop();
        _lag.Snap(0);
        _pos.Snap(from);
        _dragging = true;
        RefreshCapsule();
        FocusQuietly();
        Follow(state, at, true);
    }

    // Puts the line under the pointer, with resistance past the edges, and commits once per whole percent.
    private void Follow(DragState state, double at, bool first = false)
    {
        double raw = at - state.Grab, size = AxisSize;
        double edge = ImageCompareMath.Clamp(raw, 0, 100);
        bool full = Tween.Level(this) == ArcMotionLevel.Full;
        double placed = !full ? edge : edge + ImageCompareMath.Rubber((raw - edge) / 100 * size) / size * 100;
        state.Raw = raw;
        if (first && state.Press && Full)
        {
            double from = _pos.Position;
            _pos.Snap(placed);
            _lag.Snap(from - placed);
            _driver.Animate(_lag, 0, ArcSpring.Snappy);
        }
        else
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double dt = System.Diagnostics.Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds;
            if (_lastTick != 0 && dt > 0 && dt < .1) { _posVelocity = (placed - _lastAt) / dt; }
            _lastTick = now; _lastAt = placed;
            _pos.Snap(placed);
        }
        Commit(edge, true);
        Apply();
    }

    private void Release(Point at, bool up)
    {
        if (_drag is not { } state) { return; }
        _drag = null;
        // A tap on the photo moves the divider there.
        if (!state.Started)
        {
            if (up) { FocusQuietly(); Commit(ImageCompareMath.Clamp(PercentAt(at), 0, 100)); }
            return;
        }
        // The divider stays where it was let go. Past an edge, or still catching up to a press, it springs on with the speed it had.
        double from = _pos.Position + _lag.Position;
        double velocity = _lag.Velocity + (from is > 100 or < 0 ? _posVelocity : 0);
        double target = ImageCompareMath.Clamp(_lag.Position != 0 ? state.Raw : from, 0, 100);
        _lag.Snap(0);
        _pos.Snap(from);
        _goal = Math.Round(target, 2);
        if (from != target)
        {
            if (Full) { _pos.Launch(velocity); _driver.Animate(_pos, target, ArcSpring.Snappy); } else { _pos.Snap(target); }
        }
        _dragging = false;
        _lastTick = 0;
        Commit(target);
        RefreshCapsule();
        Apply();
    }

    // Focus that follows a pointer keeps the handle quiet: no ring until a key is pressed.
    private void FocusQuietly()
    {
        if (_handle.IsKeyboardFocused) { return; }
        _quiet = true;
        _handle.Focus();
        RefreshRing();
    }

    private void RefreshRing() => _ring.Visibility = _handle.IsKeyboardFocused && !_quiet ? Visibility.Visible : Visibility.Collapsed;

    private void OnHandleKey(object sender, KeyEventArgs e)
    {
        double next;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (e.Key == Key.Home) { next = 100; }
        else if (e.Key == Key.End) { next = 0; }
        else if (ImageCompareMath.KeyStep(e.Key, shift, Vertical) is { } step) { next = Math.Round(_latest, MidpointRounding.AwayFromZero) + step; }
        else { return; }
        e.Handled = true;
        _quiet = false;
        RefreshRing();
        double clamped = ImageCompareMath.Clamp(next, 0, 100);
        if (clamped == _latest)
        {
            if (Full && next != clamped)
            {
                _pos.Launch(Math.Sign(next - clamped) * ImageCompareMath.BumpPx / AxisSize * 100);
                _driver.Animate(_pos, _goal, ArcSpring.Morph);
            }
            return;
        }
        Commit(clamped);
    }

    private void RefreshCapsule()
    {
        double w = _dragging ? (Vertical ? 36 : 56) : 40, h = _dragging ? (Vertical ? 56 : 36) : 40, spread = _dragging ? 4 : 0;
        if (Full) { _driver.Animate(_capW, w, Tween.Morph); _driver.Animate(_capH, h, Tween.Morph); _driver.Animate(_spread, spread, Tween.Morph); }
        else { _capW.Snap(w); _capH.Snap(h); _spread.Snap(spread); }
    }

    private void Apply()
    {
        double w = FrameW, h = FrameH;
        if (w <= 0 || h <= 0) { return; }
        double shown = _pos.Position + _lag.Position, angle = _theta.Position;
        double turn = ImageCompareMath.Clamp(angle / 90, 0, 1), p = ImageCompareMath.Clamp(shown, 0, 100);
        // The divider's pivot in pixels, blending from the horizontal to the vertical layout as it turns.
        var center = new Point(ImageCompareMath.Lerp(shown / 100 * w, w / 2, turn), ImageCompareMath.Lerp(h / 2, shown / 100 * h, turn));
        var clipCenter = new Point(ImageCompareMath.Lerp(p / 100 * w, w / 2, turn), ImageCompareMath.Lerp(h / 2, p / 100 * h, turn));
        if (angle == 0) { _beforeHost.Clip = new RectangleGeometry(new Rect(0, 0, p / 100 * w, h)); }
        else if (angle == 90) { _beforeHost.Clip = new RectangleGeometry(new Rect(0, 0, w, p / 100 * h)); }
        else
        {
            var points = ImageCompareMath.HalfPlane(w, h, clipCenter, angle * Math.PI / 180);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                if (points.Count > 0) { context.BeginFigure(points[0], true, true); context.PolyLineTo(points.Skip(1).ToList(), true, false); }
            }
            geometry.Freeze();
            _beforeHost.Clip = geometry;
        }
        double reach = (w + h) * 2;
        _line.Height = _lineEdge.Height = reach;
        Canvas.SetLeft(_line, -1); Canvas.SetTop(_line, -reach / 2);
        Canvas.SetLeft(_lineEdge, -1.5); Canvas.SetTop(_lineEdge, -reach / 2);
        Canvas.SetLeft(_rotorHost, center.X); Canvas.SetTop(_rotorHost, center.Y);
        _rotor.Angle = angle;
        // How far the handle sits from the line along the drag axis, so it stays whole near an edge.
        double x = shown / 100 * w, y = shown / 100 * h;
        double offset = ImageCompareMath.Lerp(ImageCompareMath.Soften(x, w) - x, ImageCompareMath.Soften(y, h) - y, turn);
        double radians = angle * Math.PI / 180;
        double capW = _capW.Position, capH = _capH.Position;
        _handle.Width = capW; _handle.Height = capH;
        Canvas.SetLeft(_handle, center.X - capW / 2); Canvas.SetTop(_handle, center.Y - capH / 2);
        _handleMove.X = offset * Math.Cos(radians);
        _handleMove.Y = offset * Math.Sin(radians);
        _chevronTurn.Angle = angle;
        _chevronLeft.X = -_spread.Position;
        _chevronRight.X = _spread.Position;
        // Captions fade as the divider reaches them, and hand over mid-turn when the after caption changes corner.
        double across = ImageCompareMath.Clamp(1 - turn * 3, 0, 1), down = ImageCompareMath.Clamp(turn * 3 - 2, 0, 1);
        double bw = _chipBefore.ActualWidth, bh = _chipBefore.ActualHeight, aw = _chipAfter.ActualWidth, ah = _chipAfter.ActualHeight;
        const double inset = ImageCompareMath.Inset;
        _chipBefore.Opacity = across * ImageCompareMath.Fade(p / 100 * w - (inset + bw + 8)) + down * ImageCompareMath.Fade(p / 100 * h - (inset + bh + 8));
        _chipAfter.Opacity = across * ImageCompareMath.Fade(w - inset - aw - 8 - p / 100 * w);
        _chipBottom.Opacity = down * ImageCompareMath.Fade(h - inset - ah - 8 - p / 100 * h);
    }
}
