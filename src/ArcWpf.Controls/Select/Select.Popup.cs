using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace ArcWpf.Controls;
public partial class Select
{
    private Point _placement;
    private Rect _anchorPixels=Rect.Empty;
    private PopoverSide _actualSide=PopoverSide.Bottom;
    private void ChangePresentation()
    {
        if(_releasing){if(IsDropDownOpen)SetCurrentValue(IsDropDownOpenProperty,false);return;}
        if(IsDropDownOpen)
        {
            ApplyTemplate();
            if(!IsDropDownOpen)return;
            if(_popup is null||_trigger is null||_popupRoot is null||!IsLoaded||!IsEnabled||Window.GetWindow(this) is not {} owner||!Popover.AnchorVisible(_trigger,owner))
            {SetCurrentValue(IsDropDownOpenProperty,false);return;}
            ++_popupVersion;
            if(_owner is null){_owner=owner;owner.Deactivated+=OwnerUnavailable;owner.Closed+=OwnerUnavailable;owner.LocationChanged+=OwnerMoved;owner.SizeChanged+=OwnerSized;owner.DpiChanged+=OwnerDpi;}
            _popupRoot.IsHitTestVisible=true;UpdatePlacement();
            if(!IsDropDownOpen)return;
            if(_popup.IsOpen){AnimatePopup(true);return;}
            _popupMotion.Stop();
            if(_surface is not null)_surface.Opacity=ArcMotion.GetLevel(this)==ArcMotionLevel.Off?1:0;
            bool full=ArcMotion.GetLevel(this)==ArcMotionLevel.Full;
            if(_popupScale is not null)_popupScale.ScaleX=_popupScale.ScaleY=full?.97:1;
            if(_popupMove is not null)_popupMove.Y=full?(_actualSide==PopoverSide.Top?6:-6):0;
            _popup.IsOpen=true;
        }
        else if(_popup?.IsOpen==true)
        {++_popupVersion;if(_popupRoot is not null)_popupRoot.IsHitTestVisible=false;AnimatePopup(false);}
    }
    private void PopupOpened(object? sender,EventArgs e)
    {
        if(!IsDropDownOpen){FinishClose(_popupVersion);return;}
        SetValue(IsPresentedPropertyKey,true);UpdatePlacement();if(IsDropDownOpen)AnimatePopup(true);
    }
    private void PopupClosed(object? sender,EventArgs e)
    {
        if(_popup?.IsOpen==true)return; // A native Closed handler may have reopened it.
        SetValue(IsPresentedPropertyKey,false);_popupMotion.Stop();UnsubscribeOwner();
        if(IsDropDownOpen)SetCurrentValue(IsDropDownOpenProperty,false);
    }
    private void AnimatePopup(bool opening)
    {
        int token=_popupVersion;var level=ArcMotion.GetLevel(this);bool animate=level!=ArcMotionLevel.Off;
        bool full=level==ArcMotionLevel.Full;double duration=opening&&full?.24:.12;
        if(_surface is null){if(!opening)FinishClose(token);return;}
        _surface.RenderTransformOrigin=new Point(FlowDirection==FlowDirection.RightToLeft?1:0,_actualSide==PopoverSide.Top?1:0);
        if(_popupScale is not null)
        {
            _popupMotion.Double(_popupScale,ScaleTransform.ScaleXProperty,!opening&&full?.98:1,duration,animate&&full,enter:opening);
            _popupMotion.Double(_popupScale,ScaleTransform.ScaleYProperty,!opening&&full?.98:1,duration,animate&&full,enter:opening);
        }
        if(_popupMove is not null)_popupMotion.Double(_popupMove,TranslateTransform.YProperty,!opening&&full?(_actualSide==PopoverSide.Top?3:-3):0,duration,animate&&full,enter:opening);
        _popupMotion.Double(_surface,OpacityProperty,opening?1:0,duration,animate,completed:opening?null:()=>FinishClose(token),enter:opening&&full);
    }
    private void FinishClose(int token)
    {
        if(token!=_popupVersion||IsDropDownOpen)return;
        _popupMotion.Stop();if(_popup is not null)_popup.IsOpen=false;
        if(token!=_popupVersion||IsDropDownOpen)return;
        SetValue(IsPresentedPropertyKey,false);UnsubscribeOwner();
    }
    private void ReleasePresentation()
    {
        ++_popupVersion;_popupMotion.Stop();
        bool previous=_releasing;_releasing=true;
        try{SetCurrentValue(IsDropDownOpenProperty,false);if(_popup is not null)_popup.IsOpen=false;SetValue(IsPresentedPropertyKey,false);UnsubscribeOwner();}
        finally{_releasing=previous;}
        _valueMotion.Stop();_triggerMotion.Stop();RefreshValue(false);UpdateTrigger(false);
    }
    private void UnsubscribeOwner()
    {
        _positionUpdate?.Abort();_positionUpdate=null;
        if(_owner is not null){_owner.Deactivated-=OwnerUnavailable;_owner.Closed-=OwnerUnavailable;_owner.LocationChanged-=OwnerMoved;_owner.SizeChanged-=OwnerSized;_owner.DpiChanged-=OwnerDpi;_owner=null;}
        _anchorPixels=Rect.Empty;
    }
    private void OwnerUnavailable(object? sender,EventArgs e)=>ReleasePresentation();
    private void OwnerMoved(object? sender,EventArgs e)=>QueuePosition();
    private void OwnerSized(object sender,SizeChangedEventArgs e)=>QueuePosition();
    private void OwnerDpi(object sender,DpiChangedEventArgs e)=>QueuePosition();
    private void PopupSized(object sender,SizeChangedEventArgs e)=>QueuePosition();
    private void AnchorLayout(object? sender,EventArgs e)
    {
        if(_owner is null||_trigger is null)return;
        if(!Popover.AnchorVisible(_trigger,_owner)){ReleasePresentation();return;}
        if(Popover.ScreenBounds(_trigger)!=_anchorPixels)QueuePosition();
    }
    private void QueuePosition()
    {
        if(_owner is null||_positionUpdate is not null)return;
        _positionUpdate=Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>{_positionUpdate=null;UpdatePlacement();}));
    }
    private void UpdatePlacement()
    {
        if(_owner is null||_trigger is null||_popup is null||_surface is null)return;
        if(!Popover.AnchorVisible(_trigger,_owner)){ReleasePresentation();return;}
        var pixels=Popover.ScreenBounds(_trigger);var dpi=VisualTreeHelper.GetDpi(_trigger);
        var anchor=Popover.ToDip(pixels,dpi);var work=Popover.ToDip(Popover.MonitorWorkArea(pixels),dpi);
        double width=Math.Min(anchor.Width,Math.Max(0,work.Width-24));
        double height=Math.Min(MaxDropDownHeight,Math.Max(0,work.Height-24));
        _surface.Width=width;_surface.MaxHeight=height;_surface.Measure(new Size(width,height));
        var result=PopoverPositioner.Calculate(anchor,_surface.DesiredSize,work,PopoverSide.Bottom,PopoverAlignment.Start,4,12,FlowDirection==FlowDirection.RightToLeft);
        _surface.Width=result.Bounds.Width;_surface.MaxHeight=result.Bounds.Height;_actualSide=result.Side;
        var gutter=new Thickness(Math.Clamp(result.Bounds.Left-work.Left,0,48),Math.Clamp(result.Bounds.Top-work.Top,0,48),Math.Clamp(work.Right-result.Bounds.Right,0,48),Math.Clamp(work.Bottom-result.Bounds.Bottom,0,48));
        if(_popupRoot is Border frame)frame.Padding=gutter;else gutter=new Thickness();
        _placement=new Point(result.Bounds.Left-anchor.Left-gutter.Left,result.Bounds.Top-anchor.Top-gutter.Top);
        _anchorPixels=pixels;_popup.HorizontalOffset=_popup.HorizontalOffset==0?.001:0;
    }
}
