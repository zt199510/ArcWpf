using System.Collections.ObjectModel;
using System.Windows;using System.Windows.Automation;using System.Windows.Automation.Peers;
using System.Windows.Controls;using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Controls;
public enum ToastCloseReason{Programmatic,Timeout,CloseButton,Swipe,Unloaded,Replaced}
public sealed class ToastClosingEventArgs(ToastCloseReason reason):EventArgs{public ToastCloseReason Reason{get;}=reason;}
public class ToastHost:ContentControl
{
 public static readonly DependencyProperty ItemsProperty=DependencyProperty.Register(nameof(Items),typeof(ObservableCollection<Toast>),typeof(ToastHost),new FrameworkPropertyMetadata(null));
 public ObservableCollection<Toast> Items{get=>(ObservableCollection<Toast>)GetValue(ItemsProperty)!;}
 static ToastHost()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(ToastHost),new FrameworkPropertyMetadata(typeof(ToastHost)));
 public ToastHost(){SetValue(ItemsProperty,new ObservableCollection<Toast>());Unloaded+=(_,_)=>{foreach(var t in Items.ToArray())t.Close(ToastCloseReason.Unloaded);};}
 public Toast Show(string title,string? description=null,int duration=4500){var t=new Toast{Title=title,Description=description??"",Duration=duration};Items.Add(t);t.Closed+=(_,_)=>Items.Remove(t);t.IsOpen=true;return t;}
 public void DismissAll(){foreach(var t in Items.ToArray())t.Close();Items.Clear();}
}
public sealed class Toast:ContentControl
{
 public static readonly DependencyProperty TitleProperty=DependencyProperty.Register(nameof(Title),typeof(string),typeof(Toast),new FrameworkPropertyMetadata(""));public static readonly DependencyProperty DescriptionProperty=DependencyProperty.Register(nameof(Description),typeof(string),typeof(Toast),new FrameworkPropertyMetadata(""));public static readonly DependencyProperty IsOpenProperty=DependencyProperty.Register(nameof(IsOpen),typeof(bool),typeof(Toast),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OpenChanged));public static readonly DependencyProperty DurationProperty=DependencyProperty.Register(nameof(Duration),typeof(int),typeof(Toast),new FrameworkPropertyMetadata(4500));private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(Toast),new FrameworkPropertyMetadata(false));public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
 public string Title{get=>(string)GetValue(TitleProperty);set=>SetValue(TitleProperty,value);}public string Description{get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}public bool IsOpen{get=>(bool)GetValue(IsOpenProperty);set=>SetValue(IsOpenProperty,value);}public int Duration{get=>(int)GetValue(DurationProperty);set=>SetValue(DurationProperty,value);}public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
 public event EventHandler<ToastClosingEventArgs>? Closing;public event EventHandler? Closed;private Border? _surface;private DispatcherTimer? _timer;private DispatcherTimer? _closeTimer;private readonly OwnedMotion _motion=new();private TranslateTransform? _move;private ScaleTransform? _scale;private int _version;private bool _setting;internal bool IsMotionRunning=>_motion.IsRunning;
 static Toast(){DefaultStyleKeyProperty.OverrideMetadata(typeof(Toast),new FrameworkPropertyMetadata(typeof(Toast)));}public Toast(){AddHandler(ButtonBase.ClickEvent,new RoutedEventHandler((_,e)=>Close(ToastCloseReason.CloseButton)));Loaded+=(_,_)=>Synchronize();Unloaded+=(_,_)=>Close(ToastCloseReason.Unloaded);}
 public void Close(ToastCloseReason reason=ToastCloseReason.Programmatic){if(!IsOpen&&!IsPresented)return;Closing?.Invoke(this,new ToastClosingEventArgs(reason));SetOpen(false);Transition(false);_closeTimer?.Stop();_closeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(190)};_closeTimer.Tick+=(_,_)=>{_closeTimer.Stop();if(!IsOpen)FinishClose();};_closeTimer.Start();}
 private static void OpenChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var t=(Toast)d;if(t._setting)return;if((bool)e.NewValue)t.Synchronize();else t.Close();}private void SetOpen(bool v){_setting=true;try{SetCurrentValue(IsOpenProperty,v);}finally{_setting=false;}}
 private void Synchronize(){if(!IsOpen)return;ApplyTemplate();if(_surface is null)return;if(!IsPresented){SetValue(IsPresentedPropertyKey,true);_surface.Opacity=0;_move!.Y=16;_scale!.ScaleX=_scale.ScaleY=.96;}Transition(true);_timer?.Stop();if(Duration>0){_timer??=new DispatcherTimer();_timer.Interval=TimeSpan.FromMilliseconds(Duration);_timer.Tick-=Timeout;_timer.Tick+=Timeout;_timer.Start();}}
 private void Timeout(object? s,EventArgs e){_timer?.Stop();Close(ToastCloseReason.Timeout);}private void Transition(bool open){int token=++_version;if(_surface is null)return;var l=ArcMotion.GetLevel(this);bool a=l!=ArcMotionLevel.Off;bool f=l==ArcMotionLevel.Full;double sec=open?(f?.4:.12):.16;_surface.IsHitTestVisible=open;if(open){_motion.Double(_surface,OpacityProperty,1,sec,a,enter:true);}else{_motion.Double(_surface,OpacityProperty,0,sec,a,completed:()=>{if(token==_version&&!IsOpen)FinishClose();});}if(_move is not null)_motion.Double(_move,TranslateTransform.YProperty,open?0:8,sec,a&&f,spring:open);if(_scale is not null){double x=open?1:.98;_motion.Double(_scale,ScaleTransform.ScaleXProperty,x,sec,a&&f,spring:open);_motion.Double(_scale,ScaleTransform.ScaleYProperty,x,sec,a&&f,spring:open);}}
 private void FinishClose(){_timer?.Stop();_closeTimer?.Stop();_motion.Stop();SetValue(IsPresentedPropertyKey,false);if(_surface is not null){_surface.Opacity=0;_surface.IsHitTestVisible=false;}Closed?.Invoke(this,EventArgs.Empty);}
 public override void OnApplyTemplate(){Release();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as Border;if(_surface is not null){_move=new();_scale=new(1,1);var g=new TransformGroup();g.Children.Add(_scale);g.Children.Add(_move);_surface.RenderTransform=g;}if(IsOpen)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(Synchronize));}protected override void OnTemplateChanged(ControlTemplate o,ControlTemplate n){if(IsPresented)Close(ToastCloseReason.Replaced);Release();base.OnTemplateChanged(o,n);}protected override AutomationPeer OnCreateAutomationPeer()=>new ToastPeer(this);private sealed class ToastPeer(Toast o):FrameworkElementAutomationPeer(o){protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Custom;protected override string GetNameCore()=>o.Title;} private void Release(){_timer?.Stop();_motion.Stop();_surface=null;_move=null;_scale=null;}
}










