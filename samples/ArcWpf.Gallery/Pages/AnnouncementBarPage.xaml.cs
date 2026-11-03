using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        Bar.IsOpenChanged += (_, _) => UpdateShowAgain();
    }

    private void OnShowAgain(object sender, RoutedEventArgs e) => Bar.IsOpen = true;

    // Rises in as the bar closes; goes straight away when the bar comes back, so it never sits on the returning bar.
    private void UpdateShowAgain()
    {
        var rise = new TranslateTransform();
        ShowAgain.RenderTransform = rise;
        ShowAgain.BeginAnimation(OpacityProperty, null);
        if (!Bar.IsOpen)
        {
            ShowAgain.Visibility = Visibility.Visible;
            if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
            var duration = TimeSpan.FromMilliseconds(240);
            var ease = new ExponentialEase { Exponent = 7, EasingMode = EasingMode.EaseOut };
            ShowAgain.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, duration) { EasingFunction = ease });
        }
        else ShowAgain.Visibility = Visibility.Collapsed;
    }
}
