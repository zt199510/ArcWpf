using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Controls;

public sealed record ComboBoxOption(string Value, string Label, bool IsEnabled = true, IReadOnlyList<string>? Keywords = null);

/// <summary>A native text editor with a searchable, single-choice popup. Bind SelectedValue for selection.</summary>
[TemplatePart(Name="PART_ContentHost",Type=typeof(ScrollViewer))]
[TemplatePart(Name="PART_List",Type=typeof(ListBox))]
[TemplatePart(Name="PART_Popup",Type=typeof(Popup))]
public partial class ComboBox : Input
{
    public static readonly DependencyProperty OptionsProperty=DependencyProperty.Register(nameof(Options),typeof(IEnumerable<ComboBoxOption>),typeof(ComboBox),new PropertyMetadata(null,OptionsChanged));
    public static readonly DependencyProperty SelectedValueProperty=DependencyProperty.Register(nameof(SelectedValue),typeof(string),typeof(ComboBox),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,SelectionValueChanged,(_,v)=>v??""));
    public static readonly DependencyProperty QueryTextProperty=DependencyProperty.Register(nameof(QueryText),typeof(string),typeof(ComboBox),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,QueryChanged,(_,v)=>v??""));
    public static readonly DependencyProperty IsDropDownOpenProperty=DependencyProperty.Register(nameof(IsDropDownOpen),typeof(bool),typeof(ComboBox),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,OpenChanged));
    public static readonly DependencyProperty MaxDropDownHeightProperty=DependencyProperty.Register(nameof(MaxDropDownHeight),typeof(double),typeof(ComboBox),new PropertyMetadata(300d,(d,_)=>((ComboBox)d).QueuePosition()),v=>v is double x&&double.IsFinite(x)&&x>0);
    public static readonly DependencyProperty EmptyMessageProperty=DependencyProperty.Register(nameof(EmptyMessage),typeof(string),typeof(ComboBox),new PropertyMetadata("No matches found"));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(ComboBox),new PropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey HasSelectionPropertyKey=DependencyProperty.RegisterReadOnly(nameof(HasSelection),typeof(bool),typeof(ComboBox),new PropertyMetadata(false));
    public static readonly DependencyProperty HasSelectionProperty=HasSelectionPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey DisplayPlaceholderPropertyKey=DependencyProperty.RegisterReadOnly(nameof(DisplayPlaceholder),typeof(string),typeof(ComboBox),new PropertyMetadata(""));
    public static readonly DependencyProperty DisplayPlaceholderProperty=DisplayPlaceholderPropertyKey.DependencyProperty;
    public static readonly RoutedEvent SelectedValueChangedEvent=EventManager.RegisterRoutedEvent(nameof(SelectedValueChanged),RoutingStrategy.Bubble,typeof(RoutedEventHandler),typeof(ComboBox));
    public event RoutedEventHandler SelectedValueChanged {add=>AddHandler(SelectedValueChangedEvent,value);remove=>RemoveHandler(SelectedValueChangedEvent,value);}
    public IEnumerable<ComboBoxOption>? Options {get=>(IEnumerable<ComboBoxOption>?)GetValue(OptionsProperty);set=>SetValue(OptionsProperty,value);}
    public string SelectedValue {get=>(string)GetValue(SelectedValueProperty);set=>SetValue(SelectedValueProperty,value);}
    public string QueryText {get=>(string)GetValue(QueryTextProperty);set=>SetValue(QueryTextProperty,value);}
    public bool IsDropDownOpen {get=>(bool)GetValue(IsDropDownOpenProperty);set=>SetValue(IsDropDownOpenProperty,value);}
    public double MaxDropDownHeight {get=>(double)GetValue(MaxDropDownHeightProperty);set=>SetValue(MaxDropDownHeightProperty,value);}
    public string EmptyMessage {get=>(string)GetValue(EmptyMessageProperty);set=>SetValue(EmptyMessageProperty,value);}
    public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
    public bool HasSelection=>(bool)GetValue(HasSelectionProperty);
    public string DisplayPlaceholder=>(string)GetValue(DisplayPlaceholderProperty);

    private List<ComboBoxOption> _options=[];
    private ComboBoxOption[] _filtered=[];
    private ListBox? _list;
    private Popup? _popup;
    private FrameworkElement? _trigger,_surface,_popupRoot,_chevron;
    private System.Windows.Controls.Button? _clear;
    private Window? _owner;
    private ScaleTransform? _popupScale;
    private TranslateTransform? _popupMove;
    private readonly OwnedMotion _popupMotion=new();
    private DispatcherOperation? _positionUpdate,_focusUpdate;
    private int _popupVersion,_activeIndex=-1;
    private bool _keyboardFocus;
    private bool _releasing,_syncText,_syncList,_composing,_suppressFocusOpen;
    internal bool IsMotionRunning=>_popupMotion.IsRunning||_visualMotion.IsRunning||_heightMotion.IsRunning||_textMotion.IsRunning||_emptyMotion.IsRunning;
    private ComboBoxOption? SelectedOption=>_options.FirstOrDefault(x=>x.Value==SelectedValue&&x.Value.Length>0);

    static ComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ComboBox),new FrameworkPropertyMetadata(typeof(ComboBox)));
        PlaceholderProperty.OverrideMetadata(typeof(ComboBox),new FrameworkPropertyMetadata("Search or select…"));
        AcceptsReturnProperty.OverrideMetadata(typeof(ComboBox),new FrameworkPropertyMetadata(false,null,(_,_)=>false));
        AcceptsTabProperty.OverrideMetadata(typeof(ComboBox),new FrameworkPropertyMetadata(false,null,(_,_)=>false));
    }
    public ComboBox()
    {
        AddHandler(TextCompositionManager.PreviewTextInputStartEvent,new TextCompositionEventHandler(CompositionStarted),true);
        AddHandler(TextCompositionManager.PreviewTextInputUpdateEvent,new TextCompositionEventHandler(CompositionStarted),true);
        AddHandler(TextCompositionManager.PreviewTextInputEvent,new TextCompositionEventHandler(CompositionEnded),true);
        Loaded+=(_,_)=>{RefreshOptions();RefreshDisplay();UpdateVisuals(false);};
        Unloaded+=(_,_)=>{ReleasePresentation();StopVisuals();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){ReleasePresentation();StopVisuals();}};
    }
    private static void OptionsChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(ComboBox)d;
        if(e.OldValue is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,c.OptionsCollectionChanged);
        if(e.NewValue is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,c.OptionsCollectionChanged);
        c.RefreshOptions();c.RefreshDisplay();
    }
    private void OptionsCollectionChanged(object? sender,NotifyCollectionChangedEventArgs e){RefreshOptions();RefreshDisplay();}
    private static void SelectionValueChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(ComboBox)d;c.SyncSelection();c.RefreshDisplay();c.UpdateVisuals(c.IsLoaded);
        if(!c.IsDropDownOpen)c.AnimateText(!string.IsNullOrEmpty((string)e.NewValue));
        c.RaiseEvent(new RoutedEventArgs(SelectedValueChangedEvent,c));
    }
    private static void QueryChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(ComboBox)d;if(c.IsDropDownOpen&&!c._composing)c.SetEditor(c.QueryText);
        c.RefreshFilter();
    }
    private static void OpenChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(ComboBox)d;
        if(!(bool)e.NewValue){c.SetCurrentValue(QueryTextProperty,"");c._activeIndex=-1;}
        c.ChangePresentation();c.RefreshDisplay();c.UpdateVisuals(c.IsLoaded);
        if(UIElementAutomationPeer.FromElement(c) is ComboBoxPeer peer)peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,(bool)e.OldValue?ExpandCollapseState.Expanded:ExpandCollapseState.Collapsed,c.IsDropDownOpen?ExpandCollapseState.Expanded:ExpandCollapseState.Collapsed);
    }
    private void RefreshOptions()
    {
        _options=(Options??Array.Empty<ComboBoxOption>()).Where(x=>x is not null&& !string.IsNullOrEmpty(x.Value)).DistinctBy(x=>x.Value).ToList();
        RefreshFilter();
    }
    private void RefreshFilter()
    {
        string query=QueryText.Trim();
        _filtered=_options.Where(x=>query.Length==0||x.Label.Contains(query,StringComparison.CurrentCultureIgnoreCase)||(x.Keywords?.Any(k=>k.Contains(query,StringComparison.CurrentCultureIgnoreCase))??false)).ToArray();
        _activeIndex=-1;
        if(_list is not null){_syncList=true;try{_list.ItemsSource=_filtered;SyncSelection();}finally{_syncList=false;}}
        UpdateEmpty();QueuePosition();
    }
    private void SyncSelection()
    {
        if(_list is null)return;
        bool previous=_syncList;_syncList=true;
        try{_list.SelectedItem=_filtered.FirstOrDefault(x=>x.Value==SelectedValue);}finally{_syncList=previous;}
    }
    private void SetEditor(string text)
    {
        if(Text==text)return;
        _syncText=true;try{SetCurrentValue(TextProperty,text);}finally{_syncText=false;}
    }
    private void RefreshDisplay()
    {
        var selected=SelectedOption;SetValue(HasSelectionPropertyKey,selected is not null);
        SetValue(DisplayPlaceholderPropertyKey,selected?.Label??Placeholder??"");
        if(!_composing)SetEditor(IsDropDownOpen?QueryText:selected?.Label??"");
        UpdateVisuals(IsLoaded);
    }
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if(_syncText||_releasing||_options is null)return;
        // Native editing owns this text change; opening must not replace an IME composition or undo transaction.
        SetCurrentValue(QueryTextProperty,Text);
        if(IsLoaded&&IsEnabled&&!IsReadOnly&&!IsDropDownOpen)SetCurrentValue(IsDropDownOpenProperty,true);
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {base.OnGotKeyboardFocus(e);_keyboardFocus=InputManager.Current.MostRecentInputDevice is KeyboardDevice;UpdateVisuals(true);if(ReferenceEquals(e.NewFocus,this)&&!_suppressFocusOpen&&!IsReadOnly)OpenMenu();}
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);_keyboardFocus=false;UpdateVisuals(true);
        if(!Popover.IsWithin(e.NewFocus as DependencyObject,this)&&!Popover.IsWithin(e.NewFocus as DependencyObject,_popupRoot)){_composing=false;SetCurrentValue(IsDropDownOpenProperty,false);RefreshDisplay();}
    }
    private void OpenMenu()
    {
        if(!IsEnabled||IsReadOnly||!IsLoaded)return;
        if(!IsDropDownOpen){SetCurrentValue(QueryTextProperty,"");SetCurrentValue(IsDropDownOpenProperty,true);}
    }
    private void CompositionStarted(object sender,TextCompositionEventArgs e)=>_composing=true;
    private void CompositionEnded(object sender,TextCompositionEventArgs e)=>_composing=false;
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        _keyboardFocus=true;UpdateVisuals(true);
        if(ReferenceEquals(e.OriginalSource,this)&&!_composing&&IsEnabled&&!IsReadOnly&&e.Key is not (Key.ImeProcessed or Key.DeadCharProcessed))
        {
            if(e.Key is Key.Down or Key.Up){if(!IsDropDownOpen)OpenMenu();else MoveActive(e.Key==Key.Down?1:-1);e.Handled=true;}
            else if(e.Key==Key.Enter&&IsDropDownOpen&&_activeIndex>=0){Choose(_filtered[_activeIndex]);e.Handled=true;}
            else if(e.Key==Key.Escape&&IsDropDownOpen){SetCurrentValue(IsDropDownOpenProperty,false);e.Handled=true;}
            else if(e.Key==Key.Tab&&IsDropDownOpen)SetCurrentValue(IsDropDownOpenProperty,false);
        }
        base.OnPreviewKeyDown(e);
    }
    private void MoveActive(int direction)
    {
        var enabled=Enumerable.Range(0,_filtered.Length).Where(i=>_filtered[i].IsEnabled).ToArray();if(enabled.Length==0)return;
        int current=Array.IndexOf(enabled,_activeIndex);int next=current<0?(direction>0?0:enabled.Length-1):(current+direction+enabled.Length)%enabled.Length;
        SetActive(enabled[next]);
    }
    private void SetActive(int index)
    {
        _activeIndex=index;
        if(_list is null)return;
        _list.UpdateLayout();
        for(int i=0;i<_filtered.Length;i++)if(_list.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxOptionItem row)row.SetCurrentValue(ComboBoxOptionItem.IsActiveProperty,i==index);
        if(index>=0&&index<_filtered.Length)_list.ScrollIntoView(_filtered[index]);
    }
    private void Choose(ComboBoxOption option)
    {
        if(!IsEnabled||IsReadOnly||!option.IsEnabled||_composing)return;
        SetCurrentValue(SelectedValueProperty,option.Value);SetCurrentValue(IsDropDownOpenProperty,false);
        _suppressFocusOpen=true;try{Focus();}finally{_suppressFocusOpen=false;}
        AnimateText(true);
    }
    public void ClearSelection()
    {
        if(!IsEnabled||IsReadOnly||_composing)return;
        SetCurrentValue(SelectedValueProperty,"");SetCurrentValue(QueryTextProperty,"");OpenMenu();Focus();AnimateText(false);
    }
    private void ListSelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_syncList||_list?.SelectedItem is not ComboBoxOption option)return;
        if(!IsDropDownOpen||!option.IsEnabled||IsReadOnly||!IsEnabled){SyncSelection();return;}
        Choose(option);
    }
    private void ListMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(_list is null||e.ChangedButton!=MouseButton.Left)return;
        if(ItemsControl.ContainerFromElement(_list,e.OriginalSource as DependencyObject) is ListBoxItem {Content:ComboBoxOption option}){Choose(option);e.Handled=true;}
    }
    private void ListMouseMove(object sender,MouseEventArgs e)
    {
        if(_list is null)return;
        if(ItemsControl.ContainerFromElement(_list,e.OriginalSource as DependencyObject) is ListBoxItem {Content:ComboBoxOption {IsEnabled:true} option})SetActive(Array.IndexOf(_filtered,option));
    }
    private void TriggerMouseDown(object sender,MouseButtonEventArgs e)
    {
        _keyboardFocus=false;UpdateVisuals(true);
        if(Popover.IsWithin(e.OriginalSource as DependencyObject,_clear))return;
        if(!IsDropDownOpen)OpenMenu();
    }
    private void ClearClicked(object sender,RoutedEventArgs e){ClearSelection();e.Handled=true;}
    private void OutsideInput(object sender,PreProcessInputEventArgs e)
    {
        if(!IsDropDownOpen||e.StagingItem.Input is not MouseButtonEventArgs mouse||mouse.RoutedEvent!=Mouse.PreviewMouseDownEvent)return;
        var source=mouse.OriginalSource as DependencyObject??mouse.MouseDevice.DirectlyOver as DependencyObject;
        if(source is not null&&!Popover.IsWithin(source,_popupRoot)&&!Popover.IsWithin(source,_trigger))SetCurrentValue(IsDropDownOpenProperty,false);
    }
    public override void OnApplyTemplate()
    {
        ReleaseParts();base.OnApplyTemplate();
        _trigger=GetTemplateChild("PART_Trigger") as FrameworkElement;_popup=GetTemplateChild("PART_Popup") as Popup;
        _surface=GetTemplateChild("PART_MenuSurface") as FrameworkElement;_popupRoot=GetTemplateChild("PART_MenuFrame") as FrameworkElement;
        _chevron=GetTemplateChild("PART_Chevron") as FrameworkElement;_clear=GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;_list=GetTemplateChild("PART_List") as ListBox;
        if(_popup is not null){_popup.PlacementTarget=_trigger;_popup.Placement=PlacementMode.Custom;_popup.CustomPopupPlacementCallback=(_,_,_)=>[new(_placement,PopupPrimaryAxis.None)];_popup.Opened+=PopupOpened;_popup.Closed+=PopupClosed;}
        if(_surface is not null){_popupScale=new();_popupMove=new();var g=new TransformGroup();g.Children.Add(_popupScale);g.Children.Add(_popupMove);_surface.RenderTransform=g;}
        if(_trigger is not null){_trigger.LayoutUpdated+=AnchorLayout;_trigger.PreviewMouseLeftButtonDown+=TriggerMouseDown;}
        if(_clear is not null)_clear.Click+=ClearClicked;
        if(_list is not null){_list.SelectionChanged+=ListSelectionChanged;_list.PreviewMouseDown+=ListMouseDown;_list.MouseMove+=ListMouseMove;}
        if(_popupRoot is not null){_popupRoot.SizeChanged+=PopupSized;foreach(var dp in new[]{DataContextProperty,FlowDirectionProperty,ArcMotion.LevelProperty})BindingOperations.SetBinding(_popupRoot,dp,new Binding{Source=this,Path=new PropertyPath(dp)});}
        InitializeVisuals();RefreshOptions();RefreshDisplay();UpdateVisuals(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseParts();base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void ReleaseParts()
    {
        _releasing=true;
        try
        {
            ReleasePresentation();StopVisuals();
            if(_trigger is not null){_trigger.LayoutUpdated-=AnchorLayout;_trigger.PreviewMouseLeftButtonDown-=TriggerMouseDown;}
            if(_clear is not null)_clear.Click-=ClearClicked;
            if(_list is not null){_list.SelectionChanged-=ListSelectionChanged;_list.PreviewMouseDown-=ListMouseDown;_list.MouseMove-=ListMouseMove;}
            if(_popup is not null){_popup.Opened-=PopupOpened;_popup.Closed-=PopupClosed;_popup.CustomPopupPlacementCallback=null;}
            if(_popupRoot is not null){_popupRoot.SizeChanged-=PopupSized;foreach(var dp in new[]{DataContextProperty,FlowDirectionProperty,ArcMotion.LevelProperty})BindingOperations.ClearBinding(_popupRoot,dp);}
            _popup=null;_surface=_popupRoot=_chevron=_trigger=null;_clear=null;_list=null;ClearVisualParts();
        }finally{_releasing=false;}
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);if(_visualMotion is null)return;
        if((e.Property==IsEnabledProperty&&!IsEnabled)||(e.Property==IsReadOnlyProperty&&IsReadOnly)){ReleasePresentation();StopVisuals();}
        if(e.Property==PlaceholderProperty)RefreshDisplay();
        if(e.Property==ArcMotion.LevelProperty){StopVisuals();if(IsPresented)AnimatePopup(IsDropDownOpen);QueuePosition();}
        if(e.Property==BackgroundProperty||e.Property==BorderBrushProperty||e.Property==IsEnabledProperty||e.Property==IsReadOnlyProperty||e.Property==ArcMotion.LevelProperty)UpdateVisuals(IsLoaded&&IsVisible);
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new ComboBoxPeer(this);
    private sealed class ComboBoxPeer(ComboBox owner):TextBoxAutomationPeer(owner),IExpandCollapseProvider
    {
        protected override string GetClassNameCore()=>nameof(ComboBox);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.ComboBox;
        protected override string GetNameCore()=>string.IsNullOrEmpty(base.GetNameCore())?owner.Label:base.GetNameCore();
        protected override string GetHelpTextCore()=>owner.HasError?owner.EffectiveErrorMessage:owner.Description;
        public override object? GetPattern(PatternInterface pattern)=>pattern==PatternInterface.ExpandCollapse?this:base.GetPattern(pattern);
        public ExpandCollapseState ExpandCollapseState=>owner.IsDropDownOpen?ExpandCollapseState.Expanded:ExpandCollapseState.Collapsed;
        public void Expand(){if(!owner.IsEnabled)throw new ElementNotEnabledException();owner.OpenMenu();}
        public void Collapse()=>owner.SetCurrentValue(IsDropDownOpenProperty,false);
        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var children=base.GetChildrenCore()??[];
            if(owner._clear is {IsVisible:true,IsEnabled:true} button&&UIElementAutomationPeer.CreatePeerForElement(button) is {} clear&&!children.Contains(clear))children.Add(clear);
            if(owner.IsPresented&&owner._list is not null&&UIElementAutomationPeer.CreatePeerForElement(owner._list) is {} list&&!children.Contains(list))children.Add(list);
            return children;
        }
    }
}
