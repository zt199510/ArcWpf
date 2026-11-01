using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The first paint matches Arc's demo. Arc does not publish the demo's handlers, so the 1.4 GB that "Add files" adds is this page's own step, large enough to reach the near and over states.
public partial class UsageMeterPage : UserControl
{
    private readonly UsageMeterSegment _documents = new() { Id = "documents", Label = "Documents", Value = 4.2 };

    public UsageMeterPage()
    {
        InitializeComponent();
        Meter.Segments.Add(_documents);
        Meter.Segments.Add(new UsageMeterSegment { Id = "photos", Label = "Photos", Value = 5.1 });
        Meter.Segments.Add(new UsageMeterSegment { Id = "backups", Label = "Backups", Value = 3.1 });
    }

    private void AddFiles(object sender, RoutedEventArgs e) => _documents.Value += 1.4;
    private void Reset(object sender, RoutedEventArgs e) => _documents.Value = 4.2;
    private void PlanChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Meter is not null && Plan.SelectedValue is string limit) Meter.Limit = double.Parse(limit, CultureInfo.InvariantCulture);
    }
}
