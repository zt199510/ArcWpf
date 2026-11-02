using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum TreeTableSortDirection { Ascending, Descending }

public sealed class TreeTableRow
{
    public string Id { get; set; } = string.Empty;
    public object? Data { get; set; }
    public ObservableCollection<TreeTableRow> Children { get; } = new();
    public bool HasChildren { get; set; }
}

public sealed class TreeTableColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Sortable { get; set; } = true;
    public bool Numeric { get; set; }
    public DataGridLength Width { get; set; } = DataGridLength.SizeToHeader;
}

[TemplatePart(Name = "PART_Header", Type = typeof(Grid))]
[TemplatePart(Name = "PART_Body", Type = typeof(StackPanel))]
public class TreeTable : Control
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<TreeTableRow>), typeof(TreeTable), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(TreeTable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty SelectedIdsProperty = DependencyProperty.Register(nameof(SelectedIds), typeof(ObservableCollection<string>), typeof(TreeTable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));
    public static readonly DependencyProperty SelectableProperty = DependencyProperty.Register(nameof(Selectable), typeof(bool), typeof(TreeTable), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty SortColumnProperty = DependencyProperty.Register(nameof(SortColumn), typeof(string), typeof(TreeTable), new FrameworkPropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(nameof(SortDirection), typeof(TreeTableSortDirection), typeof(TreeTable), new FrameworkPropertyMetadata(TreeTableSortDirection.Ascending, Changed));
    public static new readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(nameof(MaxHeight), typeof(double), typeof(TreeTable), new FrameworkPropertyMetadata(480d, Changed));
    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(TreeTable), new FrameworkPropertyMetadata("Nothing to show", Changed));

    private Grid? _header;
    private StackPanel? _body;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    public ObservableCollection<TreeTableRow> Rows { get => (ObservableCollection<TreeTableRow>)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public ObservableCollection<TreeTableColumn> Columns { get; } = new();
    public ObservableCollection<string> ExpandedIds { get => (ObservableCollection<string>)GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }
    public ObservableCollection<string> SelectedIds { get => (ObservableCollection<string>)GetValue(SelectedIdsProperty); set => SetValue(SelectedIdsProperty, value); }
    public bool Selectable { get => (bool)GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }
    public string SortColumn { get => (string)GetValue(SortColumnProperty); set => SetValue(SortColumnProperty, value); }
    public TreeTableSortDirection SortDirection { get => (TreeTableSortDirection)GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }
    public new double MaxHeight { get => (double)GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    public string EmptyMessage { get => (string)GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    static TreeTable() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeTable), new FrameworkPropertyMetadata(typeof(TreeTable)));
    public TreeTable()
    {
        SetCurrentValue(RowsProperty, new ObservableCollection<TreeTableRow>());
        SetCurrentValue(ExpandedIdsProperty, new ObservableCollection<string>());
        SetCurrentValue(SelectedIdsProperty, new ObservableCollection<string>());
        Loaded += (_, _) => Rebuild();
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TreeTable)d).Rebuild();
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _header = GetTemplateChild("PART_Header") as Grid;
        _body = GetTemplateChild("PART_Body") as StackPanel;
        Rebuild();
    }
    public void Toggle(string id) { if (_expanded.Contains(id)) _expanded.Remove(id); else _expanded.Add(id); SyncExpanded(); Rebuild(); }

    private void Rebuild()
    {
        if (_header is null || _body is null) return;
        _expanded.Clear(); foreach (var id in ExpandedIds) _expanded.Add(id);
        _selected.Clear(); foreach (var id in SelectedIds) _selected.Add(id);
        _header.Children.Clear(); _body.Children.Clear();
        BuildHeader();
        if (Rows.Count == 0) { _body.Children.Add(new TextBlock { Text = EmptyMessage, Margin = new Thickness(16), Foreground = FindBrush("ArcWpf.Brush.TextSecondary") }); return; }
        foreach (var row in Ordered(Rows)) AddRow(row, 0);
    }
    private void BuildHeader()
    {
        _header!.ColumnDefinitions.Clear();
        if (Selectable) _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        foreach (var column in Columns) _header.ColumnDefinitions.Add(new ColumnDefinition { Width = ToGridLength(column.Width) });
        int offset = Selectable ? 1 : 0;
        for (int index = 0; index < Columns.Count; index++)
        {
            var column = Columns[index]; var button = new System.Windows.Controls.Button { Content = column.Header, BorderThickness = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8, 10, 8), IsEnabled = column.Sortable };
            button.Click += (_, _) => { if (SortColumn == column.Key) SortDirection = SortDirection == TreeTableSortDirection.Ascending ? TreeTableSortDirection.Descending : TreeTableSortDirection.Ascending; else { SortColumn = column.Key; SortDirection = TreeTableSortDirection.Ascending; } };
            Grid.SetColumn(button, index + offset); _header.Children.Add(button);
        }
    }
    private void AddRow(TreeTableRow row, int depth)
    {
        var grid = new Grid { MinHeight = 40, Background = _selected.Contains(row.Id) ? FindBrush("ArcWpf.Brush.SurfaceMuted") : Brushes.Transparent, Focusable = true, Tag = row };
        if (Selectable) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        foreach (var column in Columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = ToGridLength(column.Width) });
        int offset = Selectable ? 1 : 0;
        if (Selectable)
        {
            var check = new CheckBox { IsChecked = _selected.Contains(row.Id), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            AutomationProperties.SetName(check, row.Id);
            check.Checked += (_, _) => Select(row, true); check.Unchecked += (_, _) => Select(row, false); Grid.SetColumn(check, 0); grid.Children.Add(check);
        }
        for (int index = 0; index < Columns.Count; index++)
        {
            var value = GetValue(row.Data, Columns[index].Path.Length == 0 ? Columns[index].Key : Columns[index].Path)?.ToString() ?? string.Empty;
            var cell = new TextBlock { Text = value, Margin = new Thickness(index == 0 ? 10 + depth * 20 : 10, 8, 10, 8), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            if (index == 0 && (row.Children.Count > 0 || row.HasChildren))
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                var toggle = new System.Windows.Controls.Button { Content = _expanded.Contains(row.Id) ? "⌄" : "›", Width = 20, Height = 22, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
                toggle.Click += (_, _) => Toggle(row.Id); panel.Children.Add(toggle); panel.Children.Add(cell); Grid.SetColumn(panel, index + offset); grid.Children.Add(panel);
            }
            else { Grid.SetColumn(cell, index + offset); grid.Children.Add(cell); }
        }
        grid.MouseLeftButtonUp += (_, _) => { if (!Selectable && row.Children.Count == 0) return; if (row.Children.Count > 0 || row.HasChildren) Toggle(row.Id); else Select(row, !_selected.Contains(row.Id)); };
        _body!.Children.Add(grid);
        if (_expanded.Contains(row.Id)) foreach (var child in Ordered(row.Children)) AddRow(child, depth + 1);
    }
    private void Select(TreeTableRow row, bool value) { if (value) _selected.Add(row.Id); else _selected.Remove(row.Id); SyncSelected(); Rebuild(); }
    private void SyncExpanded() { ExpandedIds.Clear(); foreach (var id in _expanded) ExpandedIds.Add(id); }
    private void SyncSelected() { SelectedIds.Clear(); foreach (var id in _selected) SelectedIds.Add(id); }
    private IEnumerable<TreeTableRow> Ordered(IEnumerable<TreeTableRow> rows)
    {
        if (string.IsNullOrEmpty(SortColumn)) return rows;
        var column = Columns.FirstOrDefault(item => item.Key == SortColumn); if (column is null) return rows;
        return SortDirection == TreeTableSortDirection.Ascending ? rows.OrderBy(row => GetValue(row.Data, column.Path.Length == 0 ? column.Key : column.Path)?.ToString(), StringComparer.CurrentCultureIgnoreCase) : rows.OrderByDescending(row => GetValue(row.Data, column.Path.Length == 0 ? column.Key : column.Path)?.ToString(), StringComparer.CurrentCultureIgnoreCase);
    }
    private static object? GetValue(object? row, string path) { object? value = row; foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries)) value = value?.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(value); return value; }
    private Brush FindBrush(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;
    private static GridLength ToGridLength(DataGridLength width) => width.IsAbsolute ? new GridLength(width.Value, GridUnitType.Pixel) : new GridLength(1, GridUnitType.Star);
}
