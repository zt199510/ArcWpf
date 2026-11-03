using System;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class CountdownPage : UserControl
{
    private static readonly TimeSpan Far = TimeSpan.FromDays(8).Add(TimeSpan.FromHours(4)).Add(TimeSpan.FromMinutes(12));
    private bool _skipped;
    public CountdownPage()
    {
        InitializeComponent();
        Place(Far);
        Hero.Completed += (_, _) => Status.Text = "The keynote is live.";
    }
    private void Place(TimeSpan left)
    {
        var start = DateTime.UtcNow + left;
        Hero.Target = start;
        First.Target = start;
        Second.Target = start.AddMinutes(90);
        Third.Target = start.AddMinutes(195);
    }
    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _skipped = !_skipped;
        Place(_skipped ? TimeSpan.FromSeconds(10) : Far);
        Skip.Visibility = _skipped ? Visibility.Collapsed : Visibility.Visible;
        Restart.Visibility = _skipped ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = _skipped ? "Schedule moved ten seconds ahead" : string.Empty;
    }
}
