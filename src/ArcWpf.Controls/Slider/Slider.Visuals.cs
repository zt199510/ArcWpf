using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public partial class Slider
{
    private FrameworkElement? _surface, _thumbVisual, _focusRing;
    private Border? _bubble;
    private Canvas? _thumbCanvas, _bubbleCanvas, _marks, _ticks, _fillTicks;
    private Grid? _fillClip;
    private RectangleGeometry? _clip;
    private ScaleTransform? _thumbScale, _bubbleScale;
    private TranslateTransform? _bubbleMove;
    private BlurEffect? _bubbleBlur;
    private DropShadowEffect? _shadow;
    private SliderNumber? _readout, _bubbleNumber;
    private readonly OwnedMotion _visuals = new();
    private bool _rendering, _lingering, _bubbleShown;
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _linger = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly List<(SliderMark Mark, SliderMarkButton Button)> _markButtons = [];
    internal bool IsMotionRunning => _rendering || _visuals.IsRunning || (_readout?.IsRunning ?? false) || (_bubbleNumber?.IsRunning ?? false) || _markButtons.Any(m => m.Button.IsMotionRunning);
    internal bool HasPendingLinger => _linger.IsEnabled;

    public override void OnApplyTemplate()
    {
        ReleaseParts(); base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as FrameworkElement;
        _thumbVisual = GetTemplateChild("PART_ThumbVisual") as FrameworkElement;
        _focusRing = GetTemplateChild("PART_FocusRing") as FrameworkElement;
        _thumbCanvas = GetTemplateChild("PART_ThumbCanvas") as Canvas;
        _bubbleCanvas = GetTemplateChild("PART_BubbleCanvas") as Canvas;
        _bubble = GetTemplateChild("PART_Bubble") as Border;
        _fillClip = GetTemplateChild("PART_FillClip") as Grid;
        _marks = GetTemplateChild("PART_Marks") as Canvas;
        _ticks = GetTemplateChild("PART_Ticks") as Canvas;
        _fillTicks = GetTemplateChild("PART_FillTicks") as Canvas;
        if (_surface is not null)
        {
            _surface.SizeChanged += SurfaceSizeChanged;
            _surface.MouseMove += SurfaceMouseMove; _surface.MouseLeftButtonUp += SurfaceMouseUp; _surface.LostMouseCapture += SurfaceLostCapture;
        }
        if (_thumbVisual is not null) { _thumbScale = new(); _thumbVisual.RenderTransform = _thumbScale; }
        if (_fillClip is not null) { _clip = new(); _fillClip.Clip = _clip; }
        if (_bubble is not null)
        {
            _bubbleScale = new(); _bubbleMove = new(); _bubbleBlur = new() { Radius = 0 };
            var group = new TransformGroup(); group.Children.Add(_bubbleScale); group.Children.Add(_bubbleMove);
            _bubble.RenderTransform = group; _bubble.Effect = _bubbleBlur; _bubble.SizeChanged += BubbleSizeChanged;
        }
        if (GetTemplateChild("PART_ReadoutHost") is Panel host) { _readout = new(); host.Children.Add(_readout); }
        if (GetTemplateChild("PART_BubbleNumberHost") is Panel bubbleHost) { _bubbleNumber = new(); bubbleHost.Children.Add(_bubbleNumber); }
        _linger.Tick += LingerElapsed;
        RefreshText(false); RebuildMarks(); SnapVisuals(); UpdateState(false); UpdateShadow();
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    { ReleaseParts(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void SurfaceSizeChanged(object sender, SizeChangedEventArgs e) { PositionParts(); LayoutMarks(); }
    private void BubbleSizeChanged(object sender, SizeChangedEventArgs e) => PositionParts();
    private void UpdateShadow()
    {
        if (_thumbVisual is null) return;
        if (ThumbShadow is not { } source) { _thumbVisual.Effect = null; _shadow = null; return; }
        _shadow ??= source.Clone(); _thumbVisual.Effect = _shadow;
        _shadow.Color = source.Color; _shadow.Direction = source.Direction;
        _visuals.Double(_shadow, DropShadowEffect.OpacityProperty, source.Opacity, .16, FullMotion);
        _visuals.Double(_shadow, DropShadowEffect.BlurRadiusProperty, source.BlurRadius, .16, FullMotion);
        _visuals.Double(_shadow, DropShadowEffect.ShadowDepthProperty, source.ShadowDepth, .16, FullMotion);
    }
    private void LingerElapsed(object? sender, EventArgs e) { _linger.Stop(); _lingering = false; UpdateState(true); }
    private void MoveToValue()
    {
        if (FullMotion) { _position.To(Percent(Value)); _lag.To(0); }
        else { _position.Snap(Percent(Value)); _lag.Snap(0); }
        PositionParts(); EnsureRendering();
    }
    private void SnapVisuals()
    {
        StopRendering(); _visuals.Stop(); _readout?.Stop(); _bubbleNumber?.Stop();
        foreach (var mark in _markButtons) mark.Button.Stop();
        _position.Snap(Percent(Value)); _lag.Snap(0); PositionParts();
    }
    private void PositionParts()
    {
        if (_surface is null) return;
        double width = Travel, shown = Reversed ? 100 - ShownPosition : ShownPosition;
        double center = 11 + shown / 100 * width;
        if (_thumbVisual is not null) Canvas.SetLeft(_thumbVisual, center - 11);
        if (_focusRing is not null) Canvas.SetLeft(_focusRing, center - 15);
        if (_clip is not null)
        {
            double filled = Math.Clamp(ShownPosition / 100, 0, 1) * width;
            _clip.Rect = new Rect(Reversed ? width - filled : 0, 0, filled, 6);
        }
        if (_bubble is not null)
        {
            _bubble.MaxWidth = Math.Max(0, _surface.ActualWidth);
            double bubbleWidth = _bubble.ActualWidth;
            Canvas.SetLeft(_bubble, Math.Clamp(center - bubbleWidth / 2, 0, Math.Max(0, _surface.ActualWidth - bubbleWidth)));
            Canvas.SetTop(_bubble, 3 - _bubble.ActualHeight);
        }
    }
    private void UpdateState(bool animate)
    {
        bool full = FullMotion, moving = animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
        if (_thumbScale is not null)
        {
            _visuals.Double(_thumbScale, ScaleTransform.ScaleXProperty, IsDragging && full ? 1.16 : 1, .26, moving && full, spring: true);
            _visuals.Double(_thumbScale, ScaleTransform.ScaleYProperty, IsDragging && full ? 1.16 : 1, .26, moving && full, spring: true);
        }
        if (_focusRing is not null) _focusRing.Visibility = _keyFocus && IsKeyboardFocused && IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        bool show = IsEnabled && IsVisible && (IsDragging || (_keyFocus && IsKeyboardFocused) || _lingering);
        if (_bubble is null) return;
        if (show && !_bubbleShown && _bubble.Visibility != Visibility.Visible)
        {
            _bubble.Visibility = Visibility.Visible; _bubble.Opacity = moving ? 0 : 1;
            if (_bubbleScale is not null) _bubbleScale.ScaleX = _bubbleScale.ScaleY = full && moving ? .6 : 1;
            if (_bubbleMove is not null) _bubbleMove.Y = full && moving ? 6 : 0;
            if (_bubbleBlur is not null) _bubbleBlur.Radius = full && moving ? 2 : 0;
            _bubbleNumber?.SetNumber(ValueText, Value, false, ArcMotion.GetLevel(this));
        }
        _bubbleShown = show;
        if (_bubbleScale is not null)
        {
            _visuals.Double(_bubbleScale, ScaleTransform.ScaleXProperty, !show && full ? .8 : 1, show ? .26 : .12, moving && full, spring: show);
            _visuals.Double(_bubbleScale, ScaleTransform.ScaleYProperty, !show && full ? .8 : 1, show ? .26 : .12, moving && full, spring: show);
        }
        if (_bubbleMove is not null) _visuals.Double(_bubbleMove, TranslateTransform.YProperty, !show && full ? 4 : 0, show ? .26 : .12, moving && full, spring: show);
        if (_bubbleBlur is not null) _visuals.Double(_bubbleBlur, BlurEffect.RadiusProperty, !show && full ? 2 : 0, show ? .16 : .12, moving && full);
        var bubble = _bubble;
        _visuals.Double(bubble, OpacityProperty, show ? 1 : 0, show && full ? .16 : .12, moving,
            completed: () => { if (!_bubbleShown) bubble.Visibility = Visibility.Collapsed; });
        PositionParts(); EnsureRendering();
    }
    private void EnsureRendering()
    {
        if (_rendering || !IsLoaded || !IsVisible) return;
        if (!_position.IsRunning && !_lag.IsRunning && !(_readout?.IsRunning ?? false) && !(_bubbleNumber?.IsRunning ?? false) && !_visuals.IsRunning) return;
        _rendering = true; _clock.Restart(); _lastTime = 0; CompositionTarget.Rendering += RenderFrame;
    }
    private void RenderFrame(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds, dt = now - _lastTime; _lastTime = now;
        _position.Step(dt); _lag.Step(dt); _readout?.Step(dt); _bubbleNumber?.Step(dt); PositionParts();
        if (!_position.IsRunning && !_lag.IsRunning && !(_readout?.IsRunning ?? false) && !(_bubbleNumber?.IsRunning ?? false) && !_visuals.IsRunning) StopRendering();
    }
    private void StopRendering() { CompositionTarget.Rendering -= RenderFrame; _rendering = false; _clock.Stop(); }
    private void StopInteraction()
    {
        CancelGesture(); _linger.Stop(); _lingering = _keyFocus = _bubbleShown = false;
        SnapVisuals(); UpdateState(false);
        if (_bubble is not null) _bubble.Visibility = Visibility.Collapsed;
    }
    private void ReleaseParts()
    {
        StopInteraction(); _linger.Tick -= LingerElapsed;
        if (_surface is not null)
        {
            _surface.SizeChanged -= SurfaceSizeChanged; _surface.MouseMove -= SurfaceMouseMove;
            _surface.MouseLeftButtonUp -= SurfaceMouseUp; _surface.LostMouseCapture -= SurfaceLostCapture;
        }
        if (_bubble is not null) _bubble.SizeChanged -= BubbleSizeChanged;
        foreach (var mark in _markButtons) { mark.Button.Click -= MarkClicked; mark.Button.MouseEnter -= MarkHover; mark.Button.MouseLeave -= MarkHover; }
        _markButtons.Clear(); _readout?.Detach(); _bubbleNumber?.Detach(); _readout = _bubbleNumber = null;
        _surface = _thumbVisual = _focusRing = null; _bubble = null; _thumbCanvas = _bubbleCanvas = _marks = _ticks = _fillTicks = null;
        _fillClip = null; _clip = null; _thumbScale = _bubbleScale = null; _bubbleMove = null; _bubbleBlur = null; _shadow = null;
    }
    private void RebuildMarks()
    {
        if (_marks is null) return;
        foreach (var old in _markButtons) { old.Button.Click -= MarkClicked; old.Button.MouseEnter -= MarkHover; old.Button.MouseLeave -= MarkHover; }
        _markButtons.Clear(); _marks.Children.Clear(); _ticks?.Children.Clear(); _fillTicks?.Children.Clear();
        foreach (var mark in (Marks ?? []).Where(m => m is not null && double.IsFinite(m.Value) && m.Value >= Minimum && m.Value <= Maximum).DistinctBy(m => m.Value))
        {
            if (mark.Value > Minimum && mark.Value < Maximum)
            {
                foreach (var (canvas, key, opacity) in new[] { (_ticks, "ArcWpf.Brush.Foreground", .3), (_fillTicks, "ArcWpf.Brush.ControlGlyph", .6) })
                {
                    if (canvas is null) continue;
                    var tick = new Ellipse { Width = 4, Height = 4, Tag = mark, Opacity = opacity, IsHitTestVisible = false };
                    tick.SetResourceReference(Shape.FillProperty, key); canvas.Children.Add(tick); Canvas.SetTop(tick, 1);
                }
            }
            if (string.IsNullOrEmpty(mark.Label)) continue;
            var button = new SliderMarkButton { Content = mark.Label, Tag = mark, FontSize = 12, Focusable = false, IsTabStop = false, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            button.SetResourceReference(StyleProperty, "ArcWpf.Slider.MarkStyle");
            button.Click += MarkClicked; button.MouseEnter += MarkHover; button.MouseLeave += MarkHover;
            _marks.Children.Add(button); _markButtons.Add((mark, button));
        }
        _marks.Visibility = _markButtons.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshMarks(); LayoutMarks();
    }
    private void MarkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: SliderMark mark } || !IsEnabled) return;
        _pointerFocus = true; try { Focus(); } finally { _pointerFocus = false; }
        _keyFocus = false; ChangeFromInput(mark.Value);
    }
    private void MarkHover(object sender, MouseEventArgs e) => RefreshMarks();
    private void RefreshMarks()
    {
        foreach (var item in _markButtons)
        {
            var key = item.Button.IsMouseOver ? "ArcWpf.Brush.Foreground" : item.Mark.Value <= Value ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.TextMuted";
            item.Button.SetResourceReference(ForegroundProperty, key);
        }
        EnsureRendering();
    }
    private void LayoutMarks()
    {
        if (_surface is null) return;
        foreach (var canvas in new[] { _ticks, _fillTicks })
            if (canvas is not null) foreach (FrameworkElement tick in canvas.Children)
                if (tick.Tag is SliderMark mark) Canvas.SetLeft(tick, Travel * (Reversed ? 100 - Percent(mark.Value) : Percent(mark.Value)) / 100 - 2);
        foreach (var item in _markButtons)
        {
            item.Button.MaxWidth = Math.Max(0, _surface.ActualWidth);
            item.Button.Measure(new Size(Math.Max(0, _surface.ActualWidth), 20));
            double width = item.Button.DesiredSize.Width, at = 11 + Travel * (Reversed ? 100 - Percent(item.Mark.Value) : Percent(item.Mark.Value)) / 100;
            Canvas.SetLeft(item.Button, Math.Clamp(at - width / 2, 0, Math.Max(0, _surface.ActualWidth - width)));
        }
    }
}
