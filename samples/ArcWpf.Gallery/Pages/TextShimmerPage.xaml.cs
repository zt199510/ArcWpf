using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TextShimmerPage : UserControl
{
    private bool _done;

    public TextShimmerPage()
    {
        InitializeComponent();
        Paint();
    }

    private void OnFinish(object sender, RoutedEventArgs e)
    {
        _done = !_done;
        Paint();
    }

    private void Paint()
    {
        MainStatus.IsActive = RowStatus.IsActive = !_done;
        MainStatus.Text = _done ? "Summary ready" : "Generating summary";
        RowStatus.Text = _done ? "24 files synced" : "Syncing 24 files";
        MainIcon.Icon = _done ? "check" : "sparkles";
        RowIcon.Icon = _done ? "check" : "folder-sync";
        FinishLabel.Text = _done ? "Start again" : "Finish";
        string ink = _done ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.TextMuted";
        MainIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, ink);
        RowIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, ink);
    }
}
