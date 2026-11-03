using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CoverFlowPage : UserControl
{
    private static readonly (string Title, string Place, double Miles, double Gain, string Light, string Sky, string Glow, string Far, string Near)[] Hikes =
    [
        ("Yosemite Valley loop", "Yosemite, California", 7.2, 400, "Late afternoon", "#C9DDE6", "#F2D9B8", "#A9A59B", "#3E5C33"),
        ("Lake Agnes Tea House", "Banff, Alberta", 4.8, 1320, "Morning", "#B9CBD3", "#E3E9E8", "#7E96A0", "#245E63"),
        ("Lago Limides", "Cortina d'Ampezzo, Italy", 2.6, 750, "Sunset", "#34587E", "#E8B98C", "#B5674A", "#3F4F35"),
        ("Seceda ridgeline", "Val Gardena, Italy", 6.1, 1150, "Morning", "#D5DEE2", "#F1EDE2", "#8C8F8C", "#6D8A2E"),
        ("Fjadrargljufur rim", "South Iceland", 3.4, 300, "Overcast", "#B4BDB7", "#D9DBD2", "#5C6E55", "#2F4A2B"),
        ("Cape Nelson lighthouse", "Victoria, Australia", 5.3, 220, "Blue hour", "#17335E", "#E79B62", "#1F3A52", "#14222B"),
        ("Ocean Beach to Lands End", "San Francisco, California", 3.4, 550, "Sunset", "#B9CFC6", "#E9DFB6", "#5F7A72", "#2A3431"),
    ];

    public CoverFlowPage()
    {
        InitializeComponent();
        var items = new CoverFlowItem[Hikes.Length];
        for (int i = 0; i < Hikes.Length; i++) { items[i] = new CoverFlowItem { Title = Hikes[i].Title, Subtitle = Hikes[i].Place, Image = Scene(i) }; }
        Flow.ItemsSource = items;
        Show(Flow.SelectedIndex);
    }

    private void OnHike(object? sender, int index) { if (Miles is not null) { Show(index); } }

    private void Show(int index)
    {
        Miles.Value = Hikes[index].Miles;
        Gain.Value = Hikes[index].Gain;
        Light.Text = Hikes[index].Light;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        bool on = Save.IsChecked == true;
        SaveText.Text = on ? "Saved" : "Save hike";
        SaveIcon.Icon = on ? "bookmark-check" : "bookmark";
        SaveText.SetResourceReference(TextBlock.ForegroundProperty, on ? "ArcWpf.Brush.Background" : "ArcWpf.Brush.Foreground");
        SaveIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, on ? "ArcWpf.Brush.Background" : "ArcWpf.Brush.Foreground");
    }

    // An original painted landscape: sky, a glow, two jagged ridges and a lake, generated from the hike's palette.
    private static ImageSource Scene(int seed)
    {
        const int width = 480, height = 600;
        var hike = Hikes[seed];
        Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        var random = new Random(seed * 37 + 11);
        Geometry Ridge(double top, double rough)
        {
            var figure = new PathFigure { StartPoint = new Point(0, height), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(0, top + random.NextDouble() * rough), true));
            for (double x = 40; x <= width; x += 40) { figure.Segments.Add(new LineSegment(new Point(x, top + random.NextDouble() * rough), true)); }
            figure.Segments.Add(new LineSegment(new Point(width, height), true));
            return new PathGeometry([figure]);
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(C(hike.Sky), C(hike.Glow), 90), null, new Rect(0, 0, width, height));
            dc.DrawGeometry(new SolidColorBrush(C(hike.Far)), null, Ridge(200, 130));
            var near = C(hike.Near);
            dc.DrawGeometry(new LinearGradientBrush(near, Color.Multiply(near, .7f), 90), null, Ridge(350, 70));
            dc.DrawRectangle(new LinearGradientBrush(Color.Multiply(C(hike.Sky), .75f), Color.Multiply(near, .55f), 90), null, new Rect(0, 480, width, 120));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
