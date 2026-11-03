using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TimelinePage : UserControl
{
    private const string Check = "M20,6 L9,17 L4,12", Cross = "M18,6 L6,18 M6,6 L18,18";
    private static readonly DateTime Now = new(2026, 9, 21, 14, 32, 0, DateTimeKind.Utc);
    // The live demo's portraits are its own photographs, so these are neutral gradient placeholders.
    private static readonly ImageSource Emma = Portrait("#E9A15B", "#7A5CE0"), Marcus = Portrait("#F0628C", "#35C7A0"), Ava = Portrait("#57B4F2", "#E3C24A"), Sofia = Portrait("#C86BD9", "#F2A65A"), Jasmine = Portrait("#3FB6A8", "#4C6FE0");
    private static readonly (string Actor, string Title, string Meta, string Detail)[] Fresh =
    [
        ("Marcus Johnson", "approved Saved cards", "Pull request #483", "Two small copy notes left inline. Good to merge once the flag name is final."),
        ("Ava Mitchell", "commented on Saved cards", "Design review", "The empty state reads well. Can we shorten the second line?"),
        ("Marcus Johnson", "merged Saved cards into main", "Pull request #483", "Ships behind the new checkout flag."),
    ];
    private int _added;
    public TimelinePage()
    {
        InitializeComponent();
        Feed.Now = Now;
        Feed.TimeZoneId = "UTC";
        Item("merged", Now.AddMinutes(-11), "Emma Collins", "merged Checkout redesign into main", "Pull request #482", Stats("Reviewed by Marcus Johnson and Ava Mitchell. Ships behind the new checkout flag."), Emma);
        Item("deploy", Now.AddHours(-1), "", "Staging deploy passed", "Build 1,284 · 4m 12s", "All 342 tests passed. The preview is live on staging for design review.", null, Check, "success");
        Item("comment", Now.AddHours(-2), "Ava Mitchell", "commented on Billing settings", "Design review", "Can we keep the old plan names for one release? Support needs a week to update the help articles.", Ava);
        Item("moved", Now.AddHours(-5), "Marcus Johnson", "moved Apple Pay support to In review", "Sprint 38", "Assigned to Jasmine Brooks for review. Due Thursday, September 24.", Marcus);
        Item("notes", new DateTime(2026, 9, 18, 17, 40, 0, DateTimeKind.Utc), "Sofia Ramirez", "updated the release notes", "v2.8 draft", "Added the new payment methods and a note that saved cards now live in the wallet.", Sofia);
        Item("release", new DateTime(2026, 9, 18, 15, 12, 0, DateTimeKind.Utc), "Jasmine Brooks", "scheduled release v2.8", "Thursday, 10:00 to 11:00", "Rollout starts at 10 percent and widens to half of all accounts after an hour of clean error rates.", Jasmine);
        Item("failed", new DateTime(2026, 9, 18, 10, 3, 0, DateTimeKind.Utc), "", "Checkout tests failed on main", "Build 1,279 · 2 failing", "Card expiry validation and the Apple Pay sheet timed out. Marcus Johnson fixed both in #480.", null, Cross, "danger");
        Item("designs", new DateTime(2026, 9, 17, 16, 26, 0, DateTimeKind.Utc), "Emma Collins", "shared new checkout designs", "14 frames", "Covers saved cards, address autocomplete, and the error states for declined payments.", Emma);
        Item("brief", new DateTime(2026, 9, 17, 11, 10, 0, DateTimeKind.Utc), "Ava Mitchell", "created the project brief", "Q4 roadmap", "Goal: cut checkout drop-off from 23 percent to under 15 percent by the end of October.", Ava);
    }
    private void Item(string id, DateTime at, string actor, string title, string meta, object detail, ImageSource? avatar, string? icon = null, string tone = "neutral")
        => Feed.Events.Add(new TimelineEvent { Id = id, At = at, Actor = actor, Title = title, Meta = meta, Detail = detail, Avatar = avatar, Icon = icon, Tone = tone });
    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var next = Fresh[_added++];
        Item("fresh" + _added, Now.AddSeconds(-_added), next.Actor, next.Title, next.Meta, next.Detail, next.Actor.StartsWith("Marcus") ? Marcus : Ava);
        Add.IsEnabled = _added < Fresh.Length;
    }
    private static ImageSource Portrait(string from, string to)
    {
        var fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 45);
        var image = new DrawingImage(new GeometryDrawing(fill, null, new RectangleGeometry(new Rect(0, 0, 28, 28))));
        image.Freeze();
        return image;
    }
    private static FrameworkElement Stats(string text)
    {
        var root = new StackPanel();
        var copy = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        copy.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        root.Children.Add(copy);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var (name, value) in new[] { ("Files", "12"), ("Added", "+418"), ("Removed", "−96") })
        {
            var cell = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            var label = new TextBlock { Text = name, FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            var number = new TextBlock { Text = value, FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 1, 0, 0) };
            number.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
            cell.Children.Add(label);
            cell.Children.Add(number);
            row.Children.Add(cell);
        }
        root.Children.Add(row);
        return root;
    }
}
