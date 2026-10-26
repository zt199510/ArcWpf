using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace ArcWpf.Controls;

public partial class MultiSelect
{
    private readonly OwnedMotion _visualMotion=new();
    private readonly Dictionary<string,Chip> _chips=new(StringComparer.Ordinal);
    private SolidColorBrush? _backgroundColor,_borderColor;
    private RotateTransform? _rotation;
    private ScaleTransform? _clearScale;
    private BlurEffect? _clearBlur;
    private FrameworkElement? _clearVisual,_valueArea;
    
    private static readonly DependencyProperty MutedBrushProperty=DependencyProperty.Register("MutedBrush",typeof(Brush),typeof(MultiSelect),new PropertyMetadata(null,(d,_)=>((MultiSelect)d).UpdateVisuals(true)));
    private static readonly DependencyProperty SurfaceBrushProperty=DependencyProperty.Register("SurfaceBrush",typeof(Brush),typeof(MultiSelect),new PropertyMetadata(null,(d,_)=>((MultiSelect)d).UpdateVisuals(true)));
    private sealed class Chip
    {
        internal required Border Slot;
        internal required Border Body;
        internal required TextBlock Text;
        internal required TextBlock OldText;
        internal required MultiSelectAction Remove;
        internal readonly OwnedMotion Motion=new();
        internal readonly ScaleTransform Scale=new(1,1);
        internal readonly BlurEffect Blur=new();
        internal readonly BlurEffect TextBlur=new();
        internal readonly BlurEffect OldBlur=new();
        internal readonly TranslateTransform Move=new();
        internal readonly TranslateTransform OldMove=new();
        internal SolidColorBrush Background=new(Colors.Transparent);
        internal string Label="";
        internal double TargetWidth;
        internal int Version;
        internal bool Exiting;
    }
    private void InitializeVisuals()
    {
        SetResourceReference(MutedBrushProperty,"ArcWpf.Brush.SurfaceMuted");SetResourceReference(SurfaceBrushProperty,"ArcWpf.Brush.Surface");
        _backgroundColor=_borderColor=null;
        if(_chevron is not null){_rotation=new();_chevron.RenderTransform=_rotation;}
        _clearVisual=GetTemplateChild("PART_ClearVisual") as FrameworkElement;
        _valueArea=GetTemplateChild("PART_ValueArea") as FrameworkElement;
        if(_valueArea is not null)_valueArea.SizeChanged+=ValueAreaSized;
        if(_clearVisual is not null){_clearScale=new(.6,.6);_clearBlur=new(){Radius=2};_clearVisual.RenderTransform=_clearScale;_clearVisual.Effect=_clearBlur;_clearVisual.Opacity=0;}
    }
    private void ValueAreaSized(object sender,SizeChangedEventArgs e)=>UpdateTags(IsLoaded&&IsVisible);
    private void StopVisuals()
    {
        _visualMotion.Stop();foreach(var pair in _chips.ToArray()){var c=pair.Value;++c.Version;c.Motion.Stop();if(c.Exiting){_tags?.Children.Remove(c.Slot);_chips.Remove(pair.Key);}}
        
    }
    private Chip CreateChip(string key,string label,bool more)
    {
        var text=new TextBlock{FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
        var old=new TextBlock{FontSize=12,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};
        var textHost=new Grid{ClipToBounds=true};textHost.Children.Add(old);textHost.Children.Add(text);
        var remove=new MultiSelectAction{Width=14,Height=18,Margin=new Thickness(3,0,0,0),Focusable=true,Content=new System.Windows.Shapes.Path{Data=Geometry.Parse("M3 3 L11 11 M11 3 L3 11"),StrokeThickness=1.5,Width=14,Height=14,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round}};
        ((System.Windows.Shapes.Path)remove.Content).SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"ArcWpf.Brush.TextMuted");
        if(!more){string value=key[6..];remove.Click+=(_,_)=>RemoveValue(value);AutomationProperties.SetName(remove,$"Remove {label}");}
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.Children.Add(textHost);Grid.SetColumn(remove,1);grid.Children.Add(remove);
        var body=new Border{CornerRadius=new CornerRadius(14),Padding=new Thickness(8,4,8,4),BorderThickness=new Thickness(1),Child=grid,Height=28,RenderTransformOrigin=new Point(.5,.5)};
        body.SetResourceReference(Border.BorderBrushProperty,"ArcWpf.Brush.BorderSubtle");
        var slot=new Border{ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left,Width=0,Child=body};body.HorizontalAlignment=HorizontalAlignment.Left;body.Margin=new Thickness(0,0,5,0);
        var chip=new Chip{Slot=slot,Body=body,Text=text,OldText=old,Remove=remove,Label=label};
        body.RenderTransform=chip.Scale;body.Effect=chip.Blur;text.RenderTransform=chip.Move;old.RenderTransform=chip.OldMove;text.Effect=chip.TextBlur;old.Effect=chip.OldBlur;
        body.Background=chip.Background;slot.Opacity=0;chip.Scale.ScaleX=chip.Scale.ScaleY=.9;chip.Blur.Radius=4;
        return chip;
    }
    private double MeasureLabel(string label)
    {
        var text=new FormattedText(label,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection,
            new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),12,Brushes.Black,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        // TextBlock ellipsis can trigger on a subpixel equality after WPF's arrange arithmetic.
        // Keep one physical pixel beyond the rounded natural advance; do not measure a trimmed TextBlock.
        double scale=VisualTreeHelper.GetDpi(this).DpiScaleX;
        return (Math.Ceiling(text.WidthIncludingTrailingWhitespace*scale)+1)/scale;
    }
    private void UpdateTags(bool animate)
    {
        if(_tags is null)return;
        var values=Values;
        double available=Math.Max(0,_valueArea?.ActualWidth??0);if(available==0)available=Math.Max(0,ActualWidth-80);
        int visible=Math.Min(MaxVisible,values.Length);
        double minimumSlot=24+18+5+(CanRemoveTags?17:0);
        double OverflowWidth(int count)=>count==0?0:Math.Min(149,MeasureLabel("+"+count)+23);
        // Shrink the number of labels before their text area would be consumed by padding/actions.
        while(visible>0&&visible*minimumSlot+OverflowWidth(values.Length-visible)>available)visible--;
        int remaining=values.Length-visible;
        var wanted=values.Take(visible).Select(v=>(Key:"value:"+v,Label:LabelFor(v),More:false)).ToList();
        if(remaining>0)wanted.Add(("overflow","+"+remaining,true));
        var keys=wanted.Select(x=>x.Key).ToHashSet();
        var level=ArcMotion.GetLevel(this);bool full=level==ArcMotionLevel.Full;animate&=level!=ArcMotionLevel.Off&&IsLoaded&&IsVisible;
        double budget=visible==0?0:Math.Max(0,(available-OverflowWidth(remaining))/visible);
        foreach(var pair in _chips.ToArray())
        {
            var c=pair.Value;if(keys.Contains(pair.Key))continue;
            if(c.Exiting&&animate)continue;
            c.Exiting=true;c.Remove.IsEnabled=false;int version=++c.Version;
            c.Motion.Double(c.Scale,ScaleTransform.ScaleXProperty,full?.9:1,.12,animate&&full);
            c.Motion.Double(c.Scale,ScaleTransform.ScaleYProperty,full?.9:1,.12,animate&&full);
            c.Motion.Double(c.Blur,BlurEffect.RadiusProperty,full?2:0,.12,animate&&full);
            c.Motion.Double(c.Slot,OpacityProperty,0,.12,animate);
            c.Motion.Double(c.Slot,WidthProperty,full?0:c.TargetWidth,full?.4:.12,animate,full,()=>{if(version!=c.Version||!c.Exiting)return;c.Motion.Stop();_tags?.Children.Remove(c.Slot);_chips.Remove(pair.Key);});
        }
        int index=0;
        foreach(var item in wanted)
        {
            if(!_chips.TryGetValue(item.Key,out var c)){c=CreateChip(item.Key,item.Label,item.More);_chips.Add(item.Key,c);_tags.Children.Insert(Math.Min(index,_tags.Children.Count),c.Slot);}
            if(_tags.Children.IndexOf(c.Slot)!=index){_tags.Children.Remove(c.Slot);_tags.Children.Insert(Math.Min(index,_tags.Children.Count),c.Slot);}
            ++index;++c.Version;c.Exiting=false;
            c.Remove.Visibility=!item.More&&CanRemoveTags?Visibility.Visible:Visibility.Collapsed;c.Remove.IsEnabled=!IsReadOnly&&IsEnabled;
            if(c.Label!=item.Label&&item.More)
            {
                int.TryParse(c.Label.TrimStart('+'),out int previous);int direction=remaining>=previous?1:-1;
                c.OldBlur.Radius=0;c.TextBlur.Radius=full?2:0;c.OldText.Text=c.Label;c.OldText.Opacity=1;c.OldMove.Y=0;c.Text.Opacity=0;c.Move.Y=full?direction*6:0;
                c.Motion.Double(c.OldBlur,BlurEffect.RadiusProperty,full?2:0,.12,animate&&full);c.Motion.Double(c.OldText,OpacityProperty,0,.12,animate);c.Motion.Double(c.OldMove,TranslateTransform.YProperty,full?-direction*6:0,.12,animate&&full);
            }
            c.Label=item.Label;c.Text.Text=item.Label;c.Motion.Double(c.TextBlur,BlurEffect.RadiusProperty,0,.24,animate&&full);
            double width=Math.Min(144,MeasureLabel(item.Label)+18+(c.Remove.Visibility==Visibility.Visible?17:0));
            width=item.More?Math.Min(width+5,available):Math.Min(width+5,budget);
            c.TargetWidth=Math.Max(0,width);c.Body.Width=Math.Max(0,width-5);
            c.Motion.Double(c.Slot,WidthProperty,c.TargetWidth,.4,animate&&full,spring:true);
            c.Motion.Double(c.Slot,OpacityProperty,1,full?.16:.12,animate);
            c.Motion.Double(c.Text,OpacityProperty,1,full?.24:.12,animate);
            c.Motion.Double(c.Move,TranslateTransform.YProperty,0,.24,animate&&full);
            c.Motion.Double(c.Scale,ScaleTransform.ScaleXProperty,1,.26,animate&&full,spring:true);
            c.Motion.Double(c.Scale,ScaleTransform.ScaleYProperty,1,.26,animate&&full,spring:true);
            c.Motion.Double(c.Blur,BlurEffect.RadiusProperty,0,.24,animate&&full);
        }
        UpdateVisuals(animate);
    }
    private void UpdateVisuals(bool animate)
    {
        if(_visualMotion is null)return;
        var level=ArcMotion.GetLevel(this);bool full=level==ArcMotionLevel.Full;animate&=IsLoaded&&IsVisible&&level!=ArcMotionLevel.Off;
        if(_trigger is Border border)
        {
            if(Background is SolidColorBrush bg){_backgroundColor??=new(bg.Color);border.Background=_backgroundColor;_visualMotion.Color(_backgroundColor,bg.Color,.16,animate&&full);}else border.Background=Background;
            if(BorderBrush is SolidColorBrush br){_borderColor??=new(br.Color);border.BorderBrush=_borderColor;_visualMotion.Color(_borderColor,br.Color,.16,animate&&full);}else border.BorderBrush=BorderBrush;
        }
        var chipBrush=GetValue(IsMouseOver||IsDropDownOpen?SurfaceBrushProperty:MutedBrushProperty) as SolidColorBrush;
        if(chipBrush is not null)foreach(var c in _chips.Values)c.Motion.Color(c.Background,chipBrush.Color,.16,animate&&full);
        if(_rotation is not null)_visualMotion.Double(_rotation,RotateTransform.AngleProperty,IsDropDownOpen?180:0,.58,animate&&full,spring:true);
        if(_placeholder is not null)_visualMotion.Double(_placeholder,OpacityProperty,HasSelection?0:1,.12,animate);
        if(_clearVisual is null||_clear is null)return;
        bool show=HasSelection&&IsEnabled&&!IsReadOnly;
        _clear.IsHitTestVisible=show;_clear.Focusable=show;
        if(show){_clear.Visibility=Visibility.Visible;_clearVisual.Visibility=Visibility.Visible;}
        if(_clearScale is not null){_visualMotion.Double(_clearScale,ScaleTransform.ScaleXProperty,!show&&full?.6:1,show?.26:.12,animate&&full,spring:show);_visualMotion.Double(_clearScale,ScaleTransform.ScaleYProperty,!show&&full?.6:1,show?.26:.12,animate&&full,spring:show);}
        if(_clearBlur is not null)_visualMotion.Double(_clearBlur,BlurEffect.RadiusProperty,!show&&full?2:0,show?.26:.12,animate&&full);
        _visualMotion.Double(_clearVisual,OpacityProperty,show?1:0,show?.16:.12,animate,completed:()=>{if(HasSelection&&IsEnabled&&!IsReadOnly)return;_clear.Visibility=Visibility.Hidden;_clearVisual.Visibility=Visibility.Collapsed;});
    }
}
