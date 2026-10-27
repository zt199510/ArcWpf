using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class AvatarGroupPage : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<AvatarGroupMember> Team { get; } = MakeTeam();
    public ObservableCollection<AvatarGroupMember> People { get; } = MakeTeam();
    public AvatarGroupMember[] DuplicateNames { get; } = [new() { DisplayName = "Alex Kim" }, new() { DisplayName = "Alex Kim" }];
    private int _next = 6;
    public int VisibleLimit { get; private set; } = 4;
    public AvatarGroupPage() { InitializeComponent(); DataContext = this; }
    private static ObservableCollection<AvatarGroupMember> MakeTeam() => [new() { DisplayName = "Maya Chen", Source = AvatarPage.CreatePortrait(), Status = AvatarStatus.Online }, new() { DisplayName = "Leo Park" }, new() { DisplayName = "Sara Ruiz" }, new() { DisplayName = "Tom Hale" }, new() { DisplayName = "Ines Ma" }];
    private void Add(object sender, RoutedEventArgs e) => People.Add(new AvatarGroupMember { DisplayName = $"Guest {_next++}" });
    private void Remove(object sender, RoutedEventArgs e) { if (People.Count > 0) People.RemoveAt(0); }
    private void Move(object sender, RoutedEventArgs e) { if (People.Count > 1) People.Move(0, People.Count - 1); }
    private void Rename(object sender, RoutedEventArgs e) { if (People.Count > 0) { People[0].DisplayName = People[0].DisplayName == "Updated Member" ? "Maya Chen" : "Updated Member"; People[0].Status = People[0].Status == AvatarStatus.Online ? AvatarStatus.Offline : AvatarStatus.Online; } }
    private void Decrease(object sender, RoutedEventArgs e) { VisibleLimit = Math.Max(0, VisibleLimit - 1); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibleLimit))); }
    private void Increase(object sender, RoutedEventArgs e) { VisibleLimit++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibleLimit))); }
}
