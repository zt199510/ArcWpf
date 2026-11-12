using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>Fetches the children of a lazy branch; it may run off the UI thread and should honour the token.</summary>
public delegate Task<IEnumerable<TreeNode>> TreeChildrenLoader(TreeNode node, CancellationToken cancellationToken);

public enum TreeLoadState { NotLoaded, Loading, Loaded, Failed }

public sealed class TreeNode : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _label = string.Empty;
    private object? _icon;
    private bool _isEnabled = true;
    private bool _hasLazyChildren;
    private TreeLoadState _loadState;
    private string? _loadError;

    public string Id { get => _id; set { if (_id == value) return; _id = value; OnPropertyChanged(nameof(Id)); } }
    public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChanged(nameof(Label)); } }
    public ObservableCollection<TreeNode> Children { get; } = new();
    public object? Icon { get => _icon; set { if (Equals(_icon, value)) return; _icon = value; OnPropertyChanged(nameof(Icon)); } }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }
    /// <summary>A folder whose children arrive through <see cref="TreeView.ChildrenLoader"/> the first time it opens.</summary>
    public bool HasLazyChildren { get => _hasLazyChildren; set { if (_hasLazyChildren == value) return; _hasLazyChildren = value; OnPropertyChanged(nameof(HasLazyChildren)); } }
    public TreeLoadState LoadState { get => _loadState; internal set { if (_loadState == value) return; _loadState = value; OnPropertyChanged(nameof(LoadState)); } }
    /// <summary>Why the last load failed; cleared when the next one starts.</summary>
    public string? LoadError { get => _loadError; internal set { if (_loadError == value) return; _loadError = value; OnPropertyChanged(nameof(LoadError)); } }
    /// <summary>Whatever a custom row template needs to draw this node.</summary>
    public object? Data { get; set; }
    /// <summary>A branch that loads its children on demand, so an empty Children still shows as openable.</summary>
    internal bool ForceBranch { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class TreeRow(TreeNode node) : INotifyPropertyChanged
{
    private int _depth, _position, _setSize;
    private string? _parentId;
    private bool _isLast, _isExpanded, _isSelected, _isFocusable, _hasChildren;

    public TreeNode Node { get; } = node;
    public int Depth => _depth;
    public string? ParentId => _parentId;
    public int Position => _position;
    public int SetSize => _setSize;
    public bool HasChildren => Node.ForceBranch || Node.HasLazyChildren || Node.Children.Count > 0;
    public bool HasIcon => Node.Icon is not null;
    /// <summary>tree-view.tsx fileTone: the extension picks a tint for file glyphs; folders and other files have none.</summary>
    public string? FileTone => HasChildren ? null : System.IO.Path.GetExtension(Node.Label).ToLowerInvariant() switch
    {
        ".ts" or ".tsx" => "TypeScript",
        ".css" => "Stylesheet",
        ".md" => "Markdown",
        ".json" => "Json",
        _ => null
    };
    public bool IsLast { get => _isLast; set => Set(ref _isLast, value, nameof(IsLast), nameof(ScrollInset)); }
    /// <summary>Scroll padding carried by the rows, because a virtualizing panel can't scroll its host's padding.</summary>
    public Thickness ScrollInset => new(6, Depth == 1 ? 6 : 0, 6, IsLast ? 6 : 0);
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value, nameof(IsExpanded)); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value, nameof(IsSelected)); }
    public bool IsFocusable { get => _isFocusable; set => Set(ref _isFocusable, value, nameof(IsFocusable)); }
    public bool IsFirstChild => Position == 1;
    /// <summary>.item padding: 10 plus 18 per level, so the row and its selection stay full width.</summary>
    public Thickness ContentMargin => new(10 + (Depth - 1) * 18, 0, 10, 0);
    public Thickness RowMargin => new(10 + (Depth - 1) * 18, 2, 10, 0);
    public Thickness BranchMargin => new(16 + (Depth - 2) * 18, 4, 0, 4);
    /// <summary>Kept in the rows only while its exit plays; it takes no input and is not part of the tree.</summary>
    internal bool IsLeaving { get; set; }
    /// <summary>The row's settled height, which a reopened leaving row grows back to.</summary>
    internal double NaturalHeight { get; set; }
    /// <summary>Identifies the latest enter or exit, so a superseded one does not finish.</summary>
    internal object? Motion { get; set; }

    internal void Place(int depth, string? parentId, int position, int setSize)
    {
        if (_depth != depth) { _depth = depth; Raise(nameof(Depth), nameof(ContentMargin), nameof(RowMargin), nameof(BranchMargin), nameof(ScrollInset)); }
        if (_parentId != parentId) { _parentId = parentId; Raise(nameof(ParentId)); }
        if (_position != position) { _position = position; Raise(nameof(Position), nameof(IsFirstChild)); }
        if (_setSize != setSize) { _setSize = setSize; Raise(nameof(SetSize)); }
        if (_hasChildren != HasChildren) { _hasChildren = HasChildren; Raise(nameof(HasChildren), nameof(FileTone)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set(ref bool field, bool value, params string[] names) { if (field == value) return; field = value; Raise(names); }
    private void Raise(params string[] names) { foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}

/// <summary>Visible rows that a large or reordered change replaces in one reset instead of item by item.</summary>
internal sealed class TreeRowList : ObservableCollection<TreeRow>
{
    public void ResetTo(IEnumerable<TreeRow> rows)
    {
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

[TemplatePart(Name = "PART_RowsHost", Type = typeof(ItemsControl))]
public class TreeView : Control
{
    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(ObservableCollection<TreeNode>), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty DefaultExpandedIdsProperty = DependencyProperty.Register(nameof(DefaultExpandedIds), typeof(ObservableCollection<string>), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(TreeView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedIdsChanged));
    public static readonly DependencyProperty ChildrenLoaderProperty = DependencyProperty.Register(nameof(ChildrenLoader), typeof(TreeChildrenLoader), typeof(TreeView), new FrameworkPropertyMetadata(null, OnChildrenLoaderChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TreeView), new FrameworkPropertyMetadata("File tree"));
    private static readonly DependencyPropertyKey IsSpinningPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSpinning), typeof(bool), typeof(TreeView), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty IsSpinningProperty = IsSpinningPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey SelectedNodePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedNode), typeof(TreeNode), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty SelectedNodeProperty = SelectedNodePropertyKey.DependencyProperty;
    public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent(nameof(Selected), RoutingStrategy.Bubble, typeof(EventHandler<TreeNodeEventArgs>), typeof(TreeView));
    public static readonly RoutedEvent ExpandedChangedEvent = EventManager.RegisterRoutedEvent(nameof(ExpandedChanged), RoutingStrategy.Bubble, typeof(EventHandler<TreeExpandedChangedEventArgs>), typeof(TreeView));
    public static readonly RoutedEvent ChildrenLoadFailedEvent = EventManager.RegisterRoutedEvent(nameof(ChildrenLoadFailed), RoutingStrategy.Bubble, typeof(EventHandler<TreeLoadFailedEventArgs>), typeof(TreeView));

    // More rows than this changing at once are swapped in with one reset and no row motion.
    private const int BulkChange = 256;
    private ItemsControl? _rowsHost;
    private List<TreeRow> _rows = new();
    private readonly TreeRowList _items = new();
    private Dictionary<TreeNode, TreeRow> _rowCache = new();
    private readonly Dictionary<TreeNode, CancellationTokenSource> _loads = new();
    private readonly HashSet<TreeNode> _watched = new();
    private bool _rebuildQueued, _quietFocus;
    private readonly HashSet<string> _internalExpanded = new(StringComparer.Ordinal);
    private string? _focusedId;
    /// <summary>The folder that opened last; rows mounting after it stagger in order (tree-view.tsx openedId).</summary>
    private string? _openedId;
    private string? _shownSelectedId;
    private bool _deferRows;
    /// <summary>Set by a host that loads whole snapshots (JSONViewer): rows are rebuilt fresh each pass and it runs its own motion.</summary>
    private bool _hostOwnsRows;
    /// <summary>Raised when keyboard or pointer moves the current row, before focus lands on it.</summary>
    internal event Action<string>? CurrentRowMoved;
    internal ScrollViewer? RowsScroll => _rowsHost is null ? null : FindVisual(_rowsHost, item => item is ScrollViewer) as ScrollViewer;

    public ObservableCollection<TreeNode> Nodes { get => (ObservableCollection<TreeNode>?)GetValue(NodesProperty) ?? throw new InvalidOperationException("Tree nodes are not initialized."); set => SetValue(NodesProperty, value); }
    public ObservableCollection<string> DefaultExpandedIds { get => (ObservableCollection<string>?)GetValue(DefaultExpandedIdsProperty) ?? throw new InvalidOperationException("Default expanded ids are not initialized."); set => SetValue(DefaultExpandedIdsProperty, value); }
    public ObservableCollection<string>? ExpandedIds { get => (ObservableCollection<string>?)GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }
    /// <summary>Loads the children of nodes marked <see cref="TreeNode.HasLazyChildren"/> when they first open.</summary>
    public TreeChildrenLoader? ChildrenLoader { get => (TreeChildrenLoader?)GetValue(ChildrenLoaderProperty); set => SetValue(ChildrenLoaderProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Whether loading rows turn their spinner; false while motion is Off.</summary>
    public bool IsSpinning => (bool)GetValue(IsSpinningProperty);
    public TreeNode? SelectedNode { get => (TreeNode?)GetValue(SelectedNodeProperty); private set => SetValue(SelectedNodePropertyKey, value); }
    public event EventHandler<TreeNodeEventArgs> Selected { add => AddHandler(SelectedEvent, value); remove => RemoveHandler(SelectedEvent, value); }
    public event EventHandler<TreeExpandedChangedEventArgs> ExpandedChanged { add => AddHandler(ExpandedChangedEvent, value); remove => RemoveHandler(ExpandedChangedEvent, value); }
    public event EventHandler<TreeLoadFailedEventArgs> ChildrenLoadFailed { add => AddHandler(ChildrenLoadFailedEvent, value); remove => RemoveHandler(ChildrenLoadFailedEvent, value); }

    static TreeView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeView), new FrameworkPropertyMetadata(typeof(TreeView)));

    public TreeView()
    {
        SetCurrentValue(NodesProperty, new ObservableCollection<TreeNode>());
        SetCurrentValue(DefaultExpandedIdsProperty, new ObservableCollection<string>());
        AutomationProperties.SetName(this, Label);
        Nodes.CollectionChanged += NodesChanged;
        Unloaded += (_, _) => StopRowMotion();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TreeViewAutomationPeer(this);

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _rowsHost = GetTemplateChild("PART_RowsHost") as ItemsControl;
        if (_rowsHost is not null)
        {
            _rowsHost.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(RowClick), true);
            _rowsHost.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(RowMouseDown), true);
            _rowsHost.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(RowKeyDown), true);
            _rowsHost.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(RowFocus), true);
            _rowsHost.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(RowBringIntoView));
            _rowsHost.ItemsSource = _items;
        }
        InitializeExpanded();
        RebuildRows();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == NodesProperty)
        {
            if (e.OldValue is ObservableCollection<TreeNode> oldNodes) oldNodes.CollectionChanged -= NodesChanged;
            if (e.NewValue is ObservableCollection<TreeNode> newNodes) newNodes.CollectionChanged += NodesChanged;
            RebuildRows();
        }
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, Label);
        if (e.Property == ArcMotion.LevelProperty)
        {
            SetValue(IsSpinningPropertyKey, ArcMotion.GetLevel(this) != ArcMotionLevel.Off);
            if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) StopRowMotion();
        }
    }

    /// <summary>Replaces the rows, open branches and current row in one pass, for a host that owns all three.</summary>
    internal void Load(ObservableCollection<TreeNode> nodes, IEnumerable<string> expanded, string? selectedId)
    {
        _hostOwnsRows = true;
        _deferRows = true;
        var ids = new ObservableCollection<string>(expanded);
        DefaultExpandedIds = ids;
        ExpandedIds = new ObservableCollection<string>(ids);
        Nodes = nodes;
        SelectedNode = selectedId is null ? null : FindNode(nodes, selectedId);
        if (selectedId is not null) _focusedId = selectedId;
        _deferRows = false;
        RebuildRows();
    }

    internal void Select(string id)
    {
        SelectedNode = FindNode(Nodes, id);
        _focusedId = id;
        RebuildRows();
    }

    internal void FocusNode(string id) => FocusRow(id);

    /// <summary>Forgets what a lazy branch loaded; if it is open it loads again now, and its current children stay until then.</summary>
    public void Reload(TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        CancelLoad(node);
        node.LoadState = TreeLoadState.NotLoaded;
        RebuildRows();
    }

    private static TreeNode? FindNode(IEnumerable<TreeNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            if (FindNode(node.Children, id) is { } child) return child;
        }
        return null;
    }

    private static void OnExpandedIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TreeView)d).RebuildRows();
    private static void OnChildrenLoaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tree = (TreeView)d;
        foreach (var node in tree._loads.Keys.ToList()) tree.CancelLoad(node);
        tree.RebuildRows();
    }
    private void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRows();

    private void InitializeExpanded()
    {
        _internalExpanded.Clear();
        foreach (var id in DefaultExpandedIds) _internalExpanded.Add(id);
    }

    private IReadOnlySet<string> ExpandedSet => ExpandedIds is { } controlled ? controlled.ToHashSet(StringComparer.Ordinal) : _internalExpanded;

    private void RebuildRows()
    {
        if (_rowsHost is null || _deferRows) return;
        var expanded = ExpandedSet;
        bool animate = !_hostOwnsRows && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        // Where the old fill sat, read before the flags move it, so the new one can glide from there.
        Point? glideFrom = animate && SelectedNode?.Id != _shownSelectedId ? SelectionPoint(_shownSelectedId) : null;
        var cache = new Dictionary<TreeNode, TreeRow>();
        var rows = new List<TreeRow>();
        Flatten(Nodes, expanded, 1, null, rows, cache);
        _rows = rows;
        if (!rows.Any(row => row.Node.Id == _focusedId)) _focusedId = rows.FirstOrDefault()?.Node.Id;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            row.IsLast = index == rows.Count - 1;
            row.IsSelected = SelectedNode?.Id == row.Node.Id;
            row.IsFocusable = row.Node.Id == _focusedId;
        }
        // A reset throws away the row that holds keyboard focus, and focus would fall back to the window.
        bool focused = _rowsHost.IsKeyboardFocusWithin;
        var entering = new List<TreeRow>(); var leaving = new List<TreeRow>(); var revived = new List<TreeRow>();
        Sync(rows, animate, entering, leaving, revived);
        foreach (var row in _items) if (row.IsLeaving) cache[row.Node] = row;
        _rowCache = cache;
        if (focused) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(RestoreFocus));
        if (animate && (entering.Count > 0 || leaving.Count > 0 || revived.Count > 0 || glideFrom is not null)) StartRowMotion(rows, entering, leaving, revived, glideFrom);
        _shownSelectedId = SelectedNode?.Id;
        // Screen readers walk the realized rows; tell the tree's peer they changed.
        UIElementAutomationPeer.FromElement(this)?.InvalidatePeer();
        var open = rows.Where(row => row.IsExpanded).Select(row => row.Node).ToHashSet();
        Watch(open);
        foreach (var node in _loads.Keys.Where(node => !open.Contains(node)).ToList()) { CancelLoad(node); node.LoadState = TreeLoadState.NotLoaded; }
        if (ChildrenLoader is { } loader)
            foreach (var node in open)
                if (node.HasLazyChildren && node.LoadState is TreeLoadState.NotLoaded or TreeLoadState.Failed) BeginLoad(node, loader);
    }

    // Rows that stay keep their container, so focus, hover and running motion survive; rows leave and enter in place.
    private void Sync(List<TreeRow> rows, bool animate, List<TreeRow> entering, List<TreeRow> leaving, List<TreeRow> revived)
    {
        var index = new Dictionary<TreeRow, int>(rows.Count);
        for (int at = 0; at < rows.Count; at++) index[rows[at]] = at;
        int last = -1, common = 0; bool ordered = true;
        foreach (var row in _items)
        {
            if (!index.TryGetValue(row, out var at)) continue;
            if (at < last) { ordered = false; break; }
            last = at; common++;
        }
        if (_hostOwnsRows || !ordered || rows.Count + _items.Count - 2 * common > BulkChange)
        {
            StopRowMotion();
            _items.ResetTo(rows);
            return;
        }
        var merged = new List<TreeRow>(rows.Count + _items.Count - common);
        var before = new HashSet<TreeRow>(_items);
        int next = 0;
        foreach (var row in _items)
        {
            if (index.TryGetValue(row, out var at))
            {
                while (next < at) merged.Add(rows[next++]);
                merged.Add(row); next = at + 1;
                if (row.IsLeaving) { row.IsLeaving = false; revived.Add(row); }
            }
            else if (animate)
            {
                // tree-view.tsx: a leaving row drops its fill at once and takes no more input.
                merged.Add(row);
                if (!row.IsLeaving) { row.IsLeaving = true; row.IsSelected = false; row.IsFocusable = false; leaving.Add(row); }
            }
        }
        while (next < rows.Count) merged.Add(rows[next++]);
        foreach (var row in merged) if (!before.Contains(row)) entering.Add(row);
        var keep = new HashSet<TreeRow>(merged);
        for (int at = _items.Count - 1; at >= 0; at--) if (!keep.Contains(_items[at])) _items.RemoveAt(at);
        for (int at = 0; at < merged.Count; at++) if (at >= _items.Count || !ReferenceEquals(_items[at], merged[at])) _items.Insert(at, merged[at]);
    }

    private void StartRowMotion(List<TreeRow> rows, List<TreeRow> entering, List<TreeRow> leaving, List<TreeRow> revived, Point? glideFrom)
    {
        _rowsHost!.UpdateLayout();
        int opened = _openedId is null ? -1 : rows.FindIndex(row => row.Node.Id == _openedId);
        foreach (var row in entering)
        {
            if (Container(row) is not { } container) continue;
            int at = rows.IndexOf(row);
            // Only rows after the folder that opened wait their turn; seven steps at most keep the cascade short.
            double delay = opened >= 0 && at > opened ? Math.Min(at - opened - 1, 7) * TreeRowMotion.ItemStagger : 0;
            TreeRowMotion.Enter(row, container, delay);
        }
        foreach (var row in revived) if (Container(row) is { } container) TreeRowMotion.Revive(row, container);
        foreach (var row in leaving)
        {
            if (Container(row) is { } container) TreeRowMotion.Leave(row, container, () => FinishLeaving(row));
            else FinishLeaving(row);
        }
        if (glideFrom is { } from && SelectedNode is { } node && _rows.Find(row => row.Node == node) is { } selected && Container(selected) is { } target)
            TreeRowMotion.Glide(target, _rowsHost, from);
    }

    private void FinishLeaving(TreeRow row)
    {
        if (!row.IsLeaving) return;
        row.IsLeaving = false;
        _items.Remove(row);
        if (_rowCache.TryGetValue(row.Node, out var cached) && ReferenceEquals(cached, row)) _rowCache.Remove(row.Node);
    }

    /// <summary>Lands every row where it belongs: leaving rows go, the rest stop moving. For unload, template swaps and motion below Full.</summary>
    private void StopRowMotion()
    {
        foreach (var row in _items.Where(row => row.IsLeaving).ToList()) FinishLeaving(row);
        if (_rowsHost is null) return;
        foreach (var row in _items) if (Container(row) is { } container) TreeRowMotion.Stop(container);
    }

    private FrameworkElement? Container(TreeRow row) => _rowsHost?.ItemContainerGenerator.ContainerFromItem(row) as FrameworkElement;

    private Point? SelectionPoint(string? id)
    {
        if (id is null || _rowsHost is null || _items.FirstOrDefault(row => row.Node.Id == id && !row.IsLeaving) is not { } row || Container(row) is not { } container) return null;
        return TreeRowMotion.SelectionPoint(container, _rowsHost);
    }

    // Only open branches are watched: their rows are the ones a change can add or remove.
    private void Watch(HashSet<TreeNode> open)
    {
        foreach (var node in _watched.Where(node => !open.Contains(node)).ToList()) { CollectionChangedEventManager.RemoveHandler(node.Children, ChildrenChanged); _watched.Remove(node); }
        foreach (var node in open) if (_watched.Add(node)) CollectionChangedEventManager.AddHandler(node.Children, ChildrenChanged);
    }

    private void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A folder filling with a thousand files is one rebuild, not a thousand.
        if (_rebuildQueued || _deferRows) return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { _rebuildQueued = false; RebuildRows(); }));
    }

    private void BeginLoad(TreeNode node, TreeChildrenLoader loader)
    {
        var source = new CancellationTokenSource();
        _loads[node] = source;
        node.LoadError = null;
        node.LoadState = TreeLoadState.Loading;
        Task<IEnumerable<TreeNode>> task;
        try { task = loader(node, source.Token) ?? Task.FromResult(Enumerable.Empty<TreeNode>()); }
        catch (Exception error) { task = Task.FromException<IEnumerable<TreeNode>>(error); }
        // Marshalled by hand so the result lands on the tree's thread whatever context the loader ran in.
        task.ContinueWith(done => Dispatcher.BeginInvoke(new Action(() => CompleteLoad(node, source, done))), TaskScheduler.Default);
    }

    private void CompleteLoad(TreeNode node, CancellationTokenSource source, Task<IEnumerable<TreeNode>> task)
    {
        // Cancelled or superseded loads were already forgotten; their late result is dropped.
        if (!_loads.TryGetValue(node, out var current) || !ReferenceEquals(current, source)) return;
        _loads.Remove(node);
        source.Dispose();
        if (task.IsCompletedSuccessfully)
        {
            _deferRows = true;
            try
            {
                node.Children.Clear();
                foreach (var child in task.Result ?? []) node.Children.Add(child);
                node.LoadState = TreeLoadState.Loaded;
            }
            finally { _deferRows = false; }
            RebuildRows();
            return;
        }
        // The branch closes again, otherwise staying open would start the same load at once.
        var error = task.Exception?.GetBaseException();
        node.LoadError = error?.Message;
        node.LoadState = error is null ? TreeLoadState.NotLoaded : TreeLoadState.Failed;
        SetExpanded(node.Id, false);
        if (error is not null) RaiseEvent(new TreeLoadFailedEventArgs(ChildrenLoadFailedEvent, this, node, error));
    }

    private void CancelLoad(TreeNode node)
    {
        if (!_loads.Remove(node, out var source)) return;
        source.Cancel();
        source.Dispose();
    }

    private void Flatten(IEnumerable<TreeNode> nodes, IReadOnlySet<string> expanded, int depth, string? parentId, List<TreeRow> target, Dictionary<TreeNode, TreeRow> cache)
    {
        var list = nodes.ToList();
        for (var index = 0; index < list.Count; index++)
        {
            var node = list[index];
            TreeRow? row = null;
            bool known = !_hostOwnsRows && _rowCache.TryGetValue(node, out row);
            row ??= new TreeRow(node);
            row.Place(depth, parentId, index + 1, list.Count);
            bool open = row.HasChildren && expanded.Contains(node.Id);
            if (known && open && !row.IsExpanded) _openedId = node.Id;
            row.IsExpanded = open;
            cache[node] = row;
            target.Add(row);
            if (node.Children.Count > 0 && expanded.Contains(node.Id)) Flatten(node.Children, expanded, depth + 1, node.Id, target, cache);
        }
    }

    private void RowClick(object sender, RoutedEventArgs e)
    {
        if (IsNestedAction(e.OriginalSource as DependencyObject)) return;
        if (FindRow(e.OriginalSource as DependencyObject) is not { IsLeaving: false } row || !row.Node.IsEnabled) return;
        FocusRow(row.Node.Id); SelectRow(row); if (row.HasChildren) ToggleRow(row);
        e.Handled = true;
    }

    private void RowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsNestedAction(e.OriginalSource as DependencyObject)) return;
        if (FindRow(e.OriginalSource as DependencyObject) is not { IsLeaving: false } row || !row.Node.IsEnabled) return;
        FocusRow(row.Node.Id);
        SelectRow(row);
        if (row.HasChildren) ToggleRow(row);
        e.Handled = true;
    }

    private void RowKeyDown(object sender, KeyEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is not { } row) return;
        var index = _rows.IndexOf(row);
        if (index < 0) return;
        switch (e.Key)
        {
            case Key.Down: MoveFocus(Math.Min(index + 1, _rows.Count - 1)); e.Handled = true; break;
            case Key.Up: MoveFocus(Math.Max(index - 1, 0)); e.Handled = true; break;
            case Key.Home: MoveFocus(0); e.Handled = true; break;
            case Key.End: MoveFocus(Math.Max(0, _rows.Count - 1)); e.Handled = true; break;
            case Key.Right when row.HasChildren && !row.IsExpanded: ToggleRow(row); e.Handled = true; break;
            // Into the first child only: an open folder that is empty or still loading keeps the focus.
            case Key.Right when row.HasChildren: if (index + 1 < _rows.Count && _rows[index + 1].ParentId == row.Node.Id) MoveFocus(index + 1); e.Handled = true; break;
            case Key.Left when row.HasChildren && row.IsExpanded: ToggleRow(row); e.Handled = true; break;
            case Key.Left when row.ParentId is not null:
                MoveFocus(_rows.FindIndex(candidate => candidate.Node.Id == row.ParentId)); e.Handled = true; break;
            case Key.Enter or Key.Space when row.Node.IsEnabled: SelectRow(row); if (row.HasChildren) ToggleRow(row); e.Handled = true; break;
        }
    }

    private void RowFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Only a real move rebuilds: the flags change in place and the row keeps its focus.
        if (FindRow(e.NewFocus as DependencyObject) is { IsLeaving: false } row && row.Node.Id != _focusedId) { _focusedId = row.Node.Id; RebuildRows(); }
    }

    private void RowBringIntoView(object sender, RequestBringIntoViewEventArgs e) { if (_quietFocus) e.Handled = true; }

    private void MoveFocus(int index)
    {
        if (index < 0 || index >= _rows.Count) return;
        FocusRow(_rows[index].Node.Id);
    }

    private void FocusRow(string id)
    {
        _focusedId = id; RebuildRows();
        CurrentRowMoved?.Invoke(id);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            Realize(id);
            // Asked for by hand: the row may already hold focus again after the rebuild, and focusing it twice scrolls nothing.
            if (FindButton(id) is { } row) { row.Focus(); row.BringIntoView(); }
        }));
    }

    private void RestoreFocus()
    {
        // Only when nothing else took focus meanwhile, and without scrolling: the view may have moved away on purpose.
        if (_rowsHost is null || _focusedId is null || Keyboard.FocusedElement is Visual elsewhere && !elsewhere.IsAncestorOf(this) || FindButton(_focusedId) is not { } row) return;
        if (row.IsKeyboardFocused) return;
        _quietFocus = true;
        try { row.Focus(); }
        finally { _quietFocus = false; }
    }

    // A virtualized row outside the viewport has no button yet, so it is scrolled in before it takes focus.
    private void Realize(string id)
    {
        // A host that moves the scroll itself has already been told through CurrentRowMoved.
        if (CurrentRowMoved is not null || _rowsHost is null || FindButton(id) is not null) return;
        if (_rows.Find(row => row.Node.Id == id) is not { } target) return;
        int index = _items.IndexOf(target);
        if (index < 0 || FindVisual(_rowsHost, item => item is VirtualizingStackPanel) is not VirtualizingStackPanel panel) return;
        panel.BringIndexIntoViewPublic(index);
        _rowsHost.UpdateLayout();
    }

    private void SelectRow(TreeRow row)
    {
        if (!row.Node.IsEnabled) return;
        SelectedNode = row.Node; RebuildRows(); RaiseEvent(new TreeNodeEventArgs(SelectedEvent, this, row.Node));
    }

    private void ToggleRow(TreeRow row)
    {
        if (!row.HasChildren || !row.Node.IsEnabled) return;
        SetExpanded(row.Node.Id, !ExpandedSet.Contains(row.Node.Id));
    }

    internal void SelectFromAutomation(TreeRow row) { if (!row.IsLeaving) SelectRow(row); }
    internal void ExpandFromAutomation(TreeRow row, bool open)
    {
        if (row.IsLeaving || !row.HasChildren || !row.Node.IsEnabled) throw new ElementNotEnabledException();
        SetExpanded(row.Node.Id, open);
    }
    internal IEnumerable<TreeViewRow> RealizedRows() => _rowsHost is null ? [] : _items.Where(row => !row.IsLeaving).Select(row => Container(row) is { } container ? FindVisual(container, item => item is TreeViewRow) as TreeViewRow : null).OfType<TreeViewRow>();

    private void SetExpanded(string id, bool open)
    {
        var next = ExpandedSet.ToHashSet(StringComparer.Ordinal);
        if (!(open ? next.Add(id) : next.Remove(id))) return;
        if (ExpandedIds is { } controlled) { controlled.Clear(); foreach (var item in next) controlled.Add(item); }
        else { _internalExpanded.Clear(); foreach (var item in next) _internalExpanded.Add(item); }
        RebuildRows(); RaiseEvent(new TreeExpandedChangedEventArgs(ExpandedChangedEvent, this, next));
    }

    // A button inside a row template (a copy action, say) keeps its own click instead of acting as the row.
    private static bool IsNestedAction(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase button) return button.Tag is not TreeRow;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private TreeRow? FindRow(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && element.DataContext is TreeRow row) return row;
            source = source is Visual visual ? VisualTreeHelper.GetParent(visual) : null;
        }
        return null;
    }

    private System.Windows.Controls.Button? FindButton(string id) => FindVisual(_rowsHost, item => item is System.Windows.Controls.Button button && button.Tag is TreeRow { IsLeaving: false } row && row.Node.Id == id) as System.Windows.Controls.Button;
    internal static DependencyObject? FindVisual(DependencyObject? root, Func<DependencyObject, bool> match)
    {
        if (root is null) return null; if (match(root)) return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) if (FindVisual(VisualTreeHelper.GetChild(root, index), match) is { } result) return result;
        return null;
    }

    private void ReleaseTemplate()
    {
        StopRowMotion();
        if (_rowsHost is not null)
        {
            _rowsHost.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(RowClick));
            _rowsHost.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(RowMouseDown));
            _rowsHost.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(RowKeyDown));
            _rowsHost.RemoveHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(RowFocus));
            _rowsHost.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(RowBringIntoView));
            _rowsHost.ItemsSource = null;
        }
        _rowsHost = null;
    }
}

public sealed class TreeNodeEventArgs(RoutedEvent routedEvent, object source, TreeNode node) : RoutedEventArgs(routedEvent, source)
{
    public TreeNode Node { get; } = node;
}

public sealed class TreeExpandedChangedEventArgs(RoutedEvent routedEvent, object source, IReadOnlyCollection<string> expandedIds) : RoutedEventArgs(routedEvent, source)
{
    public IReadOnlyCollection<string> ExpandedIds { get; } = expandedIds;
}

public sealed class TreeLoadFailedEventArgs(RoutedEvent routedEvent, object source, TreeNode node, Exception error) : RoutedEventArgs(routedEvent, source)
{
    public TreeNode Node { get; } = node;
    public Exception Error { get; } = error;
}
