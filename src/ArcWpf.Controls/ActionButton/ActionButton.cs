using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ActionButtonFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception) : RoutedEventArgs(routedEvent, source)
{
    public Exception Exception { get; } = exception;
}

[TemplatePart(Name = "PART_Morph", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Text", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_PreviousText", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_ArrowPhase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_SpinnerPhase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_CheckPhase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Check", Type = typeof(Shape))]
public class ActionButton : Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Save", OnLabelChanged));
    public static readonly DependencyProperty PendingLabelProperty = DependencyProperty.Register(nameof(PendingLabel), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Saving", OnStateLabelChanged));
    public static readonly DependencyProperty SuccessLabelProperty = DependencyProperty.Register(nameof(SuccessLabel), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Saved", OnStateLabelChanged));
    public static readonly DependencyProperty ResetAfterMsProperty = DependencyProperty.Register(nameof(ResetAfterMs), typeof(int), typeof(ActionButton), new FrameworkPropertyMetadata(2400, null, CoerceResetAfterMs));
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(Func<Task>), typeof(ActionButton), new PropertyMetadata(null));
    private static readonly DependencyPropertyKey IsPendingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPending), typeof(bool), typeof(ActionButton), new FrameworkPropertyMetadata(false));
    private static readonly DependencyPropertyKey IsSuccessPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSuccess), typeof(bool), typeof(ActionButton), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPendingProperty = IsPendingPropertyKey.DependencyProperty;
    public static readonly DependencyProperty IsSuccessProperty = IsSuccessPropertyKey.DependencyProperty;
    public static readonly RoutedEvent ActionFailedEvent = EventManager.RegisterRoutedEvent(nameof(ActionFailed), RoutingStrategy.Bubble, typeof(EventHandler<ActionButtonFailedEventArgs>), typeof(ActionButton));

    // Arc motion tokens: duration.instant/fast/standard, ease.enter/standard, spring.snappy, blur.subtle.
    private const double Instant = .12, Fast = .16, Standard = .24, SubtleBlur = 2;
    private const double LabelOut = .1;
    private static readonly IEasingFunction EnterEase = ArcEasing.Enter;
    private static readonly IEasingFunction StandardEase = ArcEasing.Standard;
    private const double SpringSeconds = .6;
    private static readonly IEasingFunction SnappySpring = ArcEasing.Spring(.26, SpringSeconds);
    private static readonly IEasingFunction WidthSpring = ArcEasing.Spring(.24, SpringSeconds);

    private enum Phase { Arrow, Spinner, Check }

    private DispatcherTimer? _resetTimer;
    private DispatcherTimer? _spinnerExit;
    private int _runId;
    private int _morphGeneration;
    private Phase _phase = Phase.Arrow;
    private FrameworkElement? _morph;
    private TextBlock? _text;
    private TextBlock? _previousText;
    private FrameworkElement? _spinner;
    private Shape? _check;
    private readonly TranslateTransform _textOffset = new();
    private readonly RotateTransform _spin = new();
    private readonly Dictionary<Phase, PhaseVisual> _phases = new();

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string PendingLabel { get => (string)GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }
    public string SuccessLabel { get => (string)GetValue(SuccessLabelProperty); set => SetValue(SuccessLabelProperty, value); }
    public int ResetAfterMs { get => (int)GetValue(ResetAfterMsProperty); set => SetValue(ResetAfterMsProperty, value); }
    public Func<Task>? Action { get => (Func<Task>?)GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    public bool IsPending { get => (bool)GetValue(IsPendingProperty); private set => SetValue(IsPendingPropertyKey, value); }
    public bool IsSuccess { get => (bool)GetValue(IsSuccessProperty); private set => SetValue(IsSuccessPropertyKey, value); }
    public event EventHandler<ActionButtonFailedEventArgs> ActionFailed { add => AddHandler(ActionFailedEvent, value); remove => RemoveHandler(ActionFailedEvent, value); }

    private protected override bool IsBusy => base.IsBusy || IsPending;
    private bool MotionActive => IsLoaded && IsVisible;
    private ArcMotionLevel Level => MotionActive ? ArcMotion.GetLevel(this) : ArcMotionLevel.Off;
    private Phase TargetPhase => IsPending ? Phase.Spinner : IsSuccess ? Phase.Check : Phase.Arrow;
    private string CurrentText => IsPending ? PendingLabel : IsSuccess ? SuccessLabel : Label;

    static ActionButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ActionButton), new FrameworkPropertyMetadata(typeof(ActionButton)));

    public ActionButton()
    {
        SetCurrentValue(ContentProperty, Label);
        AutomationProperties.SetName(this, Label);
        Loaded += (_, _) => UpdateSpinner();
        Unloaded += (_, _) => { CancelRun(); UpdateSpinner(); };
        IsVisibleChanged += (_, _) => UpdateSpinner();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _morph = GetTemplateChild("PART_Morph") as FrameworkElement;
        _text = GetTemplateChild("PART_Text") as TextBlock;
        _previousText = GetTemplateChild("PART_PreviousText") as TextBlock;
        _spinner = GetTemplateChild("PART_Spinner") as FrameworkElement;
        _check = GetTemplateChild("PART_Check") as Shape;
        if (_text is not null) { _text.RenderTransform = _textOffset; _text.Text = Content as string; }
        if (_spinner is not null) { _spinner.RenderTransformOrigin = new Point(.5, .5); _spinner.RenderTransform = _spin; }
        _phases.Clear();
        AddPhase(Phase.Arrow, "PART_ArrowPhase");
        AddPhase(Phase.Spinner, "PART_SpinnerPhase");
        AddPhase(Phase.Check, "PART_CheckPhase");
        _phase = TargetPhase;
        foreach (var (phase, visual) in _phases) { visual.Snap(phase == _phase); }
        SnapCheck(_phase == Phase.Check);
        UpdateSpinner();
    }

    private void AddPhase(Phase phase, string name)
    {
        if (GetTemplateChild(name) is FrameworkElement element) { _phases[phase] = new PhaseVisual(element); }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPendingProperty || e.Property == IsSuccessProperty)
        {
            SetCurrentValue(ContentProperty, CurrentText);
            UpdatePhase();
            if (e.Property == IsPendingProperty) { RefreshMotion(); }
        }
        else if (e.Property == ArcMotion.LevelProperty)
        {
            UpdateSpinner();
        }
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        MorphText(oldContent as string, newContent as string);
    }

    private static object CoerceResetAfterMs(DependencyObject _, object value) => Math.Max(0, (int)value);

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ActionButton)d;
        // The visible label is decorative; the accessible name always stays on the idle label.
        AutomationProperties.SetName(button, (string)e.NewValue);
        OnStateLabelChanged(d, e);
    }

    private static void OnStateLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ActionButton)d;
        button.SetCurrentValue(ContentProperty, button.CurrentText);
    }

    protected override void OnClick()
    {
        // Pending blocks repeats without disabling, so keyboard focus stays through the save.
        // Success stays pressable: pressing again starts a new run, as in the source.
        if (IsPending || !IsEnabled) { return; }
        base.OnClick();
        if (Action is { } action) { _ = RunActionAsync(action); }
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        var run = ++_runId;
        StopResetTimer();
        // Pending wins over success in CurrentText/TargetPhase, so this order morphs straight from the shown state.
        IsPending = true;
        IsSuccess = false;
        try
        {
            await action().ConfigureAwait(true);
            if (run != _runId) { return; }
            IsSuccess = true;
            IsPending = false;
            if (ResetAfterMs > 0)
            {
                _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ResetAfterMs) };
                _resetTimer.Tick += ResetTimerTick;
                _resetTimer.Start();
            }
        }
        catch (Exception exception)
        {
            if (run != _runId) { return; }
            IsPending = false;
            RaiseEvent(new ActionButtonFailedEventArgs(ActionFailedEvent, this, exception));
        }
    }

    private void ResetTimerTick(object? sender, EventArgs e)
    {
        StopResetTimer();
        IsSuccess = false;
    }

    private void StopResetTimer()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    private void CancelRun()
    {
        ++_runId;
        StopResetTimer();
        IsPending = false;
        IsSuccess = false;
    }

    // Old label fades out fast; the new one rises 2px into place while the width springs without overshoot.
    private void MorphText(string? oldText, string? newText)
    {
        if (_text is null) { return; }
        var level = Level;
        double oldWidth = _morph?.ActualWidth ?? 0;
        int generation = ++_morphGeneration;
        _text.Text = newText;
        if (level == ArcMotionLevel.Off || oldText == newText)
        {
            SnapText();
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // Reduced motion: no width spring and no rise, the label swaps in place.
            SnapText();
            return;
        }
        if (_previousText is not null)
        {
            _previousText.Text = oldText;
            Tween(_previousText, OpacityProperty, LabelOut, StandardEase, from: 1, to: 0);
        }
        Tween(_text, OpacityProperty, Fast, EnterEase, from: 0, to: 1);
        Tween(_textOffset, TranslateTransform.YProperty, Fast, EnterEase, from: 2, to: 0);
        if (_morph is null || oldWidth <= 0) { return; }
        _text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double targetWidth = _text.DesiredSize.Width;
        if (Math.Abs(targetWidth - oldWidth) < .5) { return; }
        var morph = _morph;
        morph.ClipToBounds = true;
        var width = new DoubleAnimation(oldWidth, targetWidth, TimeSpan.FromSeconds(SpringSeconds)) { EasingFunction = WidthSpring };
        width.Completed += (_, _) =>
        {
            if (generation != _morphGeneration) { return; }
            morph.BeginAnimation(WidthProperty, null);
            morph.ClearValue(WidthProperty);
            morph.ClipToBounds = false;
        };
        morph.BeginAnimation(WidthProperty, width);
    }

    private void SnapText()
    {
        if (_morph is not null) { _morph.BeginAnimation(WidthProperty, null); _morph.ClearValue(WidthProperty); _morph.ClipToBounds = false; }
        if (_previousText is not null) { Snap(_previousText, OpacityProperty, 0); _previousText.Text = null; }
        if (_text is not null) { Snap(_text, OpacityProperty, 1); }
        Snap(_textOffset, TranslateTransform.YProperty, 0);
    }

    // One icon at a time: the arrow leaves forward and returns from behind; spinner and check pop from .6 with a soft blur.
    private void UpdatePhase()
    {
        var next = TargetPhase;
        if (next == _phase) { return; }
        var previous = _phase;
        _phase = next;
        var level = Level;
        if (_phases.TryGetValue(previous, out var outgoing))
        {
            if (level == ArcMotionLevel.Off) { outgoing.Snap(false); }
            else if (level == ArcMotionLevel.Reduced) { Tween(outgoing.Element, OpacityProperty, Instant, null, to: 0); }
            else
            {
                Tween(outgoing.Element, OpacityProperty, Fast, StandardEase, to: 0);
                Tween(outgoing.Blur, BlurEffect.RadiusProperty, Fast, StandardEase, to: SubtleBlur);
                if (previous == Phase.Arrow) { Tween(outgoing.Offset, TranslateTransform.XProperty, Fast, StandardEase, to: 8); }
                else { outgoing.ScaleTo(.6, Fast, StandardEase); }
            }
        }
        if (_phases.TryGetValue(next, out var incoming))
        {
            if (level == ArcMotionLevel.Off) { incoming.Snap(true); }
            else if (level == ArcMotionLevel.Reduced)
            {
                incoming.Snap(true);
                Tween(incoming.Element, OpacityProperty, Instant, null, from: 0, to: 1);
            }
            else
            {
                incoming.Snap(true);
                Tween(incoming.Element, OpacityProperty, Fast, EnterEase, from: 0, to: 1);
                Tween(incoming.Blur, BlurEffect.RadiusProperty, Fast, EnterEase, from: SubtleBlur, to: 0);
                if (next == Phase.Arrow) { Tween(incoming.Offset, TranslateTransform.XProperty, SpringSeconds, SnappySpring, from: -6, to: 0); }
                else
                {
                    incoming.Scale.ScaleX = incoming.Scale.ScaleY = .6;
                    incoming.ScaleTo(1, SpringSeconds, SnappySpring);
                }
            }
        }
        DrawCheck(next == Phase.Check && level == ArcMotionLevel.Full);
        if (next != Phase.Check || level != ArcMotionLevel.Full) { SnapCheck(next == Phase.Check); }
        if (previous == Phase.Spinner && level != ArcMotionLevel.Off)
        {
            // Keep turning while the ring fades out instead of snapping back to 0 degrees.
            _spinnerExit?.Stop();
            _spinnerExit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Fast) };
            _spinnerExit.Tick += (_, _) => { _spinnerExit?.Stop(); _spinnerExit = null; UpdateSpinner(); };
            _spinnerExit.Start();
        }
        UpdateSpinner();
    }

    // The tick draws itself from its short stroke; dash units are stroke-thickness multiples covering the whole path.
    private void DrawCheck(bool animate)
    {
        if (_check is null || !animate) { return; }
        Tween(_check, Shape.StrokeDashOffsetProperty, Standard, EnterEase, from: CheckDash, to: 0, delay: .05);
        Tween(_check, OpacityProperty, .05, null, from: 0, to: 1, delay: .05);
    }

    private const double CheckDash = 12;

    private void SnapCheck(bool visible)
    {
        if (_check is null) { return; }
        Snap(_check, Shape.StrokeDashOffsetProperty, visible ? 0 : CheckDash);
        Snap(_check, OpacityProperty, visible ? 1 : 0);
    }

    private void UpdateSpinner()
    {
        bool spin = (IsPending || _spinnerExit is not null) && MotionActive && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
        if (spin == (_spin.HasAnimatedProperties)) { return; }
        _spin.BeginAnimation(RotateTransform.AngleProperty, spin ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.7)) { RepeatBehavior = RepeatBehavior.Forever } : null);
        if (!spin) { _spin.Angle = 0; }
    }

    private static void Tween(IAnimatable target, DependencyProperty property, double seconds, IEasingFunction? ease, double to, double? from = null, double delay = 0)
    {
        var animation = new DoubleAnimation { To = to, Duration = TimeSpan.FromSeconds(seconds), BeginTime = TimeSpan.FromSeconds(delay), EasingFunction = ease };
        if (from is double start)
        {
            animation.From = start;
            // Hold the start value through any delay.
            ((DependencyObject)target).SetValue(property, start);
        }
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Snap(IAnimatable target, DependencyProperty property, double value)
    {
        target.BeginAnimation(property, null);
        ((DependencyObject)target).SetValue(property, value);
    }

    private sealed class PhaseVisual
    {
        public FrameworkElement Element { get; }
        public ScaleTransform Scale { get; } = new(1, 1);
        public TranslateTransform Offset { get; } = new();
        public BlurEffect Blur { get; } = new() { Radius = 0 };

        public PhaseVisual(FrameworkElement element)
        {
            Element = element;
            var group = new TransformGroup();
            group.Children.Add(Scale);
            group.Children.Add(Offset);
            element.RenderTransformOrigin = new Point(.5, .5);
            element.RenderTransform = group;
            element.Effect = Blur;
        }

        public void ScaleTo(double value, double seconds, IEasingFunction ease)
        {
            Tween(Scale, ScaleTransform.ScaleXProperty, seconds, ease, to: value);
            Tween(Scale, ScaleTransform.ScaleYProperty, seconds, ease, to: value);
        }

        public void Snap(bool visible)
        {
            ActionButton.Snap(Element, OpacityProperty, visible ? 1 : 0);
            ActionButton.Snap(Scale, ScaleTransform.ScaleXProperty, 1);
            ActionButton.Snap(Scale, ScaleTransform.ScaleYProperty, 1);
            ActionButton.Snap(Offset, TranslateTransform.XProperty, 0);
            ActionButton.Snap(Blur, BlurEffect.RadiusProperty, 0);
        }
    }
}
