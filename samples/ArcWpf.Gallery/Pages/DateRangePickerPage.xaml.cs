using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class DateRangePickerPage : UserControl
{
    public DateRangePickerPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        Picker.StartDate = today.AddDays(-6); Picker.EndDate = today; Picker.MaximumDate = today;
        Picker.Boundary = Stage;
        UpdateNote();
    }
    private void RangeChanged(object? sender, DateRangeChangedEventArgs e) => UpdateNote();
    private void UpdateNote() => Note.Text = Picker.StartDate is { } start && Picker.EndDate is { } end ? $"{(end - start).Days + 1} {((end - start).Days == 0 ? "day" : "days")} selected" : "No range selected";
}
