using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class CommentAuthor
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Square image. Initials are shown without one.</summary>
    public ImageSource? Avatar { get; init; }
}

/// <param name="Emoji">The reaction.</param>
/// <param name="Users">Ids of the people who reacted.</param>
public sealed record CommentReaction(string Emoji, IReadOnlyList<string> Users);

public sealed record ThreadComment
{
    public required string Id { get; init; }
    public required CommentAuthor Author { get; init; }
    /// <summary>Plain text. "@Full Name" mentions of known people are highlighted.</summary>
    public string Body { get; init; } = string.Empty;
    /// <summary>Display label such as "2h" or "Sep 18".</summary>
    public string CreatedAt { get; init; } = string.Empty;
    public bool Edited { get; init; }
    /// <summary>A deleted comment that still has replies keeps its place as a quiet placeholder.</summary>
    public bool Deleted { get; init; }
    public IReadOnlyList<CommentReaction>? Reactions { get; init; }
    public IReadOnlyList<ThreadComment>? Replies { get; init; }
}

public enum CommentThreadEventType { Reply, Edit, Delete, React }

/// <summary>What changed, for syncing to a server. <see cref="Comments"/> is always the full next tree.</summary>
public sealed class CommentsChangedEventArgs(IReadOnlyList<ThreadComment> comments, CommentThreadEventType type, string id) : EventArgs
{
    public IReadOnlyList<ThreadComment> Comments { get; } = comments;
    public CommentThreadEventType Type { get; } = type;
    public string Id { get; } = id;
    public ThreadComment? Comment { get; init; }
    public string? ParentId { get; init; }
    public string? Body { get; init; }
    public string? Emoji { get; init; }
    public bool Added { get; init; }
}

/// <summary>Every word the thread shows or announces. Words with a count or a name are functions, so an app can apply its own plural rules.</summary>
public sealed class CommentThreadLabels
{
    public string Thread { get; init; } = "Comment thread";
    public Func<int, string> CommentCount { get; init; } = count => $"{count} {(count == 1 ? "comment" : "comments")}";
    public Func<int, string> PeopleCount { get; init; } = count => $"{count} {(count == 1 ? "person" : "people")}";
    public string Resolve { get; init; } = "Resolve";
    public string Resolved { get; init; } = "Resolved";
    public string Reopen { get; init; } = "Reopen";
    public string Empty { get; init; } = "No comments yet. Start the conversation below.";
    public string Reply { get; init; } = "Reply";
    public string Edit { get; init; } = "Edit";
    public string Delete { get; init; } = "Delete";
    public string Cancel { get; init; } = "Cancel";
    public string Send { get; init; } = "Send";
    public string Save { get; init; } = "Save";
    public string EditComment { get; init; } = "Edit comment";
    public string EditedMark { get; init; } = "edited";
    public string DeleteConfirm { get; init; } = "Delete this comment?";
    public string DeletedText { get; init; } = "This comment was deleted";
    public string DeletedComment { get; init; } = "Deleted comment";
    public Func<string, string> ReplyingTo { get; init; } = name => $"Replying to {name}";
    public string CancelReply { get; init; } = "Cancel reply";
    public Func<int, string> ShowReplies { get; init; } = count => $"Show {count} {(count == 1 ? "reply" : "replies")}";
    public string HideReplies { get; init; } = "Hide replies";
    public string AddReaction { get; init; } = "Add reaction";
    public string CloseReactions { get; init; } = "Close reactions";
    public string Reactions { get; init; } = "Reactions";
    public Func<string, string> ReactWith { get; init; } = emoji => $"React with {emoji}";
    public Func<string, int, bool, string> ReactionCount { get; init; } = (emoji, count, mine) => $"{emoji} {count}{(mine ? ", including you" : "")}";
    public string Mentions { get; init; } = "People";
}

// Tree helpers. Every change returns a new tree so controlled parents can store it as is.
internal static class CommentTree
{
    public static List<ThreadComment> Map(IEnumerable<ThreadComment> list, string id, Func<ThreadComment, ThreadComment?> change)
    {
        var next = new List<ThreadComment>();
        foreach (var comment in list)
        {
            if (comment.Id == id) { if (change(comment) is { } changed) { next.Add(changed); } continue; }
            next.Add(comment.Replies is { Count: > 0 } ? comment with { Replies = Map(comment.Replies, id, change) } : comment);
        }
        return next;
    }

    public static int Count(IEnumerable<ThreadComment> list) => list.Sum(comment => (comment.Deleted ? 0 : 1) + Count(comment.Replies ?? []));

    public static List<CommentAuthor> Authors(IEnumerable<ThreadComment> list, IEnumerable<CommentAuthor>? seed = null)
    {
        var into = new List<CommentAuthor>(seed ?? []);
        void Walk(IEnumerable<ThreadComment> level)
        {
            foreach (var comment in level)
            {
                if (!comment.Deleted && into.All(author => author.Id != comment.Author.Id)) { into.Add(comment.Author); }
                Walk(comment.Replies ?? []);
            }
        }
        Walk(list);
        return into;
    }

    /// <summary>Path of ids from the root to <paramref name="id"/>.</summary>
    public static List<string>? PathTo(IEnumerable<ThreadComment> list, string id, List<string>? trail = null)
    {
        trail ??= [];
        foreach (var comment in list)
        {
            if (comment.Id == id) { return [.. trail, id]; }
            if (PathTo(comment.Replies ?? [], id, [.. trail, comment.Id]) is { } found) { return found; }
        }
        return null;
    }

    public static ThreadComment? Find(IEnumerable<ThreadComment> list, string id)
    {
        foreach (var comment in list)
        {
            if (comment.Id == id) { return comment; }
            if (Find(comment.Replies ?? [], id) is { } found) { return found; }
        }
        return null;
    }

    /// <summary>Splits text into plain parts and "@Full Name" mentions of known people, longest names first.</summary>
    public static IEnumerable<(string Text, bool Mention)> Parts(string text, IEnumerable<string> names)
    {
        var escaped = names.OrderByDescending(name => name.Length).Select(Regex.Escape).ToList();
        if (escaped.Count == 0) { yield return (text, false); yield break; }
        var pieces = Regex.Split(text, $"(@(?:{string.Join("|", escaped)}))");
        for (int i = 0; i < pieces.Length; i++) { if (pieces[i].Length > 0) { yield return (pieces[i], i % 2 == 1); } }
    }

    /// <summary>Where a mention query starts: an @ at the start or after a space, followed only by letters, up to the caret.</summary>
    public static (int Start, string Term)? Query(string value, int caret)
    {
        var match = Regex.Match(value[..Math.Min(caret, value.Length)], @"(^|\s)@(\p{L}*)$");
        return match.Success ? (caret - match.Groups[2].Length - 1, match.Groups[2].Value) : null;
    }

    public static List<CommentAuthor> Suggest(IEnumerable<CommentAuthor> people, string term)
    {
        string low = term.ToLowerInvariant();
        return people.Where(person => person.Name.ToLowerInvariant().Split(' ').Any(part => part.StartsWith(low, StringComparison.Ordinal)) || person.Name.ToLowerInvariant().StartsWith(low, StringComparison.Ordinal)).Take(5).ToList();
    }
}

/// <summary>
/// A threaded discussion: replies nest under their comment and collapse, reactions toggle with a count that rolls,
/// authors edit and delete their own comments in place, mentions autocomplete in the composer, and resolving folds the
/// whole thread into a single chip that reopens it. Controlled with <see cref="Comments"/>, or uncontrolled with <see cref="DefaultComments"/>.
/// </summary>
public class CommentThread : Control
{
    private const double HeightSeconds = .42, ItemStagger = .035;
    private static readonly string[] DefaultReactions = ["👍", "❤️", "🎉", "👀", "🚀", "✅"];

    public static readonly DependencyProperty CommentsProperty = Prop<IReadOnlyList<ThreadComment>?>(nameof(Comments), null);
    public static readonly DependencyProperty DefaultCommentsProperty = DependencyProperty.Register(nameof(DefaultComments), typeof(IReadOnlyList<ThreadComment>), typeof(CommentThread), new PropertyMetadata(null, (d, e) => ((CommentThread)d).OnDefaultComments((IReadOnlyList<ThreadComment>?)e.NewValue)));
    public static readonly DependencyProperty CurrentUserProperty = Prop<CommentAuthor?>(nameof(CurrentUser), null);
    public static readonly DependencyProperty PeopleProperty = Prop<IReadOnlyList<CommentAuthor>?>(nameof(People), null);
    public static readonly DependencyProperty IsResolvedProperty = DependencyProperty.Register(nameof(IsResolved), typeof(bool), typeof(CommentThread), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CommentThread)d).OnResolved()));
    public static readonly DependencyProperty TitleProperty = Prop<string?>(nameof(Title), null);
    public static readonly DependencyProperty ReactionsProperty = Prop<IReadOnlyList<string>>(nameof(Reactions), DefaultReactions);
    public static readonly DependencyProperty PlaceholderProperty = Prop(nameof(Placeholder), "Reply, or @mention someone");
    public static readonly DependencyProperty MaxDepthProperty = Prop(nameof(MaxDepth), 2);
    public static readonly DependencyProperty NowLabelProperty = Prop(nameof(NowLabel), "Just now");
    public static readonly DependencyProperty LabelsProperty = Prop(nameof(Labels), new CommentThreadLabels());
    public static readonly DependencyProperty AlwaysShowActionsProperty = Prop(nameof(AlwaysShowActions), false);
    private static DependencyProperty Prop<T>(string name, T value) => DependencyProperty.Register(name, typeof(T), typeof(CommentThread), new PropertyMetadata(value, (d, _) => ((CommentThread)d).Sync()));

    private readonly OwnedMotion _motion = new();
    private readonly Dictionary<string, CommentView> _views = [];
    private readonly Dictionary<string, bool> _collapsed = [];
    private List<ThreadComment> _inner = [];
    private Decorator? _root;
    private Border? _frame;
    private SpringHeightBox? _height;
    private Grid? _faces;
    private Canvas? _exits;
    private FrameworkElement? _threadFace, _resolvedFace;
    private TextBlock? _titleText, _subtitle, _empty, _resolvedTitle, _resolvedMeta;
    private StackPanel? _list, _resolvedStack;
    private SpringHeightBox? _replying;
    private TextBlock? _replyingText;
    private CommentComposer? _composer;
    private ArcChromeButton? _resolve, _reopen;
    private string? _editing, _confirming, _picking, _replyTo, _replyName;
    private bool _built, _resolved;
    private double _serial;

    static CommentThread() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CommentThread), new FrameworkPropertyMetadata(typeof(CommentThread)));

    public CommentThread() => Unloaded += (_, _) => _motion.Stop();

    public IReadOnlyList<ThreadComment>? Comments { get => (IReadOnlyList<ThreadComment>?)GetValue(CommentsProperty); set => SetValue(CommentsProperty, value); }
    public IReadOnlyList<ThreadComment>? DefaultComments { get => (IReadOnlyList<ThreadComment>?)GetValue(DefaultCommentsProperty); set => SetValue(DefaultCommentsProperty, value); }
    /// <summary>The person writing. Their comments can be edited and deleted.</summary>
    public CommentAuthor? CurrentUser { get => (CommentAuthor?)GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }
    /// <summary>People who can be mentioned. Defaults to everyone in the thread.</summary>
    public IReadOnlyList<CommentAuthor>? People { get => (IReadOnlyList<CommentAuthor>?)GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public bool IsResolved { get => (bool)GetValue(IsResolvedProperty); set => SetValue(IsResolvedProperty, value); }
    /// <summary>What the thread is about, such as "Hero headline".</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IReadOnlyList<string> Reactions { get => (IReadOnlyList<string>)GetValue(ReactionsProperty); set => SetValue(ReactionsProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    /// <summary>Replies deeper than this attach to the deepest allowed parent.</summary>
    public int MaxDepth { get => (int)GetValue(MaxDepthProperty); set => SetValue(MaxDepthProperty, value); }
    public string NowLabel { get => (string)GetValue(NowLabelProperty); set => SetValue(NowLabelProperty, value); }
    public CommentThreadLabels Labels { get => (CommentThreadLabels)GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    /// <summary>Keeps Reply, Edit and Delete in view without hovering, for touch screens, where Arc always shows them.</summary>
    public bool AlwaysShowActions { get => (bool)GetValue(AlwaysShowActionsProperty); set => SetValue(AlwaysShowActionsProperty, value); }

    public event EventHandler<CommentsChangedEventArgs>? CommentsChanged;
    public event EventHandler<RoutedPropertyChangedEventArgs<bool>>? ResolvedChanged;

    private IReadOnlyList<ThreadComment> Current => Comments ?? _inner;
    private bool Full => IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
    private bool Moving => IsLoaded && Tween.Level(this) != ArcMotionLevel.Off;
    private List<CommentAuthor> Mentionable => People?.ToList() ?? CommentTree.Authors(Current, CurrentUser is null ? null : [CurrentUser]);

    internal IReadOnlyList<ThreadComment> CurrentForTest => Current;
    internal ArcChromeButton? ResolveForTest => _resolve;
    internal ArcChromeButton? ReopenForTest => _reopen;
    internal CommentComposer? ComposerForTest => _composer;
    internal string SubtitleForTest => _subtitle?.Text ?? string.Empty;
    internal string ResolvedMetaForTest => _resolvedMeta?.Text ?? string.Empty;
    internal string? ReplyToForTest => _replyTo;
    internal string? EditingForTest => _editing;
    internal string? ConfirmingForTest => _confirming;
    internal string? PickingForTest => _picking;
    internal void StartReplyForTest(string id, string name) => StartReply(id, name);
    internal void StartEditForTest(string id) => StartEdit(id);
    internal void EditForTest(string id, string body) => Edit(id, body);
    internal void RemoveForTest(string id) => Remove(id);
    internal void ReactForTest(string id, string emoji) => React(id, emoji);
    internal void AskDeleteForTest(string id) => AskDelete(id);
    internal void ToggleRepliesForTest(string id) => ToggleReplies(id);
    internal bool RepliesCollapsedForTest(string id) => _collapsed.TryGetValue(id, out bool collapsed) && collapsed;
    internal bool HasViewForTest(string id) => _views.ContainsKey(id);
    internal string BodyTextForTest(string id) => _views.TryGetValue(id, out var view) ? view.BodyText : string.Empty;
    internal IReadOnlyList<string> MentionsForTest(string id) => _views.TryGetValue(id, out var view) ? view.Mentions : [];
    internal double ActionsOpacityForTest(string id) => _views.TryGetValue(id, out var view) ? view.ActionsOpacity : 0;
    internal CommentComposer? EditorForTest(string id) => _views.TryGetValue(id, out var view) ? view.Editor : null;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _resolved = IsResolved;
        _threadFace = BuildThread();
        _resolvedFace = BuildResolved();
        _exits = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        _faces = new Grid { Children = { _exits, _resolved ? _resolvedFace : _threadFace } };
        _height = new SpringHeightBox(HeightSeconds) { Child = _faces };
        _frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(26), Child = _height };
        _frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _frame.SizeChanged += (_, _) => ClipFrame();
        AutomationProperties.SetName(_frame, Labels.Thread);
        _root.Child = _frame;
        _built = true;
        ApplyRadius(false);
        Sync();
    }

    private void ClipFrame()
    {
        if (_frame is null || _height is null) { return; }
        double r = Math.Max(0, _frame.CornerRadius.TopLeft - 1);
        _height.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, _frame.ActualWidth - 2), Math.Max(0, _frame.ActualHeight - 2)), r, r);
    }

    private double _radius = 26;
    private System.Windows.Media.Animation.AnimationClock? _radiusClock;

    // The corner follows the height: --duration-spring on --ease-spring, as the CSS transition does.
    private void ApplyRadius(bool animate)
    {
        if (_frame is null) { return; }
        double target = IsResolved ? 22 : 26;
        _radiusClock?.Controller?.Stop();
        _radiusClock = null;
        if (!animate) { _radius = target; _frame.CornerRadius = new CornerRadius(target); ClipFrame(); return; }
        var ease = ArcEasing.Spring(Tween.SpringTokenSeconds / 2, Tween.SpringTokenSeconds);
        double from = _radius;
        var motion = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(Tween.SpringTokenSeconds)) { EasingFunction = ease };
        var clock = motion.CreateClock();
        clock.CurrentTimeInvalidated += (_, _) =>
        {
            if (clock.CurrentProgress is not { } progress || _frame is null) { return; }
            _radius = from + (target - from) * ease.Ease(progress);
            _frame.CornerRadius = new CornerRadius(_radius);
            ClipFrame();
        };
        _radiusClock = clock;
        clock.Controller?.Begin();
    }

    private void OnDefaultComments(IReadOnlyList<ThreadComment>? value)
    {
        if (_inner.Count == 0 && value is not null) { _inner = value.ToList(); Sync(); }
    }

    private FrameworkElement BuildThread()
    {
        _titleText = ArcText.Block(string.Empty, 14, 15.4, "Foreground", true);
        _titleText.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle = ArcText.Block(string.Empty, 12, 16.8, "TextMuted");
        var heading = new StackPanel { Children = { _titleText, _subtitle } };
        _resolve = Pill("check", 15);
        _resolve.Click += (_, _) => SetResolved(true);
        ((ArcDataGridIcon)((StackPanel)_resolve.Content).Children[0]).SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.ArcSuccess");
        _resolve.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(_resolve, Dock.Right);
        var header = new DockPanel { Margin = new Thickness(18, 12, 12, 8), Children = { _resolve, heading } };
        _list = new StackPanel { Margin = new Thickness(8, 0, 8, 0) };
        _empty = ArcText.Block(string.Empty, 14, 19.6, "TextMuted");
        _empty.Margin = new Thickness(18, 8, 18, 16);
        _empty.TextWrapping = TextWrapping.Wrap;
        _replyingText = new TextBlock { FontSize = 12, LineHeight = 16.8, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Center };
        _replyingText.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        var replyIcon = ArcText.Icon("corner-down-right", 13, (Brush)FindResource("ArcWpf.Brush.TextSecondary"));
        replyIcon.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
        replyIcon.Margin = new Thickness(0, 0, 6, 0);
        var cancelReply = IconAction("x", 14, 22);
        cancelReply.Margin = new Thickness(8, 0, 0, 0);
        cancelReply.Click += (_, _) => SetReply(null, null);
        AutomationProperties.SetName(cancelReply, Labels.CancelReply);
        var replyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(38, 0, 2, 8), Children = { replyIcon, _replyingText, cancelReply } };
        _replying = new SpringHeightBox(HeightSeconds) { Child = replyRow, Visibility = Visibility.Collapsed };
        _composer = new CommentComposer(this, Mentionable, string.Empty, Placeholder, Labels.Send, Labels.Cancel, false);
        _composer.Submitted += AddReply;
        _composer.Cancelled += () => SetReply(null, null);
        var area = new Border { Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(12, 10, 12, 12), BorderThickness = new Thickness(0, 1, 0, 0), Child = new StackPanel { Children = { _replying, _composer } } };
        area.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        return new StackPanel { Children = { header, _list, _empty, area } };
    }

    private FrameworkElement BuildResolved()
    {
        var icon = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.CommentResolvedIcon");
        var check = ArcText.Icon("check", 14, (Brush)FindResource("ArcWpf.Brush.ArcSuccess"), 2);
        check.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.ArcSuccess");
        icon.Child = check;
        _resolvedTitle = new TextBlock { FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        _resolvedTitle.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        _resolvedMeta = ArcText.Block(string.Empty, 12, 16.8, "TextMuted");
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _resolvedTitle, _resolvedMeta } };
        _resolvedStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        _reopen = Pill("rotate-ccw", 14);
        _reopen.Click += (_, _) => SetResolved(false);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(_reopen, Dock.Right);
        DockPanel.SetDock(_resolvedStack, Dock.Right);
        return new DockPanel { Margin = new Thickness(14, 10, 10, 10), Children = { icon, _reopen, _resolvedStack, text } };
    }

    private ArcChromeButton Pill(string icon, double size)
    {
        var button = new ArcChromeButton(16, 1) { Height = 32, Padding = new Thickness(10, 0, 12, 0), FontSize = 12, FontWeight = FontWeights.Medium };
        button.Roles = target => (target.IsMouseOver && target.IsEnabled ? "SurfaceMuted" : "Surface", target.IsMouseOver && target.IsEnabled ? "BorderStrong" : "Border", "Foreground");
        var glyph = ArcText.Icon(icon, size, button.Ink);
        glyph.Margin = new Thickness(0, 0, 6, 0);
        button.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, new TextBlock { VerticalAlignment = VerticalAlignment.Center } } };
        button.IsEnabledChanged += (_, e) => button.Opacity = (bool)e.NewValue ? 1 : .45;
        ThreadUi.PressScale(button, .97);
        return button;
    }

    internal static ArcChromeButton IconAction(string icon, double size, double box)
    {
        var button = new ArcChromeButton(box / 2) { Width = box, Height = box };
        button.Roles = target => target.IsMouseOver || ThreadUi.FocusVisible(target) ? ("SurfaceMuted", null, "Foreground") : (null, null, "TextMuted");
        button.Content = ArcText.Icon(icon, size, button.Ink);
        button.GotKeyboardFocus += (_, _) => button.Refresh(true);
        button.LostKeyboardFocus += (_, _) => button.Refresh(true);
        return button;
    }

    private static void SetPillText(ArcChromeButton button, string text) => ((TextBlock)((StackPanel)button.Content).Children[1]).Text = text;

    private void Sync()
    {
        if (!_built || _list is null || _composer is null) { return; }
        var labels = Labels;
        var list = Current;
        int total = CommentTree.Count(list);
        var participants = CommentTree.Authors(list);
        var mentionable = Mentionable;
        if (_frame is not null) { AutomationProperties.SetName(_frame, labels.Thread); }
        _titleText!.Text = Title ?? string.Empty;
        _titleText.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        _subtitle!.Text = labels.CommentCount(total);
        _resolve!.IsEnabled = total > 0;
        SetPillText(_resolve, labels.Resolve);
        SetPillText(_reopen!, labels.Reopen);
        _empty!.Text = labels.Empty;
        _empty.Visibility = list.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _list.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _composer.Update(mentionable, Placeholder, labels.Send, labels.Cancel, labels.Mentions, _replyTo is not null);
        _composer.SetLeading(CurrentUser);
        _resolvedTitle!.Inlines.Clear();
        _resolvedTitle.Inlines.Add(new Run(labels.Resolved));
        if (!string.IsNullOrEmpty(Title))
        {
            _resolvedTitle.Inlines.Add(new Run(" · "));
            var subject = new Run(Title) { FontWeight = FontWeights.Normal };
            subject.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            _resolvedTitle.Inlines.Add(subject);
        }
        _resolvedMeta!.Text = $"{labels.CommentCount(total)} · {labels.PeopleCount(participants.Count)}";
        FillStack(_resolvedStack!, participants, 22);
        Reconcile(_list, list, 0, _views.Count == 0);
    }

    internal static void FillStack(StackPanel stack, IEnumerable<CommentAuthor> authors, double size)
    {
        stack.Children.Clear();
        foreach (var author in authors.Take(3))
        {
            var ring = ThreadUi.Ringed(ThreadUi.Face(author.Name, author.Avatar, size, Math.Round(size * .38)), size);
            // padding-left 4, each face -5 over the last, with a 2 ring drawn outside its box.
            ring.Margin = new Thickness(stack.Children.Count == 0 ? -3 : -9, -2, 0, -2);
            stack.Children.Add(ring);
        }
    }

    internal void Reconcile(StackPanel host, IReadOnlyList<ThreadComment> comments, int depth, bool first)
    {
        var keep = comments.Select(comment => comment.Id).ToHashSet();
        foreach (var child in host.Children.OfType<CommentHost>().Where(child => !keep.Contains(child.CommentId) && !child.Box.IsCollapsing).ToList())
        {
            _views.Remove(child.CommentId);
            RemoveDescendants(child.CommentId);
            bool animate = Full;
            if (animate) { Tween.To(child, OpacityProperty, 0, Tween.Standard, ArcEasing.Enter); }
            child.Box.Collapse(() => host.Children.Remove(child), animate);
        }
        int at = 0;
        foreach (var comment in comments)
        {
            bool isNew = !_views.TryGetValue(comment.Id, out var view);
            if (isNew)
            {
                view = new CommentView(this, comment.Id);
                _views[comment.Id] = view;
            }
            view!.Update(comment, depth, isNew);
            int current = host.Children.IndexOf(view.Host);
            if (current < 0)
            {
                host.Children.Insert(Math.Min(at, host.Children.Count), view.Host);
                if (!first && Full)
                {
                    view.Host.Box.Reveal();
                    view.Host.Opacity = 0;
                    Tween.To(view.Host, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
                }
            }
            at++;
        }
    }

    private void RemoveDescendants(string id)
    {
        foreach (string key in _views.Where(pair => pair.Value.ParentId == id).Select(pair => pair.Key).ToList())
        {
            _views.Remove(key);
            RemoveDescendants(key);
        }
    }

    private void OnResolved()
    {
        if (!_built || IsResolved == _resolved || _faces is null || _height is null || _exits is null) { return; }
        _resolved = IsResolved;
        var next = _resolved ? _resolvedFace! : _threadFace!;
        var old = _resolved ? _threadFace! : _resolvedFace!;
        bool full = Full;
        if (full)
        {
            double width = _faces.ActualWidth;
            _faces.Children.Remove(old);
            old.Width = width;
            _exits.Children.Add(old);
            _height.MorphNext();
            _faces.Children.Add(next);
            next.Width = double.NaN;
            ArcEffects.BlurIn(_motion, next, 4, Tween.Standard);
            next.Opacity = 0;
            Tween.To(next, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
            ArcEffects.BlurOut(_motion, old, 4, Tween.Standard);
            Tween.To(old, OpacityProperty, 0, Tween.Standard, ArcEasing.Enter, done: () =>
            {
                _exits.Children.Remove(old);
                old.Effect = null;
                old.Width = double.NaN;
                old.BeginAnimation(OpacityProperty, null);
                old.Opacity = 1;
            });
        }
        else
        {
            _faces.Children.Remove(old);
            _faces.Children.Add(next);
        }
        ApplyRadius(Moving);
        Sync();
    }

    private void SetResolved(bool next)
    {
        SetReply(null, null);
        _editing = _confirming = _picking = null;
        SetCurrentValue(IsResolvedProperty, next);
        ResolvedChanged?.Invoke(this, new RoutedPropertyChangedEventArgs<bool>(!next, next));
        Sync();
        Dispatcher.BeginInvoke(() => (next ? _reopen : _resolve)?.Focus(), DispatcherPriority.Input);
    }

    private void Commit(List<ThreadComment> next, CommentsChangedEventArgs change)
    {
        if (Comments is null) { _inner = next; }
        CommentsChanged?.Invoke(this, change);
        Sync();
    }

    private string NewId() => $"c-{DateTime.Now.Ticks:x}-{++_serial}";

    private void AddReply(string body)
    {
        if (CurrentUser is null) { return; }
        var comment = new ThreadComment { Id = NewId(), Author = CurrentUser, Body = body, CreatedAt = NowLabel, Reactions = [] };
        string? parent = null;
        var list = Current;
        if (_replyTo is not null)
        {
            var path = CommentTree.PathTo(list, _replyTo) ?? [];
            int at = Math.Min(path.Count, MaxDepth) - 1;
            parent = at >= 0 && at < path.Count ? path[at] : null;
        }
        List<ThreadComment> next = parent is not null
            ? CommentTree.Map(list, parent, target => target with { Replies = [.. target.Replies ?? [], comment] })
            : [.. list, comment];
        if (parent is not null) { _collapsed[parent] = false; }
        Commit(next, new CommentsChangedEventArgs(next, CommentThreadEventType.Reply, comment.Id) { Comment = comment, ParentId = parent });
        SetReply(null, null);
    }

    private void Edit(string id, string body)
    {
        _editing = null;
        var next = CommentTree.Map(Current, id, comment => comment with { Body = body, Edited = true });
        Commit(next, new CommentsChangedEventArgs(next, CommentThreadEventType.Edit, id) { Body = body });
    }

    private void Remove(string id)
    {
        _confirming = null;
        var next = CommentTree.Map(Current, id, comment => comment.Replies is { Count: > 0 } ? comment with { Deleted = true, Body = string.Empty, Reactions = [] } : null);
        if (_replyTo == id) { SetReply(null, null); }
        Commit(next, new CommentsChangedEventArgs(next, CommentThreadEventType.Delete, id));
    }

    private void React(string id, string emoji)
    {
        var target = CommentTree.Find(Current, id);
        if (target is null || CurrentUser is null) { return; }
        var me = CurrentUser.Id;
        var reactions = target.Reactions ?? [];
        var existing = reactions.FirstOrDefault(reaction => reaction.Emoji == emoji);
        bool added = existing is null || !existing.Users.Contains(me);
        List<CommentReaction> changed = existing is not null
            ? reactions.Select(reaction => reaction.Emoji != emoji ? reaction : reaction with { Users = added ? [.. reaction.Users, me] : reaction.Users.Where(user => user != me).ToList() }).Where(reaction => reaction.Users.Count > 0).ToList()
            : [.. reactions, new CommentReaction(emoji, [me])];
        _picking = null;
        var next = CommentTree.Map(Current, id, comment => comment with { Reactions = changed });
        Commit(next, new CommentsChangedEventArgs(next, CommentThreadEventType.React, id) { Emoji = emoji, Added = added });
    }

    private void StartReply(string id, string name)
    {
        SetReply(id, name);
        _composer?.FocusEnd();
    }

    private void SetReply(string? id, string? name)
    {
        if (_replying is null || _replyingText is null || _composer is null) { _replyTo = id; _replyName = name; return; }
        bool had = _replyTo is not null;
        _replyTo = id;
        _replyName = name;
        _composer.SetCancellable(id is not null);
        if (id is null)
        {
            if (!had) { return; }
            bool animate = Full;
            if (animate) { Tween.To(_replying, OpacityProperty, 0, Tween.Standard, ArcEasing.Enter); }
            _replying.Collapse(() => _replying.Visibility = Visibility.Collapsed, animate);
            return;
        }
        string sentence = Labels.ReplyingTo(name ?? string.Empty);
        _replyingText.Inlines.Clear();
        int at = string.IsNullOrEmpty(name) ? -1 : sentence.IndexOf(name, StringComparison.Ordinal);
        if (at < 0) { _replyingText.Inlines.Add(new Run(sentence)); }
        else
        {
            _replyingText.Inlines.Add(new Run(sentence[..at]));
            var strong = new Run(name) { FontWeight = FontWeights.Medium };
            strong.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.Foreground");
            _replyingText.Inlines.Add(strong);
            _replyingText.Inlines.Add(new Run(sentence[(at + name!.Length)..]));
        }
        if (had || _replying.IsCollapsing) { if (_replying.IsCollapsing) { _replying.Cancel(); Tween.To(_replying, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter); } return; }
        _replying.Visibility = Visibility.Visible;
        if (Full)
        {
            _replying.Reveal();
            _replying.Opacity = 0;
            Tween.To(_replying, OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
        }
    }

    private void StartEdit(string id)
    {
        _editing = id; _confirming = null; _picking = null;
        Sync();
    }

    private void AskDelete(string id)
    {
        _confirming = id; _editing = null; _picking = null;
        Sync();
    }

    private void ToggleReplies(string id)
    {
        _collapsed[id] = !(_collapsed.TryGetValue(id, out bool collapsed) && collapsed);
        Sync();
    }

    internal sealed class CommentHost(string id, SpringHeightBox box) : Decorator
    {
        public string CommentId { get; } = id;
        public SpringHeightBox Box { get; } = box;
    }

    // A comment's row with its replies. It keeps its visuals across updates, so reactions, actions and the reply tree can animate.
    private sealed class CommentView
    {
        private enum Mode { Actions, Confirm, Picker }

        private readonly CommentThread _owner;
        private readonly string _id;
        private readonly Border _article;
        private readonly Grid _faceSlot;
        private readonly TextBlock _name, _time, _body, _deleted;
        private readonly DockPanel _meta;
        private readonly SpringHeightBox _swapHeight;
        private readonly Grid _swap;
        private readonly Canvas _swapExits;
        private readonly StackPanel _view;
        private readonly WrapPanel _footer, _reactions;
        private readonly Border _actionsSlot;
        private readonly ArcChromeButton _toggle;
        private readonly RotateTransform _chevron = new();
        private readonly StackPanel _toggleStack;
        private readonly TextBlock _toggleText;
        private readonly SpringHeightBox _repliesBox;
        private readonly StackPanel _repliesList;
        private readonly Dictionary<string, FrameworkElement> _reactionItems = [];
        private readonly Dictionary<string, ThreadCount> _reactionCounts = [];
        private readonly List<string> _mentions = [];
        private Mode? _mode;
        private FrameworkElement? _actions;
        private CommentComposer? _editor;
        private ThreadComment _data = null!;
        private string _faceKey = string.Empty;
        private bool _hasReplies, _collapsedNow, _editingNow;

        public CommentView(CommentThread owner, string id)
        {
            _owner = owner;
            _id = id;
            _faceSlot = new Grid { Width = 28, VerticalAlignment = VerticalAlignment.Top };
            _name = ArcText.Block(string.Empty, 14, 19.6, "Foreground", true);
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _time = ArcText.Block(string.Empty, 12, 16.8, "TextMuted");
            _time.Margin = new Thickness(8, 3, 0, 0);
            _time.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(_time, Dock.Right);
            _meta = new DockPanel { LastChildFill = false, MinHeight = 19.8, Children = { _name, _time } };
            DockPanel.SetDock(_name, Dock.Left);
            DockPanel.SetDock(_time, Dock.Left);
            _body = new TextBlock { FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap };
            _body.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            _reactions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            _actionsSlot = new Border { MinHeight = 28, Margin = new Thickness(0, 1, 0, 1), VerticalAlignment = VerticalAlignment.Center };
            _footer = new WrapPanel { MinHeight = 30, Margin = new Thickness(0, 4, 0, 0), Children = { _reactions, _actionsSlot } };
            _view = new StackPanel { Children = { _body, _footer } };
            _swapExits = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            _swap = new Grid { Children = { _swapExits, _view } };
            _swapHeight = new SpringHeightBox(HeightSeconds) { Child = _swap };
            _deleted = new TextBlock { FontSize = 14, FontStyle = FontStyles.Italic, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            _deleted.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            _toggle = new ArcChromeButton(14) { Height = 28, Padding = new Thickness(6, 0, 8, 0), Margin = new Thickness(-6, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, FontWeight = FontWeights.Medium, Visibility = Visibility.Collapsed };
            _toggle.Roles = button => (button.IsMouseOver || ThreadUi.FocusVisible(button) ? "SurfaceMuted" : null, null, "TextSecondary");
            var chevron = ArcText.Icon("chevron-right", 13, (Brush)_owner.FindResource("ArcWpf.Brush.TextMuted"));
            chevron.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextMuted");
            chevron.RenderTransform = _chevron;
            chevron.RenderTransformOrigin = new Point(.5, .5);
            chevron.Margin = new Thickness(0, 0, 6, 0);
            _toggleText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            _toggleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _toggle.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { chevron, _toggleText, _toggleStack } };
            _toggle.Click += (_, _) => _owner.ToggleReplies(_id);
            var main = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Children = { _meta, _deleted, _swapHeight, _toggle } };
            _swapHeight.Margin = new Thickness(0, 2, 0, 0);
            DockPanel.SetDock(_faceSlot, Dock.Left);
            _article = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(16), Background = Brushes.Transparent, Child = new DockPanel { Children = { _faceSlot, main } } };
            _article.MouseEnter += (_, _) => RefreshActionsOpacity();
            _article.MouseLeave += (_, _) => RefreshActionsOpacity();
            _article.IsKeyboardFocusWithinChanged += (_, _) => RefreshActionsOpacity();
            _repliesList = new StackPanel();
            var rail = new Rectangle { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 14) };
            rail.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Border");
            var replies = new Grid { Margin = new Thickness(24, 0, 0, 0), Children = { rail, new Border { Padding = new Thickness(12, 0, 0, 0), Child = _repliesList } } };
            _repliesBox = new SpringHeightBox(HeightSeconds) { Child = replies, Visibility = Visibility.Collapsed };
            var box = new SpringHeightBox(HeightSeconds) { Child = new StackPanel { Children = { _article, _repliesBox } } };
            Host = new CommentHost(id, box) { Child = box };
        }

        public CommentHost Host { get; }
        public string? ParentId { get; private set; }
        public string BodyText => _data.Body;
        public IReadOnlyList<string> Mentions => _mentions;
        public double ActionsOpacity => _actions?.Opacity ?? 0;
        public CommentComposer? Editor => _editor;

        public void Update(ThreadComment data, int depth, bool isNew)
        {
            _data = data;
            var labels = _owner.Labels;
            UpdateFace(data);
            AutomationProperties.SetName(_article, data.Deleted ? labels.DeletedComment : $"{data.Author.Name}, {data.CreatedAt}");
            _meta.Visibility = data.Deleted ? Visibility.Collapsed : Visibility.Visible;
            _swapHeight.Visibility = data.Deleted ? Visibility.Collapsed : Visibility.Visible;
            _deleted.Visibility = data.Deleted ? Visibility.Visible : Visibility.Collapsed;
            _deleted.Text = labels.DeletedText;
            _name.Text = data.Author.Name;
            _time.Text = data.Edited ? $"{data.CreatedAt} · {labels.EditedMark}" : data.CreatedAt;
            UpdateBody(data);
            UpdateReactions(data, labels, isNew);
            UpdateMode(isNew);
            UpdateEditing();
            UpdateReplies(data, depth, labels, isNew);
        }

        private void UpdateFace(ThreadComment data)
        {
            string key = data.Deleted ? "!" : data.Author.Id;
            if (key == _faceKey) { return; }
            _faceKey = key;
            _faceSlot.Children.Clear();
            if (data.Deleted)
            {
                var dot = new Ellipse { Width = 28, Height = 28, StrokeThickness = 1, StrokeDashArray = [3, 3], VerticalAlignment = VerticalAlignment.Top };
                dot.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.BorderStrong");
                _faceSlot.Children.Add(dot);
                return;
            }
            _faceSlot.Children.Add(ThreadUi.Face(data.Author.Name, data.Author.Avatar, 28, 12));
        }

        private void UpdateBody(ThreadComment data)
        {
            _body.Inlines.Clear();
            _mentions.Clear();
            foreach (var (text, mention) in CommentTree.Parts(data.Body, _owner.Mentionable.Select(person => person.Name)))
            {
                var run = new Run(text);
                if (mention)
                {
                    run.FontWeight = FontWeights.Medium;
                    run.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.AccentStrong");
                    _mentions.Add(text);
                }
                _body.Inlines.Add(run);
            }
        }

        private void UpdateReactions(ThreadComment data, CommentThreadLabels labels, bool isNew)
        {
            var list = data.Reactions ?? [];
            string? me = _owner.CurrentUser?.Id;
            bool animate = !isNew && _owner.Full;
            var before = animate ? Flip.Capture(_reactions) : [];
            var keep = list.Select(reaction => reaction.Emoji).ToHashSet();
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
                    if (animate) { Pop(item); }
                }
                bool pressed = me is not null && reaction.Users.Contains(me);
                var button = (ArcChromeButton)((Border)item).Child;
                button.Tag = pressed;
                button.Refresh(!isNew);
                _reactionCounts[reaction.Emoji].Set(reaction.Users.Count);
                AutomationProperties.SetName(button, labels.ReactionCount(reaction.Emoji, reaction.Users.Count, pressed));
            }
            _reactions.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (animate) { Flip.Play(_reactions, before, Tween.Snappy); }
        }

        private FrameworkElement BuildReaction(string emoji)
        {
            var count = new ThreadCount(.7, true) { VerticalAlignment = VerticalAlignment.Center };
            _reactionCounts[emoji] = count;
            var button = new ArcChromeButton(13, 1) { Height = 26, Padding = new Thickness(7, 0, 9, 0), FontSize = 12, FontWeight = FontWeights.Medium, Tag = false };
            button.Roles = target =>
            {
                bool pressed = target.Tag is true;
                return (pressed ? "AccentSubtle" : "Surface", pressed ? "CommentReactionMineEdge" : target.IsMouseOver ? "BorderStrong" : "Border", pressed ? "Foreground" : "TextSecondary");
            };
            var glyph = new ColorEmojiGlyph { Text = emoji, EmojiSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            button.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, count } };
            button.Click += (_, _) => _owner.React(_id, emoji);
            ThreadUi.PressScale(button, .94);
            return new Border { Margin = new Thickness(0, 0, 4, 0), Child = button, RenderTransform = new TranslateTransform() };
        }

        private static void Pop(FrameworkElement item)
        {
            var scale = new ScaleTransform(.7, .7);
            var inner = ((Border)item).Child;
            var group = new TransformGroup { Children = { scale } };
            if (inner.RenderTransform is ScaleTransform press) { group.Children.Add(press); }
            inner.RenderTransform = group;
            inner.RenderTransformOrigin = new Point(.5, .5);
            inner.Opacity = 0;
            Tween.Spring(inner, UIElement.OpacityProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
        }

        private void RefreshActionsOpacity()
        {
            if (_actions is null) { return; }
            bool shown = _mode != Mode.Actions || _owner.AlwaysShowActions || _article.IsMouseOver || _article.IsKeyboardFocusWithin;
            if (_owner.Moving) { Tween.To(_actions, UIElement.OpacityProperty, shown ? 1 : 0, Tween.Fast, ArcEasing.Standard); }
            else { Tween.Set(_actions, UIElement.OpacityProperty, shown ? 1 : 0); }
        }

        private void UpdateMode(bool isNew)
        {
            var next = _owner._confirming == _id ? Mode.Confirm : _owner._picking == _id ? Mode.Picker : Mode.Actions;
            if (_mode == next && !isNew) { RefreshActionsOpacity(); return; }
            bool had = _mode is not null;
            _mode = next;
            if (!had || !_owner.Moving) { Mount(next, false); return; }
            if (_owner.Full) { _swapHeight.MorphNext(); }
            // One set leaves before the next arrives.
            var old = _actions;
            _actions = null;
            old!.IsHitTestVisible = false;
            Tween.To(old, UIElement.OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, done: () => { if (_mode == next && _actions is null) { Mount(next, true); } });
        }

        private void Mount(Mode mode, bool animate)
        {
            var labels = _owner.Labels;
            FrameworkElement built = mode switch
            {
                Mode.Confirm => BuildConfirm(labels, animate),
                Mode.Picker => BuildPicker(labels, animate),
                _ => BuildActions(labels),
            };
            _actions = built;
            _actionsSlot.Child = built;
            if (mode == Mode.Actions)
            {
                bool shown = _owner.AlwaysShowActions || _article.IsMouseOver || _article.IsKeyboardFocusWithin;
                built.Opacity = animate ? 0 : shown ? 1 : 0;
                if (animate) { Tween.To(built, UIElement.OpacityProperty, shown ? 1 : 0, Tween.Instant, ArcEasing.Standard); }
                return;
            }
            built.Opacity = animate ? 0 : 1;
            if (!animate) { return; }
            var move = new TranslateTransform(_owner.Full ? (mode == Mode.Confirm ? 6 : -6) : 0, 0);
            built.RenderTransform = move;
            Tween.To(built, UIElement.OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            Tween.To(move, TranslateTransform.XProperty, 0, Tween.Fast, ArcEasing.Enter);
        }

        private static ArcChromeButton Action(string text, bool danger = false)
        {
            var button = new ArcChromeButton(14) { Height = 28, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 2, 0), FontSize = 12, FontWeight = FontWeights.Medium, Content = new TextBlock { Text = text } };
            button.Roles = target =>
            {
                bool lit = target.IsMouseOver || ThreadUi.FocusVisible(target);
                return danger ? (lit ? "CommentDangerHover" : null, null, "ArcDanger") : lit ? ("SurfaceMuted", null, "Foreground") : (null, null, "TextMuted");
            };
            button.GotKeyboardFocus += (_, _) => button.Refresh(true);
            button.LostKeyboardFocus += (_, _) => button.Refresh(true);
            return button;
        }

        private FrameworkElement BuildActions(CommentThreadLabels labels)
        {
            bool mine = _data.Author.Id == _owner.CurrentUser?.Id;
            var row = new WrapPanel { MinHeight = 28, VerticalAlignment = VerticalAlignment.Center };
            var add = IconAction("smile-plus", 15, 28);
            add.Margin = new Thickness(0, 0, 2, 0);
            AutomationProperties.SetName(add, labels.AddReaction);
            add.Click += (_, _) => { _owner._picking = _id; _owner._confirming = null; _owner.Sync(); };
            var reply = Action(labels.Reply);
            reply.Click += (_, _) => _owner.StartReply(_id, _data.Author.Name);
            row.Children.Add(add);
            row.Children.Add(reply);
            if (mine)
            {
                var edit = Action(labels.Edit);
                edit.Click += (_, _) => _owner.StartEdit(_id);
                var delete = Action(labels.Delete);
                delete.Click += (_, _) => _owner.AskDelete(_id);
                row.Children.Add(edit);
                row.Children.Add(delete);
            }
            return row;
        }

        private FrameworkElement BuildConfirm(CommentThreadLabels labels, bool focus)
        {
            var prompt = ArcText.Block(labels.DeleteConfirm, 12, 16.8);
            prompt.Margin = new Thickness(0, 0, 6, 0);
            prompt.VerticalAlignment = VerticalAlignment.Center;
            var cancel = Action(labels.Cancel);
            cancel.Click += (_, _) => { _owner._confirming = null; _owner.Sync(); };
            var delete = Action(labels.Delete, true);
            delete.Click += (_, _) => _owner.Remove(_id);
            var row = new WrapPanel { MinHeight = 28, VerticalAlignment = VerticalAlignment.Center, Children = { prompt, cancel, delete } };
            row.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; _owner._confirming = null; _owner.Sync(); } };
            if (focus) { delete.Dispatcher.BeginInvoke(() => delete.Focus(), DispatcherPriority.Input); }
            return row;
        }

        private FrameworkElement BuildPicker(CommentThreadLabels labels, bool focus)
        {
            var row = new WrapPanel { MinHeight = 28, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(row, labels.Reactions);
            string? me = _owner.CurrentUser?.Id;
            bool full = _owner.Full;
            ArcChromeButton? first = null;
            var emojis = _owner.Reactions;
            for (int i = 0; i < emojis.Count; i++)
            {
                string emoji = emojis[i];
                bool pressed = me is not null && (_data.Reactions?.FirstOrDefault(reaction => reaction.Emoji == emoji)?.Users.Contains(me) ?? false);
                var button = new ArcChromeButton(14) { Width = 28, Height = 28, Margin = new Thickness(0, 0, 2, 0), Content = new ColorEmojiGlyph { Text = emoji, EmojiSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                button.Roles = target => (pressed ? "AccentSubtle" : target.IsMouseOver ? "SurfaceMuted" : null, null, "Foreground");
                AutomationProperties.SetName(button, labels.ReactWith(emoji));
                button.Click += (_, _) => _owner.React(_id, emoji);
                ThreadUi.PressScale(button, .88);
                first ??= button;
                if (full && focus)
                {
                    var wrap = new ScaleTransform(.6, .6);
                    var content = (FrameworkElement)button.Content;
                    content.RenderTransform = wrap;
                    content.RenderTransformOrigin = new Point(.5, .5);
                    content.Opacity = 0;
                    double delay = i * ItemStagger;
                    Tween.Spring(content, UIElement.OpacityProperty, 1, Tween.Snappy, delay);
                    Tween.Spring(wrap, ScaleTransform.ScaleXProperty, 1, Tween.Snappy, delay);
                    Tween.Spring(wrap, ScaleTransform.ScaleYProperty, 1, Tween.Snappy, delay);
                }
                row.Children.Add(button);
            }
            var close = IconAction("x", 14, 28);
            AutomationProperties.SetName(close, labels.CloseReactions);
            close.Click += (_, _) => { _owner._picking = null; _owner.Sync(); };
            row.Children.Add(close);
            row.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; _owner._picking = null; _owner.Sync(); } };
            if (focus && first is not null) { first.Dispatcher.BeginInvoke(() => first.Focus(), DispatcherPriority.Input); }
            return row;
        }

        // Editing swaps the body for a composer in place: the two cross-fade while the comment's height springs between them.
        private void UpdateEditing()
        {
            bool editing = _owner._editing == _id;
            if (editing == _editingNow) { return; }
            _editingNow = editing;
            var labels = _owner.Labels;
            FrameworkElement next, old;
            if (editing)
            {
                _editor = new CommentComposer(_owner, _owner.Mentionable, _data.Body, labels.EditComment, labels.Save, labels.Cancel, true);
                _editor.Submitted += body => _owner.Edit(_id, body);
                _editor.Cancelled += () => { _owner._editing = null; _owner.Sync(); };
                _editor.SetCancellable(true);
                next = _editor;
                old = _view;
                _editor.Dispatcher.BeginInvoke(() => _editor?.FocusEnd(), DispatcherPriority.Input);
            }
            else
            {
                next = _view;
                old = _editor!;
                _editor = null;
            }
            bool moving = _owner.Moving;
            double width = _swap.ActualWidth;
            _swap.Children.Remove(old);
            _swap.Children.Add(next);
            next.BeginAnimation(UIElement.OpacityProperty, null);
            next.Opacity = 1;
            if (!moving) { return; }
            if (_owner.Full) { _swapHeight.MorphNext(); }
            old.Width = width;
            _swapExits.Children.Add(old);
            next.Opacity = 0;
            Tween.To(next, UIElement.OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
            Tween.To(old, UIElement.OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, done: () =>
            {
                _swapExits.Children.Remove(old);
                old.Width = double.NaN;
                old.BeginAnimation(UIElement.OpacityProperty, null);
                old.Opacity = 1;
            });
        }

        private void UpdateReplies(ThreadComment data, int depth, CommentThreadLabels labels, bool isNew)
        {
            var replies = data.Replies ?? [];
            bool collapsed = _owner._collapsed.TryGetValue(_id, out bool value) && value;
            bool hasReplies = replies.Count > 0;
                        _toggle.Visibility = hasReplies ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(_toggle, collapsed ? labels.ShowReplies(CommentTree.Count(replies)) : labels.HideReplies);
            _toggleText.Text = collapsed ? labels.ShowReplies(CommentTree.Count(replies)) : labels.HideReplies;
            _toggleStack.Children.Clear();
            _toggleStack.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (collapsed) { FillStack(_toggleStack, CommentTree.Authors(replies), 18); }
            double angle = collapsed ? 0 : 90;
            if (!isNew && _owner.Full) { Tween.Spring(_chevron, RotateTransform.AngleProperty, angle, Tween.Snappy); } else { Tween.Set(_chevron, RotateTransform.AngleProperty, angle); }

            _owner.Reconcile(_repliesList, replies, depth + 1, isNew);
            foreach (var reply in replies) { if (_owner._views.TryGetValue(reply.Id, out var view)) { view.ParentId = _id; } }

            bool show = hasReplies && !collapsed, was = _hasReplies && !_collapsedNow;
            bool animate = !isNew && _owner.Full;
            _hasReplies = hasReplies;
            _collapsedNow = collapsed;
            if (show == was) { return; }
            if (show)
            {
                if (_repliesBox.IsCollapsing) { _repliesBox.Cancel(); }
                _repliesBox.Visibility = Visibility.Visible;
                if (animate)
                {
                    _repliesBox.Reveal();
                    _repliesBox.Opacity = 0;
                    Tween.To(_repliesBox, UIElement.OpacityProperty, 1, Tween.Standard, ArcEasing.Enter);
                }
                else { _repliesBox.BeginAnimation(UIElement.OpacityProperty, null); _repliesBox.Opacity = 1; }
                return;
            }
            if (animate) { Tween.To(_repliesBox, UIElement.OpacityProperty, 0, Tween.Standard, ArcEasing.Enter); }
            _repliesBox.Collapse(() => _repliesBox.Visibility = Visibility.Collapsed, animate);
        }
    }
}

/// <summary>A growing text field with @mention suggestions. Ctrl + Enter sends; Escape cancels.</summary>
internal sealed class CommentComposer : Grid
{
    private readonly CommentThread _owner;
    private readonly string _initial;
    private readonly TextBox _box;
    private readonly TextBlock _hint;
    private readonly Border _edge;
    private readonly ArcChromeButton _send, _cancel;
    private readonly TextBlock _sendText, _cancelText;
    private readonly SpringHeightBox _suggestionClip;
    private readonly StackPanel _suggestions;
    private readonly Decorator _leading;
    private readonly bool _compact;
    private List<CommentAuthor> _people;
    private List<CommentAuthor> _matches = [];
    private (int Start, string Term)? _query;
    private int _active;
    private bool _open;

    public CommentComposer(CommentThread owner, List<CommentAuthor> people, string initial, string placeholder, string submit, string cancel, bool compact)
    {
        _owner = owner;
        _people = people;
        _initial = initial;
        _compact = compact;
        _box = ThreadUi.PlainTextBox();
        _box.FontSize = 14;
        _box.AcceptsReturn = true;
        _box.TextWrapping = TextWrapping.Wrap;
        _box.MaxHeight = 160 - 2;
        _box.Padding = new Thickness(12, 8, 12, 8);
        _box.Text = initial;
        TextBlock.SetLineHeight(_box, 19.6);
        TextBlock.SetLineStackingStrategy(_box, LineStackingStrategy.BlockLineHeight);
        TextColoring.SetColorEmoji(_box, true);
        _box.TextChanged += (_, _) => { ReadQuery(); Refresh(); };
        _box.SelectionChanged += (_, _) => ReadQuery();
        _box.LostKeyboardFocus += (_, _) => { SetQuery(null); RefreshEdge(); };
        _box.GotKeyboardFocus += (_, _) => RefreshEdge();
        _box.PreviewKeyDown += OnKey;
        _hint = new TextBlock { Text = placeholder, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(12, 8, 12, 8), IsHitTestVisible = false };
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        _edge = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), MinHeight = 38, Child = new Grid { Children = { _hint, _box } } };
        _edge.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        RefreshEdge();
        _suggestions = new StackPanel();
        var list = new Border { Padding = new Thickness(4), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Child = _suggestions, Margin = new Thickness(0, 6, 0, 0) };
        list.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
        list.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _suggestionClip = new SpringHeightBox(.42) { Child = list, Visibility = Visibility.Collapsed };
        var fieldWrap = new StackPanel { Children = { _edge, _suggestionClip } };

        (_send, _sendText) = ActionButton(submit, true);
        (_cancel, _cancelText) = ActionButton(cancel, false);
        _cancel.Visibility = Visibility.Collapsed;
        _cancel.Margin = new Thickness(0, 0, 4, 0);
        _send.Click += (_, _) => Send();
        _cancel.Click += (_, _) => Cancelled?.Invoke();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Children = { _cancel, _send } };
        _leading = new Decorator { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 10, 0) };
        if (compact)
        {
            Margin = new Thickness(0, 4, 0, 0);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Margin = new Thickness(0, 8, 0, 0);
            SetRow(actions, 1);
            _send.Height = _cancel.Height = 32;
            _send.FontSize = _cancel.FontSize = 12;
            Children.Add(fieldWrap);
            Children.Add(actions);
        }
        else
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.Margin = new Thickness(10, 0, 0, 0);
            SetColumn(fieldWrap, 1);
            SetColumn(actions, 2);
            Children.Add(_leading);
            Children.Add(fieldWrap);
            Children.Add(actions);
        }
        Refresh();
    }

    public event Action<string>? Submitted;
    public event Action? Cancelled;

    internal TextBox BoxForTest => _box;
    internal ArcChromeButton SendForTest => _send;
    internal bool SuggestionsOpenForTest => _open;
    internal IReadOnlyList<CommentAuthor> MatchesForTest => _matches;
    internal int ActiveForTest => _active;

    public void FocusEnd()
    {
        _box.Focus();
        _box.CaretIndex = _box.Text.Length;
    }

    public void Update(List<CommentAuthor> people, string placeholder, string submit, string cancel, string mentionsLabel, bool canCancel)
    {
        _people = people;
        _hint.Text = placeholder;
        _sendText.Text = submit;
        _cancelText.Text = cancel;
        AutomationProperties.SetName(_box, placeholder);
        AutomationProperties.SetName(_suggestions, mentionsLabel);
        SetCancellable(canCancel);
    }

    public void SetCancellable(bool value) => _cancel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;

    public void SetLeading(CommentAuthor? author)
    {
        if (_compact) { return; }
        _leading.Child = author is null ? null : ThreadUi.Face(author.Name, author.Avatar, 28, 11);
    }

    private (ArcChromeButton Button, TextBlock Text) ActionButton(string text, bool primary)
    {
        var label = new TextBlock { Text = text };
        var button = new ArcChromeButton(19) { Height = 38, Padding = new Thickness(14, 0, 14, 0), FontSize = 14, FontWeight = FontWeights.Medium, Content = label };
        button.Roles = target => primary
            ? (target.IsMouseOver && target.IsEnabled ? "CommentSendHover" : "Foreground", null, "Background")
            : (target.IsMouseOver ? "SurfaceMuted" : null, null, target.IsMouseOver ? "Foreground" : "TextSecondary");
        ThreadUi.PressScale(button, .97);
        return (button, label);
    }

    private void Refresh()
    {
        _hint.Visibility = _box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        string body = _box.Text.Trim();
        bool ready = body.Length > 0 && body != _initial.Trim();
        _send.IsEnabled = ready;
        _send.Opacity = ready ? 1 : .35;
        _send.Cursor = ready ? Cursors.Hand : Cursors.Arrow;
    }

    private void RefreshEdge() => _edge.SetResourceReference(Border.BorderBrushProperty, _box.IsKeyboardFocused ? "ArcWpf.Brush.BorderStrong" : "ArcWpf.Brush.Border");

    private void ReadQuery()
    {
        if (!_box.IsKeyboardFocused) { return; }
        SetQuery(CommentTree.Query(_box.Text, _box.CaretIndex));
    }

    private void SetQuery((int Start, string Term)? query)
    {
        bool same = query == _query;
        _query = query;
        if (!same) { _active = 0; }
        _matches = query is { } value ? CommentTree.Suggest(_people, value.Term) : [];
        bool open = _matches.Count > 0;
        RenderSuggestions();
        if (open == _open) { return; }
        _open = open;
        bool moving = IsLoaded && Tween.Level(this) != ArcMotionLevel.Off, full = IsLoaded && Tween.Level(this) == ArcMotionLevel.Full;
        if (open)
        {
            if (_suggestionClip.IsCollapsing) { _suggestionClip.Cancel(); }
            _suggestionClip.Visibility = Visibility.Visible;
            if (full) { _suggestionClip.Reveal(); }
            if (moving) { _suggestionClip.Opacity = 0; Tween.To(_suggestionClip, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter); }
            else { Tween.Set(_suggestionClip, OpacityProperty, 1); }
            return;
        }
        if (moving) { Tween.To(_suggestionClip, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard); }
        _suggestionClip.Collapse(() => _suggestionClip.Visibility = Visibility.Collapsed, full);
    }

    private void RenderSuggestions()
    {
        _suggestions.Children.Clear();
        for (int i = 0; i < _matches.Count; i++)
        {
            var person = _matches[i];
            int index = i;
            var face = ThreadUi.Face(person.Name, person.Avatar, 20, 8);
            face.Margin = new Thickness(0, 0, 8, 0);
            face.VerticalAlignment = VerticalAlignment.Center;
            var row = new Border { MinHeight = 32, Padding = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand, Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { face, ArcCenter(ArcText.Block(person.Name, 14, 19.6)) } } };
            if (index == _active) { row.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted"); } else { row.Background = Brushes.Transparent; }
            AutomationProperties.SetName(row, person.Name);
            row.MouseMove += (_, _) => { if (_active != index) { _active = index; RenderSuggestions(); } };
            // Pressing a suggestion keeps focus in the field.
            row.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; Insert(person); };
            _suggestions.Children.Add(row);
        }
    }

    private static TextBlock ArcCenter(TextBlock block) { block.VerticalAlignment = VerticalAlignment.Center; return block; }

    private void Insert(CommentAuthor person)
    {
        if (_query is not { } query) { return; }
        int caret = _box.CaretIndex;
        string text = _box.Text;
        string next = $"{text[..query.Start]}@{person.Name} {text[caret..]}";
        int at = query.Start + person.Name.Length + 2;
        _box.Text = next;
        _box.Focus();
        _box.CaretIndex = at;
        SetQuery(null);
    }

    private void Send()
    {
        string body = _box.Text.Trim();
        if (body.Length == 0) { return; }
        Submitted?.Invoke(body);
        _box.Text = string.Empty;
        SetQuery(null);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (_open)
        {
            if (e.Key is Key.Down or Key.Up)
            {
                e.Handled = true;
                _active = (_active + (e.Key == Key.Down ? 1 : -1) + _matches.Count) % _matches.Count;
                RenderSuggestions();
                return;
            }
            if (e.Key is Key.Enter or Key.Tab) { e.Handled = true; Insert(_matches[_active]); return; }
            if (e.Key == Key.Escape) { e.Handled = true; SetQuery(null); return; }
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; Send(); return; }
        if (e.Key == Key.Escape && _cancel.Visibility == Visibility.Visible) { e.Handled = true; Cancelled?.Invoke(); }
    }
}
