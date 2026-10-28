using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_PressTarget", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(FrameworkElement))]
public partial class Button : System.Windows.Controls.Button
{
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ButtonVariant), typeof(Button),
        new FrameworkPropertyMetadata(ButtonVariant.Primary), value => value is ButtonVariant variant && Enum.IsDefined(variant));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(ControlSize), typeof(Button),
        new FrameworkPropertyMetadata(ControlSize.Medium), value => value is ControlSize size && Enum.IsDefined(size));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPressAnimationEnabledProperty = DependencyProperty.Register(nameof(IsPressAnimationEnabled), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(true));

    public ButtonVariant Variant { get => (ButtonVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public ControlSize Size { get => (ControlSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public bool IsPressAnimationEnabled { get => (bool)GetValue(IsPressAnimationEnabledProperty); set => SetValue(IsPressAnimationEnabledProperty, value); }

    private FrameworkElement? _pressTarget;
    private FrameworkElement? _spinner;
    private Transform? _previousPressTransform;
    private Transform? _previousSpinnerTransform;
    private ScaleTransform? _scale;
    private RotateTransform? _rotation;
    private bool _spinning;
    private int _pressGeneration;

    static Button() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(typeof(Button)));

    public Button()
    {
        // Keep the control surface free of the WPF default black focus adorner.
        SetCurrentValue(FocusVisualStyleProperty, null);
        Loaded += (_, _) => UpdateMotion(false);
        Unloaded += (_, _) => StopMotion();
        IsVisibleChanged += (_, _) => UpdateMotion(false);
    }

    protected override void OnClick()
    {
        // All native activation routes, including ButtonAutomationPeer, dispatch through this method.
        if (!IsEnabled || IsLoading) { return; }
        base.OnClick();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);

    protected override void OnTemplateChanged(System.Windows.Controls.ControlTemplate oldTemplate, System.Windows.Controls.ControlTemplate newTemplate)
    {
        ReleaseTemplate();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _pressTarget = GetTemplateChild("PART_PressTarget") as FrameworkElement;
        _spinner = GetTemplateChild("PART_Spinner") as FrameworkElement;
        if (_pressTarget is not null)
        {
            _previousPressTransform = _pressTarget.RenderTransform;
            _scale = new ScaleTransform(1, 1);
            var group = new TransformGroup();
            group.Children.Add(_previousPressTransform);
            group.Children.Add(_scale);
            _pressTarget.SetCurrentValue(RenderTransformProperty, group);
        }
        if (_spinner is not null)
        {
            _previousSpinnerTransform = _spinner.RenderTransform;
            _rotation = new RotateTransform();
            var group = new TransformGroup();
            group.Children.Add(_previousSpinnerTransform);
            group.Children.Add(_rotation);
            _spinner.SetCurrentValue(RenderTransformProperty, group);
        }
        InitializeTransitions();
        UpdateMotion(false);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        UpdateTransitionProperty(e);
        if (e.Property == IsPressedProperty || e.Property == IsLoadingProperty || e.Property == IsEnabledProperty ||
            e.Property == IsPressAnimationEnabledProperty || e.Property == ArcMotion.LevelProperty)
        {
            UpdateMotion(e.Property == IsPressedProperty);
        }
    }

    // Derived async buttons keep focus while busy but must not answer presses.
    private protected virtual bool IsBusy => IsLoading;

    private protected void RefreshMotion() => UpdateMotion(false);

    private void UpdateMotion(bool animatePress)
    {
        bool active = IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        bool spin = active && IsLoading && _rotation is not null;
        if (spin != _spinning)
        {
            _spinning = spin;
            _rotation?.BeginAnimation(RotateTransform.AngleProperty, spin ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.7))
            { RepeatBehavior = RepeatBehavior.Forever } : null);
            if (!spin && _rotation is not null) { _rotation.Angle = 0; }
        }
        if (_scale is null) { return; }
        bool pressed = active && !IsBusy && IsPressAnimationEnabled && IsPressed;
        double target = pressed ? (ActualWidth > 220 ? .985 : ActualWidth > 0 && ActualWidth <= 48 ? .96 : .97) : 1;
        double current = _scale.ScaleX;
        int generation = ++_pressGeneration;
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _scale.ScaleX = _scale.ScaleY = target;
        if (!active || !animatePress || !IsPressAnimationEnabled || Math.Abs(current - target) < .00001) { return; }

        DoubleAnimationBase animation;
        if (pressed)
        {
            animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(.12),
                KeyFrames =
                {
                    new DiscreteDoubleKeyFrame(current, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                    new SplineDoubleKeyFrame(target, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.12)), new KeySpline(.22, 1, .36, 1))
                }
            };
        }
        else
        {
            // Analytic critically damped approximation: 90% by .26s, then settle. Not a port of Motion's solver.
            animation = new DoubleAnimation(current, target, TimeSpan.FromSeconds(.52)) { EasingFunction = new PressReleaseEase() };
        }
        animation.FillBehavior = FillBehavior.Stop;
        var scale = _scale;
        animation.Completed += (_, _) =>
        {
            if (generation != _pressGeneration) { return; }
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private void StopMotion()
    {
        StopTransitions();
        ++_pressGeneration;
        _spinning = false;
        _rotation?.BeginAnimation(RotateTransform.AngleProperty, null);
        if (_rotation is not null) { _rotation.Angle = 0; }
        _scale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        if (_scale is not null) { _scale.ScaleX = _scale.ScaleY = 1; }
    }

    private void ReleaseTemplate()
    {
        StopMotion();
        if (_pressTarget is not null) { _pressTarget.SetCurrentValue(RenderTransformProperty, _previousPressTransform); }
        if (_spinner is not null) { _spinner.SetCurrentValue(RenderTransformProperty, _previousSpinnerTransform); }
        _pressTarget = _spinner = null;
        _scale = null;
        _rotation = null;
        _labelSlot = null;
        _label = null;
        _previousLabel = null;
    }

    private sealed class PressReleaseEase : EasingFunctionBase
    {
        public PressReleaseEase() => EasingMode = EasingMode.EaseIn;
        protected override double EaseInCore(double t) => (1 - (1 + 7.78 * t) * Math.Exp(-7.78 * t)) / (1 - 8.78 * Math.Exp(-7.78));
        protected override Freezable CreateInstanceCore() => new PressReleaseEase();
    }
}

