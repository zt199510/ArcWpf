using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace ArcWpf.Gallery.Pages;
public partial class ExpandableTextPage : UserControl { public ExpandableTextPage() { InitializeComponent(); } }

/// <summary>The review rating from the Arc demo: five 13 DIP Lucide stars, one apart, filled up to the rating.</summary>
public sealed class ReviewStars : StackPanel
{
    private static readonly Geometry Star = Geometry.Parse("M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z");

    public int Rating
    {
        set
        {
            Children.Clear();
            for (int i = 0; i < 5; i++)
            {
                var path = new Path { Data = Star, StrokeThickness = 1.75, StrokeLineJoin = PenLineJoin.Round, Width = 24, Height = 24 };
                path.SetResourceReference(Shape.StrokeProperty, i < value ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted");
                if (i < value) path.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Foreground");
                Children.Add(new Viewbox { Width = 13, Height = 13, Margin = new Thickness(i == 0 ? 0 : 1, 0, 0, 0), Child = path });
            }
        }
    }

    public ReviewStars()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
    }
}
