using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ScrollAreaOrientation { Vertical, Horizontal, Both }
public enum ScrollAreaScrollbars { Auto, Always }

/// <summary>Which edges still have content beyond them.</summary>
public readonly record struct ScrollAreaEdges(bool Top, bool Bottom, bool Left, bool Right);

public sealed class ScrollAreaEdgesChangedEventArgs(RoutedEvent routedEvent, object source, ScrollAreaEdges edges) : RoutedEventArgs(routedEvent, source)
{
    public ScrollAreaEdges Edges { get; } = edges;
}

[TemplatePart(Name = "PART_Viewport", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_FadeX", Type = typeof(Border))]
[TemplatePart(Name = "PART_TrackY", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ThumbY", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PillY", Type = typeof(Border))]
[TemplatePart(Name = "PART_TrackX", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ThumbX", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_PillX", Type = typeof(Border))]
public class ScrollArea : ContentControl
{
    // scroll-area.tsx: native scrolling, platform bars hidden, overlay pills and proportional edge fades on top.
    private const double MinThumb = 28;
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(ScrollAreaOrientation), typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollAreaOrientation.Vertical, OnLayoutChanged));
    public static readonly DependencyProperty FadeProperty = DependencyProperty.Register(nameof(Fade), typeof(double), typeof(ScrollArea), new FrameworkPropertyMetadata(28d, OnLayoutChanged), v => v is double d && d >= 0 && double.IsFinite(d));
    public static readonly DependencyProperty ScrollbarsProperty = DependencyProperty.Register(nameof(Scrollbars), typeof(ScrollAreaScrollbars), typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollAreaScrollbars.Auto, OnStateChanged));
    public static readonly DependencyProperty HideDelayProperty = DependencyProperty.Register(nameof(HideDelay), typeof(TimeSpan), typeof(ScrollArea), new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(900)), v => v is TimeSpan t && t >= TimeSpan.Zero);
    public static readonly DependencyProperty WheelToHorizontalProperty = DependencyProperty.Register(nameof(WheelToHorizontal), typeof(bool), typeof(ScrollArea), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ScrollArea), new FrameworkPropertyMetadata(null, OnLabelChanged));
    public static readonly DependencyProperty CanContentScrollProperty = ScrollViewer.CanContentScrollProperty.AddOwner(typeof(ScrollArea), new FrameworkPropertyMetadata(false));
    private static readonly DependencyPropertyKey EdgesPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Edges), typeof(ScrollAreaEdges), typeof(ScrollArea), new FrameworkPropertyMetadata(default(ScrollAreaEdges)));
    public static readonly DependencyProperty EdgesProperty = EdgesPropertyKey.DependencyProperty;
    public static readonly RoutedEvent EdgesChangedEvent = EventManager.RegisterRoutedEvent(nameof(EdgesChanged), RoutingStrategy.Bubble, typeof(EventHandler<ScrollAreaEdgesChangedEventArgs>), typeof(ScrollArea));
    // Resource probes keep DynamicResource ownership; the pill brushes are private copies that animate.
    private static readonly DependencyProperty ThumbBrushProperty = DependencyProperty.Register("ThumbBrush", typeof(Brush), typeof(ScrollArea), new PropertyMetadata(null, OnStateChanged));
    private static readonly DependencyProperty ThumbHoverBrushProperty = DependencyProperty.Register("ThumbHoverBrush", typeof(Brush), typeof(ScrollArea), new PropertyMetadata(null, OnStateChanged));

    public ScrollAreaOrientation Orientation { get => (ScrollAreaOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Fade { get => (double)GetValue(FadeProperty); set => SetValue(FadeProperty, value); }
    public ScrollAreaScrollbars Scrollbars { get => (ScrollAreaScrollbars)GetValue(ScrollbarsProperty); set => SetValue(ScrollbarsProperty, value); }
    public TimeSpan HideDelay { get => (TimeSpan)GetValue(HideDelayProperty); set => SetValue(HideDelayProperty, value); }
    public bool WheelToHorizontal { get => (bool)GetValue(WheelToHorizontalProperty); set => SetValue(WheelToHorizontalProperty, value); }
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool CanContentScroll { get => (bool)GetValue(CanContentScrollProperty); set => SetValue(CanContentScrollProperty, value); }
    public ScrollAreaEdges Edges => (ScrollAreaEdges)GetValue(EdgesProperty);
    public event EventHandler<ScrollAreaEdgesChangedEventArgs> EdgesChanged { add => AddHandler(EdgesChangedEvent, value); remove => RemoveHandler(EdgesChangedEvent, value); }
    /// <summary>The scrolling element, for programmatic scrolling.</summary>
    public ScrollViewer? Viewport => _viewport;

    private ScrollViewer? _viewport;
    private Border? _fadeX, _pillY, _pillX;
    private FrameworkElement? _trackY, _thumbY, _trackX, _thumbX;
    private readonly TranslateTransform _moveY = new(), _moveX = new();
    private readonly LinearGradientBrush _maskY = Mask(true), _maskX = Mask(false);
    private readonly SolidColorBrush _pillBrushY = new(), _pillBrushX = new();
    private readonly OwnedMotion _motion = new();
    private readonly DispatcherTimer _hideTimer = new(DispatcherPriority.Normal);
    private readonly SpringDriver _paging;
    private readonly ControlSpring _pageY, _pageX;
    private bool _scrolling, _trackHoverY, _trackHoverX;
    private char _dragging;
    private double _dragStart, _dragScroll, _dragRatio;
    private bool? _shownY, _shownX;

    static ScrollArea()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(typeof(ScrollArea)));
        FocusableProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(false));
    }

    public ScrollArea()
    {
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); _scrolling = false; UpdateTracks(true); };
        _paging = new SpringDriver(ApplyPaging);
        _pageY = _paging.Add(0, .4); _pageX = _paging.Add(0, .4);
        SetResourceReference(ThumbBrushProperty, "ArcWpf.Brush.ScrollAreaThumb");
        SetResourceReference(ThumbHoverBrushProperty, "ArcWpf.Brush.ScrollAreaThumbHover");
        Unloaded += (_, _) => { _hideTimer.Stop(); _scrolling = false; _paging.Stop(); EndDrag(); _motion.Stop(); UpdateTracks(false); };
    }

    public override void OnApplyTemplate()
    {
        Release();
        base.OnApplyTemplate();
        _viewport = GetTemplateChild("PART_Viewport") as ScrollViewer;
        _fadeX = GetTemplateChild("PART_FadeX") as Border;
        _trackY = GetTemplateChild("PART_TrackY") as FrameworkElement; _thumbY = GetTemplateChild("PART_ThumbY") as FrameworkElement; _pillY = GetTemplateChild("PART_PillY") as Border;
        _trackX = GetTemplateChild("PART_TrackX") as FrameworkElement; _thumbX = GetTemplateChild("PART_ThumbX") as FrameworkElement; _pillX = GetTemplateChild("PART_PillX") as Border;
        if (_viewport is not null)
        {
            _viewport.ScrollChanged += ViewportScrolled;
            _viewport.PreviewMouseWheel += ViewportWheel;
            AutomationProperties.SetName(_viewport, Label ?? string.Empty);
        }
        Wire(_trackY, _thumbY, _moveY, _pillY, _pillBrushY);
        Wire(_trackX, _thumbX, _moveX, _pillX, _pillBrushX);
        ApplyOrientation();
        UpdateTracks(false);
    }

    private void Wire(FrameworkElement? track, FrameworkElement? thumb, TranslateTransform move, Border? pill, SolidColorBrush brush)
    {
        if (track is not null)
        {
            track.MouseLeftButtonDown += TrackDown;
            track.MouseEnter += TrackHover; track.MouseLeave += TrackHover;
            track.SizeChanged += TrackSized;
        }
        if (thumb is not null)
        {
            thumb.RenderTransform = move;
            thumb.MouseLeftButtonDown += ThumbDown; thumb.MouseMove += ThumbMove;
            thumb.MouseLeftButtonUp += ThumbUp; thumb.LostMouseCapture += ThumbLost;
        }
        if (pill is not null) { pill.Background = brush; }
    }

    private void Release()
    {
        if (_viewport is not null) { _viewport.ScrollChanged -= ViewportScrolled; _viewport.PreviewMouseWheel -= ViewportWheel; _viewport.OpacityMask = null; }
        foreach (var track in new[] { _trackY, _trackX }) { if (track is not null) { track.MouseLeftButtonDown -= TrackDown; track.MouseEnter -= TrackHover; track.MouseLeave -= TrackHover; track.SizeChanged -= TrackSized; } }
        foreach (var thumb in new[] { _thumbY, _thumbX }) { if (thumb is not null) { thumb.MouseLeftButtonDown -= ThumbDown; thumb.MouseMove -= ThumbMove; thumb.MouseLeftButtonUp -= ThumbUp; thumb.LostMouseCapture -= ThumbLost; } }
        EndDrag(); _paging.Stop(); _motion.Stop();
        _viewport = null; _fadeX = _pillY = _pillX = null; _trackY = _thumbY = _trackX = _thumbX = null; _shownY = _shownX = null;
    }

    private static LinearGradientBrush Mask(bool vertical)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = vertical ? new Point(0, 1) : new Point(1, 0) };
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Black, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Black, 1));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        return brush;
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var area = (ScrollArea)d; area.ApplyOrientation(); area.Sync(); }
    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ScrollArea)d).UpdateTracks(false);
    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { if (((ScrollArea)d)._viewport is { } viewport) AutomationProperties.SetName(viewport, (string?)e.NewValue ?? string.Empty); }

    private bool Vertical => Orientation != ScrollAreaOrientation.Horizontal;
    private bool Horizontal => Orientation != ScrollAreaOrientation.Vertical;

    private void ApplyOrientation()
    {
        if (_viewport is null) return;
        // Hidden keeps native scrolling while the overlay bars replace the platform ones.
        _viewport.VerticalScrollBarVisibility = Vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        _viewport.HorizontalScrollBarVisibility = Horizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        bool both = Orientation == ScrollAreaOrientation.Both;
        if (_trackY is not null) _trackY.Margin = new Thickness(0, 3, 1, both ? 13 : 3);
        if (_trackX is not null) _trackX.Margin = new Thickness(3, 0, both ? 13 : 3, 1);
    }

    private void ViewportScrolled(object sender, ScrollChangedEventArgs e)
    {
        Sync();
        // Extent or viewport resizes are layout, not scrolling; only offset changes wake the bars.
        if (e.VerticalChange != 0 || e.HorizontalChange != 0) Wake();
    }

    private void TrackSized(object sender, SizeChangedEventArgs e) => Sync();

    // Everything visual is written straight to the parts on each scroll change.
    private void Sync()
    {
        if (_viewport is not { } node) return;
        double maxY = Math.Max(0, node.ExtentHeight - node.ViewportHeight), maxX = Math.Max(0, node.ExtentWidth - node.ViewportWidth);
        double top = node.VerticalOffset, left = node.HorizontalOffset;
        var edges = new ScrollAreaEdges(Vertical && top > .5, Vertical && maxY - top > .5, Horizontal && left > .5, Horizontal && maxX - left > .5);
        double fade = Fade;
        if (fade > 0 && Vertical && node.ActualHeight > 0)
        {
            SetStops(_maskY, Math.Min(fade, top) / node.ActualHeight, Math.Min(fade, maxY - top) / node.ActualHeight);
            node.OpacityMask = _maskY;
        }
        else node.OpacityMask = null;
        if (_fadeX is not null)
        {
            if (fade > 0 && Horizontal && _fadeX.ActualWidth > 0) { SetStops(_maskX, Math.Min(fade, left) / _fadeX.ActualWidth, Math.Min(fade, maxX - left) / _fadeX.ActualWidth); _fadeX.OpacityMask = _maskX; }
            else _fadeX.OpacityMask = null;
        }
        Place(_trackY, _thumbY, _moveY, Vertical, top, maxY, node.ViewportHeight, node.ExtentHeight, true);
        Place(_trackX, _thumbX, _moveX, Horizontal, left, maxX, node.ViewportWidth, node.ExtentWidth, false);
        if (edges != Edges)
        {
            SetValue(EdgesPropertyKey, edges);
            RaiseEvent(new ScrollAreaEdgesChangedEventArgs(EdgesChangedEvent, this, edges));
        }
    }

    private static void SetStops(LinearGradientBrush mask, double start, double end)
    {
        mask.GradientStops[1].Offset = Math.Clamp(start, 0, .5);
        mask.GradientStops[2].Offset = 1 - Math.Clamp(end, 0, .5);
    }

    private static void Place(FrameworkElement? track, FrameworkElement? thumb, TranslateTransform move, bool axis, double scroll, double max, double client, double total, bool y)
    {
        if (track is null || thumb is null) return;
        bool overflowing = axis && max > .5;
        track.Visibility = overflowing ? Visibility.Visible : Visibility.Collapsed;
        if (!overflowing) return;
        double length = y ? track.ActualHeight : track.ActualWidth;
        double size = Math.Min(length, Math.Max(MinThumb, length * client / Math.Max(1, total)));
        double offset = (length - size) * (scroll / max);
        if (y) { thumb.Height = size; move.Y = offset; } else { thumb.Width = size; move.X = offset; }
    }

    private void Wake()
    {
        _scrolling = true;
        _hideTimer.Stop();
        _hideTimer.Interval = HideDelay;
        _hideTimer.Start();
        UpdateTracks(true);
    }

    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); UpdateTracks(true); }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); UpdateTracks(true); }
    private void TrackHover(object sender, MouseEventArgs e)
    {
        _trackHoverY = _trackY?.IsMouseOver == true; _trackHoverX = _trackX?.IsMouseOver == true;
        UpdateTracks(true);
    }

    // Visible while scrolling, dragging, hovered, or always; appearing is quick, leaving lingers.
    private void UpdateTracks(bool animate)
    {
        bool shown = Scrollbars == ScrollAreaScrollbars.Always || _scrolling || _dragging != '\0' || IsMouseOver;
        var level = ArcMotion.GetLevel(this);
        bool motion = animate && IsLoaded && level == ArcMotionLevel.Full;
        foreach (var (track, y) in new[] { (_trackY, true), (_trackX, false) })
        {
            if (track is null) continue;
            track.IsHitTestVisible = shown;
            bool? last = y ? _shownY : _shownX;
            if (last != shown) _motion.Double(track, OpacityProperty, shown ? 1 : 0, shown ? .16 : .48, motion && last is not null);
            if (y) _shownY = shown; else _shownX = shown;
        }
        bool hotY = _trackHoverY || _dragging == 'y', hotX = _trackHoverX || _dragging == 'x';
        if (_pillY is not null) _motion.Double(_pillY, WidthProperty, hotY ? 7 : 5, .16, motion);
        if (_pillX is not null) _motion.Double(_pillX, HeightProperty, hotX ? 7 : 5, .16, motion);
        var rest = (GetValue(ThumbBrushProperty) as SolidColorBrush)?.Color ?? Colors.Gray;
        var hover = (GetValue(ThumbHoverBrushProperty) as SolidColorBrush)?.Color ?? Colors.Gray;
        _motion.Color(_pillBrushY, hotY || _dragging != '\0' ? hover : rest, .16, motion);
        _motion.Color(_pillBrushX, hotX || _dragging != '\0' ? hover : rest, .16, motion);
    }

    // Dragging a thumb maps pointer travel to scroll distance.
    private void ThumbDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewport is not { } node || sender is not FrameworkElement thumb) return;
        bool y = ReferenceEquals(thumb, _thumbY);
        var track = y ? _trackY : _trackX;
        if (track is null) return;
        e.Handled = true;
        _paging.Stop();
        double length = y ? track.ActualHeight : track.ActualWidth, size = y ? thumb.ActualHeight : thumb.ActualWidth;
        double max = y ? node.ScrollableHeight : node.ScrollableWidth;
        var point = e.GetPosition(track);
        _dragging = y ? 'y' : 'x';
        _dragStart = y ? point.Y : point.X;
        _dragScroll = y ? node.VerticalOffset : node.HorizontalOffset;
        _dragRatio = max / Math.Max(1, length - size);
        thumb.CaptureMouse();
        UpdateTracks(true);
    }

    private void ThumbMove(object sender, MouseEventArgs e)
    {
        if (_dragging == '\0' || _viewport is not { } node) return;
        bool y = _dragging == 'y';
        var track = y ? _trackY : _trackX;
        if (track is null) return;
        var point = e.GetPosition(track);
        double next = _dragScroll + ((y ? point.Y : point.X) - _dragStart) * _dragRatio;
        if (y) node.ScrollToVerticalOffset(next); else node.ScrollToHorizontalOffset(next);
    }

    private void ThumbUp(object sender, MouseButtonEventArgs e) { if (_dragging != '\0') { e.Handled = true; ((UIElement)sender).ReleaseMouseCapture(); } }
    private void ThumbLost(object sender, MouseEventArgs e) { if (_dragging != '\0') { _dragging = '\0'; Wake(); } }
    private void EndDrag()
    {
        if (_dragging == '\0') return;
        var thumb = _dragging == 'y' ? _thumbY : _thumbX;
        _dragging = '\0';
        thumb?.ReleaseMouseCapture();
    }

    // Pressing the track pages 90% of the viewport toward the pointer.
    private void TrackDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewport is not { } node || !ReferenceEquals(e.OriginalSource, sender)) return;
        bool y = ReferenceEquals(sender, _trackY);
        var thumb = y ? _thumbY : _thumbX;
        if (thumb is null) return;
        e.Handled = true;
        var point = e.GetPosition(thumb);
        bool before = y ? point.Y < 0 : point.X < 0;
        double page = (y ? node.ViewportHeight : node.ViewportWidth) * .9 * (before ? -1 : 1);
        double from = y ? node.VerticalOffset : node.HorizontalOffset, max = y ? node.ScrollableHeight : node.ScrollableWidth;
        var spring = y ? _pageY : _pageX;
        double target = Math.Clamp((_paging.IsRunning ? spring.Target : from) + page, 0, max);
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full || !IsLoaded) { if (y) node.ScrollToVerticalOffset(target); else node.ScrollToHorizontalOffset(target); return; }
        // WPF has no native smooth scroll; a smooth spring (0.4s) stands in for the browser's behavior.
        if (!_paging.IsRunning) { _pageY.Snap(node.VerticalOffset); _pageX.Snap(node.HorizontalOffset); }
        _paging.Animate(spring, target, .4);
    }

    private void ApplyPaging()
    {
        if (_viewport is not { } node) { _paging.Stop(); return; }
        if (Math.Abs(_pageY.Position - node.VerticalOffset) > .01 && Vertical) node.ScrollToVerticalOffset(_pageY.Position);
        if (Math.Abs(_pageX.Position - node.HorizontalOffset) > .01 && Horizontal) node.ScrollToHorizontalOffset(_pageX.Position);
    }

    // A vertical wheel over a horizontal strip moves it sideways while there is room; at the ends the parent scrolls.
    private void ViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewport is not { } node || Orientation != ScrollAreaOrientation.Horizontal || !WheelToHorizontal) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        double max = node.ScrollableWidth, at = node.HorizontalOffset, delta = -e.Delta;
        if (max <= 0 || (delta < 0 && at <= 0) || (delta > 0 && at >= max - .5))
        {
            if (VisualTreeHelper.GetParent(this) is UIElement parent)
                parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
            return;
        }
        _paging.Stop();
        double step = delta / Mouse.MouseWheelDeltaForOneLine * SystemParameters.WheelScrollLines * 16;
        // WPF offsets are logical in RTL too, so the browser's sign flip is not needed.
        node.ScrollToHorizontalOffset(at + step);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScrollAreaPeer(this);
    private sealed class ScrollAreaPeer(ScrollArea owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ScrollArea);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    }
}
