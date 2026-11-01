using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class ResizablePanelsPage : UserControl
{
    public ResizablePanelsPage()
    {
        InitializeComponent();
        AddLines(SidebarLines, 70, 52, 64);
        AddLines(ContentLines, 88, 76, 82, 54);
        AddLines(DetailsLines, 62, 80, 46);
        ShowWidth(SidebarPane, SidebarWidth);
        ShowWidth(ContentPane, ContentWidth);
        ShowWidth(DetailsPane, DetailsWidth);
    }

    // Arc fills the width label in the browser, so its exact wording is not in the page markup; "N px" is this page's reading of it.
    private static void ShowWidth(FrameworkElement pane, TextBlock label) => pane.SizeChanged += (_, e) => label.Text = $"{Math.Round(e.NewSize.Width)} px";

    private static void AddLines(Panel host, params int[] percents)
    {
        for (int index = 0; index < percents.Length; index++)
        {
            var row = new Grid { Margin = new Thickness(0, index == 0 ? 0 : 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(percents[index], GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - percents[index], GridUnitType.Star) });
            var line = new Border { Height = 8, CornerRadius = new CornerRadius(4), Opacity = .18 };
            line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.TextMuted");
            row.Children.Add(line);
            host.Children.Add(row);
        }
    }
}
