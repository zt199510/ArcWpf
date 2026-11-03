using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>A step of the checklist. Without <see cref="ActionLabel"/> it is checked off by hand.</summary>
public sealed class OnboardingStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ActionLabel { get; init; }
    /// <summary>Runs the step. Complete to check it off; throw to keep it open and show the exception's message.</summary>
    public Func<Task>? Action { get; init; }
}

/// <summary>
/// A getting started checklist for a new account or workspace. The ring and count follow every check, finished steps strike through
/// and fold into a completed row that opens again, and the next step opens and takes focus.
/// </summary>
[TemplatePart(Name = "PART_Root", Type = typeof(Decorator))]
public class OnboardingChecklist : Control
{
    // How long a checked step stays in place, struck through, before it folds into the completed row.
    internal const int SETTLE_MS = 720;
    private const double SMOOTH = .4, QUICK_FOLD = .32, HIDE_FOLD = .34, SNAPPY = .26, STANDARD = .24, FAST = .16, INSTANT = .12, MORPH = .42, CONSIDERED = .48;

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(OnboardingChecklist), new PropertyMetadata("", (d, _) => ((OnboardingChecklist)d).RefreshHeader(false)));
    public static readonly DependencyProperty DoneTitleProperty = DependencyProperty.Register(nameof(DoneTitle), typeof(string), typeof(OnboardingChecklist), new PropertyMetadata("You’re all set", (d, _) => ((OnboardingChecklist)d).RefreshHeader(false)));
    public static readonly DependencyProperty IsHiddenProperty = DependencyProperty.Register(nameof(IsHidden), typeof(bool), typeof(OnboardingChecklist), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((OnboardingChecklist)d).RefreshBody(true)));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string DoneTitle { get => (string)GetValue(DoneTitleProperty); set => SetValue(DoneTitleProperty, value); }
    public bool IsHidden { get => (bool)GetValue(IsHiddenProperty); set => SetValue(IsHiddenProperty, value); }

    public ObservableCollection<OnboardingStep> Steps { get; } = [];
    /// <summary>Ids finished at load. Later changes come through <see cref="CompletedChanged"/>.</summary>
    public IList<string> DefaultCompleted { get; set; } = [];
    public event EventHandler<IReadOnlyList<string>>? CompletedChanged;
    public IReadOnlyList<string> Completed => _done;

    private List<string> _done = [];
    private List<string> _settled = [];
    private string? _expanded;
    private readonly HashSet<string> _pending = [];
    private readonly Dictionary<string, string> _errors = [];
    private bool _groupOpen;
    private bool _initialized;
    private readonly Dictionary<string, DispatcherTimer> _timers = [];
    private readonly OwnedMotion _motion = new();

    private Decorator? _root;
    private ChecklistRing _ring = null!;
    private SwapText _titleSwap = null!, _hideSwap = null!;
    private RollingNumber _countNumber = null!, _groupNumber = null!;
    private TextBlock _countTail = null!;
    private Chevron _hideChevron = null!, _groupChevron = null!;
    private ArcChromeButton _hide = null!;
    private Fold _body = null!;
    private Border _bodyInner = null!;
    private StackPanel _open = null!, _closed = null!;
    private Fold _group = null!, _groupPanel = null!;
    private ArcChromeButton _groupToggle = null!;
    private readonly Dictionary<string, StepRow> _rows = [];
    private readonly Dictionary<string, CompletedRow> _closedRows = [];
    private string? _focusNext;
    private bool _focusFollow;

    static OnboardingChecklist() => DefaultStyleKeyProperty.OverrideMetadata(typeof(OnboardingChecklist), new FrameworkPropertyMetadata(typeof(OnboardingChecklist)));

    public OnboardingChecklist()
    {
        Unloaded += (_, _) => { foreach (var timer in _timers.Values) { timer.Stop(); } _timers.Clear(); };
        Steps.CollectionChanged += (_, _) => { if (_root is not null) { Initialize(); } };
    }

    internal bool Reduced => ArcMotion.GetLevel(this) != ArcMotionLevel.Full;
    internal bool Animates => IsLoaded && ArcMotion.GetLevel(this) != ArcMotionLevel.Off;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild("PART_Root") as Decorator;
        if (_root is null) { return; }
        _root.Child = Build();
        Initialize();
    }

    private void Initialize()
    {
        var ids = Steps.Select(step => step.Id).ToList();
        _done = ids.Where(DefaultCompleted.Contains).ToList();
        _settled = [.. _done];
        _expanded = ids.FirstOrDefault(id => !_done.Contains(id));
        _pending.Clear();
        _errors.Clear();
        _groupOpen = false;
        foreach (var timer in _timers.Values) { timer.Stop(); }
        _timers.Clear();
        _open.Children.Clear();
        _closed.Children.Clear();
        _rows.Clear();
        _closedRows.Clear();
        _initialized = true;
        Render(false);
    }

    /// <summary>Puts the checklist back to its starting state, as the demo's reset does.</summary>
    public void Reset() => Initialize();

    private UIElement Build()
    {
        _ring = new ChecklistRing { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };
        _titleSwap = new SwapText(16, 17.6, "Foreground", true, this);
        _countNumber = new RollingNumber(14, 19.6, "TextMuted", false, this);
        _countTail = ArcText.Block("", 14, 19.6, "TextMuted");
        var countRow = new StackPanel { Orientation = Orientation.Horizontal };
        countRow.Children.Add(_countNumber);
        countRow.Children.Add(_countTail);
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
        heading.Children.Add(_titleSwap);
        countRow.Margin = new Thickness(0, 1, 0, 0);
        heading.Children.Add(countRow);

        _hideSwap = new SwapText(14, 19.6, "TextSecondary", true, this) { VerticalAlignment = VerticalAlignment.Center };
        _hideChevron = new Chevron(this) { Margin = new Thickness(4, 0, 0, 0) };
        var hideRow = new StackPanel { Orientation = Orientation.Horizontal };
        hideRow.Children.Add(_hideSwap);
        hideRow.Children.Add(_hideChevron);
        _hide = new ArcChromeButton(18) { Height = 36, Padding = new Thickness(12, 0, 8, 0), Content = hideRow, VerticalAlignment = VerticalAlignment.Center };
        _hide.Roles = button => (button.IsMouseOver || button.IsPressed ? "SurfaceMuted" : null, null, button.IsMouseOver || button.IsPressed ? "Foreground" : "TextSecondary");
        _hide.Click += (_, _) => IsHidden = !IsHidden;
        BindInk(_hide, _hideSwap, _hideChevron);

        var header = new Grid { Margin = new Thickness(20) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(heading, 1);
        Grid.SetColumn(_hide, 2);
        header.Children.Add(_ring);
        header.Children.Add(heading);
        header.Children.Add(_hide);
        // Narrow cards keep the header on one line: the button keeps its spoken name and shows only the chevron.
        header.SizeChanged += (_, e) => ApplyNarrow(e.NewSize.Width + 40 + 2 <= 380);

        _open = new StackPanel();
        _closed = new StackPanel();
        _groupNumber = new RollingNumber(14, 19.6, "TextSecondary", true, this);
        var groupTail = ArcText.Block(" completed", 14, 19.6, "TextSecondary", true);
        var groupLabel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        groupLabel.Children.Add(_groupNumber);
        groupLabel.Children.Add(groupTail);
        _groupChevron = new Chevron(this) { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var groupFace = new Grid();
        groupFace.Children.Add(groupLabel);
        groupFace.Children.Add(_groupChevron);
        _groupToggle = new ArcChromeButton(26) { MinHeight = 44, Padding = new Thickness(64, 0, 12, 0), Margin = new Thickness(0, 2, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = groupFace };
        _groupToggle.Roles = button => (button.IsMouseOver ? "ChecklistRowHover" : null, null, button.IsMouseOver ? "Foreground" : "TextSecondary");
        _groupToggle.Click += (_, _) => { _groupOpen = !_groupOpen; Render(true); };
        BindInk(_groupToggle, groupTail, _groupNumber, _groupChevron);
        _groupPanel = new Fold(this, _closed);
        var groupColumn = new StackPanel();
        groupColumn.Children.Add(_groupToggle);
        groupColumn.Children.Add(_groupPanel);
        _group = new Fold(this, groupColumn);

        var list = new StackPanel();
        list.Children.Add(_open);
        list.Children.Add(_group);
        _open.PreviewKeyDown += OnListKey;
        _bodyInner = new Border { Padding = new Thickness(8), BorderThickness = new Thickness(0, 1, 0, 0), Child = list };
        _bodyInner.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        _body = new Fold(this, _bodyInner);

        var column = new StackPanel();
        column.Children.Add(header);
        column.Children.Add(_body);
        var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(34), Child = column };
        card.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        return card;

        void BindInk(ArcChromeButton button, params object[] targets)
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case SwapText swap: swap.Ink = button.Ink; break;
                    case Chevron chevron: chevron.Ink = button.Ink; break;
                    case RollingNumber number: number.Ink = button.Ink; break;
                    case TextBlock text: text.Foreground = button.Ink; break;
                }
            }
        }
    }

    private void ApplyNarrow(bool narrow)
    {
        _hideSwap.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        _hide.Width = narrow ? 36 : double.NaN;
        _hide.Padding = narrow ? new Thickness(0) : new Thickness(12, 0, 8, 0);
        _hideChevron.Margin = narrow ? new Thickness(0) : new Thickness(4, 0, 0, 0);
    }

    // ---- State transitions, the registry's reducer ----

    private string TitleOf(string id) => Steps.FirstOrDefault(step => step.Id == id)?.Title ?? "Step";
    private List<string> Ordered(IEnumerable<string> ids) { var set = ids.ToHashSet(); return Steps.Select(step => step.Id).Where(set.Contains).ToList(); }

    public void Check(string id)
    {
        if (!_done.Contains(id))
        {
            _done = Ordered([.. _done, id]);
            _pending.Remove(id);
            _errors.Remove(id);
            int total = Steps.Count;
            Announcer.Say(this, _done.Count == total ? $"All {total} steps done." : $"{TitleOf(id)} done. {_done.Count} of {total} complete.");
            CompletedChanged?.Invoke(this, _done);
            Render(true);
        }
        if (_timers.ContainsKey(id)) { return; }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SETTLE_MS) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _timers.Remove(id);
            Settle(id);
        };
        _timers[id] = timer;
        timer.Start();
    }

    internal void Settle(string id)
    {
        if (!_done.Contains(id) || _settled.Contains(id)) { return; }
        bool hadFocus = _rows.TryGetValue(id, out var row) && row.IsKeyboardFocusWithin;
        // The step that just finished hands the open panel to the next unfinished step, wrapping to the top.
        if (_expanded == id)
        {
            var order = Steps.Select(step => step.Id).ToList();
            int start = order.IndexOf(id);
            _expanded = Enumerable.Range(1, order.Count).Select(offset => order[(start + offset) % order.Count]).FirstOrDefault(other => !_done.Contains(other));
        }
        _settled = Ordered([.. _settled, id]);
        _focusFollow = hadFocus;
        Render(true);
    }

    public void Uncheck(string id, bool fromGroup = false)
    {
        if (_timers.Remove(id, out var timer)) { timer.Stop(); }
        if (!_done.Contains(id)) { return; }
        _done.Remove(id);
        _done = [.. _done];
        _settled.Remove(id);
        _settled = [.. _settled];
        _expanded ??= id;
        if (fromGroup) { _focusNext = id; }
        Announcer.Say(this, $"{TitleOf(id)} marked as not done. {_done.Count} of {Steps.Count} complete.");
        CompletedChanged?.Invoke(this, _done);
        Render(true);
    }

    internal async Task RunAsync(OnboardingStep step)
    {
        if (step.Action is null || _pending.Contains(step.Id) || _done.Contains(step.Id)) { return; }
        _pending.Add(step.Id);
        _errors.Remove(step.Id);
        Render(true);
        try
        {
            await step.Action();
            Check(step.Id);
        }
#pragma warning disable CA1031 // A step's failure, whatever it is, keeps the step open and shows its message, as the registry's catch does.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _pending.Remove(step.Id);
            string message = string.IsNullOrEmpty(exception.Message) ? "That didn’t work. Try again." : exception.Message;
            _errors[step.Id] = message;
            Announcer.Say(this, message);
            Render(true);
        }
    }

    private void Toggle(OnboardingStep step)
    {
        if (_done.Contains(step.Id)) { Uncheck(step.Id); } else { Check(step.Id); }
    }

    private void Expand(string id)
    {
        _expanded = _expanded == id ? null : id;
        Render(true);
    }

    // Up and Down walk the step titles, Home and End jump to the first and last.
    private void OnListKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not StepTitleButton current || e.Key is not (Key.Down or Key.Up or Key.Home or Key.End)) { return; }
        var titles = _open.Children.OfType<StepRow>().Where(row => !row.Leaving).Select(row => row.TitleButton).ToList();
        int index = titles.IndexOf(current);
        int next = e.Key switch { Key.Home => 0, Key.End => titles.Count - 1, _ => (index + (e.Key == Key.Down ? 1 : -1) + titles.Count) % titles.Count };
        e.Handled = true;
        titles[next].Focus();
    }

    // ---- Rendering ----

    private void RefreshHeader(bool animate)
    {
        if (_titleSwap is null) { return; }
        int total = Steps.Count, doneCount = _done.Count;
        bool allDone = total > 0 && doneCount == total;
        _titleSwap.Set(allDone ? DoneTitle : Title, [Title, DoneTitle], animate);
        _countNumber.Set(doneCount, animate);
        _countTail.Text = $" of {total} done";
        _ring.Set(doneCount, total, animate && Animates, Reduced);
        AutomationProperties.SetName(_ring, "Setup progress");
        AutomationProperties.SetHelpText(_ring, $"{doneCount} of {total} steps done");
    }

    private void RefreshBody(bool animate)
    {
        if (_body is null) { return; }
        bool hidden = IsHidden;
        _hideSwap.Set(hidden ? "Show" : "Hide", ["Hide", "Show"], animate);
        AutomationProperties.SetName(_hide, $"{(hidden ? "Show" : "Hide")} checklist");
        _hideChevron.Rotate(hidden ? 180 : 0, animate);
        _body.IsEnabled = !hidden;
        _body.Set(!hidden, animate, hidden ? HIDE_FOLD : SMOOTH, .05);
    }

    private void Render(bool animate)
    {
        if (!_initialized) { return; }
        animate = animate && Animates;
        RefreshHeader(animate);
        RefreshBody(animate);

        // Steps still open keep their place; settled ones fold away into the group.
        var open = Steps.Where(step => !_settled.Contains(step.Id)).ToList();
        foreach (var (id, row) in _rows.ToList())
        {
            if (open.Any(step => step.Id == id)) { continue; }
            _rows.Remove(id);
            row.Leave(animate, () => _open.Children.Remove(row));
        }
        for (int index = 0; index < open.Count; index++)
        {
            var step = open[index];
            if (!_rows.TryGetValue(step.Id, out var row))
            {
                row = new StepRow(this, step, () => Toggle(step), () => Expand(step.Id), () => _ = RunAsync(step));
                _rows[step.Id] = row;
                _open.Children.Insert(LiveIndex(_open, index), row);
                row.Enter(animate);
            }
            row.Update(_done.Contains(step.Id), _expanded == step.Id, _pending.Contains(step.Id), _errors.GetValueOrDefault(step.Id), animate);
        }

        var completed = Steps.Where(step => _settled.Contains(step.Id)).ToList();
        _group.Set(completed.Count > 0, animate, completed.Count > 0 ? SMOOTH : QUICK_FOLD, .04);
        _groupNumber.Set(completed.Count, animate);
        AutomationProperties.SetName(_groupToggle, $"{completed.Count} completed");
        _groupChevron.Rotate(_groupOpen ? 0 : 180, animate);
        _groupPanel.Set(_groupOpen && completed.Count > 0, animate, _groupOpen ? SMOOTH : QUICK_FOLD, .04);
        foreach (var (id, row) in _closedRows.ToList())
        {
            if (completed.Any(step => step.Id == id)) { continue; }
            _closedRows.Remove(id);
            row.Leave(animate, () => _closed.Children.Remove(row));
        }
        for (int index = 0; index < completed.Count; index++)
        {
            var step = completed[index];
            if (_closedRows.ContainsKey(step.Id)) { continue; }
            var row = new CompletedRow(this, step, () => Uncheck(step.Id, true));
            _closedRows[step.Id] = row;
            _closed.Children.Insert(LiveIndex(_closed, index), row);
            row.Enter(animate && _groupOpen);
        }
        Dispatcher.BeginInvoke(MoveFocus, DispatcherPriority.Input);
    }

    // The slot among rows that are not on their way out.
    private static int LiveIndex(StackPanel panel, int index)
    {
        int live = 0;
        for (int at = 0; at < panel.Children.Count; at++)
        {
            if (panel.Children[at] is IFoldingRow { Leaving: true }) { continue; }
            if (live == index) { return at; }
            live++;
        }
        return panel.Children.Count;
    }

    // Focus moves after the commit that mounts its target: the next step's action, or the step reopened from the completed row.
    private void MoveFocus()
    {
        if (_focusNext is { } id) { _focusNext = null; if (_rows.TryGetValue(id, out var reopened)) { reopened.CheckButton.Focus(); } return; }
        if (!_focusFollow) { return; }
        _focusFollow = false;
        if (_expanded is not null && _rows.TryGetValue(_expanded, out var next))
        {
            if (next.ActionButton is { } action) { action.Focus(); } else { next.TitleButton.Focus(); }
            return;
        }
        _groupToggle.Focus();
    }

    internal StepRow? RowForTest(string id) => _rows.GetValueOrDefault(id);
    internal bool GroupOpenForTest { get => _groupOpen; set { _groupOpen = value; Render(false); } }
    internal bool GroupVisible => _group.Target;
    internal string? ExpandedId => _expanded;
    internal IEnumerable<string> SettledIds => _settled;
    internal interface IFoldingRow { bool Leaving { get; } }

    // ---- Parts ----

    // Height springs on smooth and never overshoots; opacity crosses separately. Closing is a little quicker than opening.
    internal sealed class Fold : Border
    {
        private readonly OnboardingChecklist _owner;
        private readonly FrameworkElement _content;
        private readonly SpringDriver _driver;
        private readonly ControlSpring _height;
        private readonly OwnedMotion _motion = new();
        private bool _target = true;
        private bool _running;
        public bool Target => _target;
        public Action? Closed { get; set; }

        public Fold(OnboardingChecklist owner, FrameworkElement content)
        {
            _owner = owner;
            _content = content;
            Child = content;
            _driver = new SpringDriver(() => Height = Math.Max(0, _height!.Position)) { Settled = OnSettled };
            _height = _driver.Add(0, SMOOTH);
            Unloaded += (_, _) => { if (_driver.IsRunning) { _driver.Finish(); OnSettled(); } };
        }

        private double Natural()
        {
            double width = 0;
            for (var node = (DependencyObject)this; node is FrameworkElement element && width <= 0; node = VisualTreeHelper.GetParent(node)) { width = element.ActualWidth; }
            _content.Measure(new Size(width > 0 ? width : double.PositiveInfinity, double.PositiveInfinity));
            return _content.DesiredSize.Height;
        }

        public void Set(bool open, bool animate, double visualDuration, double delay)
        {
            if (open == _target) { return; }
            _target = open;
            _driver.Stop();
            if (!animate || _owner.Reduced)
            {
                // Reduced motion keeps the short fade on open and nothing else.
                _motion.Stop();
                Opacity = open ? 1 : 0;
                OnSettled();
                if (open && animate) { Opacity = 0; _motion.Double(this, OpacityProperty, 1, .15, true); }
                return;
            }
            double from = ActualHeight;
            if (_height.Position != from && _running) { from = _height.Position; }
            Visibility = Visibility.Visible;
            ClipToBounds = true;
            double to = open ? Natural() : 0;
            _height.Snap(from);
            Height = from;
            _running = true;
            _driver.Animate(_height, to, visualDuration);
            if (open) { _motion.Double(this, OpacityProperty, 1, STANDARD, true, enter: true, delay: delay); }
            else { _motion.Double(this, OpacityProperty, 0, FAST, true); }
        }

        private void OnSettled()
        {
            _running = false;
            ClearValue(HeightProperty);
            ClipToBounds = false;
            Visibility = _target ? Visibility.Visible : Visibility.Collapsed;
            if (!_target) { Closed?.Invoke(); }
        }

        internal void InitialClosed()
        {
            _target = false;
            Opacity = 0;
            Visibility = Visibility.Collapsed;
            ClearValue(HeightProperty);
        }
    }

    internal sealed class StepRow : Border, IFoldingRow
    {
        private readonly OnboardingChecklist _owner;
        private readonly OwnedMotion _motion = new();
        private readonly ThemedBrush _fill;
        private readonly Fold _fold;
        private readonly Fold _panel;
        private readonly Fold? _errorSlot;
        private readonly TextBlock? _errorText;
        private readonly ActionButton? _action;
        private readonly Border _inner;
        private bool _hovered, _current;

        public CheckCircle CheckButton { get; }
        public StepTitleButton TitleButton { get; }
        public ActionButton? ActionButton => _action;
        public bool Leaving { get; private set; }
        internal bool Done { get; private set; }
        internal bool PanelOpen => _panel.Target;

        public StepRow(OnboardingChecklist owner, OnboardingStep step, Action toggle, Action expand, Action run)
        {
            _owner = owner;
            _fill = new ThemedBrush(owner, _motion);
            CheckButton = new CheckCircle(owner, toggle, step.Title);
            TitleButton = new StepTitleButton(owner, step.Title, expand);

            var panelColumn = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            if (step.Description is { Length: > 0 } description)
            {
                var text = ArcText.Block(description, 14, 19.6, "TextSecondary");
                text.TextWrapping = TextWrapping.Wrap;
                // max-width: 46ch of a 14 sans at about .56em.
                text.MaxWidth = 360;
                text.HorizontalAlignment = HorizontalAlignment.Left;
                text.Margin = new Thickness(0, -4, 0, 0);
                panelColumn.Children.Add(text);
            }
            _errorText = ArcText.Block("", 14, 19.6, "Danger");
            _errorText.TextWrapping = TextWrapping.Wrap;
            _errorText.Margin = new Thickness(0, 8, 0, 0);
            _errorSlot = new Fold(owner, _errorText);
            _errorSlot.InitialClosed();
            panelColumn.Children.Add(_errorSlot);
            if (step.ActionLabel is { Length: > 0 } label)
            {
                _action = new ActionButton(owner, label, run) { Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                panelColumn.Children.Add(_action);
            }
            _panel = new Fold(owner, panelColumn);
            _panel.InitialClosed();

            var main = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            main.Children.Add(TitleButton);
            main.Children.Add(_panel);
            Grid.SetColumn(main, 1);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            CheckButton.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(CheckButton);
            grid.Children.Add(main);
            _inner = new Border { CornerRadius = new CornerRadius(26), Padding = new Thickness(12, 4, 12, 4), Background = _fill.Brush, Child = grid };
            _inner.MouseEnter += (_, _) => { _hovered = true; RefreshFill(true); };
            _inner.MouseLeave += (_, _) => { _hovered = false; RefreshFill(true); };
            _fold = new Fold(owner, _inner);
            Child = _fold;
            Loaded += (_, _) => RefreshFill(false);
            ThemeWatch.Add(this, () => RefreshFill(false));
        }

        private void RefreshFill(bool animate)
        {
            string? role = _current ? "SurfaceMuted" : _hovered ? "ChecklistRowHover" : null;
            _fill.To(role, animate, STANDARD);
        }

        public void Update(bool done, bool expanded, bool pending, string? error, bool animate)
        {
            Done = done;
            _current = expanded;
            RefreshFill(animate);
            CheckButton.Set(done, animate);
            TitleButton.SetDone(done, animate);
            AutomationProperties.SetItemStatus(TitleButton, expanded ? "expanded" : "collapsed");
            _panel.Set(expanded, animate, expanded ? SMOOTH : QUICK_FOLD, .04);
            if (_errorSlot is not null)
            {
                if (error is not null) { _errorText!.Text = error; }
                _errorSlot.Set(error is not null, animate, error is not null ? SMOOTH : QUICK_FOLD, .04);
            }
            _action?.SetState(done ? "done" : pending ? "pending" : "idle", animate);
        }

        public void Enter(bool animate)
        {
            if (!animate) { return; }
            _fold.InitialClosed();
            _fold.Set(true, true, SMOOTH, .04);
        }

        public void Leave(bool animate, Action removed)
        {
            Leaving = true;
            IsEnabled = false;
            if (!animate) { removed(); return; }
            _fold.Closed = removed;
            _fold.Set(false, true, QUICK_FOLD, 0);
        }
    }

    internal sealed class CompletedRow : Border, IFoldingRow
    {
        private readonly Fold _fold;
        public bool Leaving { get; private set; }

        public CompletedRow(OnboardingChecklist owner, OnboardingStep step, Action uncheck)
        {
            var check = new CheckCircle(owner, uncheck, step.Title);
            check.Set(true, false);
            var title = new TitleText(owner, step.Title);
            title.SetDone(true, false);
            title.VerticalAlignment = VerticalAlignment.Center;
            var slot = new Border { MinHeight = 40, Margin = new Thickness(12, 0, 0, 0), Child = title };
            Grid.SetColumn(slot, 1);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(check);
            grid.Children.Add(slot);
            var inner = new Border { CornerRadius = new CornerRadius(26), Padding = new Thickness(12, 4, 12, 4), Background = Brushes.Transparent, Child = grid };
            inner.MouseEnter += (_, _) => inner.SetResourceReference(BackgroundProperty, "ArcWpf.Brush.ChecklistRowHover");
            inner.MouseLeave += (_, _) => inner.Background = Brushes.Transparent;
            _fold = new Fold(owner, inner);
            Child = _fold;
        }

        public void Enter(bool animate)
        {
            if (!animate) { return; }
            _fold.InitialClosed();
            _fold.Set(true, true, SMOOTH, .04);
        }

        public void Leave(bool animate, Action removed)
        {
            Leaving = true;
            IsEnabled = false;
            if (!animate) { removed(); return; }
            _fold.Closed = removed;
            _fold.Set(false, true, QUICK_FOLD, 0);
        }
    }

    // A one pixel line of the text color that draws across, line by line when a title wraps; unchecking wipes it back quickly.
    internal sealed class TitleText : Grid
    {
        private static readonly DependencyProperty StrikeProperty = DependencyProperty.Register("Strike", typeof(double), typeof(TitleText), new PropertyMetadata(0d, (d, _) => ((TitleText)d)._line.InvalidateVisual()));
        private readonly OnboardingChecklist _owner;
        private readonly TextBlock _text;
        private readonly StrikeLine _line;
        private readonly OwnedMotion _motion = new();
        private readonly ThemedBrush _ink;
        private bool _done;
        internal double Strike => (double)GetValue(StrikeProperty);

        public TitleText(OnboardingChecklist owner, string title)
        {
            _owner = owner;
            _ink = new ThemedBrush(owner, _motion);
            _text = ArcText.Block(title, 14, 19.6, "Foreground", true);
            _text.TextWrapping = TextWrapping.Wrap;
            _text.Foreground = _ink.Brush;
            _line = new StrikeLine(this, _text, _ink.Brush);
            Children.Add(_text);
            Children.Add(_line);
            IsHitTestVisible = false;
            HorizontalAlignment = HorizontalAlignment.Left;
            Loaded += (_, _) => RefreshInk(false);
            ThemeWatch.Add(this, () => RefreshInk(false));
        }

        private void RefreshInk(bool animate) => _ink.To(_done ? "TextMuted" : "Foreground", animate, STANDARD);

        public void SetDone(bool done, bool animate)
        {
            if (done == _done) { return; }
            _done = done;
            RefreshInk(animate);
            bool motion = animate && !_owner.Reduced;
            if (done) { _motion.Double(this, StrikeProperty, 1, CONSIDERED, motion, enter: true, delay: .08); }
            else { _motion.Double(this, StrikeProperty, 0, FAST, motion); }
        }

        private sealed class StrikeLine(TitleText owner, TextBlock text, Brush ink) : FrameworkElement
        {
            protected override void OnRender(DrawingContext context)
            {
                double strike = owner.Strike;
                if (strike <= 0 || ActualWidth <= 0) { return; }
                // background-position 0 60% of the line's content box; the gradient runs through the fragments of a wrapped title in order.
                var measure = new FormattedText(text.Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                int lines = Math.Max(1, (int)Math.Round(ActualHeight / 19.6));
                double total = measure.WidthIncludingTrailingWhitespace, reach = strike * total, consumed = 0;
                for (int line = 0; line < lines && reach > consumed; line++)
                {
                    double width = line == lines - 1 ? Math.Max(0, total - consumed) : Math.Min(ActualWidth, total - consumed);
                    double drawn = Math.Min(width, reach - consumed);
                    context.DrawRectangle(ink, null, new Rect(0, line * 19.6 + 11, drawn, 1));
                    consumed += width;
                }
            }
        }
    }

    internal sealed class StepTitleButton : System.Windows.Controls.Button
    {
        private readonly TitleText _line;
        public StepTitleButton(OnboardingChecklist owner, string title, Action expand)
        {
            OverridesDefaultStyle = true;
            Cursor = Cursors.Hand;
            MinHeight = 40;
            FocusVisualStyle = ArcChromeButton.FocusFor(10);
            _line = new TitleText(owner, title);
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(BackgroundProperty, Brushes.Transparent);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(content);
            Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = factory };
            Content = _line;
            AutomationProperties.SetName(this, title);
            Click += (_, _) => expand();
        }
        public void SetDone(bool done, bool animate) => _line.SetDone(done, animate);
        internal double Strike => _line.Strike;
    }

    // The fill grows from the center while the tick draws from its short stroke; unchecking retracts both quickly.
    internal sealed class CheckCircle : System.Windows.Controls.Button
    {
        private static readonly Geometry Tick = Geometry.Parse("M6.75 11.25 9.75 14.25 15.25 8.25");
        private static readonly double TickLength = 4.2426 + 8.1394;
        private readonly OnboardingChecklist _owner;
        private readonly Border _visual;
        private readonly Ellipse _fill;
        private readonly ScaleTransform _fillScale = new(.6, .6);
        private readonly Path _mark;
        private readonly ScaleTransform _press = new(1, 1);
        private readonly OwnedMotion _motion = new();
        private readonly ThemedBrush _edge;
        private readonly SpringDriver _driver;
        private readonly ControlSpring _scale;
        private bool _checked;
        internal bool IsChecked => _checked;

        public CheckCircle(OnboardingChecklist owner, Action toggle, string label)
        {
            _owner = owner;
            OverridesDefaultStyle = true;
            Width = Height = 40;
            Cursor = Cursors.Hand;
            FocusVisualStyle = ArcChromeButton.FocusFor(20);
            AutomationProperties.SetName(this, label);
            _edge = new ThemedBrush(owner, _motion);
            _driver = new SpringDriver(() => { _fillScale.ScaleX = _fillScale.ScaleY = _scale!.Position; });
            _scale = _driver.Add(.6, SNAPPY);
            _fill = new Ellipse { Width = 22, Height = 22, Opacity = 0, RenderTransformOrigin = new Point(.5, .5), RenderTransform = _fillScale };
            _fill.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Accent");
            _mark = new Path { Data = Tick, StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashCap = PenLineCap.Round, Opacity = 0, Width = 22, Height = 22, Stretch = Stretch.None };
            _mark.SetResourceReference(Shape.StrokeProperty, "ArcWpf.Brush.AccentForeground");
            _mark.StrokeDashArray = new DoubleCollection([TickLength / 1.75, TickLength / 1.75]);
            _mark.StrokeDashOffset = TickLength / 1.75;
            // inset: -1.5 against a border Chromium snaps to one device pixel, so at 1x the fill and tick sit half a pixel up and left.
            var face = new Canvas();
            foreach (var part in new UIElement[] { _fill, _mark }) { Canvas.SetLeft(part, -1.5); Canvas.SetTop(part, -1.5); face.Children.Add(part); }
            _visual = new Border { Width = 22, Height = 22, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), BorderBrush = _edge.Brush, Child = face, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5), RenderTransform = _press };
            _visual.SetResourceReference(BackgroundProperty, "ArcWpf.Brush.Surface");
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(BackgroundProperty, Brushes.Transparent);
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(20));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            factory.AppendChild(content);
            Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = factory };
            Content = _visual;
            Click += (_, _) => toggle();
            MouseEnter += (_, _) => RefreshEdge(true);
            MouseLeave += (_, _) => RefreshEdge(true);
            Loaded += (_, _) => RefreshEdge(false);
            ThemeWatch.Add(this, () => RefreshEdge(false));
        }

        private void RefreshEdge(bool animate) => _edge.To(_checked ? "Accent" : IsMouseOver ? "TextMuted" : "BorderStrong", animate);

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
            double target = IsPressed ? .92 : 1, seconds = IsPressed ? .11 : .26;
            _motion.Double(_press, ScaleTransform.ScaleXProperty, target, seconds, animate, spring: !IsPressed);
            _motion.Double(_press, ScaleTransform.ScaleYProperty, target, seconds, animate, spring: !IsPressed);
        }

        public void Set(bool on, bool animate)
        {
            if (on == _checked && animate) { return; }
            _checked = on;
            RefreshEdge(animate);
            animate = animate && !_owner.Reduced;
            AutomationProperties.SetItemStatus(this, on ? "checked" : "not checked");
            double length = TickLength / 1.75;
            if (animate) { _driver.Animate(_scale, on ? 1 : .6, SNAPPY); }
            else { _driver.Stop(); _driver.Snap(_scale, on ? 1 : .6); }
            _motion.Double(_fill, OpacityProperty, on ? 1 : 0, on ? INSTANT : FAST, animate);
            if (on)
            {
                _motion.Double(_mark, OpacityProperty, 1, .05, animate, delay: .05);
                _motion.Double(_mark, Shape.StrokeDashOffsetProperty, 0, STANDARD, animate, enter: true, delay: .05);
            }
            else
            {
                _motion.Double(_mark, OpacityProperty, 0, FAST, animate);
                _motion.Double(_mark, Shape.StrokeDashOffsetProperty, length, FAST, animate);
            }
        }
    }

    internal sealed class ActionButton : ArcChromeButton
    {
        private readonly OnboardingChecklist _owner;
        private readonly Grid _icon = new() { Width = 16, Height = 16, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly ScaleTransform _scale = new(1, 1);
        private readonly OwnedMotion _press = new();
        private string _state = "";
        internal string State => _state;

        public ActionButton(OnboardingChecklist owner, string label, Action run) : base(18)
        {
            _owner = owner;
            Height = 36;
            Padding = new Thickness(16, 0, 12, 0);
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = _scale;
            Roles = _ => ("Foreground", null, "Background");
            var text = new TextBlock { Text = label, FontSize = 14, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Medium, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(text);
            row.Children.Add(_icon);
            Content = row;
            AutomationProperties.SetName(this, label);
            Click += (_, _) => run();
            MouseEnter += (_, _) => Effect = _state == "idle" && TryFindResource("ArcWpf.Shadow.Resting") is Effect shadow ? shadow : null;
            MouseLeave += (_, _) => Effect = null;
        }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            if (_state != "idle" && IsPressed) { return; }
            bool animate = ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
            double target = IsPressed ? .97 : 1, seconds = IsPressed ? .1 : .26;
            _press.Double(_scale, ScaleTransform.ScaleXProperty, target, seconds, animate, spring: !IsPressed);
            _press.Double(_scale, ScaleTransform.ScaleYProperty, target, seconds, animate, spring: !IsPressed);
        }

        public void SetState(string state, bool animate)
        {
            if (state == _state) { return; }
            _state = state;
            Cursor = state == "pending" ? Cursors.Wait : state == "done" ? Cursors.Arrow : Cursors.Hand;
            if (state != "idle") { Effect = null; }
            AutomationProperties.SetItemStatus(this, state == "pending" ? "busy" : state);
            FrameworkElement glyph = state switch
            {
                "pending" => new ActionSpinner(Ink),
                "done" => DoneTick(animate),
                _ => ArcText.Icon("arrow-right", 16, Ink),
            };
            bool reduced = _owner.Reduced;
            var motion = new OwnedMotion();
            foreach (FrameworkElement old in _icon.Children.OfType<FrameworkElement>().ToList())
            {
                if (!animate) { _icon.Children.Remove(old); continue; }
                motion.Double(old, OpacityProperty, 0, reduced ? .1 : .12, true, completed: () => _icon.Children.Remove(old));
                if (reduced) { continue; }
                var away = new ScaleTransform(1, 1);
                old.RenderTransformOrigin = new Point(.5, .5);
                old.RenderTransform = away;
                motion.Double(away, ScaleTransform.ScaleXProperty, .6, .12, true);
                motion.Double(away, ScaleTransform.ScaleYProperty, .6, .12, true);
                ArcEffects.BlurOut(motion, old, 2, .12);
            }
            _icon.Children.Add(glyph);
            if (!animate) { return; }
            glyph.Opacity = 0;
            motion.Double(glyph, OpacityProperty, 1, reduced ? .15 : FAST, true, enter: !reduced);
            if (reduced) { return; }
            var scale = new ScaleTransform(.6, .6);
            glyph.RenderTransformOrigin = new Point(.5, .5);
            glyph.RenderTransform = scale;
            motion.Double(scale, ScaleTransform.ScaleXProperty, 1, SNAPPY, true, spring: true);
            motion.Double(scale, ScaleTransform.ScaleYProperty, 1, SNAPPY, true, spring: true);
            ArcEffects.BlurIn(motion, glyph, 2, FAST);
        }

        private FrameworkElement DoneTick(bool animate)
        {
            // M5 12.5l4.5 4.5L19 7.5 in the 24 box, drawn from its short stroke, 2 wide.
            const double LENGTH = 6.364 + 13.435;
            var path = new Path { Data = Geometry.Parse("M5 12.5l4.5 4.5L19 7.5"), Stroke = Ink, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeDashCap = PenLineCap.Round, Width = 24, Height = 24 };
            path.StrokeDashArray = new DoubleCollection([LENGTH / 2, LENGTH / 2]);
            path.StrokeDashOffset = animate && !_owner.Reduced ? LENGTH / 2 : 0;
            if (path.StrokeDashOffset != 0) { new OwnedMotion().Double(path, Shape.StrokeDashOffsetProperty, 0, STANDARD, true, enter: true, delay: .05); }
            return new Viewbox { Width = 16, Height = 16, Child = path };
        }
    }

    // 14 box, a 1.5 ring with its right quarter transparent, one turn every .7 s.
    internal sealed class ActionSpinner : FrameworkElement
    {
        private readonly RotateTransform _turn = new();
        private readonly Brush _ink;
        public ActionSpinner(Brush ink)
        {
            _ink = ink;
            Width = Height = 14;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = _turn;
            Loaded += (_, _) =>
            {
                if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
                {
                    _turn.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(.7)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                }
            };
            Unloaded += (_, _) => _turn.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        protected override void OnRender(DrawingContext context)
        {
            const double R = 6.25;
            var center = new Point(7, 7);
            var figure = new PathFigure { StartPoint = Polar(center, R, 45) };
            figure.Segments.Add(new ArcSegment(Polar(center, R, 315), new Size(R, R), 0, true, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry([figure]);
            context.DrawGeometry(null, new Pen(_ink, 1.5), geometry);
        }
        private static Point Polar(Point center, double radius, double degrees)
        {
            double a = degrees * Math.PI / 180;
            return new Point(center.X + radius * Math.Sin(a), center.Y - radius * Math.Cos(a));
        }
    }

    // The arc fills on a spring as steps are checked; once every step is done a tick draws in its center.
    internal sealed class ChecklistRing : FrameworkElement
    {
        private static readonly DependencyProperty SweepProperty = DependencyProperty.Register("Sweep", typeof(double), typeof(ChecklistRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty FillOpacityProperty = DependencyProperty.Register("FillOpacity", typeof(double), typeof(ChecklistRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty TickProperty = DependencyProperty.Register("Tick", typeof(double), typeof(ChecklistRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        private static readonly DependencyProperty TickOpacityProperty = DependencyProperty.Register("TickOpacity", typeof(double), typeof(ChecklistRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
        private readonly SpringDriver _driver;
        private readonly ControlSpring _sweep;
        private readonly OwnedMotion _motion = new();
        private bool _complete;
        internal double Ratio => _sweep.Target;

        public ChecklistRing()
        {
            _driver = new SpringDriver(() => SetValue(SweepProperty, Math.Max(0, _sweep!.Position)));
            _sweep = _driver.Add(0, MORPH);
            ThemeWatch.Add(this, InvalidateVisual);
        }

        public void Set(int done, int total, bool animate, bool reduced)
        {
            double ratio = total > 0 ? (double)done / total : 0;
            bool complete = total > 0 && done == total;
            AutomationProperties.SetItemStatus(this, $"{done} of {total}");
            if (animate && !reduced) { _driver.Animate(_sweep, ratio, MORPH); }
            else { _driver.Stop(); _driver.Snap(_sweep, ratio); }
            _motion.Double(this, FillOpacityProperty, ratio > 0 ? 1 : 0, INSTANT, animate && !reduced);
            if (complete == _complete) { return; }
            _complete = complete;
            if (complete)
            {
                _motion.Double(this, TickOpacityProperty, 1, FAST, animate, enter: true);
                SetValue(TickProperty, reduced || !animate ? 1d : 0d);
                _motion.Double(this, TickProperty, 1, STANDARD, animate && !reduced, enter: true, delay: .12);
            }
            else
            {
                _motion.Double(this, TickOpacityProperty, 0, .12, animate);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new RingPeer(this);
        private sealed class RingPeer(ChecklistRing owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        }

        protected override void OnRender(DrawingContext context)
        {
            const double R = 17;
            var center = new Point(20, 20);
            if (TryFindResource("ArcWpf.Brush.Border") is Brush track) { context.DrawEllipse(null, new Pen(track, 3), center, R, R); }
            double sweep = (double)GetValue(SweepProperty), opacity = (double)GetValue(FillOpacityProperty);
            if (sweep > 0 && opacity > 0 && TryFindResource("ArcWpf.Brush.Accent") is Brush accent)
            {
                context.PushOpacity(opacity);
                var pen = new Pen(accent, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                if (sweep >= .9999) { context.DrawEllipse(null, pen, center, R, R); }
                else
                {
                    double angle = sweep * 360;
                    var figure = new PathFigure { StartPoint = new Point(20, 20 - R) };
                    double a = angle * Math.PI / 180;
                    figure.Segments.Add(new ArcSegment(new Point(20 + R * Math.Sin(a), 20 - R * Math.Cos(a)), new Size(R, R), 0, angle > 180, SweepDirection.Clockwise, true));
                    context.DrawGeometry(null, pen, new PathGeometry([figure]));
                }
                context.Pop();
            }
            double tick = (double)GetValue(TickProperty), tickOpacity = (double)GetValue(TickOpacityProperty);
            if (tick > 0 && tickOpacity > 0 && TryFindResource("ArcWpf.Brush.Accent") is Brush ink)
            {
                // M13.5 20.5 18 25l8.5-9.5, drawn from its short stroke.
                Point a = new(13.5, 20.5), b = new(18, 25), c = new(26.5, 15.5);
                double first = (b - a).Length, second = (c - b).Length, reach = tick * (first + second);
                var figure = new PathFigure { StartPoint = a };
                if (reach <= first) { figure.Segments.Add(new LineSegment(a + (b - a) * (reach / first), true)); }
                else
                {
                    figure.Segments.Add(new LineSegment(b, true));
                    figure.Segments.Add(new LineSegment(b + (c - b) * ((reach - first) / second), true));
                }
                context.PushOpacity(tickOpacity);
                context.DrawGeometry(null, new Pen(ink, 2.25) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, new PathGeometry([figure]));
                context.Pop();
            }
        }
    }

    internal sealed class Chevron : Border
    {
        private readonly OnboardingChecklist _owner;
        private readonly ArcDataGridIcon _icon;
        private readonly RotateTransform _rotate = new();
        private readonly SpringDriver _driver;
        private readonly ControlSpring _angle;
        public Brush Ink { set => _icon.Stroke = value; }
        internal double Angle => _angle.Target;

        public Chevron(OnboardingChecklist owner)
        {
            _owner = owner;
            Width = Height = 16;
            _icon = ArcText.Icon("chevron-up", 16, Brushes.Transparent);
            Child = _icon;
            RenderTransformOrigin = new Point(.5, .5);
            RenderTransform = _rotate;
            _driver = new SpringDriver(() => _rotate.Angle = _angle!.Position);
            _angle = _driver.Add(0, SNAPPY);
        }

        public void Rotate(double angle, bool animate)
        {
            if (animate && !_owner.Reduced) { _driver.Animate(_angle, angle, SNAPPY); }
            else { _driver.Stop(); _driver.Snap(_angle, angle); }
        }
    }

    // Every label the slot can show sits invisibly underneath, so a swap never changes the slot's size.
    internal sealed class SwapText : Grid
    {
        private readonly OnboardingChecklist _owner;
        private readonly double _size, _lineHeight;
        private readonly string _role;
        private readonly bool _medium;
        private readonly Grid _sizers = new();
        private TextBlock? _current;
        private string _text = "";
        private Brush? _ink;
        internal string Text => _text;

        public SwapText(double size, double lineHeight, string role, bool medium, OnboardingChecklist owner)
        {
            _size = size;
            _lineHeight = lineHeight;
            _role = role;
            _medium = medium;
            _owner = owner;
            Children.Add(_sizers);
        }

        public Brush Ink { set { _ink = value; if (_current is not null) { _current.Foreground = value; } } }

        private TextBlock Line(string text)
        {
            var block = ArcText.Block(text, _size, _lineHeight, _role, _medium);
            if (_ink is not null) { block.Foreground = _ink; }
            return block;
        }

        public void Set(string text, string[] options, bool animate)
        {
            _sizers.Children.Clear();
            foreach (string option in options.Distinct())
            {
                var sizer = Line(option);
                sizer.Visibility = Visibility.Hidden;
                _sizers.Children.Add(sizer);
            }
            if (text == _text && _current is not null) { return; }
            _text = text;
            var next = Line(text);
            var previous = _current;
            _current = next;
            Children.Add(next);
            if (previous is null || !animate) { if (previous is not null) { Children.Remove(previous); } return; }
            bool reduced = _owner.Reduced;
            var motion = new OwnedMotion();
            motion.Double(previous, OpacityProperty, 0, reduced ? .1 : .15, true, completed: () => Children.Remove(previous));
            next.Opacity = 0;
            motion.Double(next, OpacityProperty, 1, reduced ? .15 : .22, true, enter: !reduced);
            if (reduced) { return; }
            double em = _size * .3;
            var up = new TranslateTransform();
            previous.RenderTransform = up;
            motion.Double(up, TranslateTransform.YProperty, -em, .15, true);
            ArcEffects.BlurOut(motion, previous, 2, .15);
            var rise = new TranslateTransform(0, em);
            next.RenderTransform = rise;
            motion.Double(rise, TranslateTransform.YProperty, 0, .22, true, enter: true);
            ArcEffects.BlurIn(motion, next, 4, .22);
        }
    }

    // Digits turn in the direction the count moved, one column per place value, the width of a place springing as it appears.
    internal sealed class RollingNumber : StackPanel
    {
        private readonly double _size, _lineHeight;
        private readonly string _role;
        private readonly bool _medium;
        private readonly OnboardingChecklist _owner;
        private Brush? _ink;
        private int? _value;
        private readonly List<Grid> _places = [];
        internal int Value => _value ?? 0;

        public RollingNumber(double size, double lineHeight, string role, bool medium, OnboardingChecklist owner)
        {
            Orientation = Orientation.Horizontal;
            _size = size;
            _lineHeight = lineHeight;
            _role = role;
            _medium = medium;
            _owner = owner;
        }

        public Brush Ink { set => _ink = value; }

        private TextBlock Digit(char digit)
        {
            var block = ArcText.Block(digit.ToString(), _size, _lineHeight, _role, _medium);
            System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
            if (_ink is not null) { block.Foreground = _ink; }
            return block;
        }

        public void Set(int value, bool animate)
        {
            if (_value == value) { return; }
            int direction = _value is null || value > _value ? 1 : -1;
            _value = value;
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bool reduced = _owner.Reduced;
            var motion = new OwnedMotion();
            // Places are keyed from the right, so tens appear on the left.
            while (_places.Count > digits.Length)
            {
                var place = _places[0];
                _places.RemoveAt(0);
                if (animate && !reduced) { motion.Double(place, OpacityProperty, 0, FAST, true, completed: () => Children.Remove(place)); }
                else { Children.Remove(place); }
            }
            while (_places.Count < digits.Length)
            {
                var place = new Grid();
                _places.Insert(0, place);
                Children.Insert(Children.Count - _places.Count + 1, place);
            }
            for (int i = 0; i < digits.Length; i++)
            {
                var place = _places[i];
                string target = digits[i].ToString();
                var current = place.Children.OfType<TextBlock>().LastOrDefault();
                if (current?.Text == target) { continue; }
                var next = Digit(digits[i]);
                place.Children.Add(next);
                if (current is null) { continue; }
                if (!animate) { place.Children.Remove(current); continue; }
                double em = _size * .45 * direction;
                motion.Double(current, OpacityProperty, 0, reduced ? .1 : .14, true, completed: () => place.Children.Remove(current));
                next.Opacity = 0;
                motion.Double(next, OpacityProperty, 1, reduced ? .15 : FAST, true);
                if (reduced) { continue; }
                var up = new TranslateTransform();
                current.RenderTransform = up;
                motion.Double(up, TranslateTransform.YProperty, -em, .14, true);
                ArcEffects.BlurOut(motion, current, 2, .14);
                var rise = new TranslateTransform(0, em);
                next.RenderTransform = rise;
                motion.Double(rise, TranslateTransform.YProperty, 0, SNAPPY, true, spring: true);
                ArcEffects.BlurIn(motion, next, 2, FAST);
            }
        }
    }
}
