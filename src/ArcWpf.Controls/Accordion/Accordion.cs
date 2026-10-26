using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

public enum AccordionSize
{
    Medium,
    Large,
}

[TemplatePart(Name = "PART_ItemsHost", Type = typeof(FrameworkElement))]
public class Accordion : ItemsControl
{
    public static readonly DependencyProperty ExpandedIndexProperty = DependencyProperty.Register(
        nameof(ExpandedIndex), typeof(int), typeof(Accordion), new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedIndexChanged), IsValidExpandedIndex);

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(AccordionSize), typeof(Accordion), new FrameworkPropertyMetadata(AccordionSize.Medium,
            FrameworkPropertyMetadataOptions.Inherits, OnSizeChanged), IsValidSize);

    public static readonly RoutedEvent ExpandedIndexChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ExpandedIndexChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<int>), typeof(Accordion));

    private readonly Dictionary<AccordionItem, object> _rows = new();
    private bool _syncing;

    public int ExpandedIndex { get => (int)GetValue(ExpandedIndexProperty); set => SetValue(ExpandedIndexProperty, value); }
    public AccordionSize Size { get => (AccordionSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public event RoutedPropertyChangedEventHandler<int> ExpandedIndexChanged { add => AddHandler(ExpandedIndexChangedEvent, value); remove => RemoveHandler(ExpandedIndexChangedEvent, value); }

    static Accordion() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Accordion), new FrameworkPropertyMetadata(typeof(Accordion)));

    public Accordion()
    {
        Loaded += (_, _) => SyncRows(false);
        ItemContainerGenerator.StatusChanged += (_, _) => SyncRows(false);
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is AccordionItem;
    protected override DependencyObject GetContainerForItemOverride() => new AccordionItem();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is not AccordionItem row) return;
        row.Owner = this;
        if (item is not AccordionItem)
        {
            row.SetCurrentValue(AccordionItem.HeaderProperty, item);
            row.SetCurrentValue(AccordionItem.ContentProperty, item);
        }
        row.SetCurrentValue(AccordionItem.SizeProperty, Size);
        _rows[row] = item;
        SyncRows(false);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is AccordionItem row)
        {
            _rows.Remove(row);
            row.Owner = null;
        }
        base.ClearContainerForItemOverride(element, item);
        SyncRows(false);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        SyncRows(false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.Up or Key.Down or Key.Home or Key.End)) return;
        var focused = OrderedRows().FirstOrDefault(row => row.IsKeyboardFocusWithin);
        if (focused is null) return;
        var next = FindNext(focused, e.Key);
        if (next is null) return;
        next.Focus();
        e.Handled = true;
    }

    internal void ItemExpandedChanged(AccordionItem item, bool expanded)
    {
        if (_syncing) return;
        int index = IndexOf(item);
        if (index < 0) return;
        SetCurrentValue(ExpandedIndexProperty, expanded ? index : -1);
    }

    internal IReadOnlyList<AccordionItem> OrderedRows() => _rows.Keys.OrderBy(row => IndexOf(row)).ToArray();

    internal AccordionItem? FindNext(AccordionItem current, Key key)
    {
        var rows = OrderedRows().Where(row => row.IsEnabled && row.Visibility == Visibility.Visible).ToArray();
        if (rows.Length == 0) return null;
        if (key == Key.Home) return rows[0];
        if (key == Key.End) return rows[^1];
        int index = Array.IndexOf(rows, current);
        if (index < 0) return rows[0];
        int delta = key == Key.Down ? 1 : -1;
        return rows[(index + delta + rows.Length) % rows.Length];
    }

    internal void SyncRows(bool animate)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            var rows = OrderedRows();
            foreach (var row in rows)
            {
                row.SetCurrentValue(AccordionItem.SizeProperty, Size);
                row.SetCurrentValue(AccordionItem.IsExpandedProperty, IndexOf(row) == ExpandedIndex);
            }
        }
        finally { _syncing = false; }
        foreach (var row in OrderedRows()) row.UpdatePresentation(animate);
    }

    private int IndexOf(AccordionItem row)
    {
        var item = ItemContainerGenerator.ItemFromContainer(row);
        return item == DependencyProperty.UnsetValue ? -1 : Items.IndexOf(item);
    }

    private static bool IsValidExpandedIndex(object value) => value is int index && index >= -1;
    private static bool IsValidSize(object value) => value is AccordionSize size && Enum.IsDefined(size);
    private static void OnExpandedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var accordion = (Accordion)d;
        accordion.SyncRows(true);
        accordion.RaiseEvent(new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue, ExpandedIndexChangedEvent));
    }
    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Accordion)d).SyncRows(false);

    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionPeer(this);

    private sealed class AccordionPeer(Accordion owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(Accordion);
        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : nameof(Accordion);
    }
}
