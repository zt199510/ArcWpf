using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
namespace ArcWpf.Controls;

public class SegmentedItem : ListBoxItem
{
    public static readonly DependencyProperty IconProperty=DependencyProperty.Register(nameof(Icon),typeof(object),typeof(SegmentedItem),new PropertyMetadata(null));
    public static readonly DependencyProperty AccessoryProperty=DependencyProperty.Register(nameof(Accessory),typeof(object),typeof(SegmentedItem),new PropertyMetadata(null));
    public object? Icon {get=>GetValue(IconProperty);set=>SetValue(IconProperty,value);}
    public object? Accessory {get=>GetValue(AccessoryProperty);set=>SetValue(AccessoryProperty,value);}
    private FrameworkElement? _label;
    private SolidColorBrush? _brush;
    private readonly OwnedMotion _motion=new();
    internal bool IsMotionRunning=>_motion.IsRunning;
    static SegmentedItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedItem),new FrameworkPropertyMetadata(typeof(SegmentedItem)));
    public SegmentedItem(){Loaded+=(_,_)=>Color(false);Unloaded+=(_,_)=>StopMotion();IsVisibleChanged+=(_,_)=>{if(!IsVisible)StopMotion();};}
    public override void OnApplyTemplate(){StopMotion();base.OnApplyTemplate();_label=GetTemplateChild("PART_Label") as FrameworkElement;_brush=null;Color(false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){StopMotion();_label=null;_brush=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void Color(bool animate)
    {
        if(_label is null)return;
        if(Foreground is SolidColorBrush source){_brush??=new(source.Color);_label.SetValue(TextElement.ForegroundProperty,_brush);_motion.Color(_brush,source.Color,.16,animate);}
        else _label.SetValue(TextElement.ForegroundProperty,Foreground);
    }
    internal void StopMotion()=>_motion.Stop();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(_motion is not null && (e.Property==ForegroundProperty || e.Property==IsEnabledProperty || e.Property==ArcMotion.LevelProperty)){bool animate=IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this)==ArcMotionLevel.Full;if(!animate)StopMotion();Color(animate);}}
    protected override void OnMouseEnter(MouseEventArgs e){base.OnMouseEnter(e);(ItemsControl.ItemsControlFromItemContainer(this) as SegmentedControl)?.Intent(this);}
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnGotKeyboardFocus(e);(ItemsControl.ItemsControlFromItemContainer(this) as SegmentedControl)?.Intent(this);}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if(IsEnabled && ItemsControl.ItemsControlFromItemContainer(this) is SegmentedControl owner)
        {owner.SetCurrentValue(Selector.SelectedItemProperty,owner.ItemContainerGenerator.ItemFromContainer(this));Focus();e.Handled=true;return;}
        base.OnMouseLeftButtonDown(e);
    }
}

public sealed class SegmentedPanel : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        double width=0,height=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;child.Measure(new Size(double.PositiveInfinity,available.Height));if(count++>0)width+=2;width+=child.DesiredSize.Width;height=Math.Max(height,child.DesiredSize.Height);}
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size size)
    {
        double x=0;int count=0;
        foreach(UIElement child in InternalChildren){if(child.Visibility==Visibility.Collapsed)continue;if(count++>0)x+=2;child.Arrange(new Rect(x,0,child.DesiredSize.Width,size.Height));x+=child.DesiredSize.Width;}
        return size;
    }
}

