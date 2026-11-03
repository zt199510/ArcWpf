using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class FileDropzonePage : UserControl
{
    private bool _failOnce = true;
    public FileDropzonePage()
    {
        InitializeComponent();
        Zone.Upload = Simulate;
        // The live demo's thumbnail is its own photograph, so this is a neutral gradient placeholder.
        var photo = new DrawingImage(new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(0x8A, 0x7A, 0x66), Color.FromRgb(0xCF, 0xC6, 0xB6), 60), null, new RectangleGeometry(new Rect(0, 0, 40, 40))));
        photo.Freeze();
        Zone.Items.Add(new FileDropzoneItem { Name = "lounge-chair-hero.jpg", Size = (long)(1.8 * 1048576), Status = "uploaded", PreviewSource = photo });
        Zone.Items.Add(new FileDropzoneItem { Name = "brand-guidelines.pdf", Size = (long)(2.4 * 1048576), Status = "uploaded" });
        Zone.Items.Add(new FileDropzoneItem { Name = "launch-video.mov", Size = (long)(8.6 * 1048576), Status = "failed", Error = "Connection lost" });
    }
    private async Task Simulate(FileDropzoneItem item, Action<double> progress, CancellationToken token)
    {
        for (int percent = 0; percent <= 100; percent += 8)
        {
            progress(percent);
            await Task.Delay(70, token);
        }
        if (_failOnce && item.Name.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)) { _failOnce = false; throw new InvalidOperationException("Connection lost"); }
    }
}
