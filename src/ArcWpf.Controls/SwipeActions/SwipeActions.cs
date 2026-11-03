using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;
public sealed class SwipeAction { public string Label { get; set; } = string.Empty; public object? Icon { get; set; } public SwipeActionTone Tone { get; set; } public bool KeepRow { get; set; } public Action? OnSelect { get; set; } }
public enum SwipeActionTone { Neutral, Accent, Danger }
public class SwipeActions : ItemsControl
{
    public static readonly DependencyProperty LabelProperty=DependencyProperty.Register(nameof(Label),typeof(string),typeof(SwipeActions),new FrameworkPropertyMetadata("Swipe actions"));
    public string Label{get=>(string)GetValue(LabelProperty);set=>SetValue(LabelProperty,value);}
    static SwipeActions(){DefaultStyleKeyProperty.OverrideMetadata(typeof(SwipeActions),new FrameworkPropertyMetadata(typeof(SwipeActions)));}
    public SwipeActions(){PreviewMouseDown+=HandleOutsideMouseDown;PreviewKeyDown+=HandleEscape;}
    protected override DependencyObject GetContainerForItemOverride()=>new SwipeActionsRow(); protected override bool IsItemItsOwnContainerOverride(object item)=>item is SwipeActionsRow;
    internal void CloseOthers(SwipeActionsRow row){foreach(var item in Items.OfType<SwipeActionsRow>())if(!ReferenceEquals(item,row))item.Close();}
    private void HandleEscape(object sender,KeyEventArgs e){if(e.Key!=Key.Escape)return;foreach(var item in Items.OfType<SwipeActionsRow>())item.Close();e.Handled=true;}
    private void HandleOutsideMouseDown(object sender,MouseButtonEventArgs e){
        var source=e.OriginalSource as DependencyObject;
        for(var node=source;node is not null;node=VisualTreeHelper.GetParent(node)) if(node is SwipeActionsRow) return;
        foreach(var item in Items.OfType<SwipeActionsRow>()) item.Close();
    }
}
public class SwipeActionsRow : ContentControl
{
    public static readonly DependencyProperty RowLabelProperty=DependencyProperty.Register(nameof(RowLabel),typeof(string),typeof(SwipeActionsRow),new FrameworkPropertyMetadata("Row"));
    public static readonly DependencyProperty LeadingProperty=DependencyProperty.Register(nameof(Leading),typeof(ObservableCollection<SwipeAction>),typeof(SwipeActionsRow),new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty TrailingProperty=DependencyProperty.Register(nameof(Trailing),typeof(ObservableCollection<SwipeAction>),typeof(SwipeActionsRow),new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty FullSwipeProperty=DependencyProperty.Register(nameof(FullSwipe),typeof(bool),typeof(SwipeActionsRow),new FrameworkPropertyMetadata(true));
    public string RowLabel{get=>(string)GetValue(RowLabelProperty);set=>SetValue(RowLabelProperty,value);} public ObservableCollection<SwipeAction> Leading{get=>(ObservableCollection<SwipeAction>)GetValue(LeadingProperty);set=>SetValue(LeadingProperty,value);} public ObservableCollection<SwipeAction> Trailing{get=>(ObservableCollection<SwipeAction>)GetValue(TrailingProperty);set=>SetValue(TrailingProperty,value);} public bool FullSwipe{get=>(bool)GetValue(FullSwipeProperty);set=>SetValue(FullSwipeProperty,value);}
    private FrameworkElement? _surface; private SwipeActionItems? _leadingLayer, _trailingLayer; private TranslateTransform? _translate; private ButtonBase? _more; private System.Windows.Controls.ContextMenu? _moreMenu; private Point _start; private double _origin; private bool _drag; private double _lastX; private int _lastTimestamp; private double _velocity;
    static SwipeActionsRow(){DefaultStyleKeyProperty.OverrideMetadata(typeof(SwipeActionsRow),new FrameworkPropertyMetadata(typeof(SwipeActionsRow)));}
    public SwipeActionsRow(){Leading=new ObservableCollection<SwipeAction>();Trailing=new ObservableCollection<SwipeAction>();}
    public override void OnApplyTemplate(){Release();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as FrameworkElement;_leadingLayer=GetTemplateChild("PART_LeadingActions") as SwipeActionItems;_trailingLayer=GetTemplateChild("PART_TrailingActions") as SwipeActionItems;_translate=_surface?.RenderTransform as TranslateTransform;_more=GetTemplateChild("PART_More") as ButtonBase;if(_surface is not null){_surface.PreviewMouseLeftButtonDown+=Down;_surface.PreviewMouseMove+=Move;_surface.PreviewMouseLeftButtonUp+=Up;_surface.PreviewTouchDown+=HandleTouchDown;_surface.PreviewTouchMove+=HandleTouchMove;_surface.PreviewTouchUp+=HandleTouchUp;}if(_more is not null){_more.Click+=MoreClick;BuildMoreMenu();}}
    private void Release(){if(_surface is not null){_surface.PreviewMouseLeftButtonDown-=Down;_surface.PreviewMouseMove-=Move;_surface.PreviewMouseLeftButtonUp-=Up;_surface.PreviewTouchDown-=HandleTouchDown;_surface.PreviewTouchMove-=HandleTouchMove;_surface.PreviewTouchUp-=HandleTouchUp;}if(_more is not null)_more.Click-=MoreClick;if(_moreMenu is not null){_moreMenu.Items.Clear();_moreMenu=null;}if(_more is not null)_more.ContextMenu=null;_surface=null;_leadingLayer=null;_trailingLayer=null;_translate=null;_more=null;}
    private void Down(object s,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left||IsInteractive(e.OriginalSource as DependencyObject))return;Begin(e.GetPosition(this).X,e.Timestamp);_surface?.CaptureMouse();e.Handled=true;} private void Move(object s,MouseEventArgs e){if(!_drag||_surface is null)return;var p=e.GetPosition(this);if(Math.Abs(p.X-_start.X)<6)return;SetOffset(_origin+p.X-_start.X,e.Timestamp);}
    private void Up(object s,MouseButtonEventArgs e){if(!_drag)return;End(e.GetPosition(this).X,e.Timestamp);_surface?.ReleaseMouseCapture();e.Handled=true;}
    private void HandleTouchDown(object? s,TouchEventArgs e){Begin(e.GetTouchPoint(this).Position.X,e.Timestamp);_surface?.CaptureTouch(e.TouchDevice);e.Handled=true;} private void HandleTouchMove(object? s,TouchEventArgs e){if(_drag){SetOffset(_origin+e.GetTouchPoint(this).Position.X-_start.X,e.Timestamp);e.Handled=true;}} private void HandleTouchUp(object? s,TouchEventArgs e){if(_drag){End(e.GetTouchPoint(this).Position.X,e.Timestamp);_surface?.ReleaseTouchCapture(e.TouchDevice);e.Handled=true;}}
    private void Begin(double x,int timestamp){_drag=true;_start=new Point(x,0);_origin=_translate?.X??0;_lastX=x;_lastTimestamp=timestamp;_velocity=0;(ItemsControl.ItemsControlFromItemContainer(this) as SwipeActions)?.CloseOthers(this);}
    private void SetOffset(double raw,int timestamp){var dt=Math.Max(1,timestamp-_lastTimestamp);_velocity=(raw-_lastX)/(dt/1000d);_lastX=raw;_lastTimestamp=timestamp;var min=-Math.Max(1,Trailing.Count*76);var max=Math.Max(1,Leading.Count*76);var limit=raw<min?min+Rubber(min-raw,ActualWidth):raw>max?max-Rubber(raw-max,ActualWidth):raw;if(_translate is not null)_translate.X=limit;_leadingLayer?.SetRevealWidth(Math.Max(0,limit));_trailingLayer?.SetRevealWidth(Math.Max(0,-limit));}
    private static double Rubber(double overshoot,double dimension){dimension=Math.Max(1,dimension);return overshoot*dimension*.55/(dimension+.55*overshoot);}
    private void End(double x,int timestamp){_drag=false;SetOffset(_origin+x-_start.X,timestamp);double next=_translate?.X??0;double threshold=Math.Max(48,ActualWidth*.56);if(FullSwipe&&Math.Abs(next)>=threshold){var actions=next<0?Trailing:Leading;if(actions.Count>0){InvokeAction(actions[^1]);if(!actions[^1].KeepRow){Visibility=Visibility.Collapsed;return;}}}var target=Math.Abs(next)>Math.Max(48,(next<0?Trailing.Count:Leading.Count)*38)?Math.Sign(next)*(next<0?Trailing.Count:Leading.Count)*76:0;AnimateTo(target,_velocity);}
    internal void InvokeAction(SwipeAction action){action.OnSelect?.Invoke();if(action.KeepRow)AnimateTo(0);}
    private void AnimateTo(double target,double velocity=0){if(_translate is null)return;var level=ArcMotion.GetLevel(this);if(level==ArcMotionLevel.Off||!IsLoaded){_translate.X=target;return;}var animation=new DoubleAnimation(_translate.X,target,TimeSpan.FromMilliseconds(level==ArcMotionLevel.Full?260:140)){EasingFunction=level==ArcMotionLevel.Full?new BackEase{Amplitude=.12,EasingMode=EasingMode.EaseOut}:new CubicEase{EasingMode=EasingMode.EaseOut}};_translate.BeginAnimation(TranslateTransform.XProperty,animation,HandoffBehavior.SnapshotAndReplace);}
    public void Close(){AnimateTo(0);_leadingLayer?.SetRevealWidth(0);_trailingLayer?.SetRevealWidth(0);}
    public double OffsetForTest => _translate?.X ?? 0;
    public void CommitForTest(double delta){if(_translate is not null)_translate.X=delta; if(delta<0&&Trailing.Count>0)InvokeAction(Trailing[^1]);}
    private bool IsInteractive(DependencyObject? n){for(;n is not null&& !ReferenceEquals(n,this);n=VisualTreeHelper.GetParent(n))if(n is ButtonBase)return true;return false;}
    private void BuildMoreMenu(){if(_more is null)return;_moreMenu=new System.Windows.Controls.ContextMenu();foreach(var action in Leading.Concat(Trailing)){var item=new System.Windows.Controls.MenuItem{Header=action.Label,Tag=action};item.Click+=MoreItemClick;_moreMenu.Items.Add(item);} _more.ContextMenu=_moreMenu;}
    private void MoreItemClick(object sender,RoutedEventArgs e){if(sender is not System.Windows.Controls.MenuItem item||item.Tag is not SwipeAction action)return;action.OnSelect?.Invoke();if(!action.KeepRow)Visibility=Visibility.Collapsed;else Close();e.Handled=true;}
    private void MoreClick(object? s,RoutedEventArgs e){if(_moreMenu is not null){_moreMenu.PlacementTarget=_more;_moreMenu.IsOpen=true;}e.Handled=true;}
}

/// <summary>动作层的 WPF 原生按钮容器，确保动作在揭示后仍有真实鼠标命中和键盘语义。</summary>
public sealed class SwipeActionItems : ItemsControl
{
    private const double ActionWidth = 76;
    protected override DependencyObject GetContainerForItemOverride() => new SwipeActionButton();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is SwipeActionButton;
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is SwipeActionButton button) button.Action = item as SwipeAction;
    }
    internal void SetRevealWidth(double reveal)
    {
        var baseWidth = Items.Count * ActionWidth;
        Width = Math.Max(baseWidth, reveal);
        if (ItemContainerGenerator.ContainerFromIndex(Items.Count - 1) is SwipeActionButton outer)
            outer.Width = Math.Max(ActionWidth, Width - Math.Max(0, Items.Count - 1) * ActionWidth);
    }
}

/// <summary>单个揭示动作；样式负责 Arc 颜色和几何，行为回收到所在行。</summary>
public sealed class SwipeActionButton : Button
{
    public SwipeAction? Action { get; set; }

    protected override void OnClick()
    {
        base.OnClick();
        for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is SwipeActionsRow row && Action is not null)
            {
                row.InvokeAction(Action);
                return;
            }
        }
    }
}
