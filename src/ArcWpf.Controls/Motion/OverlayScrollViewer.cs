using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

/// <summary>A scroller whose bars float over the content and show on hover, so a bar appearing never moves the layout Arc measured.</summary>
public sealed class OverlayScrollViewer : ScrollViewer
{
    static OverlayScrollViewer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(OverlayScrollViewer), new FrameworkPropertyMetadata(typeof(OverlayScrollViewer)));
}
