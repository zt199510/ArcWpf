using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ConversationItem(string id, string title, string? group = null) : INotifyPropertyChanged
{
    private string _title = title;
    private bool _archived;

    public string Id { get; } = id;
    public string? Group { get; init; } = group;
    public string Title { get => _title; set { if (_title != value) { _title = value; Raise(); } } }
    public bool IsArchived { get => _archived; set { if (_archived != value) { _archived = value; Raise(); } } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ConversationEventArgs(ConversationItem item) : EventArgs
{
    public ConversationItem Item { get; } = item;
}

public sealed class ConversationRenamedEventArgs(ConversationItem item, string oldTitle) : EventArgs
{
    public ConversationItem Item { get; } = item;
    public string OldTitle { get; } = oldTitle;
}

/// <summary>
/// The list of chats beside a conversation: new chat, search, groups, rename in place and archive. Arc has no such block; this composes
/// its workspace sidebar's rows and search with the group labels of its chat history menu. Arrow keys walk the rows, F2 renames, Delete archives.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class ConversationSidebar : Control
{
    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(nameof(SelectedId), typeof(string), typeof(ConversationSidebar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ConversationSidebar)d).Paint()));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(ConversationSidebar), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ConversationSidebar)d).OnQuery()));

    public string? SelectedId { get => (string?)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public ObservableCollection<ConversationItem> Conversations { get; } = [];

    public event EventHandler? NewRequested;
    public event EventHandler<ConversationEventArgs>? Selected;
    public event EventHandler<ConversationRenamedEventArgs>? Renamed;
    public event EventHandler<ConversationEventArgs>? Archived;
    public event EventHandler<ConversationEventArgs>? Restored;

    private readonly Grid _layout = new();
    private readonly ToolbarButton _new;
    private readonly TextBox _search = ThreadUi.PlainTextBox();
    private readonly TextBlock _searchHint = AiUi.Line("Search chats", 14, 19.6, "TextMuted");
    private readonly ToolbarButton _clear = AiUi.Icon(Lucide.X, 22, 12, "Clear search", 11);
    private readonly Border _searchFrame = new() { Height = 32, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(10, 0, 5, 0) };
    private readonly StackPanel _list = new() { Margin = new Thickness(0, 8, 0, 8) };
    private readonly OverlayScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    private readonly TextBlock _empty = AiUi.Line("", 14, 19.6, "TextMuted");
    private readonly ToolbarButton _archiveToggle;
    private readonly TurningChevron _archiveChevron = new(14, "TextMuted");
    private readonly TextBlock _archiveLabel = AiUi.Line("Archived", 12, 16.8, "TextMuted", true, false);
    private readonly SpringHeightBox _archiveBox = new(Tween.Snappy);
    private readonly StackPanel _archiveList = new();
    private readonly Dictionary<ConversationItem, Row> _rows = [];
    private Decorator? _root;
    private bool _archiveOpen;

    internal System.Windows.Controls.Button NewForTest => _new;
    internal TextBox SearchForTest => _search;
    internal System.Windows.Controls.Button ClearForTest => _clear;
    internal IReadOnlyList<string> VisibleTitlesForTest => [.. VisibleRows().Select(row => row.Item.Title)];
    internal IReadOnlyList<string> GroupsForTest => [.. _list.Children.OfType<TextBlock>().Where(label => !ReferenceEquals(label, _empty) && label.Visibility == Visibility.Visible).Select(label => label.Text)];
    internal IReadOnlyList<string> ArchivedTitlesForTest => [.. _archiveList.Children.OfType<SpringHeightBox>().Select(box => _rows.Values.First(row => ReferenceEquals(row.Box, box)).Item.Title)];
    internal System.Windows.Controls.Button RowForTest(string id) => RowOf(id).Button;
    internal System.Windows.Controls.Button RenameForTest(string id) => RowOf(id).RenameButton;
    internal System.Windows.Controls.Button ArchiveForTest(string id) => RowOf(id).ArchiveButton;
    internal bool ActionsShownForTest(string id) => RowOf(id).ActionsShown;
    internal TextBox? EditorForTest(string id) => RowOf(id).Editor;
    internal System.Windows.Controls.Button ArchiveToggleForTest => _archiveToggle;
    internal bool ArchiveOpenForTest => _archiveOpen;
    internal string EmptyForTest => _empty.Visibility == Visibility.Visible ? _empty.Text : "";
    private Row RowOf(string id) => _rows.Values.First(row => row.Item.Id == id);

    static ConversationSidebar() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ConversationSidebar), new FrameworkPropertyMetadata(typeof(ConversationSidebar)));

    public ConversationSidebar()
    {
        _new = new ToolbarButton(8) { Height = 32, Padding = new Thickness(12, 0, 12, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, "Foreground") };
        var newRow = new StackPanel { Orientation = Orientation.Horizontal };
        var newIcon = AiUi.Glyph(Lucide.SquarePen, 16, "Foreground");
        newIcon.Margin = new Thickness(0, 0, 10, 0);
        newRow.Children.Add(newIcon);
        newRow.Children.Add(AiUi.Line("New chat", 14, 19.6, "Foreground", false, false));
        _new.Content = newRow;
        AutomationProperties.SetName(_new, "New chat");
        _new.Click += (_, _) => NewRequested?.Invoke(this, EventArgs.Empty);

        BuildSearch();

        _scroll.Content = _list;
        var column = new DockPanel { LastChildFill = true };
        _new.Margin = new Thickness(0, 0, 0, 4);
        DockPanel.SetDock(_new, Dock.Top);
        column.Children.Add(_new);
        DockPanel.SetDock(_searchFrame, Dock.Top);
        column.Children.Add(_searchFrame);

        _archiveToggle = new ToolbarButton(8) { Height = 32, Padding = new Thickness(12, 0, 8, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, "TextMuted") };
        var toggleRow = new DockPanel();
        DockPanel.SetDock(_archiveChevron, Dock.Right);
        toggleRow.Children.Add(_archiveChevron);
        toggleRow.Children.Add(_archiveLabel);
        _archiveToggle.Content = toggleRow;
        _archiveToggle.Click += (_, _) => SetArchiveOpen(!_archiveOpen);
        _archiveBox.Child = _archiveList;
        _archiveBox.Visibility = Visibility.Collapsed;
        var archive = new StackPanel();
        archive.Children.Add(_archiveToggle);
        archive.Children.Add(_archiveBox);
        DockPanel.SetDock(archive, Dock.Bottom);
        column.Children.Add(archive);
        column.Children.Add(_scroll);
        _layout.Children.Add(column);
        _empty.Margin = new Thickness(12, 8, 12, 0);
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.TextTrimming = TextTrimming.None;

        Conversations.CollectionChanged += OnConversations;
        PreviewKeyDown += OnKey;
    }

    private void BuildSearch()
    {
        _searchFrame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _searchFrame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _searchFrame.Cursor = Cursors.IBeam;
        _searchFrame.Margin = new Thickness(0, 0, 0, 0);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = AiUi.Glyph(Lucide.Search, 16, "TextMuted");
        icon.Margin = new Thickness(0, 0, 8, 0);
        grid.Children.Add(icon);
        _searchHint.IsHitTestVisible = false;
        Grid.SetColumn(_searchHint, 1);
        grid.Children.Add(_searchHint);
        _search.FontSize = 14;
        _search.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_search, "Search chats");
        Grid.SetColumn(_search, 1);
        grid.Children.Add(_search);
        _clear.Visibility = Visibility.Collapsed;
        _clear.IsTabStop = false;
        _clear.Click += (_, _) => { SetCurrentValue(QueryProperty, ""); _search.Focus(); };
        Grid.SetColumn(_clear, 2);
        grid.Children.Add(_clear);
        _searchFrame.Child = grid;
        _search.TextChanged += (_, _) => { if (Query != _search.Text) { SetCurrentValue(QueryProperty, _search.Text); } };
        _search.GotKeyboardFocus += (_, _) => _searchFrame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderStrong");
        _search.LostKeyboardFocus += (_, _) => _searchFrame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_root is not null) { _root.Child = null; }
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = _layout;
        Refresh();
    }

    private void OnQuery()
    {
        if (_search.Text != Query) { _search.Text = Query ?? ""; }
        bool any = !string.IsNullOrEmpty(Query);
        _searchHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        _clear.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    private void OnConversations(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) { foreach (ConversationItem item in e.OldItems) { if (_rows.Remove(item, out var row)) { row.Detach(); } } }
        if (e.Action == NotifyCollectionChangedAction.Reset) { foreach (var row in _rows.Values) { row.Detach(); } _rows.Clear(); }
        Refresh(e.Action == NotifyCollectionChangedAction.Add ? e.NewItems?.OfType<ConversationItem>().ToList() : null);
    }

    private Row RowFor(ConversationItem item)
    {
        if (!_rows.TryGetValue(item, out var row)) { row = new Row(this, item); _rows[item] = row; }
        return row;
    }

    // Lays the rows out for the current query and archive state, reusing what is on screen.
    private void Refresh(IReadOnlyList<ConversationItem>? added = null)
    {
        if (_root is null) { return; }
        _list.Children.Clear();
        _archiveList.Children.Clear();
        string needle = (Query ?? "").Trim();
        bool Match(ConversationItem item) => needle.Length == 0 || item.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
        var groups = Conversations.Where(item => !item.IsArchived && Match(item)).GroupBy(item => item.Group ?? "").ToList();
        foreach (var group in groups)
        {
            if (group.Key.Length > 0)
            {
                var label = AiUi.Line(group.Key, 12, 16.8, "TextMuted", true);
                label.Margin = new Thickness(12, _list.Children.Count == 0 ? 4 : 14, 12, 6);
                _list.Children.Add(label);
            }
            foreach (var item in group)
            {
                var row = RowFor(item);
                Tween.Set(row.Box, OpacityProperty, 1);
                _list.Children.Add(row.Box);
                if (added?.Contains(item) == true && AiUi.Moving(this)) { row.Box.Reveal(); }
            }
        }
        if (groups.Count == 0)
        {
            bool searching = needle.Length > 0;
            _empty.Text = searching ? $"No chats match “{needle}”" : "No chats yet";
            _empty.Visibility = Visibility.Visible;
            _list.Children.Add(_empty);
        }
        var archived = Conversations.Where(item => item.IsArchived && Match(item)).ToList();
        foreach (var item in archived) { var row = RowFor(item); Tween.Set(row.Box, OpacityProperty, 1); _archiveList.Children.Add(row.Box); row.Sync(); }
        foreach (var item in Conversations.Where(item => !item.IsArchived && Match(item))) { RowFor(item).Sync(); }
        int total = Conversations.Count(item => item.IsArchived);
        _archiveToggle.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        _archiveLabel.Text = $"Archived  ·  {total}";
        AutomationProperties.SetName(_archiveToggle, $"Archived, {total}");
        if (total == 0 && _archiveOpen) { SetArchiveOpen(false); }
        // Keep one way in with Tab: the selected row, or the first row.
        var visible = VisibleRows().ToList();
        var stop = visible.FirstOrDefault(row => row.Item.Id == SelectedId) ?? visible.FirstOrDefault();
        foreach (var row in _rows.Values) { row.Button.IsTabStop = ReferenceEquals(row, stop); }
        Paint();
    }

    private IEnumerable<Row> VisibleRows()
    {
        foreach (var box in _list.Children.OfType<SpringHeightBox>()) { yield return _rows.Values.First(row => ReferenceEquals(row.Box, box)); }
        if (_archiveOpen) { foreach (var box in _archiveList.Children.OfType<SpringHeightBox>()) { yield return _rows.Values.First(row => ReferenceEquals(row.Box, box)); } }
    }

    private void Paint() { foreach (var row in _rows.Values) { row.Paint(); } }

    private void SetArchiveOpen(bool open)
    {
        _archiveOpen = open;
        _archiveChevron.Point(open);
        bool moving = AiUi.Moving(this);
        if (open)
        {
            _archiveBox.Visibility = Visibility.Visible;
            if (moving) { _archiveBox.Reveal(); } else { _archiveBox.InvalidateMeasure(); }
        }
        else if (moving) { _archiveBox.Collapse(() => _archiveBox.Visibility = Visibility.Collapsed, true); }
        else { _archiveBox.Visibility = Visibility.Collapsed; }
        Announcer.Say(this, open ? "Archived chats shown" : "Archived chats hidden");
    }

    private void Select(ConversationItem item)
    {
        if (SelectedId != item.Id) { SetCurrentValue(SelectedIdProperty, item.Id); }
        Selected?.Invoke(this, new ConversationEventArgs(item));
    }

    private void Archive(ConversationItem item)
    {
        var row = RowFor(item);
        var before = VisibleRows().ToList();
        int index = before.IndexOf(row);
        var next = before.Skip(index + 1).FirstOrDefault(candidate => !candidate.Item.IsArchived)
            ?? before.Take(index).Reverse().FirstOrDefault(candidate => !candidate.Item.IsArchived);
        bool hadFocus = row.Box.IsKeyboardFocusWithin;
        if (hadFocus) { next?.Button.Focus(); }
        // The next chat takes over when the open one goes, as the list closes over it.
        if (SelectedId == item.Id)
        {
            var heir = next?.Item;
            SetCurrentValue(SelectedIdProperty, heir?.Id);
            if (heir is not null) { Selected?.Invoke(this, new ConversationEventArgs(heir)); }
        }
        void Finish()
        {
            item.IsArchived = true;
            Refresh();
            Announcer.Say(this, $"{item.Title} archived");
            Archived?.Invoke(this, new ConversationEventArgs(item));
        }
        if (AiUi.Moving(this))
        {
            Tween.To(row.Box, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard);
            row.Box.Collapse(Finish, true);
        }
        else { Finish(); }
    }

    private void Restore(ConversationItem item)
    {
        item.IsArchived = false;
        Refresh();
        Announcer.Say(this, $"{item.Title} restored");
        Restored?.Invoke(this, new ConversationEventArgs(item));
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var rows = VisibleRows().ToList();
        if (ReferenceEquals(Keyboard.FocusedElement, _search))
        {
            if (e.Key == Key.Down && rows.Count > 0) { rows[0].Button.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape && Query.Length > 0) { SetCurrentValue(QueryProperty, ""); e.Handled = true; }
            return;
        }
        if (Keyboard.FocusedElement is TextBox) { return; }
        var current = rows.FirstOrDefault(row => row.Button.IsKeyboardFocused);
        if (current is null) { return; }
        int at = rows.IndexOf(current);
        switch (e.Key)
        {
            case Key.Down: rows[Math.Min(at + 1, rows.Count - 1)].Button.Focus(); e.Handled = true; break;
            case Key.Up:
                if (at == 0) { _search.Focus(); } else { rows[at - 1].Button.Focus(); }
                e.Handled = true;
                break;
            case Key.Home: rows[0].Button.Focus(); e.Handled = true; break;
            case Key.End: rows[^1].Button.Focus(); e.Handled = true; break;
            case Key.F2 when !current.Item.IsArchived: current.BeginRename(); e.Handled = true; break;
            case Key.Delete when !current.Item.IsArchived: Archive(current.Item); e.Handled = true; break;
        }
    }

    // A row: the row button itself, with its actions lying over the trailing end so they are not nested inside it.
    private sealed class Row
    {
        private readonly ConversationSidebar _owner;
        private readonly Grid _grid = new() { Height = 32 };
        private readonly TextBlock _title;
        private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly ToolbarButton _rename, _archive;
        private bool _keyFocus;
        private readonly Border _editFrame = new() { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(11, 0, 11, 0), Visibility = Visibility.Collapsed };

        public ConversationItem Item { get; }
        public SpringHeightBox Box { get; } = new(Tween.Snappy);
        public ToolbarButton Button { get; }
        public System.Windows.Controls.Button RenameButton => _rename;
        public System.Windows.Controls.Button ArchiveButton => _archive;
        public TextBox? Editor { get; private set; }
        public bool ActionsShown => _actions.Visibility == Visibility.Visible;

        public Row(ConversationSidebar owner, ConversationItem item)
        {
            (_owner, Item) = (owner, item);
            _title = AiUi.Line(item.Title, 14, 19.6, "TextSecondary");
            Button = new ToolbarButton(8, 1) { FocusVisualStyle = null, Height = 32, Padding = new Thickness(12, 0, 12, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = _title };
            Button.Roles = self =>
            {
                bool on = _owner.SelectedId == Item.Id;
                // The list clips its rows, so keyboard focus draws an inner edge rather than the shared outer ring.
                return (on || self.IsPressed ? "Border" : self.IsMouseOver || _keyFocus ? "SurfaceMuted" : null, _keyFocus ? "Foreground" : null, on ? "Foreground" : "TextSecondary");
            };
            Button.Click += (_, _) => { if (!Item.IsArchived) { _owner.Select(Item); } };
            _rename = AiUi.Icon(Lucide.Pencil, 26, 14, $"Rename {item.Title}", 8);
            _rename.IsTabStop = false;
            _rename.Click += (_, _) => BeginRename();
            _archive = AiUi.Icon(Lucide.Archive, 26, 14, $"Archive {item.Title}", 8);
            _archive.IsTabStop = false;
            _archive.Click += (_, _) => { if (Item.IsArchived) { _owner.Restore(Item); } else { _owner.Archive(Item); } };
            _actions.Children.Add(_rename);
            _actions.Children.Add(_archive);
            _actions.Visibility = Visibility.Collapsed;
            _grid.Children.Add(Button);
            _grid.Children.Add(_actions);
            _grid.Children.Add(_editFrame);
            _editFrame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            _editFrame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderStrong");
            Box.Child = _grid;
            Box.Margin = new Thickness(0, 0, 0, 2);
            _grid.MouseEnter += (_, _) => Actions();
            _grid.MouseLeave += (_, _) => Actions();
            _grid.IsKeyboardFocusWithinChanged += (_, _) => Actions();
            Button.GotKeyboardFocus += (_, _) => { _keyFocus = InputManager.Current.MostRecentInputDevice is KeyboardDevice; Button.Refresh(true); };
            Button.LostKeyboardFocus += (_, _) => { _keyFocus = false; Button.Refresh(true); };
            Item.PropertyChanged += OnItem;
            Sync();
        }

        public void Detach() => Item.PropertyChanged -= OnItem;

        private void OnItem(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ConversationItem.Title)) { Sync(); } }

        public void Sync()
        {
            _title.Text = Item.Title;
            AutomationProperties.SetName(Button, Item.Title);
            AutomationProperties.SetHelpText(Button, Item.IsArchived ? "Archived" : "F2 renames, Delete archives");
            _rename.Visibility = Item.IsArchived ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(_rename, $"Rename {Item.Title}");
            AutomationProperties.SetName(_archive, (Item.IsArchived ? "Restore " : "Archive ") + Item.Title);
            _archive.Content = Lucide.Glyph(Item.IsArchived ? Lucide.RotateCcw : Lucide.Archive, 14);
            Actions();
        }

        public void Paint()
        {
            bool on = _owner.SelectedId == Item.Id;
            _title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (on ? "Foreground" : "TextSecondary"));
            AutomationProperties.SetItemStatus(Button, on ? "current" : "");
            Button.Refresh(true);
        }

        private void Actions()
        {
            bool show = (_grid.IsMouseOver || _grid.IsKeyboardFocusWithin) && Editor is null;
            _actions.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            // The title stops short of the actions lying over it.
            Button.Padding = new Thickness(12, 0, show ? (Item.IsArchived ? 34 : 60) : 12, 0);
        }

        public void BeginRename()
        {
            var box = ThreadUi.PlainTextBox();
            box.Text = Item.Title;
            box.FontSize = 14;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(box, "Chat title");
            _editFrame.Child = box;
            _editFrame.Visibility = Visibility.Visible;
            Editor = box;
            Actions();
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { EndRename(true, true); e.Handled = true; }
                else if (e.Key == Key.Escape) { EndRename(false, true); e.Handled = true; }
            };
            box.LostKeyboardFocus += (_, _) => { if (ReferenceEquals(Editor, box)) { EndRename(true, false); } };
            box.Focus();
            box.SelectAll();
            _owner.Dispatcher.BeginInvoke(() => { if (ReferenceEquals(Editor, box)) { box.Focus(); box.SelectAll(); } }, DispatcherPriority.Input);
        }

        private void EndRename(bool save, bool refocus)
        {
            if (Editor is not { } box) { return; }
            string title = box.Text.Trim();
            Editor = null;
            _editFrame.Child = null;
            _editFrame.Visibility = Visibility.Collapsed;
            Actions();
            if (refocus) { Button.Focus(); }
            // An empty title keeps the old one rather than leaving a blank row.
            if (save && title.Length > 0 && title != Item.Title)
            {
                string old = Item.Title;
                Item.Title = title;
                Announcer.Say(_owner, $"Renamed to {title}");
                _owner.Renamed?.Invoke(_owner, new ConversationRenamedEventArgs(Item, old));
            }
        }
    }
}
