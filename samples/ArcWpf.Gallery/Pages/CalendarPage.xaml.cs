using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CalendarPage : UserControl
{
    public CalendarPage()
    {
        InitializeComponent();
        DeliveryCalendar.DisplayDateStart = DateTime.Today;
        // Arc's DeliveryDate sample blocks Sundays. This finite demo covers the next year.
        DeliveryCalendar.DisplayDateEnd = DateTime.Today.AddYears(1);
        for (var date = DateTime.Today; date <= DeliveryCalendar.DisplayDateEnd; date = date.AddDays(1))
            if (date.DayOfWeek == DayOfWeek.Sunday) DeliveryCalendar.BlackoutDates.Add(new CalendarDateRange(date));
    }
}
