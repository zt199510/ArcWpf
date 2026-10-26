using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

/// <summary>Wrapping pagination layout with a four-DIP gap and no trailing spacing.</summary>
public sealed class PaginationPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren) child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Layout(availableSize.Width, false);
    }

    protected override Size ArrangeOverride(Size finalSize) { Layout(finalSize.Width, true); return finalSize; }

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, used = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > width) { y += rowHeight + 4; x = 0; rowHeight = 0; }
            if (arrange) child.Arrange(new Rect(new Point(x, y), size));
            used = Math.Max(used, x + size.Width);
            rowHeight = Math.Max(rowHeight, size.Height);
            x += size.Width + 4;
        }
        return new Size(used, y + rowHeight);
    }
}
