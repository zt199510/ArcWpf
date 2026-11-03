using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Gallery.Pages;

public partial class CarouselPage : UserControl
{
    private static readonly (string Name, string Detail, string Price, string From, string To)[] Slides =
    [
        ("Oak lounge chair", "Woven seat on a solid oak frame", "$1,450", "#D9D4CB", "#A99C86"),
        ("Opal table lamp", "Hand-blown glass shade, dimmable", "$640", "#F3E3B5", "#6B5A45"),
        ("Stonewashed linen throw", "Oat, 130 × 180 cm", "$180", "#E6E3DC", "#B8B2A6"),
        ("Ribbed glass carafe", "Mouth-blown, holds 1 liter", "$95", "#CFDAD9", "#8FA3A3"),
        ("Sea at dusk", "Giclée print on cotton rag, 50 × 50 cm", "$220", "#9DB0C4", "#3E4C63"),
    ];

    public CarouselPage()
    {
        InitializeComponent();
        foreach (var slide in Slides) { Stage.Items.Add(Build(slide)); }
        Stage.SelectedIndex = 1;
    }

    private static FrameworkElement Build((string Name, string Detail, string Price, string From, string To) slide)
    {
        var photo = new Border
        {
            Height = 256,
            CornerRadius = new CornerRadius(26),
            Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(slide.From), (Color)ColorConverter.ConvertFromString(slide.To), 70),
        };
        var name = Text(slide.Name, 14, 15.4, "ArcWpf.Brush.Foreground", FontWeights.Medium);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var detail = Text(slide.Detail, 12, 16.8, "ArcWpf.Brush.TextMuted", FontWeights.Normal);
        detail.Margin = new Thickness(0, 3.2, 0, 0);
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        var words = new StackPanel { Margin = new Thickness(4, 2, 12, 0), Children = { name, detail } };
        var price = Text(slide.Price, 14, 15.4, "ArcWpf.Brush.Foreground", FontWeights.Normal);
        price.Margin = new Thickness(0, 2, 4, 0);
        var caption = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(price, Dock.Right);
        caption.Children.Add(price);
        caption.Children.Add(words);
        return new StackPanel { Children = { photo, caption } };
    }

    private static TextBlock Text(string text, double size, double line, string brush, FontWeight weight)
    {
        var block = new TextBlock { Text = text, FontSize = size, LineHeight = line, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = weight };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }
}
