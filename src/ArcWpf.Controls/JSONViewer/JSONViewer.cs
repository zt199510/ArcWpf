using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArcWpf.Controls;

internal enum JsonType { Object, Array, String, Number, Boolean, Null, Other }

/// <summary>What one tree row draws; hangs off <see cref="TreeNode.Data"/>.</summary>
internal sealed class JsonRowInfo
{
    public required string Path { get; init; }
    public object? Name { get; init; }
    public required string RootName { get; init; }
    public object? Value { get; init; }
    public JsonType Type { get; init; }
    public int Count { get; init; }
    public bool Open { get; init; }
    public bool IsMore { get; init; }
    public int Hidden { get; init; }
    public int Page { get; init; }
    public string Needle { get; init; } = string.Empty;
    public bool IsCurrentMatch { get; init; }
    public bool Copyable { get; init; }
    public string? ParentPath { get; init; }
    /// <summary>The row just appeared because its parent opened, so it unfolds once.</summary>
    public bool Entering { get; set; }
    /// <summary>This branch was just toggled, so its chevron turns once.</summary>
    public bool Turning { get; set; }
    public bool IsNode => !IsMore;
}

[TemplatePart(Name = "PART_Search", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_SearchBox", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Tree", Type = typeof(TreeView))]
[TemplatePart(Name = "PART_Toolbar", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Matches", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_ResultCount", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Previous", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_Next", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_ExpandAll", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_CollapseAll", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_PathBar", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Path", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_PathType", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_CopyPath", Type = typeof(JsonActionButton))]
public class JSONViewer : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(object), typeof(JSONViewer), new FrameworkPropertyMetadata(null, DataChanged));
    public static readonly DependencyProperty RootNameProperty = DependencyProperty.Register(nameof(RootName), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata("root", DataChanged));
    public static readonly DependencyProperty DefaultExpandDepthProperty = DependencyProperty.Register(nameof(DefaultExpandDepth), typeof(int), typeof(JSONViewer), new FrameworkPropertyMetadata(1, DataChanged), value => value is int depth && depth >= 0);
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(JSONViewer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ExpandedIdsChanged));
    public static readonly DependencyProperty SearchableProperty = DependencyProperty.Register(nameof(Searchable), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata(string.Empty, QueryChanged));
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(JSONViewer), new FrameworkPropertyMetadata(50, Changed), value => value is int size && size > 0);
    public static readonly DependencyProperty CopyableProperty = DependencyProperty.Register(nameof(Copyable), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static readonly DependencyProperty ShowPathProperty = DependencyProperty.Register(nameof(ShowPath), typeof(bool), typeof(JSONViewer), new FrameworkPropertyMetadata(true, Changed));
    public static new readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(nameof(MaxHeight), typeof(double), typeof(JSONViewer), new FrameworkPropertyMetadata(420d, Changed), value => value is double height && double.IsFinite(height) && height > 0);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata("JSON"));
    private static readonly DependencyPropertyKey SelectedPathPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedPath), typeof(string), typeof(JSONViewer), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty SelectedPathProperty = SelectedPathPropertyKey.DependencyProperty;
    private static readonly DependencyProperty ScrollFlightProperty = DependencyProperty.Register("ScrollFlight", typeof(double), typeof(JSONViewer), new FrameworkPropertyMetadata(0d, (d, e) => ((JSONViewer)d)._tree?.RowsScroll?.ScrollToVerticalOffset((double)e.NewValue)));

    private const double RowHeight = 30, TreePadding = 6;
    private const int MatchLimit = 2000, BulkRows = 400;
    private static readonly Regex Identifier = new(@"^[A-Za-z_$][\w$]*$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions CopyOptions = new() { WriteIndented = true, IndentSize = 2, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private TextBox? _search;
    private FrameworkElement? _searchBox;
    private TreeView? _tree;
    private FrameworkElement? _toolbar, _matches, _pathBar;
    private TextBlock? _path, _pathType, _resultCount;
    private ButtonBase? _previous, _next, _expandAll, _collapseAll;
    private JsonActionButton? _copyPath;
    private readonly OwnedMotion _motion = new();
    private readonly ObservableCollection<string> _expanded = new();
    private ObservableCollection<string>? _watchedExpanded;
    private bool _syncingExpanded, _initialized;
    private object? _root;
    private readonly HashSet<string> _base = new(StringComparer.Ordinal), _closed = new(StringComparer.Ordinal), _lastVisible = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pages = new(StringComparer.Ordinal);
    private readonly List<JsonRowInfo> _rows = new();
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private List<string> _matchList = new();
    private readonly HashSet<string> _matchSet = new(StringComparer.Ordinal), _ancestors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _needed = new(StringComparer.Ordinal);
    private string _needle = string.Empty, _activeRaw = string.Empty, _active = string.Empty;
    private string? _turning;
    private int _matchIndex;

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
        Unloaded += (_, _) => _motion.Stop();
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((JSONViewer)d).Rebuild();
    private static void DataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (JSONViewer)d;
        viewer._initialized = false;
        viewer.Rebuild();
    }

    private static void ExpandedIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (JSONViewer)d;
        if (viewer._watchedExpanded is not null) viewer._watchedExpanded.CollectionChanged -= viewer.ExpandedCollectionChanged;
        viewer._watchedExpanded = e.NewValue as ObservableCollection<string>;
        if (viewer._watchedExpanded is not null) viewer._watchedExpanded.CollectionChanged += viewer.ExpandedCollectionChanged;
        viewer.AdoptExpanded();
    }

    private void ExpandedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => AdoptExpanded();

    private void AdoptExpanded()
    {
        if (_syncingExpanded || !_initialized || ExpandedIds is null) return;
        _base.Clear();
        _base.UnionWith(ExpandedIds);
        Rebuild();
    }

    private static void QueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (JSONViewer)d;
        string previous = viewer._needle;
        viewer._matchIndex = 0;
        viewer.Rebuild();
        if (viewer._needle.Length > 0) { if (viewer._matchList.Count > 0) viewer.MoveTo(viewer._matchList[0], false, true); }
        else if (previous.Length > 0) viewer.MoveTo(viewer._activeRaw, false, false);
    }

    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _search = GetTemplateChild("PART_Search") as TextBox;
        _searchBox = GetTemplateChild("PART_SearchBox") as FrameworkElement;
        _tree = GetTemplateChild("PART_Tree") as TreeView;
        _toolbar = GetTemplateChild("PART_Toolbar") as FrameworkElement;
        _matches = GetTemplateChild("PART_Matches") as FrameworkElement;
        _pathBar = GetTemplateChild("PART_PathBar") as FrameworkElement;
        _path = GetTemplateChild("PART_Path") as TextBlock;
        _pathType = GetTemplateChild("PART_PathType") as TextBlock;
        _resultCount = GetTemplateChild("PART_ResultCount") as TextBlock;
        _previous = GetTemplateChild("PART_Previous") as ButtonBase;
        _next = GetTemplateChild("PART_Next") as ButtonBase;
        _expandAll = GetTemplateChild("PART_ExpandAll") as ButtonBase;
        _collapseAll = GetTemplateChild("PART_CollapseAll") as ButtonBase;
        _copyPath = GetTemplateChild("PART_CopyPath") as JsonActionButton;
        if (_search is not null) { _search.TextChanged += SearchChanged; _search.PreviewKeyDown += SearchKeyDown; }
        if (_searchBox is not null) _searchBox.MouseLeftButtonDown += SearchBoxPressed;
        if (_tree is not null)
        {
            _tree.Selected += TreeSelected;
            _tree.ExpandedChanged += TreeExpandedChanged;
            _tree.CurrentRowMoved += TreeRowMoved;
            _tree.PreviewKeyDown += TreeKeyDown;
            _tree.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(RowActionClicked));
        }
        if (_previous is not null) _previous.Click += PreviousClicked;
        if (_next is not null) _next.Click += NextClicked;
        if (_expandAll is not null) _expandAll.Click += ExpandAllClicked;
        if (_collapseAll is not null) _collapseAll.Click += CollapseAllClicked;
        if (_copyPath is not null) _copyPath.Click += CopyPathClicked;
        Rebuild();
    }

    private void ReleaseParts()
    {
        _motion.Stop();
        if (_search is not null) { _search.TextChanged -= SearchChanged; _search.PreviewKeyDown -= SearchKeyDown; }
        if (_searchBox is not null) _searchBox.MouseLeftButtonDown -= SearchBoxPressed;
        if (_tree is not null)
        {
            _tree.Selected -= TreeSelected;
            _tree.ExpandedChanged -= TreeExpandedChanged;
            _tree.CurrentRowMoved -= TreeRowMoved;
            _tree.PreviewKeyDown -= TreeKeyDown;
            _tree.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(RowActionClicked));
        }
        if (_previous is not null) _previous.Click -= PreviousClicked;
        if (_next is not null) _next.Click -= NextClicked;
        if (_expandAll is not null) _expandAll.Click -= ExpandAllClicked;
        if (_collapseAll is not null) _collapseAll.Click -= CollapseAllClicked;
        if (_copyPath is not null) _copyPath.Click -= CopyPathClicked;
        _search = null; _searchBox = null; _tree = null; _toolbar = null; _matches = null; _pathBar = null; _path = null; _pathType = null; _resultCount = null; _previous = null; _next = null; _expandAll = null; _collapseAll = null; _copyPath = null;
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _root = Normalize(Data);
        _base.Clear(); _closed.Clear(); _pages.Clear(); _lastVisible.Clear();
        if (ExpandedIds is { Count: > 0 } provided && !ReferenceEquals(provided, _expanded)) _base.UnionWith(provided);
        else CollectToDepth(_root, RootName, 0);
        _activeRaw = RootName;
        SyncExpanded();
    }

    private void CollectToDepth(object? value, string id, int level)
    {
        if (level >= DefaultExpandDepth || !IsBranch(TypeOf(value))) return;
        _base.Add(id);
        foreach (var (key, child) in Entries(value)) CollectToDepth(child, ChildPath(id, key), level + 1);
    }

    private void SyncExpanded()
    {
        if (ExpandedIds is null) return;
        _syncingExpanded = true;
        ExpandedIds.Clear();
        foreach (string id in _base) ExpandedIds.Add(id);
        _syncingExpanded = false;
    }

    private void Rebuild()
    {
        if (_tree is null) return;
        Initialize();
        if (_search is not null && _search.Text != Query) _search.Text = Query;
        RunSearch();
        _rows.Clear(); _open.Clear();
        var rootNode = BuildNode(_root, RootName, null, null);
        ResolveActive();
        bool bulk = _rows.Count > BulkRows, animate = _lastVisible.Count > 0 && !bulk && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        foreach (var row in _rows)
        {
            row.Entering = animate && !_lastVisible.Contains(row.Path);
            row.Turning = animate && row.Path == _turning;
        }
        _lastVisible.Clear();
        foreach (var row in _rows) _lastVisible.Add(row.Path);
        _turning = null;
        _tree.Label = Label;
        _tree.MaxHeight = MaxHeight;
        _tree.Load(new ObservableCollection<TreeNode> { rootNode }, _open, _active);
        UpdateChrome();
    }

    private void RunSearch()
    {
        string needle = Query.Trim().ToLowerInvariant();
        if (needle != _needle) { _closed.Clear(); _needle = needle; }
        _matchList = new List<string>(); _matchSet.Clear(); _ancestors.Clear(); _needed.Clear();
        if (needle.Length == 0) return;
        var frames = new List<(string Id, int Index)>();
        void Visit(object? value, string id, object? name)
        {
            if (_matchList.Count >= MatchLimit) return;
            var type = TypeOf(value);
            bool keyHit = name is string text && text.ToLowerInvariant().Contains(needle, StringComparison.Ordinal);
            bool valueHit = !IsBranch(type) && PrimitiveText(value, type).ToLowerInvariant().Contains(needle, StringComparison.Ordinal);
            if (keyHit || valueHit)
            {
                _matchList.Add(id); _matchSet.Add(id);
                foreach (var (frame, index) in frames) { _ancestors.Add(frame); _needed[frame] = Math.Max(_needed.GetValueOrDefault(frame), index + 1); }
            }
            if (!IsBranch(type)) return;
            int position = 0;
            foreach (var (key, child) in Entries(value))
            {
                frames.Add((id, position++));
                Visit(child, ChildPath(id, key), key);
                frames.RemoveAt(frames.Count - 1);
            }
        }
        Visit(_root, RootName, null);
    }

    private bool IsOpen(string id) => _ancestors.Contains(id) ? !_closed.Contains(id) : _base.Contains(id);
    private int LimitOf(string id) => Math.Max(_pages.GetValueOrDefault(id, 1) * PageSize, _needed.GetValueOrDefault(id));

    private TreeNode BuildNode(object? value, string id, object? name, string? parent)
    {
        var type = TypeOf(value);
        bool branch = IsBranch(type), open = branch && IsOpen(id);
        var list = branch ? Entries(value).ToList() : [];
        string? current = _matchList.Count > 0 ? _matchList[Math.Min(_matchIndex, _matchList.Count - 1)] : null;
        var info = new JsonRowInfo { Path = id, Name = name, RootName = RootName, Value = value, Type = type, Count = list.Count, Open = open, Needle = _matchSet.Contains(id) ? _needle : string.Empty, IsCurrentMatch = id == current, Copyable = Copyable, ParentPath = parent };
        var node = new TreeNode { Id = id, Data = info, ForceBranch = branch, Label = Describe(info) };
        _rows.Add(info);
        if (!open) return node;
        _open.Add(id);
        int limit = LimitOf(id), shown = Math.Min(limit, list.Count);
        for (int index = 0; index < shown; index++) node.Children.Add(BuildNode(list[index].Value, ChildPath(id, list[index].Key), list[index].Key, id));
        if (list.Count > shown)
        {
            int hidden = list.Count - shown;
            var more = new JsonRowInfo { Path = $"{id}::more", RootName = RootName, IsMore = true, Hidden = hidden, Page = Math.Min(PageSize, hidden), Type = JsonType.Other, ParentPath = id };
            node.Children.Add(new TreeNode { Id = more.Path, Data = more, Label = Describe(more) });
            _rows.Add(more);
        }
        return node;
    }

    // The current row. When it is folded away, the nearest visible branch that holds it takes over.
    private void ResolveActive()
    {
        if (_rows.Any(row => row.Path == _activeRaw)) _active = _activeRaw;
        else
        {
            string best = RootName;
            foreach (var row in _rows) if (row.IsNode && Within(row.Path, _activeRaw) && row.Path.Length > best.Length) best = row.Path;
            _active = best;
        }
        var current = _rows.FirstOrDefault(row => row.Path == _active);
        SetValue(SelectedPathPropertyKey, current is null ? string.Empty : current.IsMore ? current.ParentPath ?? string.Empty : current.Path);
    }

    private void UpdateChrome()
    {
        if (_toolbar is not null) _toolbar.Visibility = Searchable || _rows.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (_searchBox is not null) _searchBox.Visibility = Searchable ? Visibility.Visible : Visibility.Hidden;
        bool searching = _needle.Length > 0;
        if (_matches is not null && _matches.Visibility == Visibility.Visible != searching)
        {
            _matches.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            if (searching && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
            {
                _matches.Opacity = 0;
                _motion.Double(_matches, OpacityProperty, 1, Tween.Fast, true);
            }
        }
        int count = _matchList.Count;
        if (_resultCount is not null) _resultCount.Text = searching ? count > 0 ? $"{Math.Min(_matchIndex, count - 1) + 1}/{count}" : "0/0" : string.Empty;
        if (_previous is not null) _previous.IsEnabled = count > 0;
        if (_next is not null) _next.IsEnabled = count > 0;
        var current = _rows.FirstOrDefault(row => row.Path == _active);
        if (_pathBar is not null) _pathBar.Visibility = ShowPath && current is not null ? Visibility.Visible : Visibility.Collapsed;
        if (current is null) return;
        if (_path is not null) { _path.Text = current.IsNode ? current.Path : current.ParentPath ?? string.Empty; _path.ToolTip = _path.Text; }
        if (_pathType is not null) _pathType.Text = current.IsMore ? "page" : IsBranch(current.Type) ? $"{TypeName(current.Type)}, {CountLabel(current)}" : TypeName(current.Type);
        if (_copyPath is not null) _copyPath.Visibility = Copyable && current.IsNode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MoveTo(string id, bool focus, bool center)
    {
        _activeRaw = id;
        ResolveActive();
        _tree?.Select(_active);
        UpdateChrome();
        Reveal(center);
        if (focus) _tree?.FocusNode(_active);
    }

    // Scrolling keeps the current row in view, springing to where it will sit once the rows have settled.
    private void Reveal(bool center)
    {
        if (_tree is null) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_tree?.RowsScroll is not { } scroll) return;
            int index = _rows.FindIndex(row => row.Path == _active);
            if (index < 0) return;
            double top = index * RowHeight + TreePadding, bottom = top + RowHeight, view = scroll.ViewportHeight, now = scroll.VerticalOffset, target = now;
            if (center) target = top - view / 2 + RowHeight / 2;
            else if (top < now + 4) target = top - 4;
            else if (bottom > now + view - 4) target = bottom - view + 4;
            target = Math.Clamp(target, 0, Math.Max(0, scroll.ExtentHeight - view));
            if (Math.Abs(target - now) < 1) return;
            bool motion = IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
            if (!motion) { scroll.ScrollToVerticalOffset(target); return; }
            SetValue(ScrollFlightProperty, now);
            _motion.Double(this, ScrollFlightProperty, target, Tween.Smooth, true, spring: true);
        }));
    }

    private void Toggle(string id, bool want)
    {
        if (IsOpen(id) == want) return;
        _turning = id;
        if (_ancestors.Contains(id))
        {
            if (want) _closed.Remove(id); else _closed.Add(id);
            if (want && _base.Add(id)) SyncExpanded();
            return;
        }
        if (want) _base.Add(id); else _base.Remove(id);
        SyncExpanded();
    }

    public void ExpandAll()
    {
        Initialize();
        void Visit(object? value, string id)
        {
            if (!IsBranch(TypeOf(value))) return;
            _base.Add(id);
            foreach (var (key, child) in Entries(value)) Visit(child, ChildPath(id, key));
        }
        Visit(_root, RootName);
        SyncExpanded();
        _closed.Clear();
        Rebuild();
        Reveal(false);
    }

    public void CollapseAll()
    {
        Initialize();
        _base.Clear();
        if (IsBranch(TypeOf(_root))) _base.Add(RootName);
        SyncExpanded();
        _closed.Clear();
        _closed.UnionWith(_ancestors.Where(id => id != RootName));
        _pages.Clear();
        _activeRaw = RootName;
        Rebuild();
        Reveal(false);
    }

    private void ShowMore(string parent)
    {
        _pages[parent] = (int)Math.Ceiling(LimitOf(parent) / (double)PageSize) + 1;
    }

    private void StepMatch(int step)
    {
        int count = _matchList.Count;
        if (count == 0) return;
        _matchIndex = ((Math.Min(_matchIndex, count - 1) + step) % count + count) % count;
        Rebuild();
        MoveTo(_matchList[_matchIndex], false, true);
    }

    private void TreeSelected(object? sender, TreeNodeEventArgs e)
    {
        if (e.Node.Data is not JsonRowInfo info) return;
        _activeRaw = info.Path;
        if (info.IsMore) { ShowMore(info.ParentPath!); Rebuild(); Reveal(false); return; }
        ResolveActive();
        UpdateChrome();
        Reveal(false);
    }

    private void TreeExpandedChanged(object? sender, TreeExpandedChangedEventArgs e)
    {
        var next = new HashSet<string>(e.ExpandedIds, StringComparer.Ordinal);
        foreach (string id in next.Except(_open).ToList()) Toggle(id, true);
        foreach (string id in _open.Except(next).ToList()) Toggle(id, false);
        Rebuild();
    }

    private void TreeRowMoved(string id)
    {
        if (id == _active) return;
        _activeRaw = id;
        ResolveActive();
        _tree?.Select(_active);
        UpdateChrome();
        Reveal(false);
    }

    private void TreeKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.C && modifiers.HasFlag(ModifierKeys.Control))
        {
            if (modifiers.HasFlag(ModifierKeys.Shift)) CopyPath(); else CopyValue();
            e.Handled = true;
        }
        else if (e.Key == Key.Oem2 && modifiers == ModifierKeys.None && Searchable && _search is not null)
        {
            _search.Focus();
            _search.SelectAll();
            e.Handled = true;
        }
    }

    private void SearchChanged(object sender, TextChangedEventArgs e) { if (Query != _search!.Text) SetCurrentValue(QueryProperty, _search.Text); }

    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: StepMatch(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1); e.Handled = true; break;
            case Key.Escape when Query.Length > 0: SetCurrentValue(QueryProperty, string.Empty); e.Handled = true; break;
            case Key.Down when !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt): _tree?.FocusNode(_active); e.Handled = true; break;
        }
    }

    private void SearchBoxPressed(object sender, MouseButtonEventArgs e) { _search?.Focus(); }
    private void PreviousClicked(object sender, RoutedEventArgs e) => StepMatch(-1);
    private void NextClicked(object sender, RoutedEventArgs e) => StepMatch(1);
    private void ExpandAllClicked(object sender, RoutedEventArgs e) => ExpandAll();
    private void CollapseAllClicked(object sender, RoutedEventArgs e) => CollapseAll();

    private void RowActionClicked(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not JsonActionButton { DataContext: TreeRow { Node.Data: JsonRowInfo info } } action) return;
        Flash(action, () => Clipboard.SetText(action.Kind == "path" ? info.Path : CopyText(info.Value, info.Type)));
        e.Handled = true;
    }

    private void CopyPathClicked(object sender, RoutedEventArgs e) { Flash(_copyPath!, () => Clipboard.SetText(SelectedPath)); e.Handled = true; }

    private static void Flash(JsonActionButton action, Action copy)
    {
        try { copy(); action.Flash(true); }
        catch (System.Runtime.InteropServices.ExternalException) { action.Flash(false); }
    }

    public void CopyValue()
    {
        if (_rows.FirstOrDefault(row => row.Path == _active) is { IsNode: true } row) Clipboard.SetText(CopyText(row.Value, row.Type));
    }

    public void CopyPath() { if (!string.IsNullOrEmpty(SelectedPath)) Clipboard.SetText(SelectedPath); }

    internal static string CountLabel(JsonRowInfo row) => row.Type == JsonType.Array ? Plural(row.Count, "item", "items") : Plural(row.Count, "key", "keys");
    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    private static string TypeName(JsonType type) => type.ToString().ToLowerInvariant();
    private static bool Within(string ancestor, string id) => id == ancestor || id.StartsWith(ancestor + ".", StringComparison.Ordinal) || id.StartsWith(ancestor + "[", StringComparison.Ordinal);
    internal static bool IsBranch(JsonType type) => type is JsonType.Object or JsonType.Array;
    private static string Describe(JsonRowInfo row)
    {
        if (row.IsMore) return $"Show {row.Page} more, {row.Hidden} hidden";
        string name = row.Name is null ? row.RootName : Convert.ToString(row.Name, CultureInfo.InvariantCulture)!;
        return IsBranch(row.Type) ? $"{name}, {TypeName(row.Type)}, {CountLabel(row)}" : $"{name}: {PrimitiveText(row.Value, row.Type)}";
    }

    internal static string PrimitiveText(object? value, JsonType type) => type switch
    {
        JsonType.String => (string)value!,
        JsonType.Null => "null",
        JsonType.Boolean => (bool)value! ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
    private static string CopyText(object? value, JsonType type) => type == JsonType.String ? (string)value! : IsBranch(type) ? JsonSerializer.Serialize(value, CopyOptions) : PrimitiveText(value, type);
    private static string ChildPath(string parent, object key) => key is int index ? $"{parent}[{index.ToString(CultureInfo.InvariantCulture)}]" : Identifier.IsMatch((string)key) ? $"{parent}.{key}" : $"{parent}[{JsonSerializer.Serialize((string)key, CopyOptions)}]";

    private static JsonType TypeOf(object? value) => value switch
    {
        null => JsonType.Null,
        List<object?> => JsonType.Array,
        Dictionary<string, object?> => JsonType.Object,
        string => JsonType.String,
        bool => JsonType.Boolean,
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => JsonType.Number,
        _ => JsonType.Other,
    };

    private static IEnumerable<(object Key, object? Value)> Entries(object? value) => value switch
    {
        List<object?> list => list.Select((item, index) => ((object)index, item)),
        Dictionary<string, object?> map => map.Select(entry => ((object)entry.Key, entry.Value)),
        _ => [],
    };

    // The tree only ever sees plain maps, lists and primitives, whatever the caller passed in.
    private static object? Normalize(object? value)
    {
        switch (value)
        {
            case null or string or bool: return value;
            case JsonElement element: return FromElement(element);
            case IDictionary<string, object?> map: return map.ToDictionary(entry => entry.Key, entry => Normalize(entry.Value), StringComparer.Ordinal);
            case IDictionary dictionary:
                var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary) copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!] = Normalize(entry.Value);
                return copy;
            case IEnumerable items: return items.Cast<object?>().Select(Normalize).ToList();
            default: return value;
        }
    }

    private static object? FromElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => FromElement(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(FromElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long whole) ? whole : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
