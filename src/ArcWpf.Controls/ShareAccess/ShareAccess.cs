using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public enum ShareRole { Viewer, Commenter, Editor, Owner }
public enum ShareGeneralAccess { Restricted, Link }

public class ShareAccessContact
{
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public ImageSource? Source { get; set; }
    internal string Display => string.IsNullOrEmpty(Name) ? Email : Name!;
}

public sealed class ShareAccessMember : ShareAccessContact
{
    public string Id { get; set; } = "";
    public ShareRole Role { get; set; } = ShareRole.Viewer;
    public bool Pending { get; set; }
}

public sealed class ShareAccessContactCollection : Collection<ShareAccessContact>;
public sealed class ShareAccessMemberCollection : Collection<ShareAccessMember>;

public sealed class ShareMembersChangedEventArgs(IReadOnlyList<ShareAccessMember> members) : EventArgs
{
    public IReadOnlyList<ShareAccessMember> Members { get; } = members;
}

public sealed class ShareLinkCopiedEventArgs(string link) : EventArgs
{
    public string Link { get; } = link;
}

// Arc Pro: no source. Built from the public docs and preview geometry; see docs/references/components/ShareAccess.md.
public class ShareAccess : Control
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ShareAccess), new FrameworkPropertyMetadata("Q3 launch plan", (d, _) => ((ShareAccess)d).SyncTitle()));
    public static readonly DependencyProperty MembersProperty = DependencyProperty.Register(nameof(Members), typeof(IList<ShareAccessMember>), typeof(ShareAccess), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShareAccess)d).SyncPeople()));
    public static readonly DependencyProperty AccessProperty = DependencyProperty.Register(nameof(Access), typeof(ShareGeneralAccess), typeof(ShareAccess), new FrameworkPropertyMetadata(ShareGeneralAccess.Restricted, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShareAccess)d).OnAccess()));
    public static readonly DependencyProperty LinkRoleProperty = DependencyProperty.Register(nameof(LinkRole), typeof(ShareRole), typeof(ShareAccess), new FrameworkPropertyMetadata(ShareRole.Viewer, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShareAccess)d).OnLinkRole()));
    public static readonly DependencyProperty DirectoryProperty = DependencyProperty.Register(nameof(Directory), typeof(IList<ShareAccessContact>), typeof(ShareAccess), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty LinkProperty = DependencyProperty.Register(nameof(Link), typeof(string), typeof(ShareAccess), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty SelfIdProperty = DependencyProperty.Register(nameof(SelfId), typeof(string), typeof(ShareAccess), new FrameworkPropertyMetadata(null, (d, _) => ((ShareAccess)d).SyncPeople()));
    public static readonly DependencyProperty DefaultInviteRoleProperty = DependencyProperty.Register(nameof(DefaultInviteRole), typeof(ShareRole), typeof(ShareAccess), new FrameworkPropertyMetadata(ShareRole.Editor, (d, _) => ((ShareAccess)d).ResetInviteRole()));

    /// <summary>Name of what is being shared, used in the heading.</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>People with access. Replaced with the whole next list on every change.</summary>
    public IList<ShareAccessMember>? Members { get => (IList<ShareAccessMember>?)GetValue(MembersProperty); set => SetValue(MembersProperty, value); }
    public ShareGeneralAccess Access { get => (ShareGeneralAccess)GetValue(AccessProperty); set => SetValue(AccessProperty, value); }
    /// <summary>What anyone with the link can do.</summary>
    public ShareRole LinkRole { get => (ShareRole)GetValue(LinkRoleProperty); set => SetValue(LinkRoleProperty, value); }
    /// <summary>People to suggest while typing.</summary>
    public IList<ShareAccessContact>? Directory { get => (IList<ShareAccessContact>?)GetValue(DirectoryProperty); set => SetValue(DirectoryProperty, value); }
    /// <summary>The link Copy link puts on the clipboard.</summary>
    public string? Link { get => (string?)GetValue(LinkProperty); set => SetValue(LinkProperty, value); }
    /// <summary>Id of the member using the app, marked (you).</summary>
    public string? SelfId { get => (string?)GetValue(SelfIdProperty); set => SetValue(SelfIdProperty, value); }
    public ShareRole DefaultInviteRole { get => (ShareRole)GetValue(DefaultInviteRoleProperty); set => SetValue(DefaultInviteRoleProperty, value); }
    /// <summary>Called with the valid chips and role when Invite is pressed. A faulted task keeps the chips and explains it.</summary>
    public Func<IReadOnlyList<ShareAccessContact>, ShareRole, Task>? InviteHandler { get; set; }
    public event EventHandler<ShareMembersChangedEventArgs>? MembersChanged;
    public event EventHandler? AccessChanged;
    public event EventHandler? LinkRoleChanged;
    public event EventHandler<ShareLinkCopiedEventArgs>? LinkCopied;
    private EventHandler? _done;
    /// <summary>Handling it shows a Done button.</summary>
    public event EventHandler? Done { add { _done += value; SyncFooter(); } remove { _done -= value; SyncFooter(); } }

    private sealed class Chip
    {
        public required ShareAccessContact Contact;
        public required bool Valid;
        public required FrameworkElement View;
        public Border? Face;
    }

    private sealed class PersonRow
    {
        public required ShareAccessMember Member;
        public required Grid Wrap;
        public required Border Body;
        public required TextBlock Meta;
        public RoleTrigger? Trigger;
    }

    // A pill that opens a menu. Its label crossfades vertically while the width follows the new label, so the chevron glides.
    private sealed class RoleTrigger : ContentControl
    {
        public readonly SwapText Label;
        private readonly Border _face = new() { Height = 32, CornerRadius = new CornerRadius(16), Padding = new Thickness(10, 0, 8, 0) };
        private readonly SolidColorBrush _wash = new(Colors.Transparent);
        private readonly Border _ring = new() { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), Margin = new Thickness(-2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        private readonly Decorator _labelBox = new() { ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
        private readonly ArcDataGridIcon _chevron = new() { Icon = "chevron-down", Size = 16, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform() };
        private readonly FrameworkElement _scope;
        private readonly Func<string> _spoken;
        private readonly FocusVisible _focus;
        private readonly TrackedText? _prefix;
        public bool MenuOpen { get; private set; }
        public Action? Clicked;
        public Func<Key, bool>? KeyPressed;

        public RoleTrigger(FrameworkElement owner, FontWeight weight, Brush ink, Func<string> spoken, string? prefix = null)
        {
            (_scope, _spoken) = (owner, spoken);
            Label = new SwapText(14, 1.4, -.14, weight, ink);
            Focusable = true;
            IsTabStop = true;
            FocusVisualStyle = null;
            Cursor = Cursors.Hand;
            _face.Background = _wash;
            _chevron.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextMuted");
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (prefix is not null)
            {
                _prefix = CopyMotion.Text(prefix, 14, 1.4, -.14, FontWeights.Normal, new SolidColorBrush(Tween.Resolve(owner, "ArcWpf.Brush.TextMuted")));
                _prefix.Margin = new Thickness(0, 0, 4, 0);
                _prefix.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(_prefix);
            }
            _labelBox.Child = Label;
            row.Children.Add(_labelBox);
            row.Children.Add(_chevron);
            _face.Child = row;
            _ring.BorderBrush = FocusVisible.RingBrush(owner);
            var all = new Grid();
            all.Children.Add(_face);
            all.Children.Add(_ring);
            Content = all;
            _focus = new FocusVisible(this, () => { _ring.Visibility = _focus!.Shown ? Visibility.Visible : Visibility.Collapsed; Paint(); });
            MouseEnter += (_, _) => Paint();
            MouseLeave += (_, _) => Paint();
            PreviewMouseLeftButtonDown += (_, e) => { _focus.Pointer(); Focus(); e.Handled = true; };
            MouseLeftButtonUp += (_, _) => Clicked?.Invoke();
            PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up) { _focus.Key(); Clicked?.Invoke(); e.Handled = true; } };
        }

        public double LabelWidth => _labelBox.ActualWidth;

        public void Set(string text, int direction = 1)
        {
            bool animate = _scope.IsLoaded && Tween.Level(_scope) == ArcMotionLevel.Full && Label.Text.Length > 0;
            Label.Set(text, direction);
            Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = Label.DesiredSize.Width;
            if (animate) { Tween.Spring(_labelBox, WidthProperty, width, .42); }
            else { _labelBox.BeginAnimation(WidthProperty, null); _labelBox.Width = width; }
        }

        public void SetOpen(bool open)
        {
            MenuOpen = open;
            ((RotateTransform)_chevron.RenderTransform).Angle = open ? 180 : 0;
            Paint();
        }

        private void Paint()
        {
            var quiet = Tween.Resolve(_scope, "ArcWpf.Brush.SurfaceMuted");
            bool hot = IsMouseOver || MenuOpen || _focus.Shown;
            Tween.Color(_wash, hot ? quiet : OkColor.Alpha(quiet, 0), Tween.Fast, _scope.IsLoaded && Tween.Level(_scope) == ArcMotionLevel.Full);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new DisclosurePeer(this, _spoken, () => MenuOpen, () => Clicked?.Invoke(), AutomationControlType.Button);
    }

    private sealed record MenuChoice(string Label, string? Hint, bool Danger, Action Run, bool Checked);

    private static readonly ShareRole[] Assignable = [ShareRole.Viewer, ShareRole.Commenter, ShareRole.Editor];
    private static readonly Regex EmailShape = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private const double RowHeight = 52.4, PanelWidth = 440, MenuWidth = 212;

    private readonly Border _root = new() { MaxWidth = PanelWidth, CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), Padding = new Thickness(20) };
    private readonly TrackedText _title;
    private readonly Border _field = new() { MinHeight = 44, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 6, 6), Cursor = Cursors.IBeam };
    private readonly WrapPanel _chipRow = new();
    private readonly TextBox _input = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), FontSize = 14, MinWidth = 60, Height = 30, VerticalContentAlignment = VerticalAlignment.Center, FocusVisualStyle = null };
    private readonly TextBlock _placeholder = ArcText.Block("Add people by name or email", 14, 19.6, "TextMuted");
    private readonly Grid _inputSlot = new() { Margin = new Thickness(0) };
    private readonly Border _suggestions = new() { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Padding = new Thickness(6), VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(.5, 0), RenderTransform = new ScaleTransform(1, 1) };
    private readonly StackPanel _options = new();
    private readonly Border _optionHighlight = new() { CornerRadius = new CornerRadius(14), Height = 50.4, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
    private readonly Canvas _suggestLayer = new();
    private readonly Grid _error = new() { ClipToBounds = true, Height = 0 };
    private readonly TextBlock _errorText = ArcText.Block("", 12, 16.8, "ArcDanger");
    private readonly Grid _inviteRowWrap = new() { ClipToBounds = true, Height = 0 };
    private readonly RoleTrigger _inviteRole;
    private readonly Border _inviteButton = new() { Height = 36, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 0, 16, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
    private readonly TextBlock _inviteText = ArcText.Block("Invite", 14, 19.6, "Background", true);
    private readonly OdometerText _peopleCount = new(14, 1.1, -.42, FontWeights.Normal);
    private readonly StackPanel _people = new();
    private readonly Grid _undoWrap = new() { ClipToBounds = true, Height = 0 };
    private readonly TextBlock _undoText = ArcText.Block("", 14, 19.6, "TextSecondary");
    private readonly RoleTrigger _access;
    private readonly RoleTrigger _linkRole;
    private readonly Grid _accessIcon = new() { Width = 20, Height = 20, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform() };
    private readonly ArcDataGridIcon _lock = new() { Icon = "lock", Size = 20 }, _globe = new() { Icon = "globe", Size = 20 };
    private readonly SwapText _accessHint;
    private readonly Border _copy = new() { Height = 36, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(16, 0, 16, 0), Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Grid _copyStack = new();
    private readonly FrameworkElement[] _copyLabels = new FrameworkElement[3];
    private readonly Border _doneButton = new() { Height = 36, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 0, 16, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
    private readonly Border _focusRingInvite = new(), _focusRingDone = new(), _focusRingCopy = new();
    private readonly Popup _menu = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Custom };
    private readonly StackPanel _menuItems = new();
    private readonly Border _menuFace = new() { Width = MenuWidth, CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Padding = new Thickness(6), Focusable = true, FocusVisualStyle = null };
    private readonly List<(Border View, MenuChoice Choice)> _menuEntries = [];
    private readonly List<Chip> _chips = [];
    private readonly List<PersonRow> _rows = [];
    private readonly List<ShareAccessContact> _matches = [];
    private readonly SolidColorBrush _ink = new(), _secondary = new(), _muted = new();
    private readonly DispatcherTimer _copyReset = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1.8) };
    private readonly HashSet<string> _welcomed = [];
    private Decorator? _host;
    private ShareRole _inviteAs;
    private int _highlight, _menuActive = -1, _nextId = 1;
    private bool _sending, _marked, _inviteShown, _built, _inviteFailed;
    private (ShareAccessMember Member, int Index)? _removed;
    private RoleTrigger? _menuOwner;

    static ShareAccess() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ShareAccess), new FrameworkPropertyMetadata(typeof(ShareAccess)));

    public ShareAccess()
    {
        Inks();
        _title = CopyMotion.Text("", 16, 1.1, -.48, FontWeights.Medium, _ink);
        _inviteRole = new RoleTrigger(this, FontWeights.Normal, _secondary, () => $"Invite as: {RoleLabel(_inviteAs)}", "Invite as");
        _access = new RoleTrigger(this, FontWeights.Medium, _ink, () => $"General access: {AccessLabel(Access)}");
        _linkRole = new RoleTrigger(this, FontWeights.Normal, _secondary, () => $"Anyone with the link: {RoleLabel(LinkRole)}") { Visibility = Visibility.Collapsed, Opacity = 0, RenderTransform = new TranslateTransform() };
        _accessHint = new SwapText(12, 1.4, -.14, FontWeights.Normal, _muted) { HorizontalAlignment = HorizontalAlignment.Left };
        _inviteAs = DefaultInviteRole;
        _copyReset.Tick += (_, _) => { _copyReset.Stop(); ShowCopy(0); };
        Loaded += (_, _) => { Inks(); SyncFooter(); };
        Unloaded += (_, _) => { _copyReset.Stop(); _menu.IsOpen = false; };
        ThemeWatch.Add(this, () => { Inks(); PaintField(); });
    }

    internal IReadOnlyList<string> ChipsForTest => [.. _chips.Select(chip => (chip.Valid ? "" : "!") + chip.Contact.Display)];
    internal IReadOnlyList<string> MatchesForTest => [.. _matches.Select(contact => contact.Display)];
    internal int HighlightForTest => _highlight;
    internal bool SuggestionsOpenForTest => _suggestions.Visibility == Visibility.Visible;
    internal string ErrorForTest => _errorText.Text;
    internal string InviteTextForTest => _inviteText.Text;
    internal bool InviteEnabledForTest => CanSend;
    internal bool InviteRowOpenForTest => _inviteShown;
    internal ShareRole InviteRoleForTest => _inviteAs;
    internal string UndoTextForTest => _undoText.Text;
    internal bool UndoShownForTest => _removed is not null;
    internal string CountForTest => _peopleCount.Text;
    internal string HintForTest => _accessHint.Text;
    internal string AccessLabelForTest => _access.Label.Text;
    internal string LinkRoleLabelForTest => _linkRole.Label.Text;
    internal bool LinkRoleShownForTest => _linkRole.Visibility == Visibility.Visible;
    internal string CopyTextForTest => _copyState switch { 1 => "Link copied", 2 => "Couldn’t copy", _ => "Copy link" };
    internal bool MarkedForTest => _marked;
    internal bool MenuOpenForTest => _menu.IsOpen;
    internal IReadOnlyList<string> MenuForTest => [.. _menuEntries.Select(entry => (entry.Choice.Checked ? "*" : "") + entry.Choice.Label)];
    internal int MenuActiveForTest => _menuActive;
    internal string PersonMetaForTest(int index) => _rows[index].Meta.Text;
    internal string? PersonRoleForTest(int index) => _rows[index].Trigger?.Label.Text;
    internal int PeopleForTest => _rows.Count;
    internal FrameworkElement PersonTriggerForTest(int index) => _rows[index].Trigger!;
    internal double PersonHeightForTest(int index) => _rows[index].Wrap.ActualHeight;
    internal FrameworkElement CopyForTest => _copy;
    internal FrameworkElement FieldForTest => _field;
    internal TextBox InputForTest => _input;
    internal void TypeForTest(string text) { _input.Text = text; _input.CaretIndex = text.Length; }
    internal void KeyForTest(Key key) => HandleInputKey(key);
    internal void OpenRoleMenuForTest(int index) => OpenPersonMenu(_rows[index]);
    internal void OpenInviteMenuForTest() => OpenInviteMenu();
    internal void OpenAccessMenuForTest() => OpenAccessMenu();
    internal void OpenLinkMenuForTest() => OpenLinkMenu();
    internal void PickForTest(int index) => RunMenu(index);
    internal void MenuKeyForTest(Key key) => HandleMenuKey(key);
    internal void TypeAheadForTest(char c) => TypeAhead(c);
    internal void UndoForTest() => Undo();
    internal Task SendForTest() => Send();
    internal void CopyForTestAction(bool ok) => FinishCopy(ok);

    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer(ShareAccess owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => $"Share “{owner.Title}”";
    }

    private bool Reduced => Tween.Level(this) != ArcMotionLevel.Full;
    private IList<ShareAccessMember> People => Members ?? (IList<ShareAccessMember>)[];
    private bool CanSend => !_sending && _chips.Count > 0 && _chips.All(chip => chip.Valid);

    internal static string RoleLabel(ShareRole role) => role switch { ShareRole.Viewer => "Can view", ShareRole.Commenter => "Can comment", ShareRole.Editor => "Can edit", _ => "Owner" };
    private static string RoleHint(ShareRole role) => role switch { ShareRole.Viewer => "Read only", ShareRole.Commenter => "Suggest and discuss", _ => "Make any change" };
    private static string AccessLabel(ShareGeneralAccess access) => access == ShareGeneralAccess.Link ? "Anyone with the link" : "Restricted";
    private static string RoleVerb(ShareRole role) => role switch { ShareRole.Commenter => "comment", ShareRole.Editor => "edit", _ => "view" };
    private string HintText => Access == ShareGeneralAccess.Link ? $"Anyone who has the link can {RoleVerb(LinkRole)}" : "Only people with access can open";

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild("PART_Root") as Decorator;
        Build();
    }

    private void Inks()
    {
        _ink.Color = Tween.Resolve(this, "ArcWpf.Brush.Foreground");
        _secondary.Color = Tween.Resolve(this, "ArcWpf.Brush.TextSecondary");
        _muted.Color = Tween.Resolve(this, "ArcWpf.Brush.TextMuted");
    }

    private static TextBlock SectionTitle(string text)
    {
        var block = ArcText.Block(text, 14, 15.4, "TextSecondary", true);
        return block;
    }

    private FrameworkElement Ringed(Border button, Border ring, double radius)
    {
        ring.CornerRadius = new CornerRadius(radius + 2);
        ring.BorderThickness = new Thickness(2);
        ring.Margin = new Thickness(-2);
        ring.IsHitTestVisible = false;
        ring.Visibility = Visibility.Collapsed;
        ring.BorderBrush = FocusVisible.RingBrush(this);
        button.GotKeyboardFocus += (_, _) => ring.Visibility = InputManager.Current.MostRecentInputDevice is KeyboardDevice ? Visibility.Visible : Visibility.Collapsed;
        button.LostKeyboardFocus += (_, _) => ring.Visibility = Visibility.Collapsed;
        var holder = new Grid { HorizontalAlignment = button.HorizontalAlignment, VerticalAlignment = VerticalAlignment.Center };
        holder.Children.Add(button);
        holder.Children.Add(ring);
        return holder;
    }

    private void Build()
    {
        if (_host is null || _built) { return; }
        _built = true;
        _root.UseLayoutRounding = true;
        _root.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _root.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _root.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _title.HorizontalAlignment = HorizontalAlignment.Left;

        // The field: chips wrap inside it as it grows, and the caret follows them.
        _input.SetResourceReference(TextBox.ForegroundProperty, "ArcWpf.Brush.Foreground");
        _input.SetResourceReference(TextBoxBase.CaretBrushProperty, "ArcWpf.Brush.Foreground");
        _input.Margin = new Thickness(6, 0, 6, 0);
        _input.Template = BareTextBox;
        _input.TextChanged += (_, _) => OnTyped();
        _input.PreviewKeyDown += (_, e) => { if (HandleInputKey(e.Key)) { e.Handled = true; } };
        _input.PreviewTextInput += (_, e) => { if (e.Text is "," or ";") { Commit(); e.Handled = true; } };
        _input.GotKeyboardFocus += (_, _) => { PaintField(); OnTyped(); };
        _input.LostKeyboardFocus += (_, _) => { PaintField(); _marked = false; PaintChips(); Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (!_input.IsKeyboardFocused && !_suggestions.IsMouseOver) { CloseSuggestions(); } })); };
        AutomationProperties.SetName(_input, "Add people by name or email");
        _placeholder.IsHitTestVisible = false;
        _placeholder.Margin = new Thickness(6, 0, 0, 0);
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        _inputSlot.Children.Add(_input);
        _inputSlot.Children.Add(_placeholder);
        _chipRow.Children.Add(_inputSlot);
        _field.Child = _chipRow;
        _field.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _field.MouseLeftButtonDown += (_, e) => { _input.Focus(); e.Handled = true; };
        _chipRow.SizeChanged += (_, _) => FitInput();
        _errorText.Margin = new Thickness(4, 6, 4, 0);
        _errorText.TextWrapping = TextWrapping.Wrap;
        _error.Children.Add(_errorText);
        AutomationProperties.SetLiveSetting(_errorText, AutomationLiveSetting.Polite);

        // The invite row expands once there is someone to invite.
        var invite = new Border { Padding = new Thickness(0, 12, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var inviteGrid = new Grid();
        _inviteRole.HorizontalAlignment = HorizontalAlignment.Left;
        _inviteRole.Margin = new Thickness(-4, 0, 0, 0);
        _inviteRole.VerticalAlignment = VerticalAlignment.Center;
        _inviteRole.Clicked = OpenInviteMenu;
        _inviteRole.Set(RoleLabel(_inviteAs));
        _inviteText.VerticalAlignment = VerticalAlignment.Center;
        _inviteText.HorizontalAlignment = HorizontalAlignment.Center;
        _inviteButton.Child = _inviteText;
        _inviteButton.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        _inviteButton.MouseLeftButtonUp += async (_, _) => await Send();
        _inviteButton.PreviewKeyDown += async (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; await Send(); } };
        inviteGrid.Children.Add(_inviteRole);
        inviteGrid.Children.Add(Ringed(_inviteButton, _focusRingInvite, 18));
        invite.Child = inviteGrid;
        _inviteRowWrap.Children.Add(invite);
        var inviteArea = new StackPanel();
        inviteArea.Children.Add(_field);
        inviteArea.Children.Add(_error);
        inviteArea.Children.Add(_inviteRowWrap);

        _peopleCount.SetValue(OdometerText.ForegroundProperty, _muted);
        var peopleHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        peopleHead.Children.Add(SectionTitle("People with access"));
        _peopleCount.Margin = new Thickness(6, 0, 0, 0);
        peopleHead.Children.Add(_peopleCount);
        _people.Margin = new Thickness(-10, 0, -10, 0);
        var undoBar = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 8, 6, 8), Margin = new Thickness(0, 4, 0, 0), Height = 44, VerticalAlignment = VerticalAlignment.Top };
        undoBar.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var undoGrid = new Grid();
        _undoText.VerticalAlignment = VerticalAlignment.Center;
        _undoText.TextTrimming = TextTrimming.CharacterEllipsis;
        _undoText.Margin = new Thickness(0, 0, 64, 0);
        var undoButton = new Border { Height = 28, CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand, Background = Brushes.Transparent, Focusable = true, FocusVisualStyle = null, Child = new TextBlock { Text = "Undo", FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center } };
        ((TextBlock)undoButton.Child).SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        undoButton.MouseLeftButtonUp += (_, _) => Undo();
        undoButton.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { Undo(); e.Handled = true; } };
        undoGrid.Children.Add(_undoText);
        undoGrid.Children.Add(undoButton);
        undoBar.Child = undoGrid;
        _undoWrap.Children.Add(undoBar);
        var peopleSection = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        peopleSection.Children.Add(peopleHead);
        peopleSection.Children.Add(_people);
        peopleSection.Children.Add(_undoWrap);

        // General access: the lock turns into a globe, the description rolls and the link role slides in.
        _lock.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
        _globe.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
        _accessIcon.Children.Add(_lock);
        _accessIcon.Children.Add(_globe);
        var iconSlot = new Grid { Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        iconSlot.Children.Add(_accessIcon);
        _access.HorizontalAlignment = HorizontalAlignment.Left;
        _access.Margin = new Thickness(-10, -4, 0, -4);
        _access.Clicked = OpenAccessMenu;
        _accessHint.VerticalAlignment = VerticalAlignment.Top;
        var generalText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        generalText.Children.Add(_access);
        generalText.Children.Add(_accessHint);
        _linkRole.HorizontalAlignment = HorizontalAlignment.Right;
        _linkRole.VerticalAlignment = VerticalAlignment.Center;
        _linkRole.Margin = new Thickness(0, 0, -4, 0);
        _linkRole.Clicked = OpenLinkMenu;
        var general = new Grid { MinHeight = 52, ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(44) }, new ColumnDefinition() } };
        general.Children.Add(iconSlot);
        Grid.SetColumn(generalText, 1);
        general.Children.Add(generalText);
        Grid.SetColumnSpan(_linkRole, 2);
        general.Children.Add(_linkRole);
        var generalSection = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        var generalHead = SectionTitle("General access");
        generalHead.Margin = new Thickness(0, 0, 0, 6);
        generalSection.Children.Add(generalHead);
        generalSection.Children.Add(general);

        _copyLabels[0] = CopyLabel("link-2", "Copy link");
        _copyLabels[1] = CopyLabel("check", "Link copied");
        _copyLabels[2] = CopyLabel("circle-alert", "Couldn’t copy");
        foreach (var label in _copyLabels) { _copyStack.Children.Add(label); label.Opacity = 0; label.RenderTransform = new TranslateTransform(); }
        _copyLabels[0].Opacity = 1;
        _copyStack.VerticalAlignment = VerticalAlignment.Center;
        _copy.Child = _copyStack;
        _copy.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _copy.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _copy.MouseLeftButtonUp += (_, _) => CopyLink();
        _copy.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { CopyLink(); e.Handled = true; } };
        var doneText = ArcText.Block("Done", 14, 19.6, "Background", true);
        doneText.VerticalAlignment = VerticalAlignment.Center;
        _doneButton.Child = doneText;
        _doneButton.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Foreground");
        _doneButton.MouseLeftButtonUp += (_, _) => _done?.Invoke(this, EventArgs.Empty);
        _doneButton.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { _done?.Invoke(this, EventArgs.Empty); e.Handled = true; } };
        var footer = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        footer.Children.Add(Ringed(_copy, _focusRingCopy, 18));
        footer.Children.Add(Ringed(_doneButton, _focusRingDone, 18));
        var inner = new StackPanel();
        _title.Margin = new Thickness(0, 0, 0, 20);
        inner.Children.Add(_title);
        inner.Children.Add(inviteArea);
        inner.Children.Add(peopleSection);
        inner.Children.Add(generalSection);
        inner.Children.Add(footer);
        // Suggestions float under the field, over everything below it.
        _suggestions.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _suggestions.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _suggestions.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _optionHighlight.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var optionLayers = new Grid();
        optionLayers.Children.Add(_optionHighlight);
        optionLayers.Children.Add(_options);
        _suggestions.Child = optionLayers;
        _suggestLayer.Children.Add(_suggestions);
        Panel.SetZIndex(_suggestLayer, 5);
        var layers = new Grid();
        layers.Children.Add(inner);
        layers.Children.Add(_suggestLayer);
        _root.Child = layers;
        _host.Child = _root;
        _root.SizeChanged += (_, _) => { Canvas.SetLeft(_suggestions, 0); PlaceSuggestions(); };
        _menuFace.Child = _menuItems;
        _menuFace.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        _menuFace.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _menuFace.SetResourceReference(EffectProperty, "ArcWpf.Shadow.Floating");
        _menuFace.Margin = new Thickness(24, 8, 24, 48);
        _menuFace.PreviewKeyDown += (_, e) => { if (HandleMenuKey(e.Key)) { e.Handled = true; } };
        _menuFace.PreviewTextInput += (_, e) => { if (e.Text.Length == 1) { TypeAhead(e.Text[0]); e.Handled = true; } };
        _menu.Child = _menuFace;
        _menu.CustomPopupPlacementCallback = PlaceMenu;
        _menu.Closed += (_, _) => { var owner = _menuOwner; _menuOwner = null; owner?.SetOpen(false); if (owner is not null && IsLoaded && Keyboard.FocusedElement is null or Border) { owner.Focus(); } };
        SyncTitle();
        SyncPeople();
        SyncAccess(false);
        SyncInvite();
        SyncFooter();
        PaintField();
    }

    private static readonly ControlTemplate BareTextBox = MakeBareTextBox();
    private static ControlTemplate MakeBareTextBox()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = host };
        template.Seal();
        return template;
    }

    private FrameworkElement CopyLabel(string icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new ArcDataGridIcon { Icon = icon, Size = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        glyph.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Foreground");
        row.Children.Add(glyph);
        row.Children.Add(ArcText.Block(text, 14, 19.6, "Foreground", true));
        return row;
    }

    private void SyncTitle() { _title.Text = $"Share “{Title}”"; }
    private void SyncFooter() { _doneButton.Visibility = _done is null ? Visibility.Collapsed : Visibility.Visible; }

    private void PaintField()
    {
        _field.BorderBrush = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush.Border"));
        if (_input.IsKeyboardFocusWithin) { _field.BorderBrush = new SolidColorBrush(OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.Foreground"), .25)); }
    }

    private void FitInput()
    {
        // The input takes what is left of the line; when too little is left it wraps below the chips.
        double width = _chipRow.ActualWidth;
        if (width <= 0) { return; }
        double used = 0;
        foreach (UIElement child in _chipRow.Children)
        {
            if (ReferenceEquals(child, _inputSlot)) { continue; }
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double next = child.DesiredSize.Width;
            used = used + next > width ? next : used + next;
        }
        double left = width - used;
        _inputSlot.Width = left >= 80 ? left : width;
    }

    // ---- Typing and chips ----

    private void OnTyped()
    {
        string text = _input.Text;
        _placeholder.Visibility = text.Length == 0 && _chips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_marked && text.Length > 0) { _marked = false; PaintChips(); }
        if (text.Contains(' ') && text.Contains('@')) { Commit(); return; }
        Suggest(text.Trim());
    }

    // Matches the start of any name part or the email; people who already have access or are already chips are left out.
    private void Suggest(string text)
    {
        _matches.Clear();
        if (text.Length > 0)
        {
            foreach (var contact in Directory ?? [])
            {
                if (Taken(contact.Email)) { continue; }
                bool hit = contact.Email.StartsWith(text, StringComparison.OrdinalIgnoreCase) || (contact.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part.StartsWith(text, StringComparison.OrdinalIgnoreCase));
                if (hit) { _matches.Add(contact); }
                if (_matches.Count == 5) { break; }
            }
        }
        _highlight = 0;
        RenderOptions();
    }

    private bool Taken(string email) => People.Any(member => string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase)) || _chips.Any(chip => string.Equals(chip.Contact.Email, email, StringComparison.OrdinalIgnoreCase));

    private void RenderOptions()
    {
        _options.Children.Clear();
        for (int i = 0; i < _matches.Count; i++)
        {
            var contact = _matches[i];
            int index = i;
            var row = new Grid { Height = 50.4, Background = Brushes.Transparent, Cursor = Cursors.Hand, ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(46) }, new ColumnDefinition() } };
            var face = Face(contact, 28);
            face.Margin = new Thickness(8, 0, 0, 0);
            face.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(face);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(ArcText.Block(contact.Display, 14, 18.2, "Foreground", true));
            text.Children.Add(ArcText.Block(contact.Email, 12, 15.6, "TextMuted"));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            row.MouseEnter += (_, _) => MoveHighlight(index);
            row.MouseLeftButtonDown += (_, e) => { AddContact(contact); e.Handled = true; };
            AutomationProperties.SetName(row, contact.Display);
            _options.Children.Add(row);
        }
        bool show = _matches.Count > 0;
        if (show != (_suggestions.Visibility == Visibility.Visible))
        {
            _suggestions.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show && !Reduced)
            {
                var scale = (ScaleTransform)_suggestions.RenderTransform;
                _suggestions.Opacity = 0;
                scale.ScaleX = scale.ScaleY = .97;
                Tween.To(_suggestions, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
                Tween.To(scale, ScaleTransform.ScaleXProperty, 1, Tween.Fast, ArcEasing.Standard);
                Tween.To(scale, ScaleTransform.ScaleYProperty, 1, Tween.Fast, ArcEasing.Standard);
            }
        }
        _optionHighlight.BeginAnimation(HeightProperty, null);
        ((TranslateTransform)_optionHighlight.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
        ((TranslateTransform)_optionHighlight.RenderTransform).Y = _highlight * 50.4;
        _input.SetValue(AutomationProperties.ItemStatusProperty, _matches.Count > 0 ? _matches[_highlight].Display : "");
        PlaceSuggestions();
    }

    private void PlaceSuggestions()
    {
        if (_suggestions.Visibility != Visibility.Visible || !_field.IsLoaded) { return; }
        var at = _field.TransformToVisual(_suggestLayer).Transform(new Point(0, _field.ActualHeight));
        Canvas.SetLeft(_suggestions, at.X);
        Canvas.SetTop(_suggestions, at.Y + 6);
        _suggestions.Width = _field.ActualWidth;
    }

    // The highlight slides between rows as one element.
    private void MoveHighlight(int index)
    {
        _highlight = Math.Clamp(index, 0, Math.Max(0, _matches.Count - 1));
        var move = (TranslateTransform)_optionHighlight.RenderTransform;
        if (Reduced) { move.BeginAnimation(TranslateTransform.YProperty, null); move.Y = _highlight * 50.4; }
        else { Tween.Spring(move, TranslateTransform.YProperty, _highlight * 50.4, .26); }
        if (_matches.Count > 0) { _input.SetValue(AutomationProperties.ItemStatusProperty, _matches[_highlight].Display); }
    }

    private void CloseSuggestions() { _matches.Clear(); RenderOptions(); }

    internal bool HandleInputKey(Key key)
    {
        string text = _input.Text;
        switch (key)
        {
            case Key.Enter:
                if (_matches.Count > 0) { AddContact(_matches[_highlight]); return true; }
                if (text.Trim().Length > 0) { Commit(); return true; }
                if (_chips.Count > 0) { _ = Send(); return true; }
                return false;
            case Key.Tab:
                if (text.Trim().Length == 0) { return false; }
                Commit();
                return true;
            case Key.Space:
                if (!text.Contains('@')) { return false; }
                Commit();
                return true;
            case Key.OemComma or Key.OemSemicolon:
                Commit();
                return true;
            case Key.Down:
                if (_matches.Count == 0) { return false; }
                MoveHighlight((_highlight + 1) % _matches.Count);
                return true;
            case Key.Up:
                if (_matches.Count == 0) { return false; }
                MoveHighlight((_highlight - 1 + _matches.Count) % _matches.Count);
                return true;
            case Key.Escape:
                if (text.Length == 0 && _matches.Count == 0) { return false; }
                _input.Text = "";
                CloseSuggestions();
                return true;
            case Key.Back:
                // In an empty field, the first press marks the last chip and the second removes it.
                if (text.Length > 0 || _chips.Count == 0) { return false; }
                if (!_marked) { _marked = true; PaintChips(); return true; }
                _marked = false;
                RemoveChip(_chips[^1]);
                return true;
            default:
                return false;
        }
    }

    // What you typed becomes a chip: a directory person, an email, or an invalid address in red.
    private void Commit()
    {
        string text = _input.Text.Trim().TrimEnd(',', ';').Trim();
        if (text.Length == 0) { _input.Text = ""; return; }
        var known = (Directory ?? []).FirstOrDefault(contact => string.Equals(contact.Name, text, StringComparison.OrdinalIgnoreCase) || string.Equals(contact.Email, text, StringComparison.OrdinalIgnoreCase));
        if (known is not null) { AddContact(known); return; }
        if (Taken(text)) { _input.Text = ""; CloseSuggestions(); Announcer.Say(this, $"{text} already has access"); return; }
        AddChip(new ShareAccessContact { Email = text }, EmailShape.IsMatch(text));
    }

    private void AddContact(ShareAccessContact contact)
    {
        if (Taken(contact.Email)) { _input.Text = ""; CloseSuggestions(); return; }
        AddChip(contact, true);
    }

    private void AddChip(ShareAccessContact contact, bool valid)
    {
        var chip = new Chip { Contact = contact, Valid = valid, View = new Border() };
        var view = BuildChip(chip);
        chip.View = view;
        _chips.Add(chip);
        _chipRow.Children.Insert(_chipRow.Children.IndexOf(_inputSlot), view);
        _input.Text = "";
        CloseSuggestions();
        Announcer.Say(this, valid ? $"{contact.Display} added" : $"{contact.Display} isn't a valid email address");
        if (!Reduced && IsLoaded)
        {
            // Chips pop in from 80 percent scale on the snappy spring.
            var scale = new ScaleTransform(.8, .8);
            view.RenderTransformOrigin = new Point(.5, .5);
            view.RenderTransform = scale;
            view.Opacity = 0;
            Tween.Spring(scale, ScaleTransform.ScaleXProperty, 1, .26);
            Tween.Spring(scale, ScaleTransform.ScaleYProperty, 1, .26);
            Tween.To(view, OpacityProperty, 1, Tween.Instant, ArcEasing.Standard);
        }
        ChipsChanged();
    }

    private void RemoveChip(Chip chip)
    {
        _chips.Remove(chip);
        _chipRow.Children.Remove(chip.View);
        Announcer.Say(this, $"{chip.Contact.Display} removed");
        ChipsChanged();
    }

    private void ChipsChanged()
    {
        FitInput();
        _inviteFailed = false;
        OnTyped();
        SyncInvite();
    }

    private FrameworkElement BuildChip(Chip chip)
    {
        bool invalid = !chip.Valid;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (invalid)
        {
            var alert = new ArcDataGridIcon { Icon = "circle-alert", Size = 16, Margin = new Thickness(2, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            alert.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.ArcDanger");
            row.Children.Add(alert);
        }
        else
        {
            var face = Face(chip.Contact, 20);
            face.Margin = new Thickness(0, 0, 6, 0);
            face.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(face);
        }
        var text = ArcText.Block(chip.Contact.Display, 14, 19.6, invalid ? "ArcDanger" : "Foreground");
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.MaxWidth = 240;
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        var remove = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(6, 0, 0, 0), Opacity = .6, Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null, VerticalAlignment = VerticalAlignment.Center };
        var cross = new ArcDataGridIcon { Icon = "x", Size = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        cross.SetResourceReference(ArcDataGridIcon.StrokeProperty, invalid ? "ArcWpf.Brush.ArcDanger" : "ArcWpf.Brush.Foreground");
        remove.Child = cross;
        remove.MouseEnter += (_, _) => remove.Opacity = 1;
        remove.MouseLeave += (_, _) => remove.Opacity = .6;
        remove.MouseLeftButtonDown += (_, e) => { RemoveChip(chip); _input.Focus(); e.Handled = true; };
        remove.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { RemoveChip(chip); _input.Focus(); e.Handled = true; } };
        AutomationProperties.SetName(remove, $"Remove {chip.Contact.Display}");
        row.Children.Add(remove);
        var box = new Border { Height = 30, CornerRadius = new CornerRadius(15), Padding = new Thickness(invalid ? 7 : 5, 0, 4, 0), Margin = new Thickness(0, 0, 6, 0), Child = row, ToolTip = chip.Contact.Email };
        chip.Face = box;
        PaintChip(chip, false);
        return box;
    }

    private void PaintChips() { for (int i = 0; i < _chips.Count; i++) { PaintChip(_chips[i], _marked && i == _chips.Count - 1); } }

    private void PaintChip(Chip chip, bool marked)
    {
        if (chip.Face is not { } face) { return; }
        var danger = Tween.Resolve(this, "ArcWpf.Brush.ArcDanger");
        face.Background = new SolidColorBrush(chip.Valid ? Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted") : OkColor.Alpha(danger, .1));
        face.BorderThickness = marked ? new Thickness(1) : new Thickness(0);
        face.BorderBrush = marked ? new SolidColorBrush(chip.Valid ? Tween.Resolve(this, "ArcWpf.Brush.Foreground") : danger) : null;
    }

    private void SyncInvite()
    {
        var invalid = _chips.FirstOrDefault(chip => !chip.Valid);
        string message = _inviteFailed ? "Couldn’t send the invites. Try again." : invalid is null ? "" : $"“{invalid.Contact.Display}” isn’t a valid email address";
        bool hasError = message.Length > 0;
        if (hasError) { _errorText.Text = message; }
        else if (_error.ActualHeight <= 0) { _errorText.Text = ""; }
        Expand(_error, hasError ? 22.8 : 0, () => { if (!hasError) { _errorText.Text = ""; } });
        bool shown = _chips.Count > 0;
        _inviteShown = shown;
        Expand(_inviteRowWrap, shown ? 48 : 0);
        _inviteText.Text = _sending ? "Sending" : _chips.Count > 1 ? $"Invite {_chips.Count}" : "Invite";
        _inviteButton.Opacity = CanSend || _sending ? 1 : .4;
        _inviteButton.IsHitTestVisible = CanSend;
        _inviteButton.Focusable = CanSend;
        _input.SetValue(AutomationProperties.HelpTextProperty, message);
    }

    // Height changes on the smooth spring, which never overshoots.
    private void Expand(FrameworkElement element, double height, Action? done = null)
    {
        if (Reduced || !IsLoaded)
        {
            element.BeginAnimation(HeightProperty, null);
            element.Height = height;
            done?.Invoke();
            return;
        }
        Tween.Spring(element, HeightProperty, height, .4, done: done);
    }

    private async Task Send()
    {
        if (!CanSend) { return; }
        var people = _chips.Select(chip => chip.Contact).ToList();
        _sending = true;
        _inviteFailed = false;
        SyncInvite();
        try
        {
            if (InviteHandler is { } handler) { await handler(people, _inviteAs); }
        }
        catch (Exception)
        {
            // If it rejects, the chips stay and a message explains it.
            _sending = false;
            _inviteFailed = true;
            SyncInvite();
            Announcer.Say(this, "Couldn’t send the invites");
            return;
        }
        _sending = false;
        foreach (var chip in _chips.ToList()) { _chipRow.Children.Remove(chip.View); }
        _chips.Clear();
        var next = People.ToList();
        foreach (var person in people)
        {
            var member = new ShareAccessMember { Id = $"invite-{_nextId++}-{person.Email}", Email = person.Email, Name = person.Name, Source = person.Source, Role = _inviteAs, Pending = true };
            _welcomed.Add(member.Id);
            next.Add(member);
        }
        Announcer.Say(this, people.Count == 1 ? $"Invited {people[0].Display}" : $"Invited {people.Count} people");
        Push(next);
        FitInput();
        OnTyped();
        SyncInvite();
    }

    // ---- People ----

    private void Push(List<ShareAccessMember> next)
    {
        SetCurrentValue(MembersProperty, new ShareAccessMemberCollectionView(next));
        MembersChanged?.Invoke(this, new ShareMembersChangedEventArgs(next));
    }

    private sealed class ShareAccessMemberCollectionView(IList<ShareAccessMember> items) : Collection<ShareAccessMember>(items);

    private void SyncPeople()
    {
        if (!_built) { return; }
        var members = People;
        _peopleCount.Set(members.Count.ToString(), members.Count >= _rows.Count ? 1 : -1);
        foreach (var row in _rows.Where(row => members.All(member => member.Id != row.Member.Id)).ToList()) { FoldRow(row); }
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var row = _rows.FirstOrDefault(item => item.Member.Id == member.Id);
            if (row is null)
            {
                row = BuildRow(member);
                _rows.Insert(Math.Min(i, _rows.Count), row);
                _people.Children.Insert(Math.Min(_rows.IndexOf(row), _people.Children.Count), row.Wrap);
                bool welcome = _welcomed.Remove(member.Id);
                if (IsLoaded && !Reduced) { Unfold(row, welcome); }
            }
            else
            {
                row.Member = member;
                row.Meta.Text = member.Pending ? $"Invited · {member.Email}" : member.Email;
                row.Trigger?.Set(RoleLabel(member.Role));
            }
        }
    }

    private FrameworkElement Face(ShareAccessContact contact, double size)
    {
        var face = new Avatar { DisplayName = contact.Display, Source = contact.Source, Width = size, Height = size, IsHitTestVisible = false, Focusable = false };
        face.FontSize = size * .36;
        return face;
    }

    private PersonRow BuildRow(ShareAccessMember member)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(44) }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        var face = Face(member, 32);
        face.VerticalAlignment = VerticalAlignment.Center;
        face.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(face);
        var name = new TextBlock { FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        name.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        name.Inlines.Add(new System.Windows.Documents.Run(member.Display));
        if (member.Id == SelfId)
        {
            var you = new System.Windows.Documents.Run(" (you)") { FontWeight = FontWeights.Normal };
            you.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            name.Inlines.Add(you);
        }
        var meta = ArcText.Block(member.Pending ? $"Invited · {member.Email}" : member.Email, 12, 16.8, "TextMuted");
        meta.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(name);
        text.Children.Add(meta);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        RoleTrigger? trigger = null;
        if (member.Role == ShareRole.Owner)
        {
            var owner = ArcText.Block("Owner", 14, 19.6, "TextMuted");
            owner.Margin = new Thickness(12, 0, 10, 0);
            owner.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(owner, 2);
            grid.Children.Add(owner);
        }
        else
        {
            trigger = new RoleTrigger(this, FontWeights.Normal, _secondary, () => $"Role for {member.Display}: {RoleLabel(People.FirstOrDefault(item => item.Id == member.Id)?.Role ?? member.Role)}") { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            trigger.Set(RoleLabel(member.Role));
            Grid.SetColumn(trigger, 2);
            grid.Children.Add(trigger);
        }
        var body = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(10, 8, 6, 8), Child = grid, Background = Brushes.Transparent };
        var wrap = new Grid { ClipToBounds = true };
        wrap.Children.Add(body);
        var row = new PersonRow { Member = member, Wrap = wrap, Body = body, Meta = meta, Trigger = trigger };
        if (trigger is not null) { trigger.Clicked = () => OpenPersonMenu(row); }
        AutomationProperties.SetName(wrap, $"{member.Display}, {member.Email}");
        return row;
    }

    // New people expand into the list with an accent tint that fades over a second.
    private void Unfold(PersonRow row, bool welcome)
    {
        row.Wrap.Height = 0;
        Tween.Spring(row.Wrap, HeightProperty, RowHeight, .4, done: () => { row.Wrap.BeginAnimation(HeightProperty, null); row.Wrap.Height = double.NaN; });
        if (!welcome) { return; }
        var tint = new SolidColorBrush(OkColor.Alpha(Tween.Resolve(this, "ArcWpf.Brush.Accent"), .1));
        row.Body.Background = tint;
        tint.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(OkColor.Alpha(tint.Color, 0), TimeSpan.FromSeconds(1)));
    }

    // A removed row folds its height to zero while the rows below slide up.
    private void FoldRow(PersonRow row)
    {
        _rows.Remove(row);
        if (Reduced || !IsLoaded) { _people.Children.Remove(row.Wrap); return; }
        row.Wrap.Height = row.Wrap.ActualHeight;
        row.Wrap.IsHitTestVisible = false;
        Tween.Spring(row.Wrap, HeightProperty, 0, .4, done: () => _people.Children.Remove(row.Wrap));
        Tween.To(row.Wrap, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard);
    }

    private void SetRole(ShareAccessMember member, ShareRole role)
    {
        if (member.Role == role) { return; }
        var next = People.Select(item => item.Id != member.Id ? item : new ShareAccessMember { Id = item.Id, Email = item.Email, Name = item.Name, Source = item.Source, Pending = item.Pending, Role = role }).ToList();
        Announcer.Say(this, $"{member.Display}: {RoleLabel(role)}");
        Push(next);
    }

    private void Remove(ShareAccessMember member)
    {
        var list = People.ToList();
        int index = list.FindIndex(item => item.Id == member.Id);
        if (index < 0) { return; }
        _removed = (member, index);
        list.RemoveAt(index);
        _undoText.Text = $"Removed {member.Display}";
        Expand(_undoWrap, 48);
        Announcer.Say(this, $"Removed {member.Display}");
        Push(list);
    }

    private void Undo()
    {
        if (_removed is not { } removed) { return; }
        _removed = null;
        var list = People.ToList();
        list.Insert(Math.Min(removed.Index, list.Count), removed.Member);
        Expand(_undoWrap, 0);
        Announcer.Say(this, $"{removed.Member.Display} restored");
        Push(list);
    }

    // ---- General access ----

    private void OnAccess()
    {
        SyncAccess(true);
        Announcer.Say(this, Access == ShareGeneralAccess.Link ? "Anyone with the link can open" : "Only people with access can open");
        AccessChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLinkRole()
    {
        SyncAccess(true);
        LinkRoleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncAccess(bool animate)
    {
        if (!_built) { return; }
        bool open = Access == ShareGeneralAccess.Link;
        bool moving = animate && IsLoaded && !Reduced;
        _access.Set(AccessLabel(Access), open ? 1 : -1);
        _accessHint.Set(HintText, open ? 1 : -1);
        _linkRole.Set(RoleLabel(LinkRole));
        _lock.Opacity = open ? 0 : 1;
        _globe.Opacity = open ? 1 : 0;
        var spin = (RotateTransform)_accessIcon.RenderTransform;
        if (moving)
        {
            // The lock turns into a globe with a small rotation.
            spin.Angle = open ? -30 : 30;
            Tween.Spring(spin, RotateTransform.AngleProperty, 0, .26);
        }
        var move = (TranslateTransform)_linkRole.RenderTransform;
        if (open)
        {
            _linkRole.Visibility = Visibility.Visible;
            if (moving) { move.X = 16; _linkRole.Opacity = 0; Tween.Spring(move, TranslateTransform.XProperty, 0, .42); Tween.To(_linkRole, OpacityProperty, 1, Tween.Fast, ArcEasing.Standard); }
            else { move.BeginAnimation(TranslateTransform.XProperty, null); move.X = 0; _linkRole.BeginAnimation(OpacityProperty, null); _linkRole.Opacity = 1; }
        }
        else if (moving && _linkRole.Visibility == Visibility.Visible)
        {
            Tween.To(_linkRole, OpacityProperty, 0, Tween.Fast, ArcEasing.Standard, done: () => { if (Access != ShareGeneralAccess.Link) { _linkRole.Visibility = Visibility.Collapsed; } });
        }
        else { _linkRole.BeginAnimation(OpacityProperty, null); _linkRole.Opacity = 0; _linkRole.Visibility = Visibility.Collapsed; }
    }

    private void ResetInviteRole()
    {
        _inviteAs = DefaultInviteRole;
        if (_built) { _inviteRole.Set(RoleLabel(_inviteAs)); }
    }

    // ---- Copy ----

    private int _copyState;

    private void CopyLink()
    {
        bool ok = !string.IsNullOrEmpty(Link);
        if (ok)
        {
            // The clipboard can be held by another process.
            try { Clipboard.SetText(Link!); }
            catch (System.Runtime.InteropServices.ExternalException) { ok = false; }
        }
        FinishCopy(ok);
    }

    private void FinishCopy(bool ok)
    {
        ShowCopy(ok ? 1 : 2);
        Announcer.Say(this, ok ? "Link copied" : "Couldn’t copy");
        if (ok && Link is { } link) { LinkCopied?.Invoke(this, new ShareLinkCopiedEventArgs(link)); }
        _copyReset.Stop();
        _copyReset.Start();
    }

    // Swaps in place: the stack holds all three labels, so the width never changes.
    private void ShowCopy(int state)
    {
        _copyState = state;
        for (int i = 0; i < _copyLabels.Length; i++)
        {
            var label = _copyLabels[i];
            var move = (TranslateTransform)label.RenderTransform;
            double opacity = i == state ? 1 : 0;
            if (Reduced || !IsLoaded) { label.BeginAnimation(OpacityProperty, null); label.Opacity = opacity; move.BeginAnimation(TranslateTransform.YProperty, null); move.Y = 0; continue; }
            if (i == state) { move.Y = 6; }
            Tween.To(label, OpacityProperty, opacity, Tween.Fast, ArcEasing.Standard);
            Tween.To(move, TranslateTransform.YProperty, i == state ? 0 : -6, Tween.Fast, ArcEasing.Standard);
        }
    }

    // ---- Menus ----

    private void OpenInviteMenu()
    {
        var choices = Assignable.Select(role => new MenuChoice(RoleLabel(role), RoleHint(role), false, () => { _inviteAs = role; _inviteRole.Set(RoleLabel(role)); }, _inviteAs == role)).ToList();
        ShowMenu(_inviteRole, choices, MenuAlign.Start);
    }

    private void OpenPersonMenu(PersonRow row)
    {
        if (row.Trigger is null) { return; }
        var member = People.FirstOrDefault(item => item.Id == row.Member.Id) ?? row.Member;
        var choices = Assignable.Select(role => new MenuChoice(RoleLabel(role), RoleHint(role), false, () => SetRole(member, role), member.Role == role)).ToList();
        choices.Add(new MenuChoice("Remove access", null, true, () => Remove(member), false));
        ShowMenu(row.Trigger, choices, MenuAlign.End);
    }

    private void OpenAccessMenu()
    {
        var choices = new List<MenuChoice>
        {
            new("Restricted", "Only people with access can open", false, () => SetCurrentValue(AccessProperty, ShareGeneralAccess.Restricted), Access == ShareGeneralAccess.Restricted),
            new("Anyone with the link", "No sign in needed", false, () => SetCurrentValue(AccessProperty, ShareGeneralAccess.Link), Access == ShareGeneralAccess.Link),
        };
        ShowMenu(_access, choices, MenuAlign.Start);
    }

    private void OpenLinkMenu()
    {
        var choices = Assignable.Select(role => new MenuChoice(RoleLabel(role), RoleHint(role), false, () => SetCurrentValue(LinkRoleProperty, role), LinkRole == role)).ToList();
        ShowMenu(_linkRole, choices, MenuAlign.End);
    }

    private enum MenuAlign { Start, End }
    private MenuAlign _menuAlign;

    private void ShowMenu(RoleTrigger trigger, List<MenuChoice> choices, MenuAlign align)
    {
        if (_menuOwner == trigger && _menu.IsOpen) { _menu.IsOpen = false; return; }
        _menuAlign = align;
        _menuEntries.Clear();
        _menuItems.Children.Clear();
        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            int index = i;
            if (choice.Danger)
            {
                var separator = new Border { Height = 1, Margin = new Thickness(4, 5, 4, 6) };
                separator.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
                _menuItems.Children.Add(separator);
            }
            var row = new Grid();
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(ArcText.Block(choice.Label, 14, 18.2, choice.Danger ? "ArcDanger" : "Foreground"));
            if (choice.Hint is not null) { text.Children.Add(ArcText.Block(choice.Hint, 12, 15.6, "TextMuted")); }
            row.Children.Add(text);
            if (choice.Checked)
            {
                var check = new ArcDataGridIcon { Icon = "check", Size = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                check.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.Foreground");
                row.Children.Add(check);
            }
            var item = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 6, 10, 6), MinHeight = choice.Hint is null ? 40 : 45.8, Child = row, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            item.MouseEnter += (_, _) => Activate(index);
            item.MouseLeftButtonUp += (_, _) => RunMenu(index);
            AutomationProperties.SetName(item, choice.Label);
            _menuEntries.Add((item, choice));
            _menuItems.Children.Add(item);
        }
        _menuOwner = trigger;
        trigger.SetOpen(true);
        Activate(Math.Max(0, choices.FindIndex(choice => choice.Checked)));
        _menu.PlacementTarget = trigger;
        _menu.IsOpen = true;
        _menuFace.Focus();
        Keyboard.Focus(_menuFace);
    }

    private CustomPopupPlacement[] PlaceMenu(Size popup, Size target, Point offset)
    {
        // The face sits inside margins that make room for its shadow.
        double x = _menuAlign == MenuAlign.End ? target.Width - popup.Width + 24 : -24 + 4;
        double y = target.Height - 8 + 6;
        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal), new CustomPopupPlacement(new Point(x, -popup.Height + 48 - 6), PopupPrimaryAxis.Horizontal)];
    }

    private void Activate(int index)
    {
        _menuActive = index;
        var quiet = Tween.Resolve(this, "ArcWpf.Brush.SurfaceMuted");
        for (int i = 0; i < _menuEntries.Count; i++) { _menuEntries[i].View.Background = i == index ? new SolidColorBrush(quiet) : Brushes.Transparent; }
    }

    private void RunMenu(int index)
    {
        if (index < 0 || index >= _menuEntries.Count) { return; }
        var choice = _menuEntries[index].Choice;
        var owner = _menuOwner;
        _menu.IsOpen = false;
        choice.Run();
        if (owner is not null && owner.IsLoaded && !choice.Danger) { owner.Focus(); }
    }

    internal bool HandleMenuKey(Key key)
    {
        switch (key)
        {
            case Key.Down: Activate((_menuActive + 1) % _menuEntries.Count); return true;
            case Key.Up: Activate((_menuActive - 1 + _menuEntries.Count) % _menuEntries.Count); return true;
            case Key.Home: Activate(0); return true;
            case Key.End: Activate(_menuEntries.Count - 1); return true;
            case Key.Enter or Key.Space: RunMenu(_menuActive); return true;
            case Key.Escape: _menu.IsOpen = false; return true;
            case Key.Tab: _menu.IsOpen = false; return true;
            default: return false;
        }
    }

    // Typing a letter jumps to an option.
    private void TypeAhead(char letter)
    {
        int found = _menuEntries.FindIndex(entry => entry.Choice.Label.StartsWith(letter.ToString(), StringComparison.OrdinalIgnoreCase));
        if (found >= 0) { Activate(found); }
    }
}
