using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum TableSortDirection { Ascending, Descending }

public sealed class DataTableColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Sortable { get; set; } = true;
    public bool Numeric { get; set; }
    /// <summary>Defaults to an even share of the table, as a fixed-layout table track would.</summary>
    public DataGridLength Width { get; set; } = new(1, DataGridLengthUnitType.Star);
    /// <summary>Custom cell content; its DataContext is the row.</summary>
    public DataTemplate? CellTemplate { get; set; }
}

[TemplatePart(Name = "PART_DataGrid", Type = typeof(DataGrid))]
[TemplatePart(Name = "PART_EmptyState", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_SelectionStatus", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_ClearSelection", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_Footer", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Clip", Type = typeof(FrameworkElement))]
public class SortableDataTable : Control
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IEnumerable<object>), typeof(SortableDataTable), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty RowKeyPathProperty = DependencyProperty.Register(nameof(RowKeyPath), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("Id", Changed));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("Data table", Changed));
    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("No rows to show", Changed));
    public static readonly DependencyProperty DefaultSortKeyProperty = DependencyProperty.Register(nameof(DefaultSortKey), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata(string.Empty, SortChanged));
    public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(nameof(SortDirection), typeof(TableSortDirection), typeof(SortableDataTable), new FrameworkPropertyMetadata(TableSortDirection.Ascending, Changed));
    public static readonly DependencyProperty SelectableProperty = DependencyProperty.Register(nameof(Selectable), typeof(bool), typeof(SortableDataTable), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty SelectedKeysProperty = DependencyProperty.Register(nameof(SelectedKeys), typeof(ObservableCollection<string>), typeof(SortableDataTable), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty ItemNameProperty = DependencyProperty.Register(nameof(ItemName), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("rows", Changed));
    public static readonly DependencyProperty ItemNameOneProperty = DependencyProperty.Register(nameof(ItemNameOne), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("row", Changed));
    public static readonly DependencyProperty MinTableWidthProperty = DependencyProperty.Register(nameof(MinTableWidth), typeof(double), typeof(SortableDataTable), new FrameworkPropertyMetadata(560d, Changed));

    private const double SelectColumnWidth = 44, PrimaryLeftPadding = 8, CellPadding = 16, CornerInner = 17;
    private DataGrid? _grid;
    private TextBlock? _empty;
    private TextBlock? _status;
    private System.Windows.Controls.Button? _clear;
    private FrameworkElement? _footer, _clip;
    private CheckBox? _selectAll;
    private bool _updating;
    private readonly List<(DataGridColumn Column, DataTableColumn Source)> _layout = new();
    private readonly List<object> _sorted = new();
    private string _sortKey = string.Empty;
    private string? _anchor;

    public IEnumerable<object>? Rows { get => (IEnumerable<object>?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public string RowKeyPath { get => (string)GetValue(RowKeyPathProperty); set => SetValue(RowKeyPathProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }
    public string DefaultSortKey { get => (string)GetValue(DefaultSortKeyProperty); set => SetValue(DefaultSortKeyProperty, value); }
    public TableSortDirection SortDirection { get => (TableSortDirection)GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }
    public bool Selectable { get => (bool)GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }
    public ObservableCollection<string> SelectedKeys { get => (ObservableCollection<string>)GetValue(SelectedKeysProperty); set => SetValue(SelectedKeysProperty, value); }
    /// <summary>The plural noun of the footer count; <see cref="ItemNameOne"/> is its singular.</summary>
    public string ItemName { get => (string)GetValue(ItemNameProperty); set => SetValue(ItemNameProperty, value); }
    public string ItemNameOne { get => (string)GetValue(ItemNameOneProperty); set => SetValue(ItemNameOneProperty, value); }
    /// <summary>Below this width the table keeps its columns and scrolls sideways inside the grid.</summary>
    public double MinTableWidth { get => (double)GetValue(MinTableWidthProperty); set => SetValue(MinTableWidthProperty, value); }
    public ObservableCollection<DataTableColumn> Columns { get; } = new();

    static SortableDataTable() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SortableDataTable), new FrameworkPropertyMetadata(typeof(SortableDataTable)));
    public SortableDataTable()
    {
        SetCurrentValue(SelectedKeysProperty, new ObservableCollection<string>());
        Columns.CollectionChanged += (_, _) => Refresh();
        Loaded += (_, _) => Refresh();
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SortableDataTable)d).Refresh();
    private static void SortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var table = (SortableDataTable)d;
        table._sortKey = (string)e.NewValue;
        table.Refresh();
    }

    public override void OnApplyTemplate()
    {
        ReleaseGrid(); base.OnApplyTemplate();
        _grid = GetTemplateChild("PART_DataGrid") as DataGrid;
        _empty = GetTemplateChild("PART_EmptyState") as TextBlock;
        _status = GetTemplateChild("PART_SelectionStatus") as TextBlock;
        _clear = GetTemplateChild("PART_ClearSelection") as System.Windows.Controls.Button;
        _footer = GetTemplateChild("PART_Footer") as FrameworkElement;
        _clip = GetTemplateChild("PART_Clip") as FrameworkElement;
        if (_grid is not null)
        {
            _grid.Sorting += GridSorting;
            _grid.SelectionChanged += GridSelectionChanged;
            _grid.LoadingRow += GridLoadingRow;
            _grid.SizeChanged += GridSizeChanged;
            _grid.PreviewMouseLeftButtonDown += GridPressed;
            _grid.PreviewKeyDown += GridKeyDown;
            _grid.SelectionMode = DataGridSelectionMode.Extended;
            _grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        }
        if (_clip is not null) _clip.SizeChanged += ClipSizeChanged;
        if (_clear is not null) _clear.Click += ClearClicked;
        Refresh();
    }
    private void ReleaseGrid()
    {
        if (_grid is not null)
        {
            _grid.Sorting -= GridSorting;
            _grid.SelectionChanged -= GridSelectionChanged;
            _grid.LoadingRow -= GridLoadingRow;
            _grid.SizeChanged -= GridSizeChanged;
            _grid.PreviewMouseLeftButtonDown -= GridPressed;
            _grid.PreviewKeyDown -= GridKeyDown;
        }
        if (_clip is not null) _clip.SizeChanged -= ClipSizeChanged;
        if (_clear is not null) _clear.Click -= ClearClicked;
        _grid = null; _empty = null; _status = null; _clear = null; _footer = null; _clip = null;
    }
    private void Refresh()
    {
        if (_grid is null) return;
        if (string.IsNullOrEmpty(_sortKey)) _sortKey = DefaultSortKey;
        BuildColumns();
        _sorted.Clear();
        _sorted.AddRange((Rows ?? []).OrderBy(row => row, new RowComparer(this)));
        _grid.ItemsSource = null;
        _grid.ItemsSource = _sorted.ToList();
        // Changing the source clears every column's arrow, so the sort state goes on afterwards.
        foreach (var (column, source) in _layout) column.SortDirection = source.Key != _sortKey ? null : SortDirection == TableSortDirection.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        LayoutColumns();
        _empty!.Text = EmptyMessage;
        _empty.Visibility = _sorted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _grid.SelectionMode = Selectable ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;
        if (_footer is not null) _footer.Visibility = Selectable ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(_grid, Caption);
        ApplySelection();
    }

    private void BuildColumns()
    {
        var resources = Template.Resources;
        var header = (Style)resources["Header"]; var cell = (Style)resources["Cell"];
        _grid!.Columns.Clear();
        _layout.Clear();
        _selectAll = null;
        if (Selectable)
        {
            _selectAll = new CheckBox { Style = (Style)resources["SelectBox"], IsThreeState = true };
            AutomationProperties.SetName(_selectAll, "Select all rows");
            _selectAll.Click += SelectAllClicked;
            _grid.Columns.Add(new DataGridTemplateColumn { Header = _selectAll, CanUserSort = false, Width = SelectColumnWidth, HeaderStyle = (Style)resources["SelectHeader"], CellStyle = (Style)resources["SelectCell"], CellTemplate = (DataTemplate)resources["SelectCellTemplate"] });
        }
        for (int index = 0; index < Columns.Count; index++)
        {
            var column = Columns[index];
            bool primary = index == 0 && Selectable, sorted = column.Key == _sortKey;
            double left = primary ? PrimaryLeftPadding : CellPadding;
            var padding = new Thickness(left, 0, CellPadding, 0);
            var headerStyle = new Style(typeof(DataGridColumnHeader), header);
            // Right-aligned columns lead with the arrow so the label lines up with the digits below it.
            headerStyle.Setters.Add(new Setter(PaddingProperty, column.Numeric ? new Thickness(CellPadding, 0, left, 0) : padding));
            if (column.Numeric) headerStyle.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.RightToLeft));
            var cellStyle = new Style(typeof(DataGridCell), cell);
            cellStyle.Setters.Add(new Setter(PaddingProperty, padding));
            if (sorted) cellStyle.Setters.Add(new Setter(TagProperty, "sorted"));
            var dataColumn = new DataGridTemplateColumn
            {
                Header = column.Header,
                SortMemberPath = column.Key,
                CanUserSort = column.Sortable,
                HeaderStyle = headerStyle,
                CellStyle = cellStyle,
                CellTemplate = column.CellTemplate ?? TextCell(column),
            };
            _grid.Columns.Add(dataColumn);
            _layout.Add((dataColumn, column));
        }
    }

    private static DataTemplate TextCell(DataTableColumn column)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(string.IsNullOrEmpty(column.Path) ? column.Key : column.Path) { Mode = BindingMode.OneWay });
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(TextBlock.LineHeightProperty, 19.6);
        text.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
        if (column.Numeric) text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
        var template = new DataTemplate { VisualTree = text };
        template.Seal();
        return template;
    }

    // A table stretches to its host but never narrower than the minimum; fixed tracks keep their ratio and stars share the rest.
    private void LayoutColumns()
    {
        if (_grid is null || _layout.Count == 0 || _grid.ActualWidth <= 0) return;
        double total = Math.Max(_grid.ActualWidth, MinTableWidth) - (Selectable ? SelectColumnWidth : 0);
        double fixedWidth = _layout.Where(item => item.Source.Width.IsAbsolute).Sum(item => item.Source.Width.Value);
        double stars = _layout.Where(item => !item.Source.Width.IsAbsolute).Sum(item => item.Source.Width.IsStar ? item.Source.Width.Value : 1);
        double scale = stars == 0 && fixedWidth > 0 ? total / fixedWidth : 1, rest = Math.Max(0, total - fixedWidth);
        foreach (var (column, source) in _layout)
            column.Width = source.Width.IsAbsolute ? source.Width.Value * scale : rest * (source.Width.IsStar ? source.Width.Value : 1) / stars;
    }

    private void GridSizeChanged(object sender, SizeChangedEventArgs e) { if (e.WidthChanged) LayoutColumns(); }
    private void ClipSizeChanged(object sender, SizeChangedEventArgs e) => _clip!.Clip = new RectangleGeometry(new Rect(e.NewSize), CornerInner, CornerInner);
    private void GridLoadingRow(object? sender, DataGridRowEventArgs e) => e.Row.Tag = e.Row.GetIndex() == _sorted.Count - 1 ? "last" : null;

    private void GridSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string key = e.Column.SortMemberPath;
        var direction = _sortKey == key && SortDirection == TableSortDirection.Ascending ? TableSortDirection.Descending : TableSortDirection.Ascending;
        _sortKey = key;
        SetCurrentValue(DefaultSortKeyProperty, key);
        SetCurrentValue(SortDirectionProperty, direction);
        Refresh();
    }

    private void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        if (!Selectable) { _updating = true; _grid!.UnselectAll(); _updating = false; return; }
        Commit(_grid!.SelectedItems.Cast<object>().Select(KeyOf));
    }

    // A click toggles only its own row and Shift extends from the last one; the grid's replace-selection gesture never runs.
    private void GridPressed(object sender, MouseButtonEventArgs e)
    {
        if (!Selectable || FindRow(e.OriginalSource as DependencyObject) is not { } row) return;
        ToggleRow(row, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        row.Focus();
        e.Handled = true;
    }

    private void GridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Selectable && SelectedKeys.Count > 0) { Commit([]); e.Handled = true; return; }
        if (!Selectable || e.OriginalSource is not DataGridRow row) return;
        int index = row.GetIndex();
        switch (e.Key)
        {
            case Key.Space: ToggleRow(row, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; break;
            case Key.Down: FocusRowAt(index + 1); e.Handled = true; break;
            case Key.Up: FocusRowAt(index - 1); e.Handled = true; break;
            case Key.Home: FocusRowAt(0); e.Handled = true; break;
            case Key.End: FocusRowAt(_sorted.Count - 1); e.Handled = true; break;
        }
    }

    private void FocusRowAt(int index)
    {
        if (_grid is null || index < 0 || index >= _sorted.Count) return;
        _grid.ScrollIntoView(_sorted[index]);
        _grid.UpdateLayout();
        (_grid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)?.Focus();
    }

    private static DataGridRow? FindRow(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or Hyperlink) return null;
            if (source is DataGridRow row) return row;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    private void ToggleRow(DataGridRow row, bool extend)
    {
        string key = KeyOf(row.Item);
        var keys = _sorted.Select(KeyOf).ToList();
        var next = new HashSet<string>(SelectedKeys, StringComparer.Ordinal);
        bool on = !next.Contains(key);
        int from = extend && _anchor is not null ? keys.IndexOf(_anchor) : -1, to = keys.IndexOf(key);
        var members = from < 0 ? [key] : keys.GetRange(Math.Min(from, to), Math.Abs(from - to) + 1);
        foreach (string member in members) { if (on) next.Add(member); else next.Remove(member); }
        _anchor = key;
        Commit(next);
    }

    private void SelectAllClicked(object sender, RoutedEventArgs e)
    {
        var keys = _sorted.Select(KeyOf).ToList();
        Commit(keys.Count > 0 && keys.All(SelectedKeys.Contains) ? [] : keys);
        e.Handled = true;
    }

    private void Commit(IEnumerable<string> keys)
    {
        var wanted = new HashSet<string>(keys, StringComparer.Ordinal);
        SelectedKeys.Clear();
        foreach (var row in _sorted) { string key = KeyOf(row); if (wanted.Contains(key)) SelectedKeys.Add(key); }
        ApplySelection();
    }

    private void ApplySelection()
    {
        if (_grid is null) return;
        _updating = true;
        try
        {
            if (!Selectable) _grid.UnselectAll();
            else
            {
                _grid.SelectedItems.Clear();
                foreach (var row in _sorted) if (SelectedKeys.Contains(KeyOf(row))) _grid.SelectedItems.Add(row);
            }
        }
        finally { _updating = false; }
        UpdateSelectionVisual();
    }

    private void ClearClicked(object sender, RoutedEventArgs e) { Commit([]); _selectAll?.Focus(); e.Handled = true; }

    private void UpdateSelectionVisual()
    {
        int count = Selectable ? SelectedKeys.Count : 0, total = _sorted.Count;
        _status!.Text = count > 0 ? $"{count} selected" : $"{total} {(total == 1 ? ItemNameOne : ItemName)}";
        _status.SetResourceReference(TextBlock.ForegroundProperty, count > 0 ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted");
        _clear!.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_selectAll is not null) _selectAll.IsChecked = total > 0 && count == total ? true : count > 0 ? null : false;
    }

    private string KeyOf(object row) => GetValue(row, RowKeyPath)?.ToString() ?? row.GetHashCode().ToString(CultureInfo.InvariantCulture);
    private static object? GetValue(object row, string path)
    {
        object? value = row;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries)) value = value?.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(value);
        return value;
    }
    private sealed class RowComparer(SortableDataTable owner) : IComparer<object>
    {
        public int Compare(object? x, object? y)
        {
            if (string.IsNullOrEmpty(owner._sortKey)) return 0;
            string path = owner.Columns.FirstOrDefault(column => column.Key == owner._sortKey) is { Path.Length: > 0 } column ? column.Path : owner._sortKey;
            var a = GetValue(x!, path); var b = GetValue(y!, path);
            int result = a is IComparable comparable && b is not null && a.GetType() == b.GetType() ? comparable.CompareTo(b) : StringComparer.CurrentCultureIgnoreCase.Compare(a?.ToString(), b?.ToString());
            return owner.SortDirection == TableSortDirection.Descending ? -result : result;
        }
    }
}
