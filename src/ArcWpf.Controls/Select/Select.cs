using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_Popup",Type=typeof(Popup))]
[TemplatePart(Name="PART_Trigger",Type=typeof(FrameworkElement))]
public partial class Select:ComboBox
{
    public static readonly DependencyProperty LabelProperty=DependencyProperty.Register(nameof(Label),typeof(string),typeof(Select),new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty=DependencyProperty.Register(nameof(Description),typeof(string),typeof(Select),new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty PlaceholderProperty=DependencyProperty.Register(nameof(Placeholder),typeof(string),typeof(Select),new FrameworkPropertyMetadata("Select an option"));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(Select),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
    public string Label{get=>(string)GetValue(LabelProperty);set=>SetValue(LabelProperty,value);}
    public string Description{get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}
    public string Placeholder{get=>(string)GetValue(PlaceholderProperty);set=>SetValue(PlaceholderProperty,value);}
    public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
    private Popup? _popup;
    private FrameworkElement? _trigger,_popupRoot,_surface,_chevron,_value;
    private TextBlock? _oldValue;
    private RotateTransform? _rotation;
    private ScaleTransform? _popupScale;
    private TranslateTransform? _popupMove,_valueMove,_oldMove;
    private BlurEffect? _valueBlur,_oldBlur;
    private readonly OwnedMotion _popupMotion=new(),_valueMotion=new(),_triggerMotion=new();
    private SolidColorBrush? _background,_border;
    private Window? _owner;
    private int _popupVersion,_valueVersion;
    private bool _releasing;
    private string _lastText="";
    private int _lastIndex=-1;
    private DispatcherOperation? _valueUpdate,_positionUpdate;
    internal bool IsMotionRunning=>_popupMotion.IsRunning||_valueMotion.IsRunning||_triggerMotion.IsRunning;
    static Select()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Select),new FrameworkPropertyMetadata(typeof(Select)));
        IsEditableProperty.OverrideMetadata(typeof(Select),new FrameworkPropertyMetadata(false,null,(_,_)=>false));
        IsDropDownOpenProperty.OverrideMetadata(typeof(Select),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,(d,_)=>((Select)d).ChangePresentation()));
        MaxDropDownHeightProperty.OverrideMetadata(typeof(Select),new FrameworkPropertyMetadata(320d));
    }
    public Select()
    {
        Unloaded+=(_,_)=>ReleasePresentation();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)ReleasePresentation();};
        Loaded+=(_,_)=>RefreshValue(false);
    }
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is ComboBoxItem;
    protected override DependencyObject GetContainerForItemOverride()=>new SelectItem();
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        _valueUpdate?.Abort();
        _valueUpdate=Dispatcher.BeginInvoke(DispatcherPriority.DataBind,new Action(()=>{_valueUpdate=null;RefreshValue(IsLoaded&&IsVisible);}));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(IsDropDownOpen&&IsEnabled&&Keyboard.Modifiers==ModifierKeys.None&&e.Key==Key.Space)
        {
            if(!e.IsRepeat)
            {
                for(int i=0;i<Items.Count;i++)
                    if(ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem{IsHighlighted:true,IsEnabled:true})
                    {SetCurrentValue(SelectedIndexProperty,i);break;}
                SetCurrentValue(IsDropDownOpenProperty,false);
            }
            e.Handled=true;return;
        }
        if(!IsDropDownOpen&&IsEnabled&&Keyboard.Modifiers==ModifierKeys.None&&e.Key is Key.Enter or Key.Space or Key.Down)
        {if(!e.IsRepeat)SetCurrentValue(IsDropDownOpenProperty,true);e.Handled=true;return;}
        base.OnKeyDown(e);
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts();base.OnApplyTemplate();
        _popup=GetTemplateChild("PART_Popup") as Popup;_trigger=GetTemplateChild("PART_Trigger") as FrameworkElement;
        _surface=GetTemplateChild("PART_MenuSurface") as FrameworkElement;_popupRoot=_popup?.Child as FrameworkElement;
        _chevron=GetTemplateChild("PART_Chevron") as FrameworkElement;_value=GetTemplateChild("PART_Value") as FrameworkElement;_oldValue=GetTemplateChild("PART_OldValue") as TextBlock;
        if(_chevron is not null){_rotation=new RotateTransform();_chevron.RenderTransform=_rotation;}
        if(_surface is not null){_popupScale=new ScaleTransform(1,1);_popupMove=new TranslateTransform();var group=new TransformGroup();group.Children.Add(_popupScale);group.Children.Add(_popupMove);_surface.RenderTransform=group;}
        if(_value is not null){_valueMove=new TranslateTransform();_valueBlur=new BlurEffect();_value.RenderTransform=_valueMove;_value.Effect=_valueBlur;}
        if(_oldValue is not null){_oldMove=new TranslateTransform();_oldBlur=new BlurEffect();_oldValue.RenderTransform=_oldMove;_oldValue.Effect=_oldBlur;}
        if(_popup is not null)
        {
            _popup.AllowsTransparency=true;_popup.PopupAnimation=PopupAnimation.None;_popup.StaysOpen=true;
            _popup.Placement=PlacementMode.Custom;_popup.PlacementTarget=_trigger;_popup.FlowDirection=FlowDirection.LeftToRight;
            _popup.CustomPopupPlacementCallback=(_,_,_)=>[new CustomPopupPlacement(_placement,PopupPrimaryAxis.None)];
            _popup.Opened+=PopupOpened;_popup.Closed+=PopupClosed;
        }
        if(_popupRoot is not null)
        {
            Bridge(DataContextProperty);Bridge(FlowDirectionProperty);Bridge(ArcMotion.LevelProperty);
            _popupRoot.SizeChanged+=PopupSized;
        }
        if(_trigger is not null)_trigger.LayoutUpdated+=AnchorLayout;
        RefreshValue(false);UpdateTrigger(false);
    }
    private void Bridge(DependencyProperty property)=>BindingOperations.SetBinding(_popupRoot!,property,new Binding{Source=this,Path=new PropertyPath(property)});
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseParts();base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(_triggerMotion is null)return;
        bool animate=IsLoaded&&IsVisible&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        if(e.Property==BackgroundProperty||e.Property==BorderBrushProperty||e.Property==IsDropDownOpenProperty||e.Property==ArcMotion.LevelProperty)UpdateTrigger(animate);
        if(e.Property==ArcMotion.LevelProperty){RefreshValue(false);if(_popup?.IsOpen==true){if(IsDropDownOpen)AnimatePopup(true);else FinishClose(_popupVersion);}}
        if(e.Property==IsEnabledProperty&&!IsEnabled)ReleasePresentation();
        if(e.Property==MaxDropDownHeightProperty||e.Property==FlowDirectionProperty)QueuePosition();
        if(e.Property==ItemTemplateProperty||e.Property==ItemTemplateSelectorProperty||e.Property==PlaceholderProperty)RefreshValue(false);
    }
    private void UpdateTrigger(bool animate)
    {
        if(_rotation is not null)_triggerMotion.Double(_rotation,RotateTransform.AngleProperty,IsDropDownOpen?180:0,.58,animate,true);
        if(_trigger is Border border)
        {
            ApplyBrush(border,Border.BackgroundProperty,Background,ref _background,animate);
            ApplyBrush(border,Border.BorderBrushProperty,BorderBrush,ref _border,animate);
        }
    }
    private void ApplyBrush(Border target,DependencyProperty property,Brush source,ref SolidColorBrush? own,bool animate)
    {
        if(source is SolidColorBrush solid){own??=new SolidColorBrush(solid.Color);target.SetValue(property,own);_triggerMotion.Color(own,solid.Color,.16,animate);}
        else{target.SetValue(property,source);own=null;}
    }
    private void ReleaseParts()
    {
        _releasing=true;
        try
        {
            // Do not erase a pre-load IsDropDownOpen request during first template creation.
            if(_popup is not null||_owner is not null||IsPresented)ReleasePresentation();
            _valueUpdate?.Abort();_valueUpdate=null;_valueMotion.Stop();_triggerMotion.Stop();++_valueVersion;
            if(_popup is not null){_popup.Opened-=PopupOpened;_popup.Closed-=PopupClosed;_popup.CustomPopupPlacementCallback=null;}
            if(_popupRoot is not null){_popupRoot.SizeChanged-=PopupSized;BindingOperations.ClearBinding(_popupRoot,DataContextProperty);BindingOperations.ClearBinding(_popupRoot,FlowDirectionProperty);BindingOperations.ClearBinding(_popupRoot,ArcMotion.LevelProperty);}
            if(_trigger is not null)_trigger.LayoutUpdated-=AnchorLayout;
            _popup=null;_trigger=_popupRoot=_surface=_chevron=_value=null;_oldValue=null;_rotation=null;_popupScale=null;_popupMove=_valueMove=_oldMove=null;_valueBlur=_oldBlur=null;_background=_border=null;
        }
        finally{_releasing=false;}
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new SelectPeer(this);
    private sealed class SelectPeer(Select owner):ComboBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore()=>nameof(Select);
        protected override string GetNameCore(){var name=base.GetNameCore();return string.IsNullOrEmpty(name)?owner.Label??"":name;}
        protected override string GetHelpTextCore(){var help=base.GetHelpTextCore();return string.IsNullOrEmpty(help)?owner.Description??"":help;}
    }
}
