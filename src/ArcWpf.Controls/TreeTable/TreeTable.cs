using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum TreeTableSortDirection { Ascending, Descending }

public sealed class TreeTableRow
{
    public string Id { get; set; } = string.Empty;
    public object? Data { get; set; }
    public ObservableCollection<TreeTableRow> Children { get; } = new();
    /// <summary>Marks a row whose children are not loaded yet; <see cref="TreeTable.LoadChildren"/> fetches them on first expand.</summary>
    public bool HasChildren { get; set; }
}

/// <summary>What a column's CellTemplate binds to.</summary>
public sealed class TreeTableCellContext
{
    public required TreeTableRow Row { get; init; }
    public object? Data => Row.Data;
    /// <summary>One for top level rows.</summary>
    public required int Depth { get; init; }
    public required bool Expanded { get; init; }
}

public sealed class TreeTableColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Sortable { get; set; } = true;
    /// <summary>Aligns the column to its end with the arrow leading, for figures.</summary>
    public bool Numeric { get; set; }
    public GridLength Width { get; set; } = new(1, GridUnitType.Star);
    public DataTemplate? CellTemplate { get; set; }
}

[TemplatePart(Name = "PART_Header", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Body", Type = typeof(StackPanel))]
[TemplatePart(Name = "PART_Empty", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Scroller", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_Clip", Type = typeof(FrameworkElement))]
public class TreeTable : Control
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<TreeTableRow>), typeof(TreeTable), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(TreeTable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty SelectedIdsProperty = DependencyProperty.Register(nameof(SelectedIds), typeof(ObservableCollection<string>), typeof(TreeTable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty SelectableProperty = DependencyProperty.Register(nameof(Selectable), typeof(bool), typeof(TreeTable), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty SortColumnProperty = DependencyProperty.Register(nameof(SortColumn), typeof(string), typeof(TreeTable), new FrameworkPropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(nameof(SortDirection), typeof(TreeTableSortDirection), typeof(TreeTable), new FrameworkPropertyMetadata(TreeTableSortDirection.Ascending, Changed));
    public static new readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(nameof(MaxHeight), typeof(double), typeof(TreeTable), new FrameworkPropertyMetadata(double.PositiveInfinity, Changed));
    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(TreeTable), new FrameworkPropertyMetadata("Nothing to show", Changed));
    public static readonly DependencyProperty LoadChildrenProperty = DependencyProperty.Register(nameof(LoadChildren), typeof(Func<TreeTableRow, Task<IEnumerable<TreeTableRow>>>), typeof(TreeTable));
    public static readonly RoutedEvent RowActivatedEvent = EventManager.RegisterRoutedEvent(nameof(RowActivated), RoutingStrategy.Bubble, typeof(EventHandler<TreeTableRowEventArgs>), typeof(TreeTable));

    private const double RowHeight = 44, Indent = 20, ChevronSize = 24, CheckColumn = 44, HeaderHeight = 40, CornerInner = 25;
    private const string ChevronRight = "M9,18 L15,12 L9,6", ChevronsUpDown = "M7,15 L12,20 L17,15 M7,9 L12,4 L17,9", ChevronsDownUp = "M7,20 L12,15 L17,20 M7,4 L12,9 L17,4", ArrowUp = "M5,12 L12,5 L19,12 M12,19 V5", Spinner = "M21,12 A9,9 0 1 1 12,3";

    private Grid? _header;
    private StackPanel? _body;
    private TextBlock? _empty;
    private ScrollViewer? _scroller;
    private FrameworkElement? _clip;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal), _selected = new(StringComparer.Ordinal), _mixed = new(StringComparer.Ordinal), _lastShown = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeTableRow> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _parents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<TreeTableRow>> _loaded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _status = new(StringComparer.Ordinal);
    private readonly List<(TreeTableRow Row, int Depth, string? Parent, bool Expanded, bool Expandable)> _lines = new();
    private readonly List<(string Ancestor, Border Guide)> _guides = new();
    private readonly Dictionary<string, int> _siblingsSeen = new(StringComparer.Ordinal);
    private readonly OwnedMotion _motion = new();
    private ObservableCollection<TreeTableRow>? _watchedRows;
    private string? _focusId, _hoverId, _turning;
    private bool _syncing;

    public ObservableCollection<TreeTableRow> Rows { get => (ObservableCollection<TreeTableRow>)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public ObservableCollection<TreeTableColumn> Columns { get; } = new();
    public ObservableCollection<string> ExpandedIds { get => (ObservableCollection<string>)GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }
    public ObservableCollection<string> SelectedIds { get => (ObservableCollection<string>)GetValue(SelectedIdsProperty); set => SetValue(SelectedIdsProperty, value); }
    public bool Selectable { get => (bool)GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }
    public string SortColumn { get => (string)GetValue(SortColumnProperty); set => SetValue(SortColumnProperty, value); }
    public TreeTableSortDirection SortDirection { get => (TreeTableSortDirection)GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }
    public new double MaxHeight { get => (double)GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }
    /// <summary>Fetches the children of a row marked HasChildren the first time it opens. A faulted task shows a retry row.</summary>
    public Func<TreeTableRow, Task<IEnumerable<TreeTableRow>>>? LoadChildren { get => (Func<TreeTableRow, Task<IEnumerable<TreeTableRow>>>?)GetValue(LoadChildrenProperty); set => SetValue(LoadChildrenProperty, value); }
    /// <summary>Raised when a row without children is clicked or gets Enter. Parent rows toggle instead.</summary>
    public event EventHandler<TreeTableRowEventArgs> RowActivated { add => AddHandler(RowActivatedEvent, value); remove => RemoveHandler(RowActivatedEvent, value); }

    static TreeTable() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeTable), new FrameworkPropertyMetadata(typeof(TreeTable)));
    public TreeTable()
    {
        SetCurrentValue(RowsProperty, new ObservableCollection<TreeTableRow>());
        SetCurrentValue(ExpandedIdsProperty, new ObservableCollection<string>());
        SetCurrentValue(SelectedIdsProperty, new ObservableCollection<string>());
        Columns.CollectionChanged += (_, _) => Rebuild();
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => _motion.Stop();
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var table = (TreeTable)d;
        if (e.Property == RowsProperty) table.WatchRows();
        if (e.Property == ExpandedIdsProperty || e.Property == SelectedIdsProperty)
        {
            if (e.OldValue is INotifyCollectionChanged previous) previous.CollectionChanged -= table.StateCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged next) next.CollectionChanged += table.StateCollectionChanged;
        }
        if (!table._syncing) table.Rebuild();
    }

    private void WatchRows()
    {
        if (_watchedRows is not null) _watchedRows.CollectionChanged -= RowsCollectionChanged;
        _watchedRows = Rows;
        if (_watchedRows is not null) _watchedRows.CollectionChanged += RowsCollectionChanged;
    }

    private void RowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();
    private void StateCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (!_syncing) Rebuild(); }

    public override void OnApplyTemplate()
    {
        if (_clip is not null) _clip.SizeChanged -= ClipSizeChanged;
        if (_body is not null) _body.PreviewKeyDown -= BodyKeyDown;
        base.OnApplyTemplate();
        _header = GetTemplateChild("PART_Header") as Grid;
        _body = GetTemplateChild("PART_Body") as StackPanel;
        _empty = GetTemplateChild("PART_Empty") as TextBlock;
        _scroller = GetTemplateChild("PART_Scroller") as ScrollViewer;
        _clip = GetTemplateChild("PART_Clip") as FrameworkElement;
        if (_clip is not null) _clip.SizeChanged += ClipSizeChanged;
        if (_body is not null) _body.PreviewKeyDown += BodyKeyDown;
        Rebuild();
    }

    private void ClipSizeChanged(object sender, SizeChangedEventArgs e) => _clip!.Clip = new RectangleGeometry(new Rect(e.NewSize), CornerInner, CornerInner);

    public void Toggle(string id)
    {
        if (_byId.TryGetValue(id, out var row)) { SetOpen(row, !_expanded.Contains(id)); return; }
        if (!_expanded.Remove(id)) _expanded.Add(id);
        SyncExpanded();
        Rebuild();
    }

    private IReadOnlyList<TreeTableRow>? KidsOf(TreeTableRow row) => row.Children.Count > 0 ? row.Children : _loaded.GetValueOrDefault(row.Id);
    private bool CanExpand(TreeTableRow row) => KidsOf(row) is { Count: > 0 } || (row.HasChildren && LoadChildren is not null && KidsOf(row) is null);

    private void Rebuild()
    {
        if (_header is null || _body is null || Template is null) return;
        bool keepFocus = _body.IsKeyboardFocusWithin;
        _expanded.Clear(); _expanded.UnionWith(ExpandedIds);
        _selected.Clear(); _selected.UnionWith(SelectedIds);
        IndexRows();
        NormalizeMixed();
        _lines.Clear();
        Walk(Rows, 1, null);
        BuildHeader();
        _body.Children.Clear();
        _guides.Clear();
        _siblingsSeen.Clear();
        bool animate = _lastShown.Count > 0 && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        foreach (var line in _lines) AddLine(line, animate);
        _lastShown.Clear();
        foreach (var line in _lines) _lastShown.Add(line.Row.Id);
        _turning = null;
        if (_empty is not null) { _empty.Text = EmptyMessage; _empty.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed; }
        if (_scroller is not null) _scroller.MaxHeight = double.IsInfinity(MaxHeight) ? double.PositiveInfinity : Math.Max(0, MaxHeight - HeaderHeight);
        PaintGuides();
        if (keepFocus && _focusId is not null) FocusRow(_focusId);
        // Rows opened from outside, such as a controlled expand all, still fetch their children.
        foreach (var line in _lines.Where(line => line.Expanded && KidsOf(line.Row) is null && !_status.ContainsKey(line.Row.Id)).ToList()) _ = Fetch(line.Row);
    }

    private void IndexRows()
    {
        _byId.Clear(); _parents.Clear();
        void Visit(IEnumerable<TreeTableRow> list, string? parent)
        {
            foreach (var row in list)
            {
                _byId[row.Id] = row;
                _parents[row.Id] = parent;
                if (KidsOf(row) is { } kids) Visit(kids, row.Id);
            }
        }
        Visit(Rows, null);
    }

    // Tri-state: a parent is checked when all its known children are, and mixed when only some are.
    private void NormalizeMixed()
    {
        _mixed.Clear();
        bool Visit(TreeTableRow row)
        {
            bool any = _selected.Contains(row.Id);
            if (KidsOf(row) is { } kids) foreach (var kid in kids) if (Visit(kid)) any = true;
            if (any && !_selected.Contains(row.Id)) _mixed.Add(row.Id);
            return any;
        }
        foreach (var row in Rows) Visit(row);
    }

    private void Walk(IEnumerable<TreeTableRow> list, int depth, string? parent)
    {
        foreach (var row in Ordered(list))
        {
            bool expandable = CanExpand(row), open = expandable && _expanded.Contains(row.Id);
            _lines.Add((row, depth, parent, open, expandable));
            if (open && KidsOf(row) is { } kids) Walk(kids, depth + 1, row.Id);
        }
    }

    private void BuildHeader()
    {
        var header = _header!;
        var resources = Template.Resources;
        header.Children.Clear();
        header.ColumnDefinitions.Clear();
        header.Height = Selectable ? RowHeight : HeaderHeight - 1;
        if (Selectable)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CheckColumn) });
            var all = _byId.Keys.ToList();
            bool allOn = Rows.Count > 0 && Rows.All(row => _selected.Contains(row.Id)), someOn = !allOn && all.Any(_selected.Contains);
            var check = NewCheck(allOn ? true : someOn ? null : false, allOn ? "Clear selection" : "Select all rows");
            check.Click += (_, _) => ToggleAll();
            header.Children.Add(check);
        }
        foreach (var column in Columns) header.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width, MinWidth = 0 });
        int offset = Selectable ? 1 : 0;
        for (int index = 0; index < Columns.Count; index++)
        {
            var column = Columns[index];
            bool tree = index == 0;
            var cell = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(tree ? 8 : 12, 0, 12, 0), HorizontalAlignment = column.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            if (tree) cell.Children.Add(ExpandAllTool(resources));
            cell.Children.Add(column.Sortable ? SortButton(column, resources) : new TextBlock { Text = column.Header, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(cell, index + offset);
            header.Children.Add(cell);
        }
    }

    private FrameworkElement ExpandAllTool(ResourceDictionary resources)
    {
        var expandable = _byId.Values.Where(row => KidsOf(row) is { Count: > 0 }).Select(row => row.Id).ToList();
        if (expandable.Count == 0) return new Border { Width = ChevronSize, Margin = new Thickness(0, 0, 4, 0) };
        bool everyOpen = expandable.All(_expanded.Contains);
        var tool = new System.Windows.Controls.Button { Style = (Style)resources["HeaderTool"], Margin = new Thickness(0, 0, 2, 0), Content = Lucide.Glyph(everyOpen ? ChevronsDownUp : ChevronsUpDown, 16), ToolTip = everyOpen ? "Collapse all" : "Expand all" };
        AutomationProperties.SetName(tool, (string)tool.ToolTip);
        tool.Click += (_, _) =>
        {
            if (everyOpen) _expanded.Clear(); else _expanded.UnionWith(expandable);
            SyncExpanded();
            Rebuild();
        };
        return tool;
    }

    private System.Windows.Controls.Button SortButton(TreeTableColumn column, ResourceDictionary resources)
    {
        bool sorted = SortColumn == column.Key;
        var label = new TextBlock { Text = column.Header, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var icon = Lucide.Glyph(ArrowUp, 14);
        icon.Opacity = sorted ? 1 : 0;
        icon.RenderTransformOrigin = new Point(.5, .5);
        icon.RenderTransform = new RotateTransform(sorted && SortDirection == TreeTableSortDirection.Descending ? 180 : 0);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        // End-aligned columns lead with the arrow, so the label stays over its figures.
        if (column.Numeric) { icon.Margin = new Thickness(0, 0, 4, 0); content.Children.Add(icon); content.Children.Add(label); }
        else { icon.Margin = new Thickness(4, 0, 0, 0); content.Children.Add(label); content.Children.Add(icon); }
        var button = new System.Windows.Controls.Button { Style = (Style)resources["Sort"], Content = content, Tag = sorted ? "sorted" : null };
        AutomationProperties.SetName(button, column.Header);
        button.Click += (_, _) => CycleSort(column);
        return button;
    }

    private void CycleSort(TreeTableColumn column)
    {
        if (SortColumn != column.Key) { SortDirection = TreeTableSortDirection.Ascending; SortColumn = column.Key; }
        else if (SortDirection == TreeTableSortDirection.Ascending) SortDirection = TreeTableSortDirection.Descending;
        else { SortColumn = string.Empty; SortDirection = TreeTableSortDirection.Ascending; }
    }

    private CheckBox NewCheck(bool? state, string name)
    {
        // Arc's checkbox keeps a 44 hit target inside the 38 wide cell, so it sits against the cell's left padding.
        var check = new CheckBox { IsThreeState = false, IsChecked = state, Focusable = false, Margin = new Thickness(6, 0, -6, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(check, name);
        return check;
    }

    private void AddLine((TreeTableRow Row, int Depth, string? Parent, bool Expanded, bool Expandable) line, bool animate)
    {
        var (row, depth, parent, open, expandable) = line;
        var resources = Template.Resources;
        var grid = new Grid { Height = RowHeight, VerticalAlignment = VerticalAlignment.Top };
        if (Selectable) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CheckColumn) });
        foreach (var column in Columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width, MinWidth = 0 });
        int offset = Selectable ? 1 : 0;
        if (Selectable)
        {
            var check = NewCheck(_selected.Contains(row.Id) ? true : _mixed.Contains(row.Id) ? null : false, $"Select {row.Id}");
            check.Click += (_, _) => ToggleSelect(row.Id);
            grid.Children.Add(check);
        }
        var context = new TreeTableCellContext { Row = row, Depth = depth, Expanded = open };
        for (int index = 0; index < Columns.Count; index++)
        {
            var cell = index == 0 ? TreeCell(row, depth, parent, open, expandable, context, resources, animate) : PlainCell(Columns[index], context);
            Grid.SetColumn(cell, index + offset);
            grid.Children.Add(cell);
        }
        // box-shadow: inset 0 -1px 0 — the rule sits inside the row and takes no layout.
        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
        Grid.SetColumnSpan(rule, Math.Max(1, Columns.Count + offset));
        grid.Children.Add(rule);
        var view = new Border { Style = (Style)resources["Row"], Child = grid, Height = RowHeight, Focusable = true, Tag = _selected.Contains(row.Id) ? "selected" : null, Cursor = expandable ? Cursors.Hand : null, ClipToBounds = true, DataContext = row };
        KeyboardNavigation.SetIsTabStop(view, row.Id == TabId);
        AutomationProperties.SetName(view, row.Id);
        view.MouseEnter += (_, _) => { _hoverId = row.Id; PaintGuides(); };
        view.MouseLeave += (_, _) => { if (_hoverId == row.Id) { _hoverId = null; PaintGuides(); } };
        view.GotKeyboardFocus += (_, _) => { _focusId = row.Id; PaintGuides(); };
        view.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && IsNestedAction(source, view)) return;
            _focusId = row.Id;
            if (expandable) SetOpen(row, !open); else RaiseEvent(new TreeTableRowEventArgs(RowActivatedEvent, this, row));
            e.Handled = true;
        };
        _body!.Children.Add(view);
        int position = _siblingsSeen.GetValueOrDefault(parent ?? string.Empty);
        _siblingsSeen[parent ?? string.Empty] = position + 1;
        if (animate && !_lastShown.Contains(row.Id)) Unfold(view, position);
        if (open && KidsOf(row) is null) AddPending(row, depth + 1, offset, resources);
    }

    private static bool IsNestedAction(DependencyObject source, DependencyObject stop)
    {
        for (var node = source; node is not null && node != stop; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is ButtonBase) return true;
        return false;
    }

    // Rows grow from zero height, siblings a beat apart, so a branch opens top down.
    private void Unfold(FrameworkElement view, int position)
    {
        double delay = Math.Min(position, 8) * .018;
        view.Height = 0;
        view.Opacity = 0;
        _motion.Double(view, HeightProperty, RowHeight, Tween.Smooth, true, spring: true);
        _motion.Double(view, OpacityProperty, 1, Tween.Standard, true, delay: delay);
    }

    private FrameworkElement TreeCell(TreeTableRow row, int depth, string? parent, bool open, bool expandable, TreeTableCellContext context, ResourceDictionary resources, bool animate)
    {
        var host = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        var ancestors = new List<string>();
        for (string? id = parent; id is not null; id = _parents.GetValueOrDefault(id)) ancestors.Insert(0, id);
        for (int level = 0; level < ancestors.Count; level++)
        {
            var guide = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(level * Indent + ChevronSize / 2, 0, 0, 0), IsHitTestVisible = false };
            guide.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
            _guides.Add((ancestors[level], guide));
            host.Children.Add(guide);
        }
        var line = new DockPanel { Margin = new Thickness((depth - 1) * Indent, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        FrameworkElement lead;
        if (expandable)
        {
            FrameworkElement glyph;
            if (_pending.Contains(row.Id)) glyph = Spin(Lucide.Glyph(Spinner, 16));
            else
            {
                bool turning = animate && _turning == row.Id;
                var turn = new RotateTransform(open != turning ? 90 : 0);
                glyph = Lucide.Glyph(ChevronRight, 16);
                glyph.RenderTransformOrigin = new Point(.5, .5);
                glyph.RenderTransform = turn;
                if (turning) _motion.Double(turn, RotateTransform.AngleProperty, open ? 90 : 0, Tween.Snappy, true, spring: true);
            }
            var chevron = new System.Windows.Controls.Button { Style = (Style)resources["Chevron"], Content = glyph, Tag = open ? "open" : null };
            AutomationProperties.SetName(chevron, open ? "Collapse" : "Expand");
            chevron.Click += (_, e) => { _focusId = row.Id; SetOpen(row, !open); e.Handled = true; };
            lead = chevron;
        }
        else lead = new Border { Width = ChevronSize, Height = ChevronSize };
        lead.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(lead, Dock.Left);
        line.Children.Add(lead);
        line.Children.Add(CellContent(Columns[0], context, "ArcWpf.Brush.Foreground"));
        host.Children.Add(line);
        return host;
    }

    private FrameworkElement PlainCell(TreeTableColumn column, TreeTableCellContext context)
    {
        var content = CellContent(column, context, "ArcWpf.Brush.TextSecondary");
        content.Margin = new Thickness(12, 0, 12, 0);
        content.VerticalAlignment = VerticalAlignment.Center;
        content.HorizontalAlignment = column.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        return content;
    }

    private static FrameworkElement CellContent(TreeTableColumn column, TreeTableCellContext context, string brush)
    {
        if (column.CellTemplate is not null) return new ContentPresenter { Content = context, ContentTemplate = column.CellTemplate };
        var text = new TextBlock { Text = Convert.ToString(GetValue(context.Row.Data, column.Path.Length == 0 ? column.Key : column.Path), CultureInfo.CurrentCulture) ?? string.Empty, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        text.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return text;
    }

    // A loading or retry line under a parent that is still fetching.
    private void AddPending(TreeTableRow parent, int depth, int offset, ResourceDictionary resources)
    {
        bool failed = _status.TryGetValue(parent.Id, out bool loading) && !loading;
        var grid = new Grid { Height = RowHeight };
        if (Selectable) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CheckColumn) });
        foreach (var column in Columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10 + (depth - 1) * Indent + ChevronSize + 4, 0, 12, 0) };
        var label = new TextBlock { Text = failed ? "Couldn’t load" : "Loading", VerticalAlignment = VerticalAlignment.Center, Margin = failed ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0) };
        content.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, failed ? "ArcWpf.Brush.Danger" : "ArcWpf.Brush.TextMuted");
        if (failed)
        {
            content.Children.Add(label);
            var retry = new System.Windows.Controls.Button { Style = (Style)resources["Retry"], Content = "Retry" };
            retry.Click += (_, e) => { _ = Fetch(parent); e.Handled = true; };
            content.Children.Add(retry);
        }
        else { content.Children.Add(Spin(Lucide.Glyph(Spinner, 16))); content.Children.Add(label); }
        Grid.SetColumn(content, offset);
        grid.Children.Add(content);
        var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
        Grid.SetColumnSpan(rule, Math.Max(1, Columns.Count + offset));
        grid.Children.Add(rule);
        _body!.Children.Add(new Border { Child = grid, Height = RowHeight, Tag = "pending" });
    }

    private static FrameworkElement Spin(FrameworkElement glyph)
    {
        var turn = new RotateTransform();
        glyph.RenderTransformOrigin = new Point(.5, .5);
        glyph.RenderTransform = turn;
        turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.8)) { RepeatBehavior = RepeatBehavior.Forever });
        return glyph;
    }

    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    private async Task Fetch(TreeTableRow row)
    {
        if (LoadChildren is not { } load || !_pending.Add(row.Id)) return;
        _status[row.Id] = true;
        Dispatcher.BeginInvoke(Rebuild);
        try
        {
            var kids = (await load(row)).ToList();
            _loaded[row.Id] = kids;
            _status.Remove(row.Id);
            // Children that arrive under a checked parent start checked.
            if (_selected.Contains(row.Id)) { void Add(IEnumerable<TreeTableRow> list) { foreach (var kid in list) { _selected.Add(kid.Id); Add(kid.Children); } } Add(kids); SyncSelected(); }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { _status[row.Id] = false; }
        finally { _pending.Remove(row.Id); }
        Rebuild();
    }

    private void SetOpen(TreeTableRow row, bool open)
    {
        if (open == _expanded.Contains(row.Id)) return;
        if (open) _expanded.Add(row.Id); else _expanded.Remove(row.Id);
        _turning = row.Id;
        SyncExpanded();
        Rebuild();
    }

    private void ToggleSelect(string id)
    {
        bool on = !_selected.Contains(id);
        void Apply(string member)
        {
            if (on) _selected.Add(member); else _selected.Remove(member);
            if (_byId.TryGetValue(member, out var row) && KidsOf(row) is { } kids) foreach (var kid in kids) Apply(kid.Id);
        }
        Apply(id);
        Settle();
        SyncSelected();
        Rebuild();
    }

    private void ToggleAll()
    {
        bool allOn = Rows.Count > 0 && Rows.All(row => _selected.Contains(row.Id));
        _selected.Clear();
        if (!allOn) _selected.UnionWith(_byId.Keys);
        Settle();
        SyncSelected();
        Rebuild();
    }

    private void Settle()
    {
        bool Visit(TreeTableRow row)
        {
            if (KidsOf(row) is not { Count: > 0 } kids) return _selected.Contains(row.Id);
            bool all = true;
            foreach (var kid in kids) if (!Visit(kid)) all = false;
            if (all) _selected.Add(row.Id); else _selected.Remove(row.Id);
            return all;
        }
        foreach (var row in Rows) Visit(row);
    }

    private string? TabId => _focusId is not null && _lines.Any(line => line.Row.Id == _focusId) ? _focusId : _lines.Count > 0 ? _lines[0].Row.Id : null;

    private void FocusRow(string? id)
    {
        if (id is null) return;
        _focusId = id;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            var view = _body?.Children.OfType<Border>().FirstOrDefault(child => child.DataContext is TreeTableRow row && row.Id == id);
            if (view is null) return;
            view.Focus();
            view.BringIntoView();
        }));
    }

    private void BodyKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not Border { DataContext: TreeTableRow row }) return;
        int at = _lines.FindIndex(line => line.Row.Id == row.Id);
        if (at < 0) return;
        var line = _lines[at];
        void Go(int to) => FocusRow(_lines[Math.Clamp(to, 0, _lines.Count - 1)].Row.Id);
        switch (e.Key)
        {
            case Key.Down: Go(at + 1); break;
            case Key.Up: Go(at - 1); break;
            case Key.Home: Go(0); break;
            case Key.End: Go(_lines.Count - 1); break;
            case Key.PageDown: Go(at + 10); break;
            case Key.PageUp: Go(at - 10); break;
            case Key.Right:
                if (line.Expandable && !line.Expanded) SetOpen(row, true);
                else if (line.Expanded && at + 1 < _lines.Count && _lines[at + 1].Parent == row.Id) Go(at + 1);
                break;
            case Key.Left:
                if (line.Expanded) SetOpen(row, false);
                else if (line.Parent is not null) FocusRow(line.Parent);
                break;
            case Key.Multiply:
                foreach (var sibling in _lines.Where(other => other.Parent == line.Parent && other.Expandable)) _expanded.Add(sibling.Row.Id);
                SyncExpanded();
                Rebuild();
                break;
            case Key.Space when Selectable: ToggleSelect(row.Id); break;
            case Key.Enter:
                if (_status.TryGetValue(row.Id, out bool loading) && !loading) _ = Fetch(row);
                else if (line.Expandable) SetOpen(row, !line.Expanded);
                else RaiseEvent(new TreeTableRowEventArgs(RowActivatedEvent, this, row));
                break;
            case Key.A when Selectable && Keyboard.Modifiers.HasFlag(ModifierKeys.Control): ToggleAll(); break;
            default: return;
        }
        e.Handled = true;
    }

    // The branch around the row under the pointer or focus lights its guide, so a deep row shows which parent it belongs to.
    private void PaintGuides()
    {
        string? active = _hoverId ?? _focusId, branch = null;
        if (active is not null)
        {
            int at = _lines.FindIndex(line => line.Row.Id == active);
            if (at >= 0) branch = _lines[at].Expanded ? active : _lines[at].Parent;
        }
        foreach (var (ancestor, guide) in _guides) guide.SetResourceReference(Border.BackgroundProperty, ancestor == branch ? "ArcWpf.Brush.BorderStrong" : "ArcWpf.Brush.Border");
    }

    private void SyncExpanded()
    {
        _syncing = true;
        ExpandedIds.Clear();
        foreach (string id in _expanded) ExpandedIds.Add(id);
        _syncing = false;
    }

    private void SyncSelected()
    {
        _syncing = true;
        SelectedIds.Clear();
        foreach (string id in _selected) SelectedIds.Add(id);
        _syncing = false;
    }

    // Siblings sort inside their parent, so a sorted tree still reads top down.
    private IEnumerable<TreeTableRow> Ordered(IEnumerable<TreeTableRow> rows)
    {
        if (string.IsNullOrEmpty(SortColumn) || Columns.FirstOrDefault(item => item.Key == SortColumn) is not { } column) return rows;
        string path = column.Path.Length == 0 ? column.Key : column.Path;
        int sign = SortDirection == TreeTableSortDirection.Ascending ? 1 : -1;
        return rows.OrderBy(row => row, Comparer<TreeTableRow>.Create((a, b) =>
        {
            var x = GetValue(a.Data, path); var y = GetValue(b.Data, path);
            int result = x is IComparable comparable && y is not null && x.GetType() == y.GetType() ? comparable.CompareTo(y) : string.Compare(x?.ToString(), y?.ToString(), StringComparison.CurrentCultureIgnoreCase);
            return sign * result;
        }));
    }

    private static object? GetValue(object? row, string path)
    {
        object? value = row;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries)) value = value?.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(value);
        return value;
    }
}

public sealed class TreeTableRowEventArgs(RoutedEvent routedEvent, object source, TreeTableRow row) : RoutedEventArgs(routedEvent, source)
{
    public TreeTableRow Row { get; } = row;
}
