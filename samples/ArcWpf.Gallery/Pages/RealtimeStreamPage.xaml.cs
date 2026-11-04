using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class RealtimeStreamPage : UserControl
{
    private readonly Random _random = new(11);
    private readonly ObservableCollection<StreamPoint> _points = [];
    private readonly DispatcherTimer _feed = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(500) };
    private double _level = 138;
    private int _sinceSpike, _spike;

    public RealtimeStreamPage()
    {
        InitializeComponent();
        var start = DateTime.Now - TimeSpan.FromSeconds(45);
        for (int index = 0; index < 90; index++) { _points.Add(Reading(start + TimeSpan.FromMilliseconds(500 * index))); }
        Chart.Points = _points;
        _feed.Tick += (_, _) =>
        {
            _points.Add(Reading(DateTime.Now));
            // A few windows is enough; the chart draws only what it can show.
            while (_points.Count > 240) { _points.RemoveAt(0); }
        };
        Loaded += (_, _) => _feed.Start();
        Unloaded += (_, _) => _feed.Stop();
    }

    private StreamPoint Reading(DateTime time)
    {
        _level += (138 - _level) * .2 + (_random.NextDouble() - .5) * 8;
        // An incident lasts a few readings and falls away, and each reading over 300 is flagged on the data.
        if (_spike > 0)
        {
            double value = Math.Round(430 - (4 - _spike--) * 45 + (_random.NextDouble() - .5) * 8, 1);
            return new StreamPoint(time, value, value > 300 ? "Spike" : null);
        }
        if (++_sinceSpike > 28 && _random.NextDouble() < .05) { _sinceSpike = 0; _spike = 3; }
        return new StreamPoint(time, Math.Round(_level, 1));
    }
}
