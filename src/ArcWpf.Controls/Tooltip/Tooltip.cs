using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_Popup",Type=typeof(Popup))]
[TemplatePart(Name="PART_Surface",Type=typeof(TooltipSurface))]
public partial class Tooltip:ContentControl
{
    public static readonly DependencyProperty TipProperty=DependencyProperty.Register(nameof(Tip),typeof(object),typeof(Tooltip),new FrameworkPropertyMetadata(null,TipChanged));
    public static readonly DependencyProperty TipTemplateProperty=DependencyProperty.Register(nameof(TipTemplate),typeof(DataTemplate),typeof(Tooltip),new FrameworkPropertyMetadata(null,TipChanged));
    public static readonly DependencyProperty SideProperty=DependencyProperty.Register(nameof(Side),typeof(PopoverSide),typeof(Tooltip),new FrameworkPropertyMetadata(PopoverSide.Top,(d,e)=>((Tooltip)d).UpdatePlacement()),v=>v is PopoverSide s&&Enum.IsDefined(s));
    private static readonly DependencyPropertyKey IsOpenPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsOpen),typeof(bool),typeof(Tooltip),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsOpenProperty=IsOpenPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(Tooltip),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey ActualSidePropertyKey=DependencyProperty.RegisterReadOnly(nameof(ActualSide),typeof(PopoverSide),typeof(Tooltip),new FrameworkPropertyMetadata(PopoverSide.Top));
    public static readonly DependencyProperty ActualSideProperty=ActualSidePropertyKey.DependencyProperty;
    public object? Tip{get=>GetValue(TipProperty);set=>SetValue(TipProperty,value);}
    public DataTemplate? TipTemplate{get=>(DataTemplate?)GetValue(TipTemplateProperty);set=>SetValue(TipTemplateProperty,value);}
    public PopoverSide Side{get=>(PopoverSide)GetValue(SideProperty);set=>SetValue(SideProperty,value);}
    public bool IsOpen=>(bool)GetValue(IsOpenProperty);
    public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
    public PopoverSide ActualSide=>(PopoverSide)GetValue(ActualSideProperty);
    private Popup? _popup;
    private Border? _frame;
    private TooltipSurface? _surface;
    private TooltipText? _text;
    private ContentPresenter? _custom;
    private TranslateTransform? _move;
    private ScaleTransform? _scale;
    private Window? _owner;
    private UIElement? _helpTarget;
    private BindingExpressionBase? _helpBinding;
    private readonly DispatcherTimer _delay,_leave;
    private readonly OwnedMotion _motion=new();
    private static readonly ConditionalWeakTable<Dispatcher,TooltipWarmth> WarmStates=new();
    private TooltipWarmth Warm=>WarmStates.GetValue(Dispatcher,_=>new());
    private bool _suppressed,_counted,_placing;
    private int _version;
    private Point _placement;
    private Rect _lastAnchor=Rect.Empty;
    internal bool IsMotionRunning=>_motion.IsRunning||_text?.IsMotionRunning==true;
    internal bool HasPendingDelay=>_delay.IsEnabled;
    static Tooltip()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(Tooltip),new FrameworkPropertyMetadata(typeof(Tooltip)));
    public Tooltip()
    {
        _delay=new DispatcherTimer(DispatcherPriority.Input,Dispatcher){Interval=TimeSpan.FromMilliseconds(250)};
        _delay.Tick+=(_,_)=>{_delay.Stop();if(!_suppressed&&(IsMouseOver||IsKeyboardFocusWithin))OpenCore();};
        _leave=new DispatcherTimer(DispatcherPriority.Input,Dispatcher){Interval=TimeSpan.FromMilliseconds(120)};
        _leave.Tick+=(_,_)=>{_leave.Stop();if(!IsMouseOver&&!IsKeyboardFocusWithin&&_surface?.IsMouseOver!=true){_suppressed=false;Close();}};
        Loaded+=(_,_)=>AttachHelp();Unloaded+=(_,_)=>{CloseImmediately();DetachHelp();};
        MouseEnter+=(_,_)=>RequestOpen(false);MouseLeave+=(_,_)=>Leave();
        IsKeyboardFocusWithinChanged+=(_,_)=>{if(IsKeyboardFocusWithin)RequestOpen(true);else Leave();};
        PreviewMouseDown+=(_,_)=>{_suppressed=true;Close();};
        IsEnabledChanged+=(_,_)=>{if(!IsEnabled)CloseImmediately();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)CloseImmediately();};
    }
    internal void RequestOpen(bool keyboard)
    {
        _leave.Stop();if(_suppressed||!IsEnabled||Tip is null||Tip is string{Length:0})return;
        if(IsOpen)return;if(keyboard||Warm.IsWarm(TooltipWarmth.Now)){_delay.Stop();OpenCore();}else if(!_delay.IsEnabled)_delay.Start();
    }
    private void Leave(){_delay.Stop();if(!IsMouseOver&&!IsKeyboardFocusWithin)_suppressed=false;_leave.Stop();_leave.Start();}
    private void OpenCore()
    {
        if(!IsLoaded||!IsVisible||PresentationSource.FromVisual(this) is null)return;
        ApplyTemplate();if(_popup is null||_surface is null)return;
        var owner=Window.GetWindow(this);if(owner is null||!Popover.AnchorVisible(this,owner))return;
        AttachOwner(owner);BeginPresentation(Warm.IsWarm(TooltipWarmth.Now),true);
    }
    internal void BeginPresentation(bool instant,bool nativePopup)
    {
        if(_surface is null)return;_delay.Stop();_leave.Stop();bool fresh=!IsPresented;
        SetValue(IsOpenPropertyKey,true);SetValue(IsPresentedPropertyKey,true);if(!_counted){Warm.Opened();_counted=true;}
        RefreshTip();SetValue(ActualSidePropertyKey,Side);
        if(nativePopup){UpdatePlacement();if(!IsOpen)return;if(_popup is not null)_popup.IsOpen=true;LayoutUpdated-=TrackAnchor;LayoutUpdated+=TrackAnchor;}
        if(fresh){_surface.Opacity=0;if(_move is not null)_move.Y=instant?0:ActualSide==PopoverSide.Bottom?-3:3;if(_scale is not null)_scale.ScaleX=_scale.ScaleY=instant?1:.97;}
        Animate(true,instant,true);
    }
    public void Close()
    {
        _delay.Stop();_leave.Stop();if(!IsOpen)return;
        SetValue(IsOpenPropertyKey,false);Cool();Animate(false,false,true);
    }
    internal void CloseImmediately(){_delay?.Stop();_leave?.Stop();SetValue(IsOpenPropertyKey,false);Cool();FinishClose();}
    private void Cool(){if(_counted){_counted=false;Warm.Closed(TooltipWarmth.Now);}}
    private void FinishClose()
    {
        ++_version;_motion.Stop();_text?.StopMotion();SetValue(IsPresentedPropertyKey,false);
        if(_surface is not null)_surface.Opacity=0;
        if(_popup?.IsOpen==true)_popup.IsOpen=false;
        LayoutUpdated-=TrackAnchor;DetachOwner();_lastAnchor=Rect.Empty;
    }
    private void Animate(bool open,bool instant,bool animate)
    {
        if(_surface is null){if(!open)FinishClose();return;}
        int token=++_version;var level=ArcMotion.GetLevel(this);bool full=level==ArcMotionLevel.Full;animate&=level!=ArcMotionLevel.Off;
        double seconds=open&&!instant&&full?.16:.12;
        if(_move is not null)_motion.Double(_move,TranslateTransform.YProperty,0,seconds,animate&&full&&!instant,enter:open);
        if(_scale is not null){double target=full&&!open?.98:1;_motion.Double(_scale,ScaleTransform.ScaleXProperty,target,seconds,animate&&full&&!instant,enter:open);_motion.Double(_scale,ScaleTransform.ScaleYProperty,target,seconds,animate&&full&&!instant,enter:open);}
        _motion.Double(_surface,OpacityProperty,open?1:0,seconds,animate,enter:open&&!instant,completed:()=>{if(!open&&token==_version&&!IsOpen)FinishClose();});
    }
    private static void TipChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var tip=(Tooltip)d;tip.RefreshTip();if(tip.Tip is null||tip.Tip is string{Length:0})tip.Close();}
    internal static bool Plain(object? value)=>value is string or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    private void RefreshTip()
    {
        if(_text is null||_custom is null)return;bool plain=TipTemplate is null&&Plain(Tip);
        _text.Visibility=plain?Visibility.Visible:Visibility.Collapsed;_custom.Visibility=plain?Visibility.Collapsed:Visibility.Visible;
        if(plain)_text.SetText(Convert.ToString(Tip,CultureInfo.CurrentCulture)??"",IsOpen&&IsPresented);
        else {_text.StopMotion();_custom.Content=Tip;_custom.ContentTemplate=TipTemplate;}
        if(_surface is not null)AutomationProperties.SetName(_surface,plain?Convert.ToString(Tip,CultureInfo.CurrentCulture)??"":"Tooltip");UpdatePlacement();
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts();base.OnApplyTemplate();_popup=GetTemplateChild("PART_Popup") as Popup;_frame=GetTemplateChild("PART_Frame") as Border;_surface=GetTemplateChild("PART_Surface") as TooltipSurface;_text=GetTemplateChild("PART_Text") as TooltipText;_custom=GetTemplateChild("PART_Custom") as ContentPresenter;
        if(_popup is not null){_popup.PlacementTarget=this;_popup.Placement=PlacementMode.Custom;_popup.CustomPopupPlacementCallback=(_,_,_)=>[new CustomPopupPlacement(_placement,PopupPrimaryAxis.None)];_popup.Closed+=PopupClosed;}
        if(_surface is not null){_move=new();_scale=new(1,1);var transforms=new TransformGroup();transforms.Children.Add(_scale);transforms.Children.Add(_move);_surface.RenderTransform=transforms;_surface.MouseEnter+=BubbleEnter;_surface.MouseLeave+=BubbleLeave;_surface.SizeChanged+=SurfaceSized;}
        if(_frame is not null){BindingOperations.SetBinding(_frame,DataContextProperty,new Binding(nameof(DataContext)){Source=this});BindingOperations.SetBinding(_frame,FlowDirectionProperty,new Binding(nameof(FlowDirection)){Source=this});BindingOperations.SetBinding(_frame,ArcMotion.LevelProperty,new Binding{Path=new PropertyPath(ArcMotion.LevelProperty),Source=this});}
        RefreshTip();AttachHelp();
    }
    private void PopupClosed(object? sender,EventArgs e){if(IsPresented)CloseImmediately();}
    private void BubbleEnter(object sender,MouseEventArgs e)=>_leave.Stop();
    private void BubbleLeave(object sender,MouseEventArgs e)=>Leave();
    private void SurfaceSized(object sender,SizeChangedEventArgs e)=>UpdatePlacement();
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseParts();base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void ReleaseParts()
    {
        if(_delay is null)return;CloseImmediately();DetachHelp();
        if(_popup is not null){_popup.Closed-=PopupClosed;_popup.CustomPopupPlacementCallback=null;_popup.PlacementTarget=null;}
        if(_surface is not null){_surface.MouseEnter-=BubbleEnter;_surface.MouseLeave-=BubbleLeave;_surface.SizeChanged-=SurfaceSized;}
        if(_frame is not null){BindingOperations.ClearBinding(_frame,DataContextProperty);BindingOperations.ClearBinding(_frame,FlowDirectionProperty);BindingOperations.ClearBinding(_frame,ArcMotion.LevelProperty);}
        _popup=null;_frame=null;_surface=null;_text=null;_custom=null;_move=null;_scale=null;
    }
    protected override void OnContentChanged(object oldContent,object newContent){DetachHelp();base.OnContentChanged(oldContent,newContent);AttachHelp();}
    private void AttachHelp()
    {
        if(_helpTarget is not null||Content is not UIElement target)return;
        if(DependencyPropertyHelper.GetValueSource(target,AutomationProperties.HelpTextProperty).BaseValueSource!=BaseValueSource.Default)return;
        _helpTarget=target;_helpBinding=BindingOperations.SetBinding(target,AutomationProperties.HelpTextProperty,new Binding(nameof(Tip)){Source=this,Converter=HelpConverter.Instance});
    }
    private void DetachHelp(){if(_helpTarget is not null&&ReferenceEquals(BindingOperations.GetBindingExpressionBase(_helpTarget,AutomationProperties.HelpTextProperty),_helpBinding))BindingOperations.ClearBinding(_helpTarget,AutomationProperties.HelpTextProperty);_helpTarget=null;_helpBinding=null;}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&e.Property==ArcMotion.LevelProperty&&IsPresented)Animate(IsOpen,true,false);}
    private sealed class HelpConverter:IValueConverter
    {public static readonly HelpConverter Instance=new();public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Plain(value)?System.Convert.ToString(value,culture)??"":"";public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;}
}
