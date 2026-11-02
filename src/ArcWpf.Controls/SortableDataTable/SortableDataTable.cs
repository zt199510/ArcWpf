using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ArcWpf.Controls;

public enum TableSortDirection { Ascending, Descending }

public sealed class DataTableColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Sortable { get; set; } = true;
    public bool Numeric { get; set; }
    public DataGridLength Width { get; set; } = DataGridLength.SizeToHeader;
}

[TemplatePart(Name = "PART_DataGrid", Type = typeof(DataGrid))]
[TemplatePart(Name = "PART_EmptyState", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_SelectionStatus", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_ClearSelection", Type = typeof(System.Windows.Controls.Button))]
public class SortableDataTable : Control
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IEnumerable<object>), typeof(SortableDataTable), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty RowKeyPathProperty = DependencyProperty.Register(nameof(RowKeyPath), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("Id", Changed));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("Data table", Changed));
    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("No rows to show", Changed));
    public static readonly DependencyProperty DefaultSortKeyProperty = DependencyProperty.Register(nameof(DefaultSortKey), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(nameof(SortDirection), typeof(TableSortDirection), typeof(SortableDataTable), new FrameworkPropertyMetadata(TableSortDirection.Ascending, Changed));
    public static readonly DependencyProperty SelectableProperty = DependencyProperty.Register(nameof(Selectable), typeof(bool), typeof(SortableDataTable), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty SelectedKeysProperty = DependencyProperty.Register(nameof(SelectedKeys), typeof(ObservableCollection<string>), typeof(SortableDataTable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty ItemNameProperty = DependencyProperty.Register(nameof(ItemName), typeof(string), typeof(SortableDataTable), new FrameworkPropertyMetadata("rows", Changed));

    private DataGrid? _grid;
    private TextBlock? _empty;
    private TextBlock? _status;
    private System.Windows.Controls.Button? _clear;
    private bool _updating;
    private readonly ObservableCollection<object> _sorted = new();
    private string _sortKey = string.Empty;

    public IEnumerable<object>? Rows { get => (IEnumerable<object>?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public string RowKeyPath { get => (string)GetValue(RowKeyPathProperty); set => SetValue(RowKeyPathProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }
    public string DefaultSortKey { get => (string)GetValue(DefaultSortKeyProperty); set => SetValue(DefaultSortKeyProperty, value); }
    public TableSortDirection SortDirection { get => (TableSortDirection)GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }
    public bool Selectable { get => (bool)GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }
    public ObservableCollection<string> SelectedKeys { get => (ObservableCollection<string>)GetValue(SelectedKeysProperty); set => SetValue(SelectedKeysProperty, value); }
    public string ItemName { get => (string)GetValue(ItemNameProperty); set => SetValue(ItemNameProperty, value); }

    static SortableDataTable() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SortableDataTable), new FrameworkPropertyMetadata(typeof(SortableDataTable)));
    public SortableDataTable()
    {
        SetCurrentValue(SelectedKeysProperty, new ObservableCollection<string>());
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => ReleaseGrid();
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SortableDataTable)d).Refresh();

    public override void OnApplyTemplate()
    {
        ReleaseGrid(); base.OnApplyTemplate();
        _grid = GetTemplateChild("PART_DataGrid") as DataGrid;
        _empty = GetTemplateChild("PART_EmptyState") as TextBlock;
        _status = GetTemplateChild("PART_SelectionStatus") as TextBlock;
        _clear = GetTemplateChild("PART_ClearSelection") as System.Windows.Controls.Button;
        if (_grid is not null)
        {
            _grid.Sorting += GridSorting;
            _grid.SelectionChanged += GridSelectionChanged;
            _grid.SelectionMode = DataGridSelectionMode.Extended;
            _grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        }
        if (_clear is not null) _clear.Click += ClearClicked;
        Refresh();
    }
    private void ReleaseGrid()
    {
        if (_grid is not null) { _grid.Sorting -= GridSorting; _grid.SelectionChanged -= GridSelectionChanged; }
        if (_clear is not null) _clear.Click -= ClearClicked;
        _grid = null; _empty = null; _status = null; _clear = null;
    }
    private void Refresh()
    {
        if (_grid is null) return;
        _sortKey = string.IsNullOrEmpty(_sortKey) ? DefaultSortKey : _sortKey;
        _grid.Columns.Clear();
        foreach (var column in Columns)
        {
            var binding = new Binding(string.IsNullOrEmpty(column.Path) ? column.Key : column.Path) { Mode = BindingMode.OneWay };
            var dataColumn = new DataGridTextColumn { Header = column.Header, Binding = binding, SortMemberPath = column.Key, Width = column.Width, CanUserSort = column.Sortable };
            _grid.Columns.Add(dataColumn);
        }
        _sorted.Clear();
        foreach (var row in (Rows ?? []).OrderBy(row => row, new RowComparer(this))) _sorted.Add(row);
        _grid.ItemsSource = null; _grid.ItemsSource = _sorted;
        _grid.Visibility = _sorted.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _empty!.Text = EmptyMessage; _empty.Visibility = _sorted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _grid.SelectionMode = Selectable ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;
        UpdateSelectionVisual();
    }
    public ObservableCollection<DataTableColumn> Columns { get; } = new();
    private void GridSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string key = e.Column.SortMemberPath;
        var direction = _sortKey == key && SortDirection == TableSortDirection.Ascending ? TableSortDirection.Descending : TableSortDirection.Ascending;
        _sortKey = key; SetCurrentValue(DefaultSortKeyProperty, key); SetCurrentValue(SortDirectionProperty, direction); Refresh();
    }
    private void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || !Selectable) return;
        _updating = true;
        try
        {
            SelectedKeys.Clear();
            foreach (var row in _grid!.SelectedItems.Cast<object>()) SelectedKeys.Add(KeyOf(row));
            UpdateSelectionVisual();
        }
        finally { _updating = false; }
    }
    private void ClearClicked(object sender, RoutedEventArgs e) { _grid?.UnselectAll(); SelectedKeys.Clear(); UpdateSelectionVisual(); e.Handled = true; }
    private void UpdateSelectionVisual()
    {
        int count = SelectedKeys.Count;
        _status!.Text = count == 0 ? string.Empty : $"{count} {ItemName}";
        _clear!.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
            var a = GetValue(x!, owner._sortKey); var b = GetValue(y!, owner._sortKey);
            int result = a is IComparable comparable && b is not null ? comparable.CompareTo(b) : StringComparer.CurrentCultureIgnoreCase.Compare(a?.ToString(), b?.ToString());
            return owner.SortDirection == TableSortDirection.Descending ? -result : result;
        }
    }
}
