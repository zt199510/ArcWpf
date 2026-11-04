using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using NativeButton = System.Windows.Controls.Button;

namespace ArcWpf.Controls;

public enum ToolCallStatus { Running, Success, Error, Approval, Denied }

public sealed record ToolListItem(string? Lead, string Title, string? Tag = null);

/// <summary>What a call returned. Pick the shape that reads best.</summary>
public abstract record ToolOutput
{
    public sealed record List(IReadOnlyList<ToolListItem> Items) : ToolOutput;
    public sealed record Table(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows) : ToolOutput;
    public sealed record Json(string Value) : ToolOutput;
    public sealed record Text(string Value) : ToolOutput;
}

/// <summary>One tool call. Keep Id stable: a call that changes status keeps its row and its open state.</summary>
public sealed record ToolCallData(string Id, string ToolName, string Title, ToolCallStatus Status)
{
    public AiMark? Mark { get; init; }
    /// <summary>Phrasing per status; falls back to Title.</summary>
    public IReadOnlyDictionary<ToolCallStatus, string>? Titles { get; init; }
    public IReadOnlyList<KeyValuePair<string, string>>? Input { get; init; }
    public ToolOutput? Output { get; init; }
    /// <summary>Quiet meta before the duration. Derived from the output when left out.</summary>
    public string? Summary { get; init; }
    /// <summary>One line under the title: the error reason, or what allowing the call will do.</summary>
    public string? Message { get; init; }
    public double? DurationMs { get; init; }

    public string ShownTitle => Titles is not null && Titles.TryGetValue(Status, out var title) ? title : Title;
    public string? ShownSummary => Summary ?? Output switch
    {
        ToolOutput.List list => Count(list.Items.Count, "result"),
        ToolOutput.Table table => Count(table.Rows.Count, "row"),
        ToolOutput.Json json => ToolCallText.Fields(json.Value),
        _ => null,
    };

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}

public sealed class ToolCallEventArgs(ToolCallData call) : EventArgs { public ToolCallData Call { get; } = call; }

internal static class ToolCallText
{
    public static string? Fields(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => document.RootElement.EnumerateObject().Count() is var fields && fields == 1 ? "1 field" : $"{document.RootElement.EnumerateObject().Count()} fields",
                JsonValueKind.Array => document.RootElement.GetArrayLength() is var items && items == 1 ? "1 item" : $"{document.RootElement.GetArrayLength()} items",
                _ => null,
            };
        }
        catch (JsonException) { return null; }
    }

    public static string[] PrettyLines(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, Indented).Replace("\r\n", "\n").Split('\n');
        }
        catch (JsonException) { return json.Replace("\r\n", "\n").Split('\n'); }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string StatusWords(ToolCallData call)
    {
        string? summary = call.ShownSummary;
        string? time = call.DurationMs is { } ms ? AiUi.Duration(ms) : null;
        return call.Status switch
        {
            ToolCallStatus.Running => "Running",
            ToolCallStatus.Success => string.Join(", ", new[] { "Done", summary, time }.Where(part => !string.IsNullOrEmpty(part))),
            ToolCallStatus.Error => "Failed" + (string.IsNullOrEmpty(call.Message) ? "" : ", " + call.Message),
            ToolCallStatus.Approval => "Waiting for approval",
            _ => "Denied",
        };
    }

    public static string Copyable(ToolOutput? output) => output switch
    {
        ToolOutput.List list => string.Join("\n", list.Items.Select(item => string.Join(" ", new[] { item.Lead, item.Title, item.Tag }.Where(part => !string.IsNullOrEmpty(part))))),
        ToolOutput.Table table => string.Join("\n", new[] { string.Join("\t", table.Columns) }.Concat(table.Rows.Select(row => string.Join("\t", row)))),
        ToolOutput.Json json => string.Join("\n", PrettyLines(json.Value)),
        ToolOutput.Text text => text.Value,
        _ => "",
    };
}

/// <summary>One inline tool call: the tool's mark, a verb phrase, quiet meta and a status icon. Clicking the row opens its Input and Output.</summary>
public class ToolCallCard : Control
{
    public static readonly DependencyProperty CallProperty = DependencyProperty.Register(nameof(Call), typeof(ToolCallData), typeof(ToolCallCard), new FrameworkPropertyMetadata(null, (d, _) => ((ToolCallCard)d).Refresh()));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ToolCallCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ToolCallCard)d).OnOpen()));
    public static readonly DependencyProperty OutputTemplateProperty = DependencyProperty.Register(nameof(OutputTemplate), typeof(DataTemplate), typeof(ToolCallCard), new FrameworkPropertyMetadata(null, (d, _) => ((ToolCallCard)d)._panelKey = null));

    public ToolCallData? Call { get => (ToolCallData?)GetValue(CallProperty); set => SetValue(CallProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Draws a custom result for a successful call inside the Output well; the template receives the ToolCallData.</summary>
    public DataTemplate? OutputTemplate { get => (DataTemplate?)GetValue(OutputTemplateProperty); set => SetValue(OutputTemplateProperty, value); }

    public event EventHandler<ToolCallEventArgs>? ApproveRequested;
    public event EventHandler<ToolCallEventArgs>? DenyRequested;
    public event EventHandler<ToolCallEventArgs>? RetryRequested;
    /// <summary>The person opened or closed the row, not code.</summary>
    internal event EventHandler? UserToggled;

    private const int FoldAfter = 5;
    private const double Narrow = 440;

    private Border? _frame;
    private ToolbarButton? _toggle;
    private readonly Decorator _mark = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly FadeSwap _title = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _meta = AiUi.Line("", 12, 16.8, "TextMuted", false, false);
    private readonly StackPanel _inline = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly FadeSwap _status = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly TurningChevron _chevron = new();
    private readonly TextBlock _reason = AiUi.Wrapped("", 12, 16.8, "TextSecondary");
    private readonly WrapPanel _below = new() { Margin = new Thickness(38, 6, 12, 8) };
    private SpringHeightBox? _head, _panelClip;
    private readonly StackPanel _panel = new();
    private ToolCallData? _shown;
    private string? _panelKey;
    private bool _flush, _narrow;
    private AiMark? _shownMark;
    private bool _hasMark;
    private string? _titleKey;
    private ToolCallStatus? _statusKey;
    private readonly Dictionary<string, bool> _unfolded = [];

    static ToolCallCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolCallCard), new FrameworkPropertyMetadata(typeof(ToolCallCard)));

    public ToolCallCard()
    {
        SizeChanged += (_, e) => { if (e.WidthChanged && e.NewSize.Width < Narrow != _narrow) { _narrow = e.NewSize.Width < Narrow; _panelKey = null; Refresh(); } };
        Loaded += (_, _) => Refresh();
    }

    internal bool Flush { get => _flush; set { _flush = value; Chrome(); } }

    internal string TitleForTest => _titleKey ?? "";
    internal bool IsShimmeringForTest => _title.Current is TextShimmer { IsActive: true };
    internal string MetaForTest => _meta.Text;
    internal bool MetaShownForTest => _meta.Visibility == Visibility.Visible;
    internal string ReasonForTest => _reason.Visibility == Visibility.Visible ? _reason.Text : "";
    internal string NameForTest => _toggle is null ? "" : AutomationProperties.GetName(_toggle);
    internal NativeButton? ToggleForTest => _toggle;
    internal IEnumerable<NativeButton> ActionsForTest => _inline.Children.OfType<NativeButton>().Concat(_below.Children.OfType<NativeButton>());
    internal bool ActionsBelowForTest => _below.Children.Count > 0;
    internal bool PanelShownForTest => _panelClip?.Visibility == Visibility.Visible;
    internal StackPanel PanelForTest => _panel;
    internal string StatusKindForTest => _statusKey?.ToString() ?? "";
    internal double PanelIndentForTest => _panel.Margin.Left;
    internal Brush? FrameEdgeForTest => _frame?.BorderBrush;
    internal FrameworkElement? StatusViewForTest => _status.Current as FrameworkElement;
    internal NativeButton? MoreForTest => FindMore(_panel);
    private static NativeButton? FindMore(DependencyObject root)
    {
        if (root is NativeButton { Tag: "more" } more) { return more; }
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++) { if (FindMore(VisualTreeHelper.GetChild(root, i)) is { } hit) { return hit; } }
        return null;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        var head = new Grid { MinHeight = 40 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        _mark.HorizontalAlignment = HorizontalAlignment.Left;
        _mark.Margin = new Thickness(12, 0, 0, 0);
        head.Children.Add(_mark);
        Grid.SetColumn(_title, 1);
        head.Children.Add(_title);
        var aside = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        aside.Children.Add(_meta);
        aside.Children.Add(_inline);
        Grid.SetColumn(aside, 2);
        head.Children.Add(aside);
        Grid.SetColumn(_status, 3);
        head.Children.Add(_status);
        _chevron.HorizontalAlignment = HorizontalAlignment.Left;
        _chevron.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(_chevron, 5);
        head.Children.Add(_chevron);
        // The row is one button; actions sit over it in their own hit area.
        _toggle = new ToolbarButton(12) { Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, MinHeight = 40, Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, "Foreground") };
        _toggle.Click += (_, _) => { SetCurrentValue(IsOpenProperty, !IsOpen); UserToggled?.Invoke(this, EventArgs.Empty); };

        var rowStack = new Grid();
        rowStack.Children.Add(_toggle);
        head.IsHitTestVisible = true;
        head.Background = null;
        rowStack.Children.Add(head);
        // The head floats above the button so its buttons stay real; everything else in it lets the pointer through.
        foreach (UIElement child in head.Children) { if (!ReferenceEquals(child, aside)) { child.IsHitTestVisible = false; } }
        _meta.IsHitTestVisible = false;

        _reason.Margin = new Thickness(38, -4, 12, 6);
        var column = new StackPanel();
        column.Children.Add(rowStack);
        column.Children.Add(_reason);
        column.Children.Add(_below);
        _head = new SpringHeightBox(Tween.Smooth) { Child = column };
        _panel.Margin = new Thickness(38, 0, 12, 12);
        _panelClip = new SpringHeightBox(Tween.Smooth) { Child = _panel, Visibility = Visibility.Collapsed };
        var outer = new StackPanel();
        outer.Children.Add(_head);
        outer.Children.Add(_panelClip);
        _frame = new Border { Child = outer };
        host.Child = _frame;
        Chrome();
        Refresh();
    }

    private void Chrome()
    {
        if (_frame is null) { return; }
        _frame.CornerRadius = new CornerRadius(_flush ? 12 : 16);
        bool approval = Call?.Status == ToolCallStatus.Approval;
        _frame.BorderThickness = new Thickness(_flush && !approval ? 0 : 1);
        _frame.SetResourceReference(Border.BorderBrushProperty, approval ? "ArcWpf.Brush.AccentStrong" : "ArcWpf.Brush.Border");
        if (_flush) { _frame.Background = null; } else { _frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface"); }
    }

    private void Refresh()
    {
        if (_toggle is null || Call is not { } call) { return; }
        bool changed = _shown != call;
        bool statusChanged = _shown?.Status != call.Status;
        if (_shown is not null && statusChanged) { Announcer.Say(this, $"{call.ToolName}: {call.ShownTitle}. {ToolCallText.StatusWords(call)}"); _head?.MorphNext(); }
        _shown = call;
        Chrome();

        if (!_hasMark || _shownMark != call.Mark) { _hasMark = true; _shownMark = call.Mark; _mark.Child = AiUi.Mark(call.Mark, 16, call.ToolName); }

        string titleKey = call.ShownTitle + "|" + (call.Status == ToolCallStatus.Running);
        if (titleKey != _titleKey)
        {
            _titleKey = call.ShownTitle;
            if (call.Status == ToolCallStatus.Running) { _title.Set(new TextShimmer { Text = call.ShownTitle, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }); }
            else { _title.Set(AiUi.Line(call.ShownTitle, 14, 20)); }
            _titleKey = titleKey;
        }

        // Below 440 the meta keeps only the duration, and actions move under the reason so the title keeps its width.
        string? summary = call.ShownSummary, time = call.DurationMs is { } ms ? AiUi.Duration(ms) : null;
        string meta = call.Status == ToolCallStatus.Success ? _narrow ? time ?? "" : string.Join("  ·  ", new[] { summary, time }.Where(part => !string.IsNullOrEmpty(part))) : "";
        _meta.Text = meta;
        _meta.Visibility = meta.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_statusKey != call.Status) { _statusKey = call.Status; _status.Set(StatusIcon(call.Status)); }

        _reason.Text = call.Message ?? "";
        bool hasReason = !string.IsNullOrEmpty(call.Message) && call.Status is ToolCallStatus.Error or ToolCallStatus.Approval;
        _reason.Visibility = hasReason ? Visibility.Visible : Visibility.Collapsed;
        Actions(call);
        _chevron.Point(IsOpen);
        AutomationProperties.SetName(_toggle, $"{call.ToolName}: {call.ShownTitle} {ToolCallText.StatusWords(call)}");

        if (changed || _panelKey is null) { Panel(call); }
        OnOpen();
    }

    private FrameworkElement StatusIcon(ToolCallStatus status) => status switch
    {
        ToolCallStatus.Running => new AiSpinner(16),
        ToolCallStatus.Success => AiUi.Glyph(Lucide.Check, 16, "ArcSuccess"),
        ToolCallStatus.Error => AiUi.Glyph(Lucide.CircleAlert, 16, "ArcDanger"),
        ToolCallStatus.Approval => AiUi.Glyph(Lucide.Shield, 16, "Foreground"),
        _ => AiUi.Glyph(Lucide.Ban, 16, "TextMuted"),
    };

    private void Actions(ToolCallData call)
    {
        _inline.Children.Clear();
        _below.Children.Clear();
        var made = new List<NativeButton>();
        void Label(NativeButton button, string verb) => AutomationProperties.SetName(button, $"{verb}: {call.ShownTitle}");
        if (call.Status == ToolCallStatus.Error)
        {
            var retry = AiUi.Outline("Retry", Lucide.RotateCcw);
            Label(retry, "Retry");
            retry.Click += (_, e) => { e.Handled = true; RetryRequested?.Invoke(this, new ToolCallEventArgs(call)); };
            made.Add(retry);
        }
        else if (call.Status == ToolCallStatus.Approval)
        {
            var deny = AiUi.Outline("Deny");
            Label(deny, "Deny");
            deny.Click += (_, e) => { e.Handled = true; DenyRequested?.Invoke(this, new ToolCallEventArgs(call)); };
            var allow = AiUi.Solid("Allow");
            allow.Margin = new Thickness(6, 0, 0, 0);
            Label(allow, "Allow");
            allow.Click += (_, e) => { e.Handled = true; ApproveRequested?.Invoke(this, new ToolCallEventArgs(call)); };
            made.Add(deny);
            made.Add(allow);
        }
        foreach (var button in made) { (_narrow ? (Panel)_below : _inline).Children.Add(button); }
        _below.Visibility = _below.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpen()
    {
        if (_panelClip is null) { return; }
        _chevron.Point(IsOpen);
        if (IsOpen == (_panelClip.Visibility == Visibility.Visible && !_panelClip.IsCollapsing)) { return; }
        if (IsOpen)
        {
            if (_panelClip.IsCollapsing) { _panelClip.Cancel(); return; }
            if (Call is { } call) { Panel(call); }
            _panelClip.Visibility = Visibility.Visible;
            if (AiUi.Moving(this))
            {
                _panelClip.Reveal();
                _panelClip.Opacity = 0;
                Tween.To(_panelClip, OpacityProperty, 1, .16, ArcEasing.Standard);
            }
            return;
        }
        if (!AiUi.Moving(this)) { _panelClip.Visibility = Visibility.Collapsed; return; }
        Tween.To(_panelClip, OpacityProperty, 0, .1, ArcEasing.Standard);
        _panelClip.Collapse(() => { _panelClip.Visibility = Visibility.Collapsed; Tween.Set(_panelClip, OpacityProperty, 1); }, true);
    }

    // Input and Output exist only while open.
    private void Panel(ToolCallData call)
    {
        if (!IsOpen && _panelClip?.Visibility != Visibility.Visible) { _panelKey = null; return; }
        string key = $"{call.Id}|{call.Status}|{call.Output?.GetHashCode()}|{call.Input?.GetHashCode()}|{_narrow}|{string.Join(",", _unfolded.Keys)}";
        if (key == _panelKey) { return; }
        _panelKey = key;
        _panel.Margin = new Thickness(_narrow ? 12 : 38, 0, 12, 12);
        _panel.Children.Clear();
        if (call.Input is { Count: > 0 } input)
        {
            var list = new Grid { Margin = new Thickness(12, 10, 12, 10) };
            list.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            list.ColumnDefinitions.Add(new ColumnDefinition());
            for (int row = 0; row < input.Count; row++)
            {
                list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var name = AiUi.Mono(input[row].Key, 12, 18, "TextMuted");
                name.Margin = new Thickness(0, row == 0 ? 0 : 4, 16, 0);
                Grid.SetRow(name, row);
                var value = AiUi.Mono(input[row].Value, 12, 18);
                value.TextWrapping = TextWrapping.Wrap;
                value.TextTrimming = TextTrimming.None;
                value.Margin = new Thickness(0, row == 0 ? 0 : 4, 0, 0);
                Grid.SetRow(value, row);
                Grid.SetColumn(value, 1);
                list.Children.Add(name);
                list.Children.Add(value);
            }
            _panel.Children.Add(Section("Input", list, () => string.Join("\n", input.Select(pair => $"{pair.Key}: {pair.Value}"))));
        }
        var output = OutputView(call);
        if (output is not null)
        {
            var section = Section(call.Status == ToolCallStatus.Error ? "Error" : "Output", output, () => ToolCallText.Copyable(call.Output));
            if (_panel.Children.Count > 0) { section.Margin = new Thickness(0, 10, 0, 0); }
            _panel.Children.Add(section);
        }
        if (_panel.Children.Count == 0)
        {
            var empty = AiUi.Line(call.Status == ToolCallStatus.Running ? "Waiting for the result" : "No details", 12, 16.8, "TextMuted");
            empty.Margin = new Thickness(0, 2, 0, 0);
            _panel.Children.Add(empty);
        }
    }

    private static StackPanel Section(string label, UIElement body, Func<string> copy)
    {
        var head = new Grid { Height = 28 };
        head.Children.Add(AiUi.Line(label, 12, 16.8, "TextMuted"));
        var copyButton = new CopyAction(copy, $"Copy {label.ToLowerInvariant()}") { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, -6, 0) };
        head.Children.Add(copyButton);
        var well = new Border { CornerRadius = new CornerRadius(10), Child = body, ClipToBounds = true };
        well.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        var section = new StackPanel();
        section.Children.Add(head);
        section.Children.Add(well);
        return section;
    }

    private FrameworkElement? OutputView(ToolCallData call)
    {
        if (call.Status == ToolCallStatus.Success && OutputTemplate is { } template)
        {
            return new ContentPresenter { Content = call, ContentTemplate = template, Margin = new Thickness(12, 10, 12, 10) };
        }
        return call.Output switch
        {
            ToolOutput.List list => ListView(list),
            ToolOutput.Table table => TableView(table),
            ToolOutput.Json json => JsonView(json),
            ToolOutput.Text text => TextView(text),
            _ => null,
        };
    }

    private FrameworkElement Folded(int total, Func<int, FrameworkElement> rows, double rowHeight, Thickness padding)
    {
        var host = new StackPanel { Margin = padding };
        bool open = _unfolded.ContainsKey(_shown?.Id ?? "");
        int shown = open ? total : Math.Min(total, FoldAfter);
        host.Children.Add(rows(shown));
        if (total > FoldAfter)
        {
            var more = new ToolbarButton(8) { Tag = "more", Height = rowHeight, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0), Content = AiUi.Line(open ? "Show less" : $"Show {total - FoldAfter} more", 12, 16.8, "TextSecondary"), Roles = _ => (null, null, "TextSecondary") };
            more.Click += (_, _) =>
            {
                string id = _shown?.Id ?? "";
                if (!_unfolded.Remove(id)) { _unfolded[id] = true; }
                _panelClip?.MorphNext();
                _panelKey = null;
                if (Call is { } call) { Panel(call); }
            };
            host.Children.Add(more);
        }
        return host;
    }

    private FrameworkElement ListView(ToolOutput.List list) => Folded(list.Items.Count, count =>
    {
        var rows = new Grid();
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rows.ColumnDefinitions.Add(new ColumnDefinition());
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int row = 0; row < count; row++)
        {
            var item = list.Items[row];
            rows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            if (!string.IsNullOrEmpty(item.Lead)) { var lead = AiUi.Mono(item.Lead, 12, 18, "TextMuted"); lead.Margin = new Thickness(0, 0, 12, 0); Grid.SetRow(lead, row); rows.Children.Add(lead); }
            var title = AiUi.Line(item.Title, 14, 19.6);
            Grid.SetRow(title, row);
            Grid.SetColumn(title, 1);
            rows.Children.Add(title);
            if (!string.IsNullOrEmpty(item.Tag)) { var tag = AiUi.Line(item.Tag, 12, 16.8, "TextMuted", false, false); tag.Margin = new Thickness(12, 0, 0, 0); Grid.SetRow(tag, row); Grid.SetColumn(tag, 2); rows.Children.Add(tag); }
        }
        return rows;
    }, 32, new Thickness(12, 4, 12, 4));

    private FrameworkElement TableView(ToolOutput.Table table) => Folded(table.Rows.Count, count =>
    {
        var grid = new Grid();
        for (int column = 0; column < table.Columns.Count; column++) { grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); }
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        for (int column = 0; column < table.Columns.Count; column++)
        {
            var head = AiUi.Mono(table.Columns[column], 12, 18, "TextMuted");
            head.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(head, column);
            grid.Children.Add(head);
        }
        for (int row = 0; row < count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            for (int column = 0; column < table.Columns.Count && column < table.Rows[row].Count; column++)
            {
                var cell = AiUi.Mono(table.Rows[row][column], 12, 18);
                cell.Margin = new Thickness(0, 0, 20, 0);
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }
        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    }, 26, new Thickness(12, 4, 12, 4));

    private FrameworkElement JsonView(ToolOutput.Json json)
    {
        string[] lines = ToolCallText.PrettyLines(json.Value);
        return Folded(lines.Length, count =>
        {
            var block = new TextBlock { FontSize = 12, LineHeight = 18, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            block.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
            for (int index = 0; index < count; index++)
            {
                if (index > 0) { block.Inlines.Add(new LineBreak()); }
                Tint(block.Inlines, lines[index]);
            }
            return new ScrollViewer { Content = block, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        }, 24, new Thickness(12, 10, 12, 8));
    }

    // A syntax tint, one line at a time: keys, strings, numbers.
    internal static void Tint(InlineCollection inlines, string line)
    {
        void Add(string text, string role)
        {
            if (text.Length == 0) { return; }
            var run = new Run(text);
            run.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush." + role);
            inlines.Add(run);
        }
        int at = 0;
        while (at < line.Length)
        {
            char c = line[at];
            if (c == '"')
            {
                int end = at + 1;
                while (end < line.Length && line[end] != '"') { end += line[end] == '\\' ? 2 : 1; }
                end = Math.Min(end + 1, line.Length);
                bool key = line.AsSpan(end).TrimStart().StartsWith(":");
                Add(line[at..end], key ? "CodeKeyword" : "CodeString");
                at = end;
            }
            else if (char.IsDigit(c) || c == '-')
            {
                int end = at + 1;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] is '.' or 'e' or 'E' or '+' or '-')) { end++; }
                Add(line[at..end], "CodeNumber");
                at = end;
            }
            else
            {
                int end = at + 1;
                while (end < line.Length && line[end] != '"' && !char.IsDigit(line[end]) && line[end] != '-') { end++; }
                Add(line[at..end], "TextSecondary");
                at = end;
            }
        }
    }

    private static FrameworkElement TextView(ToolOutput.Text text)
    {
        var block = AiUi.Wrapped(text.Value, 12, 18);
        block.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
        block.Margin = new Thickness(12, 10, 12, 10);
        return block;
    }
}

/// <summary>Every call from one assistant turn under a quiet header: "Used 4 tools", an overlapping stack of marks and the failures.</summary>
public class ToolCallGroup : Control
{
    public static readonly DependencyProperty CallsProperty = DependencyProperty.Register(nameof(Calls), typeof(IReadOnlyList<ToolCallData>), typeof(ToolCallGroup), new FrameworkPropertyMetadata(null, (d, _) => ((ToolCallGroup)d).Refresh()));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool?), typeof(ToolCallGroup), new FrameworkPropertyMetadata(null, (d, _) => ((ToolCallGroup)d).Refresh()));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool?), typeof(ToolCallGroup), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ToolCallGroup)d).OnOpenSet()));
    public static readonly DependencyProperty AutoCollapseProperty = DependencyProperty.Register(nameof(AutoCollapse), typeof(bool), typeof(ToolCallGroup), new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty OutputTemplateProperty = DependencyProperty.Register(nameof(OutputTemplate), typeof(DataTemplate), typeof(ToolCallGroup), new FrameworkPropertyMetadata(null, (d, _) => ((ToolCallGroup)d).Refresh()));

    public IReadOnlyList<ToolCallData>? Calls { get => (IReadOnlyList<ToolCallData>?)GetValue(CallsProperty); set => SetValue(CallsProperty, value); }
    /// <summary>The agent is still working in this turn, so the header keeps saying "Using" between calls. Left null, it follows whether any call runs.</summary>
    public bool? IsActive { get => (bool?)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    /// <summary>Whether the list is open. Left null, it opens while any call is running, waiting or failed; a finished transcript starts folded.</summary>
    public bool? IsOpen { get => (bool?)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Folds the list 450 ms after every call has finished. Skipped once the person has opened or closed the group or any row.</summary>
    public bool AutoCollapse { get => (bool)GetValue(AutoCollapseProperty); set => SetValue(AutoCollapseProperty, value); }
    public DataTemplate? OutputTemplate { get => (DataTemplate?)GetValue(OutputTemplateProperty); set => SetValue(OutputTemplateProperty, value); }

    public event EventHandler<ToolCallEventArgs>? ApproveRequested;
    public event EventHandler<ToolCallEventArgs>? DenyRequested;
    public event EventHandler<ToolCallEventArgs>? RetryRequested;

    private ToolbarButton? _toggle;
    private SpringHeightBox? _clip;
    private readonly StackPanel _rows = new() { Margin = new Thickness(4) };
    private readonly Canvas _stack = new() { Height = 20, VerticalAlignment = VerticalAlignment.Center };
    private readonly SpringWidthBox _stackWidth = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly FadeSwap _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _failed = AiUi.Line("", 12, 16.8, "ArcDanger", false, false);
    private readonly TurningChevron _chevron = new();
    private readonly Dictionary<string, ToolCallCard> _cards = [];
    private readonly DispatcherTimer _fold = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _open, _touched, _built, _wasBusy, _first = true;
    private string _labelKey = "";
    private List<string> _stackKeys = [];

    static ToolCallGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToolCallGroup), new FrameworkPropertyMetadata(typeof(ToolCallGroup)));

    public ToolCallGroup()
    {
        _fold.Tick += (_, _) => { _fold.Stop(); if (!_touched && AutoCollapse && IsOpen is null && !Busy) { Show(false, true); } };
        Unloaded += (_, _) => _fold.Stop();
        _stackWidth.Child = _stack;
    }

    private bool Busy => Calls?.Any(call => call.Status is ToolCallStatus.Running or ToolCallStatus.Approval) == true;
    private bool NeedsEye => Busy || Calls?.Any(call => call.Status == ToolCallStatus.Error) == true;

    internal string LabelForTest => _labelKey;
    internal string FailedForTest => _failed.Visibility == Visibility.Visible ? _failed.Text : "";
    internal bool ListOpenForTest => _open;
    internal IReadOnlyList<ToolCallCard> CardsForTest => [.. _rows.Children.OfType<ToolCallCard>()];
    internal int MarksForTest => _stack.Children.Count;
    internal NativeButton? ToggleForTest => _toggle;
    internal void FoldNowForTest() { if (_fold.IsEnabled) { _fold.Stop(); if (!_touched && AutoCollapse && IsOpen is null && !Busy) { Show(false, false); } } }
    internal bool FoldPendingForTest => _fold.IsEnabled;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Root") is not Decorator host) { return; }
        var head = new Grid { Height = 40 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        _stackWidth.Margin = new Thickness(10, 0, 10, 0);
        head.Children.Add(_stackWidth);
        Grid.SetColumn(_label, 1);
        head.Children.Add(_label);
        _failed.Margin = new Thickness(10, 0, 10, 0);
        Grid.SetColumn(_failed, 2);
        head.Children.Add(_failed);
        _chevron.HorizontalAlignment = HorizontalAlignment.Left;
        _chevron.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(_chevron, 3);
        head.Children.Add(_chevron);
        _toggle = new ToolbarButton(12) { Margin = new Thickness(4), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Content = head, Roles = self => (self.IsMouseOver ? "SurfaceMuted" : null, null, "TextSecondary") };
        _toggle.Click += (_, _) => { _touched = true; _fold.Stop(); bool next = !_open; if (IsOpen is not null) { SetCurrentValue(IsOpenProperty, next); } else { Show(next, true); } };
        var divided = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = _rows };
        divided.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        _clip = new SpringHeightBox(Tween.Smooth) { Child = divided, Visibility = Visibility.Collapsed };
        var column = new StackPanel();
        column.Children.Add(_toggle);
        column.Children.Add(_clip);
        var frame = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Child = column };
        frame.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        frame.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        host.Child = frame;
        _built = true;
        Refresh();
    }

    private void Refresh()
    {
        if (!_built) { return; }
        var calls = Calls ?? [];
        Rows(calls);
        Marks(calls);
        bool working = IsActive ?? calls.Any(call => call.Status == ToolCallStatus.Running);
        string label = calls.Count == 1 ? (working ? "Using 1 tool" : "Used 1 tool") : working ? $"Using {calls.Count} tools" : $"Used {calls.Count} tools";
        if (label != _labelKey)
        {
            _labelKey = label;
            if (working) { _label.Set(new TextShimmer { Text = label, IsActive = true, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }); }
            else { _label.Set(AiUi.Line(label, 14, 19.6, "TextSecondary")); }
        }
        int failed = calls.Count(call => call.Status == ToolCallStatus.Error);
        _failed.Text = $"{failed} failed";
        _failed.Visibility = failed > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_toggle is not null) { AutomationProperties.SetName(_toggle, failed > 0 ? $"{label}, {failed} failed" : label); }

        if (IsOpen is { } controlled) { Show(controlled, !_first); }
        else if (_first) { Show(NeedsEye, false); }
        else if (NeedsEye && !_open && !_touched) { Show(true, true); }

        // Fold once a live run has finished cleanly, so the answer reads right under the header.
        bool busy = working || Busy;
        if (_wasBusy && !busy && AutoCollapse && !_touched && IsOpen is null && !NeedsEye) { _fold.Stop(); _fold.Start(); }
        if (busy) { _fold.Stop(); }
        _wasBusy = busy;
        _first = false;
    }

    private void OnOpenSet() { if (_built && IsOpen is { } open) { Show(open, true); } }

    private void Rows(IReadOnlyList<ToolCallData> calls)
    {
        var keep = new HashSet<string>(calls.Select(call => call.Id));
        foreach (var id in _cards.Keys.Where(id => !keep.Contains(id)).ToList()) { _rows.Children.Remove(_cards[id]); _cards.Remove(id); }
        for (int index = 0; index < calls.Count; index++)
        {
            var call = calls[index];
            if (!_cards.TryGetValue(call.Id, out var card))
            {
                card = new ToolCallCard { Flush = true, OutputTemplate = OutputTemplate };
                card.ApproveRequested += (_, e) => ApproveRequested?.Invoke(this, e);
                card.DenyRequested += (_, e) => DenyRequested?.Invoke(this, e);
                card.RetryRequested += (_, e) => RetryRequested?.Invoke(this, e);
                card.UserToggled += (_, _) => { _touched = true; _fold.Stop(); };
                _cards[call.Id] = card;
                _rows.Children.Insert(Math.Min(index, _rows.Children.Count), card);
                // A row appended by a live run grows in from zero height, so nothing below it jumps.
                if (!_first && _open && AiUi.Moving(this)) { GrowIn(card, index); }
            }
            else if (_rows.Children.IndexOf(card) != index)
            {
                _rows.Children.Remove(card);
                _rows.Children.Insert(Math.Min(index, _rows.Children.Count), card);
            }
            card.OutputTemplate = OutputTemplate;
            card.Call = call;
        }
    }

    private void GrowIn(ToolCallCard card, int index)
    {
        _clip?.MorphNext();
        card.Opacity = 0;
        Tween.To(card, OpacityProperty, 1, .16, ArcEasing.Standard);
    }

    private void Marks(IReadOnlyList<ToolCallData> calls)
    {
        var keys = calls.Select(call => call.ToolName).Distinct().Take(4).ToList();
        if (keys.SequenceEqual(_stackKeys)) { return; }
        _stackKeys = keys;
        _stack.Children.Clear();
        _stack.Width = keys.Count == 0 ? 0 : 20 + (keys.Count - 1) * 16;
        for (int index = 0; index < keys.Count; index++)
        {
            var mark = calls.First(call => call.ToolName == keys[index]).Mark;
            var disc = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = new Decorator { Child = AiUi.Mark(mark, 12, keys[index]), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            disc.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            disc.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
            Canvas.SetLeft(disc, index * 16);
            _stack.Children.Add(disc);
        }
    }

    private void Show(bool open, bool animate)
    {
        if (_clip is null) { return; }
        bool was = _open;
        _open = open;
        _chevron.Point(open);
        if (was == open && (_clip.Visibility == Visibility.Visible) == open) { return; }
        animate = animate && AiUi.Moving(this);
        if (open)
        {
            if (_clip.IsCollapsing) { _clip.Cancel(); return; }
            _clip.Visibility = Visibility.Visible;
            if (!animate) { return; }
            _clip.Reveal();
            // Opening staggers the rows in 20 ms apart with a 6 px settle.
            int index = 0;
            foreach (var card in _rows.Children.OfType<FrameworkElement>())
            {
                double delay = index++ * .02;
                var settle = new TranslateTransform(0, 6);
                card.RenderTransform = settle;
                card.Opacity = 0;
                Tween.To(card, OpacityProperty, 1, .16, ArcEasing.Standard, delay);
                Tween.To(settle, TranslateTransform.YProperty, 0, .24, ArcEasing.Enter, delay);
            }
            return;
        }
        if (!animate) { _clip.Visibility = Visibility.Collapsed; return; }
        _clip.Collapse(() => _clip.Visibility = Visibility.Collapsed, true);
    }
}
