using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

public partial class ChipGroupPage : UserControl
{
    private static readonly string[] TopicNames = ["Typography", "Color", "Motion", "Accessibility", "Layout", "Components", "Icons", "Research", "Prototyping", "Design tokens", "Illustration"];
    private sealed record Resource(string Title, string Author, string Kind, params string[] Topics);
    private static readonly Resource[] Resources =
    [
        new("Type scale for dense dashboards", "Emma Collins", "Guide", "Typography", "Layout"), new("Variable fonts in product UI", "Marcus Johnson", "Video", "Typography"),
        new("Choosing an accessible accent", "Ava Mitchell", "Guide", "Color", "Accessibility"), new("OKLCH palettes, explained", "Sofia Ramirez", "Guide", "Color", "Design tokens"),
        new("Spring motion cheat sheet", "Jasmine Brooks", "Template", "Motion"), new("Reduced motion patterns", "Ava Mitchell", "Guide", "Motion", "Accessibility"),
        new("Focus states that survive dark mode", "Emma Collins", "Kit", "Accessibility", "Components"), new("Twelve-column grid for marketing pages", "Marcus Johnson", "Template", "Layout"),
        new("Icon stroke and optical size", "Sofia Ramirez", "Guide", "Icons"), new("Command menu anatomy", "Jasmine Brooks", "Kit", "Components"),
        new("Interviewing power users", "Ava Mitchell", "Guide", "Research"), new("Prototyping gestures in Framer", "Marcus Johnson", "Video", "Prototyping", "Motion"),
        new("Token naming conventions", "Sofia Ramirez", "Guide", "Design tokens"), new("Spot illustrations in two weights", "Emma Collins", "Kit", "Illustration", "Icons"),
        new("Empty states that teach", "Jasmine Brooks", "Guide", "Components", "Illustration"), new("Diary studies on a budget", "Ava Mitchell", "Template", "Research"),
        new("Line length and reading comfort", "Emma Collins", "Guide", "Typography", "Accessibility"), new("Dark mode surface ramps", "Sofia Ramirez", "Kit", "Color"),
        new("Responsive tables", "Marcus Johnson", "Guide", "Layout", "Components"), new("Clickable prototype checklist", "Jasmine Brooks", "Template", "Prototyping"),
        new("Scroll-linked motion without jank", "Marcus Johnson", "Video", "Motion"), new("Contrast audit sheet", "Ava Mitchell", "Template", "Accessibility", "Color"),
        new("Icon grid templates", "Sofia Ramirez", "Template", "Icons"), new("Usability test script", "Emma Collins", "Template", "Research", "Prototyping"),
    ];
    private readonly HashSet<string> _shown = [];

    public ChipGroupPage()
    {
        InitializeComponent();
        Topics.ItemsSource = TopicNames.Select(name => new ChipOption(name)).ToList();
        Topics.SelectedValues = ["Motion"];
        Render(false);
    }

    private void TopicsChanged(object sender, RoutedEventArgs e) => Render(true);
    private void ClearClick(object sender, RoutedEventArgs e) { Topics.SelectedValues = []; Render(true); }

    private void MatchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Topics is null || Clear is null) return;
        Topics.Multiple = Match.SelectedIndex == 0;
        if (!Topics.Multiple) Topics.SelectedValues = (Topics.SelectedValues ?? []).Take(1).ToList();
        Render(true);
    }

    private void Render(bool animate)
    {
        var picked = Topics.SelectedValues ?? [];
        Clear.IsEnabled = picked.Count > 0;
        var matches = Resources.Where(r => r.Topics.Any(picked.Contains)).ToList();
        double from = List.ActualHeight;
        List.Children.Clear();
        var now = new HashSet<string>();
        if (matches.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(16, 24, 16, 24) };
            empty.Children.Add(new TextBlock { Text = "No topic selected", FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center });
            var hint = new TextBlock { Text = "Pick a topic to see matching resources.", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            empty.Children.Add(hint);
            List.Children.Add(empty);
        }
        foreach (var resource in matches.Take(3))
        {
            var row = Row(new TextBlock { Text = resource.Title, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis }, $"{resource.Author} · {resource.Kind}");
            List.Children.Add(row);
            now.Add(resource.Title);
            if (animate && !_shown.Contains(resource.Title)) Rise(row);
        }
        if (matches.Count > 3)
        {
            var rest = new TextBlock { Text = $"+{matches.Count - 3} more", Margin = new Thickness(16, 10, 16, 10) };
            rest.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            List.Children.Add(Separated(rest));
        }
        _shown.Clear();
        _shown.UnionWith(now);
        if (!animate || from <= 0) return;
        List.Measure(new Size(ListFrame.ActualWidth, double.PositiveInfinity));
        double to = List.DesiredSize.Height;
        ListFrame.BeginAnimation(HeightProperty, new DoubleAnimation(from, to, TimeSpan.FromSeconds(.8)) { EasingFunction = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }

    private FrameworkElement Row(TextBlock title, string meta)
    {
        var row = new DockPanel { MinHeight = 44, Margin = new Thickness(16, 8, 16, 8) };
        var tail = new TextBlock { Text = meta, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        tail.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        DockPanel.SetDock(tail, Dock.Right);
        row.Children.Add(tail);
        title.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(title);
        return Separated(row);
    }

    // .row + .row::before: a hairline inset 16 on both sides.
    private FrameworkElement Separated(FrameworkElement content)
    {
        var host = new Grid();
        if (List.Children.Count > 0)
        {
            var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 16, 0) };
            rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
            host.Children.Add(rule);
        }
        host.Children.Add(content);
        return host;
    }

    // Rows enter from y 6 with a subtle blur over .24 (ease-enter).
    private static void Rise(FrameworkElement row)
    {
        var move = new TranslateTransform(0, 6);
        row.RenderTransform = move;
        row.Opacity = 0;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        row.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(.24)) { EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.24)) { EasingFunction = ease });
    }
}
