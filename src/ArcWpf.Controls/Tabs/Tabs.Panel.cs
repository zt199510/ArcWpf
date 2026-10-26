using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ArcWpf.Controls;

public partial class Tabs
{
    private BitmapSource? CaptureOutgoing()
    {
        if(_content is null||_content.ActualWidth<=0||_content.ActualHeight<=0)return null;
        var dpi=VisualTreeHelper.GetDpi(_content);
        double w=Math.Ceiling(_content.ActualWidth*dpi.DpiScaleX),h=Math.Ceiling(_content.ActualHeight*dpi.DpiScaleY);
        if(w>4096||h>4096||w*h>8_000_000)return null;
        try
        {
            var bitmap=new RenderTargetBitmap((int)w,(int)h,dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
            bitmap.Render(_content);bitmap.Freeze();return bitmap;
        }
        catch(InvalidOperationException){return null;}
        catch(ArgumentException){return null;}
    }
    internal void BeginPanelTransition(double oldHeight,double newHeight,BitmapSource? snapshot,int direction,bool animate)
    {
        _fade.Stop();_panelActive=_heightActive=false;
        if(_panelFrame is null||_content is null)return;
        int token=++_version;
        _panelFrame.Height=double.NaN;_panelFrame.ClipToBounds=false;
        _content.Opacity=1;if(_contentMove is not null)_contentMove.X=0;
        if(_outgoing is not null){_outgoing.Source=null;_outgoing.Opacity=0;}
        var level=ArcMotion.GetLevel(this);
        if(!animate||level==ArcMotionLevel.Off)return;
        bool full=level==ArcMotionLevel.Full;
        if(full)
        {
            _panelX.Snap(direction*8);_panelX.Target=0;_panelActive=true;
            if(_contentMove is not null)_contentMove.X=_panelX.Position;
            if(Math.Abs(newHeight-oldHeight)>1&&double.IsFinite(newHeight)&&double.IsFinite(oldHeight))
            {_panelHeight.Snap(oldHeight);_panelHeight.Target=newHeight;_heightActive=true;_panelFrame.Height=oldHeight;_panelFrame.ClipToBounds=true;}
            StartFrames();
        }
        _content.Opacity=0;_fade.Double(_content,OpacityProperty,1,full?.24:.12,true,enter:true);
        if(_outgoing is not null&&snapshot is not null)
        {
            _outgoing.Source=snapshot;_outgoing.Width=snapshot.Width;_outgoing.Height=snapshot.Height;_outgoing.Opacity=1;
            if(_outgoingMove is not null){_outgoingMove.X=0;_fade.Double(_outgoingMove,TranslateTransform.XProperty,full?-direction*6:0,.12,full);}
            _fade.Double(_outgoing,OpacityProperty,0,full?.12:.1,true,completed:()=>{if(token==_version&&_outgoing is not null)_outgoing.Source=null;});
        }
    }
    private void PanelSized(object sender,SizeChangedEventArgs e)
    {
        if(!e.WidthChanged||e.PreviousSize.Width<=0)return;
        _fade.Stop();_panelActive=_heightActive=false;
        if(_panelFrame is not null){_panelFrame.Height=double.NaN;_panelFrame.ClipToBounds=false;}
        if(_content is not null)_content.Opacity=1;if(_contentMove is not null)_contentMove.X=0;
        if(_outgoing is not null){_outgoing.Source=null;_outgoing.Opacity=0;}
        if(!_indicatorActive)StopFrames();
    }
}

/// <summary>Inert transition image; the native selected presenter remains the accessible content.</summary>
public sealed class TabsSnapshot:Image
{
    protected override AutomationPeer? OnCreateAutomationPeer()=>null;
}
