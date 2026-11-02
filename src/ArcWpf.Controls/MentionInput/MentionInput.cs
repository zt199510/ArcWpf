using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ArcWpf.Controls;

public sealed class MentionPerson { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; }
public sealed class MentionChannel { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }

public class MentionInput : TextBox
{
    public static readonly DependencyProperty PeopleProperty = DependencyProperty.Register(nameof(People), typeof(ObservableCollection<MentionPerson>), typeof(MentionInput), new PropertyMetadata(null));
    public static readonly DependencyProperty SubmitOnEnterProperty = DependencyProperty.Register(nameof(SubmitOnEnter), typeof(bool), typeof(MentionInput), new PropertyMetadata(false));
    public static readonly DependencyProperty MaxSuggestionsProperty = DependencyProperty.Register(nameof(MaxSuggestions), typeof(int), typeof(MentionInput), new PropertyMetadata(6));
    public static readonly DependencyProperty ChannelsProperty = DependencyProperty.Register(nameof(Channels), typeof(ObservableCollection<MentionChannel>), typeof(MentionInput), new PropertyMetadata(null));
    public ObservableCollection<MentionPerson> People { get => (ObservableCollection<MentionPerson>)GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public bool SubmitOnEnter { get => (bool)GetValue(SubmitOnEnterProperty); set => SetValue(SubmitOnEnterProperty, value); }
    public int MaxSuggestions { get => (int)GetValue(MaxSuggestionsProperty); set => SetValue(MaxSuggestionsProperty, value); }
    public ObservableCollection<MentionChannel> Channels { get => (ObservableCollection<MentionChannel>)GetValue(ChannelsProperty); set => SetValue(ChannelsProperty, value); }
    private readonly Popup _popup = new() { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
    private readonly ListBox _list = new() { BorderThickness = new Thickness(0), Padding = new Thickness(4), MinWidth = 240 };
    private readonly Border _popupSurface = new() { Padding = new Thickness(4), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1) };
    private string _trigger = string.Empty; private char _symbol = '@';
    static MentionInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MentionInput), new FrameworkPropertyMetadata(typeof(MentionInput)));
    public MentionInput() { People = new ObservableCollection<MentionPerson>(); Channels = new ObservableCollection<MentionChannel>(); AcceptsReturn = true; _popupSurface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised"); _popupSurface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border"); _popupSurface.SetResourceReference(Border.EffectProperty, "ArcWpf.Shadow.Raised"); _popupSurface.Child = _list; TextChanged += (_, _) => RefreshSuggestions(); KeyDown += OnKeyDown; LostKeyboardFocus += (_, _) => _popup.IsOpen = false; _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is MentionPerson p) Choose(p.Name); else if (_list.SelectedItem is MentionChannel c) Choose(c.Name); }; _popup.Child = _popupSurface; }
    private void RefreshSuggestions()
    {
        var caret = CaretIndex; var before = Text[..Math.Clamp(caret, 0, Text.Length)]; var at = before.LastIndexOf('@'); var hash = before.LastIndexOf('#'); var start = Math.Max(at, hash);
        if (start < 0 || (start > 0 && !char.IsWhiteSpace(before[start - 1]))) { _popup.IsOpen = false; return; }
        var query = before[(start + 1)..]; if (query.Contains(' ') || query.Contains('\n')) { _popup.IsOpen = false; return; }
        _symbol = before[start]; _trigger = query;
        _list.ItemsSource = (_symbol == '@' ? People.Where(p => p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>() : Channels.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>()).Take(Math.Max(1, MaxSuggestions)).ToList();
        _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
        _popup.PlacementTarget = this;
        if (_list.Items.Count > 0)
        {
            _popup.HorizontalOffset = 0;
            try
            {
                var screen = PointToScreen(new Point(0, ActualHeight));
                _popup.Placement = screen.Y + 300 > SystemParameters.WorkArea.Bottom ? PlacementMode.Top : PlacementMode.Bottom;
            }
            catch { _popup.Placement = PlacementMode.Bottom; }
        }
        _popup.IsOpen = _list.Items.Count > 0;
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_popup.IsOpen) return;
        if (e.Key == Key.Down) { _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1); e.Handled = true; }
        else if (e.Key == Key.Up) { _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0); e.Handled = true; }
        else if (e.Key is Key.Enter or Key.Tab && _list.SelectedItem is object selected) { if (selected is MentionPerson person) Choose(person.Name); else if (selected is MentionChannel channel) Choose(channel.Name); e.Handled = true; }
        else if (e.Key == Key.Escape) { _popup.IsOpen = false; e.Handled = true; }
    }
    private void Choose(string name)
    {
        var start = CaretIndex - _trigger.Length - 1; if (start < 0) return;
        Text = Text.Remove(start, _trigger.Length + 1).Insert(start, _symbol + name + " "); CaretIndex = start + name.Length + 2; _popup.IsOpen = false; Focus();
    }
}
