using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;
public partial class ComboBox
{
    private readonly OwnedMotion _visualMotion=new(),_textMotion=new(),_heightMotion=new(),_emptyMotion=new();
    private FrameworkElement? _textVisual,_clearVisual,_empty,_keyboardRing;
    private Grid? _heightHost;
    private Border? _inputBorder;
    private SolidColorBrush? _backgroundColor,_borderColor;
    private RotateTransform? _rotation;
    private ScaleTransform? _clearScale;
    private TranslateTransform? _textMove,_emptyMove;
    private BlurEffect? _clearBlur,_textBlur,_emptyBlur;
    private double _targetHeight;
    private bool _wasEmpty;
    private void InitializeVisuals()
    {
        _keyboardRing=GetTemplateChild("PART_KeyboardRing") as FrameworkElement;
        _inputBorder=GetTemplateChild("PART_Trigger") as Border;
        _textVisual=GetTemplateChild("PART_TextVisual") as FrameworkElement;
        _clearVisual=GetTemplateChild("PART_ClearVisual") as FrameworkElement;
        _empty=GetTemplateChild("PART_Empty") as FrameworkElement;
        _heightHost=GetTemplateChild("PART_HeightHost") as Grid;
        _backgroundColor=_borderColor=null;_targetHeight=0;
        if(_chevron is not null){_rotation=new();_chevron.RenderTransform=_rotation;}
        if(_textVisual is not null){_textMove=new();_textBlur=new(){Radius=0};_textVisual.RenderTransform=_textMove;_textVisual.Effect=_textBlur;}
        if(_empty is not null){_emptyMove=new();_emptyBlur=new(){Radius=0};_empty.RenderTransform=_emptyMove;_empty.Effect=_emptyBlur;}
        if(_clearVisual is not null){_clearScale=new(.6,.6);_clearBlur=new(){Radius=2};_clearVisual.RenderTransform=_clearScale;_clearVisual.Effect=_clearBlur;_clearVisual.Opacity=0;}
    }
    private void ClearVisualParts()
    {
        _textVisual=_clearVisual=_empty=_keyboardRing=null;_heightHost=null;_inputBorder=null;_rotation=null;_clearScale=null;_textMove=_emptyMove=null;_clearBlur=_textBlur=_emptyBlur=null;_backgroundColor=_borderColor=null;
    }
    private void StopVisuals(){_visualMotion.Stop();_textMotion.Stop();_heightMotion.Stop();_emptyMotion.Stop();}
    private void UpdateVisuals(bool animate)
    {
        var level=ArcMotion.GetLevel(this);bool full=level==ArcMotionLevel.Full;animate&=IsLoaded&&IsVisible&&level!=ArcMotionLevel.Off;
        if(_inputBorder is not null)
        {
            if(Background is SolidColorBrush bg){_backgroundColor??=new(bg.Color);_inputBorder.Background=_backgroundColor;_visualMotion.Color(_backgroundColor,bg.Color,.16,animate&&full);}else _inputBorder.Background=Background;
            if(BorderBrush is SolidColorBrush br){_borderColor??=new(br.Color);_inputBorder.BorderBrush=_borderColor;_visualMotion.Color(_borderColor,br.Color,.16,animate&&full);}else _inputBorder.BorderBrush=BorderBrush;
        }
        if(_keyboardRing is not null)_visualMotion.Double(_keyboardRing,OpacityProperty,_keyboardFocus&&IsKeyboardFocused?1:0,.16,animate&&full);
        if(_rotation is not null)_visualMotion.Double(_rotation,RotateTransform.AngleProperty,IsDropDownOpen?180:0,.58,animate&&full,spring:true);
        if(_clearVisual is null||_clear is null)return;
        bool show=HasSelection&&IsEnabled&&!IsReadOnly;
        if(!show&&_clear.IsKeyboardFocusWithin)Focus();
        _clear.IsHitTestVisible=show;_clear.IsEnabled=show;_clear.Focusable=show;
        if(show)_clearVisual.Visibility=Visibility.Visible;
        if(_clearScale is not null)
        {
            _visualMotion.Double(_clearScale,ScaleTransform.ScaleXProperty,!show&&full?.6:1,show?.26:.12,animate&&full,spring:show);
            _visualMotion.Double(_clearScale,ScaleTransform.ScaleYProperty,!show&&full?.6:1,show?.26:.12,animate&&full,spring:show);
        }
        if(_clearBlur is not null)_visualMotion.Double(_clearBlur,BlurEffect.RadiusProperty,!show&&full?2:0,show?.26:.12,animate&&full);
        _visualMotion.Double(_clearVisual,OpacityProperty,show?1:0,show?.16:.12,animate,completed:()=>{if(!(HasSelection&&IsEnabled&&!IsReadOnly)&&_clearVisual is not null)_clearVisual.Visibility=Visibility.Collapsed;});
    }
    private void AnimateText(bool selected)
    {
        if(_textVisual is null)return;
        _textMotion.Stop();var level=ArcMotion.GetLevel(this);bool animate=IsLoaded&&IsVisible&&level!=ArcMotionLevel.Off;bool full=level==ArcMotionLevel.Full;
        _textVisual.Opacity=animate?0:1;
        if(_textMove is not null)_textMove.Y=animate&&full&&selected?FontSize*.35:0;
        if(_textBlur is not null)_textBlur.Radius=animate&&full&&selected?4:0;
        double duration=full?(selected?.24:.16):.12;
        if(_textMove is not null)_textMotion.Double(_textMove,TranslateTransform.YProperty,0,duration,animate&&full,enter:true);
        if(_textBlur is not null)_textMotion.Double(_textBlur,BlurEffect.RadiusProperty,0,duration,animate&&full,enter:true);
        _textMotion.Double(_textVisual,OpacityProperty,1,duration,animate,enter:true);
    }
    private void UpdateEmpty()
    {
        if(_empty is null)return;
        bool empty=_filtered.Length==0;
        _empty.Visibility=empty?Visibility.Visible:Visibility.Collapsed;
        if(_list is not null)_list.Visibility=empty?Visibility.Collapsed:Visibility.Visible;
        var level=ArcMotion.GetLevel(this);bool full=level==ArcMotionLevel.Full;
        bool animate=empty&&!_wasEmpty&&IsPresented&&IsVisible&&level!=ArcMotionLevel.Off;
        _wasEmpty=empty;
        if(!empty){_emptyMotion.Stop();return;}
        if(animate){_empty.Opacity=0;if(_emptyMove is not null)_emptyMove.Y=full?4:0;if(_emptyBlur is not null)_emptyBlur.Radius=full?4:0;}
        if(_emptyMove is not null)_emptyMotion.Double(_emptyMove,TranslateTransform.YProperty,0,.24,animate&&full,enter:true);
        if(_emptyBlur is not null)_emptyMotion.Double(_emptyBlur,BlurEffect.RadiusProperty,0,.24,animate&&full,enter:true);
        _emptyMotion.Double(_empty,OpacityProperty,1,full?.24:.12,animate,enter:true);
    }
    private void UpdateMenuHeight(double width,double limit)
    {
        if(_heightHost is null)return;
        _heightHost.MaxHeight=limit;
        double target=Math.Min(limit,Math.Max(1,_filtered.Length*36d));
        if(_filtered.Length==0)
        {
            _empty?.Measure(new Size(Math.Max(0,width-12),double.PositiveInfinity));
            target=Math.Min(limit,Math.Max(44,_empty?.DesiredSize.Height??44));
        }
        bool animate=_targetHeight>0&&IsPresented&&IsDropDownOpen&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        if(double.IsNaN(_heightHost.Height))_heightHost.Height=target;
        _heightMotion.Double(_heightHost,HeightProperty,target,.4,animate,spring:true);
        _targetHeight=target;
        // Rows retain their natural measure while the outer viewport opens/closes around them.
        if(_list is not null)_list.Height=target;
    }
}
