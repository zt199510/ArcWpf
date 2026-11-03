using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>An action in the command menu. <see cref="Icon"/> is an icon name; without it the row shows the command glyph.</summary>
public sealed class CommandMenuItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public string? Group { get; init; }
    public IReadOnlyList<string>? Keywords { get; init; }
    public string? Icon { get; init; }
    public string? Shortcut { get; init; }
}

public sealed class CommandMenuSelectedEventArgs(CommandMenuItem item) : EventArgs
{
    public CommandMenuItem Item { get; } = item;
}

// The search of command-palette.tsx: same scores and the same word boundaries.
internal static class CommandMenuSearch
{
    internal sealed record Ranked(CommandMenuItem Item, int Score, List<(int Start, int End)> Ranges, int Order);

    private static bool IsWordChar(char c) => char.IsLetter(c) || char.IsNumber(c);

    private static int WordStart(string text, string word)
    {
        for (int at = text.IndexOf(word, StringComparison.Ordinal); at != -1; at = at + 1 <= text.Length ? text.IndexOf(word, at + 1, StringComparison.Ordinal) : -1)
        {
            if (at == 0 || !IsWordChar(text[at - 1])) { return at; }
        }
        return -1;
    }

    // Letters typed in order, starting at a word, within a short span: "shpr" finds "Share project".
    private static List<(int Start, int End)>? Subsequence(string text, string word)
    {
        if (word.Length < 2) { return null; }
        for (int start = text.IndexOf(word[0]); start != -1; start = start + 1 <= text.Length ? text.IndexOf(word[0], start + 1) : -1)
        {
            if (start > 0 && IsWordChar(text[start - 1])) { continue; }
            var ranges = new List<(int Start, int End)> { (start, start + 1) };
            int at = start;
            for (int i = 1; i < word.Length && at != -1; i++)
            {
                at = at + 1 <= text.Length ? text.IndexOf(word[i], at + 1) : -1;
                if (at == -1) { break; }
                if (ranges[^1].End == at) { ranges[^1] = (ranges[^1].Start, at + 1); } else { ranges.Add((at, at + 1)); }
            }
            if (at != -1 && at - start < word.Length * 3) { return ranges; }
        }
        return null;
    }

    // Scores one word: the label counts most and a prefix ranks first. Descriptions and keywords match only at a word start, so "test" never finds "latest".
    private static (int Score, List<(int Start, int End)> Ranges)? ScoreWord(CommandMenuItem item, string label, string word)
    {
        int inLabel = WordStart(label, word);
        if (inLabel == 0) { return (100, [(0, word.Length)]); }
        if (inLabel > 0) { return (80, [(inLabel, inLabel + word.Length)]); }
        int inside = word.Length >= 3 ? label.IndexOf(word, StringComparison.Ordinal) : -1;
        if (inside > 0) { return (50, [(inside, inside + word.Length)]); }
        if (item.Keywords?.Any(keyword => WordStart(keyword.ToLowerInvariant(), word) != -1) == true) { return (40, []); }
        if (item.Description is not null && WordStart(item.Description.ToLowerInvariant(), word) != -1) { return (25, []); }
        var loose = Subsequence(label, word);
        return loose is not null ? (10, loose) : null;
    }

    // Every word has to match somewhere; the best matches come first, ties keep the order they were given in.
    public static List<Ranked> Rank(IReadOnlyList<CommandMenuItem> items, string query)
    {
        string[] words = query.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) { return items.Select((item, order) => new Ranked(item, 0, [], order)).ToList(); }
        var ranked = new List<Ranked>();
        for (int order = 0; order < items.Count; order++)
        {
            var item = items[order];
            string label = item.Label.ToLowerInvariant();
            int score = 0;
            var ranges = new List<(int Start, int End)>();
            bool matched = true;
            foreach (string word in words)
            {
                var match = ScoreWord(item, label, word);
                if (match is null) { matched = false; break; }
                score += match.Value.Score;
                ranges.AddRange(match.Value.Ranges);
            }
            if (matched) { ranked.Add(new Ranked(item, score, ranges, order)); }
        }
        return ranked.OrderByDescending(entry => entry.Score).ThenBy(entry => entry.Order).ToList();
    }

    // "⌘ K" or "Shift+D" becomes one keycap per key.
    public static string[] KeysOf(string shortcut) =>
        System.Text.RegularExpressions.Regex.Split(shortcut, @"\s+|\+(?=.)").Where(key => key.Length > 0).ToArray();
}

/// <summary>
/// A keyboard driven action surface: search, grouped results and shortcuts (Arc's Command palette). It renders inline;
/// put it in a dialog if it should trap focus. Ctrl+K focuses the search from anywhere in the window.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class CommandMenu : Control
{
    private const double GLIDE_ROWS = 6, MAX_LIST = 342, ROW = 38, SNAPPY = .26, SMOOTH = .4, STANDARD = .24, INSTANT = .12, FAST = .16;

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(CommandMenu), new PropertyMetadata("Search commands", (d, _) => ((CommandMenu)d).RefreshSearch(false)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(CommandMenu), new PropertyMetadata("Command palette", (d, _) => ((CommandMenu)d).RefreshSearch(false)));
    public static readonly DependencyProperty AutoFocusSearchProperty = DependencyProperty.Register(nameof(AutoFocusSearch), typeof(bool), typeof(CommandMenu), new PropertyMetadata(false));
    public static readonly DependencyProperty ShowsCloseProperty = DependencyProperty.Register(nameof(ShowsClose), typeof(bool), typeof(CommandMenu), new PropertyMetadata(false, (d, _) => ((CommandMenu)d).RefreshSearch(false)));

    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool AutoFocusSearch { get => (bool)GetValue(AutoFocusSearchProperty); set => SetValue(AutoFocusSearchProperty, value); }
    /// <summary>Shows the Esc button. Escape on an empty search raises <see cref="Closed"/> either way.</summary>
    public bool ShowsClose { get => (bool)GetValue(ShowsCloseProperty); set => SetValue(ShowsCloseProperty, value); }

    public IList<CommandMenuItem> Items { get; } = new List<CommandMenuItem>();
    public event EventHandler<CommandMenuSelectedEventArgs>? ItemSelected;
    public event EventHandler? Closed;

    private Decorator? _root;
    private Border _palette = null!, _searchRow = null!;
    private TextBox _input = null!;
    private TextBlock _placeholder = null!;
    private Grid _clearSlot = null!;
    private ArcChromeButton? _clear;
    private ArcChromeButton _close = null!;
    private Border _commandKey = null!;
    private StackPanel _keyRow = null!;
    private Grid _frame = null!;
    private OverlayScrollViewer _scroll = null!;
    private Canvas _highlightLayer = null!;
    private Border _highlight = null!;
    private StackPanel _list = null!;
    private Border? _empty;
    private readonly TranslateTransform _highlightY = new();
    private readonly SpringDriver _driver;
    private readonly ControlSpring _y, _rowHeight, _frameHeight;
    private readonly OwnedMotion _motion = new();
    private readonly OwnedMotion _rowMotion = new();
    private readonly OwnedMotion _chrome = new();
    private readonly Dictionary<string, RowEntry> _rows = [];
    private readonly Dictionary<string, GroupEntry> _groups = [];
    private readonly List<CommandMenuItem> _filtered = [];
    private int? _activeIndex;
    private bool _highlightShown;
    private Point _lastPointer = new(-1, -1);
    private bool _narrow;
    private bool _entered;

    private sealed class RowEntry(ResultRow row) { public ResultRow Row { get; } = row; public double OldTop { get; set; } }
    private sealed class GroupEntry(StackPanel panel, FrameworkElement heading) { public StackPanel Panel { get; } = panel; public FrameworkElement Heading { get; } = heading; }

    static CommandMenu() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CommandMenu), new FrameworkPropertyMetadata(typeof(CommandMenu)));

    public CommandMenu()
    {
        _driver = new SpringDriver(ApplySprings);
        _y = _driver.Add(0, SNAPPY);
        _rowHeight = _driver.Add(0, SNAPPY);
        _frameHeight = _driver.Add(0, SMOOTH);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
    private bool Animates => IsLoaded && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = Build();
        RefreshSearch(false);
        Rebuild(false);
    }

    /// <summary>Focuses the search field.</summary>
    public void FocusSearch() => _input?.Focus();

    /// <summary>The current query. Setting it filters as typing does.</summary>
    public string Query { get => _input?.Text ?? ""; set { if (_input is not null) { _input.Text = value; } } }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.PreviewKeyDown -= OnWindowKey;
            window.PreviewKeyDown += OnWindowKey;
            // These are media queries in the registry: the window decides, not the palette.
            window.SizeChanged -= OnWindowSize;
            window.SizeChanged += OnWindowSize;
            ApplyNarrow(window.ActualWidth);
        }
        Rebuild(false);
        if (!_entered)
        {
            _entered = true;
            Entrance();
        }
        if (AutoFocusSearch) { _input.Focus(); }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window) { window.PreviewKeyDown -= OnWindowKey; window.SizeChanged -= OnWindowSize; }
        _driver.Finish();
        _motion.Stop();
    }

    private void OnWindowSize(object sender, SizeChangedEventArgs e) => ApplyNarrow(e.NewSize.Width);

    private void OnWindowKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && _input is not null)
        {
            e.Handled = true;
            _input.Focus();
        }
    }

    // The panel rises in on a spring.
    private void Entrance()
    {
        if (!Animates || Reduced || _palette is null) { return; }
        var rise = new TranslateTransform(0, 6);
        var scale = new ScaleTransform(.98, .98);
        _palette.RenderTransformOrigin = new Point(.5, 0);
        _palette.RenderTransform = new TransformGroup { Children = { scale, rise } };
        _palette.Opacity = 0;
        _motion.Double(_palette, OpacityProperty, 1, FAST, true, enter: true);
        _motion.Double(rise, TranslateTransform.YProperty, 0, SMOOTH, true, spring: true);
        _motion.Double(scale, ScaleTransform.ScaleXProperty, 1, SMOOTH, true, spring: true);
        _motion.Double(scale, ScaleTransform.ScaleYProperty, 1, SMOOTH, true, spring: true);
    }

    private UIElement Build()
    {
        var searchIcon = ArcText.Icon("search", 18, Brushes.Transparent);
        searchIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextMuted");
        var iconSlot = new Border { Width = 22, Height = 22, Child = searchIcon, VerticalAlignment = VerticalAlignment.Center };

        _input = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, FocusVisualStyle = null, Margin = new Thickness(-2, 0, 0, 0) };
        _input.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _input.SetResourceReference(TextBoxBase.CaretBrushProperty, "ArcWpf.Brush.Foreground");
        _input.Template = BareTextBox();
        _input.TextChanged += (_, _) => OnQueryChanged();
        _input.PreviewKeyDown += OnInputKey;
        _input.GotKeyboardFocus += (_, _) => RefreshRow(true);
        _input.LostKeyboardFocus += (_, _) => RefreshRow(true);
        _placeholder = ArcText.Block("", 14, 19.6, "TextMuted");
        _placeholder.IsHitTestVisible = false;
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        var inputHost = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        inputHost.Children.Add(_placeholder);
        inputHost.Children.Add(_input);

        _clearSlot = new Grid { VerticalAlignment = VerticalAlignment.Center };
        _close = new ArcChromeButton(7, 1) { Height = 24, MinWidth = 27, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = true };
        var closeText = new TextBlock { Text = "Esc", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = _close.Ink };
        _close.Content = closeText;
        _close.Roles = button => (button.IsMouseOver || button.IsPressed ? "Surface" : null, "BorderSubtle", button.IsMouseOver || button.IsPressed ? "Foreground" : "TextMuted");
        _close.Click += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        AutomationProperties.SetName(_close, "Close command palette");

        var keyText = ArcText.Block("⌘ K", 12, 12, "TextMuted", true);
        keyText.LineStackingStrategy = LineStackingStrategy.MaxHeight;
        keyText.ClearValue(TextBlock.LineHeightProperty);
        keyText.VerticalAlignment = VerticalAlignment.Center;
        keyText.HorizontalAlignment = HorizontalAlignment.Center;
        _commandKey = new Border { Height = 24, MinWidth = 38, Padding = new Thickness(6, 0, 6, 0), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), Child = keyText, VerticalAlignment = VerticalAlignment.Center };
        _commandKey.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _commandKey.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");

        _keyRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _keyRow.Children.Add(_clearSlot);
        _keyRow.Children.Add(_close);
        _keyRow.Children.Add(_commandKey);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(inputHost, 1);
        Grid.SetColumn(_keyRow, 2);
        row.Children.Add(iconSlot);
        row.Children.Add(inputHost);
        row.Children.Add(_keyRow);
        _searchRow = new Border { MinHeight = 54, Padding = new Thickness(14, 8, 14, 8), BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        _searchRow.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        _searchRow.Background = new SolidColorBrush(Colors.Transparent);

        _highlight = new Border { CornerRadius = new CornerRadius(8), Opacity = 0, RenderTransform = _highlightY, IsHitTestVisible = false };
        _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        _highlightLayer = new Canvas { IsHitTestVisible = false };
        _highlightLayer.Children.Add(_highlight);
        _list = new StackPanel();
        var listHost = new Grid { Margin = new Thickness(8, 6, 8, 6) };
        listHost.Children.Add(_highlightLayer);
        listHost.Children.Add(_list);
        _highlightLayer.SizeChanged += (_, e) => _highlight.Width = e.NewSize.Width;
        _scroll = new OverlayScrollViewer { Content = listHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = MAX_LIST, Focusable = false };
        _frame = new Grid { ClipToBounds = true, VerticalAlignment = VerticalAlignment.Top };
        _frame.Children.Add(_scroll);
        AutomationProperties.SetName(_frame, "Command results");

        var column = new StackPanel();
        column.Children.Add(_searchRow);
        column.Children.Add(_frame);
        var inner = new ClippedHost(25) { Child = column };
        _palette = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), Child = inner };
        _palette.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _palette.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _palette.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        ThemeWatch.Add(_palette, () => RefreshRow(false));
        return _palette;
    }

    private static ControlTemplate BareTextBox()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        host.SetValue(FocusableProperty, false);
        return new ControlTemplate(typeof(TextBox)) { VisualTree = host };
    }

    private void RefreshRow(bool animate)
    {
        var brush = (SolidColorBrush)_searchRow.Background;
        string role = _input.IsKeyboardFocused ? "SurfaceMuted" : "Surface";
        if (TryFindResource("ArcWpf.Brush." + role) is SolidColorBrush found)
        {
            _chrome.Color(brush, _input.IsKeyboardFocused ? found.Color : Transparentize(found.Color), FAST, animate && Animates);
        }
    }
    private static Color Transparentize(Color color) { color.A = 0; return color; }

    private void RefreshSearch(bool animate)
    {
        if (_input is null) { return; }
        _placeholder.Text = Placeholder;
        _placeholder.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(_input, Label);
        _close.Visibility = ShowsClose ? Visibility.Visible : Visibility.Collapsed;
        SetClear(_input.Text.Length > 0, animate);
        RefreshRow(false);
    }

    private void SetClear(bool show, bool animate)
    {
        if (show == (_clear is not null)) { return; }
        animate = animate && Animates;
        if (show)
        {
            var button = new ArcChromeButton(7) { Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) };
            button.Content = ArcText.Icon("x", 15, button.Ink, 2);
            button.Roles = b => (b.IsMouseOver || b.IsPressed ? "Surface" : null, null, b.IsMouseOver || b.IsPressed ? "Foreground" : "TextMuted");
            button.Click += (_, _) => { ClearQuery(); _input.Focus(); };
            AutomationProperties.SetName(button, "Clear search");
            _clear = button;
            _clearSlot.Children.Clear();
            _clearSlot.Children.Add(button);
            if (!animate) { return; }
            button.Opacity = 0;
            _motion.Double(button, OpacityProperty, 1, INSTANT, true);
            if (Reduced) { return; }
            var scale = new ScaleTransform(.6, .6);
            button.RenderTransformOrigin = new Point(.5, .5);
            button.RenderTransform = scale;
            _motion.Double(scale, ScaleTransform.ScaleXProperty, 1, SNAPPY, true, spring: true);
            _motion.Double(scale, ScaleTransform.ScaleYProperty, 1, SNAPPY, true, spring: true);
            ArcEffects.BlurIn(_motion, button, 2, INSTANT);
            return;
        }
        var leaving = _clear!;
        _clear = null;
        // popLayout: it leaves the flow at once, so the keys close the gap while it fades in place.
        _clearSlot.Children.Remove(leaving);
        if (!animate || Reduced) { return; }
        leaving.IsHitTestVisible = false;
        var ghost = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        Canvas.SetTop(leaving, -12);
        ghost.Children.Add(leaving);
        _clearSlot.Children.Add(ghost);
        var leave = new System.Windows.Media.Animation.KeySpline(.22, 1, .36, 1);
        var shrink = new ScaleTransform(1, 1);
        leaving.RenderTransformOrigin = new Point(.5, .5);
        leaving.RenderTransform = shrink;
        _motion.Double(leaving, OpacityProperty, 0, INSTANT, true, ease: leave, completed: () => _clearSlot.Children.Remove(ghost));
        _motion.Double(shrink, ScaleTransform.ScaleXProperty, .6, INSTANT, true, ease: leave);
        _motion.Double(shrink, ScaleTransform.ScaleYProperty, .6, INSTANT, true, ease: leave);
        ArcEffects.BlurOut(_motion, leaving, 2, INSTANT);
    }

    private void ApplyNarrow(double width)
    {
        bool narrow = width <= 480;
        if (narrow == _narrow) { return; }
        _narrow = narrow;
        _commandKey.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        _searchRow.Padding = narrow ? new Thickness(11, 8, 11, 8) : new Thickness(14, 8, 14, 8);
        Rebuild(false);
    }

    // ---- Filtering ----

    private void OnQueryChanged()
    {
        _activeIndex = null;
        RefreshSearch(true);
        Rebuild(true);
        string query = _input.Text.Trim();
        if (query.Length > 0) { Announcer.Say(this, _filtered.Count > 0 ? $"{_filtered.Count} {(_filtered.Count == 1 ? "result" : "results")}" : "No results"); }
    }

    private void ClearQuery()
    {
        _input.Text = "";
        _activeIndex = null;
    }

    // Groups follow their best match, and the list reads top to bottom in that order, so arrow keys walk what you see.
    private void Rebuild(bool animate)
    {
        if (_list is null) { return; }
        animate = animate && Animates && !Reduced;
        var grouped = new List<(string Group, List<CommandMenuSearch.Ranked> Entries)>();
        foreach (var entry in CommandMenuSearch.Rank(Items.ToList(), _input.Text))
        {
            string group = entry.Item.Group ?? "Actions";
            var existing = grouped.FirstOrDefault(pair => pair.Group == group);
            if (existing.Entries is null) { grouped.Add((group, [entry])); } else { existing.Entries.Add(entry); }
        }
        var before = _filtered.Select((item, index) => (item.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        _filtered.Clear();
        _filtered.AddRange(grouped.SelectMany(pair => pair.Entries).Select(entry => entry.Item));
        bool glide = animate && _filtered.Select((item, index) => Math.Abs(before.GetValueOrDefault(item.Id, index) - index)).All(shift => shift <= GLIDE_ROWS);

        // Where rows and groups sit now, for the glide.
        var oldTops = _rows.Where(pair => pair.Value.Row.IsLoaded).ToDictionary(pair => pair.Key, pair => pair.Value.Row.TranslatePoint(new Point(), _list).Y);
        var oldHeads = _groups.Where(pair => pair.Value.Heading.IsLoaded).ToDictionary(pair => pair.Key, pair => pair.Value.Heading.TranslatePoint(new Point(), _list).Y);

        var keepIds = _filtered.Select(item => item.Id).ToHashSet();
        var survivors = new Dictionary<string, RowEntry>();
        foreach (var (id, entry) in _rows) { if (keepIds.Contains(id)) { survivors[id] = entry; } }
        foreach (var (_, group) in _groups) { group.Panel.Children.Clear(); }
        _list.Children.Clear();
        var liveGroups = new Dictionary<string, GroupEntry>();
        int flat = 0;
        for (int g = 0; g < grouped.Count; g++)
        {
            var (name, entries) = grouped[g];
            if (!_groups.TryGetValue(name, out var group))
            {
                var heading = ArcText.Block(name, 12, 16.8, "TextMuted", true);
                heading.Margin = new Thickness(10, 8, 10, 4);
                group = new GroupEntry(new StackPanel(), heading);
            }
            liveGroups[name] = group;
            group.Panel.Children.Add(group.Heading);
            group.Panel.Margin = new Thickness(0, g > 0 ? 6 : 0, 0, 0);
            AutomationProperties.SetName(group.Panel, name);
            foreach (var entry in entries)
            {
                int index = flat++;
                if (!survivors.TryGetValue(entry.Item.Id, out var row))
                {
                    row = new RowEntry(new ResultRow(this, entry.Item));
                    survivors[entry.Item.Id] = row;
                    if (animate && !_rows.ContainsKey(entry.Item.Id))
                    {
                        row.Row.Opacity = 0;
                        _rowMotion.Double(row.Row, OpacityProperty, 1, STANDARD, true, enter: true);
                    }
                }
                row.Row.Index = index;
                row.Row.SetRanges(entry.Ranges);
                row.Row.SetNarrow(_narrow);
                group.Panel.Children.Add(row.Row);
            }
            _list.Children.Add(group.Panel);
        }
        _rows.Clear();
        foreach (var (id, entry) in survivors) { _rows[id] = entry; }
        _groups.Clear();
        foreach (var (name, group) in liveGroups) { _groups[name] = group; }

        _empty = null;
        if (_filtered.Count == 0) { _empty = BuildEmpty(); _list.Children.Add(_empty); }

        _scroll.UpdateLayout();
        double target = Math.Min(MAX_LIST, ListHeight());
        if (animate)
        {
            if (_frame.ActualHeight > 0) { _frameHeight.Snap(_driver.IsRunning ? _frameHeight.Position : _frame.ActualHeight); }
            _driver.Animate(_frameHeight, target, SMOOTH);
            ApplySprings();
        }
        else
        {
            _frameHeight.Snap(target);
            ApplySprings();
        }

        // Remaining rows glide into their place; a long jump snaps.
        if (glide)
        {
            foreach (var (id, entry) in _rows)
            {
                if (oldTops.TryGetValue(id, out double top)) { GlideFrom(entry.Row, top); }
            }
            foreach (var (name, group) in _groups)
            {
                if (oldHeads.TryGetValue(name, out double top)) { GlideFrom(group.Heading, top); }
            }
        }
        RefreshActive(animate);
    }

    private void GlideFrom(FrameworkElement element, double oldTop)
    {
        double now = element.TranslatePoint(new Point(), _list).Y;
        if (Math.Abs(now - oldTop) < .5) { return; }
        var shift = new TranslateTransform(0, oldTop - now);
        element.RenderTransform = shift;
        _rowMotion.Double(shift, TranslateTransform.YProperty, 0, SMOOTH, true, spring: true);
    }

    private double ListHeight()
    {
        var host = (FrameworkElement)_list.Parent;
        double width = _frame.ActualWidth > 0 ? _frame.ActualWidth : ActualWidth > 0 ? ActualWidth - 2 : 438;
        host.Measure(new Size(width, double.PositiveInfinity));
        return host.DesiredSize.Height;
    }

    private Border BuildEmpty()
    {
        var icon = ArcText.Icon("search", 20, Brushes.Transparent);
        icon.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextMuted");
        var iconBox = new Border { Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 9), Child = icon, HorizontalAlignment = HorizontalAlignment.Center };
        var title = ArcText.Block($"No results for “{_input.Text.Trim()}”", 14, 19.6, "Foreground", true);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var hint = ArcText.Block("Try a different word or clear the search.", 12, 16.8, "TextSecondary");
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Margin = new Thickness(0, 4, 0, 0);
        title.Margin = new Thickness(0, 0, 0, 0);
        var column = new StackPanel { Margin = new Thickness(12, 26, 12, 28) };
        column.Children.Add(iconBox);
        column.Children.Add(title);
        column.Children.Add(hint);
        var empty = new Border { Child = column };
        if (Animates && !Reduced)
        {
            var rise = new TranslateTransform(0, 6);
            empty.RenderTransform = rise;
            empty.Opacity = 0;
            _rowMotion.Double(rise, TranslateTransform.YProperty, 0, STANDARD, true, enter: true);
            _rowMotion.Double(empty, OpacityProperty, 1, STANDARD, true, enter: true);
            ArcEffects.BlurIn(_rowMotion, empty, 2, STANDARD);
        }
        return empty;
    }

    private void ApplySprings()
    {
        _highlightY.Y = _y.Position;
        _highlight.Height = Math.Max(0, _rowHeight.Position);
        _frame.Height = Math.Max(0, _frameHeight.Position);
    }

    // ---- Active row ----

    private int SafeActive => _filtered.Count > 0 ? Math.Min(_activeIndex ?? 0, _filtered.Count - 1) : -1;
    private CommandMenuItem? Active => SafeActive >= 0 ? _filtered[SafeActive] : null;

    // One highlight follows the active result on a snappy spring, for the pointer and arrow keys alike, and fades in when it first appears.
    private void RefreshActive(bool animate)
    {
        animate = animate && Animates;
        var active = Active;
        foreach (var entry in _rows.Values) { entry.Row.SetActive(ReferenceEquals(entry.Row.Item, active), animate); }
        if (active is null || !_rows.TryGetValue(active.Id, out var row))
        {
            _highlightShown = false;
            _motion.Double(_highlight, OpacityProperty, 0, .1, animate && !Reduced);
            return;
        }
        _list.UpdateLayout();
        double top = row.Row.TranslatePoint(new Point(), _list).Y;
        double height = row.Row.ActualHeight > 0 ? row.Row.ActualHeight : ROW;
        // A glide transform on the row is not part of its place in the list.
        if (row.Row.RenderTransform is TranslateTransform glide) { top -= glide.Y; }
        if (!_highlightShown || !animate || Reduced)
        {
            _y.Snap(top);
            _rowHeight.Snap(height);
            ApplySprings();
        }
        else
        {
            _driver.Animate(_y, top, SNAPPY);
            _driver.Animate(_rowHeight, height, SNAPPY);
        }
        _motion.Double(_highlight, OpacityProperty, 1, .08, animate && !Reduced);
        _highlightShown = true;
        row.Row.BringIntoView();
        AutomationProperties.SetHelpText(_input, active.Label);
    }

    private void SetActive(int index)
    {
        _activeIndex = index;
        RefreshActive(true);
    }

    internal void PointerMoved(ResultRow row, MouseEventArgs e)
    {
        var point = e.GetPosition(null);
        // Ignore the synthetic moves sent while the list scrolls under a still pointer.
        if (point == _lastPointer) { return; }
        _lastPointer = point;
        if (row.Index != SafeActive) { SetActive(row.Index); }
    }

    internal void Choose(CommandMenuItem item)
    {
        ItemSelected?.Invoke(this, new CommandMenuSelectedEventArgs(item));
        _input.Text = "";
        _activeIndex = null;
        RefreshActive(true);
        _input.Focus();
    }

    private void OnInputKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: e.Handled = true; SetActive(Math.Min(SafeActive + 1, Math.Max(_filtered.Count - 1, 0))); break;
            case Key.Up: e.Handled = true; SetActive(Math.Max(SafeActive - 1, 0)); break;
            case Key.Home: e.Handled = true; SetActive(0); break;
            case Key.End: e.Handled = true; SetActive(Math.Max(_filtered.Count - 1, 0)); break;
            case Key.Enter:
                // Enter runs the highlighted result; after typing, with nothing highlighted yet, it runs the top match.
                if (Active is { } target) { e.Handled = true; Choose(target); }
                break;
            case Key.Escape:
                e.Handled = true;
                if (_input.Text.Length > 0) { ClearQuery(); }
                else { Closed?.Invoke(this, EventArgs.Empty); }
                break;
        }
    }

    internal int ActiveIndexForTest => SafeActive;
    internal IReadOnlyList<CommandMenuItem> FilteredForTest => _filtered;
    internal double FrameHeightTarget => _frameHeight.Target;
    internal bool HasEmptyState => _empty is not null;
    internal bool HasClearButton => _clear is not null;
    internal void FinishForTest() { _driver.Finish(); }

    // ---- Rows ----

    internal sealed class ResultRow : System.Windows.Controls.Button
    {
        private readonly CommandMenu _owner;
        private readonly TextBlock _label;
        private readonly ArcDataGridIcon _icon;
        private readonly TextBlock? _description;
        private readonly FrameworkElement? _shortcut;
        private readonly SolidColorBrush _iconInk = new();
        private readonly OwnedMotion _motion = new();
        private bool _active;
        public CommandMenuItem Item { get; }
        public int Index { get; set; }

        public ResultRow(CommandMenu owner, CommandMenuItem item)
        {
            _owner = owner;
            Item = item;
            OverridesDefaultStyle = true;
            Height = ROW;
            Focusable = false;
            Cursor = Cursors.Hand;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.SetValue(BackgroundProperty, Brushes.Transparent);
            chrome.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = chrome };

            _icon = ArcText.Icon(item.Icon ?? "command", 16, _iconInk);
            var iconSlot = new Border { Width = 16, Height = 16, Child = _icon, VerticalAlignment = VerticalAlignment.Center };

            _label = ArcText.Block("", 14, 19.6);
            _label.TextTrimming = TextTrimming.CharacterEllipsis;
            _label.VerticalAlignment = VerticalAlignment.Center;
            var copy = new CommandRowCopy(_label);
            if (item.Description is { Length: > 0 } description)
            {
                _description = ArcText.Block(description, 12, 16.8, "TextSecondary");
                _description.TextTrimming = TextTrimming.CharacterEllipsis;
                copy.Description = _description;
            }
            Grid.SetColumn(copy, 1);
            var grid = new Grid { Margin = new Thickness(10, 0, 10, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            copy.Margin = new Thickness(10, 0, 10, 0);
            grid.Children.Add(iconSlot);
            grid.Children.Add(copy);
            if (item.Shortcut is { Length: > 0 } shortcut)
            {
                var keys = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (string key in CommandMenuSearch.KeysOf(shortcut))
                {
                    var keyText = new TextBlock { Text = key, FontSize = 11, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    keyText.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                    var cap = new Border { MinWidth = 20, Height = 20, Padding = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Child = keyText, Margin = new Thickness(keys.Children.Count > 0 ? 3 : 0, 0, 0, 0) };
                    cap.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
                    cap.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
                    keys.Children.Add(cap);
                }
                _shortcut = keys;
                Grid.SetColumn(keys, 2);
                grid.Children.Add(keys);
            }
            Content = grid;
            AutomationProperties.SetName(this, item.Label);
            Click += (_, _) => _owner.Choose(item);
            MouseMove += (_, e) => _owner.PointerMoved(this, e);
            Loaded += (_, _) => RefreshIcon(false);
            ThemeWatch.Add(this, () => RefreshIcon(false));
            SetRanges([]);
        }

        public void SetNarrow(bool narrow)
        {
            if (_shortcut is not null) { _shortcut.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible; }
        }

        public void SetActive(bool active, bool animate)
        {
            if (active == _active && animate) { return; }
            _active = active;
            RefreshIcon(animate);
            AutomationProperties.SetItemStatus(this, active ? "selected" : "");
        }

        private void RefreshIcon(bool animate)
        {
            if (_owner.TryFindResource("ArcWpf.Brush." + (_active ? "Foreground" : "TextMuted")) is SolidColorBrush brush)
            {
                _motion.Color(_iconInk, brush.Color, FAST, animate);
            }
        }

        // The label with its matched letters in medium weight.
        public void SetRanges(IReadOnlyList<(int Start, int End)> ranges)
        {
            _label.Inlines.Clear();
            string text = Item.Label;
            if (ranges.Count == 0) { _label.Inlines.Add(new Run(text)); return; }
            var marked = new bool[text.Length];
            foreach (var (start, end) in ranges) { for (int i = start; i < end && i < text.Length; i++) { marked[i] = true; } }
            for (int i = 0; i < text.Length;)
            {
                int end = i;
                while (end < text.Length && marked[end] == marked[i]) { end++; }
                var run = new Run(text[i..end]);
                if (marked[i]) { run.FontWeight = FontWeights.Medium; }
                _label.Inlines.Add(run);
                i = end;
            }
        }
    }

    // flex with baseline alignment: the label keeps its width, the description takes what is left and trims.
    private sealed class CommandRowCopy : Panel
    {
        private readonly TextBlock _label;
        private TextBlock? _description;
        private const double GAP = 8;

        public CommandRowCopy(TextBlock label)
        {
            _label = label;
            Children.Add(label);
            VerticalAlignment = VerticalAlignment.Center;
        }

        public TextBlock Description { set { _description = value; Children.Add(value); } }

        protected override Size MeasureOverride(Size available)
        {
            _label.Measure(new Size(available.Width, double.PositiveInfinity));
            double used = _label.DesiredSize.Width;
            if (_description is not null)
            {
                double room = Math.Max(0, available.Width - used - GAP);
                _description.Measure(new Size(room, double.PositiveInfinity));
                used += GAP + _description.DesiredSize.Width;
            }
            return new Size(double.IsInfinity(available.Width) ? used : Math.Min(used, available.Width), 19.8);
        }

        protected override Size ArrangeOverride(Size size)
        {
            double width = Math.Min(_label.DesiredSize.Width, size.Width);
            _label.Arrange(new Rect(0, 0, width, 19.6));
            // align-items: baseline puts the 12 line 3 lower than the 14 line.
            _description?.Arrange(new Rect(width + GAP, 3, Math.Max(0, size.Width - width - GAP), 16.8));
            return size;
        }
    }
}
