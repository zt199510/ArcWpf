using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_Highlight",Type=typeof(Border))]
[TemplatePart(Name="PART_ItemsHost",Type=typeof(FrameworkElement))]
public class RadioGroup : HeaderedItemsControl
{
    public static readonly DependencyProperty SelectedValueProperty=DependencyProperty.Register(nameof(SelectedValue),typeof(object),typeof(RadioGroup),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,SelectionChanged));
    public static readonly RoutedEvent SelectedValueChangedEvent=EventManager.RegisterRoutedEvent(nameof(SelectedValueChanged),RoutingStrategy.Bubble,typeof(RoutedPropertyChangedEventHandler<object>),typeof(RadioGroup));
    public object? SelectedValue {get=>GetValue(SelectedValueProperty);set=>SetValue(SelectedValueProperty,value);}
    public event RoutedPropertyChangedEventHandler<object> SelectedValueChanged {add=>AddHandler(SelectedValueChangedEvent,value);remove=>RemoveHandler(SelectedValueChangedEvent,value);}
    private readonly string _name="ArcWpf.RadioGroup."+Guid.NewGuid().ToString("N");
    private readonly Dictionary<RadioButton,object> _rows=new();
    private bool _sync,_rendering,_placed;
    private RadioButton? _selected;
    private Border? _highlight;
    private FrameworkElement? _host;
    private readonly ControlSpring _y=new(0,.42),_height=new(0,.42);
    private TranslateTransform? _translate;
    private long _lastFrame;
    private double _lastHostWidth;
    internal bool IsMotionRunning=>_rendering;
    static RadioGroup()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioGroup),new FrameworkPropertyMetadata(typeof(RadioGroup)));
    public RadioGroup()
    {
        AddHandler(System.Windows.Controls.RadioButton.CheckedEvent,new RoutedEventHandler(RowChecked));
        Loaded+=(_,_)=>{RefreshSelection();Place(false);};
        Unloaded+=(_,_)=>{StopFrames();_placed=false;if(_highlight is not null)_highlight.Opacity=0;};
        LayoutUpdated+=(_,_)=>Place(false);
    }
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is RadioButton;
    protected override DependencyObject GetContainerForItemOverride()=>new RadioButton();
    protected override void PrepareContainerForItemOverride(DependencyObject element,object item)
    {
        base.PrepareContainerForItemOverride(element,item);
        if(element is not RadioButton row)return;
        row.PreviousGroupName=row.GroupName;row.PreviousIsTabStop=KeyboardNavigation.GetIsTabStop(row);row.GroupOwner=this;row.SetCurrentValue(System.Windows.Controls.RadioButton.GroupNameProperty,_name);
        _rows[row]=item;
        row.IsEnabledChanged+=RowAvailabilityChanged;row.IsVisibleChanged+=RowAvailabilityChanged;
        RefreshSelection();
    }
    protected override void ClearContainerForItemOverride(DependencyObject element,object item)
    {
        if(element is RadioButton row)
        {
            _rows.Remove(row);row.IsEnabledChanged-=RowAvailabilityChanged;row.IsVisibleChanged-=RowAvailabilityChanged;
            row.GroupOwner=null;row.SetCurrentValue(System.Windows.Controls.RadioButton.GroupNameProperty,row.PreviousGroupName);
            row.SetCurrentValue(KeyboardNavigation.IsTabStopProperty,row.PreviousIsTabStop);row.UpdateVisual(false);
        }
        base.ClearContainerForItemOverride(element,item);RefreshSelection();
    }
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e){base.OnItemsChanged(e);RefreshSelection();}
    private void RowAvailabilityChanged(object sender,DependencyPropertyChangedEventArgs e)=>RefreshSelection();
    private object? ValueFor(RadioButton row)=>row.Value??(_rows.TryGetValue(row,out var item)?item:null);
    internal IReadOnlyList<RadioButton> OrderedRows()=>_rows.Keys.OrderBy(r=>Items.IndexOf(_rows[r])).ToArray();
    private void RowChecked(object sender,RoutedEventArgs e)
    {
        if(!_sync&&e.OriginalSource is RadioButton row&&ReferenceEquals(row.GroupOwner,this))SetCurrentValue(SelectedValueProperty,ValueFor(row));
    }
    private static void SelectionChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var group=(RadioGroup)d;group.RefreshSelection();
        group.RaiseEvent(new RoutedPropertyChangedEventArgs<object>(e.OldValue,e.NewValue,SelectedValueChangedEvent));
    }
    internal void RefreshSelection()
    {
        if(_sync||_rows is null)return;
        _sync=true;
        RadioButton? previous=_selected;
        try
        {
            var rows=OrderedRows();_selected=SelectedValue is null?null:rows.FirstOrDefault(row=>Equals(ValueFor(row),SelectedValue));
            foreach(var row in rows)row.SetCurrentValue(ToggleButton.IsCheckedProperty,ReferenceEquals(row,_selected));
            var entry=_selected is {IsEnabled:true,Visibility:Visibility.Visible}?_selected:rows.FirstOrDefault(row=>row.IsEnabled&&row.Visibility==Visibility.Visible);
            foreach(var row in rows)row.SetCurrentValue(KeyboardNavigation.IsTabStopProperty,ReferenceEquals(row,entry));
        }
        finally {_sync=false;}
        Place(previous is not null&&_selected!=previous);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if(e.Handled||Keyboard.Modifiers!=ModifierKeys.None||e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right))return;
        var rows=OrderedRows();
        var focused=rows.FirstOrDefault(row=>row.IsKeyboardFocusWithin);
        if(focused is null)return;
        bool forward=e.Key is Key.Down or Key.Right;
        if(FlowDirection==FlowDirection.RightToLeft&&e.Key is Key.Left or Key.Right)forward=!forward;
        var next=FindNext(focused,forward);if(next is null)return;
        next.Focus();SetCurrentValue(SelectedValueProperty,ValueFor(next));e.Handled=true;
    }
    internal RadioButton? FindNext(RadioButton current,bool forward)
    {
        var eligible=OrderedRows().Where(r=>r.IsEnabled&&r.Visibility==Visibility.Visible).ToArray();
        if(eligible.Length==0)return null;
        int index=Array.IndexOf(eligible,current);
        return eligible[(index+(forward?1:-1)+eligible.Length)%eligible.Length];
    }
    public override void OnApplyTemplate()
    {
        StopFrames();_placed=false;base.OnApplyTemplate();_highlight=GetTemplateChild("PART_Highlight") as Border;_host=GetTemplateChild("PART_ItemsHost") as FrameworkElement;
        _translate=new TranslateTransform();if(_highlight is not null){_highlight.RenderTransform=_translate;_highlight.Opacity=0;}Place(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate)
    {StopFrames();_placed=false;_highlight=null;_host=null;_translate=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==ArcMotion.LevelProperty||e.Property==IsEnabledProperty){StopFrames();_placed=false;Place(false);}
    }
    internal void Place(bool animate)
    {
        if(_highlight is null||_host is null)return;
        if(_selected is null||_selected.Visibility!=Visibility.Visible||_selected.ActualHeight<=0)
        {StopFrames();_highlight.Opacity=0;_placed=false;return;}
        Point p;
        try{p=_selected.TranslatePoint(new Point(),_host);}catch(InvalidOperationException){return;}
        double height=_selected.ActualHeight;
        bool resized=Math.Abs(_lastHostWidth-_host.ActualWidth)>.01;
        _lastHostWidth=_host.ActualWidth;
        if(_placed&&!resized&&Math.Abs(_y.Target-p.Y)<.01&&Math.Abs(_height.Target-height)<.01)return;
        _y.Target=p.Y;_height.Target=height;
        animate &= !resized&&_placed&&IsLoaded&&IsVisible&&IsEnabled&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        _placed=true;_highlight.Opacity=1;
        if(!animate){StopFrames();_y.Snap(p.Y);_height.Snap(height);ApplyPlacement();return;}
        if(!_rendering){_lastFrame=Stopwatch.GetTimestamp();CompositionTarget.Rendering+=Frame;_rendering=true;}
    }
    private void Frame(object? sender,EventArgs e)
    {long now=Stopwatch.GetTimestamp();AdvanceMotion(Stopwatch.GetElapsedTime(_lastFrame,now).TotalSeconds);_lastFrame=now;}
    internal void AdvanceMotion(double seconds){bool y=_y.Step(seconds),height=_height.Step(seconds);ApplyPlacement();if(y&&height)StopFrames();}
    private void ApplyPlacement(){if(_translate is not null)_translate.Y=_y.Position;if(_highlight is not null)_highlight.Height=Math.Max(0,_height.Position);}
    private void StopFrames(){if(!_rendering)return;CompositionTarget.Rendering-=Frame;_rendering=false;}
    protected override AutomationPeer OnCreateAutomationPeer()=>new GroupPeer(this);
    private sealed class GroupPeer(RadioGroup owner):FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Group;
        protected override string GetClassNameCore()=>nameof(RadioGroup);
        protected override string GetNameCore(){var name=base.GetNameCore();return string.IsNullOrEmpty(name)?owner.Header?.ToString()??"":name;}
    }
}

