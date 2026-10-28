using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class TreeNode : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _label = string.Empty;
    private object? _icon;
    private bool _isEnabled = true;

    public string Id { get => _id; set { if (_id == value) return; _id = value; OnPropertyChanged(nameof(Id)); } }
    public string Label { get => _label; set { if (_label == value) return; _label = value; OnPropertyChanged(nameof(Label)); } }
    public ObservableCollection<TreeNode> Children { get; } = new();
    public object? Icon { get => _icon; set { if (Equals(_icon, value)) return; _icon = value; OnPropertyChanged(nameof(Icon)); } }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class TreeRow
{
    public required TreeNode Node { get; init; }
    public required int Depth { get; init; }
    public string? ParentId { get; init; }
    public required int Position { get; init; }
    public required int SetSize { get; init; }
    public bool HasChildren => Node.Children.Count > 0;
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool IsFocusable { get; set; }
    public bool IsFirstChild => Position == 1;
    public Thickness RowMargin => new(10 + (Depth - 1) * 18, 2, 10, 0);
    public Thickness BranchMargin => new(16 + (Depth - 2) * 18, 4, 0, 4);
}

[TemplatePart(Name = "PART_RowsHost", Type = typeof(ItemsControl))]
public class TreeView : Control
{
    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(ObservableCollection<TreeNode>), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty DefaultExpandedIdsProperty = DependencyProperty.Register(nameof(DefaultExpandedIds), typeof(ObservableCollection<string>), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty ExpandedIdsProperty = DependencyProperty.Register(nameof(ExpandedIds), typeof(ObservableCollection<string>), typeof(TreeView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExpandedIdsChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TreeView), new FrameworkPropertyMetadata("File tree"));
    private static readonly DependencyPropertyKey SelectedNodePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedNode), typeof(TreeNode), typeof(TreeView), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty SelectedNodeProperty = SelectedNodePropertyKey.DependencyProperty;
    public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent(nameof(Selected), RoutingStrategy.Bubble, typeof(EventHandler<TreeNodeEventArgs>), typeof(TreeView));
    public static readonly RoutedEvent ExpandedChangedEvent = EventManager.RegisterRoutedEvent(nameof(ExpandedChanged), RoutingStrategy.Bubble, typeof(EventHandler<TreeExpandedChangedEventArgs>), typeof(TreeView));

    private ItemsControl? _rowsHost;
    private readonly ObservableCollection<TreeRow> _rows = new();
    private readonly HashSet<string> _internalExpanded = new(StringComparer.Ordinal);
    private string? _focusedId;

    public ObservableCollection<TreeNode> Nodes { get => (ObservableCollection<TreeNode>?)GetValue(NodesProperty) ?? throw new InvalidOperationException("Tree nodes are not initialized."); set => SetValue(NodesProperty, value); }
    public ObservableCollection<string> DefaultExpandedIds { get => (ObservableCollection<string>?)GetValue(DefaultExpandedIdsProperty) ?? throw new InvalidOperationException("Default expanded ids are not initialized."); set => SetValue(DefaultExpandedIdsProperty, value); }
    public ObservableCollection<string>? ExpandedIds { get => (ObservableCollection<string>?)GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public TreeNode? SelectedNode { get => (TreeNode?)GetValue(SelectedNodeProperty); private set => SetValue(SelectedNodePropertyKey, value); }
    public event EventHandler<TreeNodeEventArgs> Selected { add => AddHandler(SelectedEvent, value); remove => RemoveHandler(SelectedEvent, value); }
    public event EventHandler<TreeExpandedChangedEventArgs> ExpandedChanged { add => AddHandler(ExpandedChangedEvent, value); remove => RemoveHandler(ExpandedChangedEvent, value); }

    static TreeView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeView), new FrameworkPropertyMetadata(typeof(TreeView)));

    public TreeView()
    {
        SetCurrentValue(NodesProperty, new ObservableCollection<TreeNode>());
        SetCurrentValue(DefaultExpandedIdsProperty, new ObservableCollection<string>());
        AutomationProperties.SetName(this, Label);
        Nodes.CollectionChanged += NodesChanged;
    }

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
    }

    private static void OnExpandedIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TreeView)d).RebuildRows();
    private void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildRows();

    private void InitializeExpanded()
    {
        _internalExpanded.Clear();
        foreach (var id in DefaultExpandedIds) _internalExpanded.Add(id);
    }

    private IReadOnlySet<string> ExpandedSet => ExpandedIds is { } controlled ? controlled.ToHashSet(StringComparer.Ordinal) : _internalExpanded;

    private void RebuildRows()
    {
        if (_rowsHost is null) return;
        var expanded = ExpandedSet;
        _rows.Clear();
        Flatten(Nodes, expanded, 1, null, _rows);
        var visibleFocused = _rows.Any(row => row.Node.Id == _focusedId);
        if (!visibleFocused) _focusedId = _rows.FirstOrDefault()?.Node.Id;
        foreach (var row in _rows) { row.IsSelected = SelectedNode?.Id == row.Node.Id; row.IsFocusable = row.Node.Id == _focusedId; row.IsExpanded = row.HasChildren && expanded.Contains(row.Node.Id); }
        _rowsHost.ItemsSource = null;
        _rowsHost.ItemsSource = _rows;
    }

    private static void Flatten(IEnumerable<TreeNode> nodes, IReadOnlySet<string> expanded, int depth, string? parentId, ICollection<TreeRow> target)
    {
        var list = nodes.ToList();
        for (var index = 0; index < list.Count; index++)
        {
            var node = list[index];
            target.Add(new TreeRow { Node = node, Depth = depth, ParentId = parentId, Position = index + 1, SetSize = list.Count, IsExpanded = node.Children.Count > 0 && expanded.Contains(node.Id) });
            if (node.Children.Count > 0 && expanded.Contains(node.Id)) Flatten(node.Children, expanded, depth + 1, node.Id, target);
        }
    }

    private void RowClick(object sender, RoutedEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is not { } row || !row.Node.IsEnabled) return;
        FocusRow(row.Node.Id); SelectRow(row); if (row.HasChildren) ToggleRow(row);
        e.Handled = true;
    }

    private void RowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindRow(e.OriginalSource as DependencyObject) is not { } row || !row.Node.IsEnabled) return;
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
            case Key.Right when row.HasChildren: MoveFocus(index + 1 < _rows.Count ? index + 1 : index); e.Handled = true; break;
            case Key.Left when row.HasChildren && row.IsExpanded: ToggleRow(row); e.Handled = true; break;
            case Key.Left when row.ParentId is not null:
                var parentIndex = _rows.Select((candidate, candidateIndex) => (candidate, candidateIndex)).FirstOrDefault(pair => pair.candidate.Node.Id == row.ParentId).candidateIndex;
                MoveFocus(parentIndex); e.Handled = true; break;
            case Key.Enter or Key.Space when row.Node.IsEnabled: SelectRow(row); if (row.HasChildren) ToggleRow(row); e.Handled = true; break;
        }
    }

    private void RowFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (FindRow(e.NewFocus as DependencyObject) is { } row) { _focusedId = row.Node.Id; RebuildRows(); }
    }

    private void MoveFocus(int index)
    {
        if (index < 0 || index >= _rows.Count) return;
        FocusRow(_rows[index].Node.Id);
    }

    private void FocusRow(string id)
    {
        _focusedId = id; RebuildRows();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => FindButton(id)?.Focus()));
    }

    private void SelectRow(TreeRow row)
    {
        if (!row.Node.IsEnabled) return;
        SelectedNode = row.Node; RebuildRows(); RaiseEvent(new TreeNodeEventArgs(SelectedEvent, this, row.Node));
    }

    private void ToggleRow(TreeRow row)
    {
        if (!row.HasChildren || !row.Node.IsEnabled) return;
        var next = ExpandedSet.ToHashSet(StringComparer.Ordinal);
        if (!next.Add(row.Node.Id)) next.Remove(row.Node.Id);
        if (ExpandedIds is { } controlled) { controlled.Clear(); foreach (var id in next) controlled.Add(id); }
        else { _internalExpanded.Clear(); foreach (var id in next) _internalExpanded.Add(id); }
        RebuildRows(); RaiseEvent(new TreeExpandedChangedEventArgs(ExpandedChangedEvent, this, next));
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

    private System.Windows.Controls.Button? FindButton(string id) => FindVisual(_rowsHost, item => item is System.Windows.Controls.Button button && button.Tag is TreeRow row && row.Node.Id == id) as System.Windows.Controls.Button;
    private static DependencyObject? FindVisual(DependencyObject? root, Func<DependencyObject, bool> match)
    {
        if (root is null) return null; if (match(root)) return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) if (FindVisual(VisualTreeHelper.GetChild(root, index), match) is { } result) return result;
        return null;
    }

    private void ReleaseTemplate()
    {
        if (_rowsHost is not null)
        {
            _rowsHost.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(RowClick));
            _rowsHost.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(RowMouseDown));
            _rowsHost.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(RowKeyDown));
            _rowsHost.RemoveHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(RowFocus));
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
