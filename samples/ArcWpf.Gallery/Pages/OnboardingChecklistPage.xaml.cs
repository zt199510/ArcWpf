using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class OnboardingChecklistPage : UserControl
{
    public OnboardingChecklistPage()
    {
        InitializeComponent();
        Checklist.DefaultCompleted = ["workspace"];
        Checklist.Steps.Add(new OnboardingStep { Id = "workspace", Title = "Create your workspace" });
        Checklist.Steps.Add(new OnboardingStep { Id = "team", Title = "Invite your team", Description = "Priya and Jonas are waiting.", ActionLabel = "Send invites", Action = () => Task.Delay(900) });
        Checklist.Steps.Add(new OnboardingStep { Id = "github", Title = "Connect GitHub", Description = "Link the northwind/web repo.", ActionLabel = "Connect GitHub", Action = () => Task.Delay(900) });
        Checklist.Steps.Add(new OnboardingStep { Id = "two-factor", Title = "Turn on two-factor sign-in", Description = "Protect admin access.", ActionLabel = "Turn on", Action = () => Task.Delay(900) });
    }

    private void OnReset(object sender, RoutedEventArgs e) => Checklist.Reset();
}
