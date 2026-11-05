using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ConversationSidebarPage : UserControl
{
    private int _next;

    public ConversationSidebarPage()
    {
        InitializeComponent();
        foreach (var (id, title, group) in new[]
        {
            ("orders", "Add region to orders", "Today"), ("events", "Partition the events table", "Today"),
            ("status", "Status page incident draft", "Previous 7 days"), ("churn", "Churn interview themes", "Previous 7 days"), ("pricing", "Pricing page copy review", "Previous 7 days"),
            ("onboarding", "Onboarding email sequence", "Older"), ("roadmap", "Q4 roadmap summary", "Older"),
        })
        {
            Sidebar.Conversations.Add(new ConversationItem(id, title, group));
        }
        Sidebar.SelectedId = "orders";
        Sidebar.NewRequested += (_, _) =>
        {
            _next++;
            var fresh = new ConversationItem("new-" + _next, _next == 1 ? "New chat" : $"New chat {_next}", "Today");
            Sidebar.Conversations.Insert(0, fresh);
            Sidebar.SelectedId = fresh.Id;
            Log.Text = "New " + fresh.Title;
        };
        Sidebar.Selected += (_, e) => Log.Text = "Selected " + e.Item.Title;
        Sidebar.Renamed += (_, e) => Log.Text = $"Renamed {e.OldTitle} to {e.Item.Title}";
        Sidebar.Archived += (_, e) => Log.Text = "Archived " + e.Item.Title;
        Sidebar.Restored += (_, e) => Log.Text = "Restored " + e.Item.Title;
    }
}
