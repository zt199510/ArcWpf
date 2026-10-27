using System.Windows;
using System.Windows.Media;

namespace ArcWpf.Controls;

internal sealed class PasswordEye : FrameworkElement
{
    internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(PasswordEye), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    internal static readonly DependencyProperty SlashOpacityProperty = DependencyProperty.Register(nameof(SlashOpacity), typeof(double), typeof(PasswordEye), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    internal static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(PasswordEye), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    internal double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    internal double SlashOpacity { get => (double)GetValue(SlashOpacityProperty); set => SetValue(SlashOpacityProperty, value); }
    internal Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    internal static readonly Geometry Outline = Geometry.Parse("M2.062,12.348 A1,1 0 0 1 2.062,11.652 A10.75,10.75 0 0 1 21.938,11.652 A1,1 0 0 1 21.938,12.348 A10.75,10.75 0 0 1 2.062,12.348");
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing); drawing.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        var pen = new Pen(Stroke, 1.75) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Eye() { drawing.DrawGeometry(null, pen, Outline); drawing.DrawEllipse(null, pen, new Point(12, 12), 3, 3); }
        double progress = Math.Clamp(Progress, 0, 1), opacity = Math.Clamp(SlashOpacity, 0, 1);
        if (progress <= .0001 || opacity <= .0001) Eye();
        else
        {
            var slash = new LineGeometry(new Point(2, 2), new Point(2 + 20 * progress, 2 + 20 * progress));
            var maskPen = new Pen(Brushes.Black, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, 24, 24)), slash.GetWidenedPathGeometry(maskPen));
            drawing.PushOpacity(1 - opacity); Eye(); drawing.Pop();
            drawing.PushOpacity(opacity); drawing.PushClip(clip); Eye(); drawing.Pop(); drawing.DrawGeometry(null, pen, slash); drawing.Pop();
        }
        drawing.Pop();
    }
}
