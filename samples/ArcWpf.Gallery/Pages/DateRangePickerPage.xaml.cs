using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class DateRangePickerPage : UserControl
{
    public DateRangePickerPage() { InitializeComponent(); Picker.MinimumDate = DateTime.Today.AddDays(-30); Picker.MaximumDate = DateTime.Today.AddDays(90); }
    private void RangeChanged(object sender, RoutedEventArgs e) => Status.Text = Picker.StartDate is { } start && Picker.EndDate is { } end ? $"{start:d} → {end:d}" : "请选择结束日期";
}
