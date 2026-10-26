using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NativeTab=System.Windows.Controls.TabItem;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_HeaderScroll",Type=typeof(ScrollViewer))]
[TemplatePart(Name="PART_HeaderTrack",Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Selection",Type=typeof(Border))]
[TemplatePart(Name="PART_SelectedContentHost",Type=typeof(ContentPresenter))]
[TemplatePart(Name="PART_PanelFrame",Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Outgoing",Type=typeof(Image))]
public partial class Tabs:TabControl
{
    public static readonly DependencyProperty HeaderAlignmentProperty=DependencyProperty.Register(nameof(HeaderAlignment),typeof(HorizontalAlignment),typeof(Tabs),new FrameworkPropertyMetadata(HorizontalAlignment.Left),value=>value is HorizontalAlignment alignment&&Enum.IsDefined(alignment));
    private static readonly DependencyPropertyKey HasOverflowPropertyKey=DependencyProperty.RegisterReadOnly(nameof(HasOverflow),typeof(bool),typeof(Tabs),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty HasOverflowProperty=HasOverflowPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey CanScrollBackwardPropertyKey=DependencyProperty.RegisterReadOnly(nameof(CanScrollBackward),typeof(bool),typeof(Tabs),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty CanScrollBackwardProperty=CanScrollBackwardPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey CanScrollForwardPropertyKey=DependencyProperty.RegisterReadOnly(nameof(CanScrollForward),typeof(bool),typeof(Tabs),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty CanScrollForwardProperty=CanScrollForwardPropertyKey.DependencyProperty;
    private static readonly DependencyProperty AnimatedScrollProperty=DependencyProperty.Register("AnimatedScroll",typeof(double),typeof(Tabs),new FrameworkPropertyMetadata(0d,(d,e)=>((Tabs)d)._headerScroll?.ScrollToHorizontalOffset((double)e.NewValue)));
    public HorizontalAlignment HeaderAlignment{get=>(HorizontalAlignment)GetValue(HeaderAlignmentProperty);set=>SetValue(HeaderAlignmentProperty,value);}
    public bool HasOverflow=>(bool)GetValue(HasOverflowProperty);
    public bool CanScrollBackward=>(bool)GetValue(CanScrollBackwardProperty);
    public bool CanScrollForward=>(bool)GetValue(CanScrollForwardProperty);
    private ScrollViewer? _headerScroll;
    private FrameworkElement? _headerTrack,_panelFrame;
    private Border? _selection;
    private ContentPresenter? _content;
    private Image? _outgoing;
    private ButtonBase? _backward,_forward;
    private TranslateTransform? _indicatorMove,_contentMove,_outgoingMove;
    private readonly ControlSpring _indicatorX=new(0,.42),_indicatorWidth=new(0,.42),_panelHeight=new(0,.4),_panelX=new(0,.4);
    private readonly OwnedMotion _fade=new(),_scrollMotion=new();
    private bool _rendering,_indicatorActive,_panelActive,_heightActive,_indicatorPlaced;
    private double _lastWidth=-1;
    private long _lastFrame;
    private int _version,_previousIndex=-1;
    private DispatcherOperation? _pending;
    internal bool IsMotionRunning=>_rendering||_fade.IsRunning||_scrollMotion.IsRunning;
    static Tabs()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Tabs),new FrameworkPropertyMetadata(typeof(Tabs)));
        TabStripPlacementProperty.OverrideMetadata(typeof(Tabs),new FrameworkPropertyMetadata(Dock.Top,null,(_,_)=>Dock.Top));
    }
    public Tabs()
    {
        Loaded+=(_,_)=>{_previousIndex=SelectedIndex;PlaceIndicator(false);RevealSelected(false);};
        Unloaded+=(_,_)=>ResetMotion();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)ResetMotion();};
        AddHandler(Keyboard.GotKeyboardFocusEvent,new KeyboardFocusChangedEventHandler(HeaderFocused));
    }
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is NativeTab;
    protected override DependencyObject GetContainerForItemOverride()=>new TabItem();
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e){base.OnItemsChanged(e);QueueLayout(false);}
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        int token=++_version;int before=_previousIndex;_previousIndex=SelectedIndex;
        bool animate=IsLoaded&&IsVisible&&before>=0&&ArcMotion.GetLevel(this)!=ArcMotionLevel.Off;
        double oldHeight=_panelFrame?.ActualHeight??0;
        var snapshot=animate?CaptureOutgoing():null;
        if(animate&&_panelFrame is not null)_panelFrame.Height=oldHeight;
        base.OnSelectionChanged(e);
        if(token!=_version)return;
        _pending?.Abort();
        int direction=SelectedIndex>=before?1:-1;if(FlowDirection==FlowDirection.RightToLeft)direction=-direction;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>
        {
            _pending=null;if(token!=_version)return;
            PlaceIndicator(animate);RevealSelected(animate);
            if(_content is null||_panelFrame is null)return;
            double width=Math.Max(0,_panelFrame.ActualWidth);
            _content.Measure(new Size(width,double.PositiveInfinity));
            BeginPanelTransition(oldHeight,_content.DesiredSize.Height,snapshot,direction,animate);
        }));
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if(e.Handled||Keyboard.Modifiers!=ModifierKeys.None||e.Key is not(Key.Left or Key.Right or Key.Home or Key.End))return;
        var focused=HeaderFrom(Keyboard.FocusedElement as DependencyObject);if(focused is null)return;
        int next=FindHeader(Items.IndexOf(ItemContainerGenerator.ItemFromContainer(focused)),e.Key);
        if(next<0)return;
        SetCurrentValue(SelectedIndexProperty,next);
        (ItemContainerGenerator.ContainerFromIndex(next) as NativeTab)?.Focus();RevealSelected(true);e.Handled=true;
    }
    internal int FindHeader(int current,Key key)
    {
        int[] candidates=Enumerable.Range(0,Items.Count).Where(i=>ItemContainerGenerator.ContainerFromIndex(i) is NativeTab{IsEnabled:true,Visibility:Visibility.Visible}).ToArray();
        if(candidates.Length==0)return -1;
        if(key==Key.Home)return candidates[0];if(key==Key.End)return candidates[^1];
        bool forward=key==Key.Right;if(FlowDirection==FlowDirection.RightToLeft)forward=!forward;
        int index=Array.IndexOf(candidates,current);
        return candidates[(index+(forward?1:-1)+candidates.Length)%candidates.Length];
    }
    private NativeTab? HeaderFrom(DependencyObject? child)
    {
        for(var node=child;node is not null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node is NativeTab item)return ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item),this)?item:null;
        return null;
    }
    private void HeaderFocused(object sender,KeyboardFocusChangedEventArgs e)
    {if(HeaderFrom(e.NewFocus as DependencyObject) is {} tab)Reveal(tab,true);}
    public override void OnApplyTemplate()
    {
        ReleaseParts();base.OnApplyTemplate();
        _headerScroll=GetTemplateChild("PART_HeaderScroll") as ScrollViewer;_headerTrack=GetTemplateChild("PART_HeaderTrack") as FrameworkElement;_selection=GetTemplateChild("PART_Selection") as Border;
        _panelFrame=GetTemplateChild("PART_PanelFrame") as FrameworkElement;_content=GetTemplateChild("PART_SelectedContentHost") as ContentPresenter;_outgoing=GetTemplateChild("PART_Outgoing") as Image;
        _backward=GetTemplateChild("PART_ScrollBackward") as ButtonBase;_forward=GetTemplateChild("PART_ScrollForward") as ButtonBase;
        if(_headerScroll is not null){_headerScroll.ScrollChanged+=Scrolled;_headerScroll.SizeChanged+=HeaderSized;}
        if(_headerTrack is not null)_headerTrack.LayoutUpdated+=HeaderLayout;
        if(_backward is not null)_backward.Click+=ScrollBackward;if(_forward is not null)_forward.Click+=ScrollForward;
        if(_selection is not null){_indicatorMove=new TranslateTransform();_selection.RenderTransform=_indicatorMove;_selection.Opacity=0;}
        if(_content is not null){_contentMove=new TranslateTransform();_content.RenderTransform=_contentMove;}
        if(_outgoing is not null){_outgoingMove=new TranslateTransform();_outgoing.RenderTransform=_outgoingMove;}
        if(_panelFrame is not null)_panelFrame.SizeChanged+=PanelSized;
        _previousIndex=SelectedIndex;QueueLayout(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseParts();base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);if(_fade is null)return;
        if(e.Property==ArcMotion.LevelProperty||e.Property==FlowDirectionProperty||e.Property==IsEnabledProperty){ResetMotion();QueueLayout(false);}
    }
    private void QueueLayout(bool animate)
    {
        if(_headerTrack is null)return;
        // Selection transitions own their queued layout pass; ordinary item changes don't replace it.
        if(_pending is not null)return;
        _pending=Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>{_pending=null;PlaceIndicator(animate);RevealSelected(animate);}));
    }
    private void StartFrames(){if(_rendering)return;_lastFrame=Stopwatch.GetTimestamp();CompositionTarget.Rendering+=Frame;_rendering=true;}
    private void StopFrames(){if(!_rendering)return;CompositionTarget.Rendering-=Frame;_rendering=false;}
    private void Frame(object? sender,EventArgs e){long now=Stopwatch.GetTimestamp();AdvanceMotion(Stopwatch.GetElapsedTime(_lastFrame,now).TotalSeconds);_lastFrame=now;}
    internal void AdvanceMotion(double seconds)
    {
        if(_indicatorActive){bool x=_indicatorX.Step(seconds),w=_indicatorWidth.Step(seconds);ApplyIndicator();_indicatorActive=!(x&&w);}
        if(_panelActive){if(_panelX.Step(seconds))_panelActive=false;if(_contentMove is not null)_contentMove.X=_panelX.Position;}
        if(_heightActive)
        {
            bool done=_panelHeight.Step(seconds);if(_panelFrame is not null)_panelFrame.Height=Math.Max(0,_panelHeight.Position);
            if(done){_heightActive=false;if(_panelFrame is not null){_panelFrame.Height=double.NaN;_panelFrame.ClipToBounds=false;}}
        }
        if(!_indicatorActive&&!_panelActive&&!_heightActive)StopFrames();
    }
    private void ResetMotion()
    {
        ++_version;_pending?.Abort();_pending=null;StopFrames();_indicatorActive=_panelActive=_heightActive=false;_indicatorPlaced=false;_fade.Stop();_scrollMotion.Stop();
        if(_panelFrame is not null){_panelFrame.Height=double.NaN;_panelFrame.ClipToBounds=false;}
        if(_content is not null)_content.Opacity=1;if(_contentMove is not null)_contentMove.X=0;
        if(_outgoing is not null){_outgoing.Source=null;_outgoing.Opacity=0;}
    }
    private void ReleaseParts()
    {
        ResetMotion();
        if(_headerScroll is not null){_headerScroll.ScrollChanged-=Scrolled;_headerScroll.SizeChanged-=HeaderSized;}
        if(_headerTrack is not null)_headerTrack.LayoutUpdated-=HeaderLayout;
        if(_panelFrame is not null)_panelFrame.SizeChanged-=PanelSized;
        if(_backward is not null)_backward.Click-=ScrollBackward;if(_forward is not null)_forward.Click-=ScrollForward;
        _headerScroll=null;_headerTrack=_panelFrame=null;_selection=null;_content=null;_outgoing=null;_backward=_forward=null;_indicatorMove=_contentMove=_outgoingMove=null;_lastWidth=-1;
    }
}
