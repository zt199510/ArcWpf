using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class AttachmentTrayPage : UserControl
{
    private readonly Dictionary<PromptAttachment, DispatcherTimer> _uploads = [];
    private int _next;

    public AttachmentTrayPage()
    {
        InitializeComponent();
        Tray.Items.Add(new PromptAttachment("launch-brief.pdf") { Detail = "586 KB" });
        Tray.Items.Add(new PromptAttachment("hero-shot.png") { Detail = "1.2 MB", Preview = Swatch() });
        var failed = new PromptAttachment("q3-numbers.xlsx") { Detail = "4.8 MB" };
        failed.Error = "Network connection lost";
        failed.Status = PromptAttachmentStatus.Error;
        Tray.Items.Add(failed);
        Tray.RetryRequested += (_, e) => { Log.Text = "Retry " + e.Item.Name; Run(e.Item, false); };
        Tray.Removed += (_, e) => { Log.Text = "Removed " + e.Item.Name; if (_uploads.Remove(e.Item, out var timer)) { timer.Stop(); } };
        Tray.PreviewRequested += (_, e) => Log.Text = "Preview " + e.Item.Name;
        Unloaded += (_, _) => { foreach (var timer in _uploads.Values) { timer.Stop(); } _uploads.Clear(); };
    }

    // A placeholder picture: gradients only, so the sample ships no image material.
    private static DrawingImage Swatch()
    {
        var fill = new LinearGradientBrush(Color.FromRgb(0x4F, 0x7C, 0xF0), Color.FromRgb(0xF0, 0x9A, 0x5F), 45);
        return new DrawingImage(new GeometryDrawing(fill, null, new RectangleGeometry(new Rect(0, 0, 96, 96))));
    }

    private void OnAdd(object sender, RoutedEventArgs e) => Add(false);
    private void OnAddFailing(object sender, RoutedEventArgs e) => Add(true);

    private void Add(bool fails)
    {
        _next++;
        var item = new PromptAttachment(fails ? $"will-fail-{_next}.zip" : $"notes-{_next}.md") { Detail = fails ? "9.1 MB" : "12 KB" };
        item.Progress = 0;
        item.Status = PromptAttachmentStatus.Uploading;
        Tray.Items.Add(item);
        Run(item, fails);
    }

    private void Run(PromptAttachment item, bool fails)
    {
        if (_uploads.Remove(item, out var running)) { running.Stop(); }
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(240) };
        timer.Tick += (_, _) =>
        {
            double next = item.Progress + .17;
            if (fails && next > .5) { timer.Stop(); _uploads.Remove(item); item.Error = "Upload rejected by the server"; item.Status = PromptAttachmentStatus.Error; return; }
            if (next >= 1) { timer.Stop(); _uploads.Remove(item); item.Progress = 1; item.Status = PromptAttachmentStatus.Done; return; }
            item.Progress = next;
        };
        _uploads[item] = timer;
        timer.Start();
    }

    private void OnLayout(object sender, RoutedEventArgs e)
    {
        bool chips = Tray.Layout == AttachmentTrayLayout.List;
        Tray.Layout = chips ? AttachmentTrayLayout.Chips : AttachmentTrayLayout.List;
        LayoutButton.Content = chips ? "Show as list" : "Show as chips";
    }
}
