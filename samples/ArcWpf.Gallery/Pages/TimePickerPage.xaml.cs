using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TimePickerPage : UserControl
{
    public TimePickerPage() { InitializeComponent(); TimeChanged(this, null); }
    private void TimeChanged(object sender, RoutedPropertyChangedEventArgs<TimeSpan?>? e) => Status.Text = Picker.SelectedTime is { } time ? $"Stored value: {time:hh\\:mm}" : "No time";
}
