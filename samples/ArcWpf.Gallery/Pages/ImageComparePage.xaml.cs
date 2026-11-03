using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ImageComparePage : UserControl
{
    public ImageComparePage()
    {
        InitializeComponent();
        Compare.Before = Room(false);
        Compare.After = Room(true);
    }

    private void OnCenter(object sender, RoutedEventArgs e) => Compare.Position = 50;
    private void OnMode(object sender, SelectionChangedEventArgs e) { if (Compare is not null) { Compare.Orientation = Mode.SelectedValue as string == "v" ? Orientation.Vertical : Orientation.Horizontal; } }

    // An original drawing of a room, flat and cool before the grade, warm and contrasty after it.
    private static FrameworkElement Room(bool graded)
    {
        Brush Fill(string hex) { var c = (Color)ColorConverter.ConvertFromString(hex); return new SolidColorBrush(c); }
        var canvas = new Canvas { Width = 600, Height = 400, ClipToBounds = true };
        void Put(Shape shape, double x, double y, Brush brush) { shape.Fill = brush; Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y); canvas.Children.Add(shape); }
        var wall = graded ? new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#F3DCC0"), (Color)ColorConverter.ConvertFromString("#C98F5F"), 20) : new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#C9CDD0"), (Color)ColorConverter.ConvertFromString("#A9ADB0"), 20);
        Put(new Rectangle { Width = 600, Height = 290 }, 0, 0, wall);
        Put(new Rectangle { Width = 600, Height = 110 }, 0, 290, Fill(graded ? "#6E4427" : "#7C7770"));
        Put(new Rectangle { Width = 170, Height = 190 }, 60, 40, Fill(graded ? "#FFF3D6" : "#E2E6E8"));
        Put(new Rectangle { Width = 4, Height = 190 }, 143, 40, Fill(graded ? "#8B5A32" : "#8F9193"));
        Put(new Rectangle { Width = 170, Height = 4 }, 60, 133, Fill(graded ? "#8B5A32" : "#8F9193"));
        Put(new Rectangle { Width = 290, Height = 86, RadiusX = 22, RadiusY = 22 }, 250, 232, Fill(graded ? "#2F5D62" : "#6F7F82"));
        Put(new Rectangle { Width = 86, Height = 62, RadiusX = 14, RadiusY = 14 }, 268, 200, Fill(graded ? "#E0A458" : "#A5A199"));
        Put(new Rectangle { Width = 86, Height = 62, RadiusX = 14, RadiusY = 14 }, 436, 200, Fill(graded ? "#C8553D" : "#99908A"));
        Put(new Rectangle { Width = 150, Height = 16, RadiusX = 6, RadiusY = 6 }, 120, 330, Fill(graded ? "#3B2415" : "#5F5B56"));
        Put(new Ellipse { Width = 84, Height = 84 }, 470, 56, Fill(graded ? "#F7C873" : "#CFCFCB"));
        Put(new Rectangle { Width = 5, Height = 170 }, 510, 130, Fill(graded ? "#3B2415" : "#6A6764"));
        return new Viewbox { Stretch = Stretch.UniformToFill, Child = canvas };
    }
}
