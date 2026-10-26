using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;
public sealed class DialogSwapText:Control
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(DialogSwapText),new FrameworkPropertyMetadata("",(d,e)=>((DialogSwapText)d).Refresh((string?)e.OldValue??"",((DialogSwapText)d).IsLoaded&&((DialogSwapText)d).IsVisible)));
    public string Text{get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    private TextBlock? _current,_old;
    private readonly OwnedMotion _motion=new();
    internal bool IsMotionRunning=>_motion.IsRunning;
    static DialogSwapText()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogSwapText),new FrameworkPropertyMetadata(typeof(DialogSwapText)));
    public DialogSwapText()=>Unloaded+=(_,_)=>Refresh("",false);
    public override void OnApplyTemplate(){_motion.Stop();base.OnApplyTemplate();_current=GetTemplateChild("PART_Current") as TextBlock;_old=GetTemplateChild("PART_Old") as TextBlock;Refresh("",false);}
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate){_motion.Stop();_current=_old=null;base.OnTemplateChanged(oldTemplate,newTemplate);}
    internal void Refresh(string previous,bool animate)
    {
        _motion.Stop();if(_current is null||_old is null)return;
        _current.Text=Text??"";_current.Opacity=1;_current.RenderTransform=new TranslateTransform();_current.Effect=null;_old.Text="";_old.Opacity=0;
        if(!animate||ArcMotion.GetLevel(this)!=ArcMotionLevel.Full||previous==Text)return;
        _old.Text=previous;_old.Width=Math.Max(0,_current.ActualWidth);_old.Opacity=1;
        var enter=new TranslateTransform(0,FontSize*.3);var leave=new TranslateTransform();var soft=new BlurEffect{Radius=4};var subtle=new BlurEffect();
        _current.RenderTransform=enter;_old.RenderTransform=leave;_current.Effect=soft;_old.Effect=subtle;_current.Opacity=0;
        _motion.Double(enter,TranslateTransform.YProperty,0,.24,true,enter:true);_motion.Double(soft,BlurEffect.RadiusProperty,0,.24,true,enter:true);
        _motion.Double(_current,OpacityProperty,1,.24,true,enter:true);
        _motion.Double(leave,TranslateTransform.YProperty,-FontSize*.3,.16,true);_motion.Double(subtle,BlurEffect.RadiusProperty,2,.16,true);
        _motion.Double(_old,OpacityProperty,0,.16,true,completed:()=>{if(_old is not null)_old.Text="";});
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(_motion is not null&&e.Property==ArcMotion.LevelProperty)Refresh("",false);}
    protected override AutomationPeer? OnCreateAutomationPeer()=>null;
}
public sealed class DialogDecorativeText:TextBlock{protected override AutomationPeer? OnCreateAutomationPeer()=>null;}
