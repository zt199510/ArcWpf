using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_Mark",Type=typeof(Border))]
[TemplatePart(Name="PART_Dot",Type=typeof(Border))]
[TemplatePart(Name="PART_Preview",Type=typeof(Border))]
[TemplatePart(Name="PART_Row",Type=typeof(Border))]
[TemplatePart(Name="PART_Label",Type=typeof(ContentPresenter))]
public class RadioButton : System.Windows.Controls.RadioButton
{
    public static readonly DependencyProperty DescriptionProperty=DependencyProperty.Register(nameof(Description),typeof(string),typeof(RadioButton),new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(object),typeof(RadioButton),new FrameworkPropertyMetadata(null,(d,_)=>((RadioButton)d).GroupOwner?.RefreshSelection()));
    public static readonly DependencyProperty SelectedBrushProperty=DependencyProperty.Register(nameof(SelectedBrush),typeof(Brush),typeof(RadioButton),new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty GlyphBrushProperty=DependencyProperty.Register(nameof(GlyphBrush),typeof(Brush),typeof(RadioButton),new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IndicatorBorderBrushProperty=DependencyProperty.Register(nameof(IndicatorBorderBrush),typeof(Brush),typeof(RadioButton),new FrameworkPropertyMetadata(null));
    public string Description {get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}
    public object? Value {get=>GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
    public Brush? SelectedBrush {get=>(Brush?)GetValue(SelectedBrushProperty);set=>SetValue(SelectedBrushProperty,value);}
    public Brush? GlyphBrush {get=>(Brush?)GetValue(GlyphBrushProperty);set=>SetValue(GlyphBrushProperty,value);}
    public Brush? IndicatorBorderBrush {get=>(Brush?)GetValue(IndicatorBorderBrushProperty);set=>SetValue(IndicatorBorderBrushProperty,value);}
    internal RadioGroup? GroupOwner {get;set;}
    internal string PreviousGroupName {get;set;}="";
    internal bool PreviousIsTabStop {get;set;}
    private Border? _mark,_dot,_preview,_row;
    private ContentPresenter? _label;
    private ScaleTransform? _press,_dotScale;
    private Transform? _oldMarkTransform,_oldDotTransform;
    private readonly ControlSpring _scale=new(.4,.26);
    private readonly OwnedMotion _motion=new();
    private readonly Dictionary<(DependencyObject,DependencyProperty),SolidColorBrush> _brushes=new();
    private long _lastFrame;
    private bool _rendering;
    internal bool IsMotionRunning=>_rendering||_motion.IsRunning;
    static RadioButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioButton),new FrameworkPropertyMetadata(typeof(RadioButton)));
        IsCheckedProperty.OverrideMetadata(typeof(RadioButton),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,null,(_,v)=>v??false));
        IsThreeStateProperty.OverrideMetadata(typeof(RadioButton),new FrameworkPropertyMetadata(false,null,(_,_)=>false));
    }
    public RadioButton()
    {
        SetCurrentValue(FocusVisualStyleProperty, null);
        Loaded+=(_,_)=>UpdateVisual(false); Unloaded+=(_,_)=>UpdateVisual(false); IsVisibleChanged+=(_,_)=>UpdateVisual(false);
    }
    protected override void OnClick(){if(IsEnabled)base.OnClick();}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==IsCheckedProperty||e.Property==IsPressedProperty||e.Property==IsEnabledProperty||e.Property==ArcMotion.LevelProperty||
            e.Property==BackgroundProperty||e.Property==BorderBrushProperty||e.Property==ForegroundProperty||e.Property==SelectedBrushProperty||e.Property==GlyphBrushProperty||e.Property==IndicatorBorderBrushProperty)
            UpdateVisual(IsLoaded&&IsVisible&&IsEnabled&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full);
    }
    public override void OnApplyTemplate()
    {
        Release();base.OnApplyTemplate();
        _mark=GetTemplateChild("PART_Mark") as Border; _dot=GetTemplateChild("PART_Dot") as Border; _preview=GetTemplateChild("PART_Preview") as Border;_row=GetTemplateChild("PART_Row") as Border;_label=GetTemplateChild("PART_Label") as ContentPresenter;
        if(_mark is not null){_oldMarkTransform=_mark.RenderTransform;_press=AddScale(_mark);}
        if(_dot is not null){_oldDotTransform=_dot.RenderTransform;_dotScale=AddScale(_dot);}
        UpdateVisual(false);
    }
    private static ScaleTransform AddScale(FrameworkElement element)
    {var scale=new ScaleTransform(1,1);var group=new TransformGroup();group.Children.Add(element.RenderTransform);group.Children.Add(scale);element.SetCurrentValue(RenderTransformProperty,group);return scale;}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){Release();base.OnTemplateChanged(oldTemplate,newTemplate);}
    internal void UpdateVisual(bool animate)
    {
        if(_motion is null)return;
        animate &= IsEnabled&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        bool selected=IsChecked==true;
        _scale.Target=selected?1:.4;
        if(!animate){StopFrames();_motion.Stop();_scale.Snap(_scale.Target);}
        if(_dot is not null)
        {
            _motion.Double(_dot,OpacityProperty,selected?1:0,selected?.16:.12,animate);
            Brush(_dot,Border.BackgroundProperty,selected?GlyphBrush:Foreground,.16,animate);
        }
        if(_preview is not null){_motion.Double(_preview,OpacityProperty,IsPressed&&!selected&&IsEnabled?.22:0,.12,animate);Brush(_preview,Border.BackgroundProperty,Foreground,.16,animate);}
        if(_press is not null)
        {
            double value=IsPressed&&animate?.88:1;
            _motion.Double(_press,ScaleTransform.ScaleXProperty,value,IsPressed?.1:.58,animate,!IsPressed);
            _motion.Double(_press,ScaleTransform.ScaleYProperty,value,IsPressed?.1:.58,animate,!IsPressed);
        }
        if(_mark is not null)
        {
            Brush(_mark,Border.BorderBrushProperty,selected?SelectedBrush:IndicatorBorderBrush,.16,animate);
            Brush(_mark,Border.BackgroundProperty,selected?SelectedBrush:Brushes.Transparent,.16,animate);
        }
        if(_row is not null)
        {
            Brush(_row,Border.BorderBrushProperty,selected?(GroupOwner is null?SelectedBrush:Brushes.Transparent):BorderBrush,.16,animate);
            Brush(_row,Border.BackgroundProperty,GroupOwner is not null&&selected?Brushes.Transparent:Background,.16,animate);
        }
        if(_label is not null)Brush(_label,TextElement.ForegroundProperty,Foreground,.24,animate);
        ApplyScale();
        if(animate&&_dotScale is not null&&!_rendering&&Math.Abs(_scale.Position-_scale.Target)>.0001)
        {_lastFrame=Stopwatch.GetTimestamp();CompositionTarget.Rendering+=Frame;_rendering=true;}
    }
    private void Brush(DependencyObject target,DependencyProperty property,Brush? source,double duration,bool animate)
    {
        var key=(target,property);
        if(source is SolidColorBrush solid)
        {
            if(!_brushes.TryGetValue(key,out var owned)){owned=new SolidColorBrush(solid.Color);_brushes[key]=owned;}
            target.SetValue(property,owned); _motion.Color(owned,solid.Color,duration,animate);
        }
        else {target.SetValue(property,source);_brushes.Remove(key);}
    }
    private void Frame(object? sender,EventArgs e){long now=Stopwatch.GetTimestamp();AdvanceMotion(Stopwatch.GetElapsedTime(_lastFrame,now).TotalSeconds);_lastFrame=now;}
    internal void AdvanceMotion(double elapsed){bool done=_scale.Step(elapsed);ApplyScale();if(done)StopFrames();}
    private void ApplyScale(){if(_dotScale is not null)_dotScale.ScaleX=_dotScale.ScaleY=Math.Clamp(_scale.Position,.4,1);}
    private void StopFrames(){if(!_rendering)return;CompositionTarget.Rendering-=Frame;_rendering=false;}
    private void Release()
    {
        StopFrames();_motion.Stop();_brushes.Clear();
        if(_mark is not null&&_oldMarkTransform is not null)_mark.SetCurrentValue(RenderTransformProperty,_oldMarkTransform);
        if(_dot is not null&&_oldDotTransform is not null)_dot.SetCurrentValue(RenderTransformProperty,_oldDotTransform);
        _mark=_dot=_preview=_row=null;_label=null;_press=_dotScale=null;
    }
    protected override AutomationPeer OnCreateAutomationPeer()=>new RadioPeer(this);
    private sealed class RadioPeer(RadioButton owner):RadioButtonAutomationPeer(owner)
    {
        protected override string GetClassNameCore()=>nameof(RadioButton);
        protected override string GetNameCore(){var name=base.GetNameCore();return string.IsNullOrEmpty(name)?nameof(RadioButton):name;}
        protected override string GetHelpTextCore(){var help=base.GetHelpTextCore();return string.IsNullOrEmpty(help)?owner.Description??"":help;}
    }
}
