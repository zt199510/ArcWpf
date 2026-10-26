using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;

[TemplatePart(Name="PART_Background",Type=typeof(ContentPresenter))]
public class DialogHost:ContentControl
{
    public static readonly DependencyProperty DialogProperty=DependencyProperty.Register(nameof(Dialog),typeof(Dialog),typeof(DialogHost),new FrameworkPropertyMetadata(null,Changed));
    public Dialog? Dialog{get=>(Dialog?)GetValue(DialogProperty);set=>SetValue(DialogProperty,value);}
    private ContentPresenter? _background;
    private bool _redirecting;
    static DialogHost()=>DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogHost),new FrameworkPropertyMetadata(typeof(DialogHost)));
    public DialogHost()
    {
        Loaded+=(_,_)=>{Dialog?.Attach(this);Dialog?.Synchronize();};
        Unloaded+=(_,_)=>Dialog?.RequestClose(DialogCloseReason.HostUnavailable,true);
        PreviewGotKeyboardFocus+=GuardFocus;
    }
    protected override IEnumerator LogicalChildren
    {
        get{var children=new List<object>();var original=base.LogicalChildren;while(original.MoveNext())children.Add(original.Current);if(Dialog is not null&&!children.Contains(Dialog))children.Add(Dialog);return children.GetEnumerator();}
    }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var host=(DialogHost)d;
        if(e.OldValue is Dialog old){old.RequestClose(DialogCloseReason.Replaced,true);old.Attach(null);host.RemoveLogicalChild(old);}
        if(e.NewValue is Dialog next){host.AddLogicalChild(next);next.Attach(host);}
        host.UpdateModal();
    }
    public override void OnApplyTemplate()
    {
        if(_background is not null){_background.Effect=null;_background.IsEnabled=true;}
        base.OnApplyTemplate();_background=GetTemplateChild("PART_Background") as ContentPresenter;UpdateModal();Dialog?.Synchronize();
    }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate,ControlTemplate newTemplate)
    {if(Dialog?.IsPresented==true)Dialog.RequestClose(DialogCloseReason.TemplateChanged,true);base.OnTemplateChanged(oldTemplate,newTemplate);}
    internal void UpdateModal()
    {
        if(_background is null)return;
        bool modal=Dialog?.IsPresented==true;
        _background.IsEnabled=!modal;_background.IsHitTestVisible=!modal;
        _background.Effect=modal?new BlurEffect{Radius=7}:null;
    }
    private void GuardFocus(object sender,KeyboardFocusChangedEventArgs e)
    {
        if(_redirecting||Dialog is not {IsPresented:true} dialog||Contains(dialog,e.NewFocus as DependencyObject))return;
        e.Handled=true;_redirecting=true;try{dialog.FocusInitial();}finally{_redirecting=false;}
    }
    internal static bool Contains(DependencyObject ancestor,DependencyObject? child)
    {
        for(var node=child;node is not null;node=GetParentNode(node))if(ReferenceEquals(node,ancestor))return true;
        return false;
    }
    internal static DependencyObject? GetParentNode(DependencyObject node)=>node is Visual ? VisualTreeHelper.GetParent(node)??LogicalTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
    internal static DialogHost? Find(DependencyObject origin)
    {for(DependencyObject? node=origin;node is not null;node=GetParentNode(node))if(node is DialogHost host)return host;return null;}
}



