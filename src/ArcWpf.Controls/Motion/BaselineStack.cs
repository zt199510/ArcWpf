using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

/// <summary>A horizontal row that lines its text up on one baseline, as CSS `align-items: baseline` does; other children center. The last child takes the remaining width so it can trim.</summary>
public sealed class BaselineStack : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double used = 0, height = 0, ascent = 0, descent = 0;
        var children = InternalChildren;
        for (int index = 0; index < children.Count; index++)
        {
            var child = children[index];
            bool last = index == children.Count - 1;
            child.Measure(new Size(last ? Math.Max(0, availableSize.Width - used) : double.PositiveInfinity, availableSize.Height));
            used += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            if (child is TextBlock text && child.Visibility != Visibility.Collapsed) { ascent = Math.Max(ascent, text.BaselineOffset + text.Margin.Top); descent = Math.Max(descent, text.DesiredSize.Height - text.BaselineOffset - text.Margin.Top); }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? used : Math.Min(used, availableSize.Width), Math.Max(height, ascent + descent));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double ascent = 0, descent = 0;
        foreach (UIElement child in InternalChildren)
            if (child is TextBlock text && child.Visibility != Visibility.Collapsed) { ascent = Math.Max(ascent, text.BaselineOffset + text.Margin.Top); descent = Math.Max(descent, text.DesiredSize.Height - text.BaselineOffset - text.Margin.Top); }
        double baseline = (finalSize.Height - (ascent + descent)) / 2 + ascent, x = 0;
        var children = InternalChildren;
        for (int index = 0; index < children.Count; index++)
        {
            var child = children[index];
            double width = index == children.Count - 1 ? Math.Max(child.DesiredSize.Width, 0) : child.DesiredSize.Width;
            width = Math.Min(width, Math.Max(0, finalSize.Width - x));
            double y = child is TextBlock text ? baseline - text.BaselineOffset - text.Margin.Top : (finalSize.Height - child.DesiredSize.Height) / 2;
            child.Arrange(new Rect(x, y, width, child.DesiredSize.Height));
            x += width;
        }
        return finalSize;
    }
}
