using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TocProgressPage : UserControl
{
    // Sample copy written for this page.
    private static readonly (string Title, int Level, string[] Body)[] Sections =
    [
        ("Why cold starts mattered", 1, ["Checkout is the one request nobody retries patiently. A customer who waits on a blank button assumes the payment failed, and either pays twice or leaves.", "Most of our traffic is steady, yet every deploy and every scale-up replaced warm workers with cold ones. The slowest requests were the ones that ran first."]),
        ("Measuring the boot", 1, ["We could not fix what we had only felt, so the first week went on instrumentation instead of changes. Every worker began to report how long it took to serve its first request."]),
        ("Tracing every phase", 2, ["Each boot was split into named spans: runtime start, configuration, module loading, connection setup and the first handler call. The spans were sampled in production, because staging never had the same traffic shape."]),
        ("Where the time went", 2, ["The surprise was how little of it was our own logic. Connections and handlers barely registered; the runtime spent most of a boot finding and reading files."]),
        ("Module resolution", 3, ["A single import could probe a dozen paths on a network disk. Multiply that by several thousand modules and resolution alone cost more than a second."]),
        ("Loading lazily", 1, ["The tax and fraud clients were loaded at startup and used by a fraction of requests. Moving them behind a first-use boundary was a small patch with an immediate result."]),
        ("Splitting the bundle", 2, ["We then separated the payment path from the admin tools that had drifted into the same package. The checkout bundle shrank to about a third of its size."]),
        ("Warming the cache", 2, ["New workers now read a resolution map built at deploy time, so they skip the probing altogether. They also open a pool of connections before they accept traffic."]),
        ("Results", 1, ["The median cold start fell by 61 percent and the slowest percentile came down with it. Failed payments during deploys dropped enough that the alert was retuned.", "None of it needed new infrastructure; the savings came from doing less work at the moment it hurt most."]),
        ("What we would do differently", 1, ["We would measure first sooner. Two earlier optimisations were reverted once the traces showed they touched phases that hardly mattered.", "We would also add the boot budget to continuous integration from the beginning, so that a heavy import is caught in review and not in production."]),
    ];

    public TocProgressPage()
    {
        InitializeComponent();
        var title = Text("How we cut checkout cold starts by 61 percent", 28, 30.8, "ArcWpf.Brush.Foreground", FontWeights.Medium);
        title.MaxWidth = 330;
        title.HorizontalAlignment = HorizontalAlignment.Left;
        Article.Children.Add(title);
        var byline = Text("Maya Lindqvist, platform team · September 18, 2026", 12, 19.8, "ArcWpf.Brush.TextMuted", FontWeights.Normal);
        byline.Margin = new Thickness(0, 8, 0, 0);
        Article.Children.Add(byline);
        var rule = new Border { Height = 1, Margin = new Thickness(0, 24, 0, 8) };
        rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
        Article.Children.Add(rule);
        var items = new TocProgressItemCollection();
        foreach (var (name, level, body) in Sections)
        {
            var heading = level switch
            {
                1 => Text(name, 18, 19.8, "ArcWpf.Brush.Foreground", FontWeights.Medium),
                2 => Text(name, 16, 17.6, "ArcWpf.Brush.Foreground", FontWeights.Medium),
                _ => Text(name, 14, 15.4, "ArcWpf.Brush.TextSecondary", FontWeights.Medium),
            };
            heading.Margin = new Thickness(0, level == 1 ? 24 : 16, 0, 10);
            Article.Children.Add(heading);
            foreach (string paragraph in body)
            {
                var copy = Text(paragraph, 14, 23.1, "ArcWpf.Brush.TextSecondary", FontWeights.Normal);
                copy.Margin = new Thickness(0, 0, 0, 12);
                Article.Children.Add(copy);
            }
            items.Add(new TocProgressItem { Id = name, Label = name, Level = level, Target = heading });
        }
        Toc.ScrollContainer = Reader;
        Toc.Items = items;
    }

    private static TextBlock Text(string text, double size, double line, string ink, FontWeight weight)
    {
        var block = new TextBlock { Text = text, FontSize = size, LineHeight = line, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, ink);
        return block;
    }
}
