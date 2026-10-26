using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using NativeCalendar = System.Windows.Controls.Calendar;

namespace ArcWpf.Controls;

/// <summary>Template visual shared by Arc Calendar and DatePicker's native Calendar.</summary>
public sealed class CalendarSelectionIndicator : FrameworkElement
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(CalendarSelectionIndicator), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public static readonly DependencyProperty HasIndicatorProperty = DependencyProperty.RegisterAttached("HasIndicator", typeof(bool), typeof(CalendarSelectionIndicator), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetHasIndicator(DependencyObject d) => (bool)d.GetValue(HasIndicatorProperty);
    public static void SetHasIndicator(DependencyObject d, bool value) => d.SetValue(HasIndicatorProperty, value);
    public static readonly DependencyProperty IsCoveredProperty = DependencyProperty.RegisterAttached("IsCovered", typeof(bool), typeof(CalendarSelectionIndicator), new FrameworkPropertyMetadata(false));
    public static bool GetIsCovered(DependencyObject d) => (bool)d.GetValue(IsCoveredProperty);
    public static void SetIsCovered(DependencyObject d, bool value) => d.SetValue(IsCoveredProperty, value);

    private NativeCalendar? _calendar;
    private Grid? _month;
    private readonly ControlSpring _x = new(0, .42), _y = new(0, .42), _scale = new(.6, .26);
    private bool _visibleTarget, _initialized, _running;
    private double _size, _opacity, _fadeFrom, _fadeElapsed, _fadeDuration, _exitScaleFrom;
    private long _lastFrame;
    private DateTime? _displayMonth;
    private DispatcherOperation? _pending;
    internal bool IsAnimating => _running;
    internal Rect DiscBounds => new(_x.Position, _y.Position, _size, _size);
    internal double DiscOpacity => _opacity;
    internal Vector Velocity => new(_x.Velocity, _y.Velocity);

    public CalendarSelectionIndicator()
    {
        IsHitTestVisible = false;
        Focusable = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => { if (!IsVisible) { Sync(false); Stop(); } else QueueSync(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_calendar is not null) return;
        if (TemplatedParent is not CalendarItem item) return;
        _month = item.Template.FindName("PART_MonthView", item) as Grid;
        for (DependencyObject? ancestor = VisualTreeHelper.GetParent(this); ancestor is not null; ancestor = VisualTreeHelper.GetParent(ancestor))
            if (ancestor is NativeCalendar calendar) { _calendar = calendar; break; }
        if (_calendar is null || _month is null) return;
        _calendar.SelectedDatesChanged += SelectionChanged;
        _calendar.DisplayDateChanged += DisplayChanged;
        _calendar.DisplayModeChanged += ModeChanged;
        LayoutUpdated += LayoutChanged;
        Sync(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Stop();
        _pending?.Abort(); _pending = null;
        LayoutUpdated -= LayoutChanged;
        if (_calendar is not null)
        {
            _calendar.SelectedDatesChanged -= SelectionChanged;
            _calendar.DisplayDateChanged -= DisplayChanged;
            _calendar.DisplayModeChanged -= ModeChanged;
        }
        ResetCoverage();
        _month?.ClearValue(HasIndicatorProperty);
        _calendar = null; _month = null; _initialized = false;
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => QueueSync();
    private void DisplayChanged(object? sender, CalendarDateChangedEventArgs e) => QueueSync();
    private void ModeChanged(object? sender, CalendarModeChangedEventArgs e) => QueueSync();
    private void LayoutChanged(object? sender, EventArgs e) => Sync(true);
    private void QueueSync()
    {
        if (_pending?.Status == DispatcherOperationStatus.Pending) return;
        _pending = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => { _pending = null; Sync(true); }));
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _calendar is not null) Sync(false);
    }

    private void Sync(bool animate)
    {
        if (_calendar is null || _month is null) return;
        bool single = _calendar.SelectionMode == CalendarSelectionMode.SingleDate;
        SetHasIndicator(_month, single);
        var selected = single && _calendar.DisplayMode == CalendarMode.Month
            ? _month.Children.OfType<CalendarDayButton>().FirstOrDefault(d => d.DataContext is DateTime date && date == _calendar.SelectedDate && d.ActualWidth > 0)
            : null;
        bool visible = selected is not null;
        var month = new DateTime(_calendar.DisplayDate.Year, _calendar.DisplayDate.Month, 1);
        var point = selected?.TranslatePoint(new Point(), this) ?? new Point(_x.Target, _y.Target);
        double size = selected?.ActualWidth ?? _size;
        bool layoutChange = _displayMonth != month || Math.Abs(size - _size) > .1;
        bool changed = visible != _visibleTarget || point.X != _x.Target || point.Y != _y.Target || layoutChange;
        bool full = animate && _initialized && IsVisible && single && _calendar.DisplayMode == CalendarMode.Month && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        if (_initialized && !changed && (full || !_running)) return;
        bool entering = visible && (!_visibleTarget || _displayMonth != month);
        _displayMonth = month; _size = size;
        if (entering || layoutChange || !full) { _x.Snap(point.X); _y.Snap(point.Y); }
        else { _x.Target = point.X; _y.Target = point.Y; }
        if (changed)
        {
            if (entering) _scale.Snap(.6);
            _fadeFrom = _opacity; _fadeElapsed = 0; _fadeDuration = visible ? .16 : .14;
            _exitScaleFrom = _scale.Position;
        }
        _visibleTarget = visible;
        _scale.Target = visible ? 1 : .6;
        _initialized = true;
        if (!full)
        {
            Stop(); _scale.Snap(visible ? 1 : .6); _opacity = visible ? 1 : 0;
            UpdateCoverage(); InvalidateVisual(); return;
        }
        if (!_running && changed)
        {
            _running = true; _lastFrame = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += Frame;
        }
    }

    private void Frame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible) { Stop(); return; }
        long now = Stopwatch.GetTimestamp();
        double dt = Math.Min(Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds, .05);
        _lastFrame = now;
        bool xDone = _x.Step(dt), yDone = _y.Step(dt), scaleDone = _scale.Step(dt);
        _fadeElapsed += dt;
        double progress = Math.Clamp(_fadeElapsed / _fadeDuration, 0, 1);
        // Cubic ease-out is the WPF approximation for Arc's opacity easing; position retains spring velocity.
        double eased = 1 - Math.Pow(1 - progress, 3);
        if (!_visibleTarget) { _scale.Snap(_exitScaleFrom + (.6 - _exitScaleFrom) * eased); scaleDone = progress >= 1; }
        _opacity = _fadeFrom + ((_visibleTarget ? 1 : 0) - _fadeFrom) * eased;
        UpdateCoverage(); InvalidateVisual();
        if (xDone && yDone && scaleDone && progress >= 1) Stop();
    }

    private void Stop()
    {
        if (!_running) return;
        CompositionTarget.Rendering -= Frame; _running = false;
    }

    private void ResetCoverage()
    {
        if (_month is null) return;
        foreach (var day in _month.Children.OfType<CalendarDayButton>()) day.ClearValue(IsCoveredProperty);
    }

    private void UpdateCoverage()
    {
        if (_month is null) return;
        foreach (var day in _month.Children.OfType<CalendarDayButton>())
        {
            var point = day.TranslatePoint(new Point(day.ActualWidth / 2, day.ActualHeight / 2), this);
            var delta = point - new Point(_x.Position + _size / 2, _y.Position + _size / 2);
            // Unlike the web's timed text handoff, use the actual disc position to keep text legible on interruption.
            bool covered = _opacity > .5 && delta.Length < _size * _scale.Position * .4;
            if (GetIsCovered(day) != covered) SetIsCovered(day, covered);
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (_size <= 0 || _opacity <= 0 || Fill is null) return;
        context.PushOpacity(_opacity);
        context.DrawEllipse(Fill, null, new Point(_x.Position + _size / 2, _y.Position + _size / 2), _size * _scale.Position / 2, _size * _scale.Position / 2);
        context.Pop();
    }
}
