using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ExpandingSearchPage : UserControl
{
    public ExpandingSearchPage()
    {
        InitializeComponent();
        var folder = (Geometry)FindResource("FolderKanban"); var file = (Geometry)FindResource("FileText");
        // Arc demo data (expanding-search demo chunk, 2026-10-30).
        ExpandingSearchItem Item(string id, string title, string meta, string group, Geometry icon, params string[] keywords) => new() { Id = id, Title = title, Meta = meta, Group = group, Icon = icon, Keywords = keywords };
        var items = new[]
        {
            Item("billing-migration", "Billing migration", "Payments · Due Oct 14", "Projects", folder, "invoices", "payments"),
            Item("mobile-onboarding", "Mobile onboarding", "Growth · Due Nov 3", "Projects", folder, "signup", "ios", "android"),
            Item("q4-planning", "Q4 planning", "Leadership · 8 docs", "Projects", folder, "roadmap", "goals"),
            Item("customer-portal", "Customer portal", "Support · 5 open tasks", "Projects", folder, "help", "tickets"),
            Item("release-checklist", "Release checklist", "Engineering · Edited Sep 18", "Docs", file, "deploy", "ship", "launch"),
            Item("incident-runbook", "Incident response runbook", "Platform · Edited Sep 12", "Docs", file, "on call", "outage"),
            Item("pricing-copy", "Pricing page copy", "Marketing · Edited Sep 20", "Docs", file, "plans", "website"),
            Item("rate-limits", "API rate limits", "Platform · Edited Sep 3", "Docs", file, "api", "throttle"),
            Item("billing-faq", "Billing FAQ for support", "Support · Edited Sep 19", "Docs", file, "invoices", "refunds"),
        };
        foreach (var item in items) Search.Items.Add(item);
        foreach (var id in new[] { "release-checklist", "billing-migration", "pricing-copy" }) Search.Suggestions.Add(items.First(item => item.Id == id));
    }

    // .lane: translateX((44 - 100%) / 2) while idle, none while searching, on --duration-spring.
    private void SearchExpanded(object sender, RoutedEventArgs e) => Shift(0);
    private void SearchCollapsed(object sender, RoutedEventArgs e) => Shift((44 - Lane.Width) / 2);
    private void Shift(double x) => LaneShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromSeconds(.58)) { EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 } });
    private void ItemSelected(object? sender, ExpandingSearchItemEventArgs e) => Status.Text = $"Opened {e.Item.Title}";
}
