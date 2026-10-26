using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ArcWpf.Controls;

public partial class Popover
{
    private Point _placementPoint;
    private double _surfaceMinWidth = 192;
    private double _surfaceMaxWidth = 352;

    private void UpdatePlacement()
    {
        if (_anchor is null || _owner is null || _root is null || _popup is null) { return; }
        if (!AnchorVisible(_anchor, _owner)) { RequestClose(PopoverCloseReason.AnchorUnavailable, true); return; }
        Rect pixels = ScreenBounds(_anchor);
        var dpi = VisualTreeHelper.GetDpi(_anchor);
        Rect anchor = ToDip(pixels, dpi);
        Rect work = ToDip(MonitorWorkArea(pixels), dpi);
        double maxWidth = Math.Max(0, work.Width - 2 * Math.Min(CollisionPadding, work.Width / 2));
        double maxHeight = Math.Max(0, work.Height - 2 * Math.Min(CollisionPadding, work.Height / 2));
        var target = _surface ?? _root;
        target.SetCurrentValue(MinWidthProperty, Math.Min(_surfaceMinWidth, maxWidth));
        target.SetCurrentValue(MaxWidthProperty, Math.Min(_surfaceMaxWidth, maxWidth));
        target.SetCurrentValue(MaxHeightProperty, maxHeight);
        target.Measure(new Size(maxWidth, maxHeight));
        var result = PopoverPositioner.Calculate(anchor, target.DesiredSize, work, Side, Alignment,
            SideOffset, CollisionPadding, FlowDirection == FlowDirection.RightToLeft);
        target.SetCurrentValue(MinWidthProperty, Math.Min(_surfaceMinWidth, result.Bounds.Width));
        target.SetCurrentValue(MaxWidthProperty, result.Bounds.Width);
        target.SetCurrentValue(MaxHeightProperty, result.Bounds.Height);
        // Room for the floating shadow. Near the work-area edge the shadow may clip, not shift the visible panel.
        var gutter = new Thickness(Math.Clamp(result.Bounds.Left - work.Left, 0, 48),
            Math.Clamp(result.Bounds.Top - work.Top, 0, 48),
            Math.Clamp(work.Right - result.Bounds.Right, 0, 48),
            Math.Clamp(work.Bottom - result.Bounds.Bottom, 0, 48));
        if (_root is Border frame && _surface is not null) { frame.Padding = gutter; }
        else { gutter = new Thickness(); }
        _placementPoint = new Point(result.Bounds.X - anchor.X - gutter.Left, result.Bounds.Y - anchor.Y - gutter.Top);
        SetValue(ActualSidePropertyKey, result.Side);
        _lastAnchor = pixels;
        _lastOwnerSize = _owner.RenderSize;
        // Popup does not follow owner moves by itself; a changed offset invalidates custom placement.
        _popup.HorizontalOffset = _popup.HorizontalOffset == 0 ? .001 : 0;
    }

    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset) =>
        [new CustomPopupPlacement(_placementPoint, PopupPrimaryAxis.None)];

    internal static Rect ToDip(Rect rect, DpiScale dpi) => new(rect.X / dpi.DpiScaleX, rect.Y / dpi.DpiScaleY, rect.Width / dpi.DpiScaleX, rect.Height / dpi.DpiScaleY);
    internal static Rect ScreenBounds(FrameworkElement element)
    {
        var topLeft = element.PointToScreen(new Point());
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    internal static bool AnchorVisible(FrameworkElement anchor, Window owner)
    {
        if (!anchor.IsLoaded || !anchor.IsVisible || anchor.ActualWidth <= 0 || anchor.ActualHeight <= 0 ||
            PresentationSource.FromVisual(anchor) is null) { return false; }
        Rect visible = new(new Point(), anchor.RenderSize);
        DependencyObject? parent = ParentOf(anchor);
        while (parent is not null)
        {
            if (parent is UIElement element && (element.ClipToBounds || element.Clip is not null))
            {
                var clip = element.Clip?.Bounds ?? new Rect(new Point(), element.RenderSize);
                try { visible.Intersect(element.TransformToVisual(anchor).TransformBounds(clip)); }
                catch (InvalidOperationException) { return false; }
                if (visible.IsEmpty || visible.Width <= 0 || visible.Height <= 0) { return false; }
            }
            if (ReferenceEquals(parent, owner)) { break; }
            parent = ParentOf(parent);
        }
        return true;
    }

    private static DependencyObject? ParentOf(DependencyObject value)
    {
        if (value is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(value);
            if (visualParent is not null) { return visualParent; }
        }
        return value is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(value);
    }

    internal static Rect MonitorWorkArea(Rect anchor)
    {
        var rect = new NativeRect { Left = (int)Math.Floor(anchor.Left), Top = (int)Math.Floor(anchor.Top),
            Right = (int)Math.Ceiling(anchor.Right), Bottom = (int)Math.Ceiling(anchor.Bottom) };
        nint monitor = MonitorFromRect(ref rect, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor != 0 && GetMonitorInfo(monitor, ref info))
        {
            return new Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
        }
        // MonitorFromRect with nearest should succeed; use the anchor monitor's device scale in the caller.
        throw new InvalidOperationException("Cannot determine the anchor monitor work area.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromRect(ref NativeRect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
