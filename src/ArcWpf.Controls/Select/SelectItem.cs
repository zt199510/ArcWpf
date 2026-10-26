using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;
public class SelectItem:ComboBoxItem
{
    private FrameworkElement? _indicator;
    private Border? _surface;
    private ScaleTransform? _scale;
    private BlurEffect? _blur;
    private SolidColorBrush? _background;
    private readonly OwnedMotion _motion=new();
    internal bool IsMotionRunning=>_motion.IsRunning;
    static SelectItem()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectItem),new FrameworkPropertyMetadata(typeof(SelectItem)));
    public SelectItem(){Loaded+=(_,_)=>{if(IsSelected&&_indicator is not null&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full)_indicator.Opacity=0;UpdateVisual(true);};Unloaded+=(_,_)=>UpdateVisual(false);}
    public override void OnApplyTemplate()
    {
        _motion.Stop();base.OnApplyTemplate();_surface=GetTemplateChild("PART_Surface") as Border;_indicator=GetTemplateChild("PART_Indicator") as FrameworkElement;_background=null;
        if(_indicator is not null){_scale=new ScaleTransform(1,1);_blur=new BlurEffect();_indicator.RenderTransform=_scale;_indicator.Effect=_blur;_indicator.Opacity=0;}
        UpdateVisual(false);
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate)
    {_motion.Stop();_surface=null;_indicator=null;_scale=null;_blur=null;_background=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {base.OnPropertyChanged(e);if(_motion is not null&&(e.Property==IsSelectedProperty||e.Property==BackgroundProperty||e.Property==ArcMotion.LevelProperty||e.Property==IsEnabledProperty))UpdateVisual(IsLoaded&&IsVisible);}
    internal void UpdateVisual(bool animate)
    {
        bool full=ArcMotion.GetLevel(this)==ArcMotionLevel.Full;animate &= full&&IsEnabled;
        if(!animate)_motion.Stop();
        if(_surface is not null)
        {
            if(Background is SolidColorBrush brush){_background??=new SolidColorBrush(brush.Color);_surface.Background=_background;_motion.Color(_background,brush.Color,.12,animate);}
            else _surface.Background=Background;
        }
        if(_indicator is null)return;
        if(IsSelected&&animate&&_indicator.Opacity<.001){if(_scale is not null)_scale.ScaleX=_scale.ScaleY=.6;if(_blur is not null)_blur.Radius=2;}
        if(_scale is not null){_motion.Double(_scale,ScaleTransform.ScaleXProperty,1,.24,animate,delay:.04,enter:true);_motion.Double(_scale,ScaleTransform.ScaleYProperty,1,.24,animate,delay:.04,enter:true);}
        if(_blur is not null)_motion.Double(_blur,BlurEffect.RadiusProperty,0,.24,animate,delay:.04,enter:true);
        _motion.Double(_indicator,OpacityProperty,IsSelected?1:0,.24,animate&&IsSelected,delay:.04,enter:true);
    }
}
