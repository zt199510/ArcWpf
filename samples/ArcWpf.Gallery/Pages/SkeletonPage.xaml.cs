using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace ArcWpf.Gallery.Pages;
public partial class SkeletonPage : UserControl
{
    // Arc skeleton demo: loads after 1.6s; Reload shows the placeholder again.
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1600) };

    public SkeletonPage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { _timer.Stop(); SetLoading(false); };
        Loaded += (_, _) => Reload();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void ReloadClick(object sender, RoutedEventArgs e) => Reload();
    private void Reload() { _timer.Stop(); SetLoading(true); _timer.Start(); }
    private void SetLoading(bool loading) { Summary.IsLoading = loading; Status.Text = loading ? "Loading project summary" : "Updated just now"; }
}
