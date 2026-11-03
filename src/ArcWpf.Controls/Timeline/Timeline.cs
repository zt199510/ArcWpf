using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class TimelineEvent : INotifyPropertyChanged
{
    private DateTime _at = DateTime.Now;
    private string _title = string.Empty, _actor = string.Empty, _meta = string.Empty, _tone = "neutral";
    private object? _detail, _icon;
    private ImageSource? _avatar;
    private bool _isExpanded, _isNew;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime At { get => _at; set => Set(ref _at, value); }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Actor { get => _actor; set => Set(ref _actor, value); }
    public string Meta { get => _meta; set => Set(ref _meta, value); }
    /// <summary>Text or an element shown when the row opens. Rows without detail do not expand.</summary>
    public object? Detail { get => _detail; set => Set(ref _detail, value); }
    /// <summary>A portrait for a person's update. It takes the place of the icon and tone.</summary>
    public ImageSource? Avatar { get => _avatar; set => Set(ref _avatar, value); }
    /// <summary>Path data for a Lucide-style 24 unit glyph, or an element, shown on a system event's node.</summary>
    public object? Icon { get => _icon; set => Set(ref _icon, value); }
    public string Tone { get => _tone; set => Set(ref _tone, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }
    public bool IsNew { get => _isNew; internal set => Set(ref _isNew, value); }
    public DateTime DayKey => At.ToLocalTime().Date;
    public string DisplayTime { get; private set; } = string.Empty;
    public string FullTime { get; private set; } = string.Empty;
    internal void SetDisplayTime(string display, string full)
    {
        DisplayTime = display;
        FullTime = full;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTime)));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(At)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayKey)));
    }
}

/// <summary>An activity feed: updates grouped by day under sticky labels, a marker and connecting line for each, and rows that open for detail. New updates push in at the top.</summary>
[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_Events", Type = typeof(StackPanel))]
public class Timeline : Control
{
    private const double Step = .09, Feather = 36;
    public static readonly DependencyProperty EventsProperty = DependencyProperty.Register(nameof(Events), typeof(ObservableCollection<TimelineEvent>), typeof(Timeline), new PropertyMetadata(null, OnEventsReplaced));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Timeline), new PropertyMetadata("Activity timeline", (d, e) => AutomationProperties.SetName(d, (string)e.NewValue)));
    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(nameof(Now), typeof(DateTime), typeof(Timeline), new PropertyMetadata(DateTime.UtcNow, OnTimeChanged));
    public static readonly DependencyProperty TimeZoneIdProperty = DependencyProperty.Register(nameof(TimeZoneId), typeof(string), typeof(Timeline), new PropertyMetadata("UTC", OnTimeChanged));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(Timeline), new PropertyMetadata("en-US", OnTimeChanged));
    public static readonly DependencyProperty DefaultExpandedProperty = DependencyProperty.Register(nameof(DefaultExpanded), typeof(ObservableCollection<string>), typeof(Timeline), new PropertyMetadata(null, OnDefaultExpandedChanged));
    public static readonly DependencyProperty ScrollToNewProperty = DependencyProperty.Register(nameof(ScrollToNew), typeof(bool), typeof(Timeline), new PropertyMetadata(true));
    public ObservableCollection<TimelineEvent> Events { get => (ObservableCollection<TimelineEvent>)GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public DateTime Now { get => (DateTime)GetValue(NowProperty); set => SetValue(NowProperty, value); }
    public string TimeZoneId { get => (string)GetValue(TimeZoneIdProperty); set => SetValue(TimeZoneIdProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    public ObservableCollection<string> DefaultExpanded { get => (ObservableCollection<string>)GetValue(DefaultExpandedProperty); set => SetValue(DefaultExpandedProperty, value); }
    public bool ScrollToNew { get => (bool)GetValue(ScrollToNewProperty); set => SetValue(ScrollToNewProperty, value); }

    private readonly Dictionary<string, TimelineRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimelineDay> _days = new(StringComparer.Ordinal);
    private bool _hasRendered;
    private ScrollViewer? _scrollViewer;
    private StackPanel? _panel;
    private double _revealAt;

    static Timeline() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(typeof(Timeline)));

    public Timeline()
    {
        Events = [];
        DefaultExpanded = [];
        AutomationProperties.SetName(this, Label);
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RevealInView);
    }

    private static void OnEventsReplaced(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (Timeline)d;
        if (e.OldValue is ObservableCollection<TimelineEvent> old) old.CollectionChanged -= timeline.OnEventsChanged;
        if (e.NewValue is ObservableCollection<TimelineEvent> next) next.CollectionChanged += timeline.OnEventsChanged;
        timeline.ApplyDefaultExpanded();
        timeline.UpdateTimeLabels();
        timeline.Sync();
    }

    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            var ids = DefaultExpanded?.ToHashSet(StringComparer.Ordinal);
            foreach (TimelineEvent item in e.NewItems)
            {
                if (ids?.Contains(item.Id) == true) item.IsExpanded = true;
                item.IsNew = _hasRendered;
                if (!item.IsNew) continue;
                var reset = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
                reset.Tick += (_, _) => { reset.Stop(); item.IsNew = false; };
                reset.Start();
            }
        }
        UpdateTimeLabels();
        Sync();
        if (_hasRendered && ScrollToNew && e.NewItems is { Count: > 0 }) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => _scrollViewer?.ScrollToTop()));
    }

    private static void OnDefaultExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).ApplyDefaultExpanded();

    private void ApplyDefaultExpanded()
    {
        if (Events is null || DefaultExpanded is null) return;
        var ids = DefaultExpanded.ToHashSet(StringComparer.Ordinal);
        foreach (var item in Events) if (ids.Contains(item.Id)) item.IsExpanded = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_scrollViewer is not null) _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        _panel = GetTemplateChild("PART_Events") as StackPanel;
        if (_scrollViewer is not null) _scrollViewer.ScrollChanged += OnScrollChanged;
        _rows.Clear();
        _days.Clear();
        _hasRendered = true;
        ApplyDefaultExpanded();
        UpdateTimeLabels();
        Sync();
    }

    private TimeZoneInfo Zone { get { try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); } catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; } } }
    private CultureInfo Culture { get { try { return CultureInfo.GetCultureInfo(Locale); } catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; } } }
    private static DateTimeOffset Instant(DateTime value) => new(value.ToUniversalTime());

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var timeline = (Timeline)d;
        timeline.UpdateTimeLabels();
        timeline.Sync();
    }

    // Updates from the last twelve hours read as relative time; anything older reads as a clock time under its day.
    private void UpdateTimeLabels()
    {
        if (Events is null) return;
        var zone = Zone;
        var culture = Culture;
        var now = Instant(Now);
        foreach (var item in Events)
        {
            var instant = Instant(item.At);
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            bool recent = local.Date == TimeZoneInfo.ConvertTime(now, zone).Date && now - instant < TimeSpan.FromHours(12);
            int minutes = Math.Max(0, (int)Math.Floor((now - instant).TotalMinutes));
            string display = !recent ? local.ToString("h:mm tt", culture) : minutes < 1 ? "Now" : minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h";
            item.SetDisplayTime(display, local.ToString("f", culture));
        }
    }

    // Reuses rows by id, so a new update only inserts itself and everything already on screen keeps its state.
    private void Sync()
    {
        if (_panel is null || Events is null) return;
        var zone = Zone;
        var now = TimeZoneInfo.ConvertTime(Instant(Now), zone);
        var groups = Events.OrderByDescending(item => Instant(item.At)).GroupBy(item => TimeZoneInfo.ConvertTime(Instant(item.At), zone).Date).ToArray();
        var keptRows = new HashSet<string>(StringComparer.Ordinal);
        var keptDays = new HashSet<string>(StringComparer.Ordinal);
        int dayIndex = 0;
        bool anyFresh = false;
        foreach (var group in groups)
        {
            string key = group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            keptDays.Add(key);
            bool newDay = !_days.TryGetValue(key, out var day);
            if (newDay) { day = new TimelineDay(this); _days[key] = day; _panel.Children.Insert(Math.Min(dayIndex, _panel.Children.Count), day); }
            string label = group.Key == now.Date ? "Today" : group.Key == now.Date.AddDays(-1) ? "Yesterday" : group.Key.ToString("dddd, MMMM d", Culture);
            var rows = group.ToArray();
            day!.SetHeading(label, rows.Length, IsLoaded);
            for (int i = 0; i < rows.Length; i++)
            {
                var item = rows[i];
                keptRows.Add(item.Id);
                if (!_rows.TryGetValue(item.Id, out var row))
                {
                    row = new TimelineRow(this, item);
                    _rows[item.Id] = row;
                    day.Rows.Children.Insert(Math.Min(i, day.Rows.Children.Count), row);
                    if (item.IsNew && IsLoaded) { row.PlayFresh(ArcMotion.GetLevel(this)); anyFresh = true; }
                }
                else if (!ReferenceEquals(row.Parent, day.Rows))
                {
                    (row.Parent as Panel)?.Children.Remove(row);
                    day.Rows.Children.Insert(Math.Min(i, day.Rows.Children.Count), row);
                }
                row.SetLast(i == rows.Length - 1);
            }
            dayIndex++;
        }
        foreach (var id in _rows.Keys.Where(id => !keptRows.Contains(id)).ToArray()) { var row = _rows[id]; (row.Parent as Panel)?.Children.Remove(row); row.Detach(); _rows.Remove(id); }
        foreach (var key in _days.Keys.Where(key => !keptDays.Contains(key)).ToArray()) { _panel.Children.Remove(_days[key]); _days.Remove(key); }
        if (!anyFresh) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RevealInView);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        PinHeadings();
        UpdateFeather();
        RevealInView();
    }

    // CSS sticky: each day label holds the top edge until its own section scrolls it away.
    private void PinHeadings()
    {
        if (_panel is null || _scrollViewer is null) return;
        foreach (var day in _panel.Children.OfType<TimelineDay>())
        {
            double top = day.TranslatePoint(new Point(), _panel).Y;
            day.Pin(Math.Clamp(_scrollViewer.VerticalOffset - top, 0, Math.Max(0, day.ActualHeight - day.HeadingHeight)));
        }
    }

    // While more updates wait below, the last visible row fades into the edge instead of being cut.
    private void UpdateFeather()
    {
        if (_scrollViewer is null) return;
        bool more = _scrollViewer.ScrollableHeight - _scrollViewer.VerticalOffset > 2;
        double height = _scrollViewer.ActualHeight;
        _scrollViewer.OpacityMask = !more || height <= Feather ? null : new LinearGradientBrush(new GradientStopCollection { new(Colors.Black, 0), new(Colors.Transparent, 1) }, new Point(0, height - Feather), new Point(0, height)) { MappingMode = BrushMappingMode.Absolute };
    }

    // Rows reveal against the viewport and are scheduled so the line reads as drawing downward.
    private void RevealInView()
    {
        if (!IsLoaded || _scrollViewer is null || _panel is null) return;
        var level = ArcMotion.GetLevel(this);
        double height = _scrollViewer.ActualHeight, clock = Environment.TickCount64 / 1000d;
        foreach (var row in _panel.Children.OfType<TimelineDay>().SelectMany(day => day.Rows.Children.OfType<TimelineRow>()))
        {
            if (row.Revealed) continue;
            double top = row.TranslatePoint(new Point(), _scrollViewer).Y;
            if (top >= height || top + row.ActualHeight <= 0) continue;
            double delay = Math.Max(0, _revealAt - clock);
            _revealAt = clock + delay + Step;
            row.Reveal(level, delay);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || _panel is null) return;
        var triggers = _panel.Children.OfType<TimelineDay>().SelectMany(day => day.Rows.Children.OfType<TimelineRow>()).Select(row => row.Trigger).Where(trigger => trigger.Focusable).ToArray();
        if (triggers.Length == 0) return;
        int current = Array.FindIndex(triggers, trigger => trigger.IsKeyboardFocused);
        int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? triggers.Length - 1 : Math.Clamp(current + (e.Key == Key.Down ? 1 : -1), 0, triggers.Length - 1);
        triggers[next].Focus();
        triggers[next].BringIntoView();
        e.Handled = true;
    }
}

/// <summary>A day's sticky label, its rolling count and the rows under it.</summary>
internal sealed class TimelineDay : Grid
{
    private readonly Border _heading = new() { Padding = new Thickness(0, 8, 12, 8), RenderTransform = new TranslateTransform() };
    private readonly TextBlock _label = new() { FontSize = 14, FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
    private readonly TimelineRise _count = new(12, 16.8);
    private readonly TextBlock _noun = new() { FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
    private int _value = -1;
    public StackPanel Rows { get; } = new() { Margin = new Thickness(0, 0, 0, 12) };
    public double HeadingHeight => _heading.ActualHeight;

    public TimelineDay(Timeline owner)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _heading.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { Source = owner });
        _noun.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _count.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        var count = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1) };
        count.Children.Add(_count);
        count.Children.Add(_noun);
        var row = new Grid { Margin = new Thickness(0, -2, 0, 2) };
        row.Children.Add(_label);
        row.Children.Add(count);
        _heading.Child = row;
        Panel.SetZIndex(_heading, 2);
        SetRow(Rows, 1);
        Children.Add(Rows);
        Children.Add(_heading);
    }

    public void SetHeading(string label, int count, bool animate)
    {
        _label.Text = label;
        _noun.Text = count == 1 ? " update" : " updates";
        int direction = count >= _value ? 1 : -1;
        if (count != _value) _count.Set(count.ToString(CultureInfo.InvariantCulture), direction, animate && _value >= 0, ArcMotion.GetLevel(this));
        _value = count;
    }

    public void Pin(double y) => ((TranslateTransform)_heading.RenderTransform).Y = y;
}

/// <summary>A label that rises to its new value, in the direction the value moved.</summary>
internal sealed class TimelineRise : Grid
{
    private readonly double _font, _line;
    private TextBlock? _face;

    public TimelineRise(double font, double line)
    {
        _font = font;
        _line = line;
        ClipToBounds = true;
    }

    public void Set(string text, int direction, bool animate, ArcMotionLevel level)
    {
        if (_face?.Text == text) return;
        var next = new TextBlock { Text = text, FontSize = _font, LineHeight = _line, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, HorizontalAlignment = HorizontalAlignment.Right };
        var old = _face;
        _face = next;
        Children.Add(next);
        if (old is null) return;
        if (!animate || level != ArcMotionLevel.Full) { Children.Remove(old); return; }
        var enter = new TranslateTransform(0, _line * .7 * direction);
        next.RenderTransform = enter;
        next.Opacity = 0;
        Tween.Spring(enter, TranslateTransform.YProperty, 0, Tween.Snappy);
        Tween.To(next, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        var leave = new TranslateTransform();
        old.RenderTransform = leave;
        Tween.To(leave, TranslateTransform.YProperty, _line * -.7 * direction, Tween.Fast, ArcEasing.Standard);
        Tween.To(old, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, 0, () => Children.Remove(old));
    }
}

internal sealed class TimelineRow : Grid
{
    private const double MarkerSpring = .36, LineSpring = .34, DetailSpring = .4, FoldSpring = .28;
    private readonly Timeline _owner;
    private readonly TimelineEvent _item;
    private readonly Grid _marker = new() { Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0), RenderTransformOrigin = new Point(.5, .5) };
    private readonly ScaleTransform _markerScale = new(1, 1);
    private readonly Ellipse _node = new();
    private readonly ContentPresenter _icon = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle _segment = new() { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(13.5, 40, 0, -4), RenderTransform = new ScaleTransform(1, 1) };
    private readonly StackPanel _content = new() { Margin = new Thickness(36, 0, 0, 0), RenderTransform = new TranslateTransform() };
    private readonly TextBlock _title = new() { FontSize = 14, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _meta = new() { FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TimelineRise _time = new(12, 20) { MinWidth = 44 };
    private readonly Viewbox _chevron = Lucide.Glyph(Lucide.ChevronDown, 16);
    private readonly RotateTransform _chevronTurn = new();
    private readonly Border _detail = new() { ClipToBounds = true, Height = 0, Visibility = Visibility.Collapsed };
    private readonly ContentPresenter _detailContent = new() { Margin = new Thickness(12, 0, 12, 16), RenderTransform = new TranslateTransform() };
    private readonly SpringDriver _driver;
    private readonly ControlSpring _height;
    private readonly SpringDriver _rowDriver;
    private readonly ControlSpring _rowHeight;
    private bool _open;
    public Button Trigger { get; } = new();
    public bool Revealed { get; private set; }

    public TimelineRow(Timeline owner, TimelineEvent item)
    {
        _owner = owner;
        _item = item;
        _driver = new SpringDriver(() => _detail.Height = Math.Max(0, _height!.Position)) { Settled = SettleDetail };
        _height = _driver.Add(0, DetailSpring);
        _rowDriver = new SpringDriver(() => Height = Math.Max(0, _rowHeight!.Position)) { Settled = () => { Height = double.NaN; ClipToBounds = false; } };
        _rowHeight = _rowDriver.Add(0, Tween.Smooth);

        _marker.RenderTransform = _markerScale;
        _marker.Children.Add(_node);
        _marker.Children.Add(_icon);
        var ring = new Ellipse { StrokeThickness = 1 };
        ring.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.TimelineRing");
        _marker.Children.Add(ring);
        Panel.SetZIndex(_marker, 1);
        _segment.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.BorderStrong");

        _title.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        _meta.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _time.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _chevron.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _chevron.RenderTransformOrigin = new Point(.5, .5);
        _chevron.RenderTransform = _chevronTurn;
        _chevron.VerticalAlignment = VerticalAlignment.Top;
        _chevron.Margin = new Thickness(0, 2, 0, 0);
        var text = new StackPanel();
        text.Children.Add(_title);
        text.Children.Add(_meta);
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        _time.Margin = new Thickness(12, 0, 12, 0);
        _time.VerticalAlignment = VerticalAlignment.Top;
        SetColumn(_time, 1);
        SetColumn(_chevron, 2);
        layout.Children.Add(text);
        layout.Children.Add(_time);
        layout.Children.Add(_chevron);
        Trigger.Content = layout;
        Trigger.SetResourceReference(StyleProperty, "ArcWpf.Timeline.Trigger");
        Trigger.Click += (_, _) => { if (HasDetail) _item.IsExpanded = !_item.IsExpanded; };

        _detail.Child = _detailContent;
        _content.Children.Add(Trigger);
        _content.Children.Add(_detail);
        Children.Add(_segment);
        Children.Add(_content);
        Children.Add(_marker);
        Refresh();
        SetOpen(_item.IsExpanded, false);
        _item.PropertyChanged += OnItemChanged;
    }

    private bool HasDetail => _item.Detail is not null and not "";

    public void Detach() => _item.PropertyChanged -= OnItemChanged;

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimelineEvent.IsExpanded)) SetOpen(_item.IsExpanded, IsLoaded);
        else if (e.PropertyName != nameof(TimelineEvent.IsNew)) Refresh();
    }

    private void Refresh()
    {
        _title.Inlines.Clear();
        if (!string.IsNullOrEmpty(_item.Actor))
        {
            var actor = new Run(_item.Actor) { FontWeight = FontWeights.Medium };
            actor.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
            _title.Inlines.Add(actor);
            _title.Inlines.Add(" ");
        }
        _title.Inlines.Add(_item.Title);
        _meta.Text = _item.Meta;
        _meta.Visibility = string.IsNullOrEmpty(_item.Meta) ? Visibility.Collapsed : Visibility.Visible;
        _time.Set(_item.DisplayTime, 1, IsLoaded, ArcMotion.GetLevel(_owner));
        _time.ToolTip = _item.FullTime;
        _chevron.Visibility = HasDetail ? Visibility.Visible : Visibility.Hidden;
        Trigger.Focusable = HasDetail;
        Trigger.IsHitTestVisible = HasDetail;
        AutomationProperties.SetName(Trigger, $"{_item.Actor} {_item.Title}, {_item.FullTime}".Trim());
        if (_item.Avatar is { } avatar)
        {
            _node.Fill = new ImageBrush(avatar) { Stretch = Stretch.UniformToFill };
            _icon.Content = null;
        }
        else
        {
            _node.SetResourceReference(Shape.FillProperty, _item.Tone switch { "success" => "ArcWpf.Brush.TimelineSuccess", "danger" => "ArcWpf.Brush.TimelineDanger", _ => "ArcWpf.Brush.Surface" });
            _icon.SetResourceReference(TextElement.ForegroundProperty, _item.Tone switch { "success" => "ArcWpf.Brush.LiveSuccess", "danger" => "ArcWpf.Brush.Danger", _ => "ArcWpf.Brush.TextSecondary" });
            _icon.Content = _item.Icon is string data ? Lucide.Glyph(data, 14, 2) : _item.Icon;
        }
        if (_item.Detail is string detail)
        {
            var copy = new TextBlock { Text = detail, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap };
            copy.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            _detailContent.Content = copy;
        }
        else _detailContent.Content = _item.Detail;
    }

    public void SetLast(bool last) => _segment.Visibility = last ? Visibility.Collapsed : Visibility.Visible;

    private void SetOpen(bool open, bool animate)
    {
        if (open == _open && !(open && _detail.Visibility != Visibility.Visible)) return;
        _open = open;
        var level = ArcMotion.GetLevel(_owner);
        bool full = animate && level == ArcMotionLevel.Full;
        if (full) Tween.Spring(_chevronTurn, RotateTransform.AngleProperty, open ? 180 : 0, Tween.Snappy);
        else { _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, null); _chevronTurn.Angle = open ? 180 : 0; }
        if (!full)
        {
            _driver.Stop();
            _detail.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _detail.Height = double.NaN;
            _detailContent.BeginAnimation(OpacityProperty, null);
            _detailContent.Opacity = 1;
            return;
        }
        _detail.Visibility = Visibility.Visible;
        double width = _detail.ActualWidth > 0 ? _detail.ActualWidth : Trigger.ActualWidth;
        _detailContent.Measure(new Size(width, double.PositiveInfinity));
        double from = _driver.IsRunning ? _height.Position : open ? 0 : _detail.ActualHeight;
        double to = open ? _detailContent.DesiredSize.Height : 0;
        _driver.Snap(_height, from);
        _detail.Height = from;
        _driver.Animate(_height, to, open ? DetailSpring : FoldSpring);
        var move = (TranslateTransform)_detailContent.RenderTransform;
        if (open)
        {
            _detailContent.Opacity = 0;
            move.Y = -4;
            Tween.To(_detailContent, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, .06);
            Tween.To(move, TranslateTransform.YProperty, 0, Tween.Standard, ArcEasing.Enter, .06);
            var blur = new BlurEffect { Radius = 6 };
            _detailContent.Effect = blur;
            var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)) { BeginTime = TimeSpan.FromSeconds(.06), EasingFunction = ArcEasing.Enter };
            clear.Completed += (_, _) => { if (ReferenceEquals(_detailContent.Effect, blur)) _detailContent.Effect = null; };
            blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
        }
        else
        {
            _detailContent.Effect = null;
            Tween.To(_detailContent, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard);
        }
    }

    private void SettleDetail()
    {
        if (_open) { _detail.Height = double.NaN; return; }
        _detail.Visibility = Visibility.Collapsed;
        _detail.Height = 0;
    }

    public void Reveal(ArcMotionLevel level, double delay)
    {
        Revealed = true;
        if (level != ArcMotionLevel.Full) return;
        _marker.Opacity = 0;
        _markerScale.ScaleX = _markerScale.ScaleY = .4;
        Tween.To(_marker, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, delay);
        Tween.Spring(_markerScale, ScaleTransform.ScaleXProperty, 1, MarkerSpring, delay);
        Tween.Spring(_markerScale, ScaleTransform.ScaleYProperty, 1, MarkerSpring, delay);
        var line = (ScaleTransform)_segment.RenderTransform;
        line.ScaleY = 0;
        Tween.Spring(line, ScaleTransform.ScaleYProperty, 1, LineSpring, delay + .1);
    }

    // A new update opens from nothing at the top: the row grows, its content settles, then its marker lands and the line draws.
    public void PlayFresh(ArcMotionLevel level)
    {
        Revealed = true;
        if (level == ArcMotionLevel.Off) return;
        if (level == ArcMotionLevel.Reduced) { Opacity = 0; Tween.To(this, OpacityProperty, 1, .15, ArcEasing.Standard); return; }
        ClipToBounds = true;
        Height = 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            double width = ActualWidth > 0 ? ActualWidth : 400;
            _content.Measure(new Size(Math.Max(0, width - 36), double.PositiveInfinity));
            _rowDriver.Snap(_rowHeight, 0);
            _rowDriver.Animate(_rowHeight, _content.DesiredSize.Height, Tween.Smooth);
        });
        var move = (TranslateTransform)_content.RenderTransform;
        move.Y = -10;
        Tween.Spring(move, TranslateTransform.YProperty, 0, Tween.Smooth);
        _content.Opacity = 0;
        Tween.To(_content, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter, .05);
        var blur = new BlurEffect { Radius = 6 };
        _content.Effect = blur;
        var clear = new DoubleAnimation(0, TimeSpan.FromSeconds(Tween.Standard)) { BeginTime = TimeSpan.FromSeconds(.05), EasingFunction = ArcEasing.Enter };
        clear.Completed += (_, _) => _content.Effect = null;
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
        Revealed = false;
        Reveal(level, .12);
    }
}
