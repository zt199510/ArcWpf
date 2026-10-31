using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum HoldToConfirmTone { Accent, Danger, Neutral }

[TemplatePart(Name = "PART_Root", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_FillClip", Type = typeof(Border))]
[TemplatePart(Name = "PART_Fill", Type = typeof(Border))]
[TemplatePart(Name = "PART_Label", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_FillLabel", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_LabelFrame", Type = typeof(Panel))]
[TemplatePart(Name = "PART_FillLabelFrame", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Icon", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_FillIcon", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Check", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_FillCheck", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_CheckPath", Type = typeof(Shape))]
[TemplatePart(Name = "PART_FillCheckPath", Type = typeof(Shape))]
public class HoldToConfirm : System.Windows.Controls.Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(HoldToConfirm), new FrameworkPropertyMetadata("Hold to confirm", OnTextChanged));
    public static readonly DependencyProperty ConfirmedLabelProperty = DependencyProperty.Register(nameof(ConfirmedLabel), typeof(string), typeof(HoldToConfirm), new FrameworkPropertyMetadata("Done", OnTextChanged));
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(HoldToConfirm), new FrameworkPropertyMetadata(1200d), v => v is double value && value > 0 && value <= 60000);
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(HoldToConfirmTone), typeof(HoldToConfirm), new FrameworkPropertyMetadata(HoldToConfirmTone.Accent, OnAppearanceChanged));
    public static readonly DependencyProperty ConfirmedProperty = DependencyProperty.Register(nameof(Confirmed), typeof(bool?), typeof(HoldToConfirm), new FrameworkPropertyMetadata(null, OnConfirmedChanged));
    public static readonly DependencyProperty ConfirmCommandProperty = DependencyProperty.Register(nameof(ConfirmCommand), typeof(ICommand), typeof(HoldToConfirm));
    public static readonly DependencyProperty ConfirmCommandParameterProperty = DependencyProperty.Register(nameof(ConfirmCommandParameter), typeof(object), typeof(HoldToConfirm));
    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(HoldToConfirm));
    // Theme inputs for the tone colours; the style feeds them with dynamic resources.
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(HoldToConfirm), new FrameworkPropertyMetadata(null, OnAppearanceChanged));
    public static readonly DependencyProperty OnFillBrushProperty = DependencyProperty.Register(nameof(OnFillBrush), typeof(Brush), typeof(HoldToConfirm));
    public static readonly DependencyProperty DangerBrushProperty = DependencyProperty.Register(nameof(DangerBrush), typeof(Brush), typeof(HoldToConfirm), new FrameworkPropertyMetadata(null, OnAppearanceChanged));
    private static readonly DependencyPropertyKey IsConfirmedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsConfirmed), typeof(bool), typeof(HoldToConfirm), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsConfirmedProperty = IsConfirmedPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsHoldingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsHolding), typeof(bool), typeof(HoldToConfirm), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsHoldingProperty = IsHoldingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Progress), typeof(double), typeof(HoldToConfirm), new FrameworkPropertyMetadata(0d));
    public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ConfirmedEvent = EventManager.RegisterRoutedEvent(nameof(Confirmed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HoldToConfirm));

    // motion-tokens.ts: duration.instant/fast/standard, blur.subtle/soft; springs settle at about 2.07 × visualDuration.
    private const double Instant = .12, Fast = .16, Standard = .24, SubtleBlur = 2, SoftBlur = 4, Rise = .3;
    private static double Spring(double visualDuration) => visualDuration * 2.07;
    private const double Snappy = .26, Smooth = .4, Morph = .42;
    // The check path is ~22.6 units long; dash lengths are in stroke widths (2).
    private const double CheckDash = 11.5;

    private FrameworkElement? _root, _icon, _fillIcon, _check, _fillCheck;
    private Border? _surface, _fillClip, _fill;
    private TextBlock? _label, _fillLabel;
    private Panel? _labelFrame, _fillLabelFrame;
    private Shape? _checkPath, _fillCheckPath;
    private readonly ScaleTransform _pressScale = new(1, 1);
    private readonly SolidColorBrush _surfaceBrush = new(), _borderBrush = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly OwnedMotion _fillMotion = new(), _pressMotion = new(), _colorMotion = new(), _faceMotion = new(), _widthMotion = new();
    private readonly List<UIElement> _leaving = [];
    private HoldSource _source;
    private bool _captured, _ownDone, _shownDone;
    private string? _shownText;
    private double _holdStartProgress;

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ConfirmedLabel { get => (string)GetValue(ConfirmedLabelProperty); set => SetValue(ConfirmedLabelProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public HoldToConfirmTone Tone { get => (HoldToConfirmTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool? Confirmed { get => (bool?)GetValue(ConfirmedProperty); set => SetValue(ConfirmedProperty, value); }
    public ICommand? ConfirmCommand { get => (ICommand?)GetValue(ConfirmCommandProperty); set => SetValue(ConfirmCommandProperty, value); }
    public object? ConfirmCommandParameter { get => GetValue(ConfirmCommandParameterProperty); set => SetValue(ConfirmCommandParameterProperty, value); }
    public Geometry? IconData { get => (Geometry?)GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
    public Brush? FillBrush { get => (Brush?)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush? OnFillBrush { get => (Brush?)GetValue(OnFillBrushProperty); set => SetValue(OnFillBrushProperty, value); }
    public Brush? DangerBrush { get => (Brush?)GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public bool IsConfirmed => (bool)GetValue(IsConfirmedProperty);
    public bool IsHolding => (bool)GetValue(IsHoldingProperty);
    public double Progress => (double)GetValue(ProgressProperty);
    public event RoutedEventHandler ConfirmedRaised { add => AddHandler(ConfirmedEvent, value); remove => RemoveHandler(ConfirmedEvent, value); }
    public event EventHandler<bool>? HoldChanged;

    private enum HoldSource { None, Pointer, Keyboard }
    private ArcMotionLevel Level => ArcMotion.GetLevel(this);
    private bool Full => Level == ArcMotionLevel.Full && IsLoaded;
    private bool Moving => Level != ArcMotionLevel.Off && IsLoaded;
    private string Text => IsConfirmed ? ConfirmedLabel : Label;

    static HoldToConfirm()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HoldToConfirm), new FrameworkPropertyMetadata(typeof(HoldToConfirm)));
    }
    public HoldToConfirm()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) }; _timer.Tick += Tick;
        SetCurrentValue(FocusableProperty, true);
        // role="status" announces the confirmed label; the button itself is the live region here.
        SetCurrentValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);
        Loaded += (_, _) => { SyncFace(false); UpdateAppearance(false); };
        Unloaded += (_, _) => { Stop(); _fillMotion.Stop(); _faceMotion.Stop(); _widthMotion.Stop(); FinishLeaving(); SetFill(IsConfirmed ? 1 : 0); };
    }

    public override void OnApplyTemplate()
    {
        Stop(); _fillMotion.Stop(); _faceMotion.Stop(); _widthMotion.Stop(); _pressMotion.Stop(); _leaving.Clear();
        if (_surface is not null) _surface.SizeChanged -= SurfaceSizeChanged;
        if (_labelFrame is not null) _labelFrame.SizeChanged -= LabelFrameSizeChanged;
        if (_fillLabelFrame is not null) _fillLabelFrame.SizeChanged -= LabelFrameSizeChanged;
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as FrameworkElement; _surface = GetTemplateChild("PART_Surface") as Border; _fillClip = GetTemplateChild("PART_FillClip") as Border; _fill = GetTemplateChild("PART_Fill") as Border;
        _label = GetTemplateChild("PART_Label") as TextBlock; _fillLabel = GetTemplateChild("PART_FillLabel") as TextBlock; _labelFrame = GetTemplateChild("PART_LabelFrame") as Panel; _fillLabelFrame = GetTemplateChild("PART_FillLabelFrame") as Panel;
        _icon = GetTemplateChild("PART_Icon") as FrameworkElement; _fillIcon = GetTemplateChild("PART_FillIcon") as FrameworkElement; _check = GetTemplateChild("PART_Check") as FrameworkElement; _fillCheck = GetTemplateChild("PART_FillCheck") as FrameworkElement;
        _checkPath = GetTemplateChild("PART_CheckPath") as Shape; _fillCheckPath = GetTemplateChild("PART_FillCheckPath") as Shape;
        _pressScale.ScaleX = _pressScale.ScaleY = 1;
        if (_root is not null) { _root.RenderTransform = _pressScale; _root.RenderTransformOrigin = new Point(.5, .5); }
        if (_surface is not null) { _surface.Background = _surfaceBrush; _surface.BorderBrush = _borderBrush; _surface.SizeChanged += SurfaceSizeChanged; }
        if (_labelFrame is not null) _labelFrame.SizeChanged += LabelFrameSizeChanged;
        if (_fillLabelFrame is not null) _fillLabelFrame.SizeChanged += LabelFrameSizeChanged;
        foreach (var icon in new[] { _icon, _fillIcon, _check, _fillCheck }) if (icon is not null) { icon.RenderTransform = new ScaleTransform(1, 1); icon.RenderTransformOrigin = new Point(.5, .5); }
        _shownText = null; SyncFace(false); UpdateAppearance(false); SetFill(IsConfirmed ? 1 : Progress);
    }

    protected override void OnClick() { }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { e.Handled = true; Focus(); if (Begin(HoldSource.Pointer)) _captured = CaptureMouse(); } else base.OnPreviewMouseLeftButtonDown(e); }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { e.Handled = true; if (_source != HoldSource.None) ReleaseHold(); else if (IsMouseCaptured) ReleaseMouseCapture(); } else base.OnPreviewMouseLeftButtonUp(e); }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_source != HoldSource.Pointer) return;
        // Sliding well off the button cancels, the way a native press does.
        var point = e.GetPosition(this); const double slack = 24;
        if (point.X < -slack || point.Y < -slack || point.X > ActualWidth + slack || point.Y > ActualHeight + slack) ReleaseHold();
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e) { base.OnPreviewKeyDown(e); if (e.Key is not (Key.Space or Key.Enter)) return; e.Handled = true; if (!e.IsRepeat) Begin(HoldSource.Keyboard); }
    protected override void OnPreviewKeyUp(KeyEventArgs e) { base.OnPreviewKeyUp(e); if (e.Key is Key.Space or Key.Enter) { e.Handled = true; if (_source == HoldSource.Keyboard) ReleaseHold(); } }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); ReleaseHold(); }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); _captured = false; if (_source == HoldSource.Pointer) ReleaseHold(); }
    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); UpdateAppearance(true); }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); UpdateAppearance(true); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsEnabledProperty) { if (!IsEnabled) ReleaseHold(); UpdateAppearance(true); }
        else if (e.Property == BackgroundProperty || e.Property == BorderBrushProperty) UpdateAppearance(false);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((HoldToConfirm)d).SyncFace(true);
    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((HoldToConfirm)d).UpdateAppearance(false);
    private static void OnConfirmedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((HoldToConfirm)d).SyncDone();

    /// <summary>Starts or resumes the fill from wherever it is, so a quick re-press after letting go carries on instead of starting over.</summary>
    private bool Begin(HoldSource source)
    {
        if (_source != HoldSource.None || IsConfirmed || !IsEnabled) return false;
        double current = CurrentFill(); _fillMotion.Stop(); SetFill(current);
        _holdStartProgress = current; _source = source; _clock.Restart(); _timer.Start();
        SetValue(IsHoldingPropertyKey, true); HoldChanged?.Invoke(this, true); PressTo(true);
        return true;
    }
    private void Tick(object? sender, EventArgs e)
    {
        if (_source == HoldSource.None) return;
        double remaining = (1 - _holdStartProgress) * Duration;
        double value = remaining <= 0 ? 1 : Math.Clamp(_holdStartProgress + _clock.Elapsed.TotalMilliseconds / remaining * (1 - _holdStartProgress), 0, 1);
        SetFill(value); if (value >= 1) Complete();
    }
    private void ReleaseHold() { if (_source == HoldSource.None) return; Stop(); Rewind(); }
    private void Stop()
    {
        _timer.Stop(); _clock.Stop(); _source = HoldSource.None;
        if (IsHolding) { SetValue(IsHoldingPropertyKey, false); HoldChanged?.Invoke(this, false); }
        PressTo(false);
        if (_captured) { _captured = false; ReleaseMouseCapture(); }
    }
    private void Complete()
    {
        if (_source == HoldSource.None) return;
        Stop(); _ownDone = true;
        if (ConfirmCommand?.CanExecute(ConfirmCommandParameter) == true) ConfirmCommand.Execute(ConfirmCommandParameter);
        RaiseEvent(new RoutedEventArgs(ConfirmedEvent, this));
        // A controlled button whose owner declined the confirmation rewinds.
        SyncDone();
        if (IsConfirmed) UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void SyncDone()
    {
        bool done = Confirmed ?? _ownDone;
        if (Confirmed == false) _ownDone = false;
        SetValue(IsConfirmedPropertyKey, done);
        if (_source != HoldSource.None && done) Stop();
        SyncFace(true); UpdateAppearance(true);
        // A reset rewinds the fill as the label morphs back; a confirmation set from outside fills on the same spring.
        if (_source != HoldSource.None) return;
        if (done && CurrentFill() < 1)
        {
            if (_fillClip is null || !Full) SetFill(1);
            else { _fillMotion.Stop(); SetValue(ProgressPropertyKey, 1d); _fillMotion.Double(_fillClip, FrameworkElement.WidthProperty, FullWidth, Spring(Smooth), true, spring: true); }
        }
        if (!done && CurrentFill() > 0) Rewind();
    }
    private void Rewind()
    {
        _fillMotion.Stop();
        if (_fillClip is null || !Full) { SetFill(0); return; }
        SetValue(ProgressPropertyKey, 0d);
        _fillMotion.Double(_fillClip, FrameworkElement.WidthProperty, 0, Spring(Smooth), true, spring: true);
    }
    private void PressTo(bool pressed)
    {
        double depth = pressed && Full ? (ActualWidth > 220 ? .985 : .97) : 1;
        _pressMotion.Double(_pressScale, ScaleTransform.ScaleXProperty, depth, Spring(Snappy), Full, spring: true);
        _pressMotion.Double(_pressScale, ScaleTransform.ScaleYProperty, depth, Spring(Snappy), Full, spring: true);
    }

    private double FullWidth => _surface is null ? 0 : Math.Max(0, _surface.ActualWidth + 2);
    private double CurrentFill() => _fillClip is null || FullWidth <= 0 ? Progress : Math.Clamp(_fillClip.Width / FullWidth, 0, 1);
    private void SetFill(double value)
    {
        value = Math.Clamp(value, 0, 1); SetValue(ProgressPropertyKey, value);
        if (_fillClip is null || _fill is null) return;
        _fillMotion.Stop(); _fill.Width = FullWidth; _fill.Height = _surface is null ? double.NaN : _surface.ActualHeight + 2; _fillClip.Width = FullWidth * value;
    }
    private void SurfaceSizeChanged(object? sender, SizeChangedEventArgs e) { if (!_fillMotion.IsRunning) SetFill(IsConfirmed && _source == HoldSource.None ? 1 : Progress); else if (_fill is not null) { _fill.Width = FullWidth; _fill.Height = e.NewSize.Height + 2; } }
    // labelFrame clip-path: inset(-.6em -3px) clips the outgoing label while the frame narrows, with room for the rise and the blur.
    private void LabelFrameSizeChanged(object? sender, SizeChangedEventArgs e) { if (sender is Panel frame) frame.Clip = new RectangleGeometry(new Rect(-3, -.6 * FontSize, e.NewSize.Width + 6, e.NewSize.Height + 1.2 * FontSize)); }

    private void UpdateAppearance(bool animate)
    {
        bool moving = animate && Moving;
        var surface = (Background as SolidColorBrush)?.Color ?? Colors.Transparent;
        var fill = (FillBrush as SolidColorBrush)?.Color ?? surface;
        var border = (BorderBrush as SolidColorBrush)?.Color ?? Colors.Transparent;
        // color-mix(in oklab, …) approximated in sRGB: hover is 6% of the fill over the surface; danger borders are 32% danger over --border.
        bool hover = IsMouseOver && IsEnabled && !IsConfirmed;
        if (Tone == HoldToConfirmTone.Danger && DangerBrush is SolidColorBrush danger && TryFindResource("ArcWpf.Brush.Border") is SolidColorBrush soft) border = Mix(danger.Color, soft.Color, .32);
        _colorMotion.Color(_surfaceBrush, hover ? Mix(fill, surface, .06) : surface, Fast, moving);
        _colorMotion.Color(_borderBrush, border, Fast, moving);
        if (_root is not null) _colorMotion.Double(_root, OpacityProperty, IsEnabled ? 1 : .5, Fast, moving);
    }
    private static Color Mix(Color a, Color b, double share) => Color.FromArgb((byte)Math.Round(a.A * share + b.A * (1 - share)), (byte)Math.Round(a.R * share + b.R * (1 - share)), (byte)Math.Round(a.G * share + b.G * (1 - share)), (byte)Math.Round(a.B * share + b.B * (1 - share)));

    /// <summary>Morphs the label and icon between the idle and done faces. Both faces change together so the text flips colour exactly at the fill edge.</summary>
    private void SyncFace(bool animate)
    {
        if (_label is null) return;
        string text = Text; bool done = IsConfirmed;
        if (_shownText == text && _shownDone == done) return;
        bool moving = animate && Moving && _shownText is not null, full = moving && Full;
        string? previous = _shownText; bool iconChanged = _shownDone != done || previous is null;
        _shownText = text; _shownDone = done;
        if (moving && previous != text) { MorphLabel(_labelFrame, _label, previous!, text, full); MorphLabel(_fillLabelFrame, _fillLabel, previous!, text, full); }
        else { FinishLeaving(); SetLabel(_label, text); SetLabel(_fillLabel, text); }
        if (iconChanged) { SwapIcon(done ? _icon : _check, done ? _check : _icon, done ? _checkPath : null, moving, full); SwapIcon(done ? _fillIcon : _fillCheck, done ? _fillCheck : _fillIcon, done ? _fillCheckPath : null, moving, full); }
        if (_labelFrame is not null && !moving) { _widthMotion.Stop(); _labelFrame.ClearValue(WidthProperty); _fillLabelFrame?.ClearValue(WidthProperty); }
    }
    private static void SetLabel(TextBlock? label, string text) { if (label is null) return; label.Text = text; label.Opacity = 1; label.RenderTransform = Transform.Identity; label.Effect = null; }
    private void MorphLabel(Panel? frame, TextBlock? label, string previous, string text, bool full)
    {
        if (frame is null || label is null) return;
        // The frame keeps its on-screen width, then springs to the new text, so the button morphs instead of snapping.
        double from = double.IsNaN(frame.Width) ? frame.ActualWidth : frame.Width;
        var leaving = new TextBlock { Text = previous, Style = label.Style, Foreground = label.Foreground, IsHitTestVisible = false };
        frame.Children.Add(leaving); _leaving.Add(leaving);
        label.Text = text; label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double to = label.DesiredSize.Width;
        if (full)
        {
            frame.Width = from;
            _widthMotion.Double(frame, WidthProperty, to, Spring(Morph), true, spring: true, completed: () => frame.ClearValue(WidthProperty));
        }
        else frame.Width = to;
        double rise = Rise * FontSize;
        var outMove = new TranslateTransform(); leaving.RenderTransform = outMove;
        _faceMotion.Double(leaving, OpacityProperty, 0, full ? Fast : Instant, true, completed: () => RemoveLeaving(frame, leaving, full));
        if (full)
        {
            _faceMotion.Double(outMove, TranslateTransform.YProperty, -rise, Fast, true);
            var outBlur = new BlurEffect { Radius = 0 }; leaving.Effect = outBlur; _faceMotion.Double(outBlur, BlurEffect.RadiusProperty, SubtleBlur, Fast, true);
        }
        label.Opacity = 0;
        _faceMotion.Double(label, OpacityProperty, 1, full ? Standard : Fast, true, enter: true);
        if (!full) { label.RenderTransform = Transform.Identity; label.Effect = null; return; }
        var inMove = new TranslateTransform(0, rise); label.RenderTransform = inMove;
        _faceMotion.Double(inMove, TranslateTransform.YProperty, 0, Standard, true, enter: true);
        var inBlur = new BlurEffect { Radius = SoftBlur }; label.Effect = inBlur;
        _faceMotion.Double(inBlur, BlurEffect.RadiusProperty, 0, Standard, true, enter: true, completed: () => { if (label.Effect == inBlur) label.Effect = null; });
    }
    private void RemoveLeaving(Panel frame, UIElement leaving, bool morphing)
    {
        frame.Children.Remove(leaving); _leaving.Remove(leaving);
        if (!morphing) frame.ClearValue(WidthProperty);
    }
    private void FinishLeaving()
    {
        foreach (var leaving in _leaving.ToArray()) if (VisualTreeHelper.GetParent(leaving) is Panel frame) frame.Children.Remove(leaving);
        _leaving.Clear();
    }
    private void SwapIcon(FrameworkElement? leaving, FrameworkElement? entering, Shape? draw, bool moving, bool full)
    {
        if (entering is null) return;
        // Scale rides the snappy spring; opacity and blur tween so the blur never overshoots below zero.
        if (leaving is not null)
        {
            if (moving) _faceMotion.Double(leaving, OpacityProperty, 0, full ? Fast : Instant, true, ease: StandardEase());
            else _faceMotion.Double(leaving, OpacityProperty, 0, 0, false);
            if (leaving.RenderTransform is ScaleTransform outScale) { _faceMotion.Double(outScale, ScaleTransform.ScaleXProperty, full ? .6 : 1, Fast, full); _faceMotion.Double(outScale, ScaleTransform.ScaleYProperty, full ? .6 : 1, Fast, full); }
            if (full) { var blur = new BlurEffect { Radius = 0 }; leaving.Effect = blur; _faceMotion.Double(blur, BlurEffect.RadiusProperty, SubtleBlur, Fast, true); } else leaving.Effect = null;
        }
        if (moving) entering.Opacity = 0;
        _faceMotion.Double(entering, OpacityProperty, 1, Fast, moving, enter: true);
        if (entering.RenderTransform is ScaleTransform inScale)
        {
            if (full) inScale.ScaleX = inScale.ScaleY = .6;
            _faceMotion.Double(inScale, ScaleTransform.ScaleXProperty, 1, Spring(Snappy), full, spring: true);
            _faceMotion.Double(inScale, ScaleTransform.ScaleYProperty, 1, Spring(Snappy), full, spring: true);
        }
        if (full) { var blur = new BlurEffect { Radius = SubtleBlur }; entering.Effect = blur; _faceMotion.Double(blur, BlurEffect.RadiusProperty, 0, Fast, true, enter: true, completed: () => { if (entering.Effect == blur) entering.Effect = null; }); }
        else entering.Effect = null;
        if (draw is null) return;
        // The tick draws itself from its short stroke, the way a hand would write it.
        draw.StrokeDashArray = new DoubleCollection([CheckDash, CheckDash]);
        if (full) { draw.Opacity = 0; _faceMotion.Double(draw, Shape.StrokeDashOffsetProperty, CheckDash, 0, false); _faceMotion.Double(draw, Shape.StrokeDashOffsetProperty, 0, .32, true, delay: .08, enter: true); _faceMotion.Double(draw, OpacityProperty, 1, .05, true, delay: .08); }
        else { _faceMotion.Double(draw, Shape.StrokeDashOffsetProperty, 0, 0, false); _faceMotion.Double(draw, OpacityProperty, 1, 0, false); }
    }
    private static KeySpline StandardEase() => new(.22, 1, .36, 1);

    protected override AutomationPeer OnCreateAutomationPeer() => new HoldToConfirmAutomationPeer(this);
    private sealed class HoldToConfirmAutomationPeer(HoldToConfirm owner) : ButtonAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Text;
        protected override string GetHelpTextCore() => owner.IsConfirmed ? string.Empty : $"Press and hold for {(owner.Duration / 1000).ToString("0.#", CultureInfo.InvariantCulture)} seconds to confirm. With a keyboard, hold Space or Enter.";
    }
}
