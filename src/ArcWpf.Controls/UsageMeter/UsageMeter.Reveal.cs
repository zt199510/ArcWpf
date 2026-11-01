using System.Windows;
using System.Windows.Media;

namespace ArcWpf.Controls;

public partial class UsageMeter
{
    private bool _revealed;
    private bool _revealing;
    private bool _trackingReveal;

    private void StartRevealTracking()
    {
        if (_revealed || !IsLoaded) return;
        if (Segments.Count == 0 || ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _revealed = true;
            StopRevealTracking();
            UpdateTrack(false);
            RevealNumbers();
            return;
        }
        if (!_trackingReveal)
        {
            LayoutUpdated += CheckReveal;
            _trackingReveal = true;
        }
        CheckReveal(this, EventArgs.Empty);
    }
    private void StopRevealTracking()
    {
        if (!_trackingReveal) return;
        LayoutUpdated -= CheckReveal;
        _trackingReveal = false;
    }
    private void CheckReveal(object? sender, EventArgs e)
    {
        if (_revealed || Segments.Count == 0 || !IsLoaded || !IsVisible || VisibleFraction() < .4) return;
        _revealed = true;
        StopRevealTracking();
        _revealing = true;
        UpdateTrack(true);
        _revealing = false;
        RevealNumbers();
    }
    private void RevealNumbers()
    {
        _ticker?.Reveal();
        foreach (var entry in _legendEntries) entry.Ticker.Reveal();
    }
    private double VisibleFraction()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || Window.GetWindow(this) is null) return 0;
        Rect visible = new(new Point(), RenderSize);
        for (DependencyObject? node = VisualTreeHelper.GetParent(this); node is not null; node = ParentOf(node))
        {
            if (node is UIElement element && (element.ClipToBounds || element.Clip is not null || element is Window))
            {
                Rect clip = element.Clip?.Bounds ?? new Rect(new Point(), element.RenderSize);
                try { visible.Intersect(element.TransformToVisual(this).TransformBounds(clip)); }
                catch (InvalidOperationException) { return 0; }
                if (visible.IsEmpty || visible.Width <= 0 || visible.Height <= 0) return 0;
            }
            if (node is Window) break;
        }
        return visible.Width * visible.Height / (ActualWidth * ActualHeight);
    }
    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
