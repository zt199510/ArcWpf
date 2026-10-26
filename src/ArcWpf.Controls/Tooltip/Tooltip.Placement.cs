using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace ArcWpf.Controls;
public partial class Tooltip
{
    private void AttachOwner(Window owner)
    {
        if(ReferenceEquals(_owner,owner))return;DetachOwner();_owner=owner;
        owner.LocationChanged+=OwnerMoved;owner.SizeChanged+=OwnerSized;owner.Deactivated+=OwnerEnded;owner.Closed+=OwnerEnded;owner.PreviewKeyDown+=OwnerKey;
    }
    private void DetachOwner(){if(_owner is null)return;_owner.LocationChanged-=OwnerMoved;_owner.SizeChanged-=OwnerSized;_owner.Deactivated-=OwnerEnded;_owner.Closed-=OwnerEnded;_owner.PreviewKeyDown-=OwnerKey;_owner=null;}
    private void OwnerMoved(object? sender,EventArgs e)=>UpdatePlacement();
    private void OwnerSized(object sender,SizeChangedEventArgs e)=>UpdatePlacement();
    private void OwnerEnded(object? sender,EventArgs e)=>CloseImmediately();
    private void OwnerKey(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&IsOpen){_suppressed=true;Close();e.Handled=true;}}
    private void TrackAnchor(object? sender,EventArgs e)
    {
        if(_owner is null)return;if(!Popover.AnchorVisible(this,_owner)){CloseImmediately();return;}
        if(Popover.ScreenBounds(this)!=_lastAnchor)UpdatePlacement();
    }
    private void UpdatePlacement()
    {
        if(_placing||_owner is null||_surface is null||_popup is null||!IsPresented)return;
        if(!Popover.AnchorVisible(this,_owner)){CloseImmediately();return;}
        _placing=true;
        try
        {
            var pixels=Popover.ScreenBounds(this);var dpi=VisualTreeHelper.GetDpi(this);var anchor=Popover.ToDip(pixels,dpi);var work=Popover.ToDip(Popover.MonitorWorkArea(pixels),dpi);
            _surface.MaxWidth=Math.Max(0,Math.Min(240,work.Width-24));_surface.MaxHeight=Math.Max(0,work.Height-24);_surface.Measure(new Size(_surface.MaxWidth,_surface.MaxHeight));
            var result=PopoverPositioner.Calculate(anchor,_surface.DesiredSize,work,Side,PopoverAlignment.Center,8,12,FlowDirection==FlowDirection.RightToLeft);
            _surface.MaxWidth=result.Bounds.Width;_surface.MaxHeight=result.Bounds.Height;SetValue(ActualSidePropertyKey,result.Side);
            _surface.RenderTransformOrigin=result.Side switch{PopoverSide.Top=>new Point(.5,1),PopoverSide.Bottom=>new Point(.5,0),PopoverSide.Left=>new Point(1,.5),_=>new Point(0,.5)};
            var padding=new Thickness(Math.Clamp(result.Bounds.Left-work.Left,0,24),Math.Clamp(result.Bounds.Top-work.Top,0,24),Math.Clamp(work.Right-result.Bounds.Right,0,24),Math.Clamp(work.Bottom-result.Bounds.Bottom,0,24));
            if(_frame is not null)_frame.Padding=padding;
            _placement=new Point(result.Bounds.Left-anchor.Left-padding.Left,result.Bounds.Top-anchor.Top-padding.Top);_lastAnchor=pixels;
            _popup.HorizontalOffset=_popup.HorizontalOffset==0?.001:0;
        }
        finally{_placing=false;}
    }
}
