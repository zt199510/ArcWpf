using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum HoldToConfirmTone { Accent, Danger, Neutral }

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_FillClip", Type = typeof(Border))]
[TemplatePart(Name = "PART_Fill", Type = typeof(Border))]
[TemplatePart(Name = "PART_Label", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_FillLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Icon", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Check", Type = typeof(FrameworkElement))]
public class HoldToConfirm : System.Windows.Controls.Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(HoldToConfirm), new FrameworkPropertyMetadata("Hold to confirm", OnContentPropertyChanged));
    public static readonly DependencyProperty ConfirmedLabelProperty = DependencyProperty.Register(nameof(ConfirmedLabel), typeof(string), typeof(HoldToConfirm), new FrameworkPropertyMetadata("Done", OnContentPropertyChanged));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(HoldToConfirm), new FrameworkPropertyMetadata(1200d), v => v is double value && value > 0 && value <= 60000);
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(HoldToConfirmTone), typeof(HoldToConfirm), new FrameworkPropertyMetadata(HoldToConfirmTone.Accent, OnContentPropertyChanged));
    public static readonly DependencyProperty ConfirmedProperty = DependencyProperty.Register(nameof(Confirmed), typeof(bool?), typeof(HoldToConfirm), new FrameworkPropertyMetadata(null, OnConfirmedChanged));
    public static readonly DependencyProperty ConfirmCommandProperty = DependencyProperty.Register(nameof(ConfirmCommand), typeof(ICommand), typeof(HoldToConfirm));
    public static readonly DependencyProperty ConfirmCommandParameterProperty = DependencyProperty.Register(nameof(ConfirmCommandParameter), typeof(object), typeof(HoldToConfirm));
    private static readonly DependencyPropertyKey IsConfirmedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsConfirmed), typeof(bool), typeof(HoldToConfirm), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsConfirmedProperty = IsConfirmedPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsHoldingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsHolding), typeof(bool), typeof(HoldToConfirm), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsHoldingProperty = IsHoldingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Progress), typeof(double), typeof(HoldToConfirm), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ConfirmedEvent = EventManager.RegisterRoutedEvent(nameof(Confirmed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HoldToConfirm));

    private Border? _surface, _fillClip, _fill;
    private TextBlock? _label, _fillLabel;
    private FrameworkElement? _icon, _check;
    private ScaleTransform? _pressScale;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly OwnedMotion _motion = new();
    private HoldSource _source;
    private bool _captured;
    private double _holdStartProgress;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ConfirmedLabel { get => (string)GetValue(ConfirmedLabelProperty); set => SetValue(ConfirmedLabelProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public HoldToConfirmTone Tone { get => (HoldToConfirmTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool? Confirmed { get => (bool?)GetValue(ConfirmedProperty); set => SetValue(ConfirmedProperty, value); }
    public ICommand? ConfirmCommand { get => (ICommand?)GetValue(ConfirmCommandProperty); set => SetValue(ConfirmCommandProperty, value); }
    public object? ConfirmCommandParameter { get => GetValue(ConfirmCommandParameterProperty); set => SetValue(ConfirmCommandParameterProperty, value); }
    public bool IsConfirmed => (bool)GetValue(IsConfirmedProperty);
    public bool IsHolding => (bool)GetValue(IsHoldingProperty);
    public double Progress => (double)GetValue(ProgressProperty);
    public event RoutedEventHandler ConfirmedRaised { add => AddHandler(ConfirmedEvent, value); remove => RemoveHandler(ConfirmedEvent, value); }
    public event EventHandler<bool>? HoldChanged;

    private enum HoldSource { None, Pointer, Keyboard }
    static HoldToConfirm() { DefaultStyleKeyProperty.OverrideMetadata(typeof(HoldToConfirm), new FrameworkPropertyMetadata(typeof(HoldToConfirm))); }
    public HoldToConfirm() { _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) }; _timer.Tick += Tick; SetCurrentValue(FocusableProperty, true); Loaded += (_, _) => RefreshVisual(); Unloaded += (_, _) => Stop(true); }

    public override void OnApplyTemplate()
    {
        Stop(true); if (_surface is not null) _surface.SizeChanged -= SurfaceSizeChanged; base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; _fillClip = GetTemplateChild("PART_FillClip") as Border; _fill = GetTemplateChild("PART_Fill") as Border; _label = GetTemplateChild("PART_Label") as TextBlock; _fillLabel = GetTemplateChild("PART_FillLabel") as TextBlock; _icon = GetTemplateChild("PART_Icon") as FrameworkElement; _check = GetTemplateChild("PART_Check") as FrameworkElement;
        if (_surface is not null) _surface.SizeChanged += SurfaceSizeChanged;
        _pressScale = new ScaleTransform(1, 1); if (_surface is not null) { _surface.RenderTransform = _pressScale; _surface.RenderTransformOrigin = new Point(.5, .5); }
        RefreshVisual();
    }
    protected override void OnClick() { }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { e.Handled = true; if (Begin(HoldSource.Pointer)) _captured = CaptureMouse(); } else base.OnPreviewMouseLeftButtonDown(e); }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { e.Handled = true; if (_source != HoldSource.None) ReleaseHold(); else if (IsMouseCaptured) ReleaseMouseCapture(); } else base.OnPreviewMouseLeftButtonUp(e); }
    protected override void OnPreviewMouseMove(MouseEventArgs e) { base.OnPreviewMouseMove(e); if (_source != HoldSource.Pointer || e.MouseDevice.LeftButton != MouseButtonState.Pressed) return; var point = e.GetPosition(this); const double slack = 24; if (point.X < -slack || point.Y < -slack || point.X > ActualWidth + slack || point.Y > ActualHeight + slack) ReleaseHold(); }
    protected override void OnPreviewKeyDown(KeyEventArgs e) { base.OnPreviewKeyDown(e); if (e.Key is not (Key.Space or Key.Enter) || e.IsRepeat) return; e.Handled = true; Begin(HoldSource.Keyboard); }
    protected override void OnPreviewKeyUp(KeyEventArgs e) { base.OnPreviewKeyUp(e); if (e.Key is Key.Space or Key.Enter) { e.Handled = true; if (_source == HoldSource.Keyboard) ReleaseHold(); } }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); if (_source == HoldSource.Keyboard) ReleaseHold(); }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); _captured = false; if (_source == HoldSource.Pointer) { Stop(false); if (!IsConfirmed) Rewind(); } }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.Property == IsEnabledProperty) { if (!IsEnabled) Stop(true); RefreshVisual(); } }

    private static void OnContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((HoldToConfirm)d).RefreshVisual();
    private static void OnConfirmedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HoldToConfirm)d; if (e.NewValue is bool value) { control.Stop(true); control.SetValue(IsConfirmedPropertyKey, value); control.SetProgress(value ? 1 : 0, false); control.RefreshVisual(); }
    }
    private bool Begin(HoldSource source)
    {
        if (_source != HoldSource.None || IsConfirmed || !IsEnabled) return false;
        CaptureProgressFromVisual(); _motion.Stop(); _holdStartProgress = Progress; _source = source; _clock.Restart(); _timer.Start(); SetValue(IsHoldingPropertyKey, true); HoldChanged?.Invoke(this, true); PressTo(true);
        return true;
    }
    private void Tick(object? sender, EventArgs e) { if (_source == HoldSource.None) return; double value = Math.Clamp(_holdStartProgress + (_clock.Elapsed.TotalMilliseconds / Duration) * (1 - _holdStartProgress), 0, 1); SetProgress(value, false); if (value >= 1) Complete(); }
    private void ReleaseHold() { if (_source == HoldSource.None) return; Stop(false); if (!IsConfirmed) Rewind(); }
    private void Stop(bool resetProgress)
    {
        _timer.Stop(); _clock.Stop(); _source = HoldSource.None; if (IsHolding) { SetValue(IsHoldingPropertyKey, false); HoldChanged?.Invoke(this, false); } PressTo(false); if (_captured) { ReleaseMouseCapture(); _captured = false; } if (resetProgress) { _motion.Stop(); if (!IsConfirmed) SetProgress(0, false); }
    }
    private void Complete()
    {
        if (_source == HoldSource.None) return; Stop(false); _motion.Stop(); SetValue(IsConfirmedPropertyKey, true); SetProgress(1, false); RefreshVisual(); if (ConfirmCommand?.CanExecute(ConfirmCommandParameter) != false) ConfirmCommand?.Execute(ConfirmCommandParameter); RaiseEvent(new RoutedEventArgs(ConfirmedEvent, this));
    }
    private void Rewind() { if (_fillClip is null) { SetProgress(0, false); return; } _motion.Stop(); bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full; _motion.Double(_fillClip, FrameworkElement.WidthProperty, 0, .24, animate, spring: animate, completed: () => SetProgress(0, false)); if (!animate) SetProgress(0, false); }
    private void PressTo(bool pressed) { if (_pressScale is null) return; bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full; double target = pressed ? (ActualWidth > 220 ? .985 : .97) : 1; _motion.Double(_pressScale, ScaleTransform.ScaleXProperty, target, pressed ? .12 : .2, animate, spring: !pressed); _motion.Double(_pressScale, ScaleTransform.ScaleYProperty, target, pressed ? .12 : .2, animate, spring: !pressed); }
    private void SetProgress(double value, bool animate) { value = Math.Clamp(value, 0, 1); SetValue(ProgressPropertyKey, value); UpdateFillBounds(value); }
    private void RefreshVisual()
    {
        bool done = IsConfirmed; string text = done ? ConfirmedLabel : Label; string? previous = _label?.Text; if (_label is not null) _label.Text = text; if (_fillLabel is not null) _fillLabel.Text = text; if (_icon is not null) _icon.Visibility = done ? Visibility.Collapsed : Visibility.Visible; if (_check is not null) _check.Visibility = done ? Visibility.Visible : Visibility.Collapsed; if (_source == HoldSource.None) UpdateFillBounds(done ? 1 : Progress); if (previous is not null && previous != text && IsLoaded) AnimateText();
    }
    private void AnimateText()
    {
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        if (!full) { if (_label is not null) _label.Opacity = 1; if (_fillLabel is not null) _fillLabel.Opacity = 1; return; }
        foreach (var text in new[] { _label, _fillLabel }) if (text is not null) { text.Opacity = 0; var move = new TranslateTransform(0, 3); text.RenderTransform = move; _motion.Double(text, UIElement.OpacityProperty, 1, .16, true, enter: true); _motion.Double(move, TranslateTransform.YProperty, 0, .2, true, spring: true, enter: true); }
    }
    private void CaptureProgressFromVisual()
    {
        if (_fillClip is null || _surface is null) return;
        double width = _surface.ActualWidth + 2; if (width > 0) SetValue(ProgressPropertyKey, Math.Clamp(_fillClip.ActualWidth / width, 0, 1));
    }
    private void SurfaceSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateFillBounds(IsConfirmed ? 1 : Progress);
    private void UpdateFillBounds(double progress)
    {
        if (_surface is null || _fill is null || _fillClip is null) return;
        double width = Math.Max(0, _surface.ActualWidth + 2);
        _fill.Width = width; _fillClip.Width = width * Math.Clamp(progress, 0, 1);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new HoldToConfirmAutomationPeer(this);
    private sealed class HoldToConfirmAutomationPeer(HoldToConfirm owner) : ButtonAutomationPeer(owner) { protected override string GetNameCore() => owner.IsConfirmed ? owner.ConfirmedLabel : owner.Label; }
}
