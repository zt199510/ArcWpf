using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
public class ReorderableList : ListBox
{
    public static readonly DependencyProperty CanReorderProperty = DependencyProperty.Register(nameof(CanReorder), typeof(bool), typeof(ReorderableList), new FrameworkPropertyMetadata(true));
    public static readonly RoutedEvent ItemMovedEvent = EventManager.RegisterRoutedEvent(nameof(ItemMoved), RoutingStrategy.Bubble, typeof(EventHandler<ReorderableListItemMovedEventArgs>), typeof(ReorderableList));
    private Point _dragStart; private ListBoxItem? _dragItem; private bool _dragging;
    public bool CanReorder { get => (bool)GetValue(CanReorderProperty); set => SetValue(CanReorderProperty, value); }
    public event EventHandler<ReorderableListItemMovedEventArgs> ItemMoved { add => AddHandler(ItemMovedEvent, value); remove => RemoveHandler(ItemMovedEvent, value); }

    static ReorderableList() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ReorderableList), new FrameworkPropertyMetadata(typeof(ReorderableList))); }
    public ReorderableList() { SelectionMode = SelectionMode.Single; }
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListBoxItem;
    protected override DependencyObject GetContainerForItemOverride() => new ListBoxItem();
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(DragStart), true); AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(DragMove), true); AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(DragEnd), true); AddHandler(PreviewKeyDownEvent, new KeyEventHandler(KeyMove), true);
    }
    private void DragStart(object? sender, MouseButtonEventArgs e) { if (!CanReorder || e.OriginalSource is not DependencyObject d) return; _dragItem = FindItem(d); if (_dragItem is null) return; _dragStart = e.GetPosition(this); _dragging = false; }
    private void DragMove(object? sender, MouseEventArgs e) { if (!CanReorder || _dragItem is null || e.LeftButton != MouseButtonState.Pressed) return; if (!_dragging && (e.GetPosition(this) - _dragStart).Length < 6) return; _dragging = true; var target = FindItem(e.OriginalSource as DependencyObject); if (target is null || target == _dragItem) return; MoveItem(_dragItem, ItemContainerGenerator.IndexFromContainer(_dragItem), ItemContainerGenerator.IndexFromContainer(target)); }
    private void DragEnd(object? sender, MouseButtonEventArgs e) { _dragItem = null; _dragging = false; }
    private void KeyMove(object? sender, KeyEventArgs e)
    {
        if (!CanReorder || SelectedIndex < 0) return; int old = SelectedIndex; int next = e.Key switch { Key.Up => old - 1, Key.Down => old + 1, Key.Home => 0, Key.End => Items.Count - 1, _ => old }; if (next == old || next < 0 || next >= Items.Count || e.Key is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return; if (e.Key is Key.PageUp) next = 0; if (e.Key is Key.PageDown) next = Items.Count - 1; MoveItem(ItemContainerGenerator.ContainerFromIndex(old) as ListBoxItem, old, next); e.Handled = true;
    }
    private void MoveItem(ListBoxItem? container, int oldIndex, int newIndex)
    {
        if (ItemsSource is not IList list || oldIndex < 0 || newIndex < 0 || oldIndex >= list.Count || newIndex >= list.Count) return; object item = list[oldIndex]!; list.RemoveAt(oldIndex); list.Insert(newIndex, item); SelectedIndex = newIndex; RaiseEvent(new ReorderableListItemMovedEventArgs(ItemMovedEvent, this, item, oldIndex, newIndex));
    }
    private static ListBoxItem? FindItem(DependencyObject? source) { while (source is not null) { if (source is ListBoxItem item) return item; source = System.Windows.Media.VisualTreeHelper.GetParent(source); } return null; }
    protected override AutomationPeer OnCreateAutomationPeer() => new ReorderableListPeer(this);
    private sealed class ReorderableListPeer(ReorderableList owner) : ListBoxAutomationPeer(owner) { protected override string GetClassNameCore() => nameof(ReorderableList); }
}

public sealed class ReorderableListItemMovedEventArgs(RoutedEvent routedEvent, object source, object item, int oldIndex, int newIndex) : RoutedEventArgs(routedEvent, source)
{ public object Item { get; } = item; public int OldIndex { get; } = oldIndex; public int NewIndex { get; } = newIndex; }

