using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Collections.Specialized;

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
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false));
    public static readonly DependencyProperty SelectedEmojiProperty = DependencyProperty.Register(nameof(SelectedEmoji), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty EmojisProperty = DependencyProperty.Register(nameof(Emojis), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null, OnEmojisChanged));
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(EmojiPicker), new PropertyMetadata("Search emoji"));
    public static readonly DependencyProperty AutoFocusProperty = DependencyProperty.Register(nameof(AutoFocus), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false));
    public static readonly DependencyProperty RecentEmojisProperty = DependencyProperty.Register(nameof(RecentEmojis), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null));
    public static readonly DependencyProperty MaxRecentProperty = DependencyProperty.Register(nameof(MaxRecent), typeof(int), typeof(EmojiPicker), new PropertyMetadata(8));
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
    public string SelectedCategory { get => (string)GetValue(SelectedCategoryProperty); set => SetValue(SelectedCategoryProperty, value); }
    /// <summary>当前肤色：0 为默认黄色，1 至 5 对应 Unicode Fitzpatrick 修饰符。</summary>
    public int SkinTone { get => (int)GetValue(SkinToneProperty); set => SetValue(SkinToneProperty, value); }
    public event EventHandler<string>? EmojiSelected;
    public event EventHandler<int>? SkinToneChanged;
    public void SelectEmoji(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji)) return;
        var selected = ApplySkinTone(emoji); SelectedEmoji = selected; RecordRecent(selected); EmojiSelected?.Invoke(this, selected); IsOpen = false;
    }
    private static void OnEmojisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).AttachEmojiSource(e.NewValue as ObservableCollection<string>);
    private static void OnCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiPicker)d).Build();
    private static void OnSkinToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var picker = (EmojiPicker)d; picker.Build(); picker.BuildTones(); picker.SkinToneChanged?.Invoke(picker, (int)e.NewValue); }
    private static object CoerceSkinTone(DependencyObject d, object value) => Math.Clamp((int)value, 0, SkinToneValues.Length - 1);
    private Popup? _popup; private UniformGrid? _grid; private UniformGrid? _categories; private UniformGrid? _tones; private TextBox? _search; private INotifyCollectionChanged? _emojiSource;
    static EmojiPicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(EmojiPicker), new FrameworkPropertyMetadata(typeof(EmojiPicker)));
    public EmojiPicker()
    {
        Emojis = new ObservableCollection<string>(EmojiCatalog.Items.Select(item => item.Emoji));
        SelectedEmoji = Emojis[0]; RecentEmojis = new ObservableCollection<string>();
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); if (GetTemplateChild("PART_Toggle") is System.Windows.Controls.Button toggle) toggle.Click += (_, _) => IsOpen = !IsOpen;
        _popup = GetTemplateChild("PART_Popup") as Popup; _grid = GetTemplateChild("PART_Grid") as UniformGrid; _categories = GetTemplateChild("PART_Categories") as UniformGrid; _tones = GetTemplateChild("PART_Tones") as UniformGrid; _search = GetTemplateChild("PART_Search") as TextBox;
        if (_popup is not null) { _popup.DataContext = this; _popup.CustomPopupPlacementCallback = PlacePopup; }
        if (_search is not null) _search.TextChanged += (_, _) => { SearchText = _search.Text; Build(); };
        PreviewKeyDown += OnPreviewKeyDown;
        AttachEmojiSource(Emojis);
        BuildCategories(); BuildTones(); Build();
        if (AutoFocus && IsOpen) Dispatcher.BeginInvoke(() => _search?.Focus());
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
            if (!string.IsNullOrEmpty(SearchText)) { SearchText = string.Empty; if (_search is not null) _search.Clear(); Build(); e.Handled = true; return; }
            IsOpen = false; e.Handled = true; return;
        }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return;
        var buttons = _grid?.Children.OfType<System.Windows.Controls.Button>().ToArray();
        if (buttons is null || buttons.Length == 0) return;
        var current = Array.IndexOf(buttons, Keyboard.FocusedElement as Button);
        var columns = Math.Max(1, _grid?.Columns ?? 6);
        var next = current < 0 ? 0 : current;
        next = e.Key switch
        {
            Key.Left => Math.Max(0, next - 1),
            Key.Right => Math.Min(buttons.Length - 1, next + 1),
            Key.Up => Math.Max(0, next - columns),
            Key.Down => Math.Min(buttons.Length - 1, next + columns),
            Key.Home or Key.PageUp => 0,
            Key.End or Key.PageDown => buttons.Length - 1,
            _ => next
        };
        buttons[next].Focus(); e.Handled = true;
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        try
        {
            var screen = PointToScreen(new Point(0, ActualHeight));
            var below = screen.Y + popupSize.Height + 8 < SystemParameters.WorkArea.Bottom;
            return [new CustomPopupPlacement(new Point(0, below ? targetSize.Height + 6 : -popupSize.Height - 6), PopupPrimaryAxis.Vertical)];
        }
        catch { return [new CustomPopupPlacement(new Point(0, targetSize.Height + 6), PopupPrimaryAxis.Vertical)]; }
    }
    private void Build()
    {
        if (_grid is null) return; _grid.Children.Clear(); var query = SearchText.Trim(); var allCategories = string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase); IEnumerable<string> source = allCategories ? Emojis : Emojis.Where(value => CategoryValues.TryGetValue(SelectedCategory, out var values) && values.Contains(RemoveSkinTone(value))); IEnumerable<string> items = string.IsNullOrEmpty(query) ? (allCategories ? (RecentEmojis ?? new ObservableCollection<string>()) : Enumerable.Empty<string>()).Concat(source).Distinct(StringComparer.Ordinal) : source.Where(value => MatchesSearch(value, query));
        foreach (var emoji in items) { var displayed = ApplySkinTone(emoji); var name = EmojiNames.TryGetValue(RemoveSkinTone(emoji), out var knownName) ? knownName : displayed; var button = new System.Windows.Controls.Button { Content = displayed, FontSize = 22, Padding = new Thickness(6), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Tag = emoji, Focusable = true, ToolTip = name }; button.Click += (_, _) => SelectEmoji(emoji); _grid.Children.Add(button); }
        if (_grid.Children.Count == 0) _grid.Children.Add(new TextBlock { Text = "No emoji found", Foreground = (Brush?)TryFindResource("ArcWpf.Brush.TextMuted") ?? Brushes.Gray, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Center });
    }
    private void BuildCategories()
    {
        if (_categories is null) return; _categories.Children.Clear(); var names = new[] { "All" }.Concat(CategoryValues.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)).ToArray(); _categories.Columns = names.Length;
        foreach (var name in names) { var button = new System.Windows.Controls.Button { Content = name, Tag = name, Padding = new Thickness(6, 3, 6, 3), BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 11 }; button.Click += (_, _) => SelectedCategory = name; _categories.Children.Add(button); }
    }
    private void BuildTones()
    {
        if (_tones is null) return; _tones.Children.Clear(); _tones.Columns = SkinToneValues.Length;
        for (var index = 0; index < SkinToneValues.Length; index++)
        {
            var tone = index == 0 ? "👍" : "👍" + SkinToneValues[index];
            var button = new System.Windows.Controls.Button { Content = tone, Tag = index, Padding = new Thickness(5, 2, 5, 2), BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 17, ToolTip = index == 0 ? "Default skin tone" : $"Skin tone {index}" };
            var selected = index; button.Click += (_, _) => SkinTone = selected; _tones.Children.Add(button);
        }
    }
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
