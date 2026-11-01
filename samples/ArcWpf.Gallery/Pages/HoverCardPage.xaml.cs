using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages; public partial class HoverCardPage : UserControl { public HoverCardPage() { InitializeComponent(); var profile = new HoverCardProfile { DisplayName = "Jasmine Brooks", Role = "Product designer, Payments", Bio = "Designing calmer tools for complex work.", Meta = "Hong Kong · Online" }; profile.Stats.Add(new HoverCardStat { Label = "Projects", Value = "12" }); profile.Stats.Add(new HoverCardStat { Label = "Followers", Value = "248" }); Card.CardContent = profile; } }
