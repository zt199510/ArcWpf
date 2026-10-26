using System.Windows;
namespace ArcWpf.Controls;

public enum PopoverSide { Top, Bottom, Left, Right }
public enum PopoverAlignment { Start, Center, End }
public enum PopoverCloseReason { Programmatic, Escape, OutsidePointer, AnchorUnavailable, OwnerDeactivated, OwnerClosed, Unloaded, TemplateChanged, Replaced }

public sealed class PopoverClosedEventArgs(RoutedEvent routedEvent, object source, PopoverCloseReason reason) : RoutedEventArgs(routedEvent, source)
{
    public PopoverCloseReason Reason { get; } = reason;
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget) =>
        ((EventHandler<PopoverClosedEventArgs>)genericHandler)(genericTarget, this);
}
