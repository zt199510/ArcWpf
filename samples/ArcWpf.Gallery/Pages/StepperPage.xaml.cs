using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// Only the Workspace pane is published in Arc's page markup, so the other steps show their own title and description instead of invented forms.
public partial class StepperPage : UserControl
{
    public StepperPage()
    {
        InitializeComponent();
        Steps.Steps.Add(new StepperStep { Title = "Profile", Description = "Your name and photo" });
        Steps.Steps.Add(new StepperStep { Title = "Workspace", Description = "Name and web address" });
        Steps.Steps.Add(new StepperStep { Title = "Team", Description = "Invite collaborators" });
        Steps.Steps.Add(new StepperStep { Title = "Review", Description = "Confirm and finish" });
        Steps.CurrentStepChanged += StepChanged;
        SyncPane();
    }

    private void BackClick(object sender, RoutedEventArgs e) => Steps.Previous();
    private void ContinueClick(object sender, RoutedEventArgs e) => Steps.Next();
    private void StepChanged(object? sender, RoutedEventArgs e) => SyncPane();

    private void SyncPane()
    {
        int index = Steps.CurrentIndex;
        bool known = index >= 0 && index < Steps.Steps.Count;
        WorkspacePane.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        OtherPane.Visibility = index == 1 ? Visibility.Collapsed : Visibility.Visible;
        PaneTitle.Text = known ? Steps.Steps[index].Title : "All set";
        PaneDescription.Text = known ? Steps.Steps[index].Description : "";
        Back.IsEnabled = index > 0;
    }

    private void NameChanged(object sender, TextChangedEventArgs e)
    {
        string slug = string.Join("-", WorkspaceName.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Slug.Text = slug.Length == 0 ? "your-workspace" : slug;
    }

    private void LayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Steps is null) return;
        bool vertical = Layout.SelectedValue as string == "vertical";
        Steps.Orientation = vertical ? StepperOrientation.Vertical : StepperOrientation.Horizontal;
        StepsColumn.Width = vertical ? new GridLength(192) : new GridLength(1, GridUnitType.Star);
        PanelColumn.Width = vertical ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Grid.SetRow(PanelHost, vertical ? 0 : 1);
        Grid.SetColumn(PanelHost, vertical ? 1 : 0);
        PanelHost.Margin = vertical ? new Thickness() : new Thickness(0, 24, 0, 0);
        PanelHost.Padding = vertical ? new Thickness(24, 0, 0, 0) : new Thickness();
        PanelHost.BorderThickness = vertical ? new Thickness(1, 0, 0, 0) : new Thickness();
    }
}
