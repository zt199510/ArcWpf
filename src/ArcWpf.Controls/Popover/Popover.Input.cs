using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ArcWpf.Controls;

public partial class Popover
{
    internal static bool IsWithin(DependencyObject? candidate, DependencyObject? ancestor)
    {
        if (ancestor is null) { return false; }
        for (var node = candidate; node is not null; node = ParentOf(node))
        {
            if (ReferenceEquals(node, ancestor)) { return true; }
        }
        return false;
    }

    private void OutsideInput(object sender, PreProcessInputEventArgs e)
    {
        if (!IsOpen || !CloseOnOutsideClick || e.StagingItem.Input is not MouseButtonEventArgs mouse ||
            mouse.RoutedEvent != Mouse.PreviewMouseDownEvent || mouse.ChangedButton is not (MouseButton.Left or MouseButton.Right)) { return; }
        // PreProcessInput can precede routed-event source assignment.
        var origin = mouse.OriginalSource as DependencyObject ?? mouse.MouseDevice.DirectlyOver as DependencyObject;
        if (origin is null) { return; }
        if (!IsWithin(origin, _root) && !IsWithin(origin, _anchor)) { RequestClose(PopoverCloseReason.OutsidePointer); }
        // Do not mark the staging input handled: the external click retains its own action/focus.
    }

    private void ContentKeyDown(object sender, KeyEventArgs e)
    {
        if (IsOpen && CloseOnEscape && !e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            RequestClose(PopoverCloseReason.Escape);
        }
    }

    private List<UIElement> FocusTargets()
    {
        var targets = new List<UIElement>();
        void Walk(DependencyObject node)
        {
            if (node is UIElement element && (!element.IsVisible || !element.IsEnabled)) { return; }
            if (node is UIElement focus && !ReferenceEquals(node, _surface) && focus.Focusable && KeyboardNavigation.GetIsTabStop(focus)) { targets.Add(focus); }
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) { Walk(VisualTreeHelper.GetChild(node, i)); }
        }
        if (_root is not null) { Walk(_root); }
        return targets.OrderBy(KeyboardNavigation.GetTabIndex).ToList();
    }

    private void FocusContent()
    {
        var first = FocusTargets().FirstOrDefault();
        if (first is not null && first.Focus()) { return; }
        if (_surface is not null) { _surface.Focus(); }
    }

    private void ContentPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpen || e.Key != Key.Tab || _anchor is null || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) { return; }
        bool backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var targets = FocusTargets();
        var focused = Keyboard.FocusedElement as DependencyObject;
        int index = targets.FindIndex(item => IsWithin(focused, item));
        bool boundary = targets.Count == 0 || (backwards ? index <= 0 : index == targets.Count - 1 || index < 0);
        if (!boundary) { return; }
        e.Handled = true;
        _anchor.Focus();
        _anchor.MoveFocus(new TraversalRequest(backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
    }

    private void RestoreFocus(PopoverCloseReason reason)
    {
        if (_owner?.IsActive != true) { return; }
        bool inside = IsWithin(Keyboard.FocusedElement as DependencyObject, _root);
        if (reason == PopoverCloseReason.Escape || (inside && reason == PopoverCloseReason.Programmatic))
        {
            if (_anchor is { IsLoaded: true, IsVisible: true, IsEnabled: true } && _anchor.Focus()) { return; }
            RestorePreviousFocus();
        }
        else if (inside && reason is PopoverCloseReason.AnchorUnavailable or PopoverCloseReason.Unloaded or PopoverCloseReason.TemplateChanged)
        {
            RestorePreviousFocus();
        }
    }

    private void RestorePreviousFocus()
    {
        if (_previousFocus is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element &&
            PresentationSource.FromVisual(element) is not null && !IsWithin(element, _root))
        {
            element.Focus();
        }
    }
}
