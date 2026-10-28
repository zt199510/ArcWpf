using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ActionButtonFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception) : RoutedEventArgs(routedEvent, source)
{
    public Exception Exception { get; } = exception;
}

public class ActionButton : Button
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Save", OnLabelChanged));
    public static readonly DependencyProperty PendingLabelProperty = DependencyProperty.Register(nameof(PendingLabel), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Saving"));
    public static readonly DependencyProperty SuccessLabelProperty = DependencyProperty.Register(nameof(SuccessLabel), typeof(string), typeof(ActionButton), new FrameworkPropertyMetadata("Saved"));
    public static readonly DependencyProperty ResetAfterMsProperty = DependencyProperty.Register(nameof(ResetAfterMs), typeof(int), typeof(ActionButton), new FrameworkPropertyMetadata(2400, null, CoerceResetAfterMs));
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(Func<Task>), typeof(ActionButton), new PropertyMetadata(null));
    public static readonly DependencyProperty IsPendingProperty = DependencyProperty.Register(nameof(IsPending), typeof(bool), typeof(ActionButton), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsSuccessProperty = DependencyProperty.Register(nameof(IsSuccess), typeof(bool), typeof(ActionButton), new FrameworkPropertyMetadata(false));
    private static readonly DependencyProperty IsCompletingProperty = DependencyProperty.Register(nameof(IsCompleting), typeof(bool), typeof(ActionButton), new FrameworkPropertyMetadata(false));
    public static readonly RoutedEvent ActionFailedEvent = EventManager.RegisterRoutedEvent(nameof(ActionFailed), RoutingStrategy.Bubble, typeof(EventHandler<ActionButtonFailedEventArgs>), typeof(ActionButton));

    private DispatcherTimer? _resetTimer;
    private DispatcherTimer? _completionTimer;
    private int _runId;
    private Path? _arrow;
    private Path? _check;
    private readonly TranslateTransform _arrowTranslation = new();
    private readonly RotateTransform _arrowRotation = new();

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string PendingLabel { get => (string)GetValue(PendingLabelProperty); set => SetValue(PendingLabelProperty, value); }
    public string SuccessLabel { get => (string)GetValue(SuccessLabelProperty); set => SetValue(SuccessLabelProperty, value); }
    public int ResetAfterMs { get => (int)GetValue(ResetAfterMsProperty); set => SetValue(ResetAfterMsProperty, value); }
    public Func<Task>? Action { get => (Func<Task>?)GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
    public bool IsPending { get => (bool)GetValue(IsPendingProperty); private set => SetValue(IsPendingProperty, value); }
    public bool IsSuccess { get => (bool)GetValue(IsSuccessProperty); private set => SetValue(IsSuccessProperty, value); }
    internal bool IsCompleting => (bool)GetValue(IsCompletingProperty);
    public event EventHandler<ActionButtonFailedEventArgs> ActionFailed { add => AddHandler(ActionFailedEvent, value); remove => RemoveHandler(ActionFailedEvent, value); }

    static ActionButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ActionButton), new FrameworkPropertyMetadata(typeof(ActionButton)));

    public ActionButton()
    {
        SetCurrentValue(ContentProperty, Label);
        AutomationProperties.SetName(this, Label);
        Unloaded += (_, _) => CancelRun();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _arrow = GetTemplateChild("PART_Arrow") as Path;
        _check = GetTemplateChild("PART_Check") as Path;
        if (_arrow is not null)
        {
            var transforms = new TransformGroup();
            transforms.Children.Add(_arrowTranslation);
            transforms.Children.Add(_arrowRotation);
            _arrow.RenderTransform = transforms;
        }
        UpdateActionVisuals(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPendingProperty || e.Property == IsCompletingProperty || e.Property == IsSuccessProperty || e.Property == ArcMotion.LevelProperty)
            UpdateActionVisuals(e.Property != ArcMotion.LevelProperty);
    }

    private static object CoerceResetAfterMs(DependencyObject _, object value) => Math.Max(0, (int)value);
    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ActionButton)d;
        if (!button.IsPending && !button.IsSuccess) button.SetCurrentValue(ContentProperty, e.NewValue);
        AutomationProperties.SetName(button, (string)e.NewValue);
    }

    protected override void OnClick()
    {
        if (IsPending || IsSuccess || !IsEnabled) return;
        if (Action is null) { base.OnClick(); return; }
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        _ = RunActionAsync(Action);
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        var run = ++_runId;
        _resetTimer?.Stop();
        SetCurrentValue(IsPendingProperty, true);
        SetCurrentValue(IsSuccessProperty, false);
        SetCurrentValue(IsCompletingProperty, false);
        SetCurrentValue(ContentProperty, string.IsNullOrEmpty(PendingLabel) ? Label : PendingLabel);
        try
        {
            await action().ConfigureAwait(true);
            if (run != _runId || !IsLoaded) return;
            SetCurrentValue(IsPendingProperty, false);
            SetCurrentValue(IsCompletingProperty, true);
            SetCurrentValue(IsSuccessProperty, false);
            SetCurrentValue(ContentProperty, SuccessLabel);
            _completionTimer?.Stop();
            _completionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _completionTimer.Tick += CompletionTimerTick;
            _completionTimer.Start();
        }
        catch (Exception exception)
        {
            if (run != _runId) return;
            SetCurrentValue(IsPendingProperty, false);
            SetCurrentValue(IsCompletingProperty, false);
            SetCurrentValue(IsSuccessProperty, false);
            SetCurrentValue(ContentProperty, Label);
            RaiseEvent(new ActionButtonFailedEventArgs(ActionFailedEvent, this, exception));
        }
    }

    private void ResetTimerTick(object? sender, EventArgs e)
    {
        _resetTimer?.Stop();
        _resetTimer = null;
        if (!IsPending) { SetCurrentValue(IsSuccessProperty, false); SetCurrentValue(ContentProperty, Label); }
    }

    private void CancelRun()
    {
        ++_runId;
        _resetTimer?.Stop();
        _completionTimer?.Stop();
        _resetTimer = null;
        _completionTimer = null;
        SetCurrentValue(IsPendingProperty, false);
        SetCurrentValue(IsCompletingProperty, false);
        SetCurrentValue(IsSuccessProperty, false);
    }

    private void UpdateActionVisuals(bool animate)
    {
        var full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        var targetArrow = IsSuccess ? 0d : 1d;
        var targetCheck = IsSuccess ? 1d : 0d;
        var targetOffset = IsSuccess ? 0d : 20d;
        if (_arrow is not null)
        {
            _arrow.BeginAnimation(OpacityProperty, null);
            _arrowTranslation.BeginAnimation(TranslateTransform.XProperty, null);
            _arrow.Opacity = targetArrow;
            _arrowTranslation.X = targetArrow == 0 ? 6 : 0;
            if ((IsPending || IsCompleting) && IsLoaded && IsVisible && full)
            {
                _arrowRotation.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.7)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            else
            {
                _arrowRotation.BeginAnimation(RotateTransform.AngleProperty, null);
                _arrowRotation.Angle = 0;
            }
            if (animate && full && IsLoaded && IsVisible)
            {
                var opacity = new DoubleAnimation(_arrow.Opacity == 0 ? 1 : 0, targetArrow, TimeSpan.FromSeconds(.16)) { FillBehavior = FillBehavior.Stop };
                var x = new DoubleAnimation(_arrowTranslation.X == 6 ? 0 : 6, _arrowTranslation.X, TimeSpan.FromSeconds(.18)) { FillBehavior = FillBehavior.Stop };
                _arrow.BeginAnimation(OpacityProperty, opacity);
                _arrowTranslation.BeginAnimation(TranslateTransform.XProperty, x);
            }
        }
        if (_check is not null)
        {
            _check.BeginAnimation(OpacityProperty, null);
            _check.BeginAnimation(Path.StrokeDashOffsetProperty, null);
            _check.Opacity = targetCheck;
            _check.StrokeDashOffset = targetOffset;
            if (animate && full && IsLoaded && IsVisible && IsSuccess)
            {
                _check.Opacity = 1;
                _check.StrokeDashOffset = 0;
                _check.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.16)) { FillBehavior = FillBehavior.Stop });
                _check.BeginAnimation(Path.StrokeDashOffsetProperty, new DoubleAnimation(20, 0, TimeSpan.FromSeconds(.28)) { FillBehavior = FillBehavior.Stop });
            }
        }
    }

    private void CompletionTimerTick(object? sender, EventArgs e)
    {
        _completionTimer?.Stop();
        _completionTimer = null;
        SetCurrentValue(IsCompletingProperty, false);
        SetCurrentValue(IsSuccessProperty, true);
        UpdateActionVisuals(true);
        if (ResetAfterMs > 0)
        {
            _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ResetAfterMs) };
            _resetTimer.Tick += ResetTimerTick;
            _resetTimer.Start();
        }
    }
}




