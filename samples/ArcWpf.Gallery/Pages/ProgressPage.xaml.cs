using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ArcWpf.Gallery.Pages;

public partial class ProgressPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(70) };

    public ProgressPage()
    {
        InitializeComponent();
        _timer.Tick += AdvanceUpload;
        Unloaded += (_, _) => _timer.Stop();
    }

    private void StartUpload(object sender, RoutedEventArgs e)
    {
        AnimatedProgress.Value = 0;
        AnimatedStatus.Text = "正在上传…";
        StartButton.IsEnabled = false;
        _timer.Start();
    }

    private void AdvanceUpload(object? sender, EventArgs e)
    {
        AnimatedProgress.Value = Math.Min(100, AnimatedProgress.Value + 2.5);
        if (AnimatedProgress.Value < 100) return;
        _timer.Stop();
        AnimatedStatus.Text = "上传完成";
        StartButton.IsEnabled = true;
    }
}
