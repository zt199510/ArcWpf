using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
public class ScrollArea : ContentControl
{
    // 仅包装原生 ScrollViewer，保留 WPF 内容、滚轮、键盘和虚拟化语义。
    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner(typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled));
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner(typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));
    public static readonly DependencyProperty CanContentScrollProperty = ScrollViewer.CanContentScrollProperty.AddOwner(typeof(ScrollArea), new FrameworkPropertyMetadata(true));
    public ScrollBarVisibility HorizontalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty); set => SetValue(HorizontalScrollBarVisibilityProperty, value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); set => SetValue(VerticalScrollBarVisibilityProperty, value); }
    public bool CanContentScroll { get => (bool)GetValue(CanContentScrollProperty); set => SetValue(CanContentScrollProperty, value); }
    static ScrollArea() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(typeof(ScrollArea))); }
}
