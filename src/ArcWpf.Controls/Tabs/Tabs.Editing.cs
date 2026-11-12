using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NativeTab=System.Windows.Controls.TabItem;
namespace ArcWpf.Controls;

public partial class Tabs
{
    public static readonly DependencyProperty AllowReorderProperty=DependencyProperty.Register(nameof(AllowReorder),typeof(bool),typeof(Tabs),new FrameworkPropertyMetadata(false,(d,_)=>((Tabs)d).EndDrag(false)));
    public static readonly DependencyProperty CloseCommandProperty=DependencyProperty.Register(nameof(CloseCommand),typeof(ICommand),typeof(Tabs),new FrameworkPropertyMetadata(null));
    public static readonly RoutedEvent TabClosingEvent=EventManager.RegisterRoutedEvent(nameof(TabClosing),RoutingStrategy.Bubble,typeof(EventHandler<TabClosingEventArgs>),typeof(Tabs));
    public static readonly RoutedEvent TabClosedEvent=EventManager.RegisterRoutedEvent(nameof(TabClosed),RoutingStrategy.Bubble,typeof(EventHandler<TabEventArgs>),typeof(Tabs));
    public static readonly RoutedEvent TabMovedEvent=EventManager.RegisterRoutedEvent(nameof(TabMoved),RoutingStrategy.Bubble,typeof(EventHandler<TabMovedEventArgs>),typeof(Tabs));
    /// <summary>Lets headers be dragged, or moved with Ctrl+Shift+Left/Right, when the items can be reordered in place.</summary>
    public bool AllowReorder{get=>(bool)GetValue(AllowReorderProperty);set=>SetValue(AllowReorderProperty,value);}
    /// <summary>Takes over removing a closed tab; it receives the item. Without it the tab is removed from Items or the ItemsSource list.</summary>
    public ICommand? CloseCommand{get=>(ICommand?)GetValue(CloseCommandProperty);set=>SetValue(CloseCommandProperty,value);}
    public event EventHandler<TabClosingEventArgs> TabClosing{add=>AddHandler(TabClosingEvent,value);remove=>RemoveHandler(TabClosingEvent,value);}
    public event EventHandler<TabEventArgs> TabClosed{add=>AddHandler(TabClosedEvent,value);remove=>RemoveHandler(TabClosedEvent,value);}
    public event EventHandler<TabMovedEventArgs> TabMoved{add=>AddHandler(TabMovedEvent,value);remove=>RemoveHandler(TabMovedEvent,value);}

    private readonly OwnedMotion _shift=new();
    private NativeTab? _dragTab,_lifted;
    private List<(NativeTab Tab,double X,double Width)>? _slots;
    private DispatcherTimer? _edgeTimer;
    private bool _dragging;
    private double _dragOrigin,_dragMin,_dragMax;
    private int _dragFrom,_dragTo;
    internal bool IsDragging=>_dragging;

    /// <summary>Closes the tab for an item or its container, as its close button would. False when it was cancelled or could not be removed.</summary>
    public bool CloseTab(object item)
    {
        object data=item is NativeTab tab&&ItemContainerGenerator.ItemFromContainer(tab) is {} found&&found!=DependencyProperty.UnsetValue?found:item;
        int index=Items.IndexOf(data);if(index<0)return false;
        var closing=new TabClosingEventArgs(TabClosingEvent,this,data,index);RaiseEvent(closing);
        if(closing.Cancel)return false;
        bool focused=(ItemContainerGenerator.ContainerFromIndex(index) as NativeTab)?.IsKeyboardFocusWithin==true;
        EndDrag(false);
        if(CloseCommand is {} command){if(!command.CanExecute(data))return false;command.Execute(data);}
        else if(EditableList() is {} list)list.Remove(data);
        // A command may keep the tab (an unsaved-changes prompt, say); it only counts as closed once it is gone.
        if(Items.Contains(data))return false;
        RaiseEvent(new TabEventArgs(TabClosedEvent,this,data,index));
        if(focused)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>(ItemContainerGenerator.ContainerFromItem(SelectedItem) as NativeTab)?.Focus()));
        return true;
    }

    // The list the tabs really live in; null when they can't be edited in place (a fixed array, or a sorted or filtered view).
    private IList? EditableList()
    {
        IList? list=ItemsSource is null?Items:ItemsSource as IList;
        if(list is null||list.IsReadOnly||list.IsFixedSize)return null;
        return ItemsSource is not null&&(Items.SortDescriptions.Count>0||Items.Filter is not null||Items.Count!=list.Count)?null:list;
    }

    internal bool MoveTab(int from,int to)
    {
        if(EditableList() is not {} list||from==to||from<0||to<0||from>=list.Count||to>=list.Count)return false;
        object item=list[from]!;
        if(SelectedIndex==from)
        {
            // The selected tab stays in the list while its neighbours step past it, so the selection never leaves it.
            int step=to>from?1:-1;
            for(int i=from;i!=to;i+=step){object other=list[i+step]!;list.RemoveAt(i+step);list.Insert(i,other);}
        }
        else{list.RemoveAt(from);list.Insert(to,item);}
        RaiseEvent(new TabMovedEventArgs(TabMovedEvent,this,item,from,to));
        return true;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if(e.ChangedButton!=MouseButton.Middle||HeaderFrom(e.OriginalSource as DependencyObject) is not TabItem{IsClosable:true,IsEnabled:true} tab)return;
        CloseTab(tab);e.Handled=true;
    }
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _dragTab=null;
        if(!AllowReorder||_headerTrack is null||EditableList() is null||InButton(e.OriginalSource as DependencyObject))return;
        if(HeaderFrom(e.OriginalSource as DependencyObject) is {IsEnabled:true} tab){_dragTab=tab;_dragOrigin=e.GetPosition(_headerTrack).X;}
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if(_dragTab is null||_headerTrack is null)return;
        if(e.LeftButton!=MouseButtonState.Pressed){if(_dragging)EndDrag(false);else _dragTab=null;return;}
        double x=e.GetPosition(_headerTrack).X;
        if(!_dragging)
        {
            if(Math.Abs(x-_dragOrigin)<SystemParameters.MinimumHorizontalDragDistance)return;
            if(!BeginDrag(_dragTab,_dragOrigin)){_dragTab=null;return;}
            CaptureMouse();
        }
        DragTo(x);e.Handled=true;
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if(_dragging){EndDrag(true);e.Handled=true;}
        _dragTab=null;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);EndDrag(false);}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(_dragging&&e.Key==Key.Escape){EndDrag(false);e.Handled=true;return;}
        if(!e.Handled&&Keyboard.Modifiers==ModifierKeys.Control&&e.Key is Key.F4 or Key.W&&ItemContainerGenerator.ContainerFromItem(SelectedItem) is TabItem{IsClosable:true} selected)
        {CloseTab(selected);e.Handled=true;return;}
        base.OnKeyDown(e);
    }
    // Called while the key tunnels down: left to bubble, the header strip's ScrollViewer would spend the arrow on moving focus first.
    private bool ReorderKey(KeyEventArgs e)
    {
        if(!AllowReorder||Keyboard.Modifiers!=(ModifierKeys.Control|ModifierKeys.Shift)||e.Key is not(Key.Left or Key.Right)||HeaderFrom(Keyboard.FocusedElement as DependencyObject) is not {} focused)return false;
        bool forward=e.Key==Key.Right;if(FlowDirection==FlowDirection.RightToLeft)forward=!forward;
        object item=ItemContainerGenerator.ItemFromContainer(focused);int from=Items.IndexOf(item),to=from;
        // Hidden tabs are stepped over so each press moves the header one visible place.
        do to+=forward?1:-1;while(to>=0&&to<Items.Count&&ItemContainerGenerator.ContainerFromIndex(to) is NativeTab{Visibility:not Visibility.Visible});
        if(MoveTab(from,to))Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(ItemContainerGenerator.ContainerFromItem(item) is NativeTab moved){moved.Focus();Reveal(moved,true);}}));
        return true;
    }
    private static bool InButton(DependencyObject? node)
    {
        for(;node is not null and not NativeTab;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node is ButtonBase)return true;
        return false;
    }

    internal bool BeginDrag(NativeTab tab,double origin)
    {
        EndDrag(false);
        if(_headerTrack is null||EditableList() is null)return false;
        _shift.Stop();Lift(null);
        var slots=new List<(NativeTab Tab,double X,double Width)>();
        for(int i=0;i<Items.Count;i++)if(ItemContainerGenerator.ContainerFromIndex(i) is NativeTab{Visibility:Visibility.Visible} header){header.ClearValue(TabsHeaderPanel.DragOffsetProperty);slots.Add((header,0,0));}
        _headerTrack.UpdateLayout();
        for(int i=0;i<slots.Count;i++)slots[i]=(slots[i].Tab,slots[i].Tab.TranslatePoint(new Point(),_headerTrack).X,slots[i].Tab.ActualWidth);
        int from=slots.FindIndex(slot=>ReferenceEquals(slot.Tab,tab));
        if(from<0||slots.Count<2)return false;
        _slots=slots;_dragTab=tab;_dragOrigin=origin;_dragFrom=_dragTo=from;_dragging=true;Lift(tab);
        _dragMin=slots[0].X-slots[from].X;_dragMax=slots[^1].X+slots[^1].Width-slots[from].X-slots[from].Width;
        // Normal priority: a lower one never runs while the header keeps repainting under the pointer.
        _edgeTimer??=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(16)};
        _edgeTimer.Tick-=EdgeScroll;_edgeTimer.Tick+=EdgeScroll;_edgeTimer.Start();
        return true;
    }
    internal void DragTo(double x)
    {
        if(!_dragging||_slots is null||_dragTab is null)return;
        var own=_slots[_dragFrom];double offset=Math.Clamp(x-_dragOrigin,_dragMin,_dragMax),left=own.X+offset,right=left+own.Width;
        _dragTab.SetValue(TabsHeaderPanel.DragOffsetProperty,offset);
        // A neighbour gives up its place once the dragged header's leading edge passes its middle.
        int to=_dragFrom;
        for(int i=_dragFrom+1;i<_slots.Count&&right>_slots[i].X+_slots[i].Width/2;i++)to=i;
        for(int i=_dragFrom-1;i>=0&&left<_slots[i].X+_slots[i].Width/2;i--)to=i;
        _dragTo=to;
        bool animate=IsLoaded&&IsVisible&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;double room=own.Width+TabsHeaderPanel.Gap;
        for(int i=0;i<_slots.Count;i++)
        {
            if(i==_dragFrom)continue;
            double shift=i>_dragFrom&&i<=to?-room:i<_dragFrom&&i>=to?room:0;
            _shift.Double(_slots[i].Tab,TabsHeaderPanel.DragOffsetProperty,shift,.2,animate);
        }
    }
    internal void EndDrag(bool commit)
    {
        _edgeTimer?.Stop();
        if(!_dragging||_slots is null||_dragTab is null){_dragging=false;_slots=null;return;}
        var slots=_slots;var tab=_dragTab;int from=_dragFrom,to=commit?_dragTo:from;
        _dragging=false;_slots=null;_dragTab=null;
        if(IsMouseCaptured)ReleaseMouseCapture();
        double held=slots[from].X+TabsHeaderPanel.GetDragOffset(tab);
        _shift.Stop();foreach(var slot in slots)slot.Tab.ClearValue(TabsHeaderPanel.DragOffsetProperty);
        Lift(null);
        if(to==from)return;
        object item=ItemContainerGenerator.ItemFromContainer(tab);
        if(!MoveTab(Items.IndexOf(item),Items.IndexOf(ItemContainerGenerator.ItemFromContainer(slots[to].Tab)))||_headerTrack is null)return;
        // The header glides from where it was let go into its new slot; the others are already there.
        _headerTrack.UpdateLayout();
        if(ItemContainerGenerator.ContainerFromItem(item) is not NativeTab landed)return;
        landed.SetValue(TabsHeaderPanel.DragOffsetProperty,held-landed.TranslatePoint(new Point(),_headerTrack).X);
        Lift(landed);
        _shift.Double(landed,TabsHeaderPanel.DragOffsetProperty,0,.2,IsLoaded&&IsVisible&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full,completed:()=>{if(ReferenceEquals(_lifted,landed))Lift(null);});
    }
    // One header at a time is drawn above its neighbours with its own fill, so the labels it passes over don't show through.
    private void Lift(NativeTab? tab)
    {
        if(_lifted is TabItem old)old.ClearValue(TabItem.IsDraggingPropertyKey);
        _lifted=tab;
        if(tab is TabItem next)next.SetValue(TabItem.IsDraggingPropertyKey,true);
    }
    private void EdgeScroll(object? sender,EventArgs e)
    {
        if(!_dragging||_headerScroll is null||_headerTrack is null||!HasOverflow)return;
        double pointer=Mouse.GetPosition(_headerScroll).X,view=_headerScroll.ViewportWidth,step=pointer<33?-8:pointer>view-33?8:0;
        if(step==0)return;
        _headerScroll.ScrollToHorizontalOffset(Math.Clamp(_headerScroll.HorizontalOffset+step,0,_headerScroll.ScrollableWidth));
        DragTo(Mouse.GetPosition(_headerTrack).X);
    }
}

public class TabEventArgs(RoutedEvent routedEvent,object source,object item,int index):RoutedEventArgs(routedEvent,source)
{
    /// <summary>The data item, which is the TabItem itself when tabs are declared directly.</summary>
    public object Item{get;}=item;
    public int Index{get;}=index;
}
public sealed class TabClosingEventArgs(RoutedEvent routedEvent,object source,object item,int index):TabEventArgs(routedEvent,source,item,index)
{
    public bool Cancel{get;set;}
}
public sealed class TabMovedEventArgs(RoutedEvent routedEvent,object source,object item,int oldIndex,int newIndex):RoutedEventArgs(routedEvent,source)
{
    public object Item{get;}=item;
    public int OldIndex{get;}=oldIndex;
    public int NewIndex{get;}=newIndex;
}
