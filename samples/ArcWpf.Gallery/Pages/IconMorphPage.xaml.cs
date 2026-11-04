using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcWpf.Controls;
using Button = System.Windows.Controls.Button;

namespace ArcWpf.Gallery.Pages;

public partial class IconMorphPage : UserControl
{
    private sealed record Spec(IconMorphName Icon, string Off, string On, string Name, bool Initial = false);

    private static readonly ControlTemplate TileTemplate = (ControlTemplate)XamlReader.Parse(
        """
        <ControlTemplate TargetType="Button" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Border CornerRadius="26" BorderThickness="1" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"><ContentPresenter/></Border>
        </ControlTemplate>
        """);

    private readonly DispatcherTimer _copied = new() { Interval = TimeSpan.FromSeconds(1.8) };

    public IconMorphPage()
    {
        InitializeComponent();
        Spec[] specs =
        [
            new(IconMorphName.Menu, "Menu", "Menu open", "Open menu"),
            new(IconMorphName.Play, "Paused", "Playing", "Play"),
            new(IconMorphName.Plus, "Add to list", "Added", "Add to list"),
            new(IconMorphName.Copy, "Copy command", "Copied", "Copy install command"),
            new(IconMorphName.Theme, "Light", "Dark", "Switch tile to dark"),
            new(IconMorphName.Chevron, "Show details", "Hide details", "Show details"),
            new(IconMorphName.Bell, "Alerts off", "Alerts on", "Turn alerts off", true),
            new(IconMorphName.Heart, "128 likes", "129 likes", "Like"),
        ];
        foreach (var spec in specs) { Stage.Children.Add(Tile(spec)); }
    }

    private Button Tile(Spec spec)
    {
        var icon = new IconMorph { Icon = spec.Icon, Size = 28, State = spec.Initial, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 32.8, 0, 0) };
        var label = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = spec.Initial ? spec.On : spec.Off };
        var tile = new Button { Template = TileTemplate, Width = 124.4, Height = 124.4, Margin = new Thickness(0, 0, 8, 8), HorizontalAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand, Content = new Grid { Children = { icon, new Grid { Height = 16.8, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 72.8, 0, 0), Children = { label } } } } };
        AutomationProperties.SetName(tile, spec.Name);
        void Paint()
        {
            bool on = icon.State, theme = spec.Icon == IconMorphName.Theme, copy = spec.Icon == IconMorphName.Copy, plus = spec.Icon == IconMorphName.Plus;
            label.Text = on ? spec.On : spec.Off;
            string surface = on && theme ? "Foreground" : tile.IsMouseOver ? "SurfaceMuted" : "Surface";
            string edge = on ? (theme ? "Foreground" : copy ? "Border" : "BorderStrong") : "Border";
            string ink = !on ? "TextSecondary" : theme ? "Background" : copy || plus ? "ArcSuccess" : "Foreground";
            tile.SetResourceReference(BackgroundProperty, "ArcWpf.Brush." + surface);
            tile.SetResourceReference(BorderBrushProperty, "ArcWpf.Brush." + edge);
            icon.SetResourceReference(IconMorph.ForegroundProperty, "ArcWpf.Brush." + ink);
            label.SetResourceReference(TextBlock.ForegroundProperty, on && theme ? "ArcWpf.Brush.Background" : "ArcWpf.Brush.TextSecondary");
        }
        tile.MouseEnter += (_, _) => Paint();
        tile.MouseLeave += (_, _) => Paint();
        tile.Click += (_, _) =>
        {
            icon.State = !icon.State;
            Paint();
            if (spec.Icon != IconMorphName.Copy) { return; }
            // The check confirms the copy, then the icon returns by itself.
            _copied.Stop();
            _copied.Tick -= Reset;
            _copied.Tick += Reset;
            if (icon.State) { _copied.Start(); }
        };
        void Reset(object? sender, EventArgs e) { _copied.Stop(); icon.State = false; Paint(); }
        Paint();
        return tile;
    }
}
