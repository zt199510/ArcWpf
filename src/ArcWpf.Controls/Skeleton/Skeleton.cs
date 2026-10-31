using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

public enum SkeletonShape { Rectangle, Text, Circle }

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
public class Skeleton : Control
{
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(nameof(Shape), typeof(SkeletonShape), typeof(Skeleton), new FrameworkPropertyMetadata(SkeletonShape.Rectangle, OnVisualChanged));
    public static readonly DependencyProperty IsShimmeringProperty = DependencyProperty.Register(nameof(IsShimmering), typeof(bool), typeof(Skeleton), new FrameworkPropertyMetadata(true, OnVisualChanged));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(Skeleton), new FrameworkPropertyMetadata(new CornerRadius(8), OnVisualChanged));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Skeleton), new FrameworkPropertyMetadata(null, OnVisualChanged));
    public SkeletonShape Shape { get => (SkeletonShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
    public bool IsShimmering { get => (bool)GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    private Border? _surface; private TranslateTransform? _shimmer;
    static Skeleton() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(typeof(Skeleton))); }
    public override void OnApplyTemplate() { StopShimmer(); base.OnApplyTemplate(); _surface = GetTemplateChild("PART_Surface") as Border; StartShimmer(); }
    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Skeleton)d).RefreshVisual();
    private void RefreshVisual() { if (_surface is null) return; StopShimmer(); StartShimmer(); }
    private void StartShimmer()
    {
        if (_surface is null || !IsShimmering || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        _shimmer = new TranslateTransform(-1, 0); _surface.RenderTransform = _shimmer;
        var animation = new DoubleAnimation(-1, 1, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        _shimmer.BeginAnimation(TranslateTransform.XProperty, animation);
    }
    private void StopShimmer() { if (_shimmer is not null) _shimmer.BeginAnimation(TranslateTransform.XProperty, null); _shimmer = null; }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.Property == ArcMotion.LevelProperty) RefreshVisual(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new SkeletonPeer(this);
    private sealed class SkeletonPeer(Skeleton owner) : FrameworkElementAutomationPeer(owner) { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom; protected override string GetNameCore() => "Loading placeholder"; }
}
