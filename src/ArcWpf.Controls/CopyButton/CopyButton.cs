using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class CopyButtonFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception) : RoutedEventArgs(routedEvent, source)
{
    public Exception Exception { get; } = exception;
}

[TemplatePart(Name = "PART_PressTarget", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_LabelSlot", Type = typeof(Border))]
[TemplatePart(Name = "PART_Label", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_PreviousLabel", Type = typeof(Image))]
[TemplatePart(Name = "PART_CopyPhase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_CheckPhase", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Check", Type = typeof(Shape))]
public class CopyButton : Button
{
    public static readonly RoutedUICommand CopyCommand = new("Copy", nameof(CopyCommand), typeof(CopyButton));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(CopyButton), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(CopyButton), new FrameworkPropertyMetadata("Copy", OnLabelChanged));
    public static readonly DependencyProperty CopiedLabelProperty = DependencyProperty.Register(nameof(CopiedLabel), typeof(string), typeof(CopyButton), new FrameworkPropertyMetadata("Copied", OnStateLabelChanged));
    public static readonly DependencyProperty ResetAfterMsProperty = DependencyProperty.Register(nameof(ResetAfterMs), typeof(int), typeof(CopyButton), new FrameworkPropertyMetadata(1600, null, (_, value) => Math.Max(0, (int)value)));
    private static readonly DependencyPropertyKey IsCopiedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCopied), typeof(bool), typeof(CopyButton), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsCopiedProperty = IsCopiedPropertyKey.DependencyProperty;
    public static readonly RoutedEvent CopyFailedEvent = EventManager.RegisterRoutedEvent(nameof(CopyFailed), RoutingStrategy.Bubble, typeof(EventHandler<CopyButtonFailedEventArgs>), typeof(CopyButton));

    private DispatcherTimer? _resetTimer;
    private FrameworkElement? _copyPhase;
    private FrameworkElement? _checkPhase;
    private Shape? _check;
    private readonly ScaleTransform _copyScale = new(1, 1);
    private readonly ScaleTransform _checkScale = new(1, 1);

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string CopiedLabel { get => (string)GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }
    public int ResetAfterMs { get => (int)GetValue(ResetAfterMsProperty); set => SetValue(ResetAfterMsProperty, value); }
    public bool IsCopied { get => (bool)GetValue(IsCopiedProperty); private set => SetValue(IsCopiedPropertyKey, value); }
    public event EventHandler<CopyButtonFailedEventArgs> CopyFailed { add => AddHandler(CopyFailedEvent, value); remove => RemoveHandler(CopyFailedEvent, value); }

    static CopyButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CopyButton), new FrameworkPropertyMetadata(typeof(CopyButton)));
        CommandManager.RegisterClassCommandBinding(typeof(CopyButton), new CommandBinding(CopyCommand,
            (sender, _) => ((CopyButton)sender).Copy(),
            (sender, e) => e.CanExecute = ((CopyButton)sender).IsEnabled));
    }

    public CopyButton()
    {
        SetCurrentValue(ContentProperty, Label);
        AutomationProperties.SetName(this, Label);
        Unloaded += (_, _) => ResetVisualState();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _copyPhase = GetTemplateChild("PART_CopyPhase") as FrameworkElement;
        _checkPhase = GetTemplateChild("PART_CheckPhase") as FrameworkElement;
        _check = GetTemplateChild("PART_Check") as Shape;
        if (_copyPhase is not null) _copyPhase.RenderTransform = _copyScale;
        if (_checkPhase is not null) _checkPhase.RenderTransform = _checkScale;
        SnapPhase();
    }

    protected override void OnClick()
    {
        if (!IsEnabled) return;
        base.OnClick();
        Copy();
    }

    public void Copy()
    {
        if (!IsEnabled) return;
        try
        {
            Clipboard.SetText(Value ?? string.Empty);
            StopResetTimer();
            SetValue(IsCopiedPropertyKey, true);
            SetCurrentValue(ContentProperty, CopiedLabel);
            if (ResetAfterMs > 0)
            {
                _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ResetAfterMs) };
                _resetTimer.Tick += ResetTimerTick;
                _resetTimer.Start();
            }
        }
        catch (Exception exception)
        {
            RaiseEvent(new CopyButtonFailedEventArgs(CopyFailedEvent, this, exception));
        }
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (CopyButton)d;
        AutomationProperties.SetName(button, (string)e.NewValue);
        OnStateLabelChanged(d, e);
    }

    private static void OnStateLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (CopyButton)d;
        button.SetCurrentValue(ContentProperty, button.IsCopied ? button.CopiedLabel : button.Label);
    }

    private void ResetTimerTick(object? sender, EventArgs e)
    {
        StopResetTimer();
        SetValue(IsCopiedPropertyKey, false);
        SetCurrentValue(ContentProperty, Label);
    }

    private void StopResetTimer()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    private void ResetVisualState()
    {
        StopResetTimer();
        SetValue(IsCopiedPropertyKey, false);
        SnapPhase();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsCopiedProperty || e.Property == ArcMotion.LevelProperty) UpdatePhase();
    }

    private void UpdatePhase()
    {
        if (_copyPhase is null || _checkPhase is null) return;
        var level = ArcMotion.GetLevel(this);
        if (!IsCopied || level == ArcMotionLevel.Off || !IsLoaded || !IsVisible)
        {
            SnapPhase();
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            SnapPhase();
            _copyPhase.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.FromSeconds(.12)));
            _checkPhase.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.12)));
            SnapCheck(true);
            return;
        }
        Tween(_copyPhase, OpacityProperty, .16, 1, 0);
        Tween(_copyScale, ScaleTransform.ScaleXProperty, .16, 1, .6);
        Tween(_copyScale, ScaleTransform.ScaleYProperty, .16, 1, .6);
        Tween(_checkPhase, OpacityProperty, .24, 0, 1);
        Tween(_checkScale, ScaleTransform.ScaleXProperty, .24, .6, 1);
        Tween(_checkScale, ScaleTransform.ScaleYProperty, .24, .6, 1);
        if (_check is not null)
        {
            SnapCheck(false);
            Tween(_check, Shape.StrokeDashOffsetProperty, .28, 12, 0, .05);
            Tween(_check, OpacityProperty, .05, 0, 1, .05);
        }
    }

    private void SnapPhase()
    {
        if (_copyPhase is not null) Snap(_copyPhase, OpacityProperty, IsCopied ? 0 : 1);
        if (_checkPhase is not null) Snap(_checkPhase, OpacityProperty, IsCopied ? 1 : 0);
        Snap(_copyScale, ScaleTransform.ScaleXProperty, 1); Snap(_copyScale, ScaleTransform.ScaleYProperty, 1);
        Snap(_checkScale, ScaleTransform.ScaleXProperty, 1); Snap(_checkScale, ScaleTransform.ScaleYProperty, 1);
        SnapCheck(IsCopied);
    }

    private void SnapCheck(bool visible)
    {
        if (_check is null) return;
        Snap(_check, Shape.StrokeDashOffsetProperty, visible ? 0 : 12);
        Snap(_check, OpacityProperty, visible ? 1 : 0);
    }

    private static void Tween(IAnimatable target, DependencyProperty property, double seconds, double from, double to, double delay = 0)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds)) { BeginTime = TimeSpan.FromSeconds(delay), FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        ((DependencyObject)target).SetValue(property, to);
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Snap(IAnimatable target, DependencyProperty property, double value)
    {
        target.BeginAnimation(property, null);
        ((DependencyObject)target).SetValue(property, value);
    }
}

