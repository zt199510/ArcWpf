using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace ArcWpf.Gallery.Pages;
public partial class AvatarUploadPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private ImageSource? _pending;
    public AvatarUploadPage()
    {
        InitializeComponent();
        // The live demo's portraits are its own images, so these are neutral gradient placeholders.
        Avatar.Hint = "PNG, JPG or WebP, up to 5 MB";
        Avatar.ImageSource = Photo("#7A5CE0", "#5FE03A");
        foreach (var (from, to) in new[] { ("#7FBFC0", "#D8955A"), ("#E9C65A", "#4C8FE0"), ("#E0569A", "#6FD84C") })
        {
            var photo = Photo(from, to);
            var sample = new Button { Width = 36, Height = 36, MinWidth = 0, MinHeight = 0, Margin = new Thickness(Samples.Children.Count == 0 ? 0 : 8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, Template = SampleTemplate(photo) };
            System.Windows.Automation.AutomationProperties.SetName(sample, $"Sample photo {Samples.Children.Count + 1}");
            sample.Click += (_, _) => Upload(photo);
            Samples.Children.Add(sample);
        }
        _timer.Tick += (_, _) =>
        {
            Avatar.Progress += 6;
            if (Avatar.Progress < 100) return;
            _timer.Stop();
            Avatar.IsUploading = false;
            if (FailNext.IsChecked == true) { FailNext.IsChecked = false; Avatar.Error = "Upload failed. Try again."; return; }
            Avatar.ImageSource = _pending;
        };
    }
    private void Upload(ImageSource photo)
    {
        _pending = photo;
        Avatar.Error = string.Empty;
        Avatar.Progress = 0;
        Avatar.IsUploading = true;
        _timer.Start();
    }
    private void OnLarge(object sender, RoutedEventArgs e) => Avatar.Error = "That photo is 12 MB. The limit is 5 MB.";
    private void OnPdf(object sender, RoutedEventArgs e) => Avatar.Error = "PDF files aren't supported. Use PNG, JPG or WebP.";
    private static ImageSource Photo(string from, string to)
    {
        var image = new DrawingImage(new GeometryDrawing(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 60), null, new RectangleGeometry(new Rect(0, 0, 88, 88))));
        image.Freeze();
        return image;
    }
    private static ControlTemplate SampleTemplate(ImageSource photo)
    {
        var template = new ControlTemplate(typeof(Button));
        var root = new FrameworkElementFactory(typeof(Grid));
        var fill = new FrameworkElementFactory(typeof(Ellipse));
        fill.SetValue(Shape.FillProperty, new ImageBrush(photo));
        fill.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        var ring = new FrameworkElementFactory(typeof(Ellipse));
        ring.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.Border");
        ring.SetValue(Shape.StrokeThicknessProperty, 1d);
        root.AppendChild(fill);
        root.AppendChild(ring);
        template.VisualTree = root;
        return template;
    }
}
