using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public partial class HoverCard
{
    private Point _placementPoint;
    private Rect _lastAnchor = Rect.Empty;
    private Size _lastOwnerSize;
    private DispatcherOperation? _placementUpdate;

    private static void OnCardContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HoverCard)d;
        if (e.NewValue is null && card.IsOpen) card.CloseImmediate();
        else card.QueuePlacement();
    }
    private void CardSized(object sender, SizeChangedEventArgs e) => QueuePlacement();
    private void OwnerClosed(object? sender, EventArgs e) => CloseImmediate();
    private void OwnerMoved(object? sender, EventArgs e) => QueuePlacement();
    private void OwnerSized(object sender, SizeChangedEventArgs e) => QueuePlacement();
    private void OwnerDpiChanged(object sender, DpiChangedEventArgs e) => QueuePlacement();
    private void OutsideMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen || _trigger is null || DialogHost.Contains(_trigger, e.OriginalSource as DependencyObject)) return;
        CloseImmediate();
    }
    private void DetachOwner()
    {
        if (_owner is null) return;
        _owner.Deactivated -= OwnerDeactivated;
        _owner.Closed -= OwnerClosed;
        _owner.LocationChanged -= OwnerMoved;
        _owner.SizeChanged -= OwnerSized;
        _owner.DpiChanged -= OwnerDpiChanged;
        _owner.PreviewMouseDown -= OutsideMouseDown;
        _owner = null;
    }
    private void TrackAnchor(object? sender, EventArgs e)
    {
        if (!IsOpen || _owner is null || _trigger is null) return;
        if (!Popover.AnchorVisible(_trigger, _owner)) { CloseImmediate(); return; }
        Rect bounds = Popover.ScreenBounds(_trigger);
        if (bounds != _lastAnchor || _owner.RenderSize != _lastOwnerSize) QueuePlacement();
    }
    private void QueuePlacement()
    {
        if (!IsOpen || _placementUpdate is not null) return;
        _placementUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _placementUpdate = null;
            UpdatePlacement();
        }));
    }
    private void StopPlacementTracking()
    {
        _placementUpdate?.Abort();
        _placementUpdate = null;
        LayoutUpdated -= TrackAnchor;
        _lastAnchor = Rect.Empty;
    }
    private void UpdatePlacement()
    {
        if (_trigger is null || _owner is null || _card is null || _frame is null || _popup is null || !IsLoaded) return;
        if (!Popover.AnchorVisible(_trigger, _owner)) { CloseImmediate(); return; }
        Rect pixels = Popover.ScreenBounds(_trigger);
        var dpi = VisualTreeHelper.GetDpi(_trigger);
        Rect anchor = Popover.ToDip(pixels, dpi);
        Rect work = Popover.ToDip(Popover.MonitorWorkArea(pixels), dpi);
        double maxWidth = Math.Max(0, work.Width - 24);
        double maxHeight = Math.Max(0, work.Height - 24);
        _card.SetCurrentValue(MaxWidthProperty, Math.Min(296, maxWidth));
        _card.SetCurrentValue(MaxHeightProperty, maxHeight);
        _card.Measure(new Size(maxWidth, maxHeight));
        var result = PopoverPositioner.Calculate(anchor, _card.DesiredSize, work,
            Side switch { HoverCardSide.Top => PopoverSide.Top, HoverCardSide.Left => PopoverSide.Left,
                HoverCardSide.Right => PopoverSide.Right, _ => PopoverSide.Bottom },
            Alignment switch { HoverCardAlignment.Center => PopoverAlignment.Center,
                HoverCardAlignment.End => PopoverAlignment.End, _ => PopoverAlignment.Start },
            8, 12, FlowDirection == FlowDirection.RightToLeft);
        _card.SetCurrentValue(MaxWidthProperty, result.Bounds.Width);
        _card.SetCurrentValue(MaxHeightProperty, result.Bounds.Height);
        var gutter = new Thickness(Math.Clamp(result.Bounds.Left - work.Left, 0, 48),
            Math.Clamp(result.Bounds.Top - work.Top, 0, 48),
            Math.Clamp(work.Right - result.Bounds.Right, 0, 48),
            Math.Clamp(work.Bottom - result.Bounds.Bottom, 0, 48));
        _frame.Padding = gutter;
        _placementPoint = new Point(result.Bounds.X - anchor.X - gutter.Left, result.Bounds.Y - anchor.Y - gutter.Top);
        SetValue(ActualSidePropertyKey, result.Side switch { PopoverSide.Top => HoverCardSide.Top,
            PopoverSide.Left => HoverCardSide.Left, PopoverSide.Right => HoverCardSide.Right, _ => HoverCardSide.Bottom });
        _lastAnchor = pixels;
        _lastOwnerSize = _owner.RenderSize;
        _popup.HorizontalOffset = _popup.HorizontalOffset == 0 ? .001 : 0;
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset) =>
        [new CustomPopupPlacement(_placementPoint, PopupPrimaryAxis.None)];
}
