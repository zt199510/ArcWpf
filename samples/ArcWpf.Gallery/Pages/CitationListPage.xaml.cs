using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class CitationListPage : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(320) };
    private int _count;

    public CitationListPage()
    {
        InitializeComponent();
        Answer.Inlines.Add(new Run("Stripe signs the exact bytes it sends, so the handler has to read the raw body before anything parses it"));
        Answer.Inlines.Add(Cite(1));
        Answer.Inlines.Add(new Run(". In a route handler that means "));
        Answer.Inlines.Add(new Run("request.text()"));
        Answer.Inlines.Add(Cite(2));
        Answer.Inlines.Add(new Run(", because a parsed body never matches the signature"));
        Answer.Inlines.Add(Cite(3));
        Answer.Inlines.Add(new Run("."));
        List.Sources = AiDemoData.Sources;
        List.Total = AiDemoData.Sources.Length;
        List.DurationMs = 2200;
        List.Status = CitationStatus.Done;
        _timer.Tick += (_, _) => Arrive();
        Unloaded += (_, _) => _timer.Stop();
    }

    private static InlineUIContainer Cite(int number) => new(new SourceCitation { Number = number, Source = AiDemoData.Sources[number - 1] }) { BaselineAlignment = BaselineAlignment.Center };

    private void Arrive()
    {
        _count++;
        List.Sources = AiDemoData.Sources[.._count];
        if (_count < AiDemoData.Sources.Length) { return; }
        _timer.Stop();
        List.Status = CitationStatus.Done;
    }

    private void OnSearch(object sender, RoutedEventArgs e)
    {
        _count = 0;
        List.Error = null;
        List.Sources = [];
        List.Status = CitationStatus.Searching;
        _timer.Start();
    }

    private void OnFail(object sender, RoutedEventArgs e) { _timer.Stop(); List.Error = "The search service did not answer"; List.Status = CitationStatus.Error; }
    private void OnRetry(object? sender, EventArgs e) => OnSearch(sender!, new RoutedEventArgs());
    private void OnSource(object? sender, CitationEventArgs e) => Log.Text = $"SourceClicked: {e.Number}, {e.Source.Domain}";
}
