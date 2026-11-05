using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

// A ListBox of fixed-height rows that draw themselves, for long text views such as DiffView and TerminalOutput.
// The ListBox keeps virtualization, selection, keyboard movement and automation. Text scrolls sideways under a pinned gutter, so the list never scrolls horizontally itself.
internal abstract class DrawnRowList : ListBox
{
    public const double RowHeight = 20, TextSize = 13;
    private readonly HashSet<DrawnRowItem> _live = [];
    private double _scrollX;
    private ScrollViewer? _scroller;

    protected DrawnRowList()
    {
        OverridesDefaultStyle = true;
        Focusable = true;
        FocusVisualStyle = null;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        SelectionMode = SelectionMode.Extended;
        VirtualizingPanel.SetIsVirtualizing(this, true);
        VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Item);
        ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        var scroller = new FrameworkElementFactory(typeof(OverlayScrollViewer));
        scroller.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroller.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        scroller.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroller.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        Template = new ControlTemplate(typeof(ListBox)) { VisualTree = scroller };
        SetResourceReference(FontFamilyProperty, "ArcWpf.Font.Mono");
    }

    public int LiveCountForTest => _live.Count;
    public double Cell { get; private set; } = 7;
    public Typeface Face => new(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;
    public ScrollViewer? Scroller => _scroller ??= FindScroller(this);
    public event Action<double>? HorizontalRequested;
    public event Action? CopyRequested;

    public double ScrollX
    {
        get => _scrollX;
        set { if (_scrollX != value) { _scrollX = value; InvalidateRows(); } }
    }

    public void MeasureCell()
    {
        Cell = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, TextSize, Brushes.Black, PixelsPerDip).WidthIncludingTrailingWhitespace;
    }

    public void InvalidateRows() { foreach (var item in _live) { item.InvalidateVisual(); } }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        var container = (DrawnRowItem)element;
        _live.Add(container);
        container.InvalidateVisual();
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        base.ClearContainerForItemOverride(element, item);
        _live.Remove((DrawnRowItem)element);
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled) { return; }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            HorizontalRequested?.Invoke(-e.Delta / 120.0 * Cell * 8);
            e.Handled = true;
            return;
        }
        // At either end the wheel belongs to whatever scrolls around the list, such as the chat it sits in.
        if (Scroller is { } scroller && (e.Delta > 0 ? scroller.VerticalOffset <= 0 : scroller.VerticalOffset >= scroller.ScrollableHeight) && VisualTreeHelper.GetParent(this) is UIElement parent)
        {
            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None)
        {
            HorizontalRequested?.Invoke((e.Key == Key.Left ? -1 : 1) * Cell * 8);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyRequested?.Invoke();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer found) { return found; }
            if (FindScroller(child) is { } nested) { return nested; }
        }
        return null;
    }
}

internal abstract class DrawnRowItem : ListBoxItem
{
    protected DrawnRowItem()
    {
        OverridesDefaultStyle = true;
        Template = null;
        FocusVisualStyle = null;
        Height = DrawnRowList.RowHeight;
        Padding = new Thickness(0);
        SnapsToDevicePixels = true;
    }

    protected override Size MeasureOverride(Size constraint) => new(0, DrawnRowList.RowHeight);

    protected override Size ArrangeOverride(Size arrangeBounds) => arrangeBounds;

    protected override void OnSelected(RoutedEventArgs e) { base.OnSelected(e); InvalidateVisual(); }

    protected override void OnUnselected(RoutedEventArgs e) { base.OnUnselected(e); InvalidateVisual(); }
}
