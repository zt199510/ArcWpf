using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ReactionsPage : UserControl
{
    public ReactionsPage()
    {
        InitializeComponent();
        foreach (var (id, label, icon) in new[] { ("like", "Like", "thumbs-up"), ("love", "Love", "heart"), ("celebrate", "Celebrate", "party-popper"), ("insightful", "Insightful", "lightbulb"), ("curious", "Curious", "message-circle-question"), ("ship", "Ship it", "rocket") })
        {
            Main.Options.Add(new ReactionOption { Id = id, Label = label, Icon = icon });
        }
        Main.Values.Add(new Reaction { Id = "celebrate", Count = 12 });
        Main.Values.Add(new Reaction { Id = "like", Count = 9 });
        Main.Values.Add(new Reaction { Id = "insightful", Count = 4, Reacted = true });
        Main.Values.Add(new Reaction { Id = "curious", Count = 1 });
        UpdateSummary();
    }

    private void OnChanged(object? sender, ReactionChangedEventArgs e)
    {
        UpdateSummary();
        var rise = new TranslateTransform(0, 4);
        Summary.RenderTransform = rise;
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.24)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        Summary.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.24)));
    }

    private void UpdateSummary()
    {
        int total = Main.Values.Sum(reaction => reaction.Count);
        var mine = Main.Values.Where(reaction => reaction.Reacted).Select(reaction => Main.Options.First(option => option.Id == reaction.Id).Label).ToList();
        string noun = total == 1 ? "reaction" : "reactions";
        Summary.Text = mine.Count == 0 ? $"{total} {noun}" : $"{total} {noun} · You reacted with {string.Join(" and ", mine)}";
    }
}
