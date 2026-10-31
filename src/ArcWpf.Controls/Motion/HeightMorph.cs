using System.Windows;

namespace ArcWpf.Controls;

// The registry HeightFrame (empty-state.tsx, skeleton.tsx): after a state change the frame pins its old height,
// springs (smooth) to the new content height, then returns to auto. It clips only while moving.
internal sealed class HeightMorph
{
    private const double SMOOTH = .4;
    private readonly FrameworkElement _frame;
    private readonly FrameworkElement _content;
    private readonly SpringDriver _driver;
    private readonly ControlSpring _height;

    public HeightMorph(FrameworkElement frame, FrameworkElement content)
    {
        _frame = frame;
        _content = content;
        _driver = new SpringDriver(() => _frame.Height = Math.Max(0, _height!.Position)) { Settled = Settle };
        _height = _driver.Add(0, SMOOTH);
    }

    /// <summary>Applies <paramref name="change"/>; with <paramref name="animate"/> the height springs from the old size to the new one.</summary>
    public void Morph(Action change, bool animate)
    {
        double from = _driver.IsRunning ? _height.Position : _frame.ActualHeight;
        change();
        if (!animate || from <= 0 || !_frame.IsLoaded) { Settle(); return; }
        _content.Measure(new Size(_frame.ActualWidth, double.PositiveInfinity));
        double to = _content.DesiredSize.Height;
        if (Math.Abs(to - from) < .5) { if (!_driver.IsRunning) Settle(); return; }
        if (!_driver.IsRunning) _height.Snap(from);
        _frame.Height = _height.Position;
        _frame.ClipToBounds = true;
        _driver.Animate(_height, to, SMOOTH);
    }

    public void Stop() { _driver.Stop(); Settle(); }

    private void Settle()
    {
        _driver.Stop();
        _frame.Height = double.NaN;
        _frame.ClipToBounds = false;
        Settled?.Invoke();
    }

    /// <summary>Runs when the frame returns to auto height.</summary>
    public Action? Settled { get; set; }
}
