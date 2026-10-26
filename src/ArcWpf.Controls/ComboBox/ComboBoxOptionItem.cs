using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArcWpf.Controls;
public class ComboBoxList : ListBox
{
    protected override DependencyObject GetContainerForItemOverride()=>new ComboBoxOptionItem();
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is ComboBoxOptionItem;
    protected override void PrepareContainerForItemOverride(DependencyObject element,object item)
    {base.PrepareContainerForItemOverride(element,item);if(item is ComboBoxOption option)AutomationProperties.SetName(element,option.Label);}
}
public class ComboBoxOptionItem : ListBoxItem
{
    public static readonly DependencyProperty IsActiveProperty=DependencyProperty.Register(nameof(IsActive),typeof(bool),typeof(ComboBoxOptionItem),new PropertyMetadata(false));
    public bool IsActive {get=>(bool)GetValue(IsActiveProperty);set=>SetValue(IsActiveProperty,value);}
    private Border? _surface;
    private SolidColorBrush? _brush;
    private readonly OwnedMotion _motion=new();
    static ComboBoxOptionItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(ComboBoxOptionItem),new FrameworkPropertyMetadata(typeof(ComboBoxOptionItem)));
    public ComboBoxOptionItem(){Unloaded+=(_,_)=>_motion.Stop();Loaded+=(_,_)=>Update(false);}
    public override void OnApplyTemplate(){_motion.Stop();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as Border;_brush=null;Update(false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();_surface=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&(e.Property==BackgroundProperty||e.Property==ArcMotion.LevelProperty))Update(IsLoaded&&IsVisible);}
    private void Update(bool animate)
    {if(_surface is not null&&Background is SolidColorBrush brush){_brush??=new(brush.Color);_surface.Background=_brush;_motion.Color(_brush,brush.Color,.08,animate&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full);}}
}
