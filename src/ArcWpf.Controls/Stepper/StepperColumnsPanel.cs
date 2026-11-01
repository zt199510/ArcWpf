using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

/// <summary>Horizontal step columns (stepper.tsx: repeat(n - 1, minmax(0, 1fr)) auto): the last step takes its own width, so it ends flush with the edge, and the rest share what is left.</summary>
public sealed class StepperColumnsPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        int count = InternalChildren.Count;
        if (count == 0) return new Size();
        var last = InternalChildren[count - 1];
        last.Measure(availableSize);
        double share = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Share(availableSize.Width, last.DesiredSize.Width, count);
        double width = last.DesiredSize.Width, height = last.DesiredSize.Height;
        for (int index = 0; index < count - 1; index++)
        {
            InternalChildren[index].Measure(new Size(share, availableSize.Height));
            width += InternalChildren[index].DesiredSize.Width;
            height = Math.Max(height, InternalChildren[index].DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = InternalChildren.Count;
        if (count == 0) return finalSize;
        double lastWidth = Math.Min(InternalChildren[count - 1].DesiredSize.Width, finalSize.Width), share = Share(finalSize.Width, lastWidth, count), x = 0;
        for (int index = 0; index < count - 1; index++, x += share) InternalChildren[index].Arrange(new Rect(x, 0, share, finalSize.Height));
        InternalChildren[count - 1].Arrange(new Rect(x, 0, lastWidth, finalSize.Height));
        return finalSize;
    }

    private static double Share(double width, double last, int count) => count < 2 ? 0 : Math.Max(0, width - last) / (count - 1);
}
