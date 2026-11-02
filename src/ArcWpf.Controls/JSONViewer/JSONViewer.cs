using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Search", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Tree", Type = typeof(TreeView))]
[TemplatePart(Name = "PART_Path", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_CopyValue", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_CopyPath", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_ResultCount", Type = typeof(TextBlock))]
public class JSONViewer : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(object), typeof(JSONViewer), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty RootNameProperty = DependencyProperty.Register(nameof(RootName), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata("root", Changed));
    public static readonly DependencyProperty DefaultExpandDepthProperty = DependencyProperty.Register(nameof(DefaultExpandDepth), typeof(int), typeof(JSONViewer), new FrameworkPropertyMetadata(1, Changed), value => value is int depth && depth >= 0);
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(JSONViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty SearchableProperty = DependencyProperty.Register(nameof(Searchable), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(JSONViewer), new FrameworkPropertyMetadata(50), value => value is int size && size > 0);
    public static readonly DependencyProperty CopyableProperty = DependencyProperty.Register(nameof(Copyable), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static readonly DependencyProperty ShowPathProperty = DependencyProperty.Register(nameof(ShowPath), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static new readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(nameof(MaxHeight), typeof(double), typeof(JSONViewer), new FrameworkPropertyMetadata(420d, Changed), value => value is double height && double.IsFinite(height) && height > 0);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata("JSON"));
    private static readonly DependencyPropertyKey SelectedPathPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedPath), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty SelectedPathProperty = SelectedPathPropertyKey.DependencyProperty;

    private TextBox? _search;
    private TreeView? _tree;
    private TextBlock? _path;
    private System.Windows.Controls.Button? _copyValue;
    private System.Windows.Controls.Button? _copyPath;
    private TextBlock? _resultCount;
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    private readonly ObservableCollection<string> _expanded = new();
    private string _selected = string.Empty;

    public object? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string RootName { get => (string)GetValue(RootNameProperty); set => SetValue(RootNameProperty, value); }
    public int DefaultExpandDepth { get => (int)GetValue(DefaultExpandDepthProperty); set => SetValue(DefaultExpandDepthProperty, value); }
    public ObservableCollection<string> ExpandedIds { get => (ObservableCollection<string>)GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }
    public bool Searchable { get => (bool)GetValue(SearchableProperty); set => SetValue(SearchableProperty, value); }
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
    public bool Copyable { get => (bool)GetValue(CopyableProperty); set => SetValue(CopyableProperty, value); }
    public bool ShowPath { get => (bool)GetValue(ShowPathProperty); set => SetValue(ShowPathProperty, value); }
    public new double MaxHeight { get => (double)GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string SelectedPath => (string)GetValue(SelectedPathProperty);

    static JSONViewer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(JSONViewer), new FrameworkPropertyMetadata(typeof(JSONViewer)));
    public JSONViewer()
    {
        SetCurrentValue(ExpandedIdsProperty, _expanded);
        AutomationProperties.SetName(this, Label);
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => { if (_tree is not null) _tree.Selected -= TreeSelected; };
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((JSONViewer)d).Rebuild();

    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _search = GetTemplateChild("PART_Search") as TextBox;
        _tree = GetTemplateChild("PART_Tree") as TreeView;
        _path = GetTemplateChild("PART_Path") as TextBlock;
        _copyValue = GetTemplateChild("PART_CopyValue") as System.Windows.Controls.Button;
        _copyPath = GetTemplateChild("PART_CopyPath") as System.Windows.Controls.Button;
        _resultCount = GetTemplateChild("PART_ResultCount") as TextBlock;
        if (_search is not null) _search.TextChanged += SearchChanged;
        if (_tree is not null) _tree.Selected += TreeSelected;
        if (_copyValue is not null) _copyValue.Click += CopyValueClicked;
        if (_copyPath is not null) _copyPath.Click += CopyPathClicked;
        Rebuild();
    }

    private void ReleaseParts()
    {
        if (_search is not null) _search.TextChanged -= SearchChanged;
        if (_tree is not null) _tree.Selected -= TreeSelected;
        if (_copyValue is not null) _copyValue.Click -= CopyValueClicked;
        if (_copyPath is not null) _copyPath.Click -= CopyPathClicked;
        _search = null; _tree = null; _path = null; _copyValue = null; _copyPath = null; _resultCount = null;
    }

    private void Rebuild()
    {
        if (_tree is null) return;
        if (_search is not null && _search.Text != Query) _search.Text = Query;
        _values.Clear(); _labels.Clear();
        var root = BuildNode(Data, RootName, 0, null);
        if (string.IsNullOrEmpty(_selected)) { _selected = RootName; SetValue(SelectedPathPropertyKey, _selected); }
        var roots = new ObservableCollection<TreeNode> { root };
        _expanded.Clear();
        AddDefaultExpanded(Data, RootName, 0);
        var query = Query.Trim();
        var matches = FindMatches(Data, RootName, query);
        foreach (var path in matches) AddAncestors(path);
        if (ExpandedIds is not null && !ReferenceEquals(ExpandedIds, _expanded))
        {
            ExpandedIds.Clear();
            foreach (var id in _expanded.Distinct(StringComparer.Ordinal)) ExpandedIds.Add(id);
        }
        _tree.DefaultExpandedIds = new ObservableCollection<string>(_expanded);
        _tree.Nodes = roots;
        _tree.Label = Label;
        _resultCount!.Text = string.IsNullOrEmpty(query) ? string.Empty : $"{matches.Count} matches";
        _path!.Visibility = ShowPath && !string.IsNullOrEmpty(_selected) ? Visibility.Visible : Visibility.Collapsed;
        _path.Text = _selected;
        _copyValue!.Visibility = Copyable && !string.IsNullOrEmpty(_selected) ? Visibility.Visible : Visibility.Collapsed;
        _copyPath!.Visibility = Copyable && !string.IsNullOrEmpty(_selected) ? Visibility.Visible : Visibility.Collapsed;
    }

    private TreeNode BuildNode(object? value, string path, int depth, string? name)
    {
        var type = TypeName(value);
        _values[path] = value;
        _labels[path] = name is null ? RootName : name;
        var node = new TreeNode { Id = path, Label = FormatLabel(name, value, type), IsEnabled = true };
        if (value is IDictionary<string, object?> dictionary)
            foreach (var entry in dictionary) node.Children.Add(BuildNode(entry.Value, ChildPath(path, entry.Key), depth + 1, entry.Key));
        else if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) node.Children.Add(BuildNode(ToObject(property.Value), ChildPath(path, property.Name), depth + 1, property.Name));
        else if (value is IEnumerable<object?> list)
            for (int index = 0; index < list.Count(); index++) node.Children.Add(BuildNode(list.ElementAt(index), $"{path}[{index}]", depth + 1, index.ToString(CultureInfo.InvariantCulture)));
        return node;
    }

    private void AddDefaultExpanded(object? value, string path, int depth)
    {
        if (!IsBranch(value) || depth >= DefaultExpandDepth) return;
        _expanded.Add(path);
        foreach (var (key, child) in Entries(value)) AddDefaultExpanded(child, ChildPath(path, key), depth + 1);
    }
    private void AddAncestors(string path)
    {
        int dot = path.LastIndexOfAny(new[] { '.', '[' });
        while (dot > RootName.Length)
        {
            var parent = path[..dot];
            if (IsBranch(_values.GetValueOrDefault(parent))) _expanded.Add(parent);
            dot = parent.LastIndexOfAny(new[] { '.', '[' });
        }
        if (IsBranch(Data)) _expanded.Add(RootName);
    }

    private List<string> FindMatches(object? value, string path, string query)
    {
        var matches = new List<string>(); if (string.IsNullOrEmpty(query)) return matches;
        foreach (var (key, child) in Entries(value))
        {
            string childPath = ChildPath(path, key);
            if (key.ToString()!.Contains(query, StringComparison.OrdinalIgnoreCase) || PrimitiveText(child).Contains(query, StringComparison.OrdinalIgnoreCase)) matches.Add(childPath);
            matches.AddRange(FindMatches(child, childPath, query));
        }
        return matches;
    }

    private void TreeSelected(object? sender, TreeNodeEventArgs e)
    {
        _selected = e.Node.Id;
        SetValue(SelectedPathPropertyKey, _selected);
        _path!.Text = _selected;
        _path.Visibility = ShowPath ? Visibility.Visible : Visibility.Collapsed;
        _copyValue!.Visibility = Copyable ? Visibility.Visible : Visibility.Collapsed;
        _copyPath!.Visibility = Copyable ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (Query != _search!.Text) SetCurrentValue(QueryProperty, _search.Text); }
    private void CopyValueClicked(object sender, RoutedEventArgs e) { CopyValue(); e.Handled = true; }
    private void CopyPathClicked(object sender, RoutedEventArgs e) { CopyPath(); e.Handled = true; }
    public void CopyValue() { if (_values.TryGetValue(_selected, out var value)) Clipboard.SetText(FormatCopy(value)); }
    public void CopyPath() { if (!string.IsNullOrEmpty(_selected)) Clipboard.SetText(_selected); }
    public void ExpandAll() { _expanded.Clear(); foreach (var key in _values.Keys.Where(key => IsBranch(_values[key]))) _expanded.Add(key); Rebuild(); }
    public void CollapseAll() { _expanded.Clear(); Rebuild(); }

    private static string FormatLabel(string? name, object? value, string type) => $"{name ?? "root"}: {type}{(IsBranch(value) ? $" ({Entries(value).Count()}" + (type == "array" ? " items)" : " keys)") : $" = {PrimitiveText(value)}")}";
    private static string TypeName(object? value) => value is null ? "null" : value is IEnumerable<object?> ? "array" : value is IDictionary<string, object?> ? "object" : value is bool ? "boolean" : value is string ? "string" : value is JsonElement e ? e.ValueKind.ToString().ToLowerInvariant() : "number";
    private static bool IsBranch(object? value) => value is IDictionary<string, object?> || value is IEnumerable<object?> || value is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array };
    private static string PrimitiveText(object? value) => value is null ? "null" : value is string text ? text : value.ToString() ?? string.Empty;
    private static string FormatCopy(object? value) => value is string text ? text : JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
    private static string ChildPath(string parent, object key) => key is string name && name.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '$') ? $"{parent}.{name}" : $"{parent}[{JsonSerializer.Serialize(key)}]";
    private static object? ToObject(JsonElement element) => element.ValueKind switch { JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)), JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToList(), JsonValueKind.String => element.GetString(), JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(), JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
    private static IEnumerable<(object key, object? value)> Entries(object? value)
    {
        if (value is IDictionary<string, object?> dictionary) return dictionary.Select(item => ((object)item.Key, item.Value));
        if (value is IEnumerable<object?> list) return list.Select((item, index) => ((object)index, item));
        if (value is JsonElement element) return Entries(ToObject(element));
        return [];
    }
}
