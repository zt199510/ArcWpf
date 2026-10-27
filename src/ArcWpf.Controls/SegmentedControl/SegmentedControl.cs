using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class SegmentIntentEventArgs(RoutedEvent routedEvent, object source, object item) : RoutedEventArgs(routedEvent,source)
{ public object Item { get; }=item; }

[TemplatePart(Name="PART_Scroll",Type=typeof(ScrollViewer))]
[TemplatePart(Name="PART_Track",Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Selection",Type=typeof(Border))]
public class SegmentedControl : ListBox
{
    public static readonly DependencyProperty LabelProperty=DependencyProperty.Register(nameof(Label),typeof(string),typeof(SegmentedControl),new PropertyMetadata(""));
    public string Label { get=>(string)GetValue(LabelProperty); set=>SetValue(LabelProperty,value); }
    public static readonly RoutedEvent OptionIntentEvent=EventManager.RegisterRoutedEvent(nameof(OptionIntent),RoutingStrategy.Bubble,typeof(EventHandler<SegmentIntentEventArgs>),typeof(SegmentedControl));
    public event EventHandler<SegmentIntentEventArgs> OptionIntent { add=>AddHandler(OptionIntentEvent,value); remove=>RemoveHandler(OptionIntentEvent,value); }
    private static readonly DependencyProperty ScrollPositionProperty=DependencyProperty.Register("ScrollPosition",typeof(double),typeof(SegmentedControl),new PropertyMetadata(0d,(d,e)=>((SegmentedControl)d)._scroll?.ScrollToHorizontalOffset((double)e.NewValue)));
    private ScrollViewer? _scroll;
    private FrameworkElement? _track;
    private Border? _selection;
    private TranslateTransform? _move;
    private readonly ControlSpring _x=new(0,.42),_width=new(0,.42),_height=new(0,.42);
    private readonly OwnedMotion _scrollMotion=new();
    private DispatcherOperation? _pending;
    private bool _rendering,_placed,_queueAnimate;
    private long _lastFrame;
    private (bool start,bool end,double width)? _maskState;
    internal bool IsMotionRunning=>_rendering || _scrollMotion.IsRunning || Containers().Any(c=>c.IsMotionRunning);
    private bool Animate=>IsLoaded && IsVisible && IsEnabled && ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
    static SegmentedControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SegmentedControl),new FrameworkPropertyMetadata(typeof(SegmentedControl)));
        SelectionModeProperty.OverrideMetadata(typeof(SegmentedControl),new FrameworkPropertyMetadata(SelectionMode.Single,null,(_,_)=>SelectionMode.Single));
    }
    public SegmentedControl()
    {
        Loaded+=(_,_)=>Queue(false);
        Unloaded+=(_,_)=>Stop();
        IsVisibleChanged+=(_,_)=>{ if(!IsVisible)Stop();else Queue(false); };
        IsEnabledChanged+=(_,_)=>{ if(!IsEnabled)Stop();Queue(false); };
        ItemContainerGenerator.StatusChanged+=(_,_)=>Queue(false);
    }
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is SegmentedItem;
    protected override DependencyObject GetContainerForItemOverride()=>new SegmentedItem();
    internal IEnumerable<SegmentedItem> Containers()=>Enumerable.Range(0,Items.Count).Select(i=>ItemContainerGenerator.ContainerFromIndex(i)).OfType<SegmentedItem>();
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e){base.OnItemsChanged(e);Queue(false);}
    protected override void OnSelectionChanged(SelectionChangedEventArgs e){base.OnSelectionChanged(e);Queue(true);}
    public override void OnApplyTemplate()
    {
        ReleaseTemplate();base.OnApplyTemplate();
        _scroll=GetTemplateChild("PART_Scroll") as ScrollViewer;_track=GetTemplateChild("PART_Track") as FrameworkElement;_selection=GetTemplateChild("PART_Selection") as Border;
        if(_selection is not null){_move=new();_selection.RenderTransform=_move;_selection.Visibility=Visibility.Hidden;}
        if(_scroll is not null){_scroll.ScrollChanged+=Scrolled;_scroll.PreviewMouseWheel+=Wheel;}
        if(_track is not null)_track.LayoutUpdated+=LayoutChanged;
        Queue(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseTemplate();base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void ReleaseTemplate()
    {
        Stop(); if(_scroll is not null){_scroll.ScrollChanged-=Scrolled;_scroll.PreviewMouseWheel-=Wheel;}
        if(_track is not null)_track.LayoutUpdated-=LayoutChanged;
        _scroll=null;_track=null;_selection=null;_move=null;_placed=false;_maskState=null;
    }
    private void Queue(bool animate)
    {
        _queueAnimate|=animate;
        if(!IsLoaded || !IsVisible || _pending is not null)return;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{_pending=null;bool motion=_queueAnimate;_queueAnimate=false;UpdateTabStops();Place(motion);Reveal(motion);UpdateMask();}));
    }
    private void LayoutChanged(object? sender,EventArgs e)
    {
        if(_pending is not null || !IsLoaded || !IsVisible)return;
        UpdateTabStops();Place(false,onlyIfGeometryChanged:true);UpdateMask();
    }
    private void UpdateTabStops()
    {
        var items=Containers().ToArray();var chosen=items.FirstOrDefault(c=>c.IsSelected && c.IsEnabled && c.Visibility==Visibility.Visible)??items.FirstOrDefault(c=>c.IsEnabled && c.Visibility==Visibility.Visible);
        foreach(var c in items)c.SetCurrentValue(IsTabStopProperty,ReferenceEquals(c,chosen));
    }
    private void Place(bool animate,bool onlyIfGeometryChanged=false)
    {
        if(_track is null || _selection is null || _move is null)return;
        if(SelectedIndex<0 || SelectedIndex>=Items.Count || ItemContainerGenerator.ContainerFromIndex(SelectedIndex) is not SegmentedItem {Visibility:Visibility.Visible} selected || selected.ActualWidth<=0)
        {StopFrames();_selection.Visibility=Visibility.Hidden;_placed=false;return;}
        double x=selected.TransformToAncestor(_track).TransformBounds(new Rect(selected.RenderSize)).X,w=selected.ActualWidth,h=selected.ActualHeight;
        if(onlyIfGeometryChanged && _placed && Math.Abs(_x.Target-x)<.05 && Math.Abs(_width.Target-w)<.05 && Math.Abs(_height.Target-h)<.05)return;
        _selection.Visibility=Visibility.Visible;
        if(!animate || !Animate || !_placed){StopFrames();_x.Snap(x);_width.Snap(w);_height.Snap(h);Draw();}
        else{_x.Target=x;_width.Target=w;_height.Target=h;StartFrames();}
        _placed=true;
    }
    private void Draw(){if(_selection is null || _move is null)return;_move.X=_x.Position;_selection.Width=Math.Max(0,_width.Position);_selection.Height=Math.Max(0,_height.Position);}
    private void StartFrames(){if(_rendering)return;_rendering=true;_lastFrame=Stopwatch.GetTimestamp();CompositionTarget.Rendering+=Frame;}
    private void Frame(object? sender,EventArgs e)
    {long now=Stopwatch.GetTimestamp();double dt=(now-_lastFrame)/(double)Stopwatch.Frequency;_lastFrame=now;bool x=_x.Step(dt),w=_width.Step(dt),h=_height.Step(dt);Draw();if(x&&w&&h)StopFrames();}
    private void StopFrames(){if(!_rendering)return;CompositionTarget.Rendering-=Frame;_rendering=false;}
    private void Stop()
    {
        _pending?.Abort();_pending=null;_queueAnimate=false;StopFrames();_scrollMotion.Stop();
        _x.Snap(_x.Target);_width.Snap(_width.Target);_height.Snap(_height.Target);Draw();foreach(var item in Containers())item.StopMotion();
    }
    private void Reveal(bool animate)
    {
        if(_scroll is null || _track is null || SelectedIndex<0 || SelectedIndex>=Items.Count || ItemContainerGenerator.ContainerFromIndex(SelectedIndex) is not SegmentedItem item)return;
        double start=item.TransformToAncestor(_track).TransformBounds(new Rect(item.RenderSize)).X-20,end=start+item.ActualWidth+40;
        double offset=_scroll.HorizontalOffset;
        if(item.ActualWidth+40>_scroll.ViewportWidth)offset=start;
        else if(start<offset)offset=start;else if(end>offset+_scroll.ViewportWidth)offset=end-_scroll.ViewportWidth;
        offset=Math.Clamp(offset,0,Math.Max(0,_scroll.ScrollableWidth));
        _scrollMotion.Stop();SetValue(ScrollPositionProperty,_scroll.HorizontalOffset);
        _scrollMotion.Double(this,ScrollPositionProperty,offset,.24,animate && Animate);
    }
    private void Scrolled(object sender,ScrollChangedEventArgs e)=>UpdateMask();
    private void Wheel(object sender,MouseWheelEventArgs e)
    {if(_scroll is null || _scroll.ScrollableWidth<=0)return;_scrollMotion.Stop();_scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset-e.Delta/3d);e.Handled=true;}
    private void UpdateMask()
    {
        if(_scroll is null || _scroll.ViewportWidth<=0)return;
        bool start=_scroll.HorizontalOffset>1,end=_scroll.ScrollableWidth-_scroll.HorizontalOffset>1;
        var state=(start,end,_scroll.ViewportWidth);if(_maskState==state)return;_maskState=state;
        if(!start&&!end){_scroll.OpacityMask=null;return;}
        double fade=Math.Min(.5,20/_scroll.ViewportWidth);
        _scroll.OpacityMask=new LinearGradientBrush(new GradientStopCollection{new(start?Colors.Transparent:Colors.Black,0),new(Colors.Black,start?fade:0),new(Colors.Black,end?1-fade:1),new(end?Colors.Transparent:Colors.Black,1)},new Point(0,0),new Point(1,0));
    }
    internal void Intent(SegmentedItem item)
    {if(IsEnabled && item.IsEnabled && ItemsControl.ItemsControlFromItemContainer(item)==this){var data=ItemContainerGenerator.ItemFromContainer(item);if(data!=DependencyProperty.UnsetValue)RaiseEvent(new SegmentIntentEventArgs(OptionIntentEvent,this,data));}}
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(!IsEnabled)return;
        if(e.Handled || Keyboard.Modifiers!=ModifierKeys.None){base.OnPreviewKeyDown(e);return;}
        if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            int index=FindNext(e.Key);if(index>=0){SetCurrentValue(SelectedIndexProperty,index);(ItemContainerGenerator.ContainerFromIndex(index) as SegmentedItem)?.Focus();}
            e.Handled=true;return;
        }
        if(e.Key is Key.Space or Key.Enter && ContainerFromElement(this,Keyboard.FocusedElement as DependencyObject) is SegmentedItem item && item.IsEnabled)
        {SetCurrentValue(SelectedItemProperty,ItemContainerGenerator.ItemFromContainer(item));e.Handled=true;return;}
        base.OnPreviewKeyDown(e);
    }
    internal int FindNext(Key key)
    {
        int[] valid=Enumerable.Range(0,Items.Count).Where(i=>ItemContainerGenerator.ContainerFromIndex(i) is SegmentedItem {IsEnabled:true,Visibility:Visibility.Visible}).ToArray();
        if(valid.Length==0 || !IsEnabled)return -1;
        if(key==Key.Home)return valid[0];if(key==Key.End)return valid[^1];
        bool forward=key is Key.Right or Key.Down;if(FlowDirection==FlowDirection.RightToLeft && key is Key.Right or Key.Left)forward=!forward;
        int pos=Array.IndexOf(valid,SelectedIndex);if(pos<0)return forward?valid[0]:valid[^1];return valid[(pos+(forward?1:-1)+valid.Length)%valid.Length];
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(_scrollMotion is not null && (e.Property==ArcMotion.LevelProperty || e.Property==FlowDirectionProperty)){Stop();Queue(false);}}
    protected override AutomationPeer OnCreateAutomationPeer()=>new SegmentedPeer(this);
    private sealed class SegmentedPeer(SegmentedControl owner):ListBoxAutomationPeer(owner)
    {protected override string GetClassNameCore()=>nameof(SegmentedControl);protected override string GetNameCore()=>string.IsNullOrEmpty(base.GetNameCore())?owner.Label:base.GetNameCore();}
}
