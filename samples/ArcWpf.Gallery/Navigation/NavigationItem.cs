using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ArcWpf;
namespace ArcWpf.Gallery.Navigation;
public sealed class NavigationTree:TreeView
{
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is NavigationItem;
    protected override DependencyObject GetContainerForItemOverride()=>new NavigationItem();
}
public sealed class NavigationItem:TreeViewItem
{
    private Border? _hover,_active,_body;
    private ItemsPresenter? _items;
    private FrameworkElement? _row;
    private RotateTransform? _rotation;
    private DispatcherOperation? _pending;
    private int _version;
    public bool IsAnimating=>(_body?.HasAnimatedProperties??false)||(_hover?.HasAnimatedProperties??false)||(_active?.HasAnimatedProperties??false)||(_rotation?.HasAnimatedProperties??false);
    private bool Animate=>IsLoaded && IsVisible && ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
    public NavigationItem(){Unloaded+=(_,_)=>Stop();IsVisibleChanged+=(_,_)=>{if(!IsVisible)Stop();};}
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is NavigationItem;
    protected override DependencyObject GetContainerForItemOverride()=>new NavigationItem();
    public override void OnApplyTemplate()
    {
        Stop();base.OnApplyTemplate();_hover=GetTemplateChild("PART_Hover") as Border;_active=GetTemplateChild("PART_Active") as Border;_body=GetTemplateChild("PART_Body") as Border;_items=GetTemplateChild("PART_Items") as ItemsPresenter;_row=GetTemplateChild("PART_Row") as FrameworkElement;_rotation=GetTemplateChild("PART_Rotation") as RotateTransform;
        Expand(false);State(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){Stop();_body=_hover=_active=null;_rotation=null;_items=null;_row=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    private static void Fade(UIElement? target,double value,bool animate)
    {if(target is null)return;double from=target.Opacity;target.BeginAnimation(OpacityProperty,null);target.Opacity=value;if(!animate||Math.Abs(from-value)<.001)return;var a=new DoubleAnimation(from,value,TimeSpan.FromSeconds(.14)){FillBehavior=FillBehavior.Stop};a.Completed+=(_,_)=>target.BeginAnimation(OpacityProperty,null);target.BeginAnimation(OpacityProperty,a);}
    private void State(bool animate)
    {bool leaf=DataContext is NavigationNode {IsGroup:false};Fade(_active,leaf&&IsSelected?1:0,animate);Fade(_hover,(_row?.IsMouseOver==true||IsKeyboardFocused)&&!(leaf&&IsSelected)?1:0,animate);}
    private void Expand(bool animate)
    {
        if(_body is null)return;
        bool open=HasItems && IsExpanded;double from=_body.ActualHeight;double opacityFrom=_body.Opacity;int version=++_version;
        _body.BeginAnimation(HeightProperty,null);_body.BeginAnimation(OpacityProperty,null);
        if(_rotation is not null){double start=_rotation.Angle;_rotation.BeginAnimation(RotateTransform.AngleProperty,null);_rotation.Angle=open?0:-90;if(animate)_rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(start,_rotation.Angle,TimeSpan.FromSeconds(.16)){FillBehavior=FillBehavior.Stop});}
        if(!animate){_body.Height=open?double.NaN:0;_body.Opacity=open?1:0;return;}
        _items?.Measure(new Size(Math.Max(0,ActualWidth),double.PositiveInfinity));double target=open?(_items?.DesiredSize.Height??0):0;
        _body.Height=target;_body.Opacity=open?1:0;var height=new DoubleAnimation(from,target,TimeSpan.FromSeconds(.2)){FillBehavior=FillBehavior.Stop,DecelerationRatio=.8};
        height.Completed+=(_,_)=>{if(version!=_version||_body is null)return;_body.BeginAnimation(HeightProperty,null);_body.BeginAnimation(OpacityProperty,null);_rotation?.BeginAnimation(RotateTransform.AngleProperty,null);_body.Height=IsExpanded?double.NaN:0;};
        _body.BeginAnimation(HeightProperty,height);
        _body.BeginAnimation(OpacityProperty,new DoubleAnimation(opacityFrom,open?1:0,TimeSpan.FromSeconds(.2)){FillBehavior=FillBehavior.Stop});
    }
    private void Stop(){_pending?.Abort();_pending=null;++_version;_hover?.BeginAnimation(OpacityProperty,null);_active?.BeginAnimation(OpacityProperty,null);_body?.BeginAnimation(HeightProperty,null);_body?.BeginAnimation(OpacityProperty,null);_rotation?.BeginAnimation(RotateTransform.AngleProperty,null);if(_body is not null)_body.Height=HasItems&&IsExpanded?double.NaN:0;}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==IsExpandedProperty){_pending?.Abort();_pending=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{_pending=null;Expand(Animate);}));}
        if(e.Property==IsSelectedProperty||e.Property==IsMouseOverProperty||e.Property==IsKeyboardFocusedProperty)State(Animate);
        if(e.Property==ArcMotion.LevelProperty){Stop();Expand(false);State(false);}
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if(DataContext is NavigationNode {IsGroup:true} && e.GetPosition(this).Y<28){SetCurrentValue(IsExpandedProperty,!IsExpanded);Focus();e.Handled=true;return;}
        base.OnMouseLeftButtonDown(e);
    }
}
