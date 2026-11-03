using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Collections.Specialized;
using System.Windows.Automation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public class EmojiPicker : Control
{
    private static readonly string[] SkinToneValues = ["", "🏻", "🏼", "🏽", "🏾", "🏿"];
    private static readonly HashSet<string> ToneCapable = ["👋", "🤚", "🖐️", "✋", "🖖", "👌", "🤏", "✌️", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "👇", "☝️", "✍️", "👏", "🙏", "👍", "👎", "👊", "✊", "🤝", "💪", "👐", "🙌", "💅", "🫶"];
    private static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["smile"] = ["😀", "😃", "😄", "😁", "😆", "😅", "😂", "🙂", "🙃", "😉", "😊"],
        ["love"] = ["😍", "❤️"], ["heart"] = ["❤️"], ["fire"] = ["🔥"], ["party"] = ["🎉"], ["rocket"] = ["🚀"], ["idea"] = ["💡"], ["thumb"] = ["👍"], ["clap"] = ["👏"], ["pray"] = ["🙏"]
    };
    private static readonly IReadOnlyDictionary<string, string> EmojiNames = EmojiCatalog.Items.ToDictionary(item => item.Emoji, item => item.Name, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, HashSet<string>> CategoryValues = EmojiCatalog.Items.GroupBy(item => item.Group, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Select(item => item.Emoji).ToHashSet(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false, (d, _) => ((EmojiPicker)d).OnOpenChanged()));
    public static readonly DependencyProperty SelectedEmojiProperty = DependencyProperty.Register(nameof(SelectedEmoji), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty EmojisProperty = DependencyProperty.Register(nameof(Emojis), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null, OnEmojisChanged));
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(EmojiPicker), new PropertyMetadata("Search emoji"));
    public static readonly DependencyProperty AutoFocusProperty = DependencyProperty.Register(nameof(AutoFocus), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false));
    public static readonly DependencyProperty RecentEmojisProperty = DependencyProperty.Register(nameof(RecentEmojis), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null));
    public static readonly DependencyProperty MaxRecentProperty = DependencyProperty.Register(nameof(MaxRecent), typeof(int), typeof(EmojiPicker), new PropertyMetadata(27));
    public static readonly DependencyProperty DefaultRecentProperty = DependencyProperty.Register(nameof(DefaultRecent), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null, OnDefaultRecentChanged));
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(EmojiPicker), new PropertyMetadata(9, OnColumnsChanged, CoerceColumns));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(EmojiPicker), new PropertyMetadata("Emoji picker"));
    public static readonly DependencyProperty SelectedCategoryProperty = DependencyProperty.Register(nameof(SelectedCategory), typeof(string), typeof(EmojiPicker), new PropertyMetadata("All", OnCategoryChanged));
    public static readonly DependencyProperty SkinToneProperty = DependencyProperty.Register(nameof(SkinTone), typeof(int), typeof(EmojiPicker), new FrameworkPropertyMetadata(0, OnSkinToneChanged, CoerceSkinTone));
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string SelectedEmoji { get => (string)GetValue(SelectedEmojiProperty); set => SetValue(SelectedEmojiProperty, value); }
    public ObservableCollection<string> Emojis { get => (ObservableCollection<string>)GetValue(EmojisProperty); set => SetValue(EmojisProperty, value); }
    public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public string SearchPlaceholder { get => (string)GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    public bool AutoFocus { get => (bool)GetValue(AutoFocusProperty); set => SetValue(AutoFocusProperty, value); }
    public ObservableCollection<string> RecentEmojis { get => (ObservableCollection<string>)GetValue(RecentEmojisProperty); set => SetValue(RecentEmojisProperty, value); }
    public int MaxRecent { get => (int)GetValue(MaxRecentProperty); set => SetValue(MaxRecentProperty, value); }
    public ObservableCollection<string> DefaultRecent { get => (ObservableCollection<string>)GetValue(DefaultRecentProperty); set => SetValue(DefaultRecentProperty, value); }
    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string SelectedCategory { get => (string)GetValue(SelectedCategoryProperty); set => SetValue(SelectedCategoryProperty, value); }
    /// <summary>当前肤色：0 为默认黄色，1 至 5 对应 Unicode Fitzpatrick 修饰符。</summary>
    public int SkinTone { get => (int)GetValue(SkinToneProperty); set => SetValue(SkinToneProperty, value); }
    /// <summary>Replaces the selected emoji on the trigger, for hosts that open the picker from an icon.</summary>
    public object? TriggerContent { get => GetValue(TriggerContentProperty); set => SetValue(TriggerContentProperty, value); }
    public static readonly DependencyProperty TriggerContentProperty = DependencyProperty.Register(nameof(TriggerContent), typeof(object), typeof(EmojiPicker), new PropertyMetadata(null, (d, _) => ((EmojiPicker)d).UpdateTrigger()));
    /// <summary>The colour font drawn in the grid. Emoji fall back to plain glyphs when it has no colour layers.</summary>
    public FontFamily EmojiFontFamily { get => (FontFamily)GetValue(EmojiFontFamilyProperty); set => SetValue(EmojiFontFamilyProperty, value); }
    public static readonly DependencyProperty EmojiFontFamilyProperty = DependencyProperty.Register(nameof(EmojiFontFamily), typeof(FontFamily), typeof(EmojiPicker), new PropertyMetadata(new FontFamily("Segoe UI Emoji"), (d, _) => ((EmojiPicker)d).OnFontChanged()));
    public event EventHandler<string>? EmojiSelected;
    public event EventHandler<int>? SkinToneChanged;
    public void SelectEmoji(string emoji) => Pick(emoji, keepOpen: false);
    private void Pick(string emoji, bool keepOpen)
    {
        if (string.IsNullOrWhiteSpace(emoji)) return;
        var selected = ApplySkinTone(emoji);
        SelectedEmoji = selected;
        RecordRecent(selected);
        EmojiSelected?.Invoke(this, selected);
        UpdateTrigger();
        if (!keepOpen) IsOpen = false;
    }
    private static void OnEmojisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).AttachEmojiSource(e.NewValue as ObservableCollection<string>);
    private static void OnCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).ScrollToCategory();
    private static void OnDefaultRecentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).Build();
    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).Build();
    private static object CoerceColumns(DependencyObject d, object value) => Math.Clamp((int)value, 6, 12);
    private static void OnSkinToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var picker = (EmojiPicker)d; picker.Build(); picker.UpdateToneVisuals(); picker.SkinToneChanged?.Invoke(picker, picker.SkinTone); }
    private static object CoerceSkinTone(DependencyObject d, object value) => Math.Clamp((int)value, 0, SkinToneValues.Length - 1);
    private const string Waving = "👋", ToneBase = "✋";
    private const double RowHeight = 37.11, HeaderHeight = 30, ListWidth = 334, ToneMenuWidth = 326;
    private static readonly (string Id, string[] Groups, string Title, string Icon)[] Tabs =
    [
        ("All", [], "Popular", "M12,2 a10,10 0 1 0 0,20 a10,10 0 1 0 0,-20 M12,6 v6 l4,2"),
        ("Smileys", ["Smileys", "People"], "Smileys and people", "M12,2 a10,10 0 1 0 0,20 a10,10 0 1 0 0,-20 M8,14 s1.5,2,4,2,4,-2,4,-2 M9,9 h.01 M15,9 h.01"),
        ("Animals", ["Animals"], "Animals and nature", "M11,2 a2,2 0 1 0 0,4 a2,2 0 1 0 0,-4 M18,6 a2,2 0 1 0 0,4 a2,2 0 1 0 0,-4 M20,14 a2,2 0 1 0 0,4 a2,2 0 1 0 0,-4 M9,10 a5,5,0,0,1,5,5 v3.5 a3.5,3.5,0,0,1,-6.84,1.045 Q6.52,17.48,4.46,16.84 A3.5,3.5,0,0,1,5.5,10 Z"),
        ("Food", ["Food"], "Food and drink", "M10,2 v2 M14,2 v2 M16,8 a1,1,0,0,1,1,1 v8 a4,4,0,0,1,-4,4 H7 a4,4,0,0,1,-4,-4 V9 a1,1,0,0,1,1,-1 h14 a4,4,0,1,1,0,8 h-1 M6,2 v2"),
        ("Activities", ["Activities"], "Activities", "M12,2 a10,10 0 1 0 0,20 a10,10 0 1 0 0,-20 M11.1,7.1 a16.55,16.55,0,0,1,10.9,4 M12,12 a12.6,12.6,0,0,1,-8.7,5 M16.8,13.6 a16.55,16.55,0,0,1,-9,7.5 M20.7,17 a12.8,12.8,0,0,0,-8.7,-5 a13.3,13.3,0,0,1,.4,-10 M6.6,3.3 a16.6,16.6,0,0,0,1.5,10.7"),
        ("Travel", ["Travel"], "Travel and places", "M17.8,19.2 L16,11 l3.5,-3.5 C21,6,21.5,4,21,3 c-1,-.5,-3,0,-4.5,1.5 L13,8 L4.8,6.2 c-.5,-.1,-.9,.1,-1.1,.5 l-.3,.5 c-.2,.5,-.1,1,.3,1.3 L9,12 l-2,3 H4 l-1,1 l3,2 l2,3 l1,-1 v-3 l3,-2 l3.5,5.3 c.3,.4,.8,.5,1.3,.3 l.5,-.2 c.4,-.3,.6,-.7,.5,-1.2 Z"),
        ("Objects", ["Objects"], "Objects", "M15,14 c.2,-1,.7,-1.7,1.5,-2.5 c1,-.9,1.5,-2.2,1.5,-3.5 A6,6,0,0,0,6,8 c0,1,.2,2.2,1.5,3.5 c.7,.7,1.3,1.5,1.5,2.5 M9,18 h6 M10,22 h4"),
        ("Symbols", ["Symbols"], "Symbols", "M8.3,10 a.7,.7,0,0,1,-.626,-1.079 L11.4,3 a.7,.7,0,0,1,1.198,-.043 L16.3,8.9 a.7,.7,0,0,1,-.572,1.1 Z M4,14 h6 a1,1,0,0,1,1,1 v6 a1,1,0,0,1,-1,1 h-6 a1,1,0,0,1,-1,-1 v-6 a1,1,0,0,1,1,-1 Z M17.5,14 a3.5,3.5 0 1 0 0,7 a3.5,3.5 0 1 0 0,-7"),
        ("Flags", ["Flags"], "Flags", "M4,22 V4 a1,1,0,0,1,.4,-.8 A6,6,0,0,1,8,2 c3,0,5,2,7.333,2 q2,0,3.067,-.8 A1,1,0,0,1,20,4 v10 a1,1,0,0,1,-.4,.8 A6,6,0,0,1,16,16 c-3,0,-5,-2,-8,-2 a8,8,0,0,0,-4,1"),
    ];
    private Popup? _popup; private ItemsControl? _grid; private Panel? _categories; private Panel? _tones; private TextBox? _search; private INotifyCollectionChanged? _emojiSource;
    private ButtonBase? _toggle, _toneButton; private FrameworkElement? _indicator, _toneMenu; private ScrollViewer? _scroller; private ColorEmojiGlyph? _previewGlyph; private TextBlock? _previewName, _placeholder, _empty; private int _activeTab = -1; private bool _toneOpen;
    private readonly List<double> _sectionTops = []; private List<EmojiRow> _rows = [];
    static EmojiPicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(EmojiPicker), new FrameworkPropertyMetadata(typeof(EmojiPicker)));
    public EmojiPicker()
    {
        Emojis = new ObservableCollection<string>(EmojiCatalog.Items.Select(item => item.Emoji));
        // Arc 的触发按钮使用一个稳定、可识别的默认表情，而不是依赖 Unicode 目录排序。
        SelectedEmoji = Emojis.Contains("😀") ? "😀" : Emojis[0]; RecentEmojis = new ObservableCollection<string>();
        DefaultRecent = new ObservableCollection<string>(["👍", "❤️", "😂", "🎉", "🙏", "🔥", "😊", "👀", "✅", "🙌", "💯", "😍", "🤔", "👏", "🚀", "😅", "🥳", "👋"]);
        PreviewKeyDown += OnPreviewKeyDown;
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_toggle is not null) _toggle.Click -= OnToggle;
        _toggle = GetTemplateChild("PART_Toggle") as ButtonBase;
        if (_toggle is not null) _toggle.Click += OnToggle;
        _popup = GetTemplateChild("PART_Popup") as Popup; _grid = GetTemplateChild("PART_Grid") as ItemsControl; _categories = GetTemplateChild("PART_Categories") as Panel; _tones = GetTemplateChild("PART_Tones") as Panel; _search = GetTemplateChild("PART_Search") as TextBox;
        _toneButton = GetTemplateChild("PART_ToneButton") as ButtonBase; _toneMenu = GetTemplateChild("PART_ToneMenu") as FrameworkElement; _indicator = GetTemplateChild("PART_TabIndicator") as FrameworkElement; if (_indicator is not null) _indicator.RenderTransform = new TranslateTransform();
        _previewGlyph = GetTemplateChild("PART_PreviewGlyph") as ColorEmojiGlyph; if (_previewGlyph is not null) _previewGlyph.EmojiFamily = EmojiFontFamily; _previewName = GetTemplateChild("PART_PreviewName") as TextBlock; _placeholder = GetTemplateChild("PART_Placeholder") as TextBlock; _empty = GetTemplateChild("PART_Empty") as TextBlock;
        if (_popup is not null) { _popup.DataContext = this; _popup.CustomPopupPlacementCallback = PlacePopup; }
        if (_search is not null) _search.TextChanged += (_, _) => { SearchText = _search.Text; if (_placeholder is not null) _placeholder.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed; Build(); };
        if (_toneButton is not null) _toneButton.Click += (_, _) => SetToneOpen(!_toneOpen);
        if (_grid is not null) { _grid.ItemTemplate = RowTemplate; _grid.Loaded += (_, _) => AttachScroller(); }
        AttachEmojiSource(Emojis);
        BuildCategories(); BuildTones(); Build(); UpdateCategoryVisuals(); UpdateToneVisuals(); UpdateTrigger(); ShowPreview(null);
    }
    private void OnToggle(object sender, RoutedEventArgs e) => IsOpen = !IsOpen;
    private void UpdateTrigger() { if (_toggle is ContentControl toggle) toggle.Content = TriggerContent ?? Glyph(SelectedEmoji, 20); }
    internal ColorEmojiGlyph Glyph(string emoji, double size) => new() { Text = emoji, EmojiSize = size, EmojiFamily = EmojiFontFamily };
    private void OnFontChanged() { BuildTones(); UpdateTrigger(); Build(); ShowPreview(null); if (_previewGlyph is not null) _previewGlyph.EmojiFamily = EmojiFontFamily; }
    private void OnOpenChanged()
    {
        if (!IsOpen) { SetToneOpen(false); return; }
        Build();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { AttachScroller(); UpdateIndicator(false); if (AutoFocus) _search?.Focus(); });
    }
    private static readonly DataTemplate RowTemplate = new() { VisualTree = new FrameworkElementFactory(typeof(EmojiRowView)) };
    private void AttachScroller()
    {
        if (_scroller is not null || _grid is null) return;
        _scroller = FindChild<ScrollViewer>(_grid);
        if (_scroller is not null) _scroller.ScrollChanged += (_, _) => FollowScroll();
    }
    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
    private void AttachEmojiSource(ObservableCollection<string>? source)
    {
        if (_emojiSource is not null) _emojiSource.CollectionChanged -= OnEmojiCollectionChanged;
        _emojiSource = source;
        if (_emojiSource is not null) _emojiSource.CollectionChanged += OnEmojiCollectionChanged;
    }
    private void OnEmojiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Build();
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsOpen) return;
        if (e.Key == Key.Escape)
        {
            if (_toneOpen) { SetToneOpen(false); e.Handled = true; return; }
            if (!string.IsNullOrEmpty(SearchText)) { SearchText = string.Empty; _search?.Clear(); Build(); e.Handled = true; return; }
            IsOpen = false; _toggle?.Focus(); e.Handled = true; return;
        }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return;
        if (Keyboard.FocusedElement is ButtonBase { Parent: EmojiRowView view } cell)
        {
            int row = _rows.IndexOf(view.Row!), column = view.Children.IndexOf(cell);
            MoveFocus(row, column, e.Key);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _rows.FindIndex(item => item.Title is null) is >= 0 and var first) { FocusCell(first, 0); e.Handled = true; }
    }
    private void MoveFocus(int row, int column, Key key)
    {
        switch (key)
        {
            case Key.Left when column > 0: column--; break;
            case Key.Left: row = Step(row, -1); column = _rows[row].Emojis.Length - 1; break;
            case Key.Right when column < _rows[row].Emojis.Length - 1: column++; break;
            case Key.Right: row = Step(row, 1); column = 0; break;
            case Key.Up: row = Step(row, -1); break;
            case Key.Down: row = Step(row, 1); break;
            case Key.PageUp: for (int i = 0; i < 6; i++) row = Step(row, -1); break;
            case Key.PageDown: for (int i = 0; i < 6; i++) row = Step(row, 1); break;
            case Key.Home: column = 0; break;
            case Key.End: column = _rows[row].Emojis.Length - 1; break;
        }
        FocusCell(row, Math.Min(column, _rows[row].Emojis.Length - 1));
    }
    private int Step(int row, int direction)
    {
        for (int at = row + direction; at >= 0 && at < _rows.Count; at += direction) if (_rows[at].Title is null) return at;
        return row;
    }
    private void FocusCell(int row, int column)
    {
        if (_grid is null) return;
        var panel = FindChild<VirtualizingStackPanel>(_grid);
        panel?.BringIndexIntoViewPublic(row);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_grid.ItemContainerGenerator.ContainerFromIndex(row) is DependencyObject container && FindChild<EmojiRowView>(container) is { } view && column < view.Children.Count) view.Children[column].Focus();
        });
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        const double Gutter = 24, Gap = 6;
        double height = popupSize.Height - Gutter * 2;
        try
        {
            var screen = PointToScreen(new Point(0, ActualHeight));
            var below = screen.Y + height + Gap + 8 < SystemParameters.WorkArea.Bottom;
            return [new CustomPopupPlacement(new Point(-Gutter, below ? targetSize.Height + Gap - Gutter : -height - Gap - Gutter), PopupPrimaryAxis.Vertical)];
        }
        catch (InvalidOperationException) { return [new CustomPopupPlacement(new Point(-Gutter, targetSize.Height + Gap - Gutter), PopupPrimaryAxis.Vertical)]; }
    }
    private IEnumerable<(int Tab, string Title, IEnumerable<string> Items)> Sections()
    {
        var allowed = new HashSet<string>(Emojis ?? [], StringComparer.Ordinal);
        bool hasRecent = RecentEmojis is { Count: > 0 };
        yield return (0, hasRecent ? "Recent" : "Popular", hasRecent ? RecentEmojis! : DefaultRecent ?? []);
        for (int tab = 1; tab < Tabs.Length; tab++)
        {
            int index = tab;
            yield return (tab, Tabs[tab].Title, Tabs[tab].Groups.SelectMany(group => CategoryValues.TryGetValue(group, out var values) ? EmojiCatalog.Items.Where(item => string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase)).Select(item => item.Emoji).Where(values.Contains) : []).Where(allowed.Contains));
        }
    }
    // Rows rather than cells, so the 1,800 emoji stay virtualized and scrolling only realizes what is on screen.
    private void Build()
    {
        if (_grid is null) return;
        var rows = new List<EmojiRow>();
        _sectionTops.Clear();
        var query = SearchText.Trim();
        int columns = Math.Clamp(Columns, 6, 12);
        double cell = ListWidth / columns, top = 0;
        void Chunk(IEnumerable<string> source, int section)
        {
            var batch = source.Select(ApplySkinTone).Distinct(StringComparer.Ordinal).Chunk(columns).ToArray();
            foreach (var part in batch) { rows.Add(new EmojiRow(null, part, section, cell, this)); top += RowHeight; }
        }
        if (query.Length > 0) Chunk((Emojis ?? []).Where(emoji => MatchesSearch(emoji, query)), 0);
        else
        {
            foreach (var (tab, title, items) in Sections())
            {
                while (_sectionTops.Count <= tab) _sectionTops.Add(top);
                _sectionTops[tab] = top;
                rows.Add(new EmojiRow(title, [], tab, cell, this));
                top += HeaderHeight;
                Chunk(items, tab);
            }
        }
        _rows = rows;
        _grid.ItemsSource = rows;
        if (_empty is not null) _empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ScrollToCategory()
    {
        UpdateCategoryVisuals();
        int tab = TabIndexOf(SelectedCategory);
        if (_scroller is not null && tab < _sectionTops.Count) _scroller.ScrollToVerticalOffset(_sectionTops[tab]);
        SetActiveTab(tab, IsLoaded);
    }
    private static int TabIndexOf(string category)
    {
        int index = Array.FindIndex(Tabs, tab => string.Equals(tab.Id, category, StringComparison.OrdinalIgnoreCase) || tab.Groups.Contains(category, StringComparer.OrdinalIgnoreCase));
        return index < 0 ? 0 : index;
    }
    private void FollowScroll()
    {
        if (_scroller is null || _sectionTops.Count == 0) return;
        double at = _scroller.VerticalOffset + 1;
        int tab = 0;
        for (int i = 0; i < _sectionTops.Count; i++) if (_sectionTops[i] <= at) tab = i;
        SetActiveTab(tab, true);
    }
    private void SetActiveTab(int tab, bool animate)
    {
        if (tab == _activeTab) return;
        _activeTab = tab;
        UpdateCategoryVisuals();
        UpdateIndicator(animate);
    }
    private void UpdateIndicator(bool animate)
    {
        if (_indicator?.RenderTransform is not TranslateTransform move || _categories is null || _categories.Children.Count == 0) return;
        double width = _categories.ActualWidth > 0 ? _categories.ActualWidth / Tabs.Length : ListWidth / Tabs.Length;
        _indicator.Width = width;
        double x = Math.Max(_activeTab, 0) * width;
        if (animate && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) Tween.Spring(move, TranslateTransform.XProperty, x, Tween.Morph);
        else Tween.Set(move, TranslateTransform.XProperty, x);
    }
    private void BuildCategories()
    {
        if (_categories is null) return;
        _categories.Children.Clear();
        for (int i = 0; i < Tabs.Length; i++)
        {
            var (id, _, title, icon) = Tabs[i];
            var button = new Button { Tag = id, Content = Lucide.Glyph(icon, 16) };
            button.SetResourceReference(StyleProperty, "ArcWpf.EmojiPicker.Tab");
            AutomationProperties.SetName(button, title);
            string category = id;
            button.Click += (_, _) => { if (SelectedCategory == category) ScrollToCategory(); else SelectedCategory = category; };
            _categories.Children.Add(button);
        }
        _activeTab = TabIndexOf(SelectedCategory);
        UpdateCategoryVisuals();
    }
    private void BuildTones()
    {
        if (_tones is null) return;
        _tones.Children.Clear();
        for (var index = 0; index < SkinToneValues.Length; index++)
        {
            var button = new Button { Tag = index, Content = Glyph(ToneBase + SkinToneValues[index], 19), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2 + index * 58, 2, 0, 2) };
            button.SetResourceReference(StyleProperty, "ArcWpf.EmojiPicker.Tone");
            AutomationProperties.SetName(button, index == 0 ? "Default skin tone" : $"Skin tone {index}");
            var selected = index;
            button.Click += (_, _) => { SkinTone = selected; SetToneOpen(false); };
            _tones.Children.Add(button);
        }
        UpdateToneVisuals();
    }
    // The menu unrolls left from the tone button and closes the same way.
    private void SetToneOpen(bool open)
    {
        if (_toneMenu is null || _toneOpen == open) return;
        _toneOpen = open;
        if (open) _toneMenu.Visibility = Visibility.Visible;
        void Done() { if (!_toneOpen) _toneMenu.Visibility = Visibility.Collapsed; }
        if (IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) Tween.Spring(_toneMenu, WidthProperty, open ? ToneMenuWidth : 36, Tween.Morph, 0, Done);
        else { Tween.Set(_toneMenu, WidthProperty, open ? ToneMenuWidth : 36); Done(); }
    }
    private void UpdateCategoryVisuals()
    {
        if (_categories is null) return;
        int active = _activeTab < 0 ? TabIndexOf(SelectedCategory) : _activeTab;
        for (int i = 0; i < _categories.Children.Count; i++)
            if (_categories.Children[i] is Control tab) tab.SetResourceReference(ForegroundProperty, i == active ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted");
    }
    private void UpdateToneVisuals()
    {
        if (_tones is not null)
            foreach (var button in _tones.Children.OfType<Control>())
            {
                if (button.Tag is int tone && tone == SkinTone) button.SetResourceReference(BackgroundProperty, "ArcWpf.Brush.Surface");
                else button.Background = Brushes.Transparent;
            }
        if (_toneButton is ContentControl trigger) trigger.Content = Glyph(ToneBase + SkinToneValues[SkinTone], 20);
    }
    internal void ShowPreview(string? emoji)
    {
        if (_previewGlyph is null || _previewName is null) return;
        _previewGlyph.Text = emoji ?? Waving;
        _previewGlyph.Opacity = emoji is null ? .45 : 1;
        _previewGlyph.Grayscale = emoji is null;
        string name = emoji is not null && EmojiNames.TryGetValue(RemoveSkinTone(emoji), out var known) ? known : emoji is null ? "Pick an emoji" : string.Empty;
        _previewName.Text = name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;
    }
    internal void PickFromGrid(string emoji) => Pick(emoji, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
    private static string RemoveSkinTone(string emoji)
    {
        foreach (var tone in SkinToneValues.Skip(1)) emoji = emoji.Replace(tone, string.Empty, StringComparison.Ordinal);
        return emoji;
    }
    private static bool MatchesSearch(string emoji, string query)
    {
        var normalized = RemoveSkinTone(emoji);
        return normalized.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (EmojiNames.TryGetValue(normalized, out var name) && name.Contains(query, StringComparison.OrdinalIgnoreCase))
            || Aliases.Any(alias => alias.Key.Contains(query, StringComparison.OrdinalIgnoreCase) && alias.Value.Contains(normalized, StringComparer.Ordinal));
    }
    private string ApplySkinTone(string emoji)
    {
        var baseEmoji = RemoveSkinTone(emoji);
        return SkinTone == 0 || !ToneCapable.Contains(baseEmoji) ? baseEmoji : baseEmoji + SkinToneValues[SkinTone];
    }
    private void RecordRecent(string emoji)
    {
        if (RecentEmojis is null) return;
        RecentEmojis.Remove(emoji); RecentEmojis.Insert(0, emoji);
        while (RecentEmojis.Count > Math.Max(0, MaxRecent)) RecentEmojis.RemoveAt(RecentEmojis.Count - 1);
    }
}

internal sealed record EmojiRow(string? Title, string[] Emojis, int Section, double CellWidth, EmojiPicker Owner);

/// <summary>A virtualized row of the grid: a section label, or a run of cells.</summary>
internal sealed class EmojiRowView : Grid
{
    private const double CellGlyph = 23;
    public EmojiRow? Row { get; private set; }

    public EmojiRowView()
    {
        DataContextChanged += (_, e) => Fill(e.NewValue as EmojiRow);
    }

    private void Fill(EmojiRow? row)
    {
        Row = row;
        Children.Clear();
        ColumnDefinitions.Clear();
        if (row is null) return;
        if (row.Title is not null)
        {
            Height = 30;
            var label = new TextBlock { Text = row.Title, FontSize = 12, FontWeight = FontWeights.Medium, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(6, 9.2, 0, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            Children.Add(label);
            return;
        }
        Height = row.CellWidth;
        for (int i = 0; i < row.Emojis.Length; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(row.CellWidth) });
            string emoji = row.Emojis[i];
            var cell = new Button { Content = row.Owner.Glyph(emoji, CellGlyph) };
            cell.SetResourceReference(StyleProperty, "ArcWpf.EmojiPicker.Cell");
            AutomationProperties.SetName(cell, emoji);
            cell.Click += (_, _) => row.Owner.PickFromGrid(emoji);
            cell.MouseEnter += (_, _) => row.Owner.ShowPreview(emoji);
            cell.GotKeyboardFocus += (_, _) => row.Owner.ShowPreview(emoji);
            SetColumn(cell, i);
            Children.Add(cell);
        }
    }
}
