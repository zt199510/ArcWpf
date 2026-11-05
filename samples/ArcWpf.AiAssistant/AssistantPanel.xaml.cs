using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;
using Button = ArcWpf.Controls.Button;

namespace ArcWpf.AiAssistant;

/// <summary>
/// The assistant: chats on the left, the thread and the composer in the middle, results and files on the right. It holds no window state, so it can sit
/// in any host; below 1120 wide the results float over the thread and below 820 wide the chats do too.
/// </summary>
public partial class AssistantPanel : UserControl
{
    private const double PaneWidth = 420, InlinePaneFrom = 1120, InlineSideFrom = 820;

    private readonly Dictionary<string, Conversation> _chats = [];
    private readonly HashSet<PromptAttachment> _failOnce = [];
    private IAssistantBackend _backend = new SimulatedBackend();
    private Conversation _chat = null!;
    private bool _sideOpen, _paneWasInline;
    private int _nextChat;

    public AssistantPanel()
    {
        InitializeComponent();
        Composer.Submitted += OnSubmitted;
        Composer.StopRequested += (_, _) => _chat.Last?.Stop();
        Composer.AttachRequested += OnAttachRequested;
        Composer.AttachmentAdded += (_, e) => Track(e.Attachment);
        Composer.AttachmentRemoved += (_, e) => Forget(e.Attachment);

        Files.Removed += (_, e) => { Composer.Attachments.Remove(e.Item); Forget(e.Item); };
        Files.RetryRequested += (_, e) => Upload(e.Item, _chat);

        Sidebar.NewRequested += (_, _) => NewChat();
        Sidebar.Selected += (_, e) => { Show(_chats[e.Item.Id]); _sideOpen = false; Relayout(); };
        Sidebar.Renamed += (_, e) => { if (e.Item == _chat.Item) { Title.Text = e.Item.Title; } };
        Sidebar.Archived += (_, e) => { if (e.Item == _chat.Item) { ShowNextAfterArchive(); } };

        Apply();
    }

    /// <summary>Replaces the scripted replies with a real model.</summary>
    public IAssistantBackend Backend
    {
        get => _backend;
        set
        {
            _backend = value ?? throw new ArgumentNullException(nameof(value));
            Apply();
        }
    }

    // Everything on screen comes from the backend's profile, so changing backend starts over.
    private void Apply()
    {
        var profile = _backend.Profile;
        Composer.Commands = [.. profile.Commands];
        Composer.Mentions = [.. profile.Mentions];
        Composer.AttachActions = [.. profile.AttachActions];
        Composer.Tools = [.. profile.Tools];
        Composer.Models = [.. profile.Models];
        Composer.Providers = [.. profile.Providers];
        Composer.FeaturedModels = [.. profile.FeaturedModels];
        Composer.Model = profile.Models.FirstOrDefault()?.Id;
        BadgeText.Text = profile.Badge;
        Badge.ToolTip = profile.BadgeHint;
        WelcomeText.Text = profile.Welcome;

        Starters.Items.Clear();
        foreach (var (label, prompt) in profile.Starters)
        {
            var starter = new Button { Content = label, Variant = ButtonVariant.Secondary, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            starter.Click += (_, _) => Composer.SetPrompt(prompt);
            Starters.Items.Add(starter);
        }

        _chat = null!;
        _chats.Clear();
        Sidebar.Conversations.Clear();
        foreach (var past in profile.History)
        {
            var chat = Open(new ConversationItem(past.Id, past.Title, past.Group) { IsArchived = past.Archived });
            chat.Thread.Children.Add(TurnView.Settled(past.Question, past.Answer));
            Sidebar.Conversations.Add(chat.Item);
        }
        NewChat();
    }

    private Conversation Open(ConversationItem item)
    {
        var backend = _backend;
        var chat = new Conversation(item, new Lazy<IAssistantSession>(backend.CreateSession));
        chat.Thread.MaxWidth = 760;
        chat.Thread.Margin = new Thickness(24, 8, 24, 8);
        return _chats[item.Id] = chat;
    }

    private void NewChat()
    {
        if (_chat is { IsEmpty: true }) { Composer.Focus(); return; }
        var chat = Open(new ConversationItem($"chat-{++_nextChat}", "New chat", "Today"));
        Sidebar.Conversations.Insert(0, chat.Item);
        Show(chat);
        Composer.Focus();
    }

    private void Show(Conversation chat)
    {
        if (chat == _chat) { return; }
        if (_chat is not null)
        {
            _chat.Draft = Composer.Text ?? "";
            _chat.DraftFiles.Clear();
            _chat.DraftFiles.AddRange(Composer.Attachments);
        }
        _chat = chat;
        Sidebar.SelectedId = chat.Item.Id;
        Title.Text = chat.Item.Title;
        Scroller.Content = chat.Thread;
        Welcome.Visibility = chat.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        if (Composer.Text != chat.Draft) { Composer.SetPrompt(chat.Draft); }
        Composer.Attachments.Clear();
        foreach (var file in chat.DraftFiles) { Composer.Attachments.Add(file); }
        Results.Items.Clear();
        foreach (var item in chat.Artifacts) { Results.Items.Add(item); }
        Files.Items.Clear();
        foreach (var file in chat.Files) { Files.Items.Add(file); }
        Sync();
        Relayout();
        if (chat.Last is { } last) { Dispatcher.BeginInvoke(() => last.BringIntoView(new Rect(0, Math.Max(0, last.ActualHeight - 1), 1, 1)), DispatcherPriority.Loaded); }
    }

    private void ShowNextAfterArchive()
    {
        var next = Sidebar.Conversations.FirstOrDefault(item => !item.IsArchived);
        if (next is null) { _chat = null!; NewChat(); return; }
        Show(_chats[next.Id]);
    }

    private void OnSubmitted(object? sender, PromptMessageEventArgs e)
    {
        var chat = _chat;
        var message = e.Message;
        var model = _backend.Profile.Models.FirstOrDefault(item => item.Id == message.Model);
        var tools = message.Tools;
        var request = new AssistantRequest(message.Text, [.. message.Attachments.Select(item => new AssistantFile(item.Name, item.FilePath, item.PastedText))], message.Model, model?.Name, model?.Mark, tools);

        // Send leaves a failed file behind, so it should not stay listed as part of the chat.
        foreach (var file in chat.Files.Where(file => file.Status == PromptAttachmentStatus.Error).ToList()) { Forget(file); }

        if (chat.IsEmpty)
        {
            string title = message.Text.Length > 0 ? message.Text : message.Attachments[0].Name;
            chat.Item.Title = title.Length > 40 ? title[..40].TrimEnd() + "…" : title;
            Title.Text = chat.Item.Title;
        }
        if (chat.Last is { } previous) { previous.CanRegenerate = false; }

        var turn = new TurnView(chat.Session, request);
        turn.StateChanged += (_, _) => { if (_chat == chat) { Sync(); } };
        turn.ArtifactAdded += (_, item) =>
        {
            chat.Artifacts.Add(item);
            if (_chat != chat) { return; }
            Results.Items.Add(item);
            Results.SelectedIndex = 0;
            if (ActualWidth >= InlinePaneFrom) { chat.PaneOpen = true; }
            Relayout();
        };
        turn.ArtifactRemoved += (_, item) =>
        {
            chat.Artifacts.Remove(item);
            if (_chat == chat) { Results.Items.Remove(item); Relayout(); }
        };
        turn.QuoteRequested += (_, quote) => Composer.SetPrompt($"> {quote.Replace("\n", "\n> ")}\n\n");
        chat.Thread.Children.Add(turn);
        Welcome.Visibility = Visibility.Collapsed;
        turn.Start();
        Sync();
    }

    private void Sync()
    {
        var last = _chat.Last;
        Composer.IsGenerating = last?.IsBusy == true;
        Composer.Progress = last?.Progress;
    }

    private void OnAttachRequested(object? sender, PromptAttachEventArgs e)
    {
        if (e.ActionId == "upload") { return; }
        e.Handled = true;
        var file = e.ActionId == "flaky"
            ? new PromptAttachment($"flaky-export-{_failOnce.Count + 1}.csv") { Detail = "Sample file, 2.1 MB" }
            : new PromptAttachment($"deploy-{Composer.Attachments.Count + 1}.log") { Detail = "Sample file, 48 KB" };
        if (e.ActionId == "flaky") { _failOnce.Add(file); }
        Composer.Attachments.Add(file);
        Track(file);
    }

    private void Track(PromptAttachment file)
    {
        _chat.Files.Add(file);
        Files.Items.Add(file);
        if (ActualWidth >= InlinePaneFrom) { _chat.PaneOpen = true; }
        Relayout();
        // A real backend reads the file when the message goes, so only the script pretends to upload.
        if (file.PastedText is null && _backend.Profile.IsSimulated) { Upload(file, _chat); }
    }

    private void Forget(PromptAttachment file)
    {
        foreach (var chat in _chats.Values) { chat.Files.Remove(file); }
        Files.Items.Remove(file);
        Relayout();
    }

    // Stands in for a real upload: the host owns it and reports through Status and Progress.
    private void Upload(PromptAttachment file, Conversation chat)
    {
        file.Error = null;
        file.Progress = 0;
        file.Status = PromptAttachmentStatus.Uploading;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) =>
        {
            if (!chat.Files.Contains(file)) { timer.Stop(); return; }
            file.Progress = Math.Min(1, file.Progress + 0.12);
            if (file.Progress >= 0.5 && _failOnce.Remove(file))
            {
                timer.Stop();
                file.Error = "Connection lost. This is a simulated failure.";
                file.Status = PromptAttachmentStatus.Error;
            }
            else if (file.Progress >= 1)
            {
                timer.Stop();
                file.Status = PromptAttachmentStatus.Done;
            }
        };
        timer.Start();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void ToggleSide(object sender, RoutedEventArgs e) { _sideOpen = !_sideOpen; if (_sideOpen && ActualWidth < InlinePaneFrom) { _chat.PaneOpen = false; } Relayout(); }

    private void TogglePane(object sender, RoutedEventArgs e) { _chat.PaneOpen = !_chat.PaneOpen; if (_chat.PaneOpen) { _sideOpen = false; } Relayout(); }

    private void CloseOverlays(object sender, MouseButtonEventArgs e) { _sideOpen = false; _chat.PaneOpen = false; Relayout(); }

    private void ClosePane(object sender, RoutedEventArgs e) { _chat.PaneOpen = false; Relayout(); }

    private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
    {
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = e.Theme == ThemeSwitchTheme.Dark ? ArcThemeMode.Dark : ArcThemeMode.Light;
    }

    private void Relayout()
    {
        if (_chat is null) { return; }
        double width = ActualWidth;
        bool sideInline = width >= InlineSideFrom, paneInline = width >= InlinePaneFrom;

        SideColumn.Width = new GridLength(sideInline ? 280 : 0);
        SideToggle.Visibility = sideInline ? Visibility.Collapsed : Visibility.Visible;
        Side.Width = 280;
        Side.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumnSpan(Side, sideInline ? 1 : 3);
        Side.Visibility = sideInline || _sideOpen ? Visibility.Visible : Visibility.Collapsed;

        // A pane that fit beside the thread should not end up on top of it just because the window shrank.
        if (_paneWasInline && !paneInline) { _chat.PaneOpen = false; }
        _paneWasInline = paneInline;

        bool pane = _chat.PaneOpen;
        PaneColumn.Width = new GridLength(pane && paneInline ? PaneWidth : 0);
        Pane.Visibility = pane ? Visibility.Visible : Visibility.Collapsed;
        Pane.Width = paneInline ? double.NaN : Math.Min(PaneWidth, Math.Max(width - 40, 0));
        Pane.HorizontalAlignment = paneInline ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        Grid.SetColumn(Pane, paneInline ? 2 : 0);
        Grid.SetColumnSpan(Pane, paneInline ? 1 : 3);
        Results.Width = Math.Max(0, (paneInline ? PaneWidth : Pane.Width) - 34);

        Grid.SetColumn(Scrim, sideInline ? 1 : 0);
        Grid.SetColumnSpan(Scrim, sideInline ? 2 : 3);
        Scrim.Visibility = (!sideInline && _sideOpen) || (!paneInline && pane) ? Visibility.Visible : Visibility.Collapsed;
        FilesSection.Visibility = _chat.Files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        int count = _chat.Artifacts.Count;
        PaneCount.Text = count.ToString();
        PaneCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(PaneToggle, pane ? "Hide results" : "Show results");
    }
}
