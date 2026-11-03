using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;

namespace ArcWpf.Controls;

public sealed class MentionPerson { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; public ImageSource? Avatar { get; set; } public string Initial => string.IsNullOrEmpty(Name) ? string.Empty : Name[..1]; }
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
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(MentionInput), new PropertyMetadata("Write a comment, @ to mention", OnPlaceholderChanged));
    public ObservableCollection<MentionPerson> People { get => (ObservableCollection<MentionPerson>)GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public bool SubmitOnEnter { get => (bool)GetValue(SubmitOnEnterProperty); set => SetValue(SubmitOnEnterProperty, value); }
    public int MaxSuggestions { get => (int)GetValue(MaxSuggestionsProperty); set => SetValue(MaxSuggestionsProperty, value); }
    public ObservableCollection<MentionChannel> Channels { get => (ObservableCollection<MentionChannel>)GetValue(ChannelsProperty); set => SetValue(ChannelsProperty, value); }
    public ObservableCollection<MentionToken> Mentions { get => (ObservableCollection<MentionToken>)GetValue(MentionsProperty); set => SetValue(MentionsProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public event EventHandler<string>? Submitted;
    public event EventHandler<MentionToken>? MentionAdded;
    private readonly Popup _popup = new() { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
    private readonly ListBox _list = new() { BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(6), Background = Brushes.Transparent };
    private readonly Border _highlight = new() { CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 6, 6, 0), IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
    private readonly TextBlock _empty = new() { FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(14, 12, 14, 12), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private bool _highlightPlaced;
    private readonly Border _popupSurface = new() { Width = 272, Margin = new Thickness(PopupGutter), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), RenderTransformOrigin = new Point(.05, 0) };
    private const double PopupGutter = 24;
    private string _trigger = string.Empty; private char _symbol = '@'; private string _lastText = string.Empty; private int _lastCaret; private bool _changingMentions;
    private MentionTokenLayer? _tokenLayer;
    static MentionInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MentionInput), new FrameworkPropertyMetadata(typeof(MentionInput)));
    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MentionInput)d)._tokenLayer?.InvalidateVisual();
    public MentionInput() { People = new ObservableCollection<MentionPerson>(); Channels = new ObservableCollection<MentionChannel>(); Mentions = new ObservableCollection<MentionToken>(); AcceptsReturn = true; _popup.Placement = PlacementMode.Custom; _popup.CustomPopupPlacementCallback = PlacePopup; _popupSurface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised"); _popupSurface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border"); _popupSurface.SetResourceReference(Border.EffectProperty, "ArcWpf.Shadow.Floating"); _highlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.MentionHighlight"); _empty.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary"); _list.MaxHeight = 303; ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled); _list.SelectionChanged += (_, _) => MoveHighlight(); _list.SelectionMode = SelectionMode.Single; _list.ItemTemplateSelector = new SuggestionTemplateSelector(BuildSuggestionTemplate("@", false), BuildSuggestionTemplate("#", true)); _list.ItemContainerStyle = BuildSuggestionContainerStyle(); var popupBody = new Grid(); popupBody.Children.Add(_highlight); popupBody.Children.Add(_list); popupBody.Children.Add(_empty); _popupSurface.Child = popupBody; TextChanged += OnTextChanged; KeyDown += OnKeyDown; SizeChanged += (_, _) => _tokenLayer?.InvalidateVisual(); LostKeyboardFocus += (_, _) => _popup.IsOpen = false; _list.PreviewMouseMove += OnListHover; _list.PreviewMouseLeftButtonDown += OnListClick; _popup.Child = _popupSurface; }
    private static DataTemplate BuildSuggestionTemplate(string glyph, bool channel)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 7, 10, 7));
        root.SetValue(UIElement.SnapsToDevicePixelsProperty, false);
        var column1 = new FrameworkElementFactory(typeof(ColumnDefinition)); column1.SetValue(ColumnDefinition.WidthProperty, new GridLength(38));
        var column2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        root.AppendChild(column1); root.AppendChild(column2);
        var mark = new FrameworkElementFactory(typeof(Grid));
        mark.SetValue(FrameworkElement.WidthProperty, 28d); mark.SetValue(FrameworkElement.HeightProperty, 28d); mark.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); mark.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var disc = new FrameworkElementFactory(typeof(Ellipse)); disc.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.SurfaceMuted"); mark.AppendChild(disc);
        var letter = new FrameworkElementFactory(typeof(TextBlock));
        if (channel) letter.SetValue(TextBlock.TextProperty, glyph); else letter.SetBinding(TextBlock.TextProperty, new Binding(nameof(MentionPerson.Initial)));
        letter.SetValue(TextBlock.FontSizeProperty, 12d); letter.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium); letter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); letter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); letter.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        mark.AppendChild(letter);
        if (!channel)
        {
            var photo = new FrameworkElementFactory(typeof(Image)); photo.SetBinding(Image.SourceProperty, new Binding(nameof(MentionPerson.Avatar))); photo.SetValue(Image.StretchProperty, Stretch.UniformToFill); photo.SetValue(UIElement.ClipProperty, PhotoClip);
            mark.AppendChild(photo);
        }
        root.AppendChild(mark);
        var text = new FrameworkElementFactory(typeof(StackPanel)); text.SetValue(Grid.ColumnProperty, 1); text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new Binding("Name")); name.SetValue(TextBlock.FontSizeProperty, 14d); name.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium); name.SetValue(TextBlock.LineHeightProperty, 19.6); name.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight); name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        var nameStyle = new Style(typeof(TextBlock));
        nameStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("ArcWpf.Brush.TextSecondary")));
        var active = new DataTrigger { Binding = new Binding(nameof(ListBoxItem.IsSelected)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1) }, Value = true };
        active.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("ArcWpf.Brush.Foreground")));
        nameStyle.Triggers.Add(active);
        name.SetValue(FrameworkElement.StyleProperty, nameStyle);
        var meta = new FrameworkElementFactory(typeof(TextBlock)); meta.SetBinding(TextBlock.TextProperty, new Binding(channel ? nameof(MentionChannel.Description) : nameof(MentionPerson.Role))); meta.SetValue(TextBlock.FontSizeProperty, 12d); meta.SetValue(TextBlock.LineHeightProperty, 16.8); meta.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight); meta.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); meta.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        text.AppendChild(name); text.AppendChild(meta);
        root.AppendChild(text);
        return new DataTemplate { VisualTree = root };
    }
    private static readonly Geometry PhotoClip = CreatePhotoClip();
    private static Geometry CreatePhotoClip() { var clip = new EllipseGeometry(new Point(14, 14), 14, 14); clip.Freeze(); return clip; }
    private static Style BuildSuggestionContainerStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var template = new ControlTemplate(typeof(ListBoxItem));
        var surface = new FrameworkElementFactory(typeof(Border)); surface.SetValue(Border.BackgroundProperty, Brushes.Transparent); surface.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        template.VisualTree = surface;
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }
    // One highlight glides between options instead of each row painting its own.
    private void MoveHighlight()
    {
        _highlight.Visibility = _list.SelectedIndex < 0 ? Visibility.Hidden : Visibility.Visible;
        if (_list.SelectedIndex < 0 || _list.ItemContainerGenerator.ContainerFromIndex(_list.SelectedIndex) is not ListBoxItem item || item.ActualHeight <= 0) return;
        _list.ScrollIntoView(_list.SelectedItem);
        double y = item.TranslatePoint(new Point(), _list).Y;
        _highlight.Height = item.ActualHeight;
        var move = (TranslateTransform)_highlight.RenderTransform;
        if (_highlightPlaced && ArcMotion.GetLevel(this) == ArcMotionLevel.Full) Tween.Spring(move, TranslateTransform.YProperty, y, .3);
        else Tween.Set(move, TranslateTransform.YProperty, y);
        _highlightPlaced = true;
    }
    private void PlayOpen()
    {
        _highlightPlaced = false;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, MoveHighlight);
        var level = ArcMotion.GetLevel(this);
        if (level == ArcMotionLevel.Off) return;
        _popupSurface.Opacity = 0;
        Tween.To(_popupSurface, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        if (level != ArcMotionLevel.Full) return;
        var scale = new ScaleTransform(.96, .96);
        var move = new TranslateTransform(0, 4);
        _popupSurface.RenderTransform = new TransformGroup { Children = { scale, move } };
        Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
        Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
        Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Snappy);
    }
    /// <summary>Starts a mention from a toolbar button: writes the trigger at the caret, with a space before it when one is needed.</summary>
    public void InsertTrigger(char symbol)
    {
        int caret = Math.Clamp(CaretIndex, 0, Text.Length);
        string lead = caret > 0 && !char.IsWhiteSpace(Text[caret - 1]) ? " " : string.Empty;
        Text = Text.Insert(caret, lead + symbol);
        CaretIndex = caret + lead.Length + 1;
        Focus();
        RefreshSuggestions();
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _tokenLayer = GetTemplateChild("PART_Tokens") as MentionTokenLayer;
        if (_tokenLayer is not null) _tokenLayer.Owner = this;
        if (GetTemplateChild("PART_ContentHost") is ScrollViewer host) host.ScrollChanged += (_, _) => _tokenLayer?.InvalidateVisual();
    }
    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_changingMentions) ReconcileMentions(_lastText, Text, _lastCaret);
        _lastText = Text; _lastCaret = CaretIndex; RefreshSuggestions();
        _tokenLayer?.InvalidateVisual();
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
    // Anchors the popover's text to the trigger character: 10 left of it, 8 below its line.
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        try
        {
            int anchor = Math.Clamp(CaretIndex - _trigger.Length - 1, 0, Text.Length);
            var caret = GetRectFromCharacterIndex(anchor, false);
            if (caret.IsEmpty || double.IsNaN(caret.X)) caret = new Rect(0, 0, 0, ActualHeight);
            var screenCaret = PointToScreen(new Point(caret.X, caret.Bottom));
            var workArea = SystemParameters.WorkArea;
            double height = popupSize.Height - PopupGutter * 2;
            var roomBelow = screenCaret.Y + height + 8 <= workArea.Bottom;
            var roomAbove = screenCaret.Y - caret.Height - height - 8 >= workArea.Top;
            var below = roomBelow || !roomAbove;
            var x = Math.Clamp(caret.X - 10, 0, Math.Max(0, ActualWidth - (popupSize.Width - PopupGutter * 2))) - PopupGutter;
            var y = (below ? caret.Bottom + 8 : caret.Top - height - 8) - PopupGutter;
            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical)];
        }
        catch (InvalidOperationException) { return [new CustomPopupPlacement(new Point(-PopupGutter, targetSize.Height + 6 - PopupGutter), PopupPrimaryAxis.Vertical)]; }
    }
    private object? ItemUnder(MouseEventArgs e)
    {
        var node = e.OriginalSource as DependencyObject;
        while (node is not null && node is not ListBoxItem) node = VisualTreeHelper.GetParent(node);
        return node is ListBoxItem item ? item.DataContext : null;
    }
    private void OnListHover(object sender, MouseEventArgs e) { if (ItemUnder(e) is { } item && !ReferenceEquals(item, _list.SelectedItem)) _list.SelectedItem = item; }
    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        // Pressing an option must not move focus out of the field, which would close the popup first.
        if (ItemUnder(e) is MentionPerson person) Choose(person); else if (ItemUnder(e) is MentionChannel channel) Choose(channel); else return;
        e.Handled = true;
    }
    private void RefreshSuggestions()
    {
        var caret = CaretIndex; var before = Text[..Math.Clamp(caret, 0, Text.Length)]; var at = before.LastIndexOf('@'); var hash = before.LastIndexOf('#'); var start = Math.Max(at, hash);
        if (start < 0 || (start > 0 && !char.IsWhiteSpace(before[start - 1]))) { _popup.IsOpen = false; return; }
        var query = before[(start + 1)..]; if (query.Contains(' ') || query.Contains('\n')) { _popup.IsOpen = false; return; }
        _symbol = before[start]; _trigger = query;
        _list.ItemsSource = (_symbol == '@' ? People.Where(p => p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>() : Channels.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Cast<object>()).Take(Math.Max(1, MaxSuggestions)).ToList();
        _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
        bool none = _list.Items.Count == 0;
        _list.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        _empty.Text = $"No {(_symbol == '@' ? "people" : "channels")} match \u201C{query}\u201D";
        _popup.PlacementTarget = this;
        bool opening = !_popup.IsOpen;
        _popup.IsOpen = true;
        if (opening) PlayOpen();
        else { _popup.HorizontalOffset += 0; Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, MoveHighlight); }
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

    private sealed class SuggestionTemplateSelector : DataTemplateSelector
    {
        private readonly DataTemplate _person;
        private readonly DataTemplate _channel;
        public SuggestionTemplateSelector(DataTemplate person, DataTemplate channel) { _person = person; _channel = channel; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container) => item is MentionChannel ? _channel : _person;
    }
}

/// <summary>Paints the placeholder and the token chips under the text, in the field's own coordinates.</summary>
internal sealed class MentionTokenLayer : FrameworkElement
{
    private MentionInput? _owner;
    public MentionInput? Owner { get => _owner; set { _owner = value; InvalidateVisual(); } }
    public MentionTokenLayer() => IsHitTestVisible = false;
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_owner is null) return;
        if (string.IsNullOrEmpty(_owner.Text))
        {
            if (!string.IsNullOrEmpty(_owner.Placeholder))
            {
                var brush = (_owner.TryFindResource("ArcWpf.Brush.TextMuted") as Brush) ?? Brushes.Gray;
                var typeface = new Typeface(_owner.FontFamily, _owner.FontStyle, _owner.FontWeight, _owner.FontStretch);
                var dpi = VisualTreeHelper.GetDpi(_owner).PixelsPerDip;
                var text = new FormattedText(_owner.Placeholder, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, _owner.FontSize, brush, dpi);
                var at = _owner.GetRectFromCharacterIndex(0);
                double x = at.IsEmpty || double.IsNaN(at.X) ? _owner.Padding.Left : at.X, y = at.IsEmpty || double.IsNaN(at.Y) ? _owner.Padding.Top : at.Y + (at.Height - text.Height) / 2;
                drawingContext.DrawText(text, new Point(x, y));
            }
            return;
        }
        if (_owner.Mentions.Count == 0) return;
        var accent = (_owner.TryFindResource("ArcWpf.Brush.AccentSubtle") as Brush) ?? new SolidColorBrush(Color.FromArgb(36, 96, 120, 255));
        var channel = (_owner.TryFindResource("ArcWpf.Brush.MentionChannelToken") as Brush) ?? new SolidColorBrush(Color.FromArgb(32, 128, 128, 128));
        foreach (var token in _owner.Mentions.ToArray())
        {
            var start = Math.Clamp(token.Start, 0, _owner.Text.Length); var end = Math.Clamp(token.End, start, _owner.Text.Length);
            Rect? line = null;
            for (var index = start; index < end; index++)
            {
                var leading = _owner.GetRectFromCharacterIndex(index, false);
                var trailing = _owner.GetRectFromCharacterIndex(index, true);
                if (leading.IsEmpty || double.IsNaN(leading.X) || trailing.IsEmpty || double.IsNaN(trailing.X)) continue;
                var rect = new Rect(Math.Min(leading.Left, trailing.Left), leading.Top, Math.Abs(trailing.Left - leading.Left), leading.Height);
                if (line is null || Math.Abs(line.Value.Top - rect.Top) > 0.5) { if (line is not null) Draw(line.Value, token.Kind == MentionKind.Channel ? channel : accent, drawingContext); line = rect; }
                else line = new Rect(Math.Min(line.Value.Left, rect.Left), line.Value.Top, Math.Max(line.Value.Right, rect.Right) - Math.Min(line.Value.Left, rect.Left), Math.Max(line.Value.Height, rect.Height));
            }
            if (line is not null) Draw(line.Value, token.Kind == MentionKind.Channel ? channel : accent, drawingContext);
        }
    }
    private static void Draw(Rect rect, Brush brush, DrawingContext context)
    {
        rect.Inflate(3, 0); rect.Y += (rect.Height - 22) / 2; rect.Height = 22; context.DrawRoundedRectangle(brush, null, rect, 6, 6);
    }
}
