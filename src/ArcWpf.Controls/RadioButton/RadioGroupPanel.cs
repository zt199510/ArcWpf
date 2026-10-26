using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Controls;

/// <summary>Default nonvirtualizing vertical layout for RadioGroup, with gaps only between rows.</summary>
public sealed class RadioGroupPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width=0,height=0;int count=0;
        foreach(UIElement child in InternalChildren)
        {
            if(child.Visibility==Visibility.Collapsed)continue;
            child.Measure(new Size(availableSize.Width,double.PositiveInfinity));
            width=Math.Max(width,child.DesiredSize.Width);height+=child.DesiredSize.Height;if(count++>0)height+=8;
        }
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double y=0;int count=0;
        foreach(UIElement child in InternalChildren)
        {
            if(child.Visibility==Visibility.Collapsed)continue;
            if(count++>0)y+=8;
            child.Arrange(new Rect(0,y,finalSize.Width,child.DesiredSize.Height));y+=child.DesiredSize.Height;
        }
        return finalSize;
    }
}
