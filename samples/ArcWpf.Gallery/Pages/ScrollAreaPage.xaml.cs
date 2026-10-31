using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ScrollAreaPage : UserControl
{
    private sealed record Activity(string Name, string Text, string When) { public string Initials => string.Concat(Name.Split(' ').Select(part => part[0])); }
    private sealed record Room(string Name, string Note);

    public ScrollAreaPage()
    {
        InitializeComponent();
        // Arc scroll-area demo data (2026-10-30).
        ActivityItems.ItemsSource = new[]
        {
            new Activity("Emma Collins", "approved the onboarding redesign", "2m"), new Activity("Marcus Johnson", "merged fix for the flaky checkout test", "9m"),
            new Activity("Chloe Nguyen", "shared the September retention report", "21m"), new Activity("Daniel Kim", "rotated the staging API keys", "38m"),
            new Activity("Sofia Ramirez", "moved the vendor review to Thursday", "1h"), new Activity("Jasmine Brooks", "left 4 comments on Pricing page v3", "1h"),
            new Activity("Olivia Bennett", "scaled the worker pool to 12 nodes", "2h"), new Activity("Ryan Sullivan", "closed the Northwind renewal", "3h"),
            new Activity("Hannah Walsh", "resolved 18 support tickets", "4h"), new Activity("Mateo Alvarez", "published iOS build 4.12 to TestFlight", "5h"),
            new Activity("Nathan Cole", "scheduled the Q4 planning offsite", "6h"), new Activity("Ava Mitchell", "launched the fall email campaign", "Yesterday"),
        };
        RoomItems.ItemsSource = new[]
        {
            new Room("Living room", "Oak beams, linen sofas"), new Room("Sunroom", "Round table for six"), new Room("Study", "Green walls, walnut desk"),
            new Room("Bedroom", "Striped linen, oak bed"), new Room("Reading nook", "Armchair and ottoman"), new Room("Pool house", "Glass walls, long pool"),
        };
    }

    private void ActivityEdges(object? sender, ScrollAreaEdgesChangedEventArgs e) => BackToTop.Visibility = e.Edges.Top ? Visibility.Visible : Visibility.Collapsed;
    private void BackToTopClick(object sender, RoutedEventArgs e) => ActivityArea.Viewport?.ScrollToTop();
    private void RoomEdges(object? sender, ScrollAreaEdgesChangedEventArgs e) { PrevRoom.IsEnabled = e.Edges.Left; NextRoom.IsEnabled = e.Edges.Right; }
    // One card plus the 12 DIP gap per press, like the demo's scrollBy.
    private void PrevClick(object sender, RoutedEventArgs e) => Rooms.Viewport?.ScrollToHorizontalOffset(Rooms.Viewport.HorizontalOffset - 200);
    private void NextClick(object sender, RoutedEventArgs e) => Rooms.Viewport?.ScrollToHorizontalOffset(Rooms.Viewport.HorizontalOffset + 200);
}
