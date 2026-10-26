using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ArcWpf.Controls;

public partial class Tabs
{
    internal void PlaceIndicator(bool animate)
    {
        if(_headerTrack is null||_selection is null)return;
        if(SelectedIndex<0||SelectedIndex>=Items.Count||ItemContainerGenerator.ContainerFromItem(SelectedItem) is not System.Windows.Controls.TabItem item||item.ActualWidth<=0)
        {_selection.Opacity=0;_indicatorPlaced=false;return;}
        double x=item.TranslatePoint(new Point(),_headerTrack).X,width=item.ActualWidth;
        _selection.Height=item.ActualHeight;_selection.Opacity=1;
        if(_indicatorPlaced&&Math.Abs(_indicatorX.Target-x)<.01&&Math.Abs(_indicatorWidth.Target-width)<.01)return;
        bool spring=animate&&_indicatorPlaced&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
        _indicatorX.Target=x;_indicatorWidth.Target=width;
        if(!spring){_indicatorX.Snap(x);_indicatorWidth.Snap(width);_indicatorActive=false;ApplyIndicator();}
        else {_indicatorActive=true;StartFrames();}
        _indicatorPlaced=true;
    }
    private void ApplyIndicator(){if(_indicatorMove is not null)_indicatorMove.X=_indicatorX.Position;if(_selection is not null)_selection.Width=Math.Max(0,_indicatorWidth.Position);}
    private void HeaderLayout(object? sender,EventArgs e)
    {
        bool resized=Math.Abs(ActualWidth-_lastWidth)>.1;_lastWidth=ActualWidth;
        // A pending selection pass owns the animated target; layout must not snap it first.
        if(resized)_indicatorPlaced=false;
        if(_pending is null)PlaceIndicator(false);
        UpdateOverflow();if(resized)RevealSelected(false);
    }
    private void HeaderSized(object sender,SizeChangedEventArgs e){UpdateOverflow();QueueLayout(false);}
    private void Scrolled(object sender,ScrollChangedEventArgs e)=>UpdateOverflow();
    private void UpdateOverflow()
    {
        if(_headerScroll is null)return;
        double max=_headerScroll.ScrollableWidth,offset=_headerScroll.HorizontalOffset;
        SetValue(HasOverflowPropertyKey,max>1);SetValue(CanScrollBackwardPropertyKey,offset>1);SetValue(CanScrollForwardPropertyKey,offset<max-1);
        if(max<=1){_headerScroll.OpacityMask=null;return;}
        double edge=Math.Min(.49,35/Math.Max(1,_headerScroll.ViewportWidth));
        _headerScroll.OpacityMask=new LinearGradientBrush(new GradientStopCollection
        {
            new(CanScrollBackward?Colors.Transparent:Colors.Black,0),new(Colors.Black,edge),
            new(Colors.Black,1-edge),new(CanScrollForward?Colors.Transparent:Colors.Black,1)
        },new Point(0,0),new Point(1,0));
    }
    private void ScrollBackward(object sender,RoutedEventArgs e)=>ScrollTo((_headerScroll?.HorizontalOffset??0)-(_headerScroll?.ViewportWidth??0)*.75,true);
    private void ScrollForward(object sender,RoutedEventArgs e)=>ScrollTo((_headerScroll?.HorizontalOffset??0)+(_headerScroll?.ViewportWidth??0)*.75,true);
    private void ScrollTo(double offset,bool animate)
    {
        if(_headerScroll is null)return;
        offset=Math.Clamp(offset,0,_headerScroll.ScrollableWidth);
        double current=_headerScroll.HorizontalOffset;_scrollMotion.Stop();SetValue(AnimatedScrollProperty,current);
        _scrollMotion.Double(this,AnimatedScrollProperty,offset,.24,animate&&IsLoaded&&IsVisible&&ArcMotion.GetLevel(this)==ArcMotionLevel.Full);
    }
    private void RevealSelected(bool animate)
    {if(SelectedIndex>=0&&SelectedIndex<Items.Count&&ItemContainerGenerator.ContainerFromItem(SelectedItem) is System.Windows.Controls.TabItem item)Reveal(item,animate);}
    private void Reveal(System.Windows.Controls.TabItem item,bool animate)
    {
        if(_headerScroll is null||_headerTrack is null||!HasOverflow)return;
        double start=item.TranslatePoint(new Point(),_headerTrack).X+4,end=start+item.ActualWidth;
        double offset=_headerScroll.HorizontalOffset,viewport=_headerScroll.ViewportWidth;
        if(start<offset+33)ScrollTo(start-33,animate);else if(end>offset+viewport-33)ScrollTo(end-viewport+33,animate);
    }
}


