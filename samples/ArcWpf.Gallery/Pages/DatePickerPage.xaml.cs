using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class DatePickerPage : UserControl
{
    public static readonly System.Windows.DependencyProperty StartDateProperty = System.Windows.DependencyProperty.Register(nameof(StartDate), typeof(DateTime?), typeof(DatePickerPage));
    public DateTime? StartDate { get => (DateTime?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DatePickerPage() => InitializeComponent();
}
