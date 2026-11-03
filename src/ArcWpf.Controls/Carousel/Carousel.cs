using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>Arc 风格的居中轮播：相邻卡片保持可见，支持拖拽、键盘、指示点和可选自动播放。</summary>
[TemplatePart(Name = "PART_Viewport", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Play", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Indicators", Type = typeof(CarouselIndicators))]
public class Carousel : ItemsControl
{
    public static readonly RoutedCommand PreviousCommand = new();
    public static readonly RoutedCommand NextCommand = new();
    public static readonly RoutedCommand TogglePlayCommand = new();
    public static readonly RoutedCommand SelectCommand = new();
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Carousel), new FrameworkPropertyMetadata("Carousel"));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(Carousel), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIndexChanged));
    public static readonly DependencyProperty SlideWidthProperty = DependencyProperty.Register(nameof(SlideWidth), typeof(double), typeof(Carousel), new FrameworkPropertyMetadata(340d, OnLayoutChanged, CoerceSize));
    public static readonly DependencyProperty SlideGapProperty = DependencyProperty.Register(nameof(SlideGap), typeof(double), typeof(Carousel), new FrameworkPropertyMetadata(16d, OnLayoutChanged, CoerceSize));
    public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(nameof(Interval), typeof(TimeSpan), typeof(Carousel), new FrameworkPropertyMetadata(TimeSpan.Zero, OnIntervalChanged));
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(Carousel), new FrameworkPropertyMetadata(false, OnPlayingChanged));
    private static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(Carousel), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double SlideWidth { get => (double)GetValue(SlideWidthProperty); set => SetValue(SlideWidthProperty, value); }
    public double SlideGap { get => (double)GetValue(SlideGapProperty); set => SetValue(SlideGapProperty, value); }
    public TimeSpan Interval { get => (TimeSpan)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool IsDragging => (bool)GetValue(IsDraggingProperty);
    public event EventHandler? SelectedIndexChanged;

    private ScrollViewer? _viewport; private FrameworkElement? _track; private CarouselIndicators? _indicators; private bool _dragging; private Point _dragStart; private double _origin; private DispatcherTimer? _timer; private bool _hover; private bool _focus; private double _lastPointerX; private int _lastPointerTime; private double _releaseVelocity;
    static Carousel() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(typeof(Carousel))); }
    public Carousel() { KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Local); CommandBindings.Add(new CommandBinding(PreviousCommand, (_, _) => Previous())); CommandBindings.Add(new CommandBinding(NextCommand, (_, _) => Next())); CommandBindings.Add(new CommandBinding(TogglePlayCommand, (_, _) => IsPlaying = !IsPlaying)); CommandBindings.Add(new CommandBinding(SelectCommand, (_, e) => { if (e.Parameter is int i) SelectedIndex = i; })); ItemContainerGenerator.StatusChanged += (_, _) => { if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) UpdateIndicators(); }; Loaded += (_, _) => { UpdatePosition(false); RestartTimer(); }; Unloaded += (_, _) => StopTimer(); }

    public override void OnApplyTemplate()
    {
        Release(); base.OnApplyTemplate();
        _viewport = GetTemplateChild("PART_Viewport") as ScrollViewer; _track = GetTemplateChild("PART_Track") as FrameworkElement; _indicators = GetTemplateChild("PART_Indicators") as CarouselIndicators; if (_indicators is not null) { _indicators.Owner = this; _indicators.ItemsSource = Items; _indicators.RefreshState(); }
        if (_viewport is not null) { _viewport.PreviewMouseLeftButtonDown += DragDown; _viewport.PreviewMouseMove += DragMove; _viewport.PreviewMouseLeftButtonUp += DragUp; _viewport.PreviewTouchDown += HandleTouchDown; _viewport.PreviewTouchMove += HandleTouchMove; _viewport.PreviewTouchUp += HandleTouchUp; _viewport.PreviewMouseWheel += Wheel; _viewport.MouseEnter += Hover; _viewport.MouseLeave += Hover; _viewport.GotKeyboardFocus += FocusChanged; _viewport.LostKeyboardFocus += FocusChanged; }
        UpdatePosition(false); Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateIndicators)); RestartTimer();
    }
    private void Release() { if (_viewport is null) return; _viewport.PreviewMouseLeftButtonDown -= DragDown; _viewport.PreviewMouseMove -= DragMove; _viewport.PreviewMouseLeftButtonUp -= DragUp; _viewport.PreviewTouchDown -= HandleTouchDown; _viewport.PreviewTouchMove -= HandleTouchMove; _viewport.PreviewTouchUp -= HandleTouchUp; _viewport.PreviewMouseWheel -= Wheel; _viewport.MouseEnter -= Hover; _viewport.MouseLeave -= Hover; _viewport.GotKeyboardFocus -= FocusChanged; _viewport.LostKeyboardFocus -= FocusChanged; _viewport = null; _track = null; _indicators = null; StopTimer(); }
    private static object CoerceSize(DependencyObject _, object value) => Math.Max(1, (double)value);
    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((Carousel)d).UpdatePosition(false);
    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c = (Carousel)d; c.CoerceIndex(); c.UpdatePosition(true); c.SelectedIndexChanged?.Invoke(c, EventArgs.Empty); c.RestartTimer(); }
    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((Carousel)d).RestartTimer();
    private static void OnPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((Carousel)d).RestartTimer();
    private void CoerceIndex() { int last = Math.Max(0, Items.Count - 1); if (SelectedIndex < 0) SetCurrentValue(SelectedIndexProperty, 0); else if (SelectedIndex > last) SetCurrentValue(SelectedIndexProperty, last); }
    private void UpdatePosition(bool animate)
    {
        if (_track is null || _viewport is null || Items.Count == 0) return;
        CoerceIndex(); double step = SlideWidth + SlideGap; double center = Math.Max(0, (_viewport.ActualWidth - SlideWidth) / 2); double x = center - SelectedIndex * step;
        if (_track.RenderTransform is not TranslateTransform t) _track.RenderTransform = t = new TranslateTransform();
        var level = ArcMotion.GetLevel(this);
        if (animate && IsLoaded && level == ArcMotionLevel.Full)
        {
            // Arc 的 snap 是可打断 spring；Tween.Spring 会从当前动画帧继续，避免快速连续翻页时跳回旧位置。
            Tween.Spring(t, TranslateTransform.XProperty, x, Tween.Morph);
        }
        else if (animate && IsLoaded && level == ArcMotionLevel.Reduced)
        {
            Tween.To(t, TranslateTransform.XProperty, x, Tween.Fast, ArcEasing.Standard);
        }
        else { t.BeginAnimation(TranslateTransform.XProperty, null); t.X = x; }
        AutomationProperties.SetName(_viewport, $"{Label}, {SelectedIndex + 1} of {Items.Count}");
        UpdateIndicators();
        UpdateSlideVisuals(t.X, step);
    }
    private void UpdateIndicators()
    {
        if (_indicators is null) return;
        for (var i = 0; i < Items.Count; i++) if (_indicators.ItemContainerGenerator.ContainerFromIndex(i) is CarouselIndicator indicator) indicator.IsActive = i == SelectedIndex;
    }
    private void UpdateSlideVisuals(double trackX, double step)
    {
        if (step <= 0) return;
        for (int i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not CarouselItem item) continue;
            var distance = Math.Abs((trackX + i * step) / step);
            var emphasis = Math.Max(0, 1 - Math.Min(1, distance));
            item.IsActive = i == SelectedIndex;
            item.Width = SlideWidth;
            item.Margin = new Thickness(SlideGap / 2, 0, SlideGap / 2, 0);
            item.Opacity = 0.5 + 0.5 * emphasis;
            item.RenderTransform = new ScaleTransform(0.9 + 0.1 * emphasis, 0.9 + 0.1 * emphasis);
        }
    }
    private void RestartTimer() { StopTimer(); if (!IsPlaying || _hover || _focus || IsDragging || Interval <= TimeSpan.Zero || Items.Count < 2) return; _timer = new DispatcherTimer { Interval = Interval }; _timer.Tick += (_, _) => Next(); _timer.Start(); }
    private void StopTimer() { if (_timer is not null) { _timer.Stop(); _timer = null; } }
    private void Hover(object? s, MouseEventArgs e) { _hover = _viewport?.IsMouseOver == true; RestartTimer(); }
    private void FocusChanged(object? s, KeyboardFocusChangedEventArgs e) { _focus = _viewport?.IsKeyboardFocusWithin == true; RestartTimer(); }
    private void DragDown(object sender, MouseButtonEventArgs e) { if (Items.Count < 2 || e.ChangedButton != MouseButton.Left) return; _dragging = true; SetValue(IsDraggingPropertyKey, true); _dragStart = e.GetPosition(_viewport); _origin = (_track?.RenderTransform as TranslateTransform)?.X ?? 0; _lastPointerX = _dragStart.X; _lastPointerTime = e.Timestamp; _releaseVelocity = 0; _viewport?.CaptureMouse(); StopTimer(); e.Handled = true; }
    private void DragMove(object sender, MouseEventArgs e) { if (!_dragging || _viewport is null || _track is null) return; var p = e.GetPosition(_viewport); UpdatePointer(p.X, e.Timestamp); if (_track.RenderTransform is not TranslateTransform t) _track.RenderTransform = t = new TranslateTransform(); t.X = _origin + p.X - _dragStart.X; UpdateSlideVisuals(t.X, SlideWidth + SlideGap); }
    private void DragUp(object sender, MouseButtonEventArgs e) { if (!_dragging) return; var p = e.GetPosition(_viewport); UpdatePointer(p.X, e.Timestamp); FinishDrag(p.X - _dragStart.X, _releaseVelocity); _viewport?.ReleaseMouseCapture(); e.Handled = true; }
    private void HandleTouchDown(object? sender, TouchEventArgs e) { if (Items.Count < 2 || _dragging || _viewport is null) return; _dragging = true; SetValue(IsDraggingPropertyKey, true); _dragStart = e.GetTouchPoint(_viewport).Position; _origin = (_track?.RenderTransform as TranslateTransform)?.X ?? 0; _lastPointerX = _dragStart.X; _lastPointerTime = e.Timestamp; _releaseVelocity = 0; _viewport.CaptureTouch(e.TouchDevice); StopTimer(); e.Handled = true; }
    private void HandleTouchMove(object? sender, TouchEventArgs e) { if (!_dragging || _viewport is null || _track is null) return; var p = e.GetTouchPoint(_viewport).Position; UpdatePointer(p.X, e.Timestamp); if (_track.RenderTransform is not TranslateTransform t) _track.RenderTransform = t = new TranslateTransform(); t.X = _origin + p.X - _dragStart.X; UpdateSlideVisuals(t.X, SlideWidth + SlideGap); e.Handled = true; }
    private void HandleTouchUp(object? sender, TouchEventArgs e) { if (!_dragging || _viewport is null) return; var p = e.GetTouchPoint(_viewport).Position; UpdatePointer(p.X, e.Timestamp); FinishDrag(p.X - _dragStart.X, _releaseVelocity); _viewport.ReleaseTouchCapture(e.TouchDevice); e.Handled = true; }
    private void UpdatePointer(double x, int timestamp) { var elapsed = Math.Max(1, timestamp - _lastPointerTime); _releaseVelocity = (x - _lastPointerX) / (elapsed / 1000d); _lastPointerX = x; _lastPointerTime = timestamp; }
    private void FinishDrag(double delta, double velocity)
    {
        _dragging = false; SetValue(IsDraggingPropertyKey, false);
        const double flick = 560;
        // Project a short part of the release velocity, matching the web track's flick behavior.
        var projected = delta + Math.Clamp(velocity * .10, -SlideWidth, SlideWidth);
        if (Math.Abs(velocity) >= flick) { if (velocity < 0) Next(); else Previous(); }
        else if (Math.Abs(projected) > Math.Max(24, (SlideWidth + SlideGap) * .18)) { if (projected < 0) Next(); else Previous(); }
        else UpdatePosition(true);
        RestartTimer();
    }
    private void Wheel(object? sender, MouseWheelEventArgs e) { if (Items.Count < 2) return; if (Math.Abs(e.Delta) < 1) return; if (e.Delta < 0) Next(); else Previous(); e.Handled = true; }
    public void Next() { if (Items.Count > 0) SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1); }
    public void Previous() { if (Items.Count > 0) SelectedIndex = Math.Max(0, SelectedIndex - 1); }
    public void First() => SelectedIndex = 0;
    public void Last() => SelectedIndex = Math.Max(0, Items.Count - 1);
    protected override void OnPreviewKeyDown(KeyEventArgs e) { base.OnPreviewKeyDown(e); if (e.Handled) return; switch (e.Key) { case Key.Left: Previous(); e.Handled = true; break; case Key.Right: Next(); e.Handled = true; break; case Key.Home: First(); e.Handled = true; break; case Key.End: Last(); e.Handled = true; break; case Key.Space when Interval > TimeSpan.Zero: IsPlaying = !IsPlaying; e.Handled = true; break; } }
    protected override DependencyObject GetContainerForItemOverride() => new CarouselItem();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is CarouselItem;
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item) { base.PrepareContainerForItemOverride(element, item); if (element is CarouselItem c) { c.Index = ItemContainerGenerator.IndexFromContainer(c); c.IsActive = c.Index == SelectedIndex; } }
}

public sealed class CarouselIndicators : ItemsControl
{
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(Carousel), typeof(CarouselIndicators), new FrameworkPropertyMetadata(null, OnOwnerChanged));
    public Carousel? Owner { get => (Carousel?)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    public CarouselIndicators() => ItemContainerGenerator.StatusChanged += (_, _) => { if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) RefreshState(); };
    private static void OnOwnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((CarouselIndicators)d).RefreshState();
    internal void RefreshState() { for (var i = 0; i < Items.Count; i++) if (ItemContainerGenerator.ContainerFromIndex(i) is CarouselIndicator indicator) { indicator.Owner = Owner; indicator.IsActive = i == Owner?.SelectedIndex; } }
    protected override DependencyObject GetContainerForItemOverride() => new CarouselIndicator();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is CarouselIndicator;
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item) { base.PrepareContainerForItemOverride(element, item); if (element is CarouselIndicator indicator) { indicator.Index = ItemContainerGenerator.IndexFromContainer(indicator); indicator.Owner = Owner; indicator.IsActive = indicator.Index == Owner?.SelectedIndex; } }
}

public sealed class CarouselIndicator : Button
{
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(nameof(Index), typeof(int), typeof(CarouselIndicator));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(CarouselIndicator));
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(Carousel), typeof(CarouselIndicator));
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public Carousel? Owner { get => (Carousel?)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    protected override void OnClick() { base.OnClick(); if (Owner is not null) Owner.SelectedIndex = Index; }
}

public sealed class CarouselItem : ContentControl
{
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(nameof(Index), typeof(int), typeof(CarouselItem), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(CarouselItem), new FrameworkPropertyMetadata(false));
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    static CarouselItem() { DefaultStyleKeyProperty.OverrideMetadata(typeof(CarouselItem), new FrameworkPropertyMetadata(typeof(CarouselItem))); }
}
