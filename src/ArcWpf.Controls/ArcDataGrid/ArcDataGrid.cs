using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NativeButton = System.Windows.Controls.Button;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace ArcWpf.Controls;

/// <summary>
/// A spreadsheet grid: range selection, inline editing, a fill handle, clipboard, undo, multi-column sort, a filter row,
/// pinned and hidden columns, row checks with bulk actions, totals, grouping and virtualized rows. It is one focus stop,
/// and the body is drawn by a single surface, so ten thousand rows cost only the rows in view.
/// </summary>
[TemplatePart(Name = "PART_Viewport", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Toolbar", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ToolRow", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_BulkRow", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Search", Type = typeof(NativeTextBox))]
[TemplatePart(Name = "PART_StatusBar", Type = typeof(FrameworkElement))]
public partial class ArcDataGrid : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ArcDataGrid), new FrameworkPropertyMetadata("Data grid", (d, _) => AutomationProperties.SetName(d, ((ArcDataGrid)d).Label)));
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IList<ArcDataGridRow>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).RowsReplaced()));
    public static readonly DependencyProperty MaxGridHeightProperty = DependencyProperty.Register(nameof(MaxGridHeight), typeof(double), typeof(ArcDataGrid), new FrameworkPropertyMetadata(400d, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty SortProperty = DependencyProperty.Register(nameof(Sort), typeof(IReadOnlyList<ArcDataGridSort>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).ViewStateChangedExternally(true)));
    public static readonly DependencyProperty FiltersProperty = DependencyProperty.Register(nameof(Filters), typeof(IReadOnlyDictionary<string, object>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).FiltersChangedExternally()));
    public static readonly DependencyProperty SelectedRowIdsProperty = DependencyProperty.Register(nameof(SelectedRowIds), typeof(IReadOnlyList<string>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).CheckedChangedExternally()));
    public static readonly DependencyProperty HiddenColumnsProperty = DependencyProperty.Register(nameof(HiddenColumns), typeof(IReadOnlyList<string>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).ViewStateChangedExternally(false)));
    public static readonly DependencyProperty PinnedColumnsProperty = DependencyProperty.Register(nameof(PinnedColumns), typeof(IReadOnlyList<string>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).ViewStateChangedExternally(false)));
    public static readonly DependencyProperty GroupByProperty = DependencyProperty.Register(nameof(GroupBy), typeof(IReadOnlyList<string>), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).ViewStateChangedExternally(false)));
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(ArcDataGridDensity), typeof(ArcDataGrid), new FrameworkPropertyMetadata(ArcDataGridDensity.Standard, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty RowSelectionProperty = DependencyProperty.Register(nameof(RowSelection), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty RowNumbersProperty = DependencyProperty.Register(nameof(RowNumbers), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty SelectionStatsProperty = DependencyProperty.Register(nameof(SelectionStats), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).UpdateStatus()));
    public static readonly DependencyProperty ToolbarVisibleProperty = DependencyProperty.Register(nameof(ToolbarVisible), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).UpdateChrome()));
    public static readonly DependencyProperty TotalsProperty = DependencyProperty.Register(nameof(Totals), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty CanDeleteRowsProperty = DependencyProperty.Register(nameof(CanDeleteRows), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(true, (d, _) => ((ArcDataGrid)d).UpdateChrome()));
    public static readonly DependencyProperty ExportFileNameProperty = DependencyProperty.Register(nameof(ExportFileName), typeof(string), typeof(ArcDataGrid), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(false, (d, _) => ((ArcDataGrid)d).Relayout()));
    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(ArcDataGrid), new FrameworkPropertyMetadata("No rows yet", (d, _) => ((ArcDataGrid)d).Relayout()));
    /// <summary>Inherited by the toolbar buttons: labels show from 640 DIP of grid width and collapse to icons below it.</summary>
    public static readonly DependencyProperty ShowToolLabelsProperty = DependencyProperty.RegisterAttached("ShowToolLabels", typeof(bool), typeof(ArcDataGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetShowToolLabels(DependencyObject element) => (bool)element.GetValue(ShowToolLabelsProperty);
    public static void SetShowToolLabels(DependencyObject element, bool value) => element.SetValue(ShowToolLabelsProperty, value);

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IList<ArcDataGridRow>? Rows { get => (IList<ArcDataGridRow>?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    /// <summary>The tallest the scrolling area grows before it scrolls.</summary>
    public double MaxGridHeight { get => (double)GetValue(MaxGridHeightProperty); set => SetValue(MaxGridHeightProperty, value); }
    public IReadOnlyList<ArcDataGridSort>? Sort { get => (IReadOnlyList<ArcDataGridSort>?)GetValue(SortProperty); set => SetValue(SortProperty, value); }
    /// <summary>Text and number columns take a query string; select columns take the list of values to keep.</summary>
    public IReadOnlyDictionary<string, object>? Filters { get => (IReadOnlyDictionary<string, object>?)GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }
    public IReadOnlyList<string>? SelectedRowIds { get => (IReadOnlyList<string>?)GetValue(SelectedRowIdsProperty); set => SetValue(SelectedRowIdsProperty, value); }
    public IReadOnlyList<string>? HiddenColumns { get => (IReadOnlyList<string>?)GetValue(HiddenColumnsProperty); set => SetValue(HiddenColumnsProperty, value); }
    public IReadOnlyList<string>? PinnedColumns { get => (IReadOnlyList<string>?)GetValue(PinnedColumnsProperty); set => SetValue(PinnedColumnsProperty, value); }
    public IReadOnlyList<string>? GroupBy { get => (IReadOnlyList<string>?)GetValue(GroupByProperty); set => SetValue(GroupByProperty, value); }
    public ArcDataGridDensity Density { get => (ArcDataGridDensity)GetValue(DensityProperty); set => SetValue(DensityProperty, value); }
    public bool RowSelection { get => (bool)GetValue(RowSelectionProperty); set => SetValue(RowSelectionProperty, value); }
    public bool RowNumbers { get => (bool)GetValue(RowNumbersProperty); set => SetValue(RowNumbersProperty, value); }
    public bool SelectionStats { get => (bool)GetValue(SelectionStatsProperty); set => SetValue(SelectionStatsProperty, value); }
    public bool ToolbarVisible { get => (bool)GetValue(ToolbarVisibleProperty); set => SetValue(ToolbarVisibleProperty, value); }
    public bool Totals { get => (bool)GetValue(TotalsProperty); set => SetValue(TotalsProperty, value); }
    public bool CanDeleteRows { get => (bool)GetValue(CanDeleteRowsProperty); set => SetValue(CanDeleteRowsProperty, value); }
    public string? ExportFileName { get => (string?)GetValue(ExportFileNameProperty); set => SetValue(ExportFileNameProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }
    public ObservableCollection<ArcDataGridColumn> Columns { get; } = [];
    public ObservableCollection<ArcDataGridBulkAction> BulkActions { get; } = [];
    /// <summary>Formats Export offers. With more than one, Export opens a menu.</summary>
    public IReadOnlyList<ArcDataGridExportFormat> ExportFormats { get; set; } = [ArcDataGridExportFormat.Csv];

    /// <summary>Raised with the full row list after every edit, fill, paste, cut, clear, delete, undo or redo.</summary>
    public event EventHandler<IList<ArcDataGridRow>>? RowsEdited;
    /// <summary>Raised when any view state changes: sort, filters, search, columns, density, checks or grouping.</summary>
    public event EventHandler? ViewStateChanged;

    // ---- Layout and view shared with the surface ----
    internal readonly List<ViewItem> View = [];
    internal readonly List<ArcDataGridColumn> Visible = [];
    internal double[] ColX = [], ColW = [];
    internal int PinnedCount;
    internal double GutterWidth, PinEdge, ContentWidth, RowHeight = 40;
    internal bool FilterRowOpen;
    internal readonly HashSet<string> Checked = new(StringComparer.Ordinal);
    internal readonly Dictionary<(string Row, string Column), long> Flashes = [];
    internal string SearchText = string.Empty;
    internal List<ArcDataGridRow> Filtered = [];
    internal readonly Dictionary<string, double> TotalsCache = [];
    private readonly Dictionary<string, double> _resized = new(StringComparer.Ordinal);
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly Dictionary<ArcDataGridColumn, IReadOnlyList<string>> _derivedOptions = [];
    internal ArcDataGridSurface? Surface;
    private Canvas? _overlay;
    private ScrollBar? _vbar, _hbar;
    private Grid? _viewport;
    private bool _syncing;

    internal IReadOnlyList<ArcDataGridSort> SortKeys => Sort ?? [];

    static ArcDataGrid()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ArcDataGrid), new FrameworkPropertyMetadata(typeof(ArcDataGrid)));
        FocusableProperty.OverrideMetadata(typeof(ArcDataGrid), new FrameworkPropertyMetadata(false));
    }

    public ArcDataGrid()
    {
        Columns.CollectionChanged += (_, _) => RebuildView(false);
        BulkActions.CollectionChanged += (_, _) => BuildBulkActions();
        SizeChanged += (_, _) => { LayoutColumns(); UpdateChrome(); Surface?.InvalidateVisual(); PositionOverlays(); };
        Unloaded += (_, _) => { Surface?.StopLoops(); _statusTimer?.Stop(); };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArcDataGridAutomationPeer(this);

    public override void OnApplyTemplate()
    {
        DetachChrome();
        base.OnApplyTemplate();
        _viewport = GetTemplateChild("PART_Viewport") as Grid;
        if (_viewport is not null)
        {
            _viewport.Children.Clear();
            Surface = new ArcDataGridSurface(this);
            _overlay = new Canvas { ClipToBounds = true };
            _vbar = new ScrollBar { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right, Width = 8, Opacity = 0 };
            _hbar = new ScrollBar { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Height = 8, Opacity = 0 };
            _vbar.ValueChanged += (_, e) => { if (!_syncing) { Surface.ScrollTo(Surface.ScrollX, e.NewValue); } };
            _hbar.ValueChanged += (_, e) => { if (!_syncing) { Surface.ScrollTo(e.NewValue, Surface.ScrollY); } };
            _viewport.Children.Add(Surface);
            _viewport.Children.Add(_overlay);
            _viewport.Children.Add(_vbar);
            _viewport.Children.Add(_hbar);
            _viewport.MouseEnter += (_, _) => UpdateScrollBars();
            _viewport.MouseLeave += (_, _) => UpdateScrollBars();
        }
        AttachChrome();
        _applyingTemplate = true;
        RebuildView(false);
        _applyingTemplate = false;
    }

    private void Relayout()
    {
        RowHeight = Density switch { ArcDataGridDensity.Compact => 32, ArcDataGridDensity.Comfortable => 48, _ => 40 };
        LayoutColumns();
        UpdateChrome();
        Surface?.InvalidateMeasure();
        Surface?.InvalidateVisual();
        Surface?.SnapSelection();
        PositionOverlays();
        if (!_rebuilding) { RaiseViewState(); }
    }

    private void RowsReplaced()
    {
        _derivedOptions.Clear();
        _undo.Clear(); _redo.Clear();
        RebuildView(false);
    }

    private void ViewStateChangedExternally(bool sorted)
    {
        if (_rebuilding) { return; }
        RebuildView(sorted);
    }

    private void FiltersChangedExternally()
    {
        if (_rebuilding) { return; }
        if (Filters is { Count: > 0 }) { FilterRowOpen = true; }
        RebuildFilterInputs();
        RebuildView(false);
    }

    private void CheckedChangedExternally()
    {
        if (_rebuilding) { return; }
        Checked.Clear();
        foreach (string id in SelectedRowIds ?? []) { Checked.Add(id); }
        UpdateChrome();
        Surface?.InvalidateVisual();
    }

    private bool _rebuilding;
    private IEnumerable<string> PinnedKeys()
    {
        if (PinnedColumns is { } explicitKeys) { return explicitKeys; }
        var declared = Columns.Where(column => column.Pinned).Select(column => column.Key).ToArray();
        return declared.Length > 0 ? declared : Columns.Take(1).Select(column => column.Key);
    }
    private IEnumerable<string> HiddenKeys() => HiddenColumns ?? Columns.Where(column => column.Hidden).Select(column => column.Key);

    internal void LayoutColumns()
    {
        var pinned = PinnedKeys().ToHashSet(StringComparer.Ordinal);
        var hidden = HiddenKeys().ToHashSet(StringComparer.Ordinal);
        Visible.Clear();
        var shown = Columns.Where(column => !hidden.Contains(column.Key)).ToArray();
        Visible.AddRange(shown.Where(column => pinned.Contains(column.Key)));
        PinnedCount = Visible.Count;
        Visible.AddRange(shown.Where(column => !pinned.Contains(column.Key)));
        GutterWidth = RowSelection ? RowNumbers ? 48 : 40 : 0;
        ColX = new double[Visible.Count];
        ColW = new double[Visible.Count];
        double width = Surface?.ActualWidth > 0 ? Surface.ActualWidth : ActualWidth > 2 ? ActualWidth - 2 : 520;
        double pinnedSum = 0;
        for (int i = 0; i < Visible.Count; i++)
        {
            ColW[i] = _resized.TryGetValue(Visible[i].Key, out double user) ? user : Visible[i].Width;
            if (i < PinnedCount) { pinnedSum += ColW[i]; }
        }
        // Pinned columns give way so a narrow grid still shows scrolling columns.
        double room = width / 2 - GutterWidth;
        if (PinnedCount > 0 && pinnedSum > room && room > 0)
        {
            double scale = room / pinnedSum;
            for (int i = 0; i < PinnedCount; i++) { ColW[i] = Math.Floor(Math.Max(Math.Min(Visible[i].MinWidth, ColW[i]), ColW[i] * scale)); }
        }
        double x = GutterWidth;
        for (int i = 0; i < Visible.Count; i++) { ColX[i] = x; x += ColW[i]; }
        // The last column absorbs spare width on a wide grid.
        if (Visible.Count > 0 && x < width) { ColW[^1] += width - x; x = width; }
        ContentWidth = x;
        PinEdge = GutterWidth;
        for (int i = 0; i < PinnedCount; i++) { PinEdge += ColW[i]; }
    }

    internal IReadOnlyList<string> OptionsFor(ArcDataGridColumn column)
    {
        if (column.Options is { } options) { return options; }
        if (_derivedOptions.TryGetValue(column, out var cached)) { return cached; }
        var seen = new List<string>();
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Rows ?? [])
        {
            if (row[column.Key] is string value && set.Add(value)) { seen.Add(value); if (seen.Count >= 200) { break; } }
        }
        return _derivedOptions[column] = seen;
    }

    // ---- Filtering, sorting and grouping ----
    internal sealed class ViewItem
    {
        public ArcDataGridRow? Row;
        public int Number;
        public string? GroupKey;
        public string GroupValue = string.Empty;
        public int Level;
        public bool Expanded;
        public List<ArcDataGridRow>? GroupRows;
        public ArcDataGridColumn? GroupColumn;
        public bool IsGroup => Row is null;
    }

    internal void RebuildView(bool animateSort)
    {
        if (_rebuilding) { return; }
        _rebuilding = true;
        try
        {
            RowHeight = Density switch { ArcDataGridDensity.Compact => 32, ArcDataGridDensity.Comfortable => 48, _ => 40 };
            int activeNumber = NumberAt(_active.Row), anchorNumber = NumberAt(_anchor.Row), focusNumber = NumberAt(_focus.Row);
            Dictionary<string, int>? oldIndex = null;
            if (animateSort && Surface is not null && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
            {
                oldIndex = new Dictionary<string, int>(View.Count);
                for (int i = 0; i < View.Count; i++) { if (View[i].Row is { } r) { oldIndex[r.Id] = i; } }
            }
            LayoutColumns();
            IEnumerable<ArcDataGridRow> source = Rows ?? [];
            var matcher = ArcDataGridModel.RowMatcher(Columns, Filters, SearchText, Visible);
            if (matcher is not null) { source = source.Where(matcher); }
            Filtered = ArcDataGridModel.SortRows(source, SortKeys, Columns);
            View.Clear();
            var groupKeys = GroupBy ?? [];
            if (groupKeys.Count > 0) { AppendGroups(Filtered, 0, string.Empty, groupKeys); }
            else
            {
                int number = 1;
                foreach (var row in Filtered) { View.Add(new ViewItem { Row = row, Number = number++ }); }
            }
            int numbered = 1;
            foreach (var item in View) { if (!item.IsGroup) { item.Number = numbered++; } }
            RecomputeTotals();
            RemapSelection(activeNumber, anchorNumber, focusNumber);
            UpdateChrome();
            UpdateStatus();
            RebuildFilterInputs();
            Surface?.InvalidateMeasure();
            Surface?.ClampScroll();
            if (oldIndex is not null) { Surface?.BeginRowSlide(oldIndex); }
            Surface?.SnapSelection();
            Surface?.InvalidateVisual();
            PositionOverlays();
            UpdateScrollBars();
        }
        finally { _rebuilding = false; }
        RaiseViewState();
    }

    private void AppendGroups(List<ArcDataGridRow> rows, int level, string path, IReadOnlyList<string> keys)
    {
        var column = Columns.FirstOrDefault(c => c.Key == keys[level]);
        if (column is null) { foreach (var row in rows) { View.Add(new ViewItem { Row = row }); } return; }
        var order = new List<string>();
        var buckets = new Dictionary<string, List<ArcDataGridRow>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            string value = ArcDataGridModel.Format(row[column.Key], column);
            if (!buckets.TryGetValue(value, out var bucket)) { buckets[value] = bucket = []; order.Add(value); }
            bucket.Add(row);
        }
        if (column.Type == ArcDataGridColumnType.Select && !SortKeys.Any(s => s.Key == column.Key))
        {
            var options = OptionsFor(column);
            order.Sort((a, b) => { int x = IndexOf(options, a), y = IndexOf(options, b); return x.CompareTo(y); });
        }
        foreach (string value in order)
        {
            string key = path + "\u0001" + value;
            bool expanded = !_collapsed.Contains(key);
            View.Add(new ViewItem { GroupKey = key, GroupValue = value.Length == 0 ? "(Empty)" : value, Level = level, Expanded = expanded, GroupRows = buckets[value], GroupColumn = column });
            if (!expanded) { continue; }
            if (level + 1 < keys.Count) { AppendGroups(buckets[value], level + 1, key, keys); }
            else { foreach (var row in buckets[value]) { View.Add(new ViewItem { Row = row, Level = level + 1 }); } }
        }
        static int IndexOf(IReadOnlyList<string> list, string value) { for (int i = 0; i < list.Count; i++) { if (list[i] == value) { return i; } } return int.MaxValue; }
    }

    private void RecomputeTotals()
    {
        TotalsCache.Clear();
        foreach (var column in Visible)
        {
            var kind = ArcDataGridModel.DefaultAggregate(column);
            if (ArcDataGridModel.Aggregate(Filtered, column, kind) is { } total) { TotalsCache[column.Key] = total; }
        }
    }

    internal void ToggleGroup(ViewItem group)
    {
        if (group.GroupKey is null) { return; }
        if (!_collapsed.Add(group.GroupKey)) { _collapsed.Remove(group.GroupKey); }
        RebuildView(false);
    }

    internal bool AllGroupsCollapsed => View.Count(item => item.IsGroup && item.Level == 0 && item.Expanded) == 0;

    private void ToggleAllGroups()
    {
        if (AllGroupsCollapsed) { _collapsed.Clear(); }
        else
        {
            foreach (var item in View.Where(item => item.IsGroup && item.GroupKey is not null).ToArray()) { _collapsed.Add(item.GroupKey!); }
        }
        RebuildView(false);
    }

    internal void CycleSort(ArcDataGridColumn column, bool additive)
    {
        if (!column.Sortable) { return; }
        var keys = SortKeys.ToList();
        int index = keys.FindIndex(key => key.Key == column.Key);
        if (additive)
        {
            if (index < 0) { keys.Add(new(column.Key)); }
            else if (!keys[index].Descending) { keys[index] = new(column.Key, true); }
            else { keys.RemoveAt(index); }
        }
        else
        {
            if (index < 0) { keys = [new(column.Key)]; }
            else if (!keys[index].Descending) { keys = [new(column.Key, true)]; }
            else { keys = []; }
        }
        SetSort(keys);
        Announce(keys.Count == 0 ? "Sort cleared" : $"Sorted by {string.Join(", ", keys.Select(key => key.Key))}");
    }

    private void SetSort(IReadOnlyList<ArcDataGridSort> keys)
    {
        _rebuilding = true;
        SetCurrentValue(SortProperty, keys);
        _rebuilding = false;
        RebuildView(true);
    }

    internal void SetFilter(string key, object? value)
    {
        var next = Filters?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
        if (value is null or "") { next.Remove(key); } else { next[key] = value; }
        _rebuilding = true;
        SetCurrentValue(FiltersProperty, next);
        _rebuilding = false;
        RebuildView(false);
    }

    internal void ClearFilters()
    {
        _rebuilding = true;
        SetCurrentValue(FiltersProperty, new Dictionary<string, object>());
        _rebuilding = false;
        if (_search is not null) { _search.Text = string.Empty; }
        SearchText = string.Empty;
        RebuildFilterInputs();
        RebuildView(false);
    }

    private void SetList(DependencyProperty property, IReadOnlyList<string> keys)
    {
        _rebuilding = true;
        SetCurrentValue(property, keys);
        _rebuilding = false;
        RebuildView(false);
    }

    private bool _applyingTemplate;
    private void RaiseViewState()
    {
        if (_applyingTemplate) { return; }
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
        UIElementAutomationPeer.FromElement(this)?.InvalidatePeer();
    }

    /// <summary>Returns the grid to its starting view: no sort, filters, search, checks, grouping or column changes, with the given rows.</summary>
    public void ResetState(IList<ArcDataGridRow>? rows = null)
    {
        CancelEdit();
        _rebuilding = true;
        ClearValue(SortProperty); ClearValue(FiltersProperty); ClearValue(HiddenColumnsProperty); ClearValue(PinnedColumnsProperty); ClearValue(GroupByProperty); ClearValue(SelectedRowIdsProperty); ClearValue(DensityProperty);
        Checked.Clear(); _collapsed.Clear(); _resized.Clear(); _undo.Clear(); _redo.Clear(); Flashes.Clear();
        SearchText = string.Empty;
        FilterRowOpen = false;
        if (_search is not null) { _search.Text = string.Empty; }
        if (rows is not null) { Rows = rows; }
        _rebuilding = false;
        ClearStatus();
        // The old view must not carry the old active row over to the fresh one.
        View.Clear();
        _active = new CellRef(0, 0); _anchor = _focus = _active;
        Surface?.ScrollTo(0, 0);
        RebuildFilterInputs();
        RebuildView(false);
    }

    // ---- Selection ----
    internal readonly record struct CellRef(int Column, int Row);
    private CellRef _active, _anchor, _focus;
    internal CellRef Active => _active;
    internal (int Left, int Top, int Right, int Bottom) Range => (Math.Min(_anchor.Column, _focus.Column), Math.Min(_anchor.Row, _focus.Row), Math.Max(_anchor.Column, _focus.Column), Math.Max(_anchor.Row, _focus.Row));
    private int NumberAt(int row) => row >= 0 && row < View.Count ? View[row].Number : 0;

    // The selection is a position among the data rows, as its A1 address says: it stays put while the rows under it sort, group or collapse.
    private void RemapSelection(int activeNumber, int anchorNumber, int focusNumber)
    {
        int Find(int number)
        {
            int last = -1;
            for (int i = 0; i < View.Count; i++)
            {
                if (View[i].IsGroup) { continue; }
                if (View[i].Number == Math.Max(1, number)) { return i; }
                last = i;
            }
            return Math.Max(0, last);
        }
        int lastColumn = Math.Max(0, Visible.Count - 1);
        _active = new CellRef(Math.Clamp(_active.Column, 0, lastColumn), Find(activeNumber));
        _anchor = new CellRef(Math.Clamp(_anchor.Column, 0, lastColumn), Find(anchorNumber));
        _focus = new CellRef(Math.Clamp(_focus.Column, 0, lastColumn), Find(focusNumber));
    }

    private int NextDataRow(int from, int direction)
    {
        for (int i = from; i >= 0 && i < View.Count; i += direction) { if (!View[i].IsGroup) { return i; } }
        for (int i = from; i >= 0 && i < View.Count; i -= direction) { if (!View[i].IsGroup) { return i; } }
        return from;
    }

    internal void Select(int column, int row, bool extend, bool reveal = true)
    {
        column = Math.Clamp(column, 0, Math.Max(0, Visible.Count - 1));
        row = Math.Clamp(row, 0, Math.Max(0, View.Count - 1));
        if (View.Count > 0 && View[row].IsGroup) { row = NextDataRow(row, row >= _focus.Row ? 1 : -1); }
        _focus = new CellRef(column, row);
        if (!extend) { _active = _anchor = _focus; }
        if (reveal) { Surface?.Reveal(column, row); }
        Surface?.AnimateSelection();
        UpdateStatus();
        UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }

    private void SelectBlock(int left, int top, int right, int bottom, CellRef active)
    {
        _anchor = new CellRef(left, top);
        _focus = new CellRef(right, bottom);
        _active = active;
        Surface?.AnimateSelection();
        UpdateStatus();
    }

    internal bool InRange(int column, int row)
    {
        var (l, t, r, b) = Range;
        return column >= l && column <= r && row >= t && row <= b;
    }

    /// <summary>The A1-style address of the selection: columns count visible columns, rows count data rows in view order.</summary>
    internal string Address()
    {
        if (View.Count == 0 || Visible.Count == 0) { return string.Empty; }
        var (l, t, r, b) = Range;
        string Cell(int c, int row) => ArcDataGridModel.ColumnLetters(c) + (row < View.Count ? View[row].Number : 0).ToString(CultureInfo.InvariantCulture);
        return l == r && t == b ? Cell(l, t) : Cell(l, t) + ":" + Cell(r, b);
    }

    internal IEnumerable<(ArcDataGridRow Row, ArcDataGridColumn Column)> RangeCells()
    {
        var (l, t, r, b) = Range;
        for (int row = t; row <= b && row < View.Count; row++)
        {
            if (View[row].Row is not { } item) { continue; }
            for (int c = l; c <= r && c < Visible.Count; c++) { yield return (item, Visible[c]); }
        }
    }

    internal void ToggleChecked(string id)
    {
        if (!Checked.Add(id)) { Checked.Remove(id); }
        PublishChecked();
    }

    internal void ToggleAllChecked()
    {
        bool all = Filtered.Count > 0 && Filtered.All(row => Checked.Contains(row.Id));
        if (all) { foreach (var row in Filtered) { Checked.Remove(row.Id); } }
        else { foreach (var row in Filtered) { Checked.Add(row.Id); } }
        PublishChecked();
    }

    private void PublishChecked()
    {
        _rebuilding = true;
        // Ids follow data order, whichever way they were checked.
        SetCurrentValue(SelectedRowIdsProperty, (Rows ?? []).Where(row => Checked.Contains(row.Id)).Select(row => row.Id).ToArray());
        _rebuilding = false;
        UpdateChrome();
        Surface?.InvalidateVisual();
        Announce($"{Checked.Count} {(Checked.Count == 1 ? "row" : "rows")} checked");
        RaiseViewState();
    }

    internal void ResizeColumn(ArcDataGridColumn column, double width)
    {
        _resized[column.Key] = Math.Clamp(width, column.MinWidth, 560);
        LayoutColumns();
        Surface?.ClampScroll();
        Surface?.SnapSelection();
        Surface?.InvalidateVisual();
        PositionOverlays();
        UpdateScrollBars();
    }

    // ---- Editing ----
    private sealed record Patch(string Row, string Column, object? Before, object? After);
    private sealed class UndoStep { public List<Patch>? Patches; public List<ArcDataGridRow>? RowsBefore, RowsAfter; public string Label = string.Empty; }
    private readonly List<UndoStep> _undo = [], _redo = [];
    internal bool CanUndo => _undo.Count > 0;
    internal bool CanRedo => _redo.Count > 0;

    private ArcDataGridRow? FindRow(string id)
    {
        foreach (var row in Rows ?? []) { if (row.Id == id) { return row; } }
        return null;
    }

    private void ApplyEdits(List<Patch> patches, string label, bool announce = true)
    {
        var effective = patches.Where(patch => !Equals(patch.Before, patch.After)).ToList();
        if (effective.Count == 0) { return; }
        var byId = (Rows ?? []).ToDictionary(row => row.Id, StringComparer.Ordinal);
        foreach (var patch in effective) { if (byId.TryGetValue(patch.Row, out var row)) { row.Values[patch.Column] = patch.After; } }
        Push(new UndoStep { Patches = effective, Label = label });
        Flash(effective);
        AfterEdit();
        if (announce) { Announce(label); }
    }

    private void Push(UndoStep step)
    {
        _undo.Add(step);
        if (_undo.Count > 100) { _undo.RemoveAt(0); }
        _redo.Clear();
    }

    // An edit never re-sorts or re-filters, so rows do not jump under the cursor.
    private void AfterEdit()
    {
        RecomputeTotals();
        UpdateChrome();
        UpdateStatus();
        Surface?.InvalidateVisual();
        if (Rows is { } rows) { RowsEdited?.Invoke(this, rows); }
        RaiseViewState();
    }

    private void Flash(IEnumerable<Patch> patches)
    {
        long now = Environment.TickCount64;
        foreach (var patch in patches) { Flashes[(patch.Row, patch.Column)] = now; }
        Surface?.WakeLoop();
    }

    public void Undo()
    {
        if (_undo.Count == 0) { return; }
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(step);
        Replay(step, true);
        Announce($"Undid {step.Label.ToLowerInvariant()}");
    }

    public void Redo()
    {
        if (_redo.Count == 0) { return; }
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(step);
        Replay(step, false);
        Announce($"Redid {step.Label.ToLowerInvariant()}");
    }

    private void Replay(UndoStep step, bool backwards)
    {
        CancelEdit();
        ClearStatus();
        if (step.Patches is { } patches)
        {
            var byId = (Rows ?? []).ToDictionary(row => row.Id, StringComparer.Ordinal);
            foreach (var patch in patches) { if (byId.TryGetValue(patch.Row, out var row)) { row.Values[patch.Column] = backwards ? patch.Before : patch.After; } }
            Flash(patches);
            AfterEdit();
            return;
        }
        var restored = backwards ? step.RowsBefore : step.RowsAfter;
        if (restored is null || Rows is not { } rows) { return; }
        rows.Clear();
        foreach (var row in restored) { rows.Add(row); }
        RebuildView(false);
        RowsEdited?.Invoke(this, rows);
    }

    internal bool IsColumnEditable(ArcDataGridColumn column) => column.Editable;

    private void ClearRange()
    {
        var patches = new List<Patch>();
        foreach (var (row, column) in RangeCells())
        {
            if (column.Editable) { patches.Add(new Patch(row.Id, column.Key, row[column.Key], null)); }
        }
        ApplyEdits(patches, "Clear");
    }

    private void FillDown()
    {
        var (l, t, r, b) = Range;
        if (t == b || View[t].Row is not { } top) { return; }
        var patches = new List<Patch>();
        for (int y = t + 1; y <= b && y < View.Count; y++)
        {
            if (View[y].Row is not { } row) { continue; }
            for (int c = l; c <= r; c++)
            {
                var column = Visible[c];
                if (column.Editable) { patches.Add(new Patch(row.Id, column.Key, row[column.Key], top[column.Key])); }
            }
        }
        ApplyEdits(patches, "Fill down");
    }

    /// <summary>Extends the selection to a new corner, continuing each column or row as a series.</summary>
    internal void FillTo(int column, int row)
    {
        var (l, t, r, b) = Range;
        column = Math.Clamp(column, 0, Visible.Count - 1);
        row = Math.Clamp(row, 0, View.Count - 1);
        var patches = new List<Patch>();
        int overDown = row - b, overUp = t - row, overRight = column - r, overLeft = l - column;
        if (Math.Max(overDown, overUp) >= Math.Max(overRight, overLeft) && Math.Max(overDown, overUp) > 0)
        {
            bool forward = overDown > 0;
            var targets = DataRows(forward ? b + 1 : row, forward ? row : t - 1);
            if (!forward) { targets.Reverse(); }
            var sources = DataRows(t, b);
            for (int c = l; c <= r; c++)
            {
                var col = Visible[c];
                if (!col.Editable) { continue; }
                var values = ArcDataGridModel.Extend(sources.Select(s => s[col.Key]).ToArray(), targets.Count, forward, col.Type);
                for (int i = 0; i < targets.Count; i++) { patches.Add(new Patch(targets[i].Id, col.Key, targets[i][col.Key], values[i])); }
            }
            if (forward) { SelectBlock(l, t, r, row, _active); } else { SelectBlock(l, row, r, b, _active); }
        }
        else if (Math.Max(overRight, overLeft) > 0)
        {
            bool forward = overRight > 0;
            var columns = forward ? Enumerable.Range(r + 1, overRight).ToArray() : Enumerable.Range(column, overLeft).Reverse().ToArray();
            foreach (var source in DataRows(t, b))
            {
                var values = ArcDataGridModel.Extend(Enumerable.Range(l, r - l + 1).Select(c => source[Visible[c].Key]).ToArray(), columns.Length, forward);
                for (int i = 0; i < columns.Length; i++)
                {
                    var col = Visible[columns[i]];
                    if (col.Editable) { patches.Add(new Patch(source.Id, col.Key, source[col.Key], values[i])); }
                }
            }
            if (forward) { SelectBlock(l, t, column, b, _active); } else { SelectBlock(column, t, r, b, _active); }
        }
        ApplyEdits(patches, "Fill");
    }

    private List<ArcDataGridRow> DataRows(int top, int bottom)
    {
        var rows = new List<ArcDataGridRow>();
        for (int y = top; y <= bottom && y < View.Count; y++) { if (y >= 0 && View[y].Row is { } row) { rows.Add(row); } }
        return rows;
    }

    // ---- Clipboard ----
    private List<string[]> RangeMatrix()
    {
        var (l, _, r, _) = Range;
        var (_, t, _, b) = Range;
        var matrix = new List<string[]>();
        foreach (var row in DataRows(t, b))
        {
            var line = new string[r - l + 1];
            for (int c = l; c <= r; c++) { line[c - l] = ArcDataGridModel.Format(row[Visible[c].Key], Visible[c]); }
            matrix.Add(line);
        }
        return matrix;
    }

    private static void SetClipboard(IReadOnlyList<string[]> matrix)
    {
        var data = new DataObject();
        data.SetText(ArcDataGridModel.ToTsv(matrix), TextDataFormat.UnicodeText);
        data.SetText(HtmlFragment(ArcDataGridModel.ToHtml(matrix)), TextDataFormat.Html);
        try { Clipboard.SetDataObject(data, true); } catch (System.Runtime.InteropServices.ExternalException) { }
    }

    // The CF_HTML header that lets Excel and Sheets read the table.
    private static string HtmlFragment(string html)
    {
        const string Header = "Version:0.9\r\nStartHTML:{0:D8}\r\nEndHTML:{1:D8}\r\nStartFragment:{2:D8}\r\nEndFragment:{3:D8}\r\n";
        string open = "<html><body><!--StartFragment-->", close = "<!--EndFragment--></body></html>";
        int headerLength = string.Format(CultureInfo.InvariantCulture, Header, 0, 0, 0, 0).Length;
        int start = headerLength + Encoding.UTF8.GetByteCount(open), end = start + Encoding.UTF8.GetByteCount(html);
        return string.Format(CultureInfo.InvariantCulture, Header, headerLength, end + Encoding.UTF8.GetByteCount(close), start, end) + open + html + close;
    }

    internal void CopyRange()
    {
        var matrix = RangeMatrix();
        SetClipboard(matrix);
        int cells = matrix.Sum(row => row.Length);
        ShowStatus($"Copied {cells:N0} {(cells == 1 ? "cell" : "cells")}", null);
    }

    private void CutRange()
    {
        SetClipboard(RangeMatrix());
        ClearRange();
    }

    internal void PasteClipboard()
    {
        List<string[]>? matrix = null;
        try
        {
            if (Clipboard.ContainsText(TextDataFormat.Html)) { matrix = ArcDataGridModel.ParseHtmlTable(Clipboard.GetText(TextDataFormat.Html)); }
            if (matrix is null && Clipboard.ContainsText()) { matrix = ArcDataGridModel.ParseTsv(Clipboard.GetText()); }
        }
        catch (System.Runtime.InteropServices.ExternalException) { return; }
        if (matrix is { Count: > 0 }) { Paste(matrix); }
    }

    /// <summary>Writes a block at the selection. A selection that is a whole multiple of the block is tiled; cells that do not parse are skipped and reported.</summary>
    internal void Paste(IReadOnlyList<string[]> matrix)
    {
        var (l, t, r, b) = Range;
        int height = matrix.Count, width = matrix.Max(row => row.Length);
        var rows = DataRows(t, b);
        int selectedRows = rows.Count, selectedColumns = r - l + 1;
        bool tile = selectedRows >= height && selectedColumns >= width && selectedRows % height == 0 && selectedColumns % width == 0;
        int fillRows = tile ? selectedRows : height, fillColumns = tile ? selectedColumns : width;
        if (!tile)
        {
            rows = [];
            for (int y = t; y < View.Count && rows.Count < fillRows; y++) { if (View[y].Row is { } row) { rows.Add(row); } }
        }
        var patches = new List<Patch>();
        int skipped = 0;
        for (int y = 0; y < rows.Count; y++)
        {
            var source = matrix[y % height];
            for (int x = 0; x < fillColumns && l + x < Visible.Count; x++)
            {
                var column = Visible[l + x];
                if (!column.Editable) { continue; }
                string text = x % width < source.Length ? source[x % width] : string.Empty;
                if (ArcDataGridModel.TryParse(text, column, OptionsFor(column), out object? value)) { patches.Add(new Patch(rows[y].Id, column.Key, rows[y][column.Key], value)); }
                else { skipped++; }
            }
        }
        ApplyEdits(patches, "Paste");
        int lastRow = t;
        if (rows.Count > 0) { for (int y = t; y < View.Count; y++) { if (View[y].Row == rows[^1]) { lastRow = y; break; } } }
        SelectBlock(l, t, Math.Min(l + fillColumns - 1, Visible.Count - 1), lastRow, _active);
        if (skipped > 0) { ShowStatus($"Skipped {skipped:N0} {(skipped == 1 ? "value" : "values")} that did not fit", null); }
    }

    // ---- Row operations ----
    private IReadOnlyList<ArcDataGridRow> CheckedRows() => (Rows ?? []).Where(row => Checked.Contains(row.Id)).ToArray();

    private void CopyChecked()
    {
        var columns = Visible.ToArray();
        var rows = CheckedRows();
        var matrix = new List<string[]> { columns.Select(column => column.Header).ToArray() };
        foreach (var row in rows) { matrix.Add(columns.Select(column => ArcDataGridModel.Format(row[column.Key], column)).ToArray()); }
        SetClipboard(matrix);
        ShowStatus($"Copied {rows.Count:N0} {(rows.Count == 1 ? "row" : "rows")}", null);
    }

    private void DeleteChecked()
    {
        if (Rows is not { } rows || Checked.Count == 0) { return; }
        var before = rows.ToList();
        int count = 0;
        var kept = new List<ArcDataGridRow>(rows.Count);
        foreach (var row in before) { if (Checked.Contains(row.Id)) { count++; } else { kept.Add(row); } }
        rows.Clear();
        foreach (var row in kept) { rows.Add(row); }
        _undo.Add(new UndoStep { RowsBefore = before, RowsAfter = kept, Label = "Delete" });
        if (_undo.Count > 100) { _undo.RemoveAt(0); }
        _redo.Clear();
        Checked.Clear();
        PublishChecked();
        RebuildView(false);
        RowsEdited?.Invoke(this, rows);
        ShowStatus($"Deleted {count:N0} {(count == 1 ? "row" : "rows")}", ("Undo", Undo));
    }

    /// <summary>Writes rows in the given format. Numbers and days stay raw; column order follows the columns in view.</summary>
    public void Export(Stream stream, ArcDataGridExportFormat format, IEnumerable<ArcDataGridRow>? rows = null)
    {
        var data = rows ?? Filtered;
        if (format == ArcDataGridExportFormat.Csv)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(ArcDataGridModel.ToCsv(Visible, data));
            return;
        }
        ArcDataGridModel.WriteXlsx(stream, Visible, data, Label, PinnedCount);
    }

    private void ExportToFile(ArcDataGridExportFormat format, IReadOnlyList<ArcDataGridRow> rows)
    {
        string extension = format == ArcDataGridExportFormat.Csv ? "csv" : "xlsx";
        string slug = ExportFileName ?? string.Concat(Label.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = slug + "." + extension, Filter = format == ArcDataGridExportFormat.Csv ? "CSV (*.csv)|*.csv" : "Excel workbook (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) { return; }
        using (var stream = File.Create(dialog.FileName)) { Export(stream, format, rows); }
        ShowStatus($"Exported {rows.Count:N0} {(rows.Count == 1 ? "row" : "rows")}", null);
    }

    // ---- Keyboard ----
    internal void HandleKey(KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Visible.Count == 0) { return; }
        int lastRow = LastDataRow(), lastCol = Visible.Count - 1;
        var cursor = shift ? _focus : _active;
        int page = Math.Max(1, (int)((Surface?.BodyHeight ?? RowHeight * 9) / RowHeight) - 1);
        bool handled = true;
        switch (key)
        {
            case Key.Up when alt == false: Select(cursor.Column, ctrl ? FirstDataRow() : StepRow(cursor.Row, -1), shift); break;
            case Key.Down when alt && Visible[_active.Column].Type is ArcDataGridColumnType.Select or ArcDataGridColumnType.Date: BeginEdit(null, false, true); break;
            case Key.Down when alt == false: Select(cursor.Column, ctrl ? lastRow : StepRow(cursor.Row, 1), shift); break;
            case Key.Left when alt: ResizeColumn(Visible[_active.Column], ColW[_active.Column] - 16); break;
            case Key.Right when alt: ResizeColumn(Visible[_active.Column], ColW[_active.Column] + 16); break;
            case Key.Left: Select(ctrl ? 0 : cursor.Column - 1, cursor.Row, shift); break;
            case Key.Right: Select(ctrl ? lastCol : cursor.Column + 1, cursor.Row, shift); break;
            case Key.Home: Select(0, ctrl ? FirstDataRow() : cursor.Row, shift); break;
            case Key.End: Select(lastCol, ctrl ? lastRow : cursor.Row, shift); break;
            case Key.PageUp: Select(cursor.Column, Math.Max(FirstDataRow(), cursor.Row - page), shift); break;
            case Key.PageDown: Select(cursor.Column, Math.Min(lastRow, cursor.Row + page), shift); break;
            case Key.A when ctrl: SelectBlock(0, 0, lastCol, Math.Max(0, View.Count - 1), _active); break;
            case Key.Space when ctrl: { var (l, _, r, _) = Range; SelectBlock(l, 0, r, Math.Max(0, View.Count - 1), _active); break; }
            case Key.Space when shift && RowSelection:
                { var (_, t, _, b) = Range; var rows = DataRows(t, b); bool all = rows.All(row => Checked.Contains(row.Id)); foreach (var row in rows) { if (all) { Checked.Remove(row.Id); } else { Checked.Add(row.Id); } } PublishChecked(); break; }
            case Key.Enter or Key.F2: BeginEdit(null, false); break;
            case Key.Escape:
                {
                    var (l, t, r, b) = Range;
                    if (l != r || t != b) { Select(_active.Column, _active.Row, false); }
                    else if (Checked.Count > 0) { Checked.Clear(); PublishChecked(); }
                    else { handled = false; }
                    break;
                }
            case Key.Delete or Key.Back: ClearRange(); break;
            case Key.D when ctrl: FillDown(); break;
            case Key.C when ctrl: CopyRange(); break;
            case Key.X when ctrl: CutRange(); break;
            case Key.V when ctrl: PasteClipboard(); break;
            case Key.Z when ctrl && shift: Redo(); break;
            case Key.Z when ctrl: Undo(); break;
            case Key.Y when ctrl: Redo(); break;
            default: handled = false; break;
        }
        e.Handled = handled;
    }

    private int StepRow(int from, int direction)
    {
        for (int y = from + direction; y >= 0 && y < View.Count; y += direction) { if (!View[y].IsGroup) { return y; } }
        return from;
    }
    private int FirstDataRow() => NextDataRow(0, 1);
    private int LastDataRow() => View.Count == 0 ? 0 : NextDataRow(View.Count - 1, -1);

    // ---- Overlay plumbing ----
    internal void AfterScroll()
    {
        PositionOverlays();
        UpdateScrollBars();
    }

    internal void UpdateScrollBars()
    {
        if (_vbar is null || _hbar is null || Surface is null) { return; }
        _syncing = true;
        double body = Surface.BodyHeight, extent = View.Count * RowHeight, width = Surface.ActualWidth;
        _vbar.Margin = new Thickness(0, Surface.HeadHeight, 0, Surface.FootHeight);
        _vbar.Minimum = 0; _vbar.Maximum = Math.Max(0, extent - body); _vbar.ViewportSize = body; _vbar.LargeChange = body; _vbar.SmallChange = RowHeight; _vbar.Value = Surface.ScrollY;
        _hbar.Margin = new Thickness(PinEdge, 0, 0, Surface.FootHeight);
        _hbar.Minimum = 0; _hbar.Maximum = Math.Max(0, ContentWidth - width); _hbar.ViewportSize = Math.Max(0, width - PinEdge); _hbar.LargeChange = Math.Max(1, width - PinEdge); _hbar.SmallChange = 40; _hbar.Value = Surface.ScrollX;
        bool over = _viewport?.IsMouseOver == true;
        _vbar.Opacity = over && _vbar.Maximum > 0 ? 1 : 0;
        _hbar.Opacity = over && _hbar.Maximum > 0 ? 1 : 0;
        _vbar.IsHitTestVisible = _vbar.Opacity > 0;
        _hbar.IsHitTestVisible = _hbar.Opacity > 0;
        _syncing = false;
    }
}

internal sealed class ArcDataGridAutomationPeer(ArcDataGrid owner) : FrameworkElementAutomationPeer(owner), System.Windows.Automation.Provider.IGridProvider
{
    protected override string GetClassNameCore() => nameof(ArcDataGrid);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Grid ? this : base.GetPattern(patternInterface);
    public int RowCount => owner.View.Count;
    public int ColumnCount => owner.Visible.Count;
    // Cells are drawn, not elements. The live region and status bar carry the active address and values.
    public System.Windows.Automation.Provider.IRawElementProviderSimple? GetItem(int row, int column) => null;
}
