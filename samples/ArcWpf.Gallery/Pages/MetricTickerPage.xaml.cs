using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class MetricTickerPage : UserControl
{
    private readonly Random _random = new(7);
    private readonly DispatcherTimer _feed = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly List<TickerMetric> _metrics =
    [
        new() { Id = "mrr", Label = "Monthly recurring revenue", Value = 84060, Format = "C0" },
        new() { Id = "active", Label = "Active now", Value = 1270 },
        new() { Id = "trial", Label = "Trial conversion", Value = .182, Format = "P1", Delta = TickerDelta.Absolute },
        new() { Id = "p95", Label = "API p95", Value = 217, Unit = " ms", Positive = TickerPositive.Down, Delta = TickerDelta.Absolute, Caption = "p95 across regions" },
        new() { Id = "orders", Label = "Orders per minute", Value = 330 },
        new() { Id = "errors", Label = "Error rate", Value = .0032, Format = "P2", Positive = TickerPositive.Down, Delta = TickerDelta.Absolute },
        new() { Id = "churn", Label = "Logo churn", Value = .0179, Format = "P2", Positive = TickerPositive.Down, Delta = TickerDelta.Absolute },
    ];
    private bool _streaming = true;

    public MetricTickerPage()
    {
        InitializeComponent();
        Ticker.Metrics = _metrics;
        _feed.Tick += (_, _) => Tick();
        Loaded += (_, _) => { if (_streaming) { _feed.Start(); } };
        Unloaded += (_, _) => _feed.Stop();
    }

    private void Tick()
    {
        // Two or three readings move each beat; the rest hold, so the changes read as events.
        var next = _metrics.Select(metric => new TickerMetric { Id = metric.Id, Label = metric.Label, Value = metric.Value, Format = metric.Format, Unit = metric.Unit, Delta = metric.Delta, Positive = metric.Positive, Caption = metric.Caption }).ToList();
        for (int moved = 0; moved < 2 + _random.Next(2); moved++)
        {
            var metric = next[_random.Next(next.Count)];
            double step = metric.Id switch { "mrr" => 240, "active" => 18, "trial" => .002, "p95" => 9, "orders" => 12, "errors" => .0004, _ => .0006 };
            metric.Value = Math.Max(0, metric.Value + step * (_random.NextDouble() * 2 - 1));
            if (metric.Id is "mrr" or "active" or "p95" or "orders") { metric.Value = Math.Round(metric.Value); }
        }
        _metrics.Clear();
        _metrics.AddRange(next);
        Ticker.Metrics = [.. next];
    }

    private void OnStream(object sender, RoutedEventArgs e)
    {
        _streaming = !_streaming;
        StreamText.Text = _streaming ? "Pause stream" : "Resume stream";
        StreamIcon.Icon = _streaming ? "pause" : "play";
        if (_streaming) { _feed.Start(); } else { _feed.Stop(); }
    }
}
