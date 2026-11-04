using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ShareAccessPage : UserControl
{
    public ShareAccessPage()
    {
        InitializeComponent();
        Panel.Members = new List<ShareAccessMember>
        {
            new() { Id = "jasmine", Name = "Jasmine Brooks", Email = "jasmine.brooks@fieldwork.example", Role = ShareRole.Owner, Source = Portrait("#E8A87C", "#C38D9E") },
            new() { Id = "nathan", Name = "Nathan Cole", Email = "nathan.cole@fieldwork.example", Role = ShareRole.Editor, Source = Portrait("#41B3A3", "#2C7A7B") },
            new() { Id = "chloe", Name = "Chloe Nguyen", Email = "chloe.nguyen@fieldwork.example", Role = ShareRole.Commenter, Source = Portrait("#B39DDB", "#5E60CE") },
            new() { Id = "daniel", Name = "Daniel Kim", Email = "daniel.kim@fieldwork.example", Role = ShareRole.Viewer, Source = Portrait("#F6BD60", "#D08C60") },
        };
        Panel.Directory = new List<ShareAccessContact>
        {
            new() { Name = "Marcus Johnson", Email = "marcus.johnson@fieldwork.example", Source = Portrait("#84A98C", "#52796F") },
            new() { Name = "Ava Mitchell", Email = "ava.mitchell@fieldwork.example", Source = Portrait("#F28482", "#B5838D") },
            new() { Name = "Mateo Alvarez", Email = "mateo.alvarez@fieldwork.example", Source = Portrait("#90BEDE", "#4A6FA5") },
        };
        // Stands in for the request a real app would make.
        Panel.InviteHandler = async (_, _) => await Task.Delay(900);
        Panel.Done += (_, _) => Panel.Visibility = Visibility.Hidden;
    }

    private void OnToggle(object sender, RoutedEventArgs e) => Panel.Visibility = Panel.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;

    // Code-drawn gradients, so the sample ships no portraits.
    private static ImageSource Portrait(string from, string to)
    {
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 45);
        var image = new DrawingImage(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, 0, 64, 64))));
        image.Freeze();
        return image;
    }
}
