using System.Collections;
using System.Collections.ObjectModel;
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

public sealed record MultiSelectOption(string Value, string Label, bool IsEnabled = true);

[TemplatePart(Name="PART_List", Type=typeof(ListBox))]
[TemplatePart(Name="PART_Search", Type=typeof(Search))]
[TemplatePart(Name="PART_Popup", Type=typeof(Popup))]
public partial class MultiSelect : Control
{
    private static DependencyProperty TextProperty(string name, string value) => DependencyProperty.Register(name,typeof(string),typeof(MultiSelect),new FrameworkPropertyMetadata(value, VisualChanged));
    public static readonly DependencyProperty LabelProperty=TextProperty(nameof(Label),"");
    public static readonly DependencyProperty DescriptionProperty=TextProperty(nameof(Description),"");
    public static readonly DependencyProperty PlaceholderProperty=TextProperty(nameof(Placeholder),"Select options");
    public static readonly DependencyProperty SearchPlaceholderProperty=TextProperty(nameof(SearchPlaceholder),"Search options");
    public static readonly DependencyProperty EmptyTextProperty=TextProperty(nameof(EmptyText),"No options found");
    public static readonly DependencyProperty OptionsProperty=DependencyProperty.Register(nameof(Options),typeof(IEnumerable<MultiSelectOption>),typeof(MultiSelect),new PropertyMetadata(null, OptionsChanged));
    public static readonly DependencyProperty SelectedValuesProperty=DependencyProperty.Register(nameof(SelectedValues),typeof(IList<string>),typeof(MultiSelect),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,ValuesChanged));
    public static readonly DependencyProperty SearchTextProperty=DependencyProperty.Register(nameof(SearchText),typeof(string),typeof(MultiSelect),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,(d,_)=>((MultiSelect)d).RefreshList()));
    public static readonly DependencyProperty IsDropDownOpenProperty=DependencyProperty.Register(nameof(IsDropDownOpen),typeof(bool),typeof(MultiSelect),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,(d,_)=>{var c=(MultiSelect)d;c.ChangePresentation();c.UpdateVisuals(true);}));
    public static readonly DependencyProperty MaxVisibleProperty=DependencyProperty.Register(nameof(MaxVisible),typeof(int),typeof(MultiSelect),new PropertyMetadata(2,VisualChanged),v=>(int)v>=0);
    public static readonly DependencyProperty MaxDropDownHeightProperty=DependencyProperty.Register(nameof(MaxDropDownHeight),typeof(double),typeof(MultiSelect),new PropertyMetadata(320d,(d,_)=>((MultiSelect)d).QueuePosition()),v=>v is double n&&double.IsFinite(n)&&n>0);
    public static readonly DependencyProperty IsSearchEnabledProperty=DependencyProperty.Register(nameof(IsSearchEnabled),typeof(bool),typeof(MultiSelect),new PropertyMetadata(true));
    public static readonly DependencyProperty CanRemoveTagsProperty=DependencyProperty.Register(nameof(CanRemoveTags),typeof(bool),typeof(MultiSelect),new PropertyMetadata(true,VisualChanged));
    public static readonly DependencyProperty IsReadOnlyProperty=DependencyProperty.Register(nameof(IsReadOnly),typeof(bool),typeof(MultiSelect),new PropertyMetadata(false,(d,_)=>{var c=(MultiSelect)d;if(c.IsReadOnly)c.ReleasePresentation();c.RefreshSelection();}));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsPresented),typeof(bool),typeof(MultiSelect),new PropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty=IsPresentedPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey HasSelectionPropertyKey=DependencyProperty.RegisterReadOnly(nameof(HasSelection),typeof(bool),typeof(MultiSelect),new PropertyMetadata(false));
    public static readonly DependencyProperty HasSelectionProperty=HasSelectionPropertyKey.DependencyProperty;
    public static readonly RoutedEvent SelectionChangedEvent=EventManager.RegisterRoutedEvent(nameof(SelectionChanged),RoutingStrategy.Bubble,typeof(RoutedEventHandler),typeof(MultiSelect));
    public event RoutedEventHandler SelectionChanged {add=>AddHandler(SelectionChangedEvent,value);remove=>RemoveHandler(SelectionChangedEvent,value);}
    public string Label {get=>(string)GetValue(LabelProperty);set=>SetValue(LabelProperty,value);}
    public string Description {get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}
    public string Placeholder {get=>(string)GetValue(PlaceholderProperty);set=>SetValue(PlaceholderProperty,value);}
    public string SearchPlaceholder {get=>(string)GetValue(SearchPlaceholderProperty);set=>SetValue(SearchPlaceholderProperty,value);}
    public string EmptyText {get=>(string)GetValue(EmptyTextProperty);set=>SetValue(EmptyTextProperty,value);}
    public IEnumerable<MultiSelectOption>? Options {get=>(IEnumerable<MultiSelectOption>?)GetValue(OptionsProperty);set=>SetValue(OptionsProperty,value);}
    public IList<string>? SelectedValues {get=>(IList<string>?)GetValue(SelectedValuesProperty);set=>SetValue(SelectedValuesProperty,value);}
    public string SearchText {get=>(string)GetValue(SearchTextProperty);set=>SetValue(SearchTextProperty,value);}
    public bool IsDropDownOpen {get=>(bool)GetValue(IsDropDownOpenProperty);set=>SetValue(IsDropDownOpenProperty,value);}
    public int MaxVisible {get=>(int)GetValue(MaxVisibleProperty);set=>SetValue(MaxVisibleProperty,value);}
    public double MaxDropDownHeight {get=>(double)GetValue(MaxDropDownHeightProperty);set=>SetValue(MaxDropDownHeightProperty,value);}
    public bool IsSearchEnabled {get=>(bool)GetValue(IsSearchEnabledProperty);set=>SetValue(IsSearchEnabledProperty,value);}
    public bool CanRemoveTags {get=>(bool)GetValue(CanRemoveTagsProperty);set=>SetValue(CanRemoveTagsProperty,value);}
    public bool IsReadOnly {get=>(bool)GetValue(IsReadOnlyProperty);set=>SetValue(IsReadOnlyProperty,value);}
    public bool IsPresented=>(bool)GetValue(IsPresentedProperty);
    public bool HasSelection=>(bool)GetValue(HasSelectionProperty);
    private ListBox? _list;
    private Search? _search;
    private System.Windows.Controls.Button? _toggle,_clear;
    private StackPanel? _tags;
    private FrameworkElement? _placeholder,_empty;
    private Popup? _popup;
    private FrameworkElement? _trigger,_popupRoot,_surface,_chevron;
    private ScaleTransform? _popupScale;
    private TranslateTransform? _popupMove;
    private Window? _owner;
    private int _popupVersion,_openDirection;
    private bool _releasing,_syncing,_composing;
    private DispatcherOperation? _positionUpdate,_focusUpdate;
    private readonly OwnedMotion _popupMotion=new();
    private List<MultiSelectOption> _all=[];
    private string[] _lastValues=[];
    internal bool IsMotionRunning=>_popupMotion.IsRunning||_visualMotion.IsRunning||_chips.Values.Any(x=>x.Motion.IsRunning);
    private string[] Values => (SelectedValues??Array.Empty<string>()).Where(v=>v is not null).Distinct(StringComparer.Ordinal).ToArray();
    static MultiSelect()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiSelect),new FrameworkPropertyMetadata(typeof(MultiSelect)));
    public MultiSelect()
    {
        SetCurrentValue(SelectedValuesProperty,new ObservableCollection<string>());
        Loaded+=(_,_)=>{RefreshList();RefreshSelection();UpdateVisuals(false);};
        Unloaded+=(_,_)=>{ReleasePresentation();StopVisuals();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){ReleasePresentation();StopVisuals();}};
    }
    private static void VisualChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)=>((MultiSelect)d).RefreshSelection();
    private static void OptionsChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(MultiSelect)d;
        if(e.OldValue is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,c.OptionsCollectionChanged);
        if(e.NewValue is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,c.OptionsCollectionChanged);
        c.RefreshList();c.RefreshSelection();
    }
    private static void ValuesChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var c=(MultiSelect)d;
        if(e.OldValue is INotifyCollectionChanged old)CollectionChangedEventManager.RemoveHandler(old,c.ValuesCollectionChanged);
        if(e.NewValue is INotifyCollectionChanged next)CollectionChangedEventManager.AddHandler(next,c.ValuesCollectionChanged);
        c.RefreshSelection();
    }
    private void OptionsCollectionChanged(object? sender,NotifyCollectionChangedEventArgs e){RefreshList();RefreshSelection();}
    private void ValuesCollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)=>RefreshSelection();
    private void RefreshList()
    {
        _all=(Options??Array.Empty<MultiSelectOption>()).Where(x=>x is not null).DistinctBy(x=>x.Value).ToList();
        if(_list is null)return;
        _syncing=true;
        try {_list.ItemsSource=_all.Where(x=>x.Label.Contains(SearchText??"",StringComparison.CurrentCultureIgnoreCase)).ToArray();SyncList();}
        finally {_syncing=false;}
        if(_empty is not null)_empty.Visibility=_list.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
        QueuePosition();
    }
    private void SyncList()
    {
        if(_list is null)return;
        var values=Values.ToHashSet(StringComparer.Ordinal);
        foreach(var option in _list.SelectedItems.Cast<MultiSelectOption>().ToArray())if(!values.Contains(option.Value))_list.SelectedItems.Remove(option);
        foreach(MultiSelectOption option in _list.Items)if(values.Contains(option.Value)&&!_list.SelectedItems.Contains(option))_list.SelectedItems.Add(option);
    }
    private void RefreshSelection()
    {
        var values=Values;SetValue(HasSelectionPropertyKey,values.Length>0);
        _syncing=true;try{SyncList();}finally{_syncing=false;}
        UpdateTags(IsLoaded&&IsVisible);UpdateVisuals(IsLoaded&&IsVisible);
        if(_toggle is not null)AutomationProperties.SetName(_toggle,$"{Label}: {(values.Length==0?Placeholder:string.Join(", ",values.Select(LabelFor)))}");
        if(!_lastValues.SequenceEqual(values)){_lastValues=values;RaiseEvent(new RoutedEventArgs(SelectionChangedEvent,this));}
    }
    private string LabelFor(string value)=>_all.FirstOrDefault(x=>x.Value==value)?.Label??value;
    private void Commit(IEnumerable<string> values)
    {if(IsEnabled&&!IsReadOnly)SetCurrentValue(SelectedValuesProperty,values.Distinct(StringComparer.Ordinal).ToArray());}
    public void ClearSelection(){if(!IsEnabled||IsReadOnly)return;Commit([]);SetCurrentValue(IsDropDownOpenProperty,false);_toggle?.Focus();}
    public void RemoveValue(string value){if(!IsEnabled||IsReadOnly)return;Commit(Values.Where(x=>x!=value));_toggle?.Focus();}
    private void ListSelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_syncing)return;
        if(!IsEnabled||IsReadOnly){RefreshSelection();return;}
        var next=Values.ToList();
        foreach(MultiSelectOption option in e.RemovedItems)if(option.IsEnabled)next.Remove(option.Value);
        foreach(MultiSelectOption option in e.AddedItems)if(option.IsEnabled&&!next.Contains(option.Value))next.Add(option.Value);
        Commit(next);RefreshSelection();
    }
    public override void OnApplyTemplate()
    {
        ReleaseTemplate();base.OnApplyTemplate();
        _trigger=GetTemplateChild("PART_Trigger") as FrameworkElement;_toggle=GetTemplateChild("PART_Toggle") as System.Windows.Controls.Button;
        _clear=GetTemplateChild("PART_Clear") as System.Windows.Controls.Button;_tags=GetTemplateChild("PART_Tags") as StackPanel;
        _placeholder=GetTemplateChild("PART_Placeholder") as FrameworkElement;_chevron=GetTemplateChild("PART_Chevron") as FrameworkElement;
        _popup=GetTemplateChild("PART_Popup") as Popup;_popupRoot=GetTemplateChild("PART_MenuFrame") as FrameworkElement;
        _surface=GetTemplateChild("PART_MenuSurface") as FrameworkElement;_list=GetTemplateChild("PART_List") as ListBox;
        _search=GetTemplateChild("PART_Search") as Search;_empty=GetTemplateChild("PART_Empty") as FrameworkElement;
        if(_toggle is not null){_toggle.Click+=ToggleClicked;_toggle.PreviewKeyDown+=TriggerKeyDown;}
        if(_clear is not null)_clear.Click+=ClearClicked;
        if(_list is not null)_list.SelectionChanged+=ListSelectionChanged;
        if(_popup is not null){_popup.Placement=PlacementMode.Custom;_popup.PlacementTarget=_trigger;_popup.CustomPopupPlacementCallback=(_,_,_)=>[new(_placement,PopupPrimaryAxis.None)];_popup.Opened+=PopupOpened;_popup.Closed+=PopupClosed;}
        if(_popupRoot is not null)
        {
            foreach(var dp in new[]{DataContextProperty,FlowDirectionProperty,ArcMotion.LevelProperty})BindingOperations.SetBinding(_popupRoot,dp,new Binding{Source=this,Path=new PropertyPath(dp)});
            _popupRoot.PreviewKeyDown+=MenuKeyDown;_popupRoot.SizeChanged+=PopupSized;
            _popupRoot.AddHandler(TextCompositionManager.PreviewTextInputStartEvent,new TextCompositionEventHandler(CompositionStarted),true);
            _popupRoot.AddHandler(TextCompositionManager.PreviewTextInputEvent,new TextCompositionEventHandler(CompositionEnded),true);
        }
        if(_surface is not null){_popupScale=new();_popupMove=new();var g=new TransformGroup();g.Children.Add(_popupScale);g.Children.Add(_popupMove);_surface.RenderTransform=g;}
        if(_trigger is not null){_trigger.LayoutUpdated+=AnchorLayout;_trigger.MouseLeftButtonDown+=TriggerMouseDown;}
        InitializeVisuals();RefreshList();RefreshSelection();
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){ReleaseTemplate();base.OnTemplateChanged(oldTemplate,newTemplate);}
    private void ReleaseTemplate()
    {
        _releasing=true;
        try
        {
            ReleasePresentation();StopVisuals();_chips.Clear();
            if(_toggle is not null){_toggle.Click-=ToggleClicked;_toggle.PreviewKeyDown-=TriggerKeyDown;}
            if(_clear is not null)_clear.Click-=ClearClicked;
            if(_list is not null)_list.SelectionChanged-=ListSelectionChanged;
            if(_trigger is not null){_trigger.LayoutUpdated-=AnchorLayout;_trigger.MouseLeftButtonDown-=TriggerMouseDown;}
            if(_valueArea is not null)_valueArea.SizeChanged-=ValueAreaSized;
            if(_popup is not null){_popup.Opened-=PopupOpened;_popup.Closed-=PopupClosed;_popup.CustomPopupPlacementCallback=null;}
            if(_popupRoot is not null){_popupRoot.PreviewKeyDown-=MenuKeyDown;_popupRoot.SizeChanged-=PopupSized;foreach(var dp in new[]{DataContextProperty,FlowDirectionProperty,ArcMotion.LevelProperty})BindingOperations.ClearBinding(_popupRoot,dp);_popupRoot.RemoveHandler(TextCompositionManager.PreviewTextInputStartEvent,new TextCompositionEventHandler(CompositionStarted));_popupRoot.RemoveHandler(TextCompositionManager.PreviewTextInputEvent,new TextCompositionEventHandler(CompositionEnded));}
            _list=null;_search=null;_toggle=_clear=null;_tags=null;_placeholder=_empty=_trigger=_popupRoot=_surface=_chevron=null;_popup=null;_composing=false;_clearVisual=_valueArea=null;_rotation=null;_clearScale=null;_clearBlur=null;
        }finally{_releasing=false;}
    }
    private void TriggerMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(e.Handled||IsReadOnly||!IsEnabled)return;
        for(var n=e.OriginalSource as DependencyObject;n is not null&&n!=_trigger;n=VisualTreeHelper.GetParent(n))if(n is System.Windows.Controls.Primitives.ButtonBase)return;
        _toggle?.Focus();SetCurrentValue(IsDropDownOpenProperty,!IsDropDownOpen);e.Handled=true;
    }
    private void ToggleClicked(object sender,RoutedEventArgs e){if(!IsReadOnly)SetCurrentValue(IsDropDownOpenProperty,!IsDropDownOpen);}
    private void ClearClicked(object sender,RoutedEventArgs e)=>ClearSelection();
    private void CompositionStarted(object sender,TextCompositionEventArgs e)=>_composing=true;
    private void CompositionEnded(object sender,TextCompositionEventArgs e)=>_composing=false;
    private void TriggerKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key is Key.Down or Key.Up){if(IsReadOnly||!IsEnabled)return;int direction=e.Key==Key.Down?1:-1;if(IsDropDownOpen)MoveActive(direction);else{_openDirection=direction;SetCurrentValue(IsDropDownOpenProperty,true);}e.Handled=true;}
        else if(e.Key==Key.Escape){SetCurrentValue(IsDropDownOpenProperty,false);e.Handled=true;}
    }
    private void MoveActive(int direction)
    {
        if(_list is null)return;_list.UpdateLayout();
        var enabled=_list.Items.Cast<MultiSelectOption>().Where(x=>x.IsEnabled).ToList();if(enabled.Count==0)return;
        var focused=Keyboard.FocusedElement as DependencyObject;
        var container=focused is null?null:ItemsControl.ContainerFromElement(_list,focused) as ListBoxItem;
        int index=container?.Content is MultiSelectOption o?enabled.IndexOf(o):-1;
        index=index<0?(direction>0?0:enabled.Count-1):(index+direction+enabled.Count)%enabled.Count;
        var item=enabled[index];_list.ScrollIntoView(item);_list.UpdateLayout();(_list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem)?.Focus();
    }
    private void MenuKeyDown(object sender,KeyEventArgs e)
    {
        if(_composing||e.Key is Key.ImeProcessed or Key.DeadCharProcessed)return;
        if(e.Key==Key.Escape){SetCurrentValue(IsDropDownOpenProperty,false);_toggle?.Focus();e.Handled=true;}
        else if(e.Key==Key.Tab){SetCurrentValue(IsDropDownOpenProperty,false);_toggle?.Focus();MoveOutside(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));e.Handled=true;}
        else if(e.Key is Key.Down or Key.Up){MoveActive(e.Key==Key.Down?1:-1);e.Handled=true;}
        else if(e.Key==Key.Enter && _list is not null && Keyboard.FocusedElement is DependencyObject focused && ItemsControl.ContainerFromElement(_list,focused) is ListBoxItem item){item.SetCurrentValue(ListBoxItem.IsSelectedProperty,!item.IsSelected);e.Handled=true;}
    }
    private void MoveOutside(bool backwards)
    {
        var direction=backwards?FocusNavigationDirection.Previous:FocusNavigationDirection.Next;
        UIElement? current=_toggle;
        // Skip the field's internal actions when leaving the popup by Tab.
        for(int i=0;current is not null&&i<MaxVisible+8;i++)
        {
            if(!current.MoveFocus(new TraversalRequest(direction)))break;
            var next=Keyboard.FocusedElement as UIElement;
            if(next is null||!Popover.IsWithin(next,this))break;
            if(ReferenceEquals(next,current))break;current=next;
        }
    }
    private void OutsideInput(object sender,PreProcessInputEventArgs e)
    {
        if(!IsDropDownOpen||e.StagingItem.Input is not MouseButtonEventArgs mouse||mouse.RoutedEvent!=Mouse.PreviewMouseDownEvent)return;
        var source=mouse.OriginalSource as DependencyObject??mouse.MouseDevice.DirectlyOver as DependencyObject;
        if(source is not null&&!Popover.IsWithin(source,_popupRoot)&&!Popover.IsWithin(source,_trigger))SetCurrentValue(IsDropDownOpenProperty,false);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(_visualMotion is null)return;
        if(e.Property==IsEnabledProperty&&!IsEnabled)ReleasePresentation();
        if(e.Property==ArcMotion.LevelProperty){UpdateTags(false);if(IsPresented)AnimatePopup(IsDropDownOpen);}
        if(e.Property==BackgroundProperty||e.Property==BorderBrushProperty||e.Property==IsMouseOverProperty||e.Property==IsEnabledProperty||e.Property==ArcMotion.LevelProperty)UpdateVisuals(IsLoaded&&IsVisible);
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new Peer(this);
    private sealed class Peer(MultiSelect owner):FrameworkElementAutomationPeer(owner),IExpandCollapseProvider
    {
        protected override string GetClassNameCore()=>nameof(MultiSelect);
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.ComboBox;
        protected override string GetNameCore()=>string.IsNullOrEmpty(base.GetNameCore())?owner.Label:base.GetNameCore();
        protected override string GetHelpTextCore()=>owner.Description;
        public override object? GetPattern(PatternInterface pattern)=>pattern==PatternInterface.ExpandCollapse?this:base.GetPattern(pattern);
        public ExpandCollapseState ExpandCollapseState=>owner.IsDropDownOpen?ExpandCollapseState.Expanded:ExpandCollapseState.Collapsed;
        public void Expand(){if(!owner.IsEnabled)throw new ElementNotEnabledException();owner.SetCurrentValue(IsDropDownOpenProperty,true);}
        public void Collapse()=>owner.SetCurrentValue(IsDropDownOpenProperty,false);
    }
}
