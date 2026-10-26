using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace ArcWpf.Controls;

// Native buttons and list containers retain command, focus and automation semantics.
public class MultiSelectAction : System.Windows.Controls.Button
{
    private Border? _surface;
    private readonly OwnedMotion _motion=new();
    private SolidColorBrush? _brush;
    private ScaleTransform? _scale;
    private SolidColorBrush? _foreground;
    static MultiSelectAction()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiSelectAction),new FrameworkPropertyMetadata(typeof(MultiSelectAction)));
    public MultiSelectAction(){Unloaded+=(_,_)=>_motion.Stop();Loaded+=(_,_)=>Update(false);}
    public override void OnApplyTemplate(){_motion.Stop();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as Border;_brush=null;if(_surface is not null){_scale=new();_surface.RenderTransform=_scale;}Update(false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();_surface=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&(e.Property==BackgroundProperty||e.Property==ForegroundProperty||e.Property==IsPressedProperty||e.Property==ArcMotion.LevelProperty))Update(IsLoaded);}
    private void Update(bool animate)
    {
        animate&=ArcMotion.GetLevel(this)==ArcMotionLevel.Full&&IsVisible;
        if(_surface is not null&&Background is SolidColorBrush brush){_brush??=new(brush.Color);_surface.Background=_brush;_motion.Color(_brush,brush.Color,.16,animate);}
        if(Content is Path path&&Foreground is SolidColorBrush fg){_foreground??=new(fg.Color);path.Stroke=_foreground;_motion.Color(_foreground,fg.Color,.16,animate);}
        if(_scale is not null){double v=IsPressed&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full?.96:1;_motion.Double(_scale,ScaleTransform.ScaleXProperty,v,.12,animate);_motion.Double(_scale,ScaleTransform.ScaleYProperty,v,.12,animate);}
    }
}
public class MultiSelectList : ListBox
{
    protected override DependencyObject GetContainerForItemOverride()=>new MultiSelectItem();
    protected override bool IsItemItsOwnContainerOverride(object item)=>item is MultiSelectItem;
}
public class MultiSelectItem : ListBoxItem
{
    private readonly OwnedMotion _motion=new();
    private Border? _surface;
    private Path? _check;
    private SolidColorBrush? _brush;
    static MultiSelectItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiSelectItem),new FrameworkPropertyMetadata(typeof(MultiSelectItem)));
    public MultiSelectItem(){Unloaded+=(_,_)=>_motion.Stop();Loaded+=(_,_)=>Update(false);}
    public override void OnApplyTemplate(){_motion.Stop();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as Border;_check=GetTemplateChild("PART_Check") as Path;_brush=null;Update(false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();_surface=null;_check=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&(e.Property==IsSelectedProperty||e.Property==BackgroundProperty||e.Property==ArcMotion.LevelProperty))Update(IsLoaded);}
    private void Update(bool animate)
    {
        var level=ArcMotion.GetLevel(this);animate&=level!=ArcMotionLevel.Off&&IsVisible;
        if(_surface is not null&&Background is SolidColorBrush brush){_brush??=new(brush.Color);_surface.Background=_brush;_motion.Color(_brush,brush.Color,.08,animate&&level==ArcMotionLevel.Full);}
        if(_check is null)return;
        // Path length is 19.45 DIP, divided by its 1.75 DIP stroke for WPF dash units.
        _motion.Double(_check,Shape.StrokeDashOffsetProperty,IsSelected?0:12,.24,animate&&level==ArcMotionLevel.Full,enter:true);
        _motion.Double(_check,OpacityProperty,IsSelected?1:0,.12,animate);
    }
}
