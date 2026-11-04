using System.Globalization;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ActivityHeatmapPage : UserControl
{
    private static readonly string[] Repos = ["atlas-web", "design-tokens", "billing-api"];

    public ActivityHeatmapPage()
    {
        InitializeComponent();
        Heat.Thresholds = [3, 6, 10];
        Show("2025");
        Heat.SelectedDate = new DateOnly(2025, 3, 18);
        Describe(new DateOnly(2025, 3, 18));
    }

    private void OnRange(object sender, SelectionChangedEventArgs e) { if (IsInitialized && Range.SelectedValue is string year) { Show(year); } }
    private void OnDate(object? sender, ActivityDateEventArgs e) => Describe(e.Date);

    private void Show(string year)
    {
        Heat.Days = ChartDemoData.Days(year);
        Heat.Period = year;
    }

    private void Describe(DateOnly date)
    {
        int count = Heat.Days?.FirstOrDefault(day => day.Date == date).Count ?? 0;
        string day = date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture);
        if (count == 0) { Detail.Text = $"{day}: no contributions."; return; }
        int reviews = count / 4, pulls = (count + 2) / 4, commits = count - reviews - pulls;
        string Part(int amount, string one, string many) => $"{amount} {(amount == 1 ? one : many)}";
        var parts = new List<string> { Part(commits, "commit", "commits") };
        if (pulls > 0) { parts.Add(Part(pulls, "pull request", "pull requests")); }
        if (reviews > 0) { parts.Add(Part(reviews, "review", "reviews")); }
        string list = parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}";
        Detail.Text = $"{day}: {list}, mostly in {Repos[date.DayNumber % Repos.Length]}.";
    }
}
