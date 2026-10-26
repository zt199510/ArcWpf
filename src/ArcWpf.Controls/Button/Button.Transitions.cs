using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public partial class Button
{
    private Border? _labelSlot;
    private ContentPresenter? _label;
    private Image? _previousLabel;
    private int _contentGeneration;
    private DispatcherOperation? _contentUpdate;
    private readonly List<SolidColorBrush> _transitionBrushes = new();
    private readonly List<Action> _contentClocks = new();

    private void InitializeTransitions()
    {
        _labelSlot = GetTemplateChild("PART_LabelSlot") as Border;
        _label = GetTemplateChild("Label") as ContentPresenter;
        _previousLabel = GetTemplateChild("PART_PreviousLabel") as Image;
        ApplyPalette(false);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        double width = _labelSlot?.ActualWidth ?? 0;
        BitmapSource? previous = null;
        bool active = IsLoaded && IsVisible && !IsLoading && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;
        if (active && _label is { ActualWidth: > 0, ActualHeight: > 0 } label)
        {
            // Snapshot only this label: no second live UIElement parent, input handler or automation peer.
            var dpi = VisualTreeHelper.GetDpi(label);
            int w = (int)Math.Ceiling(label.ActualWidth * dpi.DpiScaleX);
            int h = (int)Math.Ceiling(label.ActualHeight * dpi.DpiScaleY);
            if (w <= 4096 && h <= 4096)
            {
                var bitmap = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(label);
                bitmap.Freeze();
                previous = bitmap;
            }
        }
        StopContentTransition();
        base.OnContentChanged(oldContent, newContent);
        if (!active || _labelSlot is null || _label is null || _previousLabel is null) { return; }
        int generation = _contentGeneration;
        _contentUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _contentUpdate = null;
            if (generation != _contentGeneration || !IsLoaded || !IsVisible || IsLoading) { return; }
            BeginContentTransition(width, previous, newContent is UIElement);
        }));
    }

    private void BeginContentTransition(double oldWidth, BitmapSource? previous, bool icon)
    {
        if (_labelSlot is null || _label is null || _previousLabel is null) { return; }
        var level = ArcMotion.GetLevel(this);
        if (level == ArcMotionLevel.Off) { return; }
        var slot = _labelSlot;
        var label = _label;
        var outgoing = _previousLabel;
        bool full = level == ArcMotionLevel.Full;
        int generation = _contentGeneration;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double targetWidth = label.DesiredSize.Width;
        if (full && oldWidth > 0 && targetWidth > 0 && double.IsNaN(Width))
        {
            // Only the inner slot changes. A stretched parent's final layout still determines the outer width.
            slot.Width = targetWidth;
            var widthAnimation = new DoubleAnimation(oldWidth, targetWidth, TimeSpan.FromSeconds(.84))
            { EasingFunction = new PressReleaseEase(), FillBehavior = FillBehavior.Stop };
            widthAnimation.Completed += (_, _) =>
            {
                if (generation != _contentGeneration) { return; }
                slot.BeginAnimation(WidthProperty, null);
                slot.ClearValue(WidthProperty);
            };
            slot.BeginAnimation(WidthProperty, widthAnimation);
        }
        if (previous is not null)
        {
            outgoing.Source = previous;
            outgoing.Width = previous.Width;
            outgoing.Height = previous.Height;
            Animate(outgoing, OpacityProperty, 1, 0, full ? .16 : .12, () =>
            {
                if (generation == _contentGeneration) { outgoing.Source = null; }
            });
        }
        Animate(slot, OpacityProperty, 0, 1, full ? .24 : .12);
        if (full)
        {
            var incomingTransform = new TransformGroup();
            var outgoingTransform = new TransformGroup();
            slot.RenderTransform = incomingTransform;
            outgoing.RenderTransform = outgoingTransform;
            slot.RenderTransformOrigin = outgoing.RenderTransformOrigin = new Point(.5, .5);
            if (icon)
            {
                var enter = new ScaleTransform(1, 1);
                var leave = new ScaleTransform(1, 1);
                incomingTransform.Children.Add(enter);
                outgoingTransform.Children.Add(leave);
                Animate(enter, ScaleTransform.ScaleXProperty, .6, 1, .26);
                Animate(enter, ScaleTransform.ScaleYProperty, .6, 1, .26);
                Animate(leave, ScaleTransform.ScaleXProperty, 1, .6, .16);
                Animate(leave, ScaleTransform.ScaleYProperty, 1, .6, .16);
            }
            else
            {
                var enter = new TranslateTransform();
                var leave = new TranslateTransform();
                incomingTransform.Children.Add(enter);
                outgoingTransform.Children.Add(leave);
                Animate(enter, TranslateTransform.YProperty, 4, 0, .24);
                Animate(leave, TranslateTransform.YProperty, 0, -3, .16);
            }
            var enterBlur = new BlurEffect();
            var exitBlur = new BlurEffect();
            slot.Effect = enterBlur;
            outgoing.Effect = exitBlur;
            Animate(enterBlur, BlurEffect.RadiusProperty, icon ? 2 : 4, 0, .24, () =>
            {
                if (generation == _contentGeneration) { slot.Effect = null; }
            });
            Animate(exitBlur, BlurEffect.RadiusProperty, 0, icon ? 2 : 4, .16);
        }
    }

    private void Animate(DependencyObject target, DependencyProperty property, double from, double to, double seconds, Action? completed = null)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        // Target is an owned template part/transform, never a public caller property.
        target.SetValue(property, to);
        int generation = _contentGeneration;
        Action stop = () =>
        {
            if (target is UIElement element) { element.BeginAnimation(property, null); }
            else if (target is Animatable animatable) { animatable.BeginAnimation(property, null); }
        };
        _contentClocks.Add(stop);
        animation.Completed += (_, _) =>
        {
            if (generation != _contentGeneration) { return; }
            stop();
            _contentClocks.Remove(stop);
            completed?.Invoke();
        };
        if (target is UIElement ui) { ui.BeginAnimation(property, animation); }
        else if (target is Animatable value) { value.BeginAnimation(property, animation); }
    }

    private void UpdateTransitionProperty(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property == BackgroundProperty || e.Property == ForegroundProperty || e.Property == BorderBrushProperty)
        {
            ApplyPalette(IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
        }
        if (e.Property == ArcMotion.LevelProperty || e.Property == IsLoadingProperty ||
            (e.Property == IsVisibleProperty && !IsVisible) || (e.Property == IsEnabledProperty && !IsEnabled))
        {
            StopTransitions();
        }
    }

    private void ApplyPalette(bool animate)
    {
        if (_pressTarget is not Border border || _label is null) { return; }
        var background = TransitionBrush(border.Background, Background, animate);
        var outline = TransitionBrush(border.BorderBrush, BorderBrush, animate);
        var foreground = TransitionBrush(TextElement.GetForeground(_label), Foreground, animate);
        border.SetCurrentValue(Border.BackgroundProperty, background);
        border.SetCurrentValue(Border.BorderBrushProperty, outline);
        _label.SetCurrentValue(TextElement.ForegroundProperty, foreground);
        if (_spinner is System.Windows.Shapes.Shape spinner) { spinner.SetCurrentValue(System.Windows.Shapes.Shape.StrokeProperty, foreground); }
    }

    private Brush? TransitionBrush(Brush? from, Brush? to, bool animate)
    {
        if (!animate || from is not SolidColorBrush start || to is not SolidColorBrush end || start.Color == end.Color) { return to; }
        var brush = end.CloneCurrentValue();
        _transitionBrushes.Add(brush);
        var animation = new ColorAnimation(start.Color, end.Color, TimeSpan.FromSeconds(.16)) { FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) =>
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            _transitionBrushes.Remove(brush);
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        return brush;
    }

    private void StopContentTransition()
    {
        ++_contentGeneration;
        foreach (var stop in _contentClocks) { stop(); }
        _contentClocks.Clear();
        _contentUpdate?.Abort();
        _contentUpdate = null;
        if (_labelSlot is not null)
        {
            _labelSlot.BeginAnimation(WidthProperty, null);
            _labelSlot.ClearValue(WidthProperty);
            _labelSlot.BeginAnimation(OpacityProperty, null);
            _labelSlot.Opacity = 1;
            _labelSlot.RenderTransform = Transform.Identity;
            _labelSlot.Effect = null;
        }
        if (_previousLabel is not null)
        {
            _previousLabel.BeginAnimation(OpacityProperty, null);
            _previousLabel.Opacity = 0;
            _previousLabel.Source = null;
            _previousLabel.RenderTransform = Transform.Identity;
            _previousLabel.Effect = null;
        }
    }

    private void StopTransitions()
    {
        StopContentTransition();
        foreach (var brush in _transitionBrushes) { brush.BeginAnimation(SolidColorBrush.ColorProperty, null); }
        _transitionBrushes.Clear();
        ApplyPalette(false);
    }
}
