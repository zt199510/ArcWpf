using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcWpf.Controls;

public sealed class MentionPerson { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; }
public sealed class MentionChannel { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
public enum MentionKind { Person, Channel }
public sealed class MentionToken
{
    public MentionKind Kind { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Start { get; internal set; }
    public int End { get; internal set; }
    public string Text => (Kind == MentionKind.Person ? "@" : "#") + Label;
}

public class MentionInput : TextBox
{
    public static readonly DependencyProperty PeopleProperty = DependencyProperty.Register(nameof(People), typeof(ObservableCollection<MentionPerson>), typeof(MentionInput), new PropertyMetadata(null));
    public static readonly DependencyProperty SubmitOnEnterProperty = DependencyProperty.Register(nameof(SubmitOnEnter), typeof(bool), typeof(MentionInput), new PropertyMetadata(false));
    public static readonly DependencyProperty MaxSuggestionsProperty = DependencyProperty.Register(nameof(MaxSuggestions), typeof(int), typeof(MentionInput), new PropertyMetadata(6));
    public static readonly DependencyProperty ChannelsProperty = DependencyProperty.Register(nameof(Channels), typeof(ObservableCollection<MentionChannel>), typeof(MentionInput), new PropertyMetadata(null));
    public static readonly DependencyProperty MentionsProperty = DependencyProperty.Register(nameof(Mentions), typeof(ObservableCollection<MentionToken>), typeof(MentionInput), new PropertyMetadata(null));
    public ObservableCollection<MentionPerson> People { get => (ObservableCollection<MentionPerson>)GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public bool SubmitOnEnter { get => (bool)GetValue(SubmitOnEnterProperty); set => SetValue(SubmitOnEnterProperty, value); }
    public int MaxSuggestions { get => (int)GetValue(MaxSuggestionsProperty); set => SetValue(MaxSuggestionsProperty, value); }
    public ObservableCollection<MentionChannel> Channels { get => (ObservableCollection<MentionChannel>)GetValue(ChannelsProperty); set => SetValue(ChannelsProperty, value); }
    public ObservableCollection<MentionToken> Mentions { get => (ObservableCollection<MentionToken>)GetValue(MentionsProperty); set => SetValue(MentionsProperty, value); }
    public event EventHandler<string>? Submitted;
    public event EventHandler<MentionToken>? MentionAdded;
    private readonly Popup _popup = new() { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
    private readonly ListBox _list = new() { BorderThickness = new Thickness(0), Padding = new Thickness(4), MinWidth = 240 };
    private readonly Border _popupSurface = new() { Padding = new Thickness(4), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1) };
    private string _trigger = string.Empty; private char _symbol = '@'; private string _lastText = string.Empty; private int _lastCaret; private bool _changingMentions;
    private MentionTokenAdorner? _tokenAdorner;
    static MentionInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MentionInput), new FrameworkPropertyMetadata(typeof(MentionInput)));
    public MentionInput() { People = new ObservableCollection<MentionPerson>(); Channels = new ObservableCollection<MentionChannel>(); Mentions = new ObservableCollection<MentionToken>(); AcceptsReturn = true; _popup.Placement = PlacementMode.Custom; _popup.CustomPopupPlacementCallback = PlacePopup; _popupSurface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised"); _popupSurface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border"); _popupSurface.SetResourceReference(Border.EffectProperty, "ArcWpf.Shadow.Raised"); _popupSurface.Child = _list; TextChanged += OnTextChanged; KeyDown += OnKeyDown; Loaded += OnLoaded; Unloaded += OnUnloaded; SizeChanged += (_, _) => _tokenAdorner?.InvalidateVisual(); LostKeyboardFocus += (_, _) => _popup.IsOpen = false; _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is MentionPerson p) Choose(p); else if (_list.SelectedItem is MentionChannel c) Choose(c); }; _popup.Child = _popupSurface; }
    private void OnLoaded(object sender, RoutedEventArgs e) { if (_tokenAdorner is not null) return; var layer = AdornerLayer.GetAdornerLayer(this); if (layer is null) return; _tokenAdorner = new MentionTokenAdorner(this); layer.Add(_tokenAdorner); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { if (_tokenAdorner is null) return; AdornerLayer.GetAdornerLayer(this)?.Remove(_tokenAdorner); _tokenAdorner = null; }
    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_changingMentions) ReconcileMentions(_lastText, Text, _lastCaret);
        _lastText = Text; _lastCaret = CaretIndex; RefreshSuggestions();
        _tokenAdorner?.InvalidateVisual();
    }
    private void ReconcileMentions(string oldText, string newText, int hint)
    {
        if (Mentions.Count == 0) return;
        var prefix = 0; var maxPrefix = Math.Min(Math.Min(oldText.Length, newText.Length), Math.Max(0, hint));
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix]) prefix++;
        var suffix = 0; while (suffix < oldText.Length - prefix && suffix < newText.Length - prefix && oldText[^ (suffix + 1)] == newText[^ (suffix + 1)]) suffix++;
        var oldEnd = oldText.Length - suffix; var delta = newText.Length - oldText.Length;
        foreach (var mention in Mentions.ToArray())
        {
            if (mention.End <= prefix) continue;
            if (mention.Start >= oldEnd) { mention.Start += delta; mention.End += delta; continue; }
            Mentions.Remove(mention);
        }
        foreach (var mention in Mentions.ToArray()) if (mention.Start < 0 || mention.End > newText.Length || newText[mention.Start..mention.End] != mention.Text) Mentions.Remove(mention);
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        try
        {
            var caret = GetRectFromCharacterIndex(Math.Clamp(CaretIndex, 0, Text.Length), true);
            if (caret.IsEmpty || double.IsNaN(caret.X)) caret = new Rect(0, 0, 0, ActualHeight);
            var screenCaret = PointToScreen(new Point(caret.X, caret.Bottom));
            var workArea = SystemParameters.WorkArea;
            var roomBelow = screenCaret.Y + popupSize.Height + 6 <= workArea.Bottom;
            var roomAbove = screenCaret.Y - caret.Height - popupSize.Height - 6 >= workArea.Top;
            var below = roomBelow || !roomAbove;
            var x = Math.Clamp(caret.X, 0, Math.Max(0, ActualWidth - popupSize.Width));
            var y = below ? caret.Bottom + 6 : caret.Top - popupSize.Height - 6;
            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical)];
        }
        catch { return [new CustomPopupPlacement(new Point(0, targetSize.Height + 6), PopupPrimaryAxis.Vertical)]; }
    }
    private void RefreshSuggestions()
    {
        var caret = CaretIndex; var before = Text[..Math.Clamp(caret, 0, Text.Length)]; var at = before.LastIndexOf('@'); var hash = before.LastIndexOf('#'); var start = Math.Max(at, hash);
        if (start < 0 || (start > 0 && !char.IsWhiteSpace(before[start - 1]))) { _popup.IsOpen = false; return; }
        var query = before[(start + 1)..]; if (query.Contains(' ') || query.Contains('\n')) { _popup.IsOpen = false; return; }
        _symbol = before[start]; _trigger = query;
        _list.ItemsSource = (_symbol == '@' ? People.Where(p => p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>() : Channels.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>()).Take(Math.Max(1, MaxSuggestions)).ToList();
        _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
        _popup.PlacementTarget = this;
        _popup.IsOpen = _list.Items.Count > 0;
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_popup.IsOpen)
        {
            if (SelectionLength == 0 && e.Key == Key.Back) { var mention = Mentions.FirstOrDefault(item => item.End == CaretIndex); if (mention is not null) { RemoveMention(mention); e.Handled = true; return; } }
            if (SelectionLength == 0 && e.Key == Key.Delete) { var mention = Mentions.FirstOrDefault(item => item.Start == CaretIndex); if (mention is not null) { RemoveMention(mention); e.Handled = true; return; } }
            if (SelectionLength == 0 && e.Key == Key.Left) { var mention = Mentions.FirstOrDefault(item => item.End == CaretIndex); if (mention is not null) { CaretIndex = mention.Start; e.Handled = true; return; } }
            if (SelectionLength == 0 && e.Key == Key.Right) { var mention = Mentions.FirstOrDefault(item => item.Start == CaretIndex); if (mention is not null) { CaretIndex = mention.End; e.Handled = true; return; } }
            if (e.Key == Key.Enter && SubmitOnEnter) { Submitted?.Invoke(this, Text); e.Handled = true; }
            return;
        }
        if (e.Key == Key.Down) { _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1); e.Handled = true; }
        else if (e.Key == Key.Up) { _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0); e.Handled = true; }
        else if (e.Key is Key.Enter or Key.Tab && _list.SelectedItem is object selected) { if (selected is MentionPerson person) Choose(person); else if (selected is MentionChannel channel) Choose(channel); e.Handled = true; }
        else if (e.Key == Key.Escape) { _popup.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.Back && SelectionLength == 0) { var mention = Mentions.FirstOrDefault(item => item.End == CaretIndex); if (mention is not null) { RemoveMention(mention); e.Handled = true; } }
        else if (e.Key == Key.Delete && SelectionLength == 0) { var mention = Mentions.FirstOrDefault(item => item.Start == CaretIndex); if (mention is not null) { RemoveMention(mention); e.Handled = true; } }
        else if (e.Key == Key.Left && SelectionLength == 0) { var mention = Mentions.FirstOrDefault(item => item.End == CaretIndex); if (mention is not null) { CaretIndex = mention.Start; e.Handled = true; } }
        else if (e.Key == Key.Right && SelectionLength == 0) { var mention = Mentions.FirstOrDefault(item => item.Start == CaretIndex); if (mention is not null) { CaretIndex = mention.End; e.Handled = true; } }
    }
    private void RemoveMention(MentionToken mention)
    {
        var caret = mention.Start; _changingMentions = true; try { Text = Text.Remove(mention.Start, mention.End - mention.Start); Mentions.Remove(mention); } finally { _changingMentions = false; } CaretIndex = caret; _lastText = Text; _lastCaret = caret;
    }
    private void Choose(MentionPerson person) => Choose(MentionKind.Person, person.Id, person.Name);
    private void Choose(MentionChannel channel) => Choose(MentionKind.Channel, channel.Id, channel.Name);
    private void Choose(MentionKind kind, string id, string name)
    {
        var start = CaretIndex - _trigger.Length - 1; if (start < 0) return;
        var tokenText = (kind == MentionKind.Person ? "@" : "#") + name; var token = new MentionToken { Kind = kind, Id = id, Label = name, Start = start, End = start + tokenText.Length };
        _changingMentions = true; try { Text = Text.Remove(start, _trigger.Length + 1).Insert(start, tokenText + " "); Mentions.Add(token); } finally { _changingMentions = false; }
        CaretIndex = token.End + 1; _lastText = Text; _lastCaret = CaretIndex; _popup.IsOpen = false; Focus(); MentionAdded?.Invoke(this, token);
    }

    private sealed class MentionTokenAdorner : Adorner
    {
        private readonly MentionInput _owner;
        public MentionTokenAdorner(MentionInput owner) : base(owner) { _owner = owner; IsHitTestVisible = false; SnapsToDevicePixels = true; }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (_owner.Mentions.Count == 0 || string.IsNullOrEmpty(_owner.Text)) return;
            var accent = (_owner.TryFindResource("ArcWpf.Brush.AccentSubtle") as Brush) ?? new SolidColorBrush(Color.FromArgb(36, 96, 120, 255));
            var channel = (_owner.TryFindResource("ArcWpf.Brush.SurfaceMuted") as Brush) ?? new SolidColorBrush(Color.FromArgb(32, 128, 128, 128));
            foreach (var token in _owner.Mentions.ToArray())
            {
                var start = Math.Clamp(token.Start, 0, _owner.Text.Length); var end = Math.Clamp(token.End, start, _owner.Text.Length);
                Rect? line = null;
                for (var index = start; index < end; index++)
                {
                    var rect = _owner.GetRectFromCharacterIndex(index, true);
                    if (rect.IsEmpty || double.IsNaN(rect.X)) continue;
                    if (line is null || Math.Abs(line.Value.Top - rect.Top) > 0.5) { if (line is not null) Draw(line.Value, token.Kind == MentionKind.Channel ? channel : accent, drawingContext); line = rect; }
                    else line = new Rect(Math.Min(line.Value.Left, rect.Left), line.Value.Top, Math.Max(line.Value.Right, rect.Right) - Math.Min(line.Value.Left, rect.Left), Math.Max(line.Value.Height, rect.Height));
                }
                if (line is not null) Draw(line.Value, token.Kind == MentionKind.Channel ? channel : accent, drawingContext);
            }
        }
        private static void Draw(Rect rect, Brush brush, DrawingContext context)
        {
            rect.Inflate(2.5, 1); context.DrawRoundedRectangle(brush, null, rect, 5, 5);
        }
    }
}
