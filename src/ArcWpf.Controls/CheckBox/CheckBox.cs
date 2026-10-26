using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Path = System.Windows.Shapes.Path;
namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Box", Type = typeof(Border))]
[TemplatePart(Name = "PART_Fill", Type = typeof(Border))]
[TemplatePart(Name = "PART_Mark", Type = typeof(Path))]
public class CheckBox : System.Windows.Controls.CheckBox
{
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(CheckBox), new FrameworkPropertyMetadata(string.Empty));
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    private Border? _box, _fill;
    private Path? _mark;
    private ScaleTransform? _press, _fillScale;
    private Transform? _boxTransform, _fillTransform;
    private readonly ControlSpring _draw = new(0, .26);
    private readonly ControlSpring _morph = new(0, .42);
    private readonly List<Action> _stops = new();
    private long _lastFrame;
    private int _generation;
    private bool _rendering;
    private SolidColorBrush? _border;
    internal bool IsMotionRunning => _rendering || _stops.Count != 0;
    internal double DrawProgress => _draw.Position;
    internal double MorphProgress => _morph.Position;
    static CheckBox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CheckBox), new FrameworkPropertyMetadata(typeof(CheckBox)));
    public CheckBox()
    {
        SetCurrentValue(FocusVisualStyleProperty, null);
        Loaded += (_, _) => UpdateVisual(false);
        Unloaded += (_, _) => UpdateVisual(false);
        IsVisibleChanged += (_, _) => UpdateVisual(false);
    }
    protected override void OnClick() { if (IsEnabled) base.OnClick(); }
    public override void OnApplyTemplate()
    {
        ReleaseParts(); base.OnApplyTemplate();
        _box = GetTemplateChild("PART_Box") as Border;
        _fill = GetTemplateChild("PART_Fill") as Border;
        _mark = GetTemplateChild("PART_Mark") as Path;
        if (_box is not null) { _boxTransform = _box.RenderTransform; _press = AttachScale(_box); }
        if (_fill is not null) { _fillTransform = _fill.RenderTransform; _fillScale = AttachScale(_fill); }
        UpdateVisual(false);
    }
    private static ScaleTransform AttachScale(FrameworkElement element)
    {
        var scale = new ScaleTransform(1, 1);
        var group = new TransformGroup(); group.Children.Add(element.RenderTransform); group.Children.Add(scale);
        element.SetCurrentValue(RenderTransformProperty, group); return scale;
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    { ReleaseParts(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsCheckedProperty || e.Property == IsPressedProperty || e.Property == IsEnabledProperty ||
            e.Property == ArcMotion.LevelProperty || e.Property == BorderBrushProperty)
            UpdateVisual(IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
    }
    internal void UpdateVisual(bool animate)
    {
        if (_draw is null) return; // WPF may set properties during construction.
        animate &= IsEnabled && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        double opacity = IsChecked != false ? 1 : 0;
        double fillOpacity = _fill?.Opacity ?? opacity, markOpacity = _mark?.Opacity ?? opacity;
        double pressed = _press?.ScaleX ?? 1;
        Color? currentColor = _border?.Color;
        StopClocks();
        _draw.Target = opacity; _morph.Target = IsChecked is null ? 1 : 0;
        if (!animate)
        {
            StopFrames(); _draw.Snap(opacity); _morph.Snap(IsChecked is null ? 1 : 0);
        }
        if (_fill is not null) Fade(_fill, fillOpacity, opacity, animate);
        if (_mark is not null) Fade(_mark, markOpacity, opacity, animate);
        if (_press is not null)
        {
            double target = animate && IsPressed ? .95 : 1;
            _press.ScaleX = _press.ScaleY = target;
            if (animate && Math.Abs(pressed - target) > .0001)
            {
                double duration = IsPressed ? .12 : .58;
                Animate(_press, ScaleTransform.ScaleXProperty, pressed, target, duration, !IsPressed);
                Animate(_press, ScaleTransform.ScaleYProperty, pressed, target, duration, !IsPressed);
            }
        }
        if (_box is not null)
        {
            if (BorderBrush is SolidColorBrush source)
            {
                _border = new SolidColorBrush(source.Color);
                _box.SetCurrentValue(Border.BorderBrushProperty, _border);
                if (animate && currentColor is { } color && color != source.Color)
                {
                    var brush = _border;
                    var animation = new ColorAnimation(color, source.Color, TimeSpan.FromSeconds(.16)) { FillBehavior = FillBehavior.Stop, EasingFunction = new StandardEase() };
                    Track(() => brush.BeginAnimation(SolidColorBrush.ColorProperty, null), animation);
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                }
            }
            else { _border = null; _box.SetCurrentValue(Border.BorderBrushProperty, BorderBrush); }
        }
        ApplyGeometry();
        if (animate && (_fill is not null || _mark is not null) && !_rendering &&
            (Math.Abs(_draw.Position - _draw.Target) > .0001 || Math.Abs(_morph.Position - _morph.Target) > .0001))
        { _lastFrame = Stopwatch.GetTimestamp(); CompositionTarget.Rendering += OnRendering; _rendering = true; }
    }
    private void Fade(UIElement element, double from, double to, bool animate)
    {
        // These template-part opacities are owned by the motion engine. SetCurrentValue
        // is transient under animation invalidation: removing the clock can restore
        // the default opacity (1), flashing the unchecked fill back on.
        // Keep a durable animation base; public control properties/bindings are untouched.
        element.SetValue(OpacityProperty, to);
        if (animate && Math.Abs(from - to) > .0001) Animate(element, OpacityProperty, from, to, to == 1 ? .12 : .16, false);
    }
    // Cubic-bezier via WPF KeySpline; release uses the same frozen CSS spring samples as Popover.
    private void Animate(DependencyObject target, DependencyProperty property, double from, double to, double seconds, bool spring)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(seconds), FillBehavior = FillBehavior.Stop };
        if (spring)
        {
            double[] samples = [0,.0213,.0739,.1443,.2235,.305,.3849,.4606,.5305,.594,.6508,.7011,.7452,.7836,.8168,.8454,.8699,.8907,.9084,.9234,.9361,.9467,.9557,.9632,.9694,.9747,.979,.9826,.9857,.9882,.9902,.992,.9934,.9946,.9955,.9963,1];
            for (int i=0;i<samples.Length;i++) animation.KeyFrames.Add(new LinearDoubleKeyFrame(from+(to-from)*samples[i],KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds*i/(samples.Length-1)))));
        }
        else
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)), new KeySpline(.22,1,.36,1)));
        }
        void Begin(AnimationTimeline? value) { if(target is UIElement ui) ui.BeginAnimation(property,value); else ((Animatable)target).BeginAnimation(property,value); }
        Track(() => Begin(null), animation); Begin(animation);
    }
    private void Track(Action stop, Timeline animation)
    {
        int generation = _generation; _stops.Add(stop);
        animation.Completed += (_, _) => { if(generation != _generation) return; stop(); _stops.Remove(stop); };
    }
    private void StopClocks() { ++_generation; foreach(var stop in _stops) stop(); _stops.Clear(); }
    private void OnRendering(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp(); double dt = Stopwatch.GetElapsedTime(_lastFrame,now).TotalSeconds; _lastFrame = now; AdvanceMotion(dt);
    }
    internal void AdvanceMotion(double seconds)
    {
        bool drawDone = _draw.Step(seconds), morphDone = _morph.Step(seconds);
        ApplyGeometry(); if(drawDone && morphDone) StopFrames();
    }
    private void ApplyGeometry()
    {
        double progress = Math.Clamp(_draw.Position,0,1);
        if(_fillScale is not null) _fillScale.ScaleX = _fillScale.ScaleY = .6 + .4 * progress;
        if(_mark is not null) _mark.Data = CreateMark(Math.Clamp(_morph.Position,0,1),progress);
    }
    internal static Geometry CreateMark(double morph, double progress)
    {
        Point Lerp(Point a, Point b) => a + (b-a)*morph;
        Point a=Lerp(new(4.25,9.25),new(4.75,9)), b=Lerp(new(7.25,12.25),new(9,9)), c=Lerp(new(13.75,5.75),new(13.25,9));
        var geometry = new StreamGeometry();
        double first=(b-a).Length, second=(c-b).Length, remaining=(first+second)*progress;
        if(progress > 0)
        {
            using var context=geometry.Open(); context.BeginFigure(a,false,false);
            context.LineTo(a+(b-a)*Math.Min(1,remaining/first),true,false);
            if(remaining>first) context.LineTo(b+(c-b)*Math.Min(1,(remaining-first)/second),true,false);
        }
        geometry.Freeze(); return geometry;
    }
    private void StopFrames() { if(!_rendering)return; CompositionTarget.Rendering -= OnRendering; _rendering=false; }
    private void ReleaseParts()
    {
        StopFrames(); StopClocks();
        if(_box is not null && _boxTransform is not null) _box.SetCurrentValue(RenderTransformProperty,_boxTransform);
        if(_fill is not null && _fillTransform is not null) _fill.SetCurrentValue(RenderTransformProperty,_fillTransform);
        _box=null; _fill=null; _mark=null; _press=null; _fillScale=null; _border=null;
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new CheckBoxPeer(this);
    private sealed class CheckBoxPeer(CheckBox owner) : CheckBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CheckBox);
        protected override string GetNameCore() { string name=base.GetNameCore(); return string.IsNullOrEmpty(name)?nameof(CheckBox):name; }
        protected override string GetHelpTextCore() { string help=base.GetHelpTextCore(); return string.IsNullOrEmpty(help)?owner.Description:help; }
    }
    private sealed class StandardEase : EasingFunctionBase
    {
        private readonly KeySpline _spline = new(.22,1,.36,1);
        public StandardEase() => EasingMode = EasingMode.EaseIn;
        protected override double EaseInCore(double normalizedTime) => _spline.GetSplineProgress(normalizedTime);
        protected override Freezable CreateInstanceCore() => new StandardEase();
    }
}
