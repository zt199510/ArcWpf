using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Controls;

public enum DialogCloseReason{Programmatic,Escape,Overlay,CloseButton,HostUnavailable,Replaced,TemplateChanged}
public sealed class DialogClosingEventArgs(DialogCloseReason reason):CancelEventArgs{public DialogCloseReason Reason{get;}=reason;}
[TemplatePart(Name="PART_Surface",Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Overlay",Type=typeof(Border))]
[TemplatePart(Name="PART_CloseButton",Type=typeof(ButtonBase))]
public class Dialog:ContentControl
{
    public static readonly DependencyProperty IsOpenProperty=DependencyProperty.Register(nameof(IsOpen),typeof(bool),typeof(Dialog),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OpenChanged));
    public static readonly DependencyProperty TitleProperty=DependencyProperty.Register(nameof(Title),typeof(string),typeof(Dialog),new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty=DependencyProperty.Register(nameof(Description),typeof(string),typeof(Dialog),new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty CloseOnEscapeProperty=DependencyProperty.Register(nameof(CloseOnEscape),typeof(bool),typeof(Dialog),new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty CloseOnOverlayClickProperty=DependencyProperty.Register(nameof(CloseOnOverlayClick),typeof(bool),typeof(Dialog),new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty InitialFocusElementProperty=DependencyProperty.Register(nameof(InitialFocusElement),typeof(IInputElement),typeof(Dialog),new FrameworkPropertyMetadata(null));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(Dialog),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
    public static readonly RoutedUICommand CloseCommand=new("Close dialog",nameof(CloseCommand),typeof(Dialog));
    public bool IsOpen{get=>(bool)GetValue(IsOpenProperty);set=>SetValue(IsOpenProperty,value);}
    public string Title{get=>(string)GetValue(TitleProperty);set=>SetValue(TitleProperty,value);}
    public string Description{get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}
    public bool CloseOnEscape{get=>(bool)GetValue(CloseOnEscapeProperty);set=>SetValue(CloseOnEscapeProperty,value);}
    public bool CloseOnOverlayClick{get=>(bool)GetValue(CloseOnOverlayClickProperty);set=>SetValue(CloseOnOverlayClickProperty,value);}
    public IInputElement? InitialFocusElement{get=>(IInputElement?)GetValue(InitialFocusElementProperty);set=>SetValue(InitialFocusElementProperty,value);}
    public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
    public event EventHandler<DialogClosingEventArgs>? Closing;
    public event EventHandler? Opened;
    public event EventHandler? Closed;
    private DialogHost? _host;
    private FrameworkElement? _surface;
    private Border? _overlay;
    private ButtonBase? _close;
    private TranslateTransform? _move;
    private ScaleTransform? _scale;
    private readonly OwnedMotion _motion=new();
    private bool _setting,_requesting;
    private int _version;
    private IInputElement? _restoreFocus;
    private DispatcherOperation? _focus;
    internal bool IsMotionRunning=>_motion.IsRunning;
    static Dialog()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(Dialog),new FrameworkPropertyMetadata(typeof(Dialog)));
    public Dialog()
    {
        CommandBindings.Add(new CommandBinding(CloseCommand,(_,e)=>{Close();e.Handled=true;},(_,e)=>{e.CanExecute=IsOpen;e.Handled=true;}));
        Loaded+=(_,_)=>Synchronize();Unloaded+=(_,_)=>RequestClose(DialogCloseReason.HostUnavailable,true);
    }
    public void Show(FrameworkElement origin)
    {
        var host=DialogHost.Find(origin)??throw new InvalidOperationException("Place a DialogHost around the window content before showing a Dialog.");
        if(_host is not null&&!ReferenceEquals(_host,host))throw new InvalidOperationException("A Dialog belongs to one DialogHost at a time.");
        host.SetCurrentValue(DialogHost.DialogProperty,this);SetCurrentValue(IsOpenProperty,true);Synchronize();
    }
    public void Close()=>RequestClose(DialogCloseReason.Programmatic);
    internal void Attach(DialogHost? host){_host=host;if(host is not null)Synchronize();}
    private static void OpenChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {var dialog=(Dialog)d;if(dialog._setting)return;if((bool)e.NewValue)dialog.Synchronize();else dialog.RequestClose(DialogCloseReason.Programmatic);}
    private void SetOpen(bool value){_setting=true;try{SetCurrentValue(IsOpenProperty,value);}finally{_setting=false;}}
    internal void Synchronize()
    {
        if(!IsOpen||_host is null)return;
        ApplyTemplate();if(_surface is null)return;
        if(IsPresented&&_surface.IsHitTestVisible)return;
        bool fresh=!IsPresented;if(fresh)_restoreFocus=Keyboard.FocusedElement;
        SetValue(IsPresentedPropertyKey,true);_host.UpdateModal();
        if(fresh){_surface.Opacity=0;if(_overlay is not null)_overlay.Opacity=0;if(_move is not null)_move.Y=8;if(_scale is not null)_scale.ScaleX=_scale.ScaleY=.96;}
        Transition(true,_host.IsLoaded&&_host.IsVisible);
        QueueFocus();Opened?.Invoke(this,EventArgs.Empty);
    }
    internal void RequestClose(DialogCloseReason reason,bool force=false)
    {
        if(_requesting){if(force){SetOpen(false);FinishClose(false);}return;}
        if(!IsOpen&&!IsPresented)return;
        // Repeated dismissals must not invalidate the completion of an already running exit.
        if(!force&&!IsOpen&&IsPresented&&_surface?.IsHitTestVisible==false)return;
        _requesting=true;
        try
        {
            if(!force){var args=new DialogClosingEventArgs(reason);Closing?.Invoke(this,args);if(args.Cancel){SetOpen(true);return;}}
            SetOpen(false);
            if(force||!IsPresented){FinishClose(!force);return;}
            Transition(false,IsLoaded&&IsVisible);
        }
        finally{_requesting=false;}
    }
    internal void Transition(bool open,bool animate)
    {
        int token=++_version;
        if(_surface is null){if(!open)FinishClose(true);return;}
        _surface.IsHitTestVisible=open;
        var level=ArcMotion.GetLevel(this);animate&=level!=ArcMotionLevel.Off;
        bool full=level==ArcMotionLevel.Full;
        double fade=full?(open?.16:.16):.12;
        if(_move is not null)_motion.Double(_move,TranslateTransform.YProperty,full?(open?0:4):0,open?.4:.16,animate&&full,spring:open);
        if(_scale is not null)
        {
            double target=full&&!open?.98:1;
            _motion.Double(_scale,ScaleTransform.ScaleXProperty,target,open?.4:.16,animate&&full,spring:open);
            _motion.Double(_scale,ScaleTransform.ScaleYProperty,target,open?.4:.16,animate&&full,spring:open);
        }
        if(_overlay is not null)_motion.Double(_overlay,OpacityProperty,open?1:0,full&&open?.24:fade,animate,enter:open);
        _motion.Double(_surface,OpacityProperty,open?1:0,fade,animate,enter:open,completed:()=>{if(!open&&token==_version&&!IsOpen)FinishClose(true);});
    }
    private void FinishClose(bool restore)
    {
        ++_version;_motion.Stop();_focus?.Abort();_focus=null;
        bool was=IsPresented;SetValue(IsPresentedPropertyKey,false);_host?.UpdateModal();
        if(_surface is not null){_surface.Opacity=0;_surface.IsHitTestVisible=false;}if(_overlay is not null)_overlay.Opacity=0;
        var target=_restoreFocus;_restoreFocus=null;
        if(restore&&target is UIElement{IsVisible:true,IsEnabled:true} element)element.Focus();
        if(was)Closed?.Invoke(this,EventArgs.Empty);
    }
    private void QueueFocus(){_focus?.Abort();_focus=Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{_focus=null;if(IsOpen&&IsPresented)FocusInitial();}));}
    internal void FocusInitial()
    {
        if(InitialFocusElement is DependencyObject candidate&&DialogHost.Contains(this,candidate)&&ReferenceEquals(Keyboard.Focus(InitialFocusElement),InitialFocusElement))return;
        if(_surface?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))!=true)_surface?.Focus();
    }
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(!e.Handled&&e.Key==Key.Escape&&IsOpen&&CloseOnEscape){RequestClose(DialogCloseReason.Escape);e.Handled=true;}}
    private void Outside(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left&&IsOpen&&CloseOnOverlayClick){RequestClose(DialogCloseReason.Overlay);e.Handled=true;}}
    private void CloseClicked(object sender,RoutedEventArgs e)=>RequestClose(DialogCloseReason.CloseButton);
    public override void OnApplyTemplate()
    {
        ReleaseParts();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as FrameworkElement;_overlay=GetTemplateChild("PART_Overlay") as Border;_close=GetTemplateChild("PART_CloseButton") as ButtonBase;
        if(_overlay is not null)_overlay.MouseLeftButtonDown+=Outside;if(_close is not null)_close.Click+=CloseClicked;
        if(_surface is not null){_move=new TranslateTransform();_scale=new ScaleTransform(1,1);var group=new TransformGroup();group.Children.Add(_scale);group.Children.Add(_move);_surface.RenderTransform=group;}
        if(IsOpen&&_host is not null)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(Synchronize));
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){if(IsPresented)RequestClose(DialogCloseReason.TemplateChanged,true);ReleaseParts();base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void ReleaseParts(){_motion.Stop();if(_overlay is not null)_overlay.MouseLeftButtonDown-=Outside;if(_close is not null)_close.Click-=CloseClicked;_surface=null;_overlay=null;_close=null;_move=null;_scale=null;}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(_motion is not null&&e.Property==ArcMotion.LevelProperty&&IsPresented)Transition(IsOpen,false);}
    protected override AutomationPeer OnCreateAutomationPeer()=>new DialogPeer(this);
    private sealed class DialogPeer(Dialog owner):FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore()=>nameof(Dialog);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Group;
        protected override bool IsDialogCore()=>true;
        protected override string GetNameCore()=>string.IsNullOrEmpty(AutomationProperties.GetName(owner))?owner.Title:base.GetNameCore();
        protected override string GetHelpTextCore()=>string.IsNullOrEmpty(AutomationProperties.GetHelpText(owner))?owner.Description:base.GetHelpTextCore();
    }
}



