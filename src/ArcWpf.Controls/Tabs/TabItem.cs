using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
namespace ArcWpf.Controls;
[TemplatePart(Name="PART_Close",Type=typeof(ButtonBase))]
public class TabItem:System.Windows.Controls.TabItem
{
    public static readonly DependencyProperty IsClosableProperty=DependencyProperty.Register(nameof(IsClosable),typeof(bool),typeof(TabItem),new FrameworkPropertyMetadata(false));
    /// <summary>Shows a close button and lets middle-click or Ctrl+F4 close the tab through its Tabs.</summary>
    public bool IsClosable{get=>(bool)GetValue(IsClosableProperty);set=>SetValue(IsClosableProperty,value);}
    internal static readonly DependencyPropertyKey IsDraggingPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsDragging),typeof(bool),typeof(TabItem),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsDraggingProperty=IsDraggingPropertyKey.DependencyProperty;
    /// <summary>True from the start of a reorder drag until the header has settled in its slot.</summary>
    public bool IsDragging=>(bool)GetValue(IsDraggingProperty);
    private ButtonBase? _close;
    private ContentPresenter? _label;
    private SolidColorBrush? _foreground;
    private readonly OwnedMotion _motion=new();
    static TabItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(TabItem),new FrameworkPropertyMetadata(typeof(TabItem)));
    public TabItem(){Loaded+=(_,_)=>UpdateColor(false);Unloaded+=(_,_)=>UpdateColor(false);}
    public override void OnApplyTemplate()
    {
        _motion.Stop();ReleaseClose();base.OnApplyTemplate();_label=GetTemplateChild("PART_Label") as ContentPresenter;_foreground=null;UpdateColor(false);
        _close=GetTemplateChild("PART_Close") as ButtonBase;if(_close is not null){_close.Click+=CloseClick;NameClose();}
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();ReleaseClose();_label=null;_foreground=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnHeaderChanged(object oldHeader,object newHeader){base.OnHeaderChanged(oldHeader,newHeader);NameClose();}
    private void ReleaseClose(){if(_close is not null)_close.Click-=CloseClick;_close=null;}
    private void NameClose(){if(_close is not null)AutomationProperties.SetName(_close,Header is string text?$"Close {text}":"Close tab");}
    private void CloseClick(object sender,RoutedEventArgs e){(ItemsControl.ItemsControlFromItemContainer(this) as Tabs)?.CloseTab(this);e.Handled=true;}
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
    internal const double Gap=4;
    /// <summary>Slides a header away from its slot while tabs are being reordered, without touching its transform.</summary>
    internal static readonly DependencyProperty DragOffsetProperty=DependencyProperty.RegisterAttached("DragOffset",typeof(double),typeof(TabsHeaderPanel),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsParentArrange));
    internal static double GetDragOffset(DependencyObject element)=>(double)element.GetValue(DragOffsetProperty);
    protected override Size MeasureOverride(Size availableSize)
    {
        double width=0,height=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;child.Measure(new Size(double.PositiveInfinity,availableSize.Height));width+=child.DesiredSize.Width;if(count++>0)width+=Gap;height=Math.Max(height,child.DesiredSize.Height);}
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;if(count++>0)x+=Gap;child.Arrange(new Rect(x+GetDragOffset(child),0,child.DesiredSize.Width,finalSize.Height));x+=child.DesiredSize.Width;}
        return finalSize;
    }
}
