using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArcWpf.Controls;

public class EmojiPicker : Control
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false));
    public static readonly DependencyProperty SelectedEmojiProperty = DependencyProperty.Register(nameof(SelectedEmoji), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty EmojisProperty = DependencyProperty.Register(nameof(Emojis), typeof(ObservableCollection<string>), typeof(EmojiPicker), new PropertyMetadata(null));
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EmojiPicker), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(EmojiPicker), new PropertyMetadata("Search emoji"));
    public static readonly DependencyProperty AutoFocusProperty = DependencyProperty.Register(nameof(AutoFocus), typeof(bool), typeof(EmojiPicker), new PropertyMetadata(false));
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string SelectedEmoji { get => (string)GetValue(SelectedEmojiProperty); set => SetValue(SelectedEmojiProperty, value); }
    public ObservableCollection<string> Emojis { get => (ObservableCollection<string>)GetValue(EmojisProperty); set => SetValue(EmojisProperty, value); }
    public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public string SearchPlaceholder { get => (string)GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    public bool AutoFocus { get => (bool)GetValue(AutoFocusProperty); set => SetValue(AutoFocusProperty, value); }
    public event EventHandler<string>? EmojiSelected;
    private Popup? _popup; private UniformGrid? _grid; private TextBox? _search;
    static EmojiPicker() => DefaultStyleKeyProperty.OverrideMetadata(typeof(EmojiPicker), new FrameworkPropertyMetadata(typeof(EmojiPicker)));
    public EmojiPicker()
    {
        Emojis = new ObservableCollection<string>(new[] { "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🙂", "🙃", "😉", "😊", "😍", "🤔", "😎", "🎉", "🔥", "❤️", "👍", "👏", "🙏", "✅", "⭐", "🚀", "💡" });
        SelectedEmoji = Emojis[0];
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); if (GetTemplateChild("PART_Toggle") is Button toggle) toggle.Click += (_, _) => IsOpen = !IsOpen;
        _popup = GetTemplateChild("PART_Popup") as Popup; _grid = GetTemplateChild("PART_Grid") as UniformGrid; _search = GetTemplateChild("PART_Search") as TextBox;
        if (_popup is not null) { _popup.DataContext = this; _popup.CustomPopupPlacementCallback = PlacePopup; }
        if (_search is not null) _search.TextChanged += (_, _) => { SearchText = _search.Text; Build(); };
        Build();
        if (AutoFocus && IsOpen) Dispatcher.BeginInvoke(() => _search?.Focus());
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
        if (_grid is null) return; _grid.Children.Clear(); var query = SearchText.Trim(); IEnumerable<string> items = string.IsNullOrEmpty(query) ? Emojis : Emojis.Where(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var emoji in items) { var button = new Button { Content = emoji, FontSize = 22, Padding = new Thickness(6), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Tag = emoji, Focusable = true, ToolTip = emoji }; button.Click += (_, _) => { SelectedEmoji = emoji; EmojiSelected?.Invoke(this, emoji); IsOpen = false; }; _grid.Children.Add(button); }
    }
}
