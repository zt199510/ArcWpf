using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;
public partial class Select
{
    internal void RefreshValue(bool animate)
    {
        string text=SelectedIndex<0?Placeholder:Text;
        int direction=SelectedIndex>=_lastIndex?1:-1;
        string previous=_lastText;bool changed=text!=previous;
        _lastText=text;_lastIndex=SelectedIndex;
        if(_value is null||_oldValue is null)return;
        bool simple=ItemTemplate is null&&ItemTemplateSelector is null&&
            (SelectedItem is not UIElement||SelectedItem is ComboBoxItem{Content:string});
        if(GetTemplateChild("SelectedContent") is FrameworkElement content)content.Visibility=SelectedIndex>=0&&!simple?Visibility.Visible:Visibility.Collapsed;
        if(GetTemplateChild("SelectedText") is FrameworkElement selectedText)selectedText.Visibility=SelectedIndex>=0&&simple?Visibility.Visible:Visibility.Collapsed;
        var level=ArcMotion.GetLevel(this);
        animate &= changed&&level!=ArcMotionLevel.Off&&simple;
        int token=++_valueVersion;_valueMotion.Stop();
        _value.Opacity=1;if(_valueMove is not null)_valueMove.Y=0;if(_valueBlur is not null)_valueBlur.Radius=0;
        _oldValue.Opacity=0;_oldValue.Text="";
        if(!animate)return;
        bool full=level==ArcMotionLevel.Full;
        _oldValue.Text=previous;_oldValue.Opacity=1;
        if(_oldMove is not null)_oldMove.Y=0;if(_oldBlur is not null)_oldBlur.Radius=0;
        _value.Opacity=0;
        if(_valueMove is not null)_valueMove.Y=full?direction*.35*FontSize:0;
        if(_valueBlur is not null)_valueBlur.Radius=full?4:0;
        double enter=full?.24:.12,exit=full?.16:.12;
        if(_valueMove is not null)_valueMotion.Double(_valueMove,TranslateTransform.YProperty,0,enter,full,enter:true);
        if(_valueBlur is not null)_valueMotion.Double(_valueBlur,BlurEffect.RadiusProperty,0,enter,full,enter:true);
        if(_oldMove is not null)_valueMotion.Double(_oldMove,TranslateTransform.YProperty,full?-direction*.3*FontSize:0,exit,full);
        if(_oldBlur is not null)_valueMotion.Double(_oldBlur,BlurEffect.RadiusProperty,full?2:0,exit,full);
        _valueMotion.Double(_value,OpacityProperty,1,enter,true,enter:full);
        _valueMotion.Double(_oldValue,OpacityProperty,0,exit,true,completed:()=>{if(token==_valueVersion&&_oldValue is not null)_oldValue.Text="";});
    }
}
