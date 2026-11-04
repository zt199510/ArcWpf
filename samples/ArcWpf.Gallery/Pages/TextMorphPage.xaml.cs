using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TextMorphPage : UserControl
{
    private static readonly string[] Steps = ["Publish", "Publishing", "Published"];
    private int _step;
    private bool _following;

    public TextMorphPage()
    {
        InitializeComponent();
        Paint();
    }

    private void OnNext(object sender, RoutedEventArgs e) => Status.Text = Steps[_step = (_step + 1) % Steps.Length];

    private void OnFollow(object sender, RoutedEventArgs e)
    {
        _following = !_following;
        FollowText.Text = _following ? "Following" : "Follow";
        FollowIcon.Icon = _following ? "check" : "plus";
        Paint();
    }

    private void Paint()
    {
        Follow.SetResourceReference(BackgroundProperty, _following ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.Surface");
        Follow.SetResourceReference(BorderBrushProperty, _following ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.Border");
        string ink = _following ? "ArcWpf.Brush.Background" : "ArcWpf.Brush.Foreground";
        FollowText.SetResourceReference(ForegroundProperty, ink);
        FollowIcon.SetResourceReference(ArcWpf.Controls.ArcDataGridIcon.StrokeProperty, ink);
    }
}
