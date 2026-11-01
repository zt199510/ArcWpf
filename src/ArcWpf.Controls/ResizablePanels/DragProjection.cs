using System.Diagnostics;

namespace ArcWpf.Controls;

internal sealed class DragProjection
{
    private readonly List<(double Time, double Position)> _samples = [];
    private long _started;
    private double _position;

    internal void Start()
    {
        _started = Stopwatch.GetTimestamp();
        _position = 0;
        _samples.Clear();
        _samples.Add((0, 0));
    }

    internal void Add(double delta)
    {
        _position += delta;
        AddSample(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, _position);
    }

    internal double Project()
        => ProjectAt(Stopwatch.GetElapsedTime(_started).TotalMilliseconds);

    internal void AddSample(double milliseconds, double position)
    {
        _samples.Add((milliseconds, position));
        if (_samples.Count > 16) _samples.RemoveAt(0);
    }

    internal double ProjectAt(double milliseconds)
    {
        var recent = _samples.Where(sample => milliseconds - sample.Time <= 90).ToArray();
        if (recent.Length < 2 || milliseconds - recent[^1].Time > 50) return 0;
        double elapsed = recent[^1].Time - recent[0].Time;
        if (elapsed <= 0) return 0;
        double velocity = (recent[^1].Position - recent[0].Position) / (elapsed / 1000);
        return velocity / 1000 * .99 / (1 - .99);
    }
}
