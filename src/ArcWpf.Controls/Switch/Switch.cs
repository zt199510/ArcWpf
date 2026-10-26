using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Thumb", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
public class Switch : ToggleButton
{
    private FrameworkElement? _thumb;
    private Transform? _originalTransform;
    private readonly ControlSpring _position = new(0, .3);
    private readonly ControlSpring _width = new(18, .26);
    private TranslateTransform? _translation;
    private ScaleTransform? _pulse;
    private long _lastFrame;
    private long _lastRelease;
    private bool _wasPressed;
    private bool _rendering;
    private int _pulseGeneration;
    internal bool IsMotionRunning => _rendering;

    static Switch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(typeof(Switch)));
        IsCheckedProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, (_, value) => value ?? false));
        IsThreeStateProperty.OverrideMetadata(typeof(Switch), new FrameworkPropertyMetadata(false, null, (_, _) => false));
    }

    public Switch()
    {
        // Arc controls use state surfaces instead of the WPF default black focus adorner.
        SetCurrentValue(FocusVisualStyleProperty, null);
        Loaded += (_, _) => SetMotionTargets(false);
        Unloaded += (_, _) => SetMotionTargets(false);
        IsVisibleChanged += (_, _) => SetMotionTargets(false);
    }

    protected override void OnClick()
    {
        if (IsEnabled) { base.OnClick(); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (!e.IsRepeat && IsEnabled) { OnClick(); }
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty)
        {
            if (_wasPressed && !IsPressed) { _lastRelease = Stopwatch.GetTimestamp(); }
            _wasPressed = IsPressed;
        }
        if (e.Property == IsCheckedProperty || e.Property == IsPressedProperty || e.Property == IsEnabledProperty ||
            e.Property == ArcMotion.LevelProperty)
        {
            bool animate = IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
            SetMotionTargets(animate);
            if (e.Property == IsCheckedProperty && animate && !IsPressed &&
                (_lastRelease == 0 || Stopwatch.GetElapsedTime(_lastRelease).TotalMilliseconds >= 250))
            {
                Pulse();
            }
        }
    }

    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _thumb = GetTemplateChild("PART_Thumb") as FrameworkElement;
        if (_thumb is not null)
        {
            _originalTransform = _thumb.RenderTransform;
            _translation = new TranslateTransform();
            _pulse = new ScaleTransform(1, 1);
            var transforms = new TransformGroup();
            transforms.Children.Add(_originalTransform);
            transforms.Children.Add(_pulse);
            transforms.Children.Add(_translation);
            _thumb.SetCurrentValue(RenderTransformProperty, transforms);
        }
        SetMotionTargets(false);
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        ReleaseParts();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    internal void SetMotionTargets(bool animate)
    {
        bool full = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        bool pressed = animate && full && IsEnabled && IsPressed;
        double width = pressed ? 23 : 18;
        double x = IsChecked == true ? 36 - width : 0;
        _position.Target = x;
        _width.Target = width;
        if (!animate || !full || !IsEnabled || _thumb is null)
        {
            StopRendering();
            ++_pulseGeneration;
            _position.Snap(x);
            _width.Snap(width);
            _pulse?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ApplyGeometry();
            return;
        }
        if (!_rendering)
        {
            _lastFrame = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnRendering;
            _rendering = true;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds;
        _lastFrame = now;
        AdvanceMotion(seconds);
    }

    internal void AdvanceMotion(double seconds)
    {
        bool positionDone = _position.Step(seconds);
        bool widthDone = _width.Step(seconds);
        ApplyGeometry();
        if (positionDone && widthDone) { StopRendering(); }
    }

    private void ApplyGeometry()
    {
        if (_thumb is not null) { _thumb.SetCurrentValue(WidthProperty, _width.Position); }
        if (_translation is not null) { _translation.X = _position.Position; }
    }

    private void Pulse()
    {
        if (_pulse is null) { return; }
        var pulse = _pulse;
        int generation = ++_pulseGeneration;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(.34), FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(pulse.ScaleX, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(1.16, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.136)), new KeySpline(.22, 1, .36, 1)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.34)), new KeySpline(.22, 1, .36, 1)));
        animation.Completed += (_, _) =>
        {
            if (generation == _pulseGeneration) { pulse.BeginAnimation(ScaleTransform.ScaleXProperty, null); }
        };
        pulse.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    private void StopRendering()
    {
        if (!_rendering) { return; }
        CompositionTarget.Rendering -= OnRendering;
        _rendering = false;
    }

    private void ReleaseParts()
    {
        StopRendering();
        ++_pulseGeneration;
        _pulse?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        if (_thumb is not null)
        {
            _thumb.SetCurrentValue(RenderTransformProperty, _originalTransform);
            _thumb.ClearValue(WidthProperty);
        }
        _thumb = null;
        _translation = null;
        _pulse = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SwitchPeer(this);

    private sealed class SwitchPeer(Switch owner) : ToggleButtonAutomationPeer(owner), IToggleProvider
    {
        protected override string GetClassNameCore() => nameof(Switch);
        protected override string GetLocalizedControlTypeCore() => "开关";
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);
        ToggleState IToggleProvider.ToggleState => owner.IsChecked == true ? ToggleState.On : ToggleState.Off;
        void IToggleProvider.Toggle()
        {
            if (!owner.IsEnabled) { throw new ElementNotEnabledException(); }
            owner.OnClick();
        }
    }
}
