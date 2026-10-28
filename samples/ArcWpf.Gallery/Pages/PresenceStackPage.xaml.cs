using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class PresenceStackPage : UserControl
{
    public ObservableCollection<PresencePerson> People { get; } = [new("maya") { Name = "Maya Chen", Source = AvatarPage.CreatePortrait() }, new("leo") { Name = "Leo Park", Status = PresenceStatus.Typing }, new("sara") { Name = "Sara Ruiz", Status = PresenceStatus.Idle }, new("tom") { Name = "Tom Hale", Detail = "Editing the canvas" }, new("ines") { Name = "Ines Ma" }];
    public ObservableCollection<PresencePerson> DemoPeople { get; } = [new("jasmine") { Name = "Jasmine Brooks", Source = AvatarPage.CreatePortrait() }, new("emma") { Name = "Emma Collins", Status = PresenceStatus.Typing }, new("marcus") { Name = "Marcus Johnson" }, new("sofia") { Name = "Sofia Ramirez", Status = PresenceStatus.Idle }, new("maya") { Name = "Maya Chen" }, new("leo") { Name = "Leo Park" }];
    private readonly DispatcherTimer _demoTimer = new() { Interval = TimeSpan.FromSeconds(3.5) };
    private bool _playing = true;
    private int _tick;
    private int _next = 1;
    public PresenceStackPage()
    {
        InitializeComponent(); DataContext = this;
        DemoPeople.CollectionChanged += (_, _) => DemoMeta.Text = $"You and {Math.Max(0, DemoPeople.Count - 1)} others";
        _demoTimer.Tick += (_, _) => { _tick++; if (_tick % 3 == 1) DemoJoin(this, new()); else if (_tick % 3 == 2) DemoStatus(this, new()); else DemoLeave(this, new()); };
        Loaded += (_, _) => UpdatePlayback(); Unloaded += (_, _) => _demoTimer.Stop(); IsVisibleChanged += (_, _) => UpdatePlayback();
    }
    private void UpdatePlayback() { if (IsLoaded && IsVisible && _playing) _demoTimer.Start(); else _demoTimer.Stop(); }
    private void TogglePlayback(object sender, RoutedEventArgs e) { _playing = !_playing; Playback.Content = _playing ? "Pause" : "Play"; UpdatePlayback(); }
    private void DemoJoin(object sender, RoutedEventArgs e) { int id = _next++; DemoPeople.Add(new("demo-" + id) { Name = "Guest " + id }); }
    private void DemoLeave(object sender, RoutedEventArgs e) { if (DemoPeople.Count > 1) DemoPeople.RemoveAt(DemoPeople.Count - 1); }
    private void DemoStatus(object sender, RoutedEventArgs e) { if (DemoPeople.Count > 1) DemoPeople[1].Status = (PresenceStatus)(((int)DemoPeople[1].Status + 1) % 3); }
    private void Join(object sender, RoutedEventArgs e) { int id = _next++; People.Add(new("guest-" + id) { Name = "Guest " + id }); }
    private void Leave(object sender, RoutedEventArgs e) { if (People.Count > 0) People.RemoveAt(0); }
    private void Reorder(object sender, RoutedEventArgs e) { if (People.Count > 1) People.Move(0, People.Count - 1); }
    private void Status(object sender, RoutedEventArgs e) { if (People.Count > 0) People[0].Status = (PresenceStatus)(((int)People[0].Status + 1) % 3); }
    private void Detail(object sender, RoutedEventArgs e) { if (People.Count > 0) People[0].Detail = People[0].Detail is null ? "Editing the canvas" : null; }
    private void Selected(object sender, PresencePersonSelectedEventArgs e) => Result.Text = "已选择：" + e.Person.Name;
}
