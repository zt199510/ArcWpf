using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace ArcWpf.Controls;

public sealed record PromptCommand(string Id, string Label, string? Hint = null, string? Glyph = null);
public sealed record PromptMention(string Id, string Label, string? Detail = null, string? Glyph = null);
public sealed record PromptMentionGroup(string Title, IReadOnlyList<PromptMention> Items);
public sealed record PromptAttachAction(string Id, string Label, string Glyph);

public enum PromptTokenKind { Command, Mention }
public sealed record PromptToken(PromptTokenKind Kind, string Id, string Text, int Start)
{
    public int End => Start + Text.Length;
}

public enum PromptAttachmentStatus { Uploading, Done, Error }

/// <summary>One file or pasted text on the composer. The host owns the upload and reports through Status and Progress.</summary>
public sealed class PromptAttachment(string name) : INotifyPropertyChanged
{
    private PromptAttachmentStatus _status = PromptAttachmentStatus.Done;
    private double _progress = 1;
    private string? _error;

    public string Name { get; } = name;
    public string? Detail { get; init; }
    public string? FilePath { get; init; }
    public string? PastedText { get; init; }
    /// <summary>A thumbnail to show. Without one, an image file at <see cref="FilePath"/> is loaded instead.</summary>
    public System.Windows.Media.ImageSource? Preview { get; init; }
    public PromptAttachmentStatus Status { get => _status; set { if (_status != value) { _status = value; Raise(nameof(Status)); } } }
    /// <summary>0 to 1 while uploading.</summary>
    public double Progress { get => _progress; set { if (_progress != value) { _progress = value; Raise(nameof(Progress)); } } }
    /// <summary>The short reason a failed file shows.</summary>
    public string? Error { get => _error; set { if (_error != value) { _error = value; Raise(nameof(Error)); } } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A toggle in the toolbar, such as Web search.</summary>
public sealed class PromptTool(string id, string label, string glyph) : INotifyPropertyChanged
{
    private bool _isOn;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public string Glyph { get; } = glyph;
    public bool IsOn { get => _isOn; set { if (_isOn != value) { _isOn = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOn))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record PromptMessage(string Text, IReadOnlyList<PromptToken> Tokens, IReadOnlyList<PromptAttachment> Attachments, string? Model, ModelEffort? Effort, IReadOnlyList<string> Tools);
public sealed class PromptMessageEventArgs(PromptMessage message) : EventArgs { public PromptMessage Message { get; } = message; }
public sealed class PromptAttachEventArgs(string actionId) : EventArgs
{
    public string ActionId { get; } = actionId;
    /// <summary>Set it to take over the action. Upload file falls back to a file dialog when nothing handles it.</summary>
    public bool Handled { get; set; }
}
public sealed class PromptAttachmentEventArgs(PromptAttachment attachment) : EventArgs { public PromptAttachment Attachment { get; } = attachment; }

/// <summary>
/// The prompt box of an AI app: an autosizing field with atomic command and mention tokens, attachments, tool toggles, a model chip,
/// dictation and a send button that turns into a stop control ringed by the reply's progress.
/// </summary>
public class PromptInput : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(PromptInput), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PromptInput)d).OnTextProperty()));
    public static readonly DependencyProperty PlaceholderProperty = Prop(nameof(Placeholder), typeof(string), "Ask anything, / for commands, @ to add context");
    public static readonly DependencyProperty MaxLengthProperty = Prop(nameof(MaxLength), typeof(int), 4000);
    public static readonly DependencyProperty LongPasteLengthProperty = Prop(nameof(LongPasteLength), typeof(int), 2000);
    public static readonly DependencyProperty CommandsProperty = Prop(nameof(Commands), typeof(IReadOnlyList<PromptCommand>), null);
    public static readonly DependencyProperty MentionsProperty = Prop(nameof(Mentions), typeof(IReadOnlyList<PromptMentionGroup>), null);
    public static readonly DependencyProperty AttachActionsProperty = Prop(nameof(AttachActions), typeof(IReadOnlyList<PromptAttachAction>), null);
    public static readonly DependencyProperty ToolsProperty = DependencyProperty.Register(nameof(Tools), typeof(IReadOnlyList<PromptTool>), typeof(PromptInput), new FrameworkPropertyMetadata(null, (d, _) => ((PromptInput)d).BuildTools()));
    public static readonly DependencyProperty ModelsProperty = Prop(nameof(Models), typeof(IReadOnlyList<PickerModel>), null);
    public static readonly DependencyProperty ProvidersProperty = Prop(nameof(Providers), typeof(IReadOnlyList<PickerProvider>), null);
    public static readonly DependencyProperty FeaturedModelsProperty = Prop(nameof(FeaturedModels), typeof(IReadOnlyList<string>), null);
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(string), typeof(PromptInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty EffortProperty = DependencyProperty.Register(nameof(Effort), typeof(ModelEffort?), typeof(PromptInput), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty IsGeneratingProperty = Prop(nameof(IsGenerating), typeof(bool), false);
    public static readonly DependencyProperty ProgressProperty = Prop(nameof(Progress), typeof(double?), null);
    public static readonly DependencyProperty ErrorTextProperty = Prop(nameof(ErrorText), typeof(string), null);
    public static readonly DependencyProperty ShowDictationProperty = Prop(nameof(ShowDictation), typeof(bool), true);
    public static readonly DependencyProperty IsDictatingProperty = Prop(nameof(IsDictating), typeof(bool), false);

    private static DependencyProperty Prop(string name, Type type, object? fallback) => DependencyProperty.Register(name, type, typeof(PromptInput), new FrameworkPropertyMetadata(fallback, (d, _) => ((PromptInput)d).Refresh()));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    /// <summary>A ring counter appears at 80 percent, turns to the warning color at 95 percent, and sending stops above the limit.</summary>
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    /// <summary>A paste at least this long becomes an attachment instead of filling the field.</summary>
    public int LongPasteLength { get => (int)GetValue(LongPasteLengthProperty); set => SetValue(LongPasteLengthProperty, value); }
    public IReadOnlyList<PromptCommand>? Commands { get => (IReadOnlyList<PromptCommand>?)GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }
    public IReadOnlyList<PromptMentionGroup>? Mentions { get => (IReadOnlyList<PromptMentionGroup>?)GetValue(MentionsProperty); set => SetValue(MentionsProperty, value); }
    /// <summary>The plus menu. Id "upload" opens a file dialog unless AttachRequested handles it.</summary>
    public IReadOnlyList<PromptAttachAction>? AttachActions { get => (IReadOnlyList<PromptAttachAction>?)GetValue(AttachActionsProperty); set => SetValue(AttachActionsProperty, value); }
    public IReadOnlyList<PromptTool>? Tools { get => (IReadOnlyList<PromptTool>?)GetValue(ToolsProperty); set => SetValue(ToolsProperty, value); }
    public IReadOnlyList<PickerModel>? Models { get => (IReadOnlyList<PickerModel>?)GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }
    public IReadOnlyList<PickerProvider>? Providers { get => (IReadOnlyList<PickerProvider>?)GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }
    public IReadOnlyList<string>? FeaturedModels { get => (IReadOnlyList<string>?)GetValue(FeaturedModelsProperty); set => SetValue(FeaturedModelsProperty, value); }
    public string? Model { get => (string?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public ModelEffort? Effort { get => (ModelEffort?)GetValue(EffortProperty); set => SetValue(EffortProperty, value); }
    /// <summary>A reply is running: the send button is the stop control, and Escape stops.</summary>
    public bool IsGenerating { get => (bool)GetValue(IsGeneratingProperty); set => SetValue(IsGeneratingProperty, value); }
    /// <summary>How far the reply has come, 0 to 1, shown as an arc around the stop control.</summary>
    public double? Progress { get => (double?)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public string? ErrorText { get => (string?)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public bool ShowDictation { get => (bool)GetValue(ShowDictationProperty); set => SetValue(ShowDictationProperty, value); }
    public bool IsDictating { get => (bool)GetValue(IsDictatingProperty); set => SetValue(IsDictatingProperty, value); }
    public ObservableCollection<PromptAttachment> Attachments { get; } = [];
    public IReadOnlyList<PromptToken> Tokens => _tokens;
    /// <summary>The display name of the model the chip shows.</summary>
    public string? PickedName => _picker.Chosen?.Name;

    public event EventHandler<PromptMessageEventArgs>? Submitted;
    public event EventHandler? StopRequested;
    public event EventHandler<PromptAttachEventArgs>? AttachRequested;
    /// <summary>A file or long paste joined the composer. Set its Status to Uploading here to run an upload.</summary>
    public event EventHandler<PromptAttachmentEventArgs>? AttachmentAdded;
    /// <summary>A chip's remove or cancel control was used; a running upload should be cancelled.</summary>
    public event EventHandler<PromptAttachmentEventArgs>? AttachmentRemoved;
    public event EventHandler? DictationRequested;

    private const double Gutter = 24, LineHeight = 24, FieldInsetY = 8, MaxLines = 8, ListWidth = 298, ListMaxHeight = 320;
    private enum ListKind { None, Command, Mention }

    private readonly List<PromptToken> _tokens = [];
    private TextBox _text = null!;
    private readonly TextBlock _placeholder = AiUi.Line("", 16, LineHeight, "TextMuted");
    private readonly PromptTokenLayer _layer;
    private readonly SpringHeightBox _fieldClip = new(.2);
    private readonly SpringHeightBox _trayClip = new(.2) { Visibility = Visibility.Collapsed };
    private readonly StackPanel _tray = new() { Orientation = Orientation.Horizontal };
    private readonly Border _surface = new() { CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), Padding = new Thickness(11) };
    private readonly StackPanel _toolRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 0) };
    private readonly ModelPicker _picker = new() { IsCompact = true, ShowSummary = false, OpensUpward = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly SpringHeightBox _errorClip = new(.2);
    private readonly StackPanel _errorRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 8, 14, 0) };
    private readonly TextBlock _errorLine = AiUi.Wrapped("", 12, 16.8, "TextSecondary");
    private readonly StackPanel _counter = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly AiRing _counterRing = new(16, 1.75);
    private readonly TextBlock _counterText = AiUi.Line("", 12, 16.8, "TextMuted", false, false);
    private readonly AiRing _replyRing = new(44, 2) { IsHitTestVisible = false, Opacity = 0, Margin = new Thickness(-4), Role = "Accent" };
    private ToolbarButton _send = null!, _plus = null!, _mic = null!;
    private readonly Grid _sendFace = new() { Width = 16, Height = 16 };
    private FrameworkElement? _sendGlyph;
    private bool? _sendIsStop;

    private readonly Popup _listPopup = new() { AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Custom };
    private readonly Border _listSurface = new() { Width = ListWidth, CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Margin = new Thickness(Gutter) };
    private readonly StackPanel _listRows = new() { Margin = new Thickness(6) };
    private readonly ScrollViewer _listScroll = new() { MaxHeight = ListMaxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false };
    private readonly List<ListRow> _rows = [];
    private ListKind _list;
    private int _selected;
    private int _triggerAt, _dismissedAt = -1;
    private Point? _pointerAtOpen;

    private readonly Popup _plusPopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Custom };
    private readonly Border _plusSurface = new() { Width = 252, CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Margin = new Thickness(Gutter) };
    private readonly StackPanel _plusRows = new() { Margin = new Thickness(6) };

    private string _lastText = "";
    private int _lastCaret;
    private bool _built, _changing, _syncing;
    private Window? _window;

    private sealed record ListRow(ToolbarButton View, object Item, Border Surface);

    static PromptInput() => DefaultStyleKeyProperty.OverrideMetadata(typeof(PromptInput), new FrameworkPropertyMetadata(typeof(PromptInput)));

    public PromptInput()
    {
        _layer = new PromptTokenLayer(this);
        foreach (var surface in new[] { _listSurface, _plusSurface })
        {
            surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceRaised");
            surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            surface.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 36, ShadowDepth = 14, Direction = 270, Opacity = .11, Color = Colors.Black };
        }
        _listScroll.Content = _listRows;
        _listSurface.Child = _listScroll;
        _listPopup.Child = _listSurface;
        _listPopup.CustomPopupPlacementCallback = PlaceList;
        _plusSurface.Child = _plusRows;
        _plusPopup.Child = _plusSurface;
        _plusPopup.CustomPopupPlacementCallback = PlacePlus;
        _plusPopup.Opened += (_, _) => _plus.IsHitTestVisible = false;
        _plusPopup.Closed += (_, _) => Dispatcher.BeginInvoke(() => _plus.IsHitTestVisible = true, System.Windows.Threading.DispatcherPriority.Input);
        _plusRows.PreviewKeyDown += OnPlusKey;
        Attachments.CollectionChanged += OnAttachments;
        Loaded += (_, _) => Watch(true);
        Unloaded += (_, _) => { Watch(false); CloseList(); _plusPopup.IsOpen = false; };
        AllowDrop = true;
    }

    public new bool Focus() => _text?.Focus() ?? base.Focus();

    /// <summary>Fills the field from a starter prompt; known commands and mentions in it become tokens.</summary>
    public void SetPrompt(string prompt)
    {
        _changing = true;
        try
        {
            _tokens.Clear();
            _text.Text = prompt;
            foreach (var command in Commands ?? [])
            {
                string word = "/" + command.Label.TrimStart('/');
                for (int at = prompt.IndexOf(word, StringComparison.Ordinal); at >= 0; at = prompt.IndexOf(word, at + 1, StringComparison.Ordinal))
                {
                    if (at == 0 || prompt[at - 1] == '\n') { _tokens.Add(new PromptToken(PromptTokenKind.Command, command.Id, word, at)); }
                }
            }
            foreach (var mention in (Mentions ?? []).SelectMany(group => group.Items))
            {
                string word = "@" + mention.Label;
                for (int at = prompt.IndexOf(word, StringComparison.Ordinal); at >= 0; at = prompt.IndexOf(word, at + 1, StringComparison.Ordinal))
                {
                    if (at == 0 || char.IsWhiteSpace(prompt[at - 1])) { _tokens.Add(new PromptToken(PromptTokenKind.Mention, mention.Id, word, at)); }
                }
            }
            _text.CaretIndex = prompt.Length;
        }
        finally { _changing = false; }
        _lastText = _text.Text;
        _lastCaret = _text.CaretIndex;
        AfterEdit();
        _text.Focus();
    }

    /// <summary>Adds files as attachments, as a drop does.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (Attachments.Any(item => item.FilePath == path)) { continue; }
            Add(new PromptAttachment(Path.GetFileName(path)) { FilePath = path, Detail = Size(path) });
        }
    }

    private static string? Size(string path)
    {
        try
        {
            long bytes = new FileInfo(path).Length;
            return bytes >= 1 << 20 ? (bytes / (double)(1 << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : Math.Max(1, bytes >> 10) + " KB";
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void Add(PromptAttachment attachment)
    {
        AttachmentAdded?.Invoke(this, new PromptAttachmentEventArgs(attachment));
        Attachments.Add(attachment);
    }

    private int Length => _text?.Text.Length ?? 0;
    private bool Over => Length > MaxLength;
    private bool Uploading => Attachments.Any(item => item.Status == PromptAttachmentStatus.Uploading);
    private bool HasContent => (_text?.Text.Trim().Length ?? 0) > 0 || Attachments.Any(item => item.Status == PromptAttachmentStatus.Done);
    private bool CanSend => !IsGenerating && HasContent && !Over && !Uploading;

    internal TextBox TextBoxForTest => _text;
    internal bool ListOpenForTest => _listPopup.IsOpen;
    internal string ListKindForTest => _list.ToString();
    internal string[] ListLabelsForTest => [.. _rows.Select(row => AutomationProperties.GetName(row.View))];
    internal int SelectedForTest => _selected;
    internal bool KeyForTest(Key key, bool shift = false) => HandleKey(key, shift);
    internal void SuggestForTest() => Suggest();
    internal System.Windows.Controls.Button SendForTest => _send;
    internal bool CanSendForTest => CanSend;
    internal string SendNameForTest => AutomationProperties.GetName(_send);
    internal bool SendIsStopForTest => _sendIsStop == true;
    internal bool ErrorShownForTest => _errorRow.Visibility == Visibility.Visible;
    internal string ErrorForTest => _errorLine.Text;
    internal bool CounterShownForTest => _counter.Visibility == Visibility.Visible;
    internal string CounterRoleForTest => _counterRing.Role;
    internal string CounterTextForTest => _counterText.Text;
    internal bool ReplyRingShownForTest => _replyRing.Opacity > 0;
    internal double ReplyRingValueForTest => _replyRing.Value;
    internal int ChipCountForTest => _tray.Children.Count;
    internal Border SurfaceForTest => _surface;
    internal System.Windows.Controls.Button PlusForTest => _plus;
    internal bool PlusOpenForTest => _plusPopup.IsOpen;
    internal string[] PlusLabelsForTest => [.. _plusRows.Children.OfType<ToolbarButton>().Select(AutomationProperties.GetName)];
    internal void ClickPlusRowForTest(string label) => _plusRows.Children.OfType<ToolbarButton>().First(row => AutomationProperties.GetName(row) == label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal void ClickListRowForTest(int index) => _rows[index].View.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal Border ListSurfaceForTest => _listSurface;
    internal Border PlusSurfaceForTest => _plusSurface;
    internal FrameworkElement ListRowForTest(int index) => _rows[index].View;
    internal ToolbarButton[] ToolButtonsForTest => [.. _toolRow.Children.OfType<ToolbarButton>()];
    internal ModelPicker PickerForTest => _picker;
    internal double FieldHeightForTest => _fieldClip.ActualHeight;
    internal void PasteForTest(string text) => Paste(text);
    internal System.Windows.Controls.Button MicForTest => _mic;
    internal System.Windows.Controls.Button? RemoveForTest(int index) => ((_tray.Children[index] as AttachmentChip)!).RemoveButton;
    internal string ChipStateForTest(int index) => ((_tray.Children[index] as AttachmentChip)!).StateForTest;
    internal Brush TokenBrush => new SolidColorBrush(OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), .12));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host || _built) { return; }
        _text = ThreadUi.PlainTextBox();
        _text.FontSize = 16;
        _text.AcceptsReturn = true;
        _text.TextWrapping = TextWrapping.Wrap;
        _text.Padding = new Thickness(8, FieldInsetY, 8, FieldInsetY);
        _text.MaxHeight = LineHeight * MaxLines + FieldInsetY * 2;
        _text.MinHeight = 40;
        _text.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        TextBlock.SetLineHeight(_text, LineHeight);
        TextBlock.SetLineStackingStrategy(_text, LineStackingStrategy.BlockLineHeight);
        _text.TextChanged += OnTextChanged;
        _text.SelectionChanged += OnSelection;
        _text.PreviewKeyDown += OnFieldKey;
        _text.LostKeyboardFocus += (_, _) => { CloseList(); RefreshEdge(); };
        _text.GotKeyboardFocus += (_, _) => RefreshEdge();
        _text.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => _layer.InvalidateVisual()));
        DataObject.AddPastingHandler(_text, OnPasting);
        _placeholder.IsHitTestVisible = false;
        _placeholder.VerticalAlignment = VerticalAlignment.Top;
        _placeholder.Margin = new Thickness(8, FieldInsetY, 8, 0);
        var field = new Grid { Cursor = Cursors.IBeam };
        field.Children.Add(_layer);
        field.Children.Add(_placeholder);
        field.Children.Add(_text);
        _fieldClip.Child = field;

        var tray = new ScrollViewer { Content = _tray, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Margin = new Thickness(0, 0, 0, 8) };
        _trayClip.Child = tray;

        _plus = AiUi.Icon(Lucide.Plus, 36, 20, "Add files and more");
        ChromeCircle(_plus);
        _plus.Click += (_, _) => OpenPlus();

        _mic = AiUi.Icon(Lucide.Mic, 36, 20, "Dictate");
        ChromeCircle(_mic);
        _mic.Roles = self => (IsDictating ? "SurfaceMuted" : self.IsMouseOver ? "SurfaceMuted" : null, null, IsDictating || self.IsMouseOver ? "Foreground" : "TextSecondary");
        _mic.Click += (_, _) => DictationRequested?.Invoke(this, EventArgs.Empty);

        _send = new ToolbarButton(18) { Width = 36, Height = 36, Padding = new Thickness(0), Content = _sendFace, ClipToBounds = false };
        _send.Roles = _ => IsGenerating ? ("SurfaceMuted", null, "Foreground") : CanSend ? ("Accent", null, "AccentForeground") : ("SurfaceMuted", null, "TextMuted");
        _send.Click += (_, _) => { if (IsGenerating) { StopRequested?.Invoke(this, EventArgs.Empty); } else { Send(); } };
        var sendHost = new Grid { Width = 36, Height = 36, Margin = new Thickness(4, 0, 0, 0) };
        sendHost.Children.Add(_replyRing);
        sendHost.Children.Add(_send);
        ThreadUi.PressScale(_send, .92);

        _picker.SetBinding(ModelPicker.ValueProperty, new System.Windows.Data.Binding(nameof(Model)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
        _picker.SetBinding(ModelPicker.EffortProperty, new System.Windows.Data.Binding(nameof(Effort)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
        _picker.SetBinding(ModelPicker.ModelsProperty, new System.Windows.Data.Binding(nameof(Models)) { Source = this });
        _picker.SetBinding(ModelPicker.ProvidersProperty, new System.Windows.Data.Binding(nameof(Providers)) { Source = this });
        _picker.SetBinding(ModelPicker.FeaturedProperty, new System.Windows.Data.Binding(nameof(FeaturedModels)) { Source = this });

        _counter.Children.Add(_counterRing);
        _counterText.Margin = new Thickness(5, 0, 0, 0);
        _counter.Children.Add(_counterText);

        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(_plus);
        left.Children.Add(_toolRow);
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(_counter);
        right.Children.Add(_picker);
        right.Children.Add(_mic);
        right.Children.Add(sendHost);
        var toolbar = new Grid { MinHeight = 36, Margin = new Thickness(0, 4, 0, 0) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(left);
        Grid.SetColumn(right, 1);
        toolbar.Children.Add(right);

        var column = new StackPanel();
        column.Children.Add(_trayClip);
        column.Children.Add(_fieldClip);
        column.Children.Add(toolbar);
        _surface.Child = column;
        _surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");

        var alert = AiUi.Glyph(Lucide.CircleAlert, 14, "Danger");
        alert.Margin = new Thickness(0, 1, 6, 0);
        alert.VerticalAlignment = VerticalAlignment.Top;
        _errorRow.Children.Add(alert);
        _errorRow.Children.Add(_errorLine);
        _errorRow.Visibility = Visibility.Collapsed;
        _errorClip.Child = _errorRow;

        var outer = new StackPanel();
        outer.Children.Add(_surface);
        outer.Children.Add(_errorClip);
        host.Child = outer;
        _listPopup.PlacementTarget = _fieldClip;
        _plusPopup.PlacementTarget = _plus;
        SizeChanged += (_, e) => { if (e.WidthChanged) { Fold(); } };
        ThemeWatch.Add(this, () => { _layer.InvalidateVisual(); RefreshEdge(); });
        _built = true;
        _text.Text = Text ?? "";
        _lastText = _text.Text;
        BuildTools();
        foreach (var attachment in Attachments) { AddChip(attachment, false); }
        Refresh();
        Fold();
    }

    private static void ChromeCircle(ToolbarButton button) => button.Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, self.IsMouseOver ? "Foreground" : "TextSecondary");

    private void Watch(bool on)
    {
        if (_window is not null) { _window.Deactivated -= OnWindowMoved; _window.LocationChanged -= OnWindowMoved; _window = null; }
        if (!on || Window.GetWindow(this) is not { } window) { return; }
        _window = window;
        window.Deactivated += OnWindowMoved;
        window.LocationChanged += OnWindowMoved;
    }

    private void OnWindowMoved(object? sender, EventArgs e) => CloseList();

    // A container: below 680 the tool chips fold into icons, below 560 the effort leaves the model chip, below 400 the chip keeps only its mark.
    private void Fold()
    {
        double width = ActualWidth;
        if (width <= 0) { return; }
        _picker.FoldLevel = width < 400 ? 2 : width < 560 ? 1 : 0;
        _counterText.Visibility = width < 400 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in _toolRow.Children.OfType<ToolbarButton>()) { FoldTool(button, width >= 680); }
    }

    private void BuildTools()
    {
        if (!_built) { return; }
        _toolRow.Children.Clear();
        foreach (var tool in Tools ?? [])
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Lucide.Glyph(tool.Glyph, 18));
            var word = AiUi.Line(tool.Label, 14, 19.6, "Foreground", false, false);
            word.Margin = new Thickness(6, 0, 0, 0);
            word.ClearValue(TextBlock.ForegroundProperty);
            row.Children.Add(word);
            var button = new ToolbarButton(18) { Height = 36, MinWidth = 36, Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 0), Content = row, Tag = tool, ToolTip = tool.Label };
            button.Roles = self => (tool.IsOn ? "ControlTrack" : self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, tool.IsOn || self.IsMouseOver ? "Foreground" : "TextSecondary");
            button.Click += (_, _) => tool.IsOn = !tool.IsOn;
            tool.PropertyChanged += (_, _) => { button.Refresh(true); AutomationProperties.SetName(button, $"{tool.Label}, {(tool.IsOn ? "on" : "off")}"); Announcer.Say(this, $"{tool.Label} {(tool.IsOn ? "on" : "off")}"); };
            AutomationProperties.SetName(button, $"{tool.Label}, {(tool.IsOn ? "on" : "off")}");
            _toolRow.Children.Add(button);
        }
        Fold();
    }

    private static void FoldTool(ToolbarButton button, bool wide)
    {
        var row = (StackPanel)button.Content;
        row.Children[1].Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        button.Padding = wide ? new Thickness(10, 0, 12, 0) : new Thickness(0);
        button.Width = wide ? double.NaN : 36;
        row.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void OnTextProperty()
    {
        if (!_built || _syncing || _text.Text == Text) { return; }
        _changing = true;
        try { _tokens.Clear(); _text.Text = Text ?? ""; _text.CaretIndex = _text.Text.Length; }
        finally { _changing = false; }
        _lastText = _text.Text;
        _lastCaret = _text.CaretIndex;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_changing) { Reconcile(_lastText, _text.Text, _lastCaret); }
        _lastText = _text.Text;
        _lastCaret = _text.CaretIndex;
        AfterEdit();
    }

    private void AfterEdit()
    {
        _syncing = true;
        SetCurrentValue(TextProperty, _text.Text);
        _syncing = false;
        _fieldClip.MorphNext();
        _layer.InvalidateVisual();
        Refresh();
        Suggest();
    }

    // Tokens shift with edits around them; an edit that lands inside one turns it back into plain text.
    private void Reconcile(string was, string now, int hint)
    {
        if (_tokens.Count == 0) { return; }
        int prefix = 0, limit = Math.Min(Math.Min(was.Length, now.Length), Math.Max(0, hint));
        while (prefix < limit && was[prefix] == now[prefix]) { prefix++; }
        int suffix = 0;
        while (suffix < was.Length - prefix && suffix < now.Length - prefix && was[^(suffix + 1)] == now[^(suffix + 1)]) { suffix++; }
        int oldEnd = was.Length - suffix, delta = now.Length - was.Length;
        for (int index = _tokens.Count - 1; index >= 0; index--)
        {
            var token = _tokens[index];
            if (token.End <= prefix) { continue; }
            if (token.Start >= oldEnd) { _tokens[index] = token with { Start = token.Start + delta }; continue; }
            _tokens.RemoveAt(index);
        }
        _tokens.RemoveAll(token => token.Start < 0 || token.End > now.Length || now.Substring(token.Start, token.Text.Length) != token.Text);
    }

    // A click can't park the caret inside a token.
    private void OnSelection(object sender, RoutedEventArgs e)
    {
        _lastCaret = _text.CaretIndex;
        if (_text.SelectionLength == 0 && _tokens.FirstOrDefault(token => _text.CaretIndex > token.Start && _text.CaretIndex < token.End) is { } inside) { _text.CaretIndex = inside.End; return; }
        if (_list != ListKind.None) { Suggest(); }
    }

    private void OnFieldKey(object sender, KeyEventArgs e)
    {
        if (e.ImeProcessedKey != Key.None) { return; }
        if (HandleKey(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), e.IsRepeat)) { e.Handled = true; }
    }

    private bool HandleKey(Key key, bool shift, bool repeat = false)
    {
        if (_list != ListKind.None)
        {
            switch (key)
            {
                case Key.Down: Select(_rows.Count == 0 ? 0 : (_selected + 1) % _rows.Count); return true;
                case Key.Up: Select(_rows.Count == 0 ? 0 : (_selected - 1 + _rows.Count) % _rows.Count); return true;
                case Key.Enter or Key.Tab when !shift:
                    if (_rows.Count > 0) { Choose(_rows[_selected].Item); return true; }
                    if (key == Key.Tab) { CloseList(); return false; }
                    break;
                case Key.Escape: _dismissedAt = _triggerAt; CloseList(); return true;
            }
        }
        int caret = _text.CaretIndex;
        if (_text.SelectionLength == 0)
        {
            if (key == Key.Back && _tokens.FirstOrDefault(token => token.End == caret) is { } before) { RemoveToken(before); return true; }
            if (key == Key.Delete && _tokens.FirstOrDefault(token => token.Start == caret) is { } after) { RemoveToken(after); return true; }
            if (key == Key.Left && !shift && _tokens.FirstOrDefault(token => token.End == caret) is { } hopLeft) { _text.CaretIndex = hopLeft.Start; return true; }
            if (key == Key.Right && !shift && _tokens.FirstOrDefault(token => token.Start == caret) is { } hopRight) { _text.CaretIndex = hopRight.End; return true; }
        }
        if (key == Key.Enter && !shift)
        {
            if (!repeat) { Send(); }
            return true;
        }
        if (key == Key.Escape)
        {
            if (IsDictating) { DictationRequested?.Invoke(this, EventArgs.Empty); return true; }
            if (IsGenerating) { StopRequested?.Invoke(this, EventArgs.Empty); return true; }
        }
        return false;
    }

    private void RemoveToken(PromptToken token)
    {
        _changing = true;
        try
        {
            _tokens.Remove(token);
            _text.Select(token.Start, token.Text.Length);
            _text.SelectedText = "";
            // Everything after the token moves back by its length.
            for (int index = 0; index < _tokens.Count; index++)
            {
                if (_tokens[index].Start > token.Start) { _tokens[index] = _tokens[index] with { Start = _tokens[index].Start - token.Text.Length }; }
            }
            _text.CaretIndex = token.Start;
        }
        finally { _changing = false; }
        _lastText = _text.Text;
        _lastCaret = _text.CaretIndex;
        AfterEdit();
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.FileDrop))
        {
            e.CancelCommand();
            if (e.DataObject.GetData(DataFormats.FileDrop) is string[] files) { AddFiles(files); }
            return;
        }
        if (e.DataObject.GetData(DataFormats.UnicodeText) is string text && text.Length >= LongPasteLength)
        {
            e.CancelCommand();
            Paste(text);
        }
    }

    private void Paste(string text)
    {
        if (text.Length < LongPasteLength) { _text.SelectedText = text; _text.CaretIndex = _text.SelectionStart + _text.SelectionLength; return; }
        Add(new PromptAttachment("Pasted text") { PastedText = text, Detail = text.Length.ToString("N0", CultureInfo.InvariantCulture) + " characters" });
        Announcer.Say(this, "Long paste added as an attachment");
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { return; }
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        _surface.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Accent");
    }

    protected override void OnDragLeave(DragEventArgs e) { base.OnDragLeave(e); RefreshEdge(); }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        RefreshEdge();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) { e.Handled = true; AddFiles(files); }
    }

    private void Send()
    {
        if (!CanSend) { return; }
        var attachments = Attachments.Where(item => item.Status == PromptAttachmentStatus.Done).ToList();
        var message = new PromptMessage(_text.Text.Trim(), [.. _tokens.OrderBy(token => token.Start)], attachments, _picker.Chosen?.Id, _picker.ShownEffort, [.. (Tools ?? []).Where(tool => tool.IsOn).Select(tool => tool.Id)]);
        _changing = true;
        try { _tokens.Clear(); _text.Clear(); }
        finally { _changing = false; }
        _lastText = "";
        _lastCaret = 0;
        Attachments.Clear();
        SetCurrentValue(ErrorTextProperty, null);
        AfterEdit();
        Submitted?.Invoke(this, new PromptMessageEventArgs(message));
    }

    private void OnAttachments(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_built) { return; }
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added) { foreach (PromptAttachment item in added) { AddChip(item, true); } }
        else
        {
            _tray.Children.Clear();
            foreach (var item in Attachments) { AddChip(item, false); }
        }
        bool any = _tray.Children.Count > 0;
        if (any != (_trayClip.Visibility == Visibility.Visible)) { _trayClip.Visibility = any ? Visibility.Visible : Visibility.Collapsed; if (any && AiUi.Moving(this)) { _trayClip.Reveal(); } }
        Refresh();
    }

    private void AddChip(PromptAttachment item, bool animate)
    {
        var chip = new AttachmentChip(item, () => { Attachments.Remove(item); AttachmentRemoved?.Invoke(this, new PromptAttachmentEventArgs(item)); _text.Focus(); }, () => { Refresh(); if (!Uploading && item.Status == PromptAttachmentStatus.Done) { Announcer.Say(this, "Uploads finished"); } });
        chip.Margin = new Thickness(_tray.Children.Count > 0 ? 8 : 0, 0, 0, 0);
        _tray.Children.Add(chip);
        _trayClip.Visibility = Visibility.Visible;
        if (animate && AiUi.Moving(this))
        {
            var scale = new ScaleTransform(.9, .9);
            chip.RenderTransform = scale;
            chip.RenderTransformOrigin = new Point(.5, .5);
            chip.Opacity = 0;
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
            Tween.To(chip, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        }
    }

    private void RefreshEdge()
    {
        if (!_built) { return; }
        _surface.SetResourceReference(Border.BorderBrushProperty, _text.IsKeyboardFocused || Length > 0 ? "ArcWpf.Brush.BorderStrong" : "ArcWpf.Brush.Border");
    }

    private void Refresh()
    {
        if (!_built) { return; }
        _placeholder.Text = Placeholder ?? "";
        _placeholder.Visibility = Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_text, string.IsNullOrEmpty(Placeholder) ? "Message" : Placeholder);
        _mic.Visibility = ShowDictation ? Visibility.Visible : Visibility.Collapsed;
        _mic.Refresh(IsLoaded);
        AutomationProperties.SetName(_mic, IsDictating ? "Stop dictating" : "Dictate");
        _picker.Visibility = Models is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        int max = Math.Max(1, MaxLength);
        double used = Length / (double)max;
        _counter.Visibility = used >= .8 ? Visibility.Visible : Visibility.Collapsed;
        _counterRing.Value = Math.Min(1, used);
        _counterRing.Role = Over ? "Danger" : used >= .95 ? "Warning" : "TextMuted";
        _counterText.Text = (max - Length).ToString(CultureInfo.InvariantCulture);
        _counterText.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (Over ? "Danger" : used >= .95 ? "Warning" : "TextMuted"));

        SendFace(IsGenerating);
        _send.IsInert = !IsGenerating && !CanSend;
        AutomationProperties.SetName(_send, IsGenerating ? "Stop generating" : Uploading ? "Send after uploads finish" : Over ? "Send with the message over the limit" : "Send message");
        _send.Refresh(IsLoaded);
        ReplyRing();

        bool failed = !string.IsNullOrEmpty(ErrorText);
        if (failed != (_errorRow.Visibility == Visibility.Visible)) { _errorClip.MorphNext(); }
        _errorRow.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        _errorLine.Text = ErrorText ?? "";
        RefreshEdge();
    }

    private void ReplyRing()
    {
        bool show = IsGenerating && Progress is not null;
        _replyRing.Value = Math.Clamp(Progress ?? _replyRing.Value, 0, 1);
        if (show == _replyRing.Opacity > 0) { return; }
        // The arc fades as the button turns back into an arrow.
        if (AiUi.Moving(this)) { Tween.To(_replyRing, OpacityProperty, show ? 1 : 0, Tween.Fast, ArcEasing.Standard); }
        else { Tween.Set(_replyRing, OpacityProperty, show ? 1 : 0); }
    }

    // The arrow lifts out and the stop square scales in on the morph spring.
    private void SendFace(bool stop)
    {
        if (_sendIsStop == stop) { return; }
        bool first = _sendIsStop is null;
        _sendIsStop = stop;
        var old = _sendGlyph;
        FrameworkElement next;
        if (stop)
        {
            var square = new Border { Width = 11, Height = 11, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            square.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
            next = square;
        }
        else { next = Lucide.Glyph(Lucide.ArrowUp, 16, 2.25); }
        _sendGlyph = next;
        _sendFace.Children.Add(next);
        if (first || !AiUi.Moving(this))
        {
            if (old is not null) { _sendFace.Children.Remove(old); }
            return;
        }
        var scale = new ScaleTransform(.6, .6);
        var shift = new TranslateTransform(0, stop ? 0 : 6);
        next.RenderTransform = new TransformGroup { Children = { scale, shift } };
        next.RenderTransformOrigin = new Point(.5, .5);
        next.Opacity = 0;
        Tween.To(next, OpacityProperty, 1, Tween.Fast, ArcEasing.Enter);
        Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Morph);
        Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Morph);
        Tween.Spring(shift, TranslateTransform.YProperty, 0, Tween.Morph);
        if (old is not null)
        {
            var lift = new TranslateTransform();
            old.RenderTransform = lift;
            Tween.To(lift, TranslateTransform.YProperty, -6, Tween.Instant, ArcEasing.Standard);
            Tween.To(old, OpacityProperty, 0, Tween.Instant, ArcEasing.Standard, 0, () => _sendFace.Children.Remove(old));
        }
    }

    // "/" at the start of a line, "@" at the start or after a space. The list closes the moment the query breaks.
    private void Suggest()
    {
        if (!_built || !_text.IsKeyboardFocused && !_listPopup.IsOpen && _list == ListKind.None && Length == 0) { CloseList(); return; }
        int caret = _text.CaretIndex;
        string before = _text.Text[..Math.Clamp(caret, 0, Length)];
        int lineStart = before.LastIndexOf('\n') + 1;
        string line = before[lineStart..];
        ListKind kind = ListKind.None;
        int at = -1;
        if (line.StartsWith('/') && !line.Contains(' ')) { kind = ListKind.Command; at = lineStart; }
        else
        {
            int mark = before.LastIndexOf('@');
            if (mark >= lineStart && (mark == 0 || char.IsWhiteSpace(before[mark - 1])) && !before[mark..].Contains(' ')) { kind = ListKind.Mention; at = mark; }
        }
        if (kind == ListKind.None || _tokens.Any(token => at >= token.Start && at < token.End) || (kind == ListKind.Command && Commands is not { Count: > 0 }) || (kind == ListKind.Mention && Mentions is not { Count: > 0 }) || at == _dismissedAt)
        {
            if (at != _dismissedAt) { _dismissedAt = -1; }
            CloseList();
            return;
        }
        _dismissedAt = -1;
        _triggerAt = at;
        BuildList(kind, before[(at + 1)..]);
    }

    private void BuildList(ListKind kind, string query)
    {
        object? was = _rows.Count > _selected ? _rows[_selected].Item : null;
        _rows.Clear();
        _listRows.Children.Clear();
        if (kind == ListKind.Command)
        {
            foreach (var command in Commands!.Where(item => item.Label.TrimStart('/').StartsWith(query, StringComparison.OrdinalIgnoreCase))) { AddRow(command, "/" + command.Label.TrimStart('/'), command.Hint, command.Glyph ?? Lucide.Sparkles); }
        }
        else
        {
            foreach (var group in Mentions!)
            {
                var items = group.Items.Where(item => item.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (items.Count == 0) { continue; }
                var title = AiUi.Line(group.Title, 12, 16.8, "TextMuted");
                title.Margin = new Thickness(10, 6, 10, 4);
                _listRows.Children.Add(title);
                foreach (var item in items) { AddRow(item, item.Label, item.Detail, item.Glyph ?? Lucide.FileText); }
            }
        }
        if (_rows.Count == 0)
        {
            var none = AiUi.Line(kind == ListKind.Command ? "No commands match" : "Nothing matches", 14, 19.6, "TextMuted");
            none.Margin = new Thickness(10, 10, 10, 10);
            _listRows.Children.Add(none);
        }
        int keep = was is null ? -1 : _rows.FindIndex(row => Equals(row.Item, was));
        bool opening = _list != kind || !_listPopup.IsOpen;
        _list = kind;
        _selected = opening || keep < 0 ? 0 : keep;
        Paint();
        if (opening)
        {
            _pointerAtOpen = PointToScreen(Mouse.GetPosition(this));
            _listPopup.IsOpen = true;
            if (AiUi.Moving(this))
            {
                var scale = new ScaleTransform(.97, .97);
                _listSurface.RenderTransform = scale;
                _listSurface.RenderTransformOrigin = new Point(.2, 1);
                _listSurface.Opacity = 0;
                Tween.To(_listSurface, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
                Tween.To(scale, ScaleTransform.ScaleXProperty, 1, Tween.Fast, ArcEasing.Enter);
                Tween.To(scale, ScaleTransform.ScaleYProperty, 1, Tween.Fast, ArcEasing.Enter);
            }
            else { Tween.Set(_listSurface, OpacityProperty, 1); }
        }
        else { _listPopup.HorizontalOffset += 0; }
        Announcer.Say(this, _rows.Count == 0 ? "No matches" : $"{_rows.Count} options");
    }

    private void AddRow(object item, string label, string? hint, string glyph)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = AiUi.Glyph(glyph, 16, "TextSecondary");
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        icon.Margin = new Thickness(10, 0, 0, 0);
        grid.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(AiUi.Line(label, 14, 19.6));
        if (!string.IsNullOrEmpty(hint)) { text.Children.Add(AiUi.Line(hint, 12, 16.8, "TextMuted")); }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var surface = new Border { CornerRadius = new CornerRadius(15), Child = grid, Padding = new Thickness(0, 0, 8, 0) };
        // The field keeps focus while the list is open: rows take the pointer and never the keyboard.
        var row = new ToolbarButton(15) { Height = 46.4, Padding = new Thickness(0), Focusable = false, IsTabStop = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = surface, Roles = _ => (null, null, "Foreground") };
        int index = _rows.Count;
        // A list that opens under a resting pointer keeps its first option: only a real move selects.
        row.MouseMove += (_, e) =>
        {
            var at = PointToScreen(e.GetPosition(this));
            if (at == _pointerAtOpen || index == _selected) { return; }
            _pointerAtOpen = null;
            Select(index);
        };
        row.Click += (_, _) => Choose(item);
        AutomationProperties.SetName(row, label);
        _rows.Add(new ListRow(row, item, surface));
        _listRows.Children.Add(row);
    }

    private void Select(int index)
    {
        _selected = index;
        Paint();
        if (index < _rows.Count) { _rows[index].View.BringIntoView(); Announcer.Say(this, AutomationProperties.GetName(_rows[index].View)); }
    }

    private void Paint()
    {
        for (int index = 0; index < _rows.Count; index++)
        {
            if (index == _selected) { _rows[index].Surface.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted"); }
            else { _rows[index].Surface.Background = Brushes.Transparent; }
        }
    }

    private void CloseList()
    {
        _list = ListKind.None;
        _rows.Clear();
        _selected = 0;
        if (_listPopup.IsOpen) { _listPopup.IsOpen = false; }
    }

    private void Choose(object item)
    {
        var (kind, id, word) = item switch
        {
            PromptCommand command => (PromptTokenKind.Command, command.Id, "/" + command.Label.TrimStart('/')),
            PromptMention mention => (PromptTokenKind.Mention, mention.Id, "@" + mention.Label),
            _ => throw new ArgumentException("Unknown list item", nameof(item)),
        };
        int start = _triggerAt, caret = _text.CaretIndex;
        CloseList();
        _changing = true;
        try
        {
            for (int index = 0; index < _tokens.Count; index++)
            {
                if (_tokens[index].Start >= caret) { _tokens[index] = _tokens[index] with { Start = _tokens[index].Start + word.Length + 1 - (caret - start) }; }
            }
            _text.Select(start, caret - start);
            _text.SelectedText = word + " ";
            _tokens.Add(new PromptToken(kind, id, word, start));
            _text.CaretIndex = start + word.Length + 1;
        }
        finally { _changing = false; }
        _lastText = _text.Text;
        _lastCaret = _text.CaretIndex;
        AfterEdit();
        _text.Focus();
    }

    /// <summary>Starts a mention from the plus menu: writes the trigger at the caret, with a space before it when one is needed.</summary>
    private void InsertMentionTrigger()
    {
        int caret = _text.CaretIndex;
        string lead = caret > 0 && !char.IsWhiteSpace(_text.Text[caret - 1]) ? " " : "";
        _text.Focus();
        _text.SelectedText = lead + "@";
        _text.CaretIndex = caret + lead.Length + 1;
        Suggest();
    }

    private void OpenPlus()
    {
        _plusRows.Children.Clear();
        var actions = AttachActions ?? [new PromptAttachAction("upload", "Upload file", Lucide.Paperclip)];
        foreach (var action in actions)
        {
            _plusRows.Children.Add(PlusRow(action.Label, action.Glyph, () =>
            {
                var args = new PromptAttachEventArgs(action.Id);
                AttachRequested?.Invoke(this, args);
                if (!args.Handled && action.Id == "upload") { Browse(); }
            }));
        }
        if (Mentions is { Count: > 0 })
        {
            var line = new Border { Height = 1, Margin = new Thickness(10, 6, 10, 6) };
            line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.BorderSubtle");
            _plusRows.Children.Add(line);
            _plusRows.Children.Add(PlusRow("Mention a file or doc", Lucide.AtSign, InsertMentionTrigger));
        }
        _plusPopup.IsOpen = true;
        if (AiUi.Moving(this))
        {
            var scale = new ScaleTransform(.97, .97);
            _plusSurface.RenderTransform = scale;
            _plusSurface.RenderTransformOrigin = new Point(0, 1);
            _plusSurface.Opacity = 0;
            Tween.To(_plusSurface, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
            Tween.To(scale, ScaleTransform.ScaleXProperty, 1, Tween.Fast, ArcEasing.Enter);
            Tween.To(scale, ScaleTransform.ScaleYProperty, 1, Tween.Fast, ArcEasing.Enter);
        }
        else { Tween.Set(_plusSurface, OpacityProperty, 1); }
        Dispatcher.BeginInvoke(() => _plusRows.Children.OfType<ToolbarButton>().FirstOrDefault()?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private ToolbarButton PlusRow(string label, string glyph, Action action)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = AiUi.Glyph(glyph, 16, "TextSecondary");
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        icon.Margin = new Thickness(10, 0, 0, 0);
        grid.Children.Add(icon);
        var text = AiUi.Line(label, 14, 19.6);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var row = new ToolbarButton(14) { Height = 40, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = grid, FocusVisualStyle = null, Roles = self => (self.IsMouseOver || self.IsKeyboardFocused ? "SurfaceMuted" : null, null, "Foreground") };
        row.GotKeyboardFocus += (_, _) => row.Refresh(true);
        row.LostKeyboardFocus += (_, _) => row.Refresh(true);
        row.Click += (_, _) =>
        {
            _plusPopup.IsOpen = false;
            _text.Focus();
            action();
        };
        AutomationProperties.SetName(row, label);
        return row;
    }

    private void OnPlusKey(object sender, KeyEventArgs e)
    {
        var rows = _plusRows.Children.OfType<ToolbarButton>().ToList();
        int at = rows.FindIndex(row => row.IsKeyboardFocused);
        if (e.Key == Key.Down) { rows[(at + 1) % rows.Count].Focus(); e.Handled = true; }
        else if (e.Key == Key.Up) { rows[(at - 1 + rows.Count) % rows.Count].Focus(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _plusPopup.IsOpen = false; _plus.Focus(); e.Handled = true; }
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog { Multiselect = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) { AddFiles(dialog.FileNames); }
    }

    // Above the caret's line, its text lined up with the trigger.
    private CustomPopupPlacement[] PlaceList(Size popup, Size target, Point offset)
    {
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var at = _text.GetRectFromCharacterIndex(Math.Clamp(_triggerAt, 0, Length), false);
        if (at.IsEmpty || double.IsNaN(at.X)) { at = new Rect(8, 8, 0, LineHeight); }
        var origin = _text.TranslatePoint(new Point(at.X, at.Top), _fieldClip);
        double gutter = Gutter * scale, gap = 6 * scale;
        double x = origin.X * scale - gutter - 36 * scale;
        x = Math.Max(-gutter, Math.Min(x, target.Width - popup.Width - gutter));
        double above = origin.Y * scale - popup.Height - gutter - gap; // The size given here leaves out the gutter margin.
        double below = (origin.Y + at.Height) * scale + gap - gutter;
        return [new(new Point(x, above), PopupPrimaryAxis.Horizontal), new(new Point(x, below), PopupPrimaryAxis.Horizontal)];
    }

    private CustomPopupPlacement[] PlacePlus(Size popup, Size target, Point offset)
    {
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double gutter = Gutter * scale, gap = 8 * scale;
        return [new(new Point(-gutter, -popup.Height - gutter - gap), PopupPrimaryAxis.Horizontal), new(new Point(-gutter, target.Height + gap - gutter), PopupPrimaryAxis.Horizontal)];
    }

    private sealed class AttachmentChip : Border
    {
        private readonly PromptAttachment _item;
        private readonly Action _changed;
        private readonly Decorator _trail = new() { Width = 24, Height = 24, Margin = new Thickness(4, 0, 0, 0) };
        private readonly TextBlock _detail;
        private readonly AiRing _ring = new(16, 1.75);
        internal System.Windows.Controls.Button RemoveButton { get; }
        internal string StateForTest { get; private set; } = "";

        public AttachmentChip(PromptAttachment item, Action remove, Action changed)
        {
            (_item, _changed) = (item, changed);
            Height = 44;
            CornerRadius = new CornerRadius(14);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(10, 0, 6, 0);
            SetResourceReference(BorderBrushProperty, "ArcWpf.Brush.Border");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = AiUi.Glyph(item.PastedText is null ? Lucide.FileText : Lucide.Layers, 16, "TextSecondary");
            icon.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(icon);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170 };
            text.Children.Add(AiUi.Line(item.Name, 12, 16.8, "Foreground", true));
            _detail = AiUi.Line(item.Detail ?? "", 12, 16.8, "TextMuted");
            text.Children.Add(_detail);
            row.Children.Add(text);
            var button = AiUi.Icon(Lucide.X, 24, 14, $"Remove {item.Name}");
            button.Click += (_, _) => remove();
            RemoveButton = button;
            row.Children.Add(_trail);
            Child = row;
            item.PropertyChanged += OnItem;
            Unloaded += (_, _) => item.PropertyChanged -= OnItem;
            Loaded += (_, _) => { item.PropertyChanged -= OnItem; item.PropertyChanged += OnItem; };
            Sync(false);
        }

        private void OnItem(object? sender, PropertyChangedEventArgs e) { Sync(true); _changed(); }

        private void Sync(bool animate)
        {
            var button = (ToolbarButton)RemoveButton;
            string state = _item.Status.ToString();
            _ring.Value = _item.Progress;
            _detail.Text = _item.Status == PromptAttachmentStatus.Error ? _item.Error ?? "Upload failed" : _item.Detail ?? "";
            _detail.Visibility = _detail.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _detail.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush." + (_item.Status == PromptAttachmentStatus.Error ? "Danger" : "TextMuted"));
            if (state == StateForTest) { return; }
            bool finished = StateForTest == nameof(PromptAttachmentStatus.Uploading) && _item.Status == PromptAttachmentStatus.Done;
            StateForTest = state;
            AutomationProperties.SetName(button, _item.Status == PromptAttachmentStatus.Uploading ? "Cancel upload" : $"Remove {_item.Name}");
            if (_item.Status == PromptAttachmentStatus.Uploading)
            {
                // A running upload's control is its ring; pressing it cancels.
                var hold = new Grid();
                _trail.Child = null;
                (_ring.Parent as Panel)?.Children.Remove(_ring);
                hold.Children.Add(_ring);
                button.Content = hold;
            }
            else
            {
                button.Content = Lucide.Glyph(Lucide.X, 14);
                // The check pops in before handing over to the remove control.
                if (finished && animate && AiUi.Moving(this))
                {
                    var check = AiUi.Glyph(Lucide.Check, 16, "Success");
                    var scale = new ScaleTransform(.6, .6);
                    check.RenderTransform = scale;
                    check.RenderTransformOrigin = new Point(.5, .5);
                    Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, Tween.Snappy);
                    Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, Tween.Snappy);
                    _trail.Child = check;
                    var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(.7) };
                    timer.Tick += (_, _) => { timer.Stop(); if (StateForTest == nameof(PromptAttachmentStatus.Done)) { _trail.Child = button; } };
                    timer.Start();
                    return;
                }
            }
            _trail.Child = button;
        }
    }
}

/// <summary>Paints the token chips under the text, in the field's own coordinates.</summary>
internal sealed class PromptTokenLayer : FrameworkElement
{
    private readonly PromptInput _owner;
    public PromptTokenLayer(PromptInput owner) { _owner = owner; IsHitTestVisible = false; }

    protected override void OnRender(DrawingContext dc)
    {
        var box = _owner.TextBoxForTest;
        if (box is null || _owner.Tokens.Count == 0) { return; }
        var ink = _owner.TokenBrush;
        var at = box.TranslatePoint(new Point(), this);
        foreach (var token in _owner.Tokens)
        {
            if (token.End > box.Text.Length) { continue; }
            Rect? line = null;
            for (int index = token.Start; index < token.End; index++)
            {
                var lead = box.GetRectFromCharacterIndex(index, false);
                var trail = box.GetRectFromCharacterIndex(index, true);
                if (lead.IsEmpty || trail.IsEmpty || double.IsNaN(lead.X) || double.IsNaN(trail.X)) { continue; }
                var rect = new Rect(Math.Min(lead.Left, trail.Left), lead.Top, Math.Abs(trail.Left - lead.Left), lead.Height);
                if (line is { } current && Math.Abs(current.Top - rect.Top) <= .5) { line = Rect.Union(current, rect); }
                else { if (line is { } done) { Draw(dc, done, ink, at); } line = rect; }
            }
            if (line is { } last) { Draw(dc, last, ink, at); }
        }
    }

    private static void Draw(DrawingContext dc, Rect rect, Brush ink, Point at)
    {
        rect.Inflate(3, 0);
        rect.Y += (rect.Height - 20) / 2 + at.Y;
        rect.X += at.X;
        rect.Height = 20;
        dc.DrawRoundedRectangle(ink, null, rect, 6, 6);
    }
}

/// <summary>A thin arc that fills clockwise from twelve o'clock.</summary>
internal sealed class AiRing : FrameworkElement
{
    private readonly double _size, _stroke;
    private double _value;
    private string _role = "TextMuted";

    public AiRing(double size, double stroke)
    {
        (_size, _stroke) = (size, stroke);
        Width = Height = size;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        ThemeWatch.Add(this, InvalidateVisual);
    }

    public double Value { get => _value; set { if (_value != value) { _value = value; InvalidateVisual(); } } }
    public string Role { get => _role; set { if (_role != value) { _role = value; InvalidateVisual(); } } }

    protected override void OnRender(DrawingContext dc)
    {
        double r = (_size - _stroke) / 2, c = _size / 2;
        var track = new SolidColorBrush(OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), .1));
        if (Role != "Accent") { dc.DrawEllipse(null, new Pen(track, _stroke), new Point(c, c), r, r); }
        var pen = new Pen(new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush." + Role)), _stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (_value >= 1) { dc.DrawEllipse(null, pen, new Point(c, c), r, r); return; }
        if (_value <= 0) { return; }
        double angle = _value * 2 * Math.PI;
        var figure = new PathFigure { StartPoint = new Point(c, c - r) };
        figure.Segments.Add(new ArcSegment(new Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle)), new Size(r, r), 0, angle > Math.PI, SweepDirection.Clockwise, true));
        dc.DrawGeometry(null, pen, new PathGeometry([figure]));
    }
}
