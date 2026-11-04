using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum CitationStatus { Searching, Done, Error }

/// <summary>One cited source. Sources stay in citation order: source 1 is cited as [1].</summary>
public sealed record CitationSource(string Title, string Url)
{
    public string Id { get; init; } = Url;
    /// <summary>Readable site name for the preview and the stack.</summary>
    public string? Site { get; init; }
    /// <summary>The passage the answer relied on, shown in the preview (four lines).</summary>
    public string? Snippet { get; init; }
    public string? Published { get; init; }
    public AiMark? Mark { get; init; }

    public string Domain => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host : Url;
    public string Path => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.PathAndQuery.TrimEnd('/') : "";
    public string Name => Site ?? Domain;
}

public sealed class CitationEventArgs(CitationSource source, int number) : EventArgs
{
    public CitationSource Source { get; } = source;
    /// <summary>The 1-based citation number.</summary>
    public int Number { get; } = number;
}

/// <summary>The hover preview of a source: its site, title, the passage it was cited for and where it lives. It never takes focus.</summary>
public sealed class SourcePopover
{
    private readonly Popup _popup = new() { AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Custom, Focusable = false, IsHitTestVisible = false };
    private readonly Border _surface = new() { Width = 304, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Margin = new Thickness(24), Padding = new Thickness(16, 12, 16, 12) };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _rise = new();
    private FrameworkElement? _target;
    private CitationSource? _source;
    private DispatcherTimer? _watch;
    private const double Gap = 8;

    public SourcePopover()
    {
        _surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        _surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _surface.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 32, ShadowDepth = 12, Direction = 270, Opacity = .12, Color = Colors.Black };
        _surface.RenderTransformOrigin = new Point(.5, .5);
        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_rise);
        _surface.RenderTransform = transforms;
        _popup.Child = _surface;
        _popup.CustomPopupPlacementCallback = Place;
    }

    public bool IsOpen => _popup.IsOpen;
    public CitationSource? Source => _source;
    /// <summary>What the preview points at.</summary>
    public FrameworkElement? Target => _target;

    internal Border SurfaceForTest => _surface;
    internal Popup PopupForTest => _popup;

    // Above the target, aligned to its edge so it stays with the component; below when there is no room. The size given here leaves out the gutter margin.
    private CustomPopupPlacement[] Place(Size popup, Size target, Point offset)
    {
        double scale = _target is null ? 1 : PresentationSource.FromVisual(_target)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double gutter = 24 * scale, gap = Gap * scale;
        bool leftEdge = true;
        if (_target?.IsLoaded == true && Window.GetWindow(_target) is { IsLoaded: true } window)
        {
            var at = _target.TranslatePoint(new Point(), window);
            leftEdge = at.X + (304 + 24) <= window.ActualWidth;
        }
        double x = leftEdge ? -gutter : target.Width - popup.Width - gutter;
        return
        [
            new(new Point(x, -popup.Height - gutter - gap), PopupPrimaryAxis.Horizontal),
            new(new Point(x, target.Height - gutter + gap), PopupPrimaryAxis.Horizontal),
        ];
    }

    /// <summary>Opens over the target, or switches to it at once when already open.</summary>
    public void Show(FrameworkElement target, CitationSource source, int number)
    {
        bool wasOpen = _popup.IsOpen;
        _target = target;
        _source = source;
        _surface.Child = Build(source);
        _popup.PlacementTarget = target;
        if (wasOpen) { _popup.HorizontalOffset += .01; _popup.HorizontalOffset -= .01; return; }
        _popup.IsOpen = true;
        if (!AiUi.Moving(target)) { Tween.Set(_surface, UIElement.OpacityProperty, 1); return; }
        _surface.Opacity = 0;
        _scale.ScaleX = _scale.ScaleY = .98;
        _rise.Y = 4;
        Tween.To(_surface, UIElement.OpacityProperty, 1, .16, ArcEasing.Enter);
        Tween.To(_scale, ScaleTransform.ScaleXProperty, 1, .16, ArcEasing.Enter);
        Tween.To(_scale, ScaleTransform.ScaleYProperty, 1, .16, ArcEasing.Enter);
        Tween.To(_rise, TranslateTransform.YProperty, 0, .16, ArcEasing.Enter);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);
    private struct CursorPoint { public int X, Y; }

    // Showing the preview's window can report a leave while the pointer has not moved off its target, so the real cursor decides.
    internal static bool StillOver(FrameworkElement target)
    {
        if (!target.IsVisible || !GetCursorPos(out var cursor)) { return false; }
        var at = target.PointFromScreen(new Point(cursor.X, cursor.Y));
        return at.X >= 0 && at.Y >= 0 && at.X < target.ActualWidth && at.Y < target.ActualHeight;
    }

    // A report like that leaves no later leave to count on, so a short watch takes over until the pointer really goes.
    internal void WatchLeave(FrameworkElement target, Action left)
    {
        _watch?.Stop();
        var watch = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(120) };
        watch.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_watch, watch) || !ReferenceEquals(_target, target)) { watch.Stop(); return; }
            if (StillOver(target)) { return; }
            watch.Stop();
            left();
        };
        _watch = watch;
        watch.Start();
    }

    public void Hide()
    {
        _watch?.Stop();
        _popup.IsOpen = false;
        _target = null;
    }

    private static FrameworkElement Build(CitationSource source)
    {
        var column = new StackPanel();
        var head = new Grid { Height = 18 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var mark = AiUi.Mark(source.Mark, 16, source.Name);
        mark.HorizontalAlignment = HorizontalAlignment.Left;
        head.Children.Add(mark);
        var site = AiUi.Line(source.Name, 12, 18, "Foreground", true);
        Grid.SetColumn(site, 1);
        head.Children.Add(site);
        if (!string.IsNullOrEmpty(source.Published))
        {
            var date = AiUi.Line(source.Published, 12, 18, "TextMuted", false, false);
            date.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(date, 2);
            head.Children.Add(date);
        }
        column.Children.Add(head);
        var title = AiUi.Wrapped(source.Title, 14, 20, "Foreground", true);
        title.MaxHeight = 40;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(0, 4, 0, 0);
        column.Children.Add(title);
        if (!string.IsNullOrEmpty(source.Snippet))
        {
            var snippet = AiUi.Wrapped(source.Snippet, 12, 18, "TextSecondary");
            snippet.MaxHeight = 72;
            snippet.TextTrimming = TextTrimming.CharacterEllipsis;
            snippet.Margin = new Thickness(0, 4, 0, 0);
            column.Children.Add(snippet);
        }
        var url = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var path = AiUi.Line(source.Domain + source.Path, 12, 18, "TextMuted");
        path.MaxWidth = 250;
        url.Children.Add(path);
        var out_ = AiUi.Glyph(Lucide.ExternalLink, 14, "TextMuted");
        out_.Margin = new Thickness(4, 0, 0, 0);
        url.Children.Add(out_);
        column.Children.Add(url);
        return column;
    }
}

/// <summary>An inline [n] chip for answer text, styled like the number on each card. Inside a CitationList it highlights its card on click.</summary>
public class SourceCitation : System.Windows.Controls.Button
{
    public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(nameof(Number), typeof(int), typeof(SourceCitation), new FrameworkPropertyMetadata(1, (d, _) => ((SourceCitation)d).Sync()));
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(CitationSource), typeof(SourceCitation), new FrameworkPropertyMetadata(null, (d, _) => ((SourceCitation)d).Sync()));
    public static readonly DependencyProperty IsLitProperty = DependencyProperty.Register(nameof(IsLit), typeof(bool), typeof(SourceCitation), new FrameworkPropertyMetadata(false, (d, _) => ((SourceCitation)d).Paint()));

    public int Number { get => (int)GetValue(NumberProperty); set => SetValue(NumberProperty, value); }
    public CitationSource? Source { get => (CitationSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    /// <summary>Accent while its card is highlighted.</summary>
    public bool IsLit { get => (bool)GetValue(IsLitProperty); set => SetValue(IsLitProperty, value); }

    private readonly Border _face = new() { MinWidth = 18, Height = 18, CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 0, 5, 0) };
    private readonly TextBlock _text = new() { FontSize = 11, FontWeight = FontWeights.Medium, LineHeight = 18, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Center };
    private static SourcePopover? _sharedPreview;
    private readonly DispatcherTimer _intent = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(320) };

    internal static SourcePopover SharedPreview => _sharedPreview ??= new SourcePopover();

    public SourceCitation()
    {
        OverridesDefaultStyle = true;
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = presenter };
        template.Seal();
        Template = template;
        FocusVisualStyle = ArcChromeButton.FocusFor(6);
        _face.Child = _text;
        Content = _face;
        Cursor = Cursors.Hand;
        Margin = new Thickness(2, 0, 2, 0);
        Sync();
        Paint();
        ThemeWatch.Add(this, Paint);
        MouseEnter += (_, _) => { if (Source is not null && !IsLitOwnedByList) { _intent.Start(); } };
        MouseLeave += (_, _) =>
        {
            if (ReferenceEquals(SharedPreview.Target, this) && SourcePopover.StillOver(this)) { SharedPreview.WatchLeave(this, SharedPreview.Hide); return; }
            _intent.Stop();
            if (ReferenceEquals(SharedPreview.Target, this)) { SharedPreview.Hide(); }
        };
        GotKeyboardFocus += (_, _) => { if (Source is not null && ThreadUi.FocusVisible(this)) { Preview(); } };
        LostKeyboardFocus += (_, _) => { if (ReferenceEquals(SharedPreview.Target, this)) { SharedPreview.Hide(); } };
        _intent.Tick += (_, _) => { _intent.Stop(); Preview(); };
        Unloaded += (_, _) => { _intent.Stop(); if (ReferenceEquals(SharedPreview.Target, this)) { SharedPreview.Hide(); } };
    }

    private bool IsLitOwnedByList => false;
    private void Preview() { if (Source is { } source && IsVisible) { SharedPreview.Show(this, source, Number); } }

    private void Sync()
    {
        _text.Text = Number.ToString(CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this, Source is { } source ? $"Source {Number}: {source.Title}, {source.Domain}" : $"Source {Number}");
    }

    private void Paint()
    {
        _face.SetResourceReference(Border.BackgroundProperty, IsLit ? "ArcWpf.Brush.Accent" : "ArcWpf.Brush.SurfaceMuted");
        _text.SetResourceReference(TextBlock.ForegroundProperty, IsLit ? "ArcWpf.Brush.AccentForeground" : "ArcWpf.Brush.TextSecondary");
    }

    internal void IntentForTest() => Preview();
}

/// <summary>
/// A row of cited sources under an AI answer. Cards stream into reserved slots while searching; past Max the rest collapse into a
/// stack of marks; hovering a card lifts a preview with the passage, and a [n] citation highlights its card.
/// </summary>
public class CitationList : ContentControl
{
    public static readonly DependencyProperty SourcesProperty = Prop(nameof(Sources), typeof(IReadOnlyList<CitationSource>), null);
    public static readonly DependencyProperty StatusProperty = Prop(nameof(Status), typeof(CitationStatus), CitationStatus.Done);
    public static readonly DependencyProperty TotalProperty = Prop(nameof(Total), typeof(int?), null);
    public static readonly DependencyProperty QueryProperty = Prop(nameof(Query), typeof(string), null);
    public static readonly DependencyProperty DurationMsProperty = Prop(nameof(DurationMs), typeof(double?), null);
    public static readonly DependencyProperty MaxProperty = Prop(nameof(Max), typeof(int), 3);
    public static readonly DependencyProperty ErrorProperty = Prop(nameof(Error), typeof(string), null);
    public static readonly DependencyProperty ShowStatusProperty = Prop(nameof(ShowStatus), typeof(bool), true);
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(CitationList), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CitationList)d).Refresh(true)));
    public static readonly DependencyProperty HighlightedProperty = DependencyProperty.Register(nameof(Highlighted), typeof(int?), typeof(CitationList), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CitationList)d).Light()));
    public static readonly DependencyProperty LabelProperty = Prop(nameof(Label), typeof(string), "Sources");

    private static DependencyProperty Prop(string name, Type type, object? fallback) => DependencyProperty.Register(name, type, typeof(CitationList), new FrameworkPropertyMetadata(fallback, (d, _) => ((CitationList)d).Refresh(true)));

    /// <summary>The sources in citation order. Append as results arrive.</summary>
    public IReadOnlyList<CitationSource>? Sources { get => (IReadOnlyList<CitationSource>?)GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }
    public CitationStatus Status { get => (CitationStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    /// <summary>How many sources the search will return: drives "Searching 3 of 7" and reserves slots, so the row never shifts while it fills.</summary>
    public int? Total { get => (int?)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }
    public string? Query { get => (string?)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public double? DurationMs { get => (double?)GetValue(DurationMsProperty); set => SetValue(DurationMsProperty, value); }
    /// <summary>Cards shown before the rest collapse into the stack.</summary>
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public string? Error { get => (string?)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public bool ShowStatus { get => (bool)GetValue(ShowStatusProperty); set => SetValue(ShowStatusProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>The highlighted source number (1-based). A citation click highlights its card for 2.6 s.</summary>
    public int? Highlighted { get => (int?)GetValue(HighlightedProperty); set => SetValue(HighlightedProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public event EventHandler<CitationEventArgs>? SourceClicked;
    public event EventHandler? RetryRequested;

    private const double NarrowWidth = 540, CardHeight = 84, CardGap = 8;

    private readonly Grid _status = new() { MinHeight = 20, Margin = new Thickness(0, 0, 0, 16) };
    private readonly Decorator _statusIcon = new() { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly FadeSwap _statusText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly Decorator _statusAction = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentPresenter _answer = new();
    private readonly SpringHeightBox _answerClip = new(Tween.Smooth);
    private readonly UniformGrid _cards = new();
    private readonly SpringHeightBox _cardsClip = new(Tween.Smooth);
    private readonly Dictionary<string, FrameworkElement> _cardViews = [];
    private readonly SourcePopover _preview = new();
    private readonly DispatcherTimer _hover = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(320) };
    private readonly DispatcherTimer _unlight = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(2.6) };
    private (FrameworkElement Card, CitationSource Source, int Number)? _hovered;
    private bool _built;
    private CitationStatus? _announced;
    private string _statusIconKey = "", _statusKey = "", _actionKey = "";
    private int _columns = 2;

    static CitationList() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CitationList), new FrameworkPropertyMetadata(typeof(CitationList)));

    public CitationList()
    {
        _hover.Tick += (_, _) => { _hover.Stop(); if (_hovered is { } shown) { _preview.Show(shown.Card, shown.Source, shown.Number); } };
        _unlight.Tick += (_, _) => { _unlight.Stop(); SetCurrentValue(HighlightedProperty, null); };
        Unloaded += (_, _) => { _hover.Stop(); _unlight.Stop(); _preview.Hide(); };
        SizeChanged += (_, e) => { if (e.WidthChanged) { Columns(); } };
        AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OnCitationClick));
    }

    private IReadOnlyList<CitationSource> Items => Sources ?? [];

    internal string StatusForTest => _statusKey;
    internal bool StatusShownForTest => _status.Visibility == Visibility.Visible;
    internal int ColumnsForTest => _columns;
    internal string[] CardKindsForTest => [.. _cards.Children.OfType<FrameworkElement>().Select(card => card.Tag as string ?? "")];
    internal FrameworkElement CardForTest(int index) => (FrameworkElement)_cards.Children[index];
    internal SourcePopover PreviewForTest => _preview;
    internal void HoverForTest(int index, bool instant = true) { Hover((FrameworkElement)_cards.Children[index], Items[index], index + 1); if (instant) { _hover.Stop(); _preview.Show((FrameworkElement)_cards.Children[index], Items[index], index + 1); } }
    internal void LeaveForTest() { _hover.Stop(); _hovered = null; _preview.Hide(); }
    internal bool HoverPendingForTest => _hover.IsEnabled;
    internal void UnlightNowForTest() { if (_unlight.IsEnabled) { _unlight.Stop(); SetCurrentValue(HighlightedProperty, null); } }
    internal System.Windows.Controls.Button? StatusActionForTest => _statusAction.Child as System.Windows.Controls.Button;
    internal bool IsLitForTest(int index) => _cards.Children[index] is System.Windows.Controls.Button { Tag: "card" } button && (button.BorderBrush as SolidColorBrush)?.Color == Tween.Resolve(this, "ArcWpf.Brush.BorderStrong");

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        _status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.ColumnDefinitions.Add(new ColumnDefinition());
        _status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Children.Add(_statusIcon);
        Grid.SetColumn(_statusText, 1);
        _status.Children.Add(_statusText);
        Grid.SetColumn(_statusAction, 2);
        _status.Children.Add(_statusAction);
        _answerClip.Child = _answer;
        _cardsClip.Child = _cards;
        var column = new StackPanel();
        column.Children.Add(_status);
        column.Children.Add(_answerClip);
        column.Children.Add(_cardsClip);
        AutomationProperties.SetName(_cards, Label);
        host.Child = column;
        _built = true;
        Refresh(false);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        _answer.Content = newContent;
        _answerClip.Visibility = newContent is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Columns()
    {
        int columns = ActualWidth > 0 && ActualWidth < NarrowWidth ? 2 : Math.Max(1, Max + 1);
        if (columns != _columns) { _columns = columns; _cards.Columns = columns; }
    }

    private void Refresh(bool animate)
    {
        if (!_built) { return; }
        Columns();
        _cards.Columns = _columns;
        StatusLine();
        Cards(animate);
        Light();
        if (_announced != Status)
        {
            if (_announced is not null)
            {
                Announcer.Say(this, Status switch
                {
                    CitationStatus.Searching => $"Searching for {Query}",
                    CitationStatus.Error => $"Search failed. {Error}",
                    _ => Items.Count == 0 ? "No sources found" : $"{Items.Count} sources found",
                });
            }
            _announced = Status;
        }
    }

    private void StatusLine()
    {
        _status.Visibility = ShowStatus ? Visibility.Visible : Visibility.Collapsed;
        int count = Items.Count;
        bool none = Status == CitationStatus.Done && count == 0;
        string iconKey = Status == CitationStatus.Searching ? "spin" : Status == CitationStatus.Error ? "alert" : none ? "none" : "globe";
        if (iconKey != _statusIconKey)
        {
            _statusIconKey = iconKey;
            _statusIcon.Child = iconKey switch
            {
                "spin" => new AiSpinner(14, "TextMuted"),
                "alert" => AiUi.Glyph(Lucide.CircleAlert, 14, "ArcDanger"),
                "none" => AiUi.Glyph(Lucide.SearchX, 14, "TextSecondary"),
                _ => AiUi.Glyph(Lucide.Globe, 14, "TextSecondary"),
            };
        }
        (string lead, string? detail) = Status switch
        {
            CitationStatus.Searching => (Total is { } total ? $"Searching {count} of {total}" : "Searching", string.IsNullOrEmpty(Query) ? null : Query),
            CitationStatus.Error => ("Search failed", Error),
            _ when none => ("No sources found", Query),
            _ => (count == 1 ? "1 source" : $"{count} sources", DurationMs is { } ms ? AiUi.Duration(ms) : null),
        };
        string key = lead + "|" + detail;
        if (key != _statusKey)
        {
            bool kindChanged = _statusKey.Split(' ').FirstOrDefault() != lead.Split(' ').FirstOrDefault();
            _statusKey = key;
            var line = new TextBlock { FontSize = 14, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextTrimming = TextTrimming.CharacterEllipsis };
            var first = new System.Windows.Documents.Run(lead);
            first.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, Status == CitationStatus.Done && !none ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary");
            line.Inlines.Add(first);
            if (!string.IsNullOrEmpty(detail))
            {
                var second = new System.Windows.Documents.Run(" · " + detail);
                second.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
                line.Inlines.Add(second);
            }
            if (!kindChanged && _statusText.Current is not null) { _statusText.Children.Clear(); _statusText.Set(line); } else { _statusText.Set(line); }
        }
        bool retry = Status == CitationStatus.Error || none;
        bool toggle = !retry && count > Max;
        string action = retry ? (none ? "again" : "retry") : toggle ? (IsExpanded ? "fewer" : $"all{count}") : "";
        if (action == _actionKey) { return; }
        _actionKey = action;
        if (retry)
        {
            var button = AiUi.Outline(none ? "Search again" : "Retry", Lucide.RotateCcw);
            button.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
            _statusAction.Child = button;
        }
        else if (toggle)
        {
            var chevron = new TurningChevron(14);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(AiUi.Line(IsExpanded ? "Show fewer" : $"Show all {count}", 12, 16.8, "TextSecondary", false, false));
            chevron.Margin = new Thickness(4, 0, 0, 0);
            row.Children.Add(chevron);
            chevron.Point(IsExpanded);
            var button = new ToolbarButton(8) { Height = 28, Padding = new Thickness(8, 0, 8, 0), Content = row, Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, "TextSecondary") };
            button.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
            _statusAction.Child = button;
        }
        else { _statusAction.Child = null; }
    }

    private void Cards(bool animate)
    {
        var items = Items;
        int reserved = Status == CitationStatus.Searching && Total is { } total ? total : items.Count;
        bool stacked = !IsExpanded && reserved > Max + 1;
        int direct = stacked ? Max : reserved;
        var wanted = new List<FrameworkElement>();
        bool moving = animate && AiUi.Moving(this);
        for (int index = 0; index < direct; index++)
        {
            if (index >= items.Count) { wanted.Add(Slot(index)); continue; }
            var source = items[index];
            string key = "card:" + source.Id;
            if (!_cardViews.TryGetValue(key, out var card))
            {
                card = Card(source, index + 1);
                _cardViews[key] = card;
                if (moving) { Arrive(card); }
            }
            wanted.Add(card);
        }
        if (stacked)
        {
            string key = $"stack:{string.Join(",", items.Skip(Max).Select(item => item.Id))}|{reserved}";
            if (!_cardViews.TryGetValue(key, out var stack))
            {
                foreach (string old in _cardViews.Keys.Where(old => old.StartsWith("stack:", StringComparison.Ordinal)).ToList()) { _cardViews.Remove(old); }
                stack = Stack(items.Skip(Max).ToList(), reserved - Max);
                _cardViews[key] = stack;
                if (moving) { Arrive(stack); }
            }
            wanted.Add(stack);
        }
        if (!_cards.Children.OfType<FrameworkElement>().SequenceEqual(wanted))
        {
            if (moving && _cardsClip.IsLoaded) { _cardsClip.MorphNext(); }
            _cards.Children.Clear();
            foreach (var card in wanted) { _cards.Children.Add(card); }
        }
        _cardsClip.Visibility = wanted.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // Skeleton bars sit on the same 18 px lines as the text that replaces them.
    private FrameworkElement Slot(int index)
    {
        if (_cardViews.TryGetValue("slot:" + index, out var slot)) { return slot; }
        var column = new StackPanel();
        Bar(column, 92, 0);
        Bar(column, 180, 8);
        var card = new Border { Height = CardHeight, Margin = new Thickness(0, 0, CardGap, CardGap), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(13, 15, 13, 13), Child = column, Tag = "slot" };
        card.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        _cardViews["slot:" + index] = card;
        return card;
    }

    private static void Bar(StackPanel column, double width, double top)
    {
        var bar = new Border { Width = width, Height = 12, CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, top, 0, 0) };
        bar.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        column.Children.Add(bar);
    }

    // Cards fade in with a 4 px rise in 180 ms as they arrive. Nothing bounces.
    private static void Arrive(FrameworkElement card)
    {
        var rise = new TranslateTransform(0, 4);
        card.RenderTransform = rise;
        card.Opacity = 0;
        Tween.To(card, OpacityProperty, 1, .18, ArcEasing.Enter);
        Tween.To(rise, TranslateTransform.YProperty, 0, .18, ArcEasing.Enter);
    }

    private FrameworkElement Card(CitationSource source, int number)
    {
        var column = new StackPanel();
        var top = new StackPanel { Orientation = Orientation.Horizontal, Height = 18 };
        var mark = AiUi.Mark(source.Mark, 14, source.Name);
        mark.Margin = new Thickness(0, 0, 8, 0);
        mark.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(mark);
        var domain = AiUi.Line(source.Domain, 12, 18, "TextMuted");
        domain.MaxWidth = 130;
        top.Children.Add(domain);
        var chip = new Border { MinWidth = 18, Height = 18, CornerRadius = new CornerRadius(6), Margin = new Thickness(8, 0, 0, 0), Child = new TextBlock { Text = number.ToString(CultureInfo.InvariantCulture), FontSize = 11, FontWeight = FontWeights.Medium, LineHeight = 18, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Center } };
        chip.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        ((TextBlock)chip.Child).SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        top.Children.Add(chip);
        column.Children.Add(top);
        var title = AiUi.Wrapped(source.Title, 13, 18);
        title.MaxHeight = 36;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(0, 4, 0, 0);
        column.Children.Add(title);
        ToolbarButton card = null!;
        card = new ToolbarButton(14, 1)
        {
            Tag = "card",
            Height = CardHeight,
            Margin = new Thickness(0, 0, CardGap, CardGap),
            Padding = new Thickness(13, 12, 13, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = column,
            Roles = self => (Highlighted == number ? "SurfaceMuted" : self.IsMouseOver ? "SurfaceMuted" : "Surface", Highlighted == number ? "BorderStrong" : self.IsMouseOver ? "Border" : "BorderSubtle", "Foreground"),
        };
        AutomationProperties.SetName(card, $"{number}. {source.Title}, {source.Domain}");
        card.Click += (_, _) => SourceClicked?.Invoke(this, new CitationEventArgs(source, number));
        card.MouseEnter += (_, _) => Hover(card, source, number);
        card.MouseLeave += (_, _) =>
        {
            if (ReferenceEquals(_preview.Target, card) && SourcePopover.StillOver(card)) { _preview.WatchLeave(card, Leave); return; }
            Leave();
        };
        return card;
    }

    private FrameworkElement Stack(IReadOnlyList<CitationSource> rest, int more)
    {
        var column = new StackPanel();
        var marks = new Canvas { Height = 18, HorizontalAlignment = HorizontalAlignment.Left, Width = 20 + Math.Max(0, Math.Min(rest.Count, 3) - 1) * 15 };
        for (int index = 0; index < Math.Min(rest.Count, 3); index++)
        {
            var disc = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Child = new Decorator { Child = AiUi.Mark(rest[index].Mark, 12, rest[index].Name), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            disc.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            Canvas.SetLeft(disc, index * 15);
            Canvas.SetTop(disc, -1);
            marks.Children.Add(disc);
        }
        column.Children.Add(marks);
        var count = AiUi.Line($"+{more} more", 13, 18);
        count.Margin = new Thickness(0, 4, 0, 0);
        column.Children.Add(count);
        column.Children.Add(AiUi.Line(string.Join(", ", rest.Select(source => source.Name).Distinct()), 12, 18, "TextMuted"));
        var card = new ToolbarButton(14, 1)
        {
            Tag = "stack",
            Height = CardHeight,
            Margin = new Thickness(0, 0, CardGap, CardGap),
            Padding = new Thickness(13, 12, 13, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = column,
            Roles = self => (self.IsMouseOver ? "SurfaceMuted" : "Surface", self.IsMouseOver ? "Border" : "BorderSubtle", "Foreground"),
        };
        AutomationProperties.SetName(card, $"Show {more} more sources: {string.Join(", ", rest.Select(source => source.Name).Distinct())}");
        card.Click += (_, _) => SetCurrentValue(IsExpandedProperty, true);
        return card;
    }

    // Previews open after a 320 ms hover intent, then switch instantly between cards.
    private void Hover(FrameworkElement card, CitationSource source, int number)
    {
        _hovered = (card, source, number);
        if (_preview.IsOpen) { _hover.Stop(); _preview.Show(card, source, number); return; }
        _hover.Stop();
        _hover.Start();
    }

    private void Leave()
    {
        _hover.Stop();
        _hovered = null;
        _preview.Hide();
    }

    private void OnCitationClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not SourceCitation chip) { return; }
        int number = chip.Number;
        // A citation pointing into the stack opens it first.
        if (number > Max && !IsExpanded && Items.Count > Max + 1) { SetCurrentValue(IsExpandedProperty, true); }
        SetCurrentValue(HighlightedProperty, number);
        _unlight.Stop();
        _unlight.Start();
        Announcer.Say(this, $"Source {number} highlighted");
    }

    private void Light()
    {
        if (!_built) { return; }
        foreach (var card in _cards.Children.OfType<ArcChromeButton>()) { card.Refresh(true); }
        Walk(_answer);
    }

    private void Walk(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is SourceCitation chip)
            {
                chip.IsLit = chip.Number == Highlighted;
                if (chip.Source is null && chip.Number >= 1 && chip.Number <= Items.Count) { chip.Source = Items[chip.Number - 1]; }
            }
            Walk(child);
        }
    }

    internal void ClickCitationForTest(SourceCitation chip) => OnCitationClick(this, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, chip));
}
