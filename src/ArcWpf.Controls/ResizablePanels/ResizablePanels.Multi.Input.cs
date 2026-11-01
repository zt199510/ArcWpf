using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public partial class ResizablePanels
{
    private void MultiDragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not GridSplitter { Tag: int index }) return;
        StopMultiMotion();
        _multiDragHandle = index;
        double available = _layout is null ? 0 : _layout.ActualWidth - 6 * (Panels.Count - 1);
        _multiDragStartShares = available > 0
            ? Enumerable.Range(0, Panels.Count).Select(at => _layout!.ColumnDefinitions[at * 2].ActualWidth / available).ToArray()
            : (double[])_multiShares.Clone();
        _multiShares = (double[])_multiDragStartShares.Clone();
        _multiOvershoot = 0;
        _multiPointerDelta = 0;
        _multiDragProjection.Start();
    }
    private void MultiDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not GridSplitter { Tag: int index } || _layout is null || index >= Panels.Count - 1) return;
        if (_multiDragStartShares is null || _multiDragHandle != index) return;
        double available = _layout.ActualWidth - 6 * (Panels.Count - 1);
        if (available <= 0) return;
        _multiPointerDelta += e.HorizontalChange;
        _multiDragProjection.Add(e.HorizontalChange);
        double startA = _multiDragStartShares[index] * available;
        double pair = (_multiDragStartShares[index] + _multiDragStartShares[index + 1]) * available;
        var before = _layout.ColumnDefinitions[index * 2];
        var after = _layout.ColumnDefinitions[(index + 1) * 2];
        double low = Math.Max(before.MinWidth, pair - after.MaxWidth);
        double high = Math.Min(before.MaxWidth, pair - after.MinWidth);
        if (low > high) low = high = Math.Clamp(startA, 0, pair);
        double raw = startA + _multiPointerDelta;
        double target = Math.Clamp(raw, low, high);
        _multiOvershoot = raw - target;
        _multiShares[index] = target / available;
        _multiShares[index + 1] = (pair - target) / available;
        ApplyMultiLayout();
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full || Math.Abs(_multiOvershoot) < .01) return;
        var handle = _multiHandles[index];
        double distance = Math.Abs(_multiOvershoot);
        handle.Offset.X = Math.CopySign(distance * 44 * .55 / (44 + .55 * distance), _multiOvershoot);
    }
    private void MultiDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not GridSplitter { Tag: int index } || _layout is null || index >= _multiHandles.Count) return;
        var handle = _multiHandles[index];
        _multiMotion.Double(handle.Offset, TranslateTransform.XProperty, 0, .26, ArcMotion.GetLevel(this) == ArcMotionLevel.Full, spring: true);
        _multiDragHandle = -1;
        if (e.Canceled)
        {
            if (_multiDragStartShares is not null) _multiShares = _multiDragStartShares;
            ApplyMultiLayout();
            _multiDragStartShares = null;
            _multiOvershoot = 0;
            _multiPointerDelta = 0;
            return;
        }
        double available = _layout.ActualWidth - 6 * (Panels.Count - 1);
        double start = _multiDragStartShares is null ? 0 : _multiDragStartShares[index] * available;
        double pair = _multiDragStartShares is null ? 0 : (_multiDragStartShares[index] + _multiDragStartShares[index + 1]) * available;
        double raw = start + _multiPointerDelta + _multiDragProjection.Project();
        if (raw < _layout.ColumnDefinitions[index * 2].MinWidth - Math.Max(Panels[index].MinSize * .5, 40) && Panels[index].Collapsible)
            Panels[index].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true);
        else if (pair - raw < _layout.ColumnDefinitions[(index + 1) * 2].MinWidth - Math.Max(Panels[index + 1].MinSize * .5, 40) && Panels[index + 1].Collapsible)
            Panels[index + 1].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true);
        else ApplyMultiLayout();
        RaiseLayoutChanged();
        _multiDragStartShares = null;
        _multiOvershoot = 0;
        _multiPointerDelta = 0;
    }
    private void MultiDoubleClicked(object sender, MouseButtonEventArgs e) { ResetMulti(); e.Handled = true; }
    private void MultiRestoreClicked(object sender, RoutedEventArgs e)
    {
        var handle = _multiHandles.FirstOrDefault(item => ReferenceEquals(item.Restore, sender));
        if (handle is null) return;
        RestoreMulti(handle.Index);
        handle.Splitter.Focus();
        e.Handled = true;
    }
    private void RestoreMulti(int handle)
    {
        if (Panels[handle].IsCollapsed) Panels[handle].SetCurrentValue(ResizablePanel.IsCollapsedProperty, false);
        else if (Panels[handle + 1].IsCollapsed) Panels[handle + 1].SetCurrentValue(ResizablePanel.IsCollapsedProperty, false);
    }
    private void MultiKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not GridSplitter { Tag: int index } splitter || _layout is null || index >= Panels.Count - 1) return;
        if (e.Key == Key.Escape && _multiDragHandle == index) { splitter.CancelDrag(); e.Handled = true; return; }
        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Enter)) return;
        bool beforeCollapsed = Panels[index].IsCollapsed, afterCollapsed = Panels[index + 1].IsCollapsed;
        if (beforeCollapsed || afterCollapsed)
        {
            if (e.Key == Key.Enter || beforeCollapsed && e.Key == Key.Right || afterCollapsed && e.Key == Key.Left) RestoreMulti(index);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            if (Panels[index].Collapsible) Panels[index].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true);
            else if (Panels[index + 1].Collapsible) Panels[index + 1].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true);
            e.Handled = true;
            return;
        }
        double available = _layout.ActualWidth - 6 * (Panels.Count - 1);
        if (available <= 0) return;
        double a = _multiShares[index] * available, b = _multiShares[index + 1] * available, pair = a + b;
        var before = _layout.ColumnDefinitions[index * 2];
        var after = _layout.ColumnDefinitions[(index + 1) * 2];
        double low = Math.Max(before.MinWidth, pair - after.MaxWidth);
        double high = Math.Min(before.MaxWidth, pair - after.MinWidth);
        if (low > high) low = high = Math.Clamp(a, 0, pair);
        if (e.Key == Key.Left && a <= low + .5 && Panels[index].Collapsible)
        {
            Panels[index].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true); e.Handled = true; return;
        }
        if (e.Key == Key.Right && a >= high - .5 && Panels[index + 1].Collapsible)
        {
            Panels[index + 1].SetCurrentValue(ResizablePanel.IsCollapsedProperty, true); e.Handled = true; return;
        }
        double step = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? 64 : 16;
        double target = e.Key switch { Key.Left => a - step, Key.Right => a + step, Key.Home => low, _ => high };
        target = Math.Clamp(target, low, high);
        _multiShares[index] = target / available;
        _multiShares[index + 1] = (pair - target) / available;
        ApplyMultiLayout();
        RaiseLayoutChanged();
        e.Handled = true;
    }
}
