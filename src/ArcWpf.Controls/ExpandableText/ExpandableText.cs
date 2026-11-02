using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;

/// <summary>
/// expandable-text.tsx: long text clamped to a few lines with a soft fade over the last one. One progress value drives the height, the
/// fade and the chevron, so they always move in step; the toggle only appears when the text really overflows.
/// </summary>
[TemplatePart(Name = "PART_Content", Type = typeof(ExpandableTextClip))]
[TemplatePart(Name = "PART_Toggle", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_MoreLabel", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_LessLabel", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Chevron", Type = typeof(Decorator))]
public class ExpandableText : ContentControl
{
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(int), typeof(ExpandableText), new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ExpandableText)d).UpdateClamp(), (_, v) => Math.Max(1, (int)v)));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandableText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedChanged));
    public static readonly DependencyProperty MoreLabelProperty = DependencyProperty.Register(nameof(MoreLabel), typeof(string), typeof(ExpandableText), new FrameworkPropertyMetadata("Read more"));
    public static readonly DependencyProperty LessLabelProperty = DependencyProperty.Register(nameof(LessLabel), typeof(string), typeof(ExpandableText), new FrameworkPropertyMetadata("Show less"));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(ExpandableText), new PropertyMetadata(0d, (d, _) => ((ExpandableText)d).ApplyProgress()));
    public int Lines { get => (int)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public string MoreLabel { get => (string)GetValue(MoreLabelProperty); set => SetValue(MoreLabelProperty, value); }
    public string LessLabel { get => (string)GetValue(LessLabelProperty); set => SetValue(LessLabelProperty, value); }
    public bool IsOverflowing { get; private set; }
    public event RoutedEventHandler? ExpandedChanged;

    // A soft ease out: it answers the click at once, then spends most of its time settling.
    private static readonly IEasingFunction Ease = FrozenEase();
    private ExpandableTextClip? _content;
    private System.Windows.Controls.Button? _toggle;
    private FrameworkElement? _more, _less;
    private Decorator? _chevron;
    private readonly RotateTransform _rotate = new();
    private bool _animating;
    private int _run;

    static ExpandableText() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExpandableText), new FrameworkPropertyMetadata(typeof(ExpandableText)));
    public ExpandableText()
    {
        PreviewGotKeyboardFocus += OnContentFocus;
        Unloaded += (_, _) => Settle();
    }

    private static IEasingFunction FrozenEase()
    {
        var ease = new BezierEase(.32, .5, .2, 1);
        ease.Freeze();
        return ease;
    }

    public override void OnApplyTemplate()
    {
        if (_toggle is not null) _toggle.Click -= ToggleClick;
        base.OnApplyTemplate();
        _content = GetTemplateChild("PART_Content") as ExpandableTextClip;
        _toggle = GetTemplateChild("PART_Toggle") as System.Windows.Controls.Button;
        _more = GetTemplateChild("PART_MoreLabel") as FrameworkElement;
        _less = GetTemplateChild("PART_LessLabel") as FrameworkElement;
        _chevron = GetTemplateChild("PART_Chevron") as Decorator;
        if (_chevron is not null)
        {
            _chevron.Child = Lucide.Glyph(Lucide.ChevronDown, 16);
            _chevron.RenderTransform = _rotate;
        }
        if (_toggle is not null) _toggle.Click += ToggleClick;
        if (_content is not null) _content.Measured = OnMeasured;
        SetValue(ProgressProperty, IsExpanded ? 1d : 0d);
        UpdateClamp();
        UpdateLabels(false);
        UpdateToggleName();
    }

    private double LineHeight
    {
        get
        {
            double line = TextBlock.GetLineHeight(this);
            return double.IsNaN(line) || line <= 0 ? FontSize * 1.4 : line;
        }
    }

    private void UpdateClamp()
    {
        if (_content is null) return;
        _content.LineHeight = LineHeight;
        _content.Lines = Lines;
        _content.InvalidateMeasure();
    }

    // The clip reports each measurement, so the toggle stays correct as the width or font changes.
    private void OnMeasured(double full, double clamp)
    {
        IsOverflowing = full > clamp + 1;
        var toggle = IsOverflowing ? Visibility.Visible : Visibility.Collapsed;
        if (_toggle is not null && _toggle.Visibility != toggle) _toggle.Visibility = toggle;
        UpdateMask();
    }

    private static void OnExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ExpandableText)d;
        control.Move((bool)e.NewValue);
        control.UpdateToggleName();
        control.ExpandedChanged?.Invoke(control, new RoutedEventArgs());
    }

    private void ToggleClick(object sender, RoutedEventArgs e)
    {
        bool next = !IsExpanded;
        if (!next) BringStartIntoView();
        SetCurrentValue(IsExpandedProperty, next);
        e.Handled = true;
    }

    // Closing from far down the page would leave the reader below the text.
    private void BringStartIntoView()
    {
        for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not ScrollViewer viewer) continue;
            if (TransformToAncestor(viewer).Transform(new Point()).Y < 0) BringIntoView(new Rect(0, 0, ActualWidth, 1));
            return;
        }
    }

    // A link or button hidden below the clamp opens the text when it receives keyboard focus.
    private void OnContentFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsExpanded || !IsOverflowing || _content is null || e.NewFocus is not FrameworkElement target || !_content.IsAncestorOf(target)) return;
        double bottom = target.TransformToAncestor(_content).Transform(new Point(0, target.ActualHeight)).Y;
        if (bottom > _content.ActualHeight + 1) SetCurrentValue(IsExpandedProperty, true);
    }

    private void Move(bool expanded)
    {
        double target = expanded ? 1 : 0;
        int run = ++_run;
        var level = ArcMotion.GetLevel(this);
        bool motion = IsLoaded && IsVisible && _content is not null && level == ArcMotionLevel.Full && IsOverflowing;
        UpdateLabels(IsLoaded && level != ArcMotionLevel.Off);
        if (!motion)
        {
            _animating = false;
            Tween.Set(this, ProgressProperty, target);
            UpdateMask();
            return;
        }
        double span = Math.Max(_content!.FullHeight - _content.ClampHeight, 1);
        double distance = Math.Abs(target - (double)GetValue(ProgressProperty)) * span;
        // Opening takes 500 to 600 ms and closing 400 to 450 ms, growing gently with the distance the text travels.
        double seconds = expanded ? .5 + Math.Min(distance / 1600, .1) : .4 + Math.Min(distance / 2000, .05);
        _animating = true;
        UpdateMask();
        Tween.To(this, ProgressProperty, target, seconds, Ease, done: () => { if (run == _run) Settle(); });
    }

    private void Settle()
    {
        _animating = false;
        Tween.Set(this, ProgressProperty, IsExpanded ? 1d : 0d);
        UpdateMask();
    }

    private void ApplyProgress()
    {
        double progress = (double)GetValue(ProgressProperty);
        _rotate.Angle = progress * 180;
        if (_content is not null) _content.Progress = progress;
        UpdateMask();
    }

    // The fade eases away as the text opens and only returns as it nears the clamp again.
    private void UpdateMask()
    {
        if (_content is null) return;
        double progress = Math.Clamp((double)GetValue(ProgressProperty), 0, 1);
        double fade = Math.Pow(1 - progress, 1.35);
        bool masked = IsOverflowing && (!IsExpanded || _animating) && fade > .002;
        _content.SetFade(masked ? fade : 0, LineHeight);
    }

    // The labels crossfade with a short vertical drift and a brief blur, timed to the opening.
    private void UpdateLabels(bool animate)
    {
        if (_more is null || _less is null) return;
        bool reduced = ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
        SetLabel(_more, !IsExpanded, -4, animate, reduced);
        SetLabel(_less, IsExpanded, 4, animate, reduced);
    }

    private static void SetLabel(FrameworkElement label, bool active, double offset, bool animate, bool reduced)
    {
        if (label.RenderTransform is not TranslateTransform move) label.RenderTransform = move = new TranslateTransform();
        double y = active ? 0 : offset;
        if (!animate || reduced)
        {
            Tween.Set(label, OpacityProperty, active ? 1 : 0);
            Tween.Set(move, TranslateTransform.YProperty, y);
            label.Effect = null;
            return;
        }
        Tween.To(label, OpacityProperty, active ? 1 : 0, Tween.Standard, ArcEasing.Standard);
        Tween.To(move, TranslateTransform.YProperty, y, Tween.Considered, ArcEasing.Standard);
        var effect = new BlurEffect { Radius = active ? 2 : 0 };
        label.Effect = effect;
        var blur = new DoubleAnimation(active ? 0 : 2, TimeSpan.FromSeconds(Tween.Standard)) { EasingFunction = ArcEasing.Standard };
        blur.Completed += (_, _) => { if (ReferenceEquals(label.Effect, effect)) label.Effect = null; };
        effect.BeginAnimation(BlurEffect.RadiusProperty, blur);
    }

    private void UpdateToggleName()
    {
        if (_toggle is not null) AutomationProperties.SetName(_toggle, IsExpanded ? LessLabel : MoreLabel);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandableTextPeer(this);
    private sealed class ExpandableTextPeer(ExpandableText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.IsExpanded ? owner.LessLabel : owner.MoreLabel;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>Lays its child out at full height and shows only the part that the progress allows, faded by a mask over the last 1.6 lines.</summary>
public sealed class ExpandableTextClip : Decorator
{
    internal Action<double, double>? Measured;
    internal double LineHeight { get; set; } = 19.6;
    internal int Lines { get; set; } = 3;
    internal double FullHeight { get; private set; }
    internal double ClampHeight => Math.Min(Lines * LineHeight, FullHeight);
    private double _progress, _fade, _maskLine;

    internal double Progress
    {
        get => _progress;
        set { _progress = value; InvalidateMeasure(); }
    }

    public ExpandableTextClip() => ClipToBounds = true;

    private double VisibleHeight => ClampHeight + (FullHeight - ClampHeight) * Math.Clamp(_progress, 0, 1);

    internal void SetFade(double fade, double line)
    {
        _fade = fade;
        _maskLine = line;
        UpdateMask();
    }

    private void UpdateMask()
    {
        double height = VisibleHeight;
        if (_fade <= 0 || height <= 0) { OpacityMask = null; return; }
        double solid = Math.Clamp((height - 1.6 * _maskLine) / height, 0, 1);
        byte end = (byte)Math.Round(255 * (1 - _fade * .96));
        // A relative mask would span the clipped child's full height, which spreads the fade over text nobody can see.
        OpacityMask = new LinearGradientBrush(new GradientStopCollection { new(Colors.Black, 0), new(Colors.Black, solid), new(Color.FromArgb(end, 0, 0, 0), 1) }, new Point(0, 0), new Point(0, height)) { MappingMode = BrushMappingMode.Absolute };
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var child = Child;
        if (child is null) return new Size();
        child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        FullHeight = child.DesiredSize.Height;
        Measured?.Invoke(FullHeight, Lines * LineHeight);
        UpdateMask();
        return new Size(child.DesiredSize.Width, VisibleHeight);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, FullHeight));
        return arrangeSize;
    }
}
