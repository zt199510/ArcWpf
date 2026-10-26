using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace ArcWpf.Controls;
public class TabItem:System.Windows.Controls.TabItem
{
    private ContentPresenter? _label;
    private SolidColorBrush? _foreground;
    private readonly OwnedMotion _motion=new();
    static TabItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(TabItem),new FrameworkPropertyMetadata(typeof(TabItem)));
    public TabItem(){Loaded+=(_,_)=>UpdateColor(false);Unloaded+=(_,_)=>UpdateColor(false);}
    public override void OnApplyTemplate(){_motion.Stop();base.OnApplyTemplate();_label=GetTemplateChild("PART_Label") as ContentPresenter;_foreground=null;UpdateColor(false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();_label=null;_foreground=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(_motion is not null&&(e.Property==ForegroundProperty||e.Property==ArcMotion.LevelProperty||e.Property==IsEnabledProperty))UpdateColor(IsLoaded&&IsVisible&&IsEnabled&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full);}
    private void UpdateColor(bool animate)
    {
        if(!animate)_motion.Stop();if(_label is null)return;
        if(Foreground is SolidColorBrush source){_foreground??=new SolidColorBrush(source.Color);_label.SetValue(TextElement.ForegroundProperty,_foreground);_motion.Color(_foreground,source.Color,.16,animate);}
        else _label.SetValue(TextElement.ForegroundProperty,Foreground);
    }
}

/// <summary>Single-row, nonvirtualizing header layout with spacing only between visible items.</summary>
public sealed class TabsHeaderPanel:Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width=0,height=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;child.Measure(new Size(double.PositiveInfinity,availableSize.Height));width+=child.DesiredSize.Width;if(count++>0)width+=4;height=Math.Max(height,child.DesiredSize.Height);}
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;if(count++>0)x+=4;child.Arrange(new Rect(x,0,child.DesiredSize.Width,finalSize.Height));x+=child.DesiredSize.Width;}
        return finalSize;
    }
}
