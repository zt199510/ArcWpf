using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;
public sealed class TooltipSurface:ContentControl
{
    static TooltipSurface()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(TooltipSurface),new FrameworkPropertyMetadata(typeof(TooltipSurface)));
    protected override void OnPreviewGotKeyboardFocus(KeyboardFocusChangedEventArgs e){e.Handled=true;base.OnPreviewGotKeyboardFocus(e);}
    protected override AutomationPeer OnCreateAutomationPeer()=>new SurfacePeer(this);
    private sealed class SurfacePeer(TooltipSurface owner):FrameworkElementAutomationPeer(owner)
    {protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.ToolTip;protected override string GetClassNameCore()=>nameof(Tooltip);}
}
public sealed class TooltipText:Control
{
    private TextBlock? _current,_old;
    private FrameworkElement? _size;
    private string _value="";
    private readonly OwnedMotion _motion=new();
    internal bool IsMotionRunning=>_motion.IsRunning;
    static TooltipText()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(TooltipText),new FrameworkPropertyMetadata(typeof(TooltipText)));
    public TooltipText()=>Unloaded+=(_,_)=>StopMotion();
    public override void OnApplyTemplate(){StopMotion();base.OnApplyTemplate();_current=GetTemplateChild("PART_Current") as TextBlock;_old=GetTemplateChild("PART_Old") as TextBlock;_size=GetTemplateChild("PART_Size") as FrameworkElement;if(_current is not null){_current.Text=_value;MeasureCopy();}}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){StopMotion();_current=_old=null;_size=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    internal void SetText(string text,bool animate)
    {
        if(text==_value)return;
        string previous=_value;_value=text;if(_current is null||_old is null||_size is null)return;
        double width=_size.ActualWidth,height=_size.ActualHeight;StopMotion();_current.Text=text;MeasureCopy();
        if(!animate||ArcMotion.GetLevel(this)!=ArcMotionLevel.Full||width<=0||height<=0)return;
        _current.Measure(new Size(206,double.PositiveInfinity));var target=_current.DesiredSize;
        _size.Width=width;_size.Height=height;
        _motion.Double(_size,WidthProperty,target.Width,.42,true,spring:true,completed:()=>{if(_size is not null)_size.Width=double.NaN;});
        _motion.Double(_size,HeightProperty,target.Height,.42,true,spring:true,completed:()=>{if(_size is not null)_size.Height=double.NaN;});
        _old.Text=previous;_old.Width=width;_old.Opacity=1;_current.Opacity=0;
        var move=new TranslateTransform(0,FontSize*.3);var leave=new TranslateTransform();var blur=new BlurEffect{Radius=4};var oldBlur=new BlurEffect();
        _current.RenderTransform=move;_old.RenderTransform=leave;_current.Effect=blur;_old.Effect=oldBlur;
        _motion.Double(move,TranslateTransform.YProperty,0,.24,true,enter:true);_motion.Double(blur,BlurEffect.RadiusProperty,0,.24,true,enter:true);_motion.Double(_current,OpacityProperty,1,.24,true,enter:true);
        _motion.Double(leave,TranslateTransform.YProperty,-FontSize*.3,.12,true);_motion.Double(oldBlur,BlurEffect.RadiusProperty,2,.12,true);_motion.Double(_old,OpacityProperty,0,.12,true,completed:()=>{if(_old is not null)_old.Text="";});
    }
    private void MeasureCopy()
    {
        if(_current is null)return;
        _current.Width=double.NaN;_current.MaxWidth=double.PositiveInfinity;
        _current.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double width=Math.Min(206,Math.Ceiling(_current.DesiredSize.Width));
        _current.MaxWidth=206;_current.Width=width;
    }
    internal void StopMotion(){_motion.Stop();if(_size is not null){_size.Width=_size.Height=double.NaN;}if(_current is not null){_current.Opacity=1;_current.RenderTransform=Transform.Identity;_current.Effect=null;}if(_old is not null){_old.Text="";_old.Opacity=0;}}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&e.Property==ArcMotion.LevelProperty)StopMotion();if(_motion is not null&&(e.Property==FontFamilyProperty||e.Property==FontSizeProperty||e.Property==FontWeightProperty)){StopMotion();MeasureCopy();}}
    protected override AutomationPeer? OnCreateAutomationPeer()=>null;
}



