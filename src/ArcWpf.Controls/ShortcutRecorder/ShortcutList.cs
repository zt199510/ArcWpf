using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed record ShortcutListItem(string Label, string Shortcut, string? Keywords = null);
public sealed record ShortcutListGroup(string Label, IReadOnlyList<ShortcutListItem> Items);

/// <summary>
/// A searchable, grouped cheatsheet (ShortcutList in shortcut-recorder.tsx). Holding a modifier lights that cap in every row and fades rows that do
/// not use it, so people can explore by holding keys; a row whose whole combination is down lights up. Search matches names, groups, and key names.
/// </summary>
public class ShortcutList : Control
{
    public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register(nameof(Groups), typeof(IList<ShortcutListGroup>), typeof(ShortcutList), new PropertyMetadata(null, (d, _) => ((ShortcutList)d).Rebuild()));
    public static readonly DependencyProperty PlatformProperty = DependencyProperty.Register(nameof(Platform), typeof(ShortcutPlatform), typeof(ShortcutList), new PropertyMetadata(ShortcutPlatform.Other, (d, _) => ((ShortcutList)d).Rebuild()));
    public static readonly DependencyProperty IsSearchableProperty = DependencyProperty.Register(nameof(IsSearchable), typeof(bool), typeof(ShortcutList), new PropertyMetadata(true, (d, _) => ((ShortcutList)d).Rebuild()));
    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(ShortcutList), new PropertyMetadata("Search shortcuts", (d, _) => ((ShortcutList)d).Rebuild()));
    public static readonly DependencyProperty HighlightPressedProperty = DependencyProperty.Register(nameof(HighlightPressed), typeof(bool), typeof(ShortcutList), new PropertyMetadata(true));

    public IList<ShortcutListGroup>? Groups { get => (IList<ShortcutListGroup>?)GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }
    public ShortcutPlatform Platform { get => (ShortcutPlatform)GetValue(PlatformProperty); set => SetValue(PlatformProperty, value); }
    public bool IsSearchable { get => (bool)GetValue(IsSearchableProperty); set => SetValue(IsSearchableProperty, value); }
    public string SearchPlaceholder { get => (string)GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    /// <summary>Light up caps as keys are held, and fade rows that do not use the held modifiers.</summary>
    public bool HighlightPressed { get => (bool)GetValue(HighlightPressedProperty); set => SetValue(HighlightPressedProperty, value); }

    private readonly StackPanel _root = new();
    private readonly Grid _groups = new();
    private readonly TextBlock _empty = new() { Padding = new Thickness(0, 16, 0, 16), TextWrapping = TextWrapping.Wrap };
    private readonly HashSet<string> _held = [];
    private readonly Dictionary<string, GroupView> _views = [];
    private Border? _searchRow;
    private TextBox? _search;
    private TextBlock? _count, _placeholder;
    private FieldIconButton? _clear;
    private Window? _window;
    private bool _twoColumns;

    static ShortcutList()
    {
        FocusableProperty.OverrideMetadata(typeof(ShortcutList), new FrameworkPropertyMetadata(false));
    }

    public ShortcutList()
    {
        AddVisualChild(_root);
        AddLogicalChild(_root);
        _root.SetResourceReference(TextElement.FontFamilyProperty, "ArcWpf.Font.Body");
        _root.SetValue(TextElement.FontSizeProperty, 14d);
        _empty.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _groups.ColumnDefinitions.Add(new ColumnDefinition());
        AutomationProperties.SetName(this, "Keyboard shortcuts");
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, e) => { if ((e.NewSize.Width >= 520) != _twoColumns) Layout(); };
        Rebuild();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _root;
    protected override Size MeasureOverride(Size constraint) { _root.Measure(constraint); return _root.DesiredSize; }
    protected override Size ArrangeOverride(Size arrangeBounds) { _root.Arrange(new Rect(arrangeBounds)); return arrangeBounds; }

    // Held keys come from the whole window in the tunnelling phase, so they still register while a focused recorder owns the chord.
    private void Attach()
    {
        Detach();
        if (!HighlightPressed) return;
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewKeyDown += WindowKeyDown;
        _window.PreviewKeyUp += WindowKeyUp;
        _window.Deactivated += WindowDeactivated;
    }

    private void Detach()
    {
        if (_window is null) return;
        _window.PreviewKeyDown -= WindowKeyDown;
        _window.PreviewKeyUp -= WindowKeyUp;
        _window.Deactivated -= WindowDeactivated;
        _window = null;
    }

    private void WindowKeyDown(object sender, KeyEventArgs e) { Shortcuts.Track(_held, e, true); Highlight(); }
    private void WindowKeyUp(object sender, KeyEventArgs e) { Shortcuts.Track(_held, e, false); Highlight(); }
    private void WindowDeactivated(object? sender, EventArgs e) { _held.Clear(); Highlight(); }

    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    private void Rebuild()
    {
        _root.Children.Clear();
        _views.Clear();
        _groups.Children.Clear();
        _searchRow = null;
        if (IsSearchable) _root.Children.Add(BuildSearch());
        _groups.Margin = new Thickness(0, IsSearchable ? 12 : 0, 0, 0);
        _root.Children.Add(_groups);
        _root.Children.Add(_empty);
        Filter(false);
    }

    // .searchRow: 36 tall, padding 0 6 0 12, radius 18, border → foreground while focused; search icon, input, match count, clear.
    private Border BuildSearch()
    {
        var edge = new SolidColorBrush(Colors.Gray);
        var row = new Border { Height = 36, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(12, 0, 6, 0), BorderBrush = edge };
        row.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        var grid = new Grid();
        foreach (var width in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var icon = Lucide.Glyph(Lucide.Search, 16);
        icon.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        icon.Margin = new Thickness(0, 0, 8, 0);
        grid.Children.Add(icon);
        var field = new Grid { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(field, 1);
        _placeholder = new TextBlock { Text = SearchPlaceholder, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _search = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), FocusVisualStyle = null, VerticalContentAlignment = VerticalAlignment.Center };
        _search.SetResourceReference(StyleProperty, "ArcWpf.TagInput.Editor");
        _search.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        AutomationProperties.SetName(_search, SearchPlaceholder);
        _search.TextChanged += (_, _) => Filter(true);
        _search.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _search.Text.Length > 0) { e.Handled = true; _search.Clear(); } };
        field.Children.Add(_placeholder);
        field.Children.Add(_search);
        grid.Children.Add(field);
        _count = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        _count.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
        Grid.SetColumn(_count, 2);
        grid.Children.Add(_count);
        row.Child = grid;
        void Edge(bool animate) => Tween.Color(edge, Tween.Resolve(row, row.IsKeyboardFocusWithin ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.Border"), Tween.Fast, animate && Full);
        row.IsKeyboardFocusWithinChanged += (_, _) => Edge(true);
        row.Loaded += (_, _) => Edge(false);
        _searchRow = row;
        return row;
    }

    private string Query => _search?.Text ?? "";

    private bool Matches(ShortcutListGroup group, ShortcutListItem item, IReadOnlyList<string> words)
    {
        if (words.Count == 0) return true;
        bool mac = Platform == ShortcutPlatform.Mac;
        var aliases = new Dictionary<string, string>
        {
            ["cmd"] = "command", ["command"] = "command", ["ctrl"] = "control", ["control"] = "control", ["opt"] = mac ? "option" : "alt", ["option"] = mac ? "option" : "alt",
            ["alt"] = mac ? "option" : "alt", ["win"] = "windows", ["esc"] = "escape", ["return"] = mac ? "return" : "enter",
        };
        var tokens = Shortcuts.Tokens(item.Shortcut, Platform);
        var haystack = $"{item.Label} {group.Label} {item.Keywords ?? ""} {string.Join(" ", tokens.Select(token => $"{token.Spoken} {token.Label}"))}".ToLowerInvariant();
        return words.All(word => haystack.Contains(aliases.GetValueOrDefault(word, word)) || haystack.Contains(word));
    }

    /// <summary>Search words: symbols read as their key names, split on spaces and plus signs.</summary>
    public static IReadOnlyList<string> SearchWords(string query) =>
        query.Trim().ToLowerInvariant().Replace("⌘", " cmd ").Replace("⇧", " shift ").Replace("⌥", " alt ").Replace("⌃", " ctrl ")
            .Split([' ', '+', '\t'], StringSplitOptions.RemoveEmptyEntries);

    private void Filter(bool animate)
    {
        animate &= IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
        bool full = animate && Full;
        var words = SearchWords(Query);
        var groups = Groups ?? [];
        int total = 0;
        foreach (var group in groups)
        {
            var items = group.Items.Where(item => Matches(group, item, words)).ToList();
            total += items.Count;
            if (!_views.TryGetValue(group.Label, out var view))
            {
                view = new GroupView(this, group.Label);
                _views[group.Label] = view;
                _groups.Children.Add(view);
            }
            view.Show(items, full, animate);
        }
        foreach (var label in _views.Keys.Where(label => groups.All(group => group.Label != label)).ToList()) { _groups.Children.Remove(_views[label]); _views.Remove(label); }
        Layout();
        if (_placeholder is not null) _placeholder.Visibility = Query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_count is not null) _count.Text = Query.Length > 0 ? $"{total} {(total == 1 ? "match" : "matches")}" : "";
        SetClear(Query.Length > 0, full);
        _empty.Text = $"No shortcuts match “{Query.Trim()}”";
        _empty.Visibility = total == 0 && groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Highlight();
    }

    // The clear button pops from scale .6 on the snappy spring and leaves in .1 s.
    private void SetClear(bool show, bool full)
    {
        if (_searchRow?.Child is not Grid grid) return;
        if (show && _clear is null)
        {
            _clear = new FieldIconButton(Lucide.X, 14, 24, 12) { RestFill = "FieldClear", HoverFill = "FieldSelection", Margin = new Thickness(8, 0, 0, 0), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
            AutomationProperties.SetName(_clear, "Clear search");
            _clear.Click += (_, _) => { _search?.Clear(); _search?.Focus(); };
            Grid.SetColumn(_clear, 3);
            grid.Children.Add(_clear);
            if (full)
            {
                var scale = (ScaleTransform)_clear.RenderTransform;
                scale.ScaleX = scale.ScaleY = .6;
                _clear.Opacity = 0;
                Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
                Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
                Tween.Spring(_clear, OpacityProperty, 1, Tween.Snappy);
            }
        }
        else if (!show && _clear is not null)
        {
            var leaving = _clear;
            _clear = null;
            leaving.IsHitTestVisible = false;
            if (!full) { grid.Children.Remove(leaving); return; }
            var scale = (ScaleTransform)leaving.RenderTransform;
            Tween.To(scale, ScaleTransform.ScaleXProperty, .6, .1, null);
            Tween.To(scale, ScaleTransform.ScaleYProperty, .6, .1, null);
            Tween.To(leaving, OpacityProperty, 0, .1, null, done: () => grid.Children.Remove(leaving));
        }
    }

    // Two columns 24 DIP apart once the list is at least 520 wide (a container query in the source); groups flow row by row.
    private void Layout()
    {
        _twoColumns = ActualWidth >= 520;
        int columns = _twoColumns ? 2 : 1;
        _groups.ColumnDefinitions.Clear();
        _groups.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) _groups.ColumnDefinitions.Add(new ColumnDefinition());
        int index = 0;
        foreach (var view in _views.Values)
        {
            if (!view.IsShown) { Grid.SetRow(view, 0); Grid.SetColumn(view, 0); view.Visibility = view.IsLeaving ? Visibility.Visible : Visibility.Collapsed; continue; }
            view.Visibility = Visibility.Visible;
            int row = index / columns, column = index % columns;
            while (_groups.RowDefinitions.Count <= row) _groups.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(view, row);
            Grid.SetColumn(view, column);
            view.Margin = new Thickness(column == 1 ? 4 : -8, 0, column == 0 && columns == 2 ? 4 : -8, 0);
            index++;
        }
    }

    private void Highlight()
    {
        var mods = _held.Where(Shortcuts.IsHeldModifier).ToList();
        foreach (var view in _views.Values) view.Highlight(_held, mods, Full);
    }

    /// <summary>A group: xs muted heading (12 above, 4 below), then rows that open and close their height on the smooth spring.</summary>
    private sealed class GroupView : StackPanel
    {
        private readonly ShortcutList _owner;
        private readonly StackPanel _rows = new();
        private readonly Dictionary<string, RowView> _views = [];
        private readonly Grid _clip = new() { ClipToBounds = true };

        public GroupView(ShortcutList owner, string label)
        {
            _owner = owner;
            _clip.Margin = new Thickness(8, 0, 8, 0);
            var heading = new TextBlock { Text = label, FontSize = 12, Padding = new Thickness(0, 12, 0, 4) };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            var stack = new StackPanel();
            stack.Children.Add(heading);
            stack.Children.Add(_rows);
            _clip.Children.Add(stack);
            Children.Add(_clip);
            AutomationProperties.SetName(this, label);
        }

        public bool IsShown { get; private set; } = true;
        public bool IsLeaving { get; private set; }

        public void Show(IReadOnlyList<ShortcutListItem> items, bool full, bool animate)
        {
            var labels = items.Select(item => item.Label).ToList();
            foreach (var (label, row) in _views.Where(pair => !labels.Contains(pair.Key)).ToList()) { _views.Remove(label); Collapse(row, _rows, full, animate); }
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (!_views.TryGetValue(item.Label, out var row))
                {
                    row = new RowView(item, _owner.Platform);
                    _views[item.Label] = row;
                    _rows.Children.Insert(Math.Min(index, _rows.Children.Count), row);
                    if (IsShown && IsLoaded) Expand(row, full, animate);
                }
            }
            bool show = items.Count > 0;
            if (show == IsShown) { Rule(); return; }
            IsShown = show;
            if (show) { IsLeaving = false; Expand(_clip, full, animate); }
            else if (full) { IsLeaving = true; Collapse(_clip, null, full, animate, () => { IsLeaving = false; _owner.Layout(); }); }
            else Tween.Set(_clip, HeightProperty, 0);
            Rule();
        }

        // Hairlines between rows; a lit row and the row after it drop theirs.
        private void Rule()
        {
            RowView? previous = null;
            foreach (var row in _rows.Children.OfType<RowView>().Where(row => !row.Leaving))
            {
                row.SetRule(previous is not null && !row.IsMatch && previous.IsMatch is false);
                previous = row;
            }
        }

        public void Highlight(HashSet<string> held, IReadOnlyList<string> mods, bool full)
        {
            foreach (var row in _views.Values) row.Highlight(held, mods, full);
            Rule();
        }

        private static void Expand(FrameworkElement element, bool full, bool animate)
        {
            element.BeginAnimation(HeightProperty, null);
            element.Height = double.NaN;
            if (!animate) { element.Opacity = 1; return; }
            if (!full) { element.Opacity = 0; Tween.To(element, OpacityProperty, 1, .1, null); return; }
            element.Measure(new Size(element.ActualWidth > 0 ? element.ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
            double to = element.DesiredSize.Height;
            element.Height = 0;
            element.Opacity = 0;
            Tween.Spring(element, OpacityProperty, 1, Tween.Smooth);
            Tween.Spring(element, HeightProperty, to, Tween.Smooth, done: () => Tween.Set(element, HeightProperty, double.NaN));
        }

        private static void Collapse(FrameworkElement element, Panel? owner, bool full, bool animate, Action? done = null)
        {
            if (element is RowView row) row.Leaving = true;
            void Remove() { owner?.Children.Remove(element); done?.Invoke(); }
            if (!animate) { if (owner is null) Tween.Set(element, HeightProperty, 0); Remove(); return; }
            if (!full) { Tween.To(element, OpacityProperty, 0, .1, null, done: () => { if (owner is null) Tween.Set(element, HeightProperty, 0); Remove(); }); return; }
            element.Height = element.ActualHeight;
            Tween.Spring(element, OpacityProperty, 0, Tween.Smooth);
            Tween.Spring(element, HeightProperty, 0, Tween.Smooth, done: Remove);
        }
    }

    /// <summary>A 36 DIP row: secondary label (ellipsis) and small caps; dims to .38 when a held modifier is not in it, lights when all its keys are down.</summary>
    private sealed class RowView : Border
    {
        private readonly Border _inner = new() { MinHeight = 36, Padding = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(10) };
        private readonly Border _rule = new() { Height = 1, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        private readonly SolidColorBrush _fill = new(Colors.Transparent), _ink = new(Colors.Gray);
        private readonly ShortcutKeys _keys;
        private readonly IReadOnlyList<string> _ids;

        public RowView(ShortcutListItem item, ShortcutPlatform platform)
        {
            ClipToBounds = true;
            Margin = new Thickness(-8, 0, -8, 0);
            _ids = Shortcuts.Tokens(item.Shortcut, platform).Select(token => token.Id).ToList();
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = item.Label, Foreground = _ink, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            grid.Children.Add(label);
            _keys = new ShortcutKeys { Platform = platform, Size = ControlSize.Small, Shortcut = item.Shortcut };
            Grid.SetColumn(_keys, 1);
            grid.Children.Add(_keys);
            _inner.Background = _fill;
            _inner.Child = grid;
            _rule.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
            var host = new Grid();
            host.Children.Add(_inner);
            host.Children.Add(_rule);
            Child = host;
            Loaded += (_, _) => Paint(false);
        }

        public bool Leaving { get; set; }
        public bool IsMatch { get; private set; }
        private bool _dim;

        public void SetRule(bool on) => _rule.Visibility = on ? Visibility.Visible : Visibility.Hidden;

        public void Highlight(HashSet<string> held, IReadOnlyList<string> mods, bool full)
        {
            bool match = held.Count > 0 && _ids.All(held.Contains);
            bool dim = mods.Count > 0 && !match && !mods.All(_ids.Contains);
            _keys.SetHeld(held);
            if (match == IsMatch && dim == _dim) return;
            IsMatch = match;
            _dim = dim;
            Paint(full);
            if (full) Tween.To(_inner, OpacityProperty, dim ? .38 : 1, Tween.Standard, ArcEasing.Standard);
            else Tween.Set(_inner, OpacityProperty, dim ? .38 : 1);
        }

        private void Paint(bool animate)
        {
            var fill = Tween.Resolve(this, "ArcWpf.Brush.AccentSubtle");
            if (!IsMatch) fill.A = 0;
            Tween.Color(_fill, fill, Tween.Fast, animate);
            Tween.Color(_ink, Tween.Resolve(this, IsMatch ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary"), Tween.Fast, animate);
        }
    }
}
