using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>
/// A chat or support thread: grouped messages with day separators, reactions, read receipts that glide to the latest read message,
/// a typing indicator, and a composer. The list stays pinned to the newest message and offers a pill when new messages land out of view.
/// </summary>
public class ChatThread : Control
{
    private const double StickDistance = 48, FarDistance = 180, Settle = .44, Grow = Tween.Smooth, Pop = .3, ArriveSeconds = .46, Gutter = 10;
    private static readonly string[] DefaultReactions = ["👍", "❤️", "😂", "🎉", "👀", "🙏"];

    public static readonly DependencyProperty ParticipantsProperty = Prop<IReadOnlyList<ChatParticipant>>(nameof(Participants), []);
    public static readonly DependencyProperty CurrentUserIdProperty = Prop(nameof(CurrentUserId), "");
    public static readonly DependencyProperty MessagesProperty = Prop<IReadOnlyList<ChatMessage>?>(nameof(Messages), null);
    public static readonly DependencyProperty DefaultMessagesProperty = DependencyProperty.Register(nameof(DefaultMessages), typeof(IReadOnlyList<ChatMessage>), typeof(ChatThread), new PropertyMetadata(null, (d, e) => ((ChatThread)d).OnDefaultMessages((IReadOnlyList<ChatMessage>?)e.NewValue)));
    public static readonly DependencyProperty TypingProperty = Prop<IReadOnlyList<string>>(nameof(Typing), []);
    public static readonly DependencyProperty ReadByProperty = Prop<IReadOnlyDictionary<string, string>?>(nameof(ReadBy), null);
    public static readonly DependencyProperty ReactionsProperty = Prop<IReadOnlyList<string>>(nameof(Reactions), DefaultReactions);
    public static readonly DependencyProperty PlaceholderProperty = Prop(nameof(Placeholder), "Message");
    public static readonly DependencyProperty ShowComposerProperty = Prop(nameof(ShowComposer), true);
    public static readonly DependencyProperty AllowAttachmentsProperty = Prop(nameof(AllowAttachments), true);
    public static readonly DependencyProperty AcceptProperty = Prop<string?>(nameof(Accept), null);
    public static readonly DependencyProperty GroupWindowProperty = Prop(nameof(GroupWindow), TimeSpan.FromMinutes(5));
    public static readonly DependencyProperty CultureProperty = Prop(nameof(Culture), CultureInfo.GetCultureInfo("en-US"));
    public static readonly DependencyProperty LabelProperty = Prop(nameof(Label), "Conversation");
    public static readonly DependencyProperty LabelsProperty = Prop(nameof(Labels), new ChatThreadLabels());
    private static DependencyProperty Prop<T>(string name, T value) => DependencyProperty.Register(name, typeof(T), typeof(ChatThread), new PropertyMetadata(value, (d, _) => ((ChatThread)d).Sync()));

    private readonly SpringDriver _pinDriver;
    private readonly ControlSpring _pin;
    private readonly Dictionary<string, MessageView> _views = [];
    private readonly Dictionary<string, FrameworkElement> _days = [];
    private readonly HashSet<string> _known = [];
    private readonly Dictionary<string, FrameworkElement> _readers = [];
    private readonly OwnedMotion _motion = new();
    private List<ChatMessage> _inner = [];
    private Decorator? _root;
    private ScrollViewer? _scroller;
    private Border? _content;
    private TranslateTransform? _contentMove;
    private StackPanel? _list;
    private TextBlock? _status;
    private ChatComposer? _composer;
    private FrameworkElement? _typingRow;
    private string _typingKey = string.Empty;
    private ChatPill? _pill;
    private bool _stick = true, _atBottom = true, _far, _built;
    private double _lastHeight;
    private int _seen = -1, _counter;
    private string? _pickerFor;
    private ChatReactionPicker? _picker;

    static ChatThread() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ChatThread), new FrameworkPropertyMetadata(typeof(ChatThread)));

    public ChatThread()
    {
        _pinDriver = new SpringDriver(() => { if (_contentMove is not null) { _contentMove.Y = _pin!.Position; } });
        _pin = _pinDriver.Add(0, Settle);
        Unloaded += (_, _) => { _pinDriver.Finish(); _motion.Stop(); ClosePicker(false); };
    }

    public IReadOnlyList<ChatParticipant> Participants { get => (IReadOnlyList<ChatParticipant>)GetValue(ParticipantsProperty); set => SetValue(ParticipantsProperty, value); }
    public string CurrentUserId { get => (string)GetValue(CurrentUserIdProperty); set => SetValue(CurrentUserIdProperty, value); }
    /// <summary>Controlled messages, oldest first. Leave it out to let the thread keep its own list.</summary>
    public IReadOnlyList<ChatMessage>? Messages { get => (IReadOnlyList<ChatMessage>?)GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }
    public IReadOnlyList<ChatMessage>? DefaultMessages { get => (IReadOnlyList<ChatMessage>?)GetValue(DefaultMessagesProperty); set => SetValue(DefaultMessagesProperty, value); }
    /// <summary>Participant ids currently typing.</summary>
    public IReadOnlyList<string> Typing { get => (IReadOnlyList<string>)GetValue(TypingProperty); set => SetValue(TypingProperty, value); }
    /// <summary>The last message each participant has read, by participant id. Their avatar sits under that message and glides as it moves.</summary>
    public IReadOnlyDictionary<string, string>? ReadBy { get => (IReadOnlyDictionary<string, string>?)GetValue(ReadByProperty); set => SetValue(ReadByProperty, value); }
    /// <summary>Emoji offered by the reaction picker.</summary>
    public IReadOnlyList<string> Reactions { get => (IReadOnlyList<string>)GetValue(ReactionsProperty); set => SetValue(ReactionsProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool ShowComposer { get => (bool)GetValue(ShowComposerProperty); set => SetValue(ShowComposerProperty, value); }
    public bool AllowAttachments { get => (bool)GetValue(AllowAttachmentsProperty); set => SetValue(AllowAttachmentsProperty, value); }
    public string? Accept { get => (string?)GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }
    /// <summary>Messages from one person closer together than this share a group.</summary>
    public TimeSpan GroupWindow { get => (TimeSpan)GetValue(GroupWindowProperty); set => SetValue(GroupWindowProperty, value); }
    public CultureInfo Culture { get => (CultureInfo)GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>Accessible name of the message log.</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ChatThreadLabels Labels { get => (ChatThreadLabels)GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    public event EventHandler<ChatDraftEventArgs>? Sent;
    public event EventHandler<ChatReactEventArgs>? Reacted;
    public event EventHandler<ChatMessageEventArgs>? RetryRequested;
    public event EventHandler<ChatAttachmentEventArgs>? AttachmentInvoked;

    private IReadOnlyList<ChatMessage> Current => Messages ?? _inner;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Moving => IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;

    internal ScrollViewer? ScrollerForTest => _scroller;
    internal StackPanel? ListForTest => _list;
    internal ChatComposer? ComposerForTest => _composer;
    internal bool IsPinnedForTest => _stick;
    internal bool PillVisibleForTest => _pill is { Showing: true };
    internal string PillTextForTest => _pill?.Text ?? string.Empty;
    internal bool TypingVisibleForTest => _typingRow is not null;
    internal string StatusAnnouncementForTest => _status?.Text ?? string.Empty;
    internal IReadOnlyList<ChatMessage> CurrentForTest => Current;
    internal string? PickerForTest => _pickerFor;
    internal Border? BubbleForTest(string id) => _views.TryGetValue(id, out var view) ? view.Bubble : null;
    internal string StatusForTest(string id) => _views.TryGetValue(id, out var view) ? view.StatusText : string.Empty;
    internal int ReadersForTest(string id) => _views.TryGetValue(id, out var view) ? view.ReaderCount : 0;
    internal ArcChromeButton? ReactButtonForTest(string id) => _views.TryGetValue(id, out var view) ? view.ReactButton : null;
    internal void ReactForTest(string id, string emoji) => React(id, emoji);
    internal void TogglePickerForTest(string id) => TogglePicker(id);
    internal void FinishMotionForTest() { _pinDriver.Finish(); _pill?.FinishForTest(); }

    public void ScrollToBottom(bool smooth = true)
    {
        if (_scroller is null) { return; }
        _stick = true;
        if (!smooth || !Moving || _scroller.ScrollableHeight <= 0) { _scroller.ScrollToEnd(); return; }
        // Smooth scrolling keeps its own easing, as a browser's scrollTo does.
        var ride = new ScrollRide(_scroller);
        ride.To(_scroller.ScrollableHeight);
    }

    public void FocusComposer() => _composer?.Focus();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _contentMove = new TranslateTransform();
        _list = new StackPanel();
        AutomationProperties.SetLiveSetting(_list, AutomationLiveSetting.Polite);
        _content = new Border { Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 0, Gutter, 0), RenderTransform = _contentMove, Child = _list };
        _content.SizeChanged += OnContentResized;
        // The content springs up from where it was when the thread grows; the clip keeps that travel out of the scroll range.
        var track = new Grid { ClipToBounds = true, Children = { _content } };
        _scroller = new OverlayScrollViewer { Focusable = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = track };
        _scroller.ScrollChanged += OnScroll;
        _status = new TextBlock { Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _pill = new ChatPill(this) { Margin = new Thickness(0, 0, 0, 12) };
        _pill.Click += (_, _) => ScrollToBottom();
        var viewport = new Grid { Children = { _scroller, _status, _pill } };
        _composer = new ChatComposer();
        _composer.Sent += (_, e) => Send(e.Draft);
        DockPanel.SetDock(_composer, Dock.Bottom);
        _root.Child = new DockPanel { Children = { _composer, viewport } };
        _built = true;
        Sync();
        Dispatcher.BeginInvoke(() => _scroller?.ScrollToEnd(), DispatcherPriority.Loaded);
    }

    private void OnDefaultMessages(IReadOnlyList<ChatMessage>? value)
    {
        if (_inner.Count == 0 && value is not null) { _inner = value.ToList(); Sync(); }
    }

    private void Sync()
    {
        if (!_built || _list is null || _composer is null) { return; }
        var labels = Labels;
        _composer.Visibility = ShowComposer ? Visibility.Visible : Visibility.Collapsed;
        _composer.Placeholder = Placeholder;
        _composer.AllowAttachments = AllowAttachments;
        _composer.Accept = Accept;
        _composer.Labels = labels;
        AutomationProperties.SetName(_list, Label);
        var messages = Current;
        if (_seen < 0) { _seen = messages.Count; }
        var people = Participants.GroupBy(person => person.Id).ToDictionary(group => group.Key, group => group.First());
        // Your own message always brings the thread back to the bottom.
        var fresh = messages.Where(message => !_known.Contains(message.Id)).ToList();
        bool firstBuild = _known.Count == 0;
        foreach (var message in fresh) { _known.Add(message.Id); }
        if (fresh.Any(message => message.AuthorId == CurrentUserId)) { _stick = true; }

        var rows = ChatRules.Rows(messages, people, CurrentUserId, GroupWindow, Culture, labels, DateTime.Now);
        var receipts = ChatRules.Receipts(messages, ReadBy, people, CurrentUserId);
        int readIndex = -1;
        foreach (string id in receipts.Keys) { readIndex = Math.Max(readIndex, messages.ToList().FindIndex(message => message.Id == id)); }
        int lastMine = -1;
        for (int i = messages.Count - 1; i >= 0; i--) { if (messages[i].AuthorId == CurrentUserId) { lastMine = i; break; } }

        var readerBefore = _readers.ToDictionary(pair => pair.Key, pair => _list.IsLoaded && pair.Value.IsVisible ? pair.Value.TranslatePoint(new Point(), _list) : (Point?)null);
        _readers.Clear();
        var keep = new HashSet<string>();
        var order = new List<FrameworkElement>();
        bool firstRow = true;
        ChatRules.Row? previous = null;
        foreach (var row in rows)
        {
            keep.Add(row.Key);
            if (row is ChatRules.DayRow day)
            {
                if (!_days.TryGetValue(day.Key, out var element)) { _days[day.Key] = element = BuildDay(day.Label); }
                else { ((TextBlock)((Grid)element).Children[2]).Text = day.Label; }
                element.Margin = new Thickness(4, firstRow ? 4 : 20, 4, 4);
                order.Add(element);
            }
            else if (row is ChatRules.MessageRow message)
            {
                bool isNew = !_views.TryGetValue(message.Key, out var view);
                if (isNew) { _views[message.Key] = view = new MessageView(this, message.Message.Id); }
                bool afterDay = previous is ChatRules.DayRow;
                receipts.TryGetValue(message.Key, out var seenBy);
                var status = message.Message.Status == ChatMessageStatus.Failed ? ChatMessageStatus.Failed : message.Message.Status == ChatMessageStatus.Sending ? ChatMessageStatus.Sending : readIndex >= message.Index ? ChatMessageStatus.Read : message.Message.Status ?? ChatMessageStatus.Sent;
                view!.Update(message, afterDay, message.Index == lastMine && message.Mine ? status : null, seenBy ?? [], isNew);
                foreach (var (id, element) in view.Readers) { _readers[id] = element; }
                order.Add(view!.Row);
                if (isNew && !firstBuild && _list.IsLoaded) { view.Arrive(message.Mine); }
            }
            previous = row;
            firstRow = false;
        }
        foreach (string key in _views.Keys.Where(key => !keep.Contains(key)).ToList())
        {
            var gone = _views[key].Row;
            _views.Remove(key);
            if (_pickerFor == key) { ClosePicker(false); }
            Leave(gone);
        }
        foreach (string key in _days.Keys.Where(key => !keep.Contains(key)).ToList()) { _list.Children.Remove(_days[key]); _days.Remove(key); }
        SyncTyping(people, labels, order);
        // Keep leaving rows where they are; put everything else in row order.
        int at = 0;
        foreach (var element in order)
        {
            int current = _list.Children.IndexOf(element);
            if (current < 0) { _list.Children.Insert(at, element); }
            else if (current != at) { _list.Children.RemoveAt(current); _list.Children.Insert(at, element); }
            at++;
        }
        GlideReaders(readerBefore);
        UpdateStackWidths();
        UpdatePill();
    }

    private void Leave(FrameworkElement row)
    {
        if (!Moving) { _list?.Children.Remove(row); return; }
        row.IsHitTestVisible = false;
        Tween.To(row, OpacityProperty, 0, Tween.Exit, null, done: () => _list?.Children.Remove(row));
    }

    // Each reader's avatar glides from its old message to the new one.
    private void GlideReaders(Dictionary<string, Point?> before)
    {
        if (!Full || _list is null || before.Count == 0) { return; }
        Dispatcher.BeginInvoke(() =>
        {
            if (_list is null) { return; }
            foreach (var (id, element) in _readers)
            {
                if (!before.TryGetValue(id, out var was) || was is null || !element.IsVisible) { continue; }
                var now = element.TranslatePoint(new Point(), _list);
                double x = was.Value.X - now.X, y = was.Value.Y - now.Y;
                if (Math.Abs(x) < .5 && Math.Abs(y) < .5) { continue; }
                var move = new TranslateTransform(x, y);
                element.RenderTransform = move;
                Tween.Spring(move, TranslateTransform.XProperty, 0, Settle);
                Tween.Spring(move, TranslateTransform.YProperty, 0, Settle);
            }
        }, DispatcherPriority.Render);
    }

    private FrameworkElement BuildDay(string label)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Rectangle Line(int column)
        {
            var line = new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Center };
            line.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.BorderSubtle");
            Grid.SetColumn(line, column);
            return line;
        }
        grid.Children.Add(Line(0));
        grid.Children.Add(Line(2));
        var text = ArcText.Block(label, 12, 16.8, "TextMuted", true);
        text.Margin = new Thickness(12, 0, 12, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private void SyncTyping(Dictionary<string, ChatParticipant> people, ChatThreadLabels labels, List<FrameworkElement> order)
    {
        var typers = Typing.Select(id => people.TryGetValue(id, out var who) ? who : null).Where(who => who is not null && who.Id != CurrentUserId).Select(who => who!).ToList();
        string announcement = typers.Count > 0 ? labels.Typing(typers.Select(who => who.Name).ToList()) : string.Empty;
        if (_status is not null && _status.Text != announcement) { _status.Text = announcement; }
        string key = typers.Count > 0 ? typers[0].Id : string.Empty;
        if (key != _typingKey)
        {
            if (_typingRow is { } old)
            {
                _typingRow = null;
                if (Moving)
                {
                    old.IsHitTestVisible = false;
                    if (old.RenderTransform is TransformGroup group && group.Children[0] is ScaleTransform scale)
                    {
                        Tween.To(scale, ScaleTransform.ScaleXProperty, .9, Tween.Exit, ArcEasing.Standard);
                        Tween.To(scale, ScaleTransform.ScaleYProperty, .9, Tween.Exit, ArcEasing.Standard);
                    }
                    Tween.To(old, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard, done: () => _list?.Children.Remove(old));
                }
                else { _list?.Children.Remove(old); }
            }
            _typingKey = key;
            if (typers.Count > 0) { _typingRow = BuildTyping(typers[0]); }
        }
        if (_typingRow is not null) { order.Add(_typingRow); }
    }

    private FrameworkElement BuildTyping(ChatParticipant who)
    {
        var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        bool still = !Full;
        for (int i = 0; i < 3; i++)
        {
            var dot = new Ellipse { Width = 6, Height = 6, Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0), RenderTransform = new TranslateTransform(), Opacity = .6 };
            dot.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.TextMuted");
            dots.Children.Add(dot);
            if (!still) { Breathe(dot, i * .16); }
        }
        var bubble = new Border { Height = 36, Padding = new Thickness(14, 0, 14, 0), CornerRadius = new CornerRadius(18), Child = dots, HorizontalAlignment = HorizontalAlignment.Left };
        bubble.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ChatTheirs");
        var face = ThreadUi.Face(who.Name, who.Avatar, 28);
        var host = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Children = { face } };
        Canvas.SetLeft(face, -36); Canvas.SetTop(face, -28);
        var line = new Grid { Children = { bubble, host } };
        var scale = new ScaleTransform();
        var move = new TranslateTransform();
        var row = new Border { Margin = new Thickness(36, 14, 0, 0), Child = line, RenderTransform = new TransformGroup { Children = { scale, move } }, RenderTransformOrigin = new Point(0, 1) };
        if (Moving)
        {
            row.Opacity = 0;
            bool full = Full;
            scale.ScaleX = scale.ScaleY = full ? .9 : 1;
            move.Y = full ? 8 : 0;
            double seconds = full ? Tween.Snappy : Tween.Fast;
            Tween.Spring(row, OpacityProperty, 1, seconds);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, seconds);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, seconds);
            Tween.Spring(move, TranslateTransform.YProperty, 0, seconds);
        }
        return row;
    }

    private static void Breathe(Ellipse dot, double delay)
    {
        var ease = ArcEasing.InOut;
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(1.2), RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay) };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(.35, KeyTime.FromPercent(0)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(.3), ease));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(.35, KeyTime.FromPercent(.6), ease));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(.35, KeyTime.FromPercent(1)));
        dot.BeginAnimation(OpacityProperty, opacity);
        var lift = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(1.2), RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay) };
        lift.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromPercent(.3), ease));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(.6), ease));
        lift.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        ((TranslateTransform)dot.RenderTransform).BeginAnimation(TranslateTransform.YProperty, lift);
    }

    // Pinning: while the reader is at the bottom, every growth of the content keeps the newest message in view.
    // The scroll jumps to the end before paint and the content springs up from where it was, so nothing snaps.
    private void OnContentResized(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) { UpdateStackWidths(); }
        double height = e.NewSize.Height, delta = height - _lastHeight;
        _lastHeight = height;
        if (!_stick || _scroller is null) { return; }
        _scroller.ScrollToEnd();
        if (delta > 0 && delta < _scroller.ViewportHeight && Full && e.PreviousSize.Height > 0)
        {
            _pin.Snap(_pin.Position + delta);
            if (_contentMove is not null) { _contentMove.Y = _pin.Position; }
            _pinDriver.Animate(_pin, 0, Settle);
        }
    }

    private void OnScroll(object sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null) { return; }
        double distance = _scroller.ExtentHeight - _scroller.VerticalOffset - _scroller.ViewportHeight;
        // Only a real change of position moves the pin; growth keeps it where it was until the jump lands.
        if (e.VerticalChange != 0 || e.ViewportHeightChange != 0) { _stick = distance < StickDistance || _stick && e.VerticalChange == 0; }
        _atBottom = distance < StickDistance;
        _far = distance > FarDistance;
        if (_atBottom) { _seen = Current.Count; }
        UpdatePill();
    }

    private void UpdatePill()
    {
        if (_pill is null) { return; }
        var messages = Current;
        int unread = _atBottom ? 0 : messages.Skip(Math.Max(0, _seen)).Count(message => message.AuthorId != CurrentUserId);
        _pill.Show(!_atBottom && (unread > 0 || _far), unread, Labels);
    }

    private void UpdateStackWidths()
    {
        if (_list is null || _list.ActualWidth <= 0) { return; }
        double max = Math.Min(_list.ActualWidth * (_list.ActualWidth <= 420 - 24 ? .84 : .78), 544);
        foreach (var view in _views.Values) { view.SetMaxWidth(max); }
    }

    private void React(string id, string emoji)
    {
        ClosePicker(false);
        Reacted?.Invoke(this, new ChatReactEventArgs(id, emoji));
        if (Messages is not null) { return; }
        _inner = _inner.Select(message => message.Id == id ? ChatRules.Toggle(message, emoji) : message).ToList();
        Sync();
    }

    private void Send(ChatDraft draft)
    {
        _stick = true;
        Sent?.Invoke(this, new ChatDraftEventArgs(draft));
        if (Messages is not null) { return; }
        var attachments = draft.Files.Select((path, index) =>
        {
            bool image = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
            long? size = null;
            try { size = new System.IO.FileInfo(path).Length; } catch (System.IO.IOException) { } catch (UnauthorizedAccessException) { }
            return new ChatAttachment { Id = $"local-file-{++_counter}-{index}", Name = System.IO.Path.GetFileName(path), Path = path, Size = size, Kind = image ? ChatAttachmentKind.Image : ChatAttachmentKind.File, Image = image ? LoadLocal(path) : null };
        }).ToList();
        _inner = [.. _inner, new ChatMessage { Id = $"local-{DateTime.Now.Ticks}-{++_counter}", AuthorId = CurrentUserId, Text = draft.Text.Length > 0 ? draft.Text : null, Attachments = attachments, CreatedAt = DateTime.Now, Status = ChatMessageStatus.Sent }];
        Sync();
    }

    private static ImageSource? LoadLocal(string path)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 480;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) when (error is System.IO.IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    private void TogglePicker(string id)
    {
        if (_pickerFor == id) { ClosePicker(true); return; }
        ClosePicker(false);
        if (!_views.TryGetValue(id, out var view) || _scroller is null) { return; }
        _pickerFor = id;
        // The picker opens above the bubble unless that would run past the top of the thread.
        bool below = view.Line.TranslatePoint(new Point(), _scroller).Y < 56;
        _picker = view.OpenPicker(below, Reactions, Labels, Full, Moving);
        Window.GetWindow(this)?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPressOutsidePicker), true);
    }

    private void OnPressOutsidePicker(object sender, MouseButtonEventArgs e)
    {
        if (_pickerFor is null || !_views.TryGetValue(_pickerFor, out var view) || Outside.IsWithin(e.OriginalSource, view.PickerWrap)) { return; }
        ClosePicker(false);
    }

    private void ClosePicker(bool focusTrigger)
    {
        if (_pickerFor is null) { return; }
        Window.GetWindow(this)?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPressOutsidePicker));
        if (_views.TryGetValue(_pickerFor, out var view)) { view.ClosePicker(Full, Moving); if (focusTrigger) { view.ReactButton.Focus(); } }
        _pickerFor = null;
        _picker = null;
    }

    // One message row. It keeps its visuals across updates, so its reactions and count can animate.
    private sealed class MessageView
    {
        private readonly ChatThread _owner;
        private readonly string _id;
        private readonly Dictionary<string, FrameworkElement> _reactionItems = [];
        private readonly Dictionary<string, ThreadCount> _reactionCounts = [];
        private readonly StackPanel _stack;
        private readonly TextBlock _author, _time;
        private readonly StackPanel _meta;
        private readonly Border _gutter;
        private readonly Grid _lineGrid;
        private readonly Canvas _faceHost;
        private readonly StackPanel _parts;
        private readonly Border _bubble;
        private readonly TextBlock _bubbleText;
        private readonly SpringHeightBox _reactionClip;
        private readonly WrapPanel _reactions;
        private readonly StackPanel _receipts;
        private readonly Grid _pickerWrap;
        private readonly ArcChromeButton _reactButton;
        private readonly Canvas _pickerHost;
        private readonly Border _row;
        private Avatar? _face;
        private string _faceFor = string.Empty;
        private ChatRules.MessageRow? _data;
        private string _attachmentKey = string.Empty;
        private FrameworkElement? _attachmentHost;
        private string _status = string.Empty;
        private int _readerCount;
        private Border? _pickerSurface;
        private bool _mine, _hasReactions;

        public MessageView(ChatThread owner, string id)
        {
            _owner = owner;
            _id = id;
            _author = ArcText.Block(string.Empty, 12, 16.8, "TextSecondary", true);
            _time = ArcText.Block(string.Empty, 12, 16.8, "TextMuted");
            _meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 12, 6), Children = { _author, _time } };
            _bubbleText = new TextBlock { FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap };
            _bubble = new Border { Padding = new Thickness(13, 8, 13, 9), Child = _bubbleText, HorizontalAlignment = HorizontalAlignment.Left };
            _parts = new StackPanel { Children = { _bubble } };
            _reactButton = new ArcChromeButton(14) { Width = 28, Height = 28, Opacity = 0 };
            _reactButton.Roles = button => (button.IsMouseOver || _pickerSurface is not null ? "SurfaceMuted" : null, null, button.IsMouseOver || _pickerSurface is not null ? "Foreground" : "TextMuted");
            _reactButton.Content = ArcText.Icon("smile-plus", 16, _reactButton.Ink);
            _reactButton.Click += (_, _) => _owner.TogglePicker(_id);
            _pickerHost = new Canvas { Width = 0, Height = 0 };
            _pickerWrap = new Grid { Children = { _reactButton } };
            _faceHost = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
            _lineGrid = new Grid();
            _lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            _lineGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _lineGrid.Children.Add(_parts);
            _lineGrid.Children.Add(_pickerWrap);
            _lineGrid.Children.Add(_faceHost);
            _lineGrid.Children.Add(_pickerHost);
            Grid.SetColumnSpan(_faceHost, 3);
            Grid.SetColumnSpan(_pickerHost, 3);
            _reactions = new WrapPanel { Margin = new Thickness(8, 2, 4, 2) };
            _reactionClip = new SpringHeightBox(Grow) { Margin = new Thickness(-4, 2, -4, 2), Child = _reactions, Visibility = Visibility.Collapsed };
            _receipts = new StackPanel { Orientation = Orientation.Horizontal, MinHeight = 16, Margin = new Thickness(6, 5, 6, 0), Visibility = Visibility.Collapsed };
            _stack = new StackPanel { Children = { _meta, _lineGrid, _reactionClip, _receipts } };
            _gutter = new Border { Child = _stack };
            _row = new Border { Child = _gutter, Background = Brushes.Transparent };
            _row.MouseEnter += (_, _) => RefreshButton();
            _row.MouseLeave += (_, _) => RefreshButton();
            _row.IsKeyboardFocusWithinChanged += (_, _) => RefreshButton();
        }

        public FrameworkElement Row => _row;
        public Border Bubble => _bubble;
        public FrameworkElement Line => _lineGrid;
        public ArcChromeButton ReactButton => _reactButton;
        public FrameworkElement PickerWrap => _lineGrid;
        public string StatusText => _status;
        public int ReaderCount => _readerCount;
        public Dictionary<string, FrameworkElement> Readers { get; } = [];

        private void RefreshButton()
        {
            bool shown = _row.IsMouseOver || _row.IsKeyboardFocusWithin || _pickerSurface is not null;
            bool animate = _owner.Moving;
            if (animate) { Tween.To(_reactButton, UIElement.OpacityProperty, shown ? 1 : 0, Tween.Fast, ArcEasing.Standard); }
            else { Tween.Set(_reactButton, UIElement.OpacityProperty, shown ? 1 : 0); }
        }

        public void SetMaxWidth(double max)
        {
            _stack.MaxWidth = max;
            _parts.MaxWidth = Math.Max(0, max - 34);
        }

        public void Update(ChatRules.MessageRow data, bool afterDay, ChatMessageStatus? status, IReadOnlyList<ChatParticipant> seenBy, bool isNew)
        {
            _data = data;
            _mine = data.Mine;
            var labels = _owner.Labels;
            var culture = _owner.Culture;
            double top = afterDay ? 10 : data.First ? 14 : 2;
            _row.Margin = new Thickness(0, top, 0, 0);
            _gutter.Padding = new Thickness(data.Mine ? 0 : 36, 0, 0, 0);
            _gutter.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _meta.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _meta.Visibility = data.First ? Visibility.Visible : Visibility.Collapsed;
            _author.Text = data.Author.Name;
            _author.Visibility = data.Mine ? Visibility.Collapsed : Visibility.Visible;
            _author.Margin = new Thickness(0, 0, 6, 0);
            _time.Text = data.Message.CreatedAt.ToString("h:mm tt", culture);
            _lineGrid.Margin = new Thickness(0);
            _lineGrid.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _lineGrid.ColumnDefinitions[0].Width = GridLength.Auto;
            Grid.SetColumn(_parts, data.Mine ? 2 : 0);
            Grid.SetColumn(_pickerWrap, data.Mine ? 0 : 2);
            _pickerWrap.VerticalAlignment = VerticalAlignment.Center;
            _parts.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            UpdateFace(data);
            UpdateAttachments(data, culture);
            UpdateBubble(data, culture);
            UpdateReactions(data, labels, isNew);
            UpdateReceipts(data, labels, status, seenBy);
            AutomationProperties.SetName(_row, $"{data.Author.Name}, {_time.Text}: {data.Message.Text}");
        }

        private void UpdateFace(ChatRules.MessageRow data)
        {
            bool show = !data.Mine && data.Last;
            string key = show ? data.Author.Id : string.Empty;
            if (key == _faceFor) { return; }
            _faceFor = key;
            _faceHost.Children.Clear();
            _face = null;
            if (!show) { return; }
            _face = ThreadUi.Face(data.Author.Name, data.Author.Avatar, 28);
            Canvas.SetLeft(_face, -36);
            Canvas.SetTop(_face, -28);
            _faceHost.Children.Add(_face);
        }

        private void UpdateBubble(ChatRules.MessageRow data, CultureInfo culture)
        {
            bool hasText = !string.IsNullOrEmpty(data.Message.Text);
            _bubble.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            _bubbleText.Text = data.Message.Text ?? string.Empty;
            _bubble.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            const double r = 18, tight = 6;
            _bubble.CornerRadius = data.Mine
                ? new CornerRadius(r, data.First ? r : tight, data.Last ? r : tight, r)
                : new CornerRadius(data.First ? r : tight, r, r, data.Last ? r : tight);
            _bubble.SetResourceReference(Border.BackgroundProperty, data.Mine ? "ArcWpf.Brush.Accent" : "ArcWpf.Brush.ChatTheirs");
            _bubbleText.SetResourceReference(TextBlock.ForegroundProperty, data.Mine ? "ArcWpf.Brush.AccentForeground" : "ArcWpf.Brush.Foreground");
            _bubble.Opacity = data.Message.Status == ChatMessageStatus.Failed ? .6 : 1;
            _bubble.ToolTip = hasText ? $"{data.Message.CreatedAt.ToString("MMM d, yyyy", culture)}, {data.Message.CreatedAt.ToString("h:mm tt", culture)}" : null;
        }

        private void UpdateAttachments(ChatRules.MessageRow data, CultureInfo culture)
        {
            var list = data.Message.Attachments ?? [];
            string key = string.Join("|", list.Select(entry => entry.Id));
            if (key == _attachmentKey) { return; }
            _attachmentKey = key;
            if (_attachmentHost is not null) { _parts.Children.Remove(_attachmentHost); _attachmentHost = null; }
            if (list.Count == 0) { return; }
            var host = new StackPanel { HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            var images = list.Where(entry => entry.Kind == ChatAttachmentKind.Image && entry.Image is not null).ToList();
            var files = list.Where(entry => entry.Kind != ChatAttachmentKind.Image || entry.Image is null).ToList();
            if (images.Count > 0) { host.Children.Add(BuildGallery(data.Message, images)); }
            foreach (var file in files) { host.Children.Add(BuildFile(data.Message, file, host.Children.Count > 0)); }
            _attachmentHost = host;
            host.Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(data.Message.Text) ? 0 : 4);
            _parts.Children.Insert(0, host);
        }

        private FrameworkElement BuildGallery(ChatMessage message, IReadOnlyList<ChatAttachment> images)
        {
            const double width = 240;
            int count = Math.Min(images.Count, 4);
            ArcChromeButton Tile(ChatAttachment image, double w, double h, double radius, int at)
            {
                var surface = new Border { Width = w, Height = h, Background = new ImageBrush(image.Image) { Stretch = Stretch.UniformToFill }, CornerRadius = new CornerRadius(radius) };
                var grid = new Grid { Children = { surface } };
                if (at == 3 && images.Count > 4)
                {
                    var cover = new Border { CornerRadius = new CornerRadius(radius), Child = new TextBlock { Text = $"+{images.Count - 4}", FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                    cover.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.ChatMore");
                    grid.Children.Add(cover);
                }
                var tile = new ArcChromeButton(radius) { Width = w, Height = h, Content = grid };
                AutomationProperties.SetName(tile, image.Alt ?? image.Name);
                tile.Click += (_, _) => _owner.AttachmentInvoked?.Invoke(_owner, new ChatAttachmentEventArgs(message, image));
                return tile;
            }
            if (count == 1)
            {
                var only = images[0];
                double ratio = only.PixelWidth is > 0 && only.PixelHeight is > 0 ? only.PixelWidth.Value / only.PixelHeight.Value : 4d / 3;
                return Tile(only, width, width / ratio, 16, 0);
            }
            double cell = (width - 3) / 2;
            var grid = new Grid { Width = width };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            int rows = count == 2 ? 1 : 2;
            for (int i = 0; i < rows; i++)
            {
                if (i > 0) { grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) }); }
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            for (int i = 0; i < count; i++)
            {
                bool wide = count == 3 && i == 0;
                int position = count == 3 && i > 0 ? i + 1 : i;
                var tile = Tile(images[i], wide ? width : cell, wide ? width / 2 : cell, 6, i);
                Grid.SetRow(tile, position / 2 * 2);
                Grid.SetColumn(tile, position % 2 * 2);
                if (wide) { Grid.SetColumnSpan(tile, 3); }
                grid.Children.Add(tile);
            }
            grid.Clip = null;
            var frame = new Border { Child = grid };
            frame.Loaded += (_, _) => frame.Clip = new RectangleGeometry(new Rect(frame.RenderSize), 16, 16);
            frame.SizeChanged += (_, e) => frame.Clip = new RectangleGeometry(new Rect(e.NewSize), 16, 16);
            return frame;
        }

        private FrameworkElement BuildFile(ChatMessage message, ChatAttachment file, bool gap)
        {
            var button = new ArcChromeButton(16, 1) { Padding = new Thickness(11, 9, 14, 9), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, gap ? 4 : 0, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Roles = target => (target.IsMouseOver ? "SurfaceMuted" : "Surface", "Border", "Foreground");
            var icon = ArcText.Icon("file-text", 18, (Brush)_owner.FindResource("ArcWpf.Brush.TextSecondary"));
            icon.Margin = new Thickness(0, 0, 10, 0);
            icon.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
            var name = ArcText.Block(file.Name, 14, 19.6, "Foreground", true);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var copy = new StackPanel { Children = { name } };
            if (file.Size is { } size) { copy.Children.Add(ArcText.Block(ChatRules.FormatBytes(size), 12, 16.8, "TextMuted")); }
            DockPanel.SetDock(icon, Dock.Left);
            button.Content = new DockPanel { Children = { icon, copy } };
            AutomationProperties.SetName(button, file.Name);
            button.Click += (_, _) => _owner.AttachmentInvoked?.Invoke(_owner, new ChatAttachmentEventArgs(message, file));
            return button;
        }

        private void UpdateReactions(ChatRules.MessageRow data, ChatThreadLabels labels, bool isNew)
        {
            var list = data.Message.Reactions ?? [];
            bool had = _hasReactions;
            _hasReactions = list.Count > 0;
            bool animate = !isNew && _owner.Full;
            _reactions.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _reactions.Margin = data.Mine ? new Thickness(4, 2, 8, 2) : new Thickness(8, 2, 4, 2);
            _reactionClip.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var before = animate ? Flip.Capture(_reactions) : [];
            var keep = list.Select(entry => entry.Emoji).ToHashSet();
            foreach (string emoji in _reactionItems.Keys.Where(emoji => !keep.Contains(emoji)).ToList())
            {
                _reactions.Children.Remove(_reactionItems[emoji]);
                _reactionItems.Remove(emoji);
                _reactionCounts.Remove(emoji);
            }
            for (int i = 0; i < list.Count; i++)
            {
                var reaction = list[i];
                if (!_reactionItems.TryGetValue(reaction.Emoji, out var item))
                {
                    item = BuildReaction(reaction.Emoji);
                    _reactionItems[reaction.Emoji] = item;
                    _reactions.Children.Insert(Math.Min(i, _reactions.Children.Count), item);
                    if (animate) { PopReaction(item); }
                }
                var button = (ArcChromeButton)((Border)item).Child;
                button.Tag = reaction.Mine;
                button.Refresh(!isNew);
                _reactionCounts[reaction.Emoji].Set(reaction.Count);
                AutomationProperties.SetName(button, labels.ReactionCount(reaction.Emoji, reaction.Count, reaction.Mine));
            }
            if (animate) { Flip.Play(_reactions, before, Pop); }
            if (_hasReactions && !had)
            {
                _reactionClip.Visibility = Visibility.Visible;
                if (animate) { _reactionClip.Reveal(); _reactionClip.Opacity = 0; Tween.To(_reactionClip, UIElement.OpacityProperty, 1, Tween.Fast, null); }
            }
            else if (!_hasReactions && had)
            {
                if (animate) { Tween.To(_reactionClip, UIElement.OpacityProperty, 0, Tween.Fast, null); _reactionClip.Collapse(() => _reactionClip.Visibility = Visibility.Collapsed, true); }
                else { _reactionClip.Visibility = Visibility.Collapsed; }
            }
        }

        private FrameworkElement BuildReaction(string emoji)
        {
            var count = new ThreadCount(8, false) { VerticalAlignment = VerticalAlignment.Center };
            _reactionCounts[emoji] = count;
            var button = new ArcChromeButton(13, 1) { Height = 26, Padding = new Thickness(7, 0, 9, 0), FontSize = 12, FontWeight = FontWeights.Medium, Tag = false };
            button.Roles = target =>
            {
                bool mine = target.Tag is true;
                return (mine ? "ChatReactionMine" : "Surface", mine ? "ChatReactionMineEdge" : target.IsMouseOver ? "BorderStrong" : "BorderSubtle", mine ? "Foreground" : "TextSecondary");
            };
            var glyph = new ColorEmojiGlyph { Text = emoji, EmojiSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            button.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, count } };
            button.Click += (_, _) => _owner.React(_id, emoji);
            ThreadUi.PressScale(button, .95);
            return new Border { Margin = new Thickness(0, 0, 4, 0), Child = button, RenderTransform = new TranslateTransform() };
        }

        private static void PopReaction(FrameworkElement item)
        {
            var scale = new ScaleTransform(.6, .6);
            item.RenderTransformOrigin = new Point(.5, .5);
            var inner = ((Border)item).Child;
            var wrap = new TransformGroup { Children = { scale } };
            if (inner.RenderTransform is ScaleTransform press) { wrap.Children.Add(press); }
            inner.RenderTransform = wrap;
            inner.RenderTransformOrigin = new Point(.5, .5);
            inner.Opacity = 0;
            Tween.Spring(inner, UIElement.OpacityProperty, 1, Pop);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Pop);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Pop);
        }

        private void UpdateReceipts(ChatRules.MessageRow data, ChatThreadLabels labels, ChatMessageStatus? status, IReadOnlyList<ChatParticipant> seenBy)
        {
            _receipts.Children.Clear();
            Readers.Clear();
            _readerCount = seenBy.Count;
            _status = status switch
            {
                ChatMessageStatus.Sending => labels.Sending,
                ChatMessageStatus.Sent => labels.Sent,
                ChatMessageStatus.Delivered => labels.Delivered,
                ChatMessageStatus.Read => labels.Read,
                ChatMessageStatus.Failed => labels.Failed,
                _ => string.Empty,
            };
            _receipts.Visibility = status is not null || seenBy.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _receipts.HorizontalAlignment = data.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (status is not null)
            {
                bool failed = status == ChatMessageStatus.Failed;
                var text = ArcText.Block(_status, 12, 16.8, failed ? "ArcDanger" : "TextMuted");
                text.VerticalAlignment = VerticalAlignment.Center;
                _receipts.Children.Add(text);
                if (failed && _owner.RetryRequested is not null)
                {
                    var retry = new ArcChromeButton(10, 1) { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0), FontSize = 12, FontWeight = FontWeights.Medium };
                    retry.Roles = button => (button.IsMouseOver ? "SurfaceMuted" : "Surface", button.IsMouseOver ? "BorderStrong" : "Border", "Foreground");
                    var glyph = ArcText.Icon("rotate-cw", 12, (Brush)_owner.FindResource("ArcWpf.Brush.TextSecondary"), 2);
                    glyph.Margin = new Thickness(0, 0, 4, 0);
                    retry.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, new TextBlock { Text = labels.Retry, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight } } };
                    retry.Click += (_, _) => _owner.RetryRequested?.Invoke(_owner, new ChatMessageEventArgs(_id));
                    _receipts.Children.Add(retry);
                }
            }
            if (seenBy.Count == 0) { return; }
            var readers = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(status is null ? 0 : 6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(readers, labels.ReadBy(seenBy.Select(who => who.Name).ToList()));
            for (int i = 0; i < seenBy.Count; i++)
            {
                var ring = ThreadUi.Ringed(ThreadUi.Face(seenBy[i].Name, seenBy[i].Avatar, 16), 16);
                ring.Margin = new Thickness(i == 0 ? -2 : -6, -2, -2, -2);
                readers.Children.Add(ring);
                Readers[seenBy[i].Id] = ring;
            }
            _receipts.Children.Add(readers);
        }

        public void Arrive(bool mine)
        {
            var scale = new ScaleTransform();
            var move = new TranslateTransform();
            _row.RenderTransform = new TransformGroup { Children = { scale, move } };
            _row.RenderTransformOrigin = mine ? new Point(1, 1) : new Point(0, 1);
            _row.Opacity = 0;
            if (!_owner.Full)
            {
                Tween.Spring(_row, UIElement.OpacityProperty, 1, Tween.Fast);
                return;
            }
            scale.ScaleX = scale.ScaleY = .92;
            move.Y = 14;
            move.X = mine ? 8 : -8;
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, ArriveSeconds);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, ArriveSeconds);
            Tween.Spring(move, TranslateTransform.YProperty, 0, ArriveSeconds);
            Tween.Spring(move, TranslateTransform.XProperty, 0, ArriveSeconds);
            Tween.To(_row, UIElement.OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
            ArcEffects.BlurIn(_owner._motion, _row, 4, Tween.Standard);
        }

        public ChatReactionPicker OpenPicker(bool below, IReadOnlyList<string> emoji, ChatThreadLabels labels, bool full, bool moving)
        {
            var picker = new ChatReactionPicker(emoji, labels, full, moving, picked => _owner.React(_id, picked), () => _owner.ClosePicker(true));
            _pickerSurface = picker;
            _pickerHost.Children.Clear();
            _pickerHost.Children.Add(picker);
            _pickerHost.HorizontalAlignment = _mine ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            _pickerHost.VerticalAlignment = below ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            if (_mine) { Canvas.SetLeft(picker, -4); } else { Canvas.SetRight(picker, -4); }
            if (below) { Canvas.SetTop(picker, 6); } else { Canvas.SetBottom(picker, 6); }
            Panel.SetZIndex(_row, 5);
            _reactButton.Refresh(true);
            RefreshButton();
            picker.PlayIn(_mine, below);
            return picker;
        }

        public void ClosePicker(bool full, bool moving)
        {
            if (_pickerSurface is not ChatReactionPicker picker) { return; }
            _pickerSurface = null;
            _reactButton.Refresh(true);
            RefreshButton();
            picker.PlayOut(full, moving, () =>
            {
                _pickerHost.Children.Remove(picker);
                if (_pickerSurface is null) { Panel.SetZIndex(_row, 0); }
            });
        }
    }
}

/// <summary>The row of emoji that opens over a bubble.</summary>
internal sealed class ChatReactionPicker : Border
{
    private const double Pop = .3;
    private readonly List<ArcChromeButton> _buttons = [];
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _move = new();
    private readonly bool _full, _moving;
    private bool _below;
    private int _active;

    public ChatReactionPicker(IReadOnlyList<string> emoji, ChatThreadLabels labels, bool full, bool moving, Action<string> pick, Action close)
    {
        _full = full;
        _moving = moving;
        Padding = new Thickness(4);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(22);
        SetResourceReference(BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        SetResourceReference(BorderBrushProperty, "ArcWpf.Brush.Border");
        SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        RenderTransform = new TransformGroup { Children = { _scale, _move } };
        AutomationProperties.SetName(this, labels.Reactions);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < emoji.Count; i++)
        {
            string value = emoji[i];
            var button = new ArcChromeButton(17) { Width = 34, Height = 34, Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
            button.FocusVisualStyle = null;
            button.Roles = target => (target.IsMouseOver || ThreadUi.FocusVisible(target) ? "SurfaceMuted" : null, null, "Foreground");
            button.Content = new ColorEmojiGlyph { Text = value, EmojiSize = 19, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (_, _) => pick(value);
            KeyboardNavigation.SetIsTabStop(button, i == 0);
            AutomationProperties.SetName(button, labels.ReactWith(value));
            int index = i;
            button.GotKeyboardFocus += (_, _) => { _active = index; for (int at = 0; at < _buttons.Count; at++) { KeyboardNavigation.SetIsTabStop(_buttons[at], at == index); } button.Refresh(true); };
            button.LostKeyboardFocus += (_, _) => button.Refresh(true);
            var scale = new ScaleTransform();
            var lift = new TranslateTransform();
            button.RenderTransform = new TransformGroup { Children = { scale, lift } };
            button.RenderTransformOrigin = new Point(.5, .5);
            void To(double value2) { foreach (var p in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty }) { if (_moving) { Tween.To(scale, p, value2, Tween.Instant, ArcEasing.Standard); } else { Tween.Set(scale, p, value2); } } }
            button.MouseEnter += (_, _) => To(1.12);
            button.MouseLeave += (_, _) => To(1);
            button.PreviewMouseLeftButtonDown += (_, _) => To(.9);
            button.PreviewMouseLeftButtonUp += (_, _) => To(button.IsMouseOver ? 1.12 : 1);
            _buttons.Add(button);
            row.Children.Add(button);
        }
        Child = row;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; close(); return; }
            int step = e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0;
            if (step == 0 || _buttons.Count == 0) { return; }
            e.Handled = true;
            _buttons[(_active + step + _buttons.Count) % _buttons.Count].Focus();
        };
    }

    public void PlayIn(bool mine, bool below)
    {
        _below = below;
        Loaded += OnLoaded;
        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            // The origin sits over the reaction button: 18 from the near corner.
            double w = ActualWidth, h = ActualHeight;
            _scale.CenterX = mine ? 18 : w - 18;
            _scale.CenterY = below ? 0 : h;
            _buttons.FirstOrDefault()?.Focus();
        }
        if (!_moving) { return; }
        Opacity = 0;
        double seconds = _full ? Pop : Tween.Fast;
        Tween.Spring(this, OpacityProperty, 1, seconds);
        if (!_full) { return; }
        _scale.ScaleX = _scale.ScaleY = .85;
        _move.Y = below ? -6 : 6;
        Tween.Spring(_scale, ScaleTransform.ScaleXProperty, 1, Pop);
        Tween.Spring(_scale, ScaleTransform.ScaleYProperty, 1, Pop);
        Tween.Spring(_move, TranslateTransform.YProperty, 0, Pop);
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var lift = (TranslateTransform)((TransformGroup)button.RenderTransform).Children[1];
            lift.Y = 6;
            button.Opacity = 0;
            double delay = i * .025;
            Tween.Spring(button, OpacityProperty, 1, Pop, delay);
            Tween.Spring(lift, TranslateTransform.YProperty, 0, Pop, delay);
        }
    }

    public void PlayOut(bool full, bool moving, Action done)
    {
        IsHitTestVisible = false;
        if (!moving) { done(); return; }
        Tween.To(this, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard, done: done);
        if (!full) { return; }
        Tween.To(_scale, ScaleTransform.ScaleXProperty, .9, Tween.Exit, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleYProperty, .9, Tween.Exit, ArcEasing.Standard);
        Tween.To(_move, TranslateTransform.YProperty, _below ? -4 : 4, Tween.Exit, ArcEasing.Standard);
    }
}

/// <summary>The pill when new messages land out of view, or the reader has scrolled far. The width morphs as its label changes.</summary>
internal sealed class ChatPill : ArcChromeButton
{
    private readonly SpringDriver _driver;
    private readonly ControlSpring _width;
    private readonly ChatThread _owner;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _move = new();
    private readonly StackPanel _row;
    private readonly TextBlock _before, _after;
    private readonly ThreadCount _count;
    private bool _showing, _measured;
    private string _shape = string.Empty;

    public ChatPill(ChatThread owner) : base(16, 1)
    {
        _owner = owner;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Bottom;
        Height = 32;
        Padding = new Thickness(11, 0, 14, 0);
        FontSize = 12;
        FontWeight = FontWeights.Medium;
        Visibility = Visibility.Collapsed;
        Roles = _ => ("SurfaceRaised", "Border", "Foreground");
        SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        RenderTransform = new TransformGroup { Children = { _scale, _move } };
        RenderTransformOrigin = new Point(.5, .5);
        _driver = new SpringDriver(() => Width = _width!.Position);
        _width = _driver.Add(0, Tween.Morph);
        _before = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _after = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _count = new ThreadCount(8, false) { VerticalAlignment = VerticalAlignment.Center };
        var icon = ArcText.Icon("arrow-down", 14, Ink, 2);
        icon.Margin = new Thickness(0, 0, 6, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        _row = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, _before, _count, _after } };
        Content = _row;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        Unloaded += (_, _) => _driver.Finish();
    }

    public bool Showing => _showing;
    public string Text => _before.Text + (_count.Visibility == Visibility.Visible ? _count.Value.ToString() : string.Empty) + _after.Text;
    public void FinishForTest() => _driver.Finish();

    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;

    public void Show(bool show, int unread, ChatThreadLabels labels)
    {
        string text = unread > 0 ? labels.NewMessages(unread) : labels.JumpToLatest;
        string digits = unread.ToString(CultureInfo.InvariantCulture);
        int at = unread > 0 ? text.IndexOf(digits, StringComparison.Ordinal) : -1;
        _before.Text = at < 0 ? text : text[..at];
        _after.Text = at < 0 ? string.Empty : text[(at + digits.Length)..];
        _count.Visibility = at < 0 ? Visibility.Collapsed : Visibility.Visible;
        if (at >= 0) { _count.Set(unread); }
        AutomationProperties.SetName(this, text);
        string shape = text;
        if (show != _showing)
        {
            _showing = show;
            Animate(show);
        }
        if (shape != _shape && Visibility == Visibility.Visible) { Resize(); }
        _shape = shape;
    }

    private double MeasureWidth()
    {
        _row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return _row.DesiredSize.Width + Padding.Left + Padding.Right + 2;
    }

    private void Resize()
    {
        double target = MeasureWidth();
        if (!_measured || !Full) { _measured = true; _driver.Snap(_width, target); return; }
        _driver.Animate(_width, target, Tween.Morph);
    }

    private void Animate(bool show)
    {
        bool moving = IsLoaded && Tween.Level(this) != ArcMotionLevel.Off, full = Full;
        if (show)
        {
            Visibility = Visibility.Visible;
            _measured = false;
            Resize();
            if (!moving) { Tween.Set(this, OpacityProperty, 1); return; }
            Opacity = 0;
            Tween.To(this, OpacityProperty, 1, Tween.Fast, null);
            if (!full) { return; }
            Tween.Set(_move, TranslateTransform.YProperty, 12);
            Tween.Set(_scale, ScaleTransform.ScaleXProperty, .94);
            Tween.Set(_scale, ScaleTransform.ScaleYProperty, .94);
            Tween.Spring(_move, TranslateTransform.YProperty, 0, Tween.Morph);
            Tween.Spring(_scale, ScaleTransform.ScaleXProperty, 1, Tween.Morph);
            Tween.Spring(_scale, ScaleTransform.ScaleYProperty, 1, Tween.Morph);
            return;
        }
        if (!moving) { Visibility = Visibility.Collapsed; return; }
        Tween.To(this, OpacityProperty, 0, Tween.Exit, ArcEasing.Standard, done: () => { if (!_showing) { Visibility = Visibility.Collapsed; } });
        if (!full) { return; }
        Tween.To(_move, TranslateTransform.YProperty, 12, Tween.Exit, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleXProperty, .94, Tween.Exit, ArcEasing.Standard);
        Tween.To(_scale, ScaleTransform.ScaleYProperty, .94, Tween.Exit, ArcEasing.Standard);
    }
}

/// <summary>Smooth-scroll to a position, stopped by the wheel or the thumb as a browser's is.</summary>
internal sealed class ScrollRide
{
    private readonly ScrollViewer _scroller;
    public ScrollRide(ScrollViewer scroller) => _scroller = scroller;

    public void To(double target)
    {
        double from = _scroller.VerticalOffset, distance = target - from;
        if (Math.Abs(distance) < 1) { _scroller.ScrollToEnd(); return; }
        double seconds = Math.Clamp(Math.Abs(distance) / 1600, .2, .6);
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var ease = ArcEasing.InOut;
        void Frame(object? sender, EventArgs e)
        {
            double t = Math.Min(1, System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalSeconds / seconds);
            _scroller.ScrollToVerticalOffset(from + distance * ease.Ease(t));
            if (t < 1) { return; }
            CompositionTarget.Rendering -= Frame;
            _scroller.ScrollToEnd();
        }
        CompositionTarget.Rendering += Frame;
    }
}
