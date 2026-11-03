using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class AnnouncementBarPage : UserControl
{
    public AnnouncementBarPage()
    {
        InitializeComponent();
        Bar.Announcements.Add(new AnnouncementItem { Id = "launch", Message = "Fieldwork 4.0 is here: docs, plans, and cycles in one place", ActionLabel = "See what's new" });
        Bar.Announcements.Add(new AnnouncementItem { Id = "sale", Message = "Annual plans are 20% off.", CountdownLabel = "Ends in", CountdownTarget = System.DateTimeOffset.UtcNow.AddDays(2).AddHours(3), ActionLabel = "Upgrade" });
        Bar.Announcements.Add(new AnnouncementItem { Id = "maintenance", Message = "Scheduled maintenance Sunday, 2:00 to 2:30 AM ET", ActionLabel = "View status" });
    }
}
