using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using NativeButton = System.Windows.Controls.Button;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace ArcWpf.Controls;

public partial class ArcDataGrid
{
    private FrameworkElement? _toolbar, _toolRow, _bulkRow, _statusBar, _history, _statusGroup, _rowCountHost;
    private NativeTextBox? _search;
    private NativeButton? _searchClear, _undoButton, _redoButton, _filterButton, _columnsButton, _densityButton, _collapseButton, _exportButton, _bulkClear, _copyButton, _bulkExport, _deleteButton, _statusAction;
    private FrameworkElement? _filterCount;
    private TextBlock? _rowCount, _filterCountText, _address, _statusText;
    private StackPanel? _stats, _bulkActionsHost, _bulkCountHost;
    private readonly OdometerNumber _bulkNumber = new() { FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
    private DispatcherTimer? _statusTimer;
    private Action? _statusCallback;
    private readonly Dictionary<string, OdometerNumber> _statNumbers = [];
    private bool _bulkShown;

    private T? Part<T>(string name) where T : class => GetTemplateChild(name) as T;
    // String keys in a theme dictionary are invisible to runtime lookup, so these styles use component keys.
    private Style? GridStyle(string id) => TryFindResource(new ComponentResourceKey(typeof(ArcDataGrid), id)) as Style;

    private void DetachChrome()
    {
        if (_bulkCountHost is not null) { _bulkCountHost.Children.Remove(_bulkNumber); }
        if (_search is not null) { _search.TextChanged -= SearchChanged; _search.PreviewKeyDown -= SearchKey; }
        foreach (var (button, handler) in _wired) { button.Click -= handler; }
        _wired.Clear();
    }

    private readonly List<(NativeButton Button, RoutedEventHandler Handler)> _wired = [];
    private void Wire(NativeButton? button, Action action)
    {
        if (button is null) { return; }
        RoutedEventHandler handler = (_, _) => action();
        button.Click += handler;
        _wired.Add((button, handler));
    }

    private void AttachChrome()
    {
        _toolbar = Part<FrameworkElement>("PART_Toolbar");
        _toolRow = Part<FrameworkElement>("PART_ToolRow");
        _bulkRow = Part<FrameworkElement>("PART_BulkRow");
        _statusBar = Part<FrameworkElement>("PART_StatusBar");
        _history = Part<FrameworkElement>("PART_History");
        _rowCountHost = _rowCount = Part<TextBlock>("PART_RowCount");
        _search = Part<NativeTextBox>("PART_Search");
        _searchClear = Part<NativeButton>("PART_SearchClear");
        _undoButton = Part<NativeButton>("PART_Undo");
        _redoButton = Part<NativeButton>("PART_Redo");
        _filterButton = Part<NativeButton>("PART_Filter");
        _filterCount = Part<FrameworkElement>("PART_FilterCount");
        _filterCountText = Part<TextBlock>("PART_FilterCountText");
        _columnsButton = Part<NativeButton>("PART_Columns");
        _densityButton = Part<NativeButton>("PART_Density");
        _collapseButton = Part<NativeButton>("PART_Collapse");
        _exportButton = Part<NativeButton>("PART_Export");
        _bulkClear = Part<NativeButton>("PART_BulkClear");
        _bulkCountHost = Part<StackPanel>("PART_BulkCount");
        _bulkActionsHost = Part<StackPanel>("PART_BulkActions");
        _copyButton = Part<NativeButton>("PART_Copy");
        _bulkExport = Part<NativeButton>("PART_BulkExport");
        _deleteButton = Part<NativeButton>("PART_Delete");
        _address = Part<TextBlock>("PART_Address");
        _stats = Part<StackPanel>("PART_Stats");
        _statusGroup = Part<FrameworkElement>("PART_Status");
        _statusText = Part<TextBlock>("PART_StatusText");
        _statusAction = Part<NativeButton>("PART_StatusAction");
        _statNumbers.Clear();
        if (Part<FrameworkElement>("PART_Clip") is { } clip)
        {
            void Round() => clip.Clip = new RectangleGeometry(new Rect(0, 0, clip.ActualWidth, clip.ActualHeight), 25, 25);
            clip.SizeChanged += (_, _) => Round();
        }

        if (_search is not null) { _search.TextChanged += SearchChanged; _search.PreviewKeyDown += SearchKey; }
        Wire(_searchClear, () => { if (_search is not null) { _search.Text = string.Empty; _search.Focus(); } });
        Wire(_undoButton, Undo);
        Wire(_redoButton, Redo);
        Wire(_filterButton, () => { FilterRowOpen = !FilterRowOpen; RebuildFilterInputs(); UpdateChrome(); Surface?.InvalidateMeasure(); Surface?.InvalidateVisual(); PositionOverlays(); RaiseViewState(); });
        Wire(_columnsButton, OpenColumnsMenu);
        Wire(_densityButton, OpenDensityMenu);
        Wire(_collapseButton, ToggleAllGroups);
        Wire(_exportButton, () => OpenExportMenu(_exportButton!, Filtered));
        Wire(_bulkClear, () => { Checked.Clear(); PublishChecked(); });
        Wire(_copyButton, CopyChecked);
        Wire(_bulkExport, () => OpenExportMenu(_bulkExport!, CheckedRows()));
        Wire(_deleteButton, DeleteChecked);
        Wire(_statusAction, () => { var callback = _statusCallback; ClearStatus(); callback?.Invoke(); });
        if (_bulkCountHost is not null && _bulkNumber.Parent is null) { _bulkCountHost.Children.Insert(0, _bulkNumber); }
        _bulkNumber.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _bulkNumber.Set(0, "0", false);
        BuildBulkActions();
        UpdateChrome();
        UpdateStatus();
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = _search?.Text ?? string.Empty;
        RebuildView(false);
    }

    private void SearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_search!.Text.Length > 0) { _search.Text = string.Empty; } else { Surface?.Focus(); }
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Down) { Surface?.Focus(); e.Handled = true; }
    }

    private void BuildBulkActions()
    {
        if (_bulkActionsHost is null) { return; }
        _bulkActionsHost.Children.Clear();
        var style = GridStyle("ToolButton");
        foreach (var action in BulkActions)
        {
            var button = new NativeButton { Tag = action.Icon, Content = action.Label, Style = style, Margin = new Thickness(0, 0, 4, 0) };
            if (action.IsDanger) { button.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.DataGridDanger"); }
            AutomationProperties.SetName(button, action.Label);
            button.Click += (_, _) => action.Invoked?.Invoke(CheckedRows());
            _bulkActionsHost.Children.Add(button);
        }
    }

    internal void UpdateChrome()
    {
        double width = ActualWidth;
        SetShowToolLabels(this, width >= 640);
        if (_toolbar is not null) { _toolbar.Visibility = ToolbarVisible ? Visibility.Visible : Visibility.Collapsed; }
        bool narrow = width > 0 && width < 460;
        int shown = Filtered.Count, all = Rows?.Count ?? 0;
        string count = shown == all ? $"{all:N0} {(all == 1 ? "row" : "rows")}" : $"{shown:N0} of {all:N0} rows";
        if (_rowCount is not null) { _rowCount.Text = count; _rowCount.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible; }
        if (_history is not null) { _history.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible; }
        if (_undoButton is not null) { _undoButton.IsEnabled = CanUndo; }
        if (_redoButton is not null) { _redoButton.IsEnabled = CanRedo; }
        if (_searchClear is not null) { _searchClear.Visibility = SearchText.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
        int active = ActiveFilterCount();
        if (_filterButton is not null) { SetPressed(_filterButton, FilterRowOpen); }
        if (_filterCount is not null) { _filterCount.Visibility = active > 0 ? Visibility.Visible : Visibility.Collapsed; }
        if (_filterCountText is not null) { _filterCountText.Text = active.ToString(CultureInfo.InvariantCulture); }
        if (_densityButton is not null)
        {
            _densityButton.Tag = Density switch { ArcDataGridDensity.Compact => "rows-4", ArcDataGridDensity.Comfortable => "rows-2", _ => "rows-3" };
            AutomationProperties.SetName(_densityButton, "Row density, " + Density.ToString().ToLowerInvariant());
        }
        bool grouped = GroupBy is { Count: > 0 };
        if (_collapseButton is not null)
        {
            _collapseButton.Visibility = grouped ? Visibility.Visible : Visibility.Collapsed;
            _collapseButton.Tag = AllGroupsCollapsed ? "chevrons-up-down" : "chevrons-down-up";
            AutomationProperties.SetName(_collapseButton, AllGroupsCollapsed ? "Expand all groups" : "Collapse all groups");
        }
        if (_deleteButton is not null) { _deleteButton.Visibility = CanDeleteRows ? Visibility.Visible : Visibility.Collapsed; }
        if (_bulkExport is not null) { _bulkExport.Visibility = Visibility.Visible; }
        _bulkNumber.Set(Checked.Count, Checked.Count.ToString("N0", CultureInfo.InvariantCulture), true);
        SwapToolbar(Checked.Count > 0);
    }

    private static void SetPressed(NativeButton button, bool pressed)
    {
        if (pressed)
        {
            button.SetResourceReference(BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            button.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        }
        else
        {
            button.ClearValue(BackgroundProperty);
            button.ClearValue(ForegroundProperty);
        }
    }

    private int ActiveFilterCount()
    {
        if (Filters is null) { return 0; }
        int count = 0;
        foreach (var pair in Filters)
        {
            if (pair.Value is string text ? text.Trim().Length > 0 : pair.Value is not null) { count++; }
        }
        return count;
    }

    // The toolbar swaps to bulk actions with a short slide, fade and blur.
    private void SwapToolbar(bool bulk)
    {
        if (_toolRow is null || _bulkRow is null) { return; }
        if (bulk == _bulkShown && (_bulkRow.Visibility == Visibility.Visible) == bulk) { return; }
        _bulkShown = bulk;
        var entering = bulk ? _bulkRow : _toolRow;
        var leaving = bulk ? _toolRow : _bulkRow;
        leaving.Visibility = Visibility.Collapsed;
        leaving.BeginAnimation(OpacityProperty, null);
        leaving.Effect = null;
        entering.Visibility = Visibility.Visible;
        if (!IsLoaded || ArcMotion.GetLevel(this) == ArcMotionLevel.Off) { return; }
        var ease = ArcEasing.Enter;
        var duration = new Duration(TimeSpan.FromSeconds(.16));
        var blur = new BlurEffect { Radius = 4 };
        entering.Effect = blur;
        var shift = new TranslateTransform(0, bulk ? 6 : -6);
        entering.RenderTransform = shift;
        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) => { entering.Effect = null; };
        entering.BeginAnimation(OpacityProperty, fade);
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(4, 0, duration) { EasingFunction = ease });
        if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full) { shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = ease }); }
        else { shift.Y = 0; }
    }

    // ---- Status bar ----
    internal void UpdateStatus()
    {
        if (_address is null || _stats is null) { return; }
        bool show = SelectionStats && View.Count > 0;
        _address.Visibility = SelectionStats ? Visibility.Visible : Visibility.Collapsed;
        _stats.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _address.Text = show ? Address() : "\u2013";
        if (!show) { return; }
        double sum = 0;
        int numeric = 0, count = 0;
        foreach (var (row, column) in RangeCells())
        {
            object? value = row[column.Key];
            if (value is null or "") { continue; }
            count++;
            if (column.IsNumeric && value is double number) { sum += number; numeric++; }
        }
        // A single cell only counts; its number is already on screen.
        var (left, top, right, bottom) = Range;
        bool hasNumbers = numeric > 0 && (left != right || top != bottom);
        SetStat("Sum", hasNumbers, sum, FormatStat(sum, false));
        SetStat("Average", hasNumbers, hasNumbers ? sum / numeric : 0, FormatStat(hasNumbers ? sum / numeric : 0, true));
        SetStat("Count", true, count, count.ToString("N0", CultureInfo.InvariantCulture));
    }

    // Sums read as whole numbers and averages keep one decimal, whichever columns the range spans.
    private string FormatStat(double value, bool average)
    {
        var (l, _, r, _) = Range;
        var numeric = Enumerable.Range(l, Math.Max(0, Math.Min(r, Visible.Count - 1) - l + 1)).Select(c => Visible[c]).Where(column => column.IsNumeric).ToArray();
        var type = numeric.Length > 0 && numeric.All(column => column.Type == numeric[0].Type) ? numeric[0].Type : ArcDataGridColumnType.Number;
        var column = new ArcDataGridColumn { Type = type, Decimals = average ? 1 : numeric.Select(c => c.Decimals).DefaultIfEmpty(0).Max() };
        return ArcDataGridModel.Format(value, column);
    }

    private void SetStat(string label, bool visible, double value, string text)
    {
        if (_stats is null) { return; }
        var host = _stats.Children.OfType<StackPanel>().FirstOrDefault(panel => (string?)panel.Tag == label);
        if (host is null)
        {
            if (!visible) { return; }
            host = new StackPanel { Orientation = Orientation.Horizontal, Tag = label, Margin = new Thickness(0, 0, 16, 0) };
            var caption = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            var number = new OdometerNumber { FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            number.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
            host.Children.Add(caption);
            host.Children.Add(number);
            _statNumbers[label] = number;
            int index = label == "Sum" ? 0 : label == "Average" ? Math.Min(_stats.Children.Count, _stats.Children.OfType<StackPanel>().Any(panel => (string?)panel.Tag == "Sum") ? 1 : 0) : _stats.Children.Count;
            _stats.Children.Insert(index, host);
        }
        host.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible) { _statNumbers[label].Set(value, text, true); }
    }

    /// <summary>Shows a message at the end of the status bar, with an optional action such as Undo.</summary>
    internal void ShowStatus(string text, (string Label, Action Callback)? action)
    {
        Announce(text);
        if (_statusGroup is null || _statusText is null) { return; }
        _statusText.Text = text;
        _statusCallback = action?.Callback;
        if (_statusAction is not null)
        {
            _statusAction.Content = action?.Label;
            _statusAction.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        }
        _statusGroup.Visibility = Visibility.Visible;
        if (IsLoaded && ArcMotion.GetLevel(this) != ArcMotionLevel.Off)
        {
            var duration = new Duration(TimeSpan.FromSeconds(.16));
            _statusGroup.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ArcEasing.Enter, FillBehavior = FillBehavior.Stop });
            if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
            {
                var shift = new TranslateTransform(0, 4);
                _statusGroup.RenderTransform = shift;
                shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { EasingFunction = ArcEasing.Enter });
            }
        }
        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(action is null ? 4 : 8) };
        _statusTimer.Tick += (_, _) => ClearStatus();
        _statusTimer.Start();
    }

    internal void ClearStatus()
    {
        _statusTimer?.Stop();
        _statusCallback = null;
        if (_statusGroup is not null) { _statusGroup.Visibility = Visibility.Collapsed; }
    }

    internal void Announce(string text)
    {
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged) && GetTemplateChild("PART_Live") is TextBlock live)
        {
            live.Text = text;
            UIElementAutomationPeer.CreatePeerForElement(live)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    // ---- Filter row ----
    private readonly Dictionary<string, FrameworkElement> _filterInputs = new(StringComparer.Ordinal);

    internal void RebuildFilterInputs()
    {
        if (_overlay is null) { return; }
        var wanted = FilterRowOpen ? Visible.Where(column => column.Filterable).Select(column => column.Key).ToHashSet(StringComparer.Ordinal) : [];
        foreach (string key in _filterInputs.Keys.Where(key => !wanted.Contains(key)).ToArray())
        {
            _overlay.Children.Remove(_filterInputs[key]);
            _filterInputs.Remove(key);
        }
        foreach (var column in Visible)
        {
            if (!wanted.Contains(column.Key)) { continue; }
            if (!_filterInputs.TryGetValue(column.Key, out var input))
            {
                input = column.Type == ArcDataGridColumnType.Select ? CreateFilterSelect(column) : CreateFilterInput(column);
                _filterInputs[column.Key] = input;
                _overlay.Children.Add(input);
            }
            RefreshFilterInput(column, input);
        }
        PositionOverlays();
    }

    private NativeTextBox CreateFilterInput(ArcDataGridColumn column)
    {
        var box = new NativeTextBox
        {
            Style = GridStyle("FilterInput"),
            Tag = column.Type == ArcDataGridColumnType.Date ? "> 2026-01-31" : column.IsNumeric ? "> 100, 10..50" : "Contains",
            TextAlignment = column.IsNumeric ? TextAlignment.Right : TextAlignment.Left,
            Height = 30
        };
        AutomationProperties.SetName(box, column.IsNumeric ? $"Filter {column.Header}. Use > < = or a range like 10..50" : $"Filter {column.Header}");
        box.TextChanged += (_, _) =>
        {
            if (_rebuilding) { return; }
            SetFilter(column.Key, box.Text);
            box.FontWeight = box.Text.Length > 0 ? FontWeights.Medium : FontWeights.Normal;
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (box.Text.Length > 0) { box.Text = string.Empty; } else { Surface?.Focus(); }
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.Down) { Surface?.Focus(); e.Handled = true; }
        };
        return box;
    }

    private NativeButton CreateFilterSelect(ArcDataGridColumn column)
    {
        var button = new NativeButton { Style = GridStyle("FilterSelect"), Height = 30 };
        button.Click += (_, _) => OpenSelectFilter(button, column);
        return button;
    }

    private void RefreshFilterInput(ArcDataGridColumn column, FrameworkElement input)
    {
        object? value = null;
        Filters?.TryGetValue(column.Key, out value);
        if (input is NativeTextBox box)
        {
            string text = value as string ?? string.Empty;
            if (box.Text != text) { _rebuilding = true; box.Text = text; _rebuilding = false; }
            box.FontWeight = text.Length > 0 ? FontWeights.Medium : FontWeights.Normal;
        }
        else if (input is NativeButton button)
        {
            var options = OptionsFor(column);
            var keep = value as IEnumerable<string>;
            int kept = keep?.Count() ?? options.Count;
            button.Content = keep is null || kept == options.Count ? "All" : kept == 0 ? "None" : kept == 1 ? keep.First() : $"{kept} selected";
            button.FontWeight = keep is null || kept == options.Count ? FontWeights.Normal : FontWeights.Medium;
            AutomationProperties.SetName(button, $"Filter {column.Header}, {button.Content}");
        }
    }

    internal void PositionOverlays()
    {
        if (_overlay is null || Surface is null) { return; }
        foreach (var pair in _filterInputs)
        {
            int index = Visible.FindIndex(column => column.Key == pair.Key);
            if (index < 0 || index >= ColX.Length) { pair.Value.Visibility = Visibility.Collapsed; continue; }
            pair.Value.Visibility = FilterRowOpen ? Visibility.Visible : Visibility.Collapsed;
            bool pinned = index < PinnedCount;
            double x = ColX[index] + 4 - (pinned ? 0 : Surface.ScrollX);
            double width = ColW[index] - 8;
            pair.Value.Width = Math.Max(0, width);
            Canvas.SetLeft(pair.Value, x);
            Canvas.SetTop(pair.Value, 40 + 4.5);
            // A scrolling input slides under the pinned columns rather than over them.
            double hidden = pinned ? 0 : Math.Max(0, PinEdge - x);
            pair.Value.Clip = hidden > 0 ? new RectangleGeometry(new Rect(hidden, -2, Math.Max(0, width - hidden), 34)) : null;
            pair.Value.IsHitTestVisible = hidden < width;
        }
        PositionFilterClear();
        PositionEditor();
        PositionEmpty();
    }

    // ---- Filter row clear action ----
    private NativeButton? _filterClearButton;

    private void PositionFilterClear()
    {
        if (_overlay is null) { return; }
        bool show = FilterRowOpen && GutterWidth > 0 && (Filters?.Count ?? 0) > 0;
        if (show && _filterClearButton is null)
        {
            _filterClearButton = new NativeButton { Style = GridStyle("FilterClearButton") };
            AutomationProperties.SetName(_filterClearButton, "Clear all filters");
            _filterClearButton.Click += (_, _) => { ClearFilters(); Surface?.Focus(); };
            _overlay.Children.Add(_filterClearButton);
            Canvas.SetLeft(_filterClearButton, 10);
            Canvas.SetTop(_filterClearButton, 45.5);
        }
        if (_filterClearButton is not null) { _filterClearButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed; }
    }

    // ---- Empty state action ----
    private NativeButton? _clearFiltersButton;

    private void PositionEmpty()
    {
        if (_overlay is null || Surface is null) { return; }
        bool show = !IsLoading && View.Count == 0 && (Rows?.Count ?? 0) > 0;
        if (show && _clearFiltersButton is null)
        {
            _clearFiltersButton = new NativeButton { Content = "Clear filters", Style = GridStyle("OutlineButton") };
            _clearFiltersButton.Click += (_, _) => ClearFilters();
            _overlay.Children.Add(_clearFiltersButton);
        }
        if (_clearFiltersButton is null) { return; }
        _clearFiltersButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) { return; }
        _clearFiltersButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_clearFiltersButton, (Surface.ActualWidth - _clearFiltersButton.DesiredSize.Width) / 2);
        Canvas.SetTop(_clearFiltersButton, Surface.HeadHeight + 87.8);
    }

    // ---- Cell editor ----
    private NativeTextBox? _editor;
    private ArcDataGridRow? _editRow;
    private ArcDataGridColumn? _editColumn;
    private int _editColumnIndex;
    private bool _replaceMode;
    private Popup? _editPopup;
    private readonly List<(Border Row, string Option)> _optionRows = [];
    private int _optionIndex = -1;
    private TranslateTransform? _shake;

    internal bool IsEditing => _editor is { Visibility: Visibility.Visible };
    internal (int Column, string RowId)? EditingCell => IsEditing && _editRow is not null ? (_editColumnIndex, _editRow.Id) : null;

    /// <summary>Starts editing the active cell. Null text keeps the value and its caret; other text replaces it, and arrows then commit and move, as in Excel.</summary>
    internal void BeginEdit(string? initial, bool replace, bool openPopup = false)
    {
        if (_overlay is null || _active.Row >= View.Count || View.Count == 0 || View[_active.Row].Row is not { } row || _active.Column >= Visible.Count) { return; }
        var column = Visible[_active.Column];
        if (!column.Editable) { ShowStatus($"{column.Header} is read only", null); return; }
        _anchor = _focus = _active;
        Surface?.Reveal(_active.Column, _active.Row);
        Surface?.AnimateSelection();
        UpdateStatus();
        if (_editor is null)
        {
            _editor = new NativeTextBox { Style = GridStyle("Editor") };
            _shake = new TranslateTransform();
            _editor.RenderTransform = _shake;
            _editor.PreviewKeyDown += EditorKey;
            _editor.TextChanged += EditorChanged;
            _editor.LostKeyboardFocus += EditorLostFocus;
            _overlay.Children.Add(_editor);
        }
        _editRow = row;
        _editColumn = column;
        _editColumnIndex = _active.Column;
        _replaceMode = replace;
        object? raw = row[column.Key];
        string text = initial ?? (raw switch
        {
            null => string.Empty,
            double number => column.Type == ArcDataGridColumnType.Percent || column.Type == ArcDataGridColumnType.Currency || column.Type == ArcDataGridColumnType.Number ? number.ToString("0.########", CultureInfo.InvariantCulture) : number.ToString(CultureInfo.InvariantCulture),
            _ => raw.ToString() ?? string.Empty
        });
        _editor.TextChanged -= EditorChanged;
        _editor.Text = text;
        _editor.TextChanged += EditorChanged;
        _editor.TextAlignment = column.IsNumeric ? TextAlignment.Right : TextAlignment.Left;
        _editor.FontWeight = Active.Column < PinnedCount ? FontWeights.Medium : FontWeights.Normal;
        _editor.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        _editor.Visibility = Visibility.Visible;
        _editor.CaretIndex = text.Length;
        PositionEditor();
        Surface?.InvalidateVisual();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { _editor?.Focus(); if (openPopup) { OpenEditPopup(); } });
    }

    private void PositionEditor()
    {
        if (_editor is null || !IsEditing || Surface is null || _editRow is null) { return; }
        int index = _editColumnIndex;
        if (index >= ColX.Length) { return; }
        int rowIndex = View.FindIndex(item => item.Row == _editRow);
        if (rowIndex < 0) { CancelEdit(); return; }
        bool pinned = index < PinnedCount;
        double x = ColX[index] - (pinned ? 0 : Surface.ScrollX);
        double y = Surface.HeadHeight + rowIndex * RowHeight - Surface.ScrollY;
        bool picker = _editColumn?.Type is ArcDataGridColumnType.Select or ArcDataGridColumnType.Date;
        _editor.Width = Math.Max(0, ColW[index] - 24 - (picker ? 0 : 0));
        _editor.Height = RowHeight - 1;
        Canvas.SetLeft(_editor, x + 12);
        Canvas.SetTop(_editor, y);
        double top = y < Surface.HeadHeight ? Surface.HeadHeight - y : 0;
        double hidden = pinned ? 0 : Math.Max(0, PinEdge - (x + 12));
        double bottom = Math.Max(0, y + RowHeight - 1 - (Surface.ActualHeight - Surface.FootHeight));
        _editor.Clip = new RectangleGeometry(new Rect(hidden, top, Math.Max(0, _editor.Width - hidden), Math.Max(0, RowHeight - 1 - top - bottom)));
    }

    private void EditorChanged(object sender, TextChangedEventArgs e)
    {
        _editor?.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.Foreground");
        if (_editPopup?.IsOpen == true && _editColumn?.Type == ArcDataGridColumnType.Select) { HighlightMatchingOption(); }
        else if (_editColumn?.Type == ArcDataGridColumnType.Select && _editPopup?.IsOpen != true && _editor?.IsKeyboardFocused == true && _replaceMode) { OpenEditPopup(); }
    }

    private void EditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsEditing || e.NewFocus is DependencyObject target && (_editPopup?.Child is { } child && IsDescendant(child, target))) { return; }
        if (e.NewFocus is null) { return; }
        CommitEdit(0, 0, keepOpenOnError: false);
    }

    private static bool IsDescendant(DependencyObject root, DependencyObject node)
    {
        for (DependencyObject? current = node; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, root)) { return true; }
        }
        return false;
    }

    private void EditorKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool listOpen = _editPopup?.IsOpen == true && _editColumn?.Type == ArcDataGridColumnType.Select;
        switch (key)
        {
            case Key.Down when alt && _editColumn?.Type is ArcDataGridColumnType.Select or ArcDataGridColumnType.Date:
                OpenEditPopup();
                e.Handled = true;
                return;
            case Key.Down when listOpen: MoveOption(1); e.Handled = true; return;
            case Key.Up when listOpen: MoveOption(-1); e.Handled = true; return;
            case Key.Enter when listOpen && _optionIndex >= 0: PickOption(_optionRows[_optionIndex].Option, shift ? -1 : 1, 0); e.Handled = true; return;
            case Key.Tab when listOpen && _optionIndex >= 0: PickOption(_optionRows[_optionIndex].Option, 0, shift ? -1 : 1); e.Handled = true; return;
            case Key.Enter: CommitEdit(shift ? -1 : 1, 0); e.Handled = true; return;
            case Key.Tab: CommitEdit(0, shift ? -1 : 1); e.Handled = true; return;
            case Key.Escape:
                if (_editPopup?.IsOpen == true) { _editPopup.IsOpen = false; } else { CancelEdit(); Surface?.Focus(); }
                e.Handled = true;
                return;
        }
        if (_replaceMode && key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            CommitEdit(key == Key.Up ? -1 : key == Key.Down ? 1 : 0, key == Key.Left ? -1 : key == Key.Right ? 1 : 0);
            e.Handled = true;
        }
    }

    internal void CommitEdit(int rowStep, int columnStep, bool keepOpenOnError = true)
    {
        if (!IsEditing || _editRow is null || _editColumn is null || _editor is null) { return; }
        if (!ArcDataGridModel.TryParse(_editor.Text, _editColumn, OptionsFor(_editColumn), out object? value))
        {
            if (keepOpenOnError) { MarkInvalid(); return; }
            CancelEdit();
            return;
        }
        var row = _editRow;
        var column = _editColumn;
        CloseEditor();
        ApplyEdits([new Patch(row.Id, column.Key, row[column.Key], value)], "Edit", announce: false);
        Announce($"{column.Header} set to {ArcDataGridModel.Format(value, column)}");
        if (rowStep != 0 || columnStep != 0)
        {
            int nextRow = rowStep != 0 ? StepRow(_active.Row, rowStep) : _active.Row;
            Select(Math.Clamp(_active.Column + columnStep, 0, Visible.Count - 1), nextRow, false);
        }
        else { Surface?.InvalidateVisual(); }
        Surface?.Focus();
    }

    // An invalid edit shakes once and stays open, in the danger color.
    private void MarkInvalid()
    {
        if (_editor is null || _shake is null) { return; }
        _editor.SetResourceReference(ForegroundProperty, "ArcWpf.Brush.DataGridDanger");
        AutomationProperties.SetHelpText(_editor, "Invalid value");
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Full) { return; }
        var keys = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        double[] offsets = [-4, 4, -3, 3, -1.5, 0];
        for (int i = 0; i < offsets.Length; i++) { keys.KeyFrames.Add(new EasingDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50 * (i + 1))))); }
        _shake.BeginAnimation(TranslateTransform.XProperty, keys);
    }

    public void CancelEdit()
    {
        if (!IsEditing) { return; }
        CloseEditor();
        Surface?.InvalidateVisual();
    }

    private void CloseEditor()
    {
        if (_editPopup is not null) { _editPopup.IsOpen = false; }
        if (_editor is null) { return; }
        _editor.Visibility = Visibility.Collapsed;
        _editRow = null;
        _editColumn = null;
    }

    private void OpenEditPopup()
    {
        if (_editor is null || _editColumn is null) { return; }
        if (_editPopup?.IsOpen == true) { return; }
        if (_editColumn.Type == ArcDataGridColumnType.Select) { OpenOptionList(); }
        else if (_editColumn.Type == ArcDataGridColumnType.Date) { OpenCalendar(); }
    }

    private void OpenOptionList()
    {
        if (_editor is null || _editColumn is null) { return; }
        var list = new StackPanel();
        _optionRows.Clear();
        _optionIndex = -1;
        foreach (string option in OptionsFor(_editColumn))
        {
            var row = MenuRow(null, option, null, () => PickOption(option, 0, 0), focusOnHover: false);
            list.Children.Add(row);
            _optionRows.Add((row, option));
        }
        _editPopup = ShowPopup(_editor, list, -12 - 6, RowHeight - 1 - 4 + 4, Math.Max(ColW[_editColumnIndex], 208), focusFirst: false);
        HighlightMatchingOption();
    }

    private void HighlightMatchingOption()
    {
        if (_editor is null) { return; }
        string text = _editor.Text.Trim();
        int match = _optionRows.FindIndex(pair => string.Equals(pair.Option, text, StringComparison.OrdinalIgnoreCase));
        if (match < 0 && text.Length > 0) { match = _optionRows.FindIndex(pair => pair.Option.StartsWith(text, StringComparison.OrdinalIgnoreCase)); }
        SetOption(match >= 0 ? match : 0);
    }

    private void MoveOption(int step)
    {
        if (_optionRows.Count > 0) { SetOption(Math.Clamp(_optionIndex + step, 0, _optionRows.Count - 1)); }
    }

    private void SetOption(int index)
    {
        for (int i = 0; i < _optionRows.Count; i++)
        {
            if (i == index) { _optionRows[i].Row.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted"); } else { _optionRows[i].Row.Background = Brushes.Transparent; }
        }
        _optionIndex = index;
    }

    private void PickOption(string option, int rowStep, int columnStep)
    {
        if (_editor is null) { return; }
        _editor.Text = option;
        if (_editPopup is not null) { _editPopup.IsOpen = false; }
        CommitEdit(rowStep, columnStep);
    }

    private void OpenCalendar()
    {
        if (_editor is null || _editColumn is null) { return; }
        var calendar = new Calendar { FocusVisualStyle = null };
        string? iso = ArcDataGridModel.ParseDay(_editor.Text);
        if (iso is not null)
        {
            var day = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            calendar.SelectedDate = day;
            calendar.DisplayDate = day;
        }
        calendar.SelectedDatesChanged += (_, e) =>
        {
            if (calendar.SelectedDate is not { } picked || e.AddedItems.Count == 0 || _editor is null) { return; }
            if (!calendar.IsKeyboardFocusWithin && !calendar.IsMouseOver) { return; }
            _editor.Text = picked.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (Mouse.LeftButton == MouseButtonState.Pressed || calendar.IsMouseOver) { _editPopup!.IsOpen = false; CommitEdit(0, 0); }
        };
        calendar.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && calendar.SelectedDate is { } picked && _editor is not null)
            {
                _editor.Text = picked.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                _editPopup!.IsOpen = false;
                CommitEdit(0, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) { _editPopup!.IsOpen = false; _editor?.Focus(); e.Handled = true; }
        };
        _editPopup = ShowPopup(_editor, calendar, -6, RowHeight - 1 + 6, 304, focusFirst: false);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => calendar.Focus());
    }

    // ---- Menus ----
    private const double ShadowLeft = 48, ShadowTop = 28;

    /// <summary>Opens a menu popup at an offset from the anchor's top left. Menus fade and scale in from the anchor.</summary>
    private Popup ShowPopup(FrameworkElement anchor, FrameworkElement content, double x, double y, double width, bool focusFirst = true, bool menuPadding = true)
    {
        var card = new Border { Width = width, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Child = content, Padding = menuPadding ? new Thickness(5) : new Thickness(0), SnapsToDevicePixels = true };
        card.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.Border");
        card.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 3, Direction = 270, Color = Colors.Black, Opacity = .045 };
        var far = new Border { CornerRadius = new CornerRadius(18), Child = card, Effect = new DropShadowEffect { BlurRadius = 48, ShadowDepth = 20, Direction = 270, Color = Colors.Black, Opacity = .105 } };
        far.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
        var host = new Border { Padding = new Thickness(ShadowLeft, ShadowTop, ShadowLeft, ShadowLeft + 20), Child = far, Background = Brushes.Transparent };
        var popup = new Popup
        {
            AllowsTransparency = true, PlacementTarget = anchor, Placement = PlacementMode.Relative, HorizontalOffset = x - ShadowLeft, VerticalOffset = y - ShadowTop,
            StaysOpen = false, Child = host, PopupAnimation = PopupAnimation.None, Focusable = false
        };
        KeyboardNavigation.SetDirectionalNavigation(host, KeyboardNavigationMode.Cycle);
        host.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { popup.IsOpen = false; if (!IsEditing) { Surface?.Focus(); } e.Handled = true; }
        };
        popup.Closed += (_, _) => { if (ReferenceEquals(_openMenu, popup)) { _openMenu = null; } Surface?.MenuClosed(); };
        popup.IsOpen = true;
        _openMenu = popup;
        if (ArcMotion.GetLevel(this) != ArcMotionLevel.Off)
        {
            var scale = new ScaleTransform(.97, .97);
            far.RenderTransform = scale;
            far.RenderTransformOrigin = new Point(x < 0 || anchor.ActualWidth - x > width / 2 ? 0 : 1, 0);
            var duration = new Duration(TimeSpan.FromSeconds(.16));
            far.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ArcEasing.Enter, FillBehavior = FillBehavior.Stop });
            if (ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
            {
                var grow = new DoubleAnimation(1, duration) { EasingFunction = ArcEasing.Enter };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            }
            else { scale.ScaleX = scale.ScaleY = 1; }
        }
        if (focusFirst)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => (FirstRow(content))?.Focus());
        }
        return popup;
    }

    private Popup? _openMenu;
    internal bool IsMenuOpen => _openMenu?.IsOpen == true;
    /// <summary>The open menu's content, for tests and automation.</summary>
    internal FrameworkElement? OpenMenuContent => (((_openMenu?.Child as Border)?.Child as Border)?.Child as Border)?.Child as FrameworkElement;

    private static UIElement? FirstRow(DependencyObject root)
    {
        if (root is Border { Focusable: true } row) { return row; }
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FirstRow(VisualTreeHelper.GetChild(root, i)) is { } found) { return found; }
        }
        return null;
    }

    private Border MenuRow(string? icon, string text, string? meta, Action action, bool isChecked = false, bool checkBox = false, bool radio = false, bool enabled = true, bool focusOnHover = true, string? trailingIcon = null, double textWidth = double.NaN)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        FrameworkElement? lead = null;
        if (checkBox)
        {
            var mark = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
            mark.SetResourceReference(Border.BorderBrushProperty, isChecked ? "ArcWpf.Brush.ControlOn" : "ArcWpf.Brush.BorderStrong");
            mark.SetResourceReference(Border.BackgroundProperty, isChecked ? "ArcWpf.Brush.ControlOn" : "ArcWpf.Brush.Surface");
            var glyph = new ArcDataGridIcon { Icon = "check", Size = 12, StrokeThickness = 2.25, Visibility = isChecked ? Visibility.Visible : Visibility.Hidden, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            glyph.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.ControlGlyph");
            mark.Child = glyph;
            lead = mark;
        }
        else if (icon is not null)
        {
            var glyph = new ArcDataGridIcon { Icon = icon, Size = 16, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            glyph.SetResourceReference(ArcDataGridIcon.StrokeProperty, "ArcWpf.Brush.TextSecondary");
            lead = glyph;
        }
        if (lead is not null) { grid.Children.Add(lead); }
        var label = new TextBlock { Text = text, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        label.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        if (meta is not null)
        {
            var hint = new TextBlock { Text = meta, FontSize = 14, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            Grid.SetColumn(hint, 2);
            grid.Children.Add(hint);
        }
        if (trailingIcon is not null || radio && isChecked)
        {
            var trail = new ArcDataGridIcon { Icon = trailingIcon ?? "check", Size = 14, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            trail.SetResourceReference(ArcDataGridIcon.StrokeProperty, radio ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextSecondary");
            Grid.SetColumn(trail, 2);
            grid.Children.Add(trail);
        }
        var row = new Border { Height = 34, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(12), Child = grid, Focusable = enabled, FocusVisualStyle = null, Cursor = enabled ? Cursors.Hand : Cursors.Arrow, Opacity = enabled ? 1 : .45 };
        row.Background = Brushes.Transparent;
        AutomationProperties.SetName(row, text);
        if (!enabled) { return row; }
        row.MouseEnter += (_, _) => { if (focusOnHover) { row.Focus(); } else { row.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted"); } };
        row.MouseLeave += (_, _) => { if (!focusOnHover) { row.Background = Brushes.Transparent; } };
        row.GotKeyboardFocus += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        row.LostKeyboardFocus += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) => { action(); e.Handled = true; };
        row.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space) { action(); e.Handled = true; }
            else if (e.Key == Key.Down) { row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; }
            else if (e.Key == Key.Up) { row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)); e.Handled = true; }
        };
        return row;
    }

    private Border Separator()
    {
        var line = new Border { Height = 1, Margin = new Thickness(-4, 4, -4, 4) };
        line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.DataGridLine");
        return line;
    }

    private void CloseMenu()
    {
        if (_openMenu is not null) { _openMenu.IsOpen = false; }
    }

    private void OpenColumnsMenu()
    {
        if (_columnsButton is null) { return; }
        var panel = new StackPanel();
        void Fill()
        {
            panel.Children.Clear();
            var hidden = HiddenKeys().ToHashSet(StringComparer.Ordinal);
            var pinned = PinnedKeys().ToHashSet(StringComparer.Ordinal);
            var scroll = new StackPanel();
            foreach (var column in Columns)
            {
                bool shown = !hidden.Contains(column.Key);
                bool last = shown && Columns.Count(c => !hidden.Contains(c.Key)) == 1;
                scroll.Children.Add(MenuRow(null, column.Header, null, () => { ToggleHidden(column.Key); Fill(); }, isChecked: shown, checkBox: true, enabled: !last, trailingIcon: pinned.Contains(column.Key) ? "pin" : null));
            }
            panel.Children.Add(new ScrollViewer { Content = scroll, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 320, Focusable = false });
            panel.Children.Add(Separator());
            panel.Children.Add(MenuRow(null, "Show all columns", null, () => { SetList(HiddenColumnsProperty, Array.Empty<string>()); Fill(); }, enabled: hidden.Count > 0));
        }
        Fill();
        ShowPopup(_columnsButton, panel, _columnsButton.ActualWidth - 208, _columnsButton.ActualHeight + 6, 208);
    }

    private void ToggleHidden(string key)
    {
        var hidden = HiddenKeys().ToList();
        if (!hidden.Remove(key)) { hidden.Add(key); }
        SetList(HiddenColumnsProperty, hidden);
    }

    private void OpenDensityMenu()
    {
        if (_densityButton is null) { return; }
        var panel = new StackPanel();
        (ArcDataGridDensity Value, string Icon, string Text)[] choices = [(ArcDataGridDensity.Compact, "rows-4", "Compact"), (ArcDataGridDensity.Standard, "rows-3", "Standard"), (ArcDataGridDensity.Comfortable, "rows-2", "Comfortable")];
        foreach (var choice in choices)
        {
            panel.Children.Add(MenuRow(choice.Icon, choice.Text, null, () => { SetCurrentValue(DensityProperty, choice.Value); CloseMenu(); Surface?.Focus(); }, isChecked: Density == choice.Value, radio: true));
        }
        ShowPopup(_densityButton, panel, _densityButton.ActualWidth - 208, _densityButton.ActualHeight + 6, 208);
    }

    private void OpenExportMenu(NativeButton anchor, IReadOnlyList<ArcDataGridRow> rows)
    {
        if (ExportFormats.Count <= 1)
        {
            ExportToFile(ExportFormats.Count == 1 ? ExportFormats[0] : ArcDataGridExportFormat.Csv, rows);
            return;
        }
        var panel = new StackPanel();
        foreach (var format in ExportFormats)
        {
            bool csv = format == ArcDataGridExportFormat.Csv;
            panel.Children.Add(MenuRow(csv ? "file-text" : "file-spreadsheet", csv ? "CSV" : "Excel workbook", csv ? ".csv" : ".xlsx", () => { CloseMenu(); ExportToFile(format, rows); }));
        }
        ShowPopup(anchor, panel, anchor.ActualWidth - 208, anchor.ActualHeight + 6, 208);
    }

    internal void OpenHeaderMenu(int index, Rect button)
    {
        if (Surface is null || index >= Visible.Count) { return; }
        var column = Visible[index];
        var panel = new StackPanel();
        void Close() { CloseMenu(); Surface?.Focus(); }
        if (column.Sortable)
        {
            panel.Children.Add(MenuRow("arrow-up-narrow-wide", "Sort ascending", null, () => { Close(); SetSort([new(column.Key)]); }));
            panel.Children.Add(MenuRow("arrow-down-wide-narrow", "Sort descending", null, () => { Close(); SetSort([new(column.Key, true)]); }));
            if (SortKeys.Any(key => key.Key == column.Key)) { panel.Children.Add(MenuRow("x", "Clear sort", null, () => { Close(); SetSort(SortKeys.Where(key => key.Key != column.Key).ToArray()); })); }
            panel.Children.Add(Separator());
        }
        if (column.Filterable)
        {
            panel.Children.Add(MenuRow("list-filter", "Filter", null, () =>
            {
                Close();
                FilterRowOpen = true;
                RebuildFilterInputs();
                UpdateChrome();
                Surface?.InvalidateMeasure();
                Surface?.InvalidateVisual();
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (_filterInputs.TryGetValue(column.Key, out var input)) { input.Focus(); } });
            }));
        }
        if (column.CanGroup)
        {
            var groups = (GroupBy ?? []).ToList();
            bool grouped = groups.Contains(column.Key);
            panel.Children.Add(MenuRow("group", (grouped ? "Stop grouping by " : "Group by ") + column.Header, null, () =>
            {
                Close();
                if (grouped) { groups.Remove(column.Key); } else { groups.Add(column.Key); }
                SetList(GroupByProperty, groups);
            }));
        }
        bool pinned = index < PinnedCount;
        panel.Children.Add(MenuRow(pinned ? "pin-off" : "pin", pinned ? "Unpin column" : "Pin column", null, () =>
        {
            Close();
            var keys = PinnedKeys().ToList();
            if (pinned) { keys.Remove(column.Key); } else { keys.Add(column.Key); }
            SetList(PinnedColumnsProperty, keys);
        }));
        panel.Children.Add(MenuRow("eye-off", "Hide column", null, () => { Close(); ToggleHidden(column.Key); }, enabled: Visible.Count > 1));
        Surface.MenuColumn = index;
        ShowPopup(Surface, panel, button.Right - 208 - 4, button.Bottom + 4, 208);
    }

    private void OpenSelectFilter(NativeButton anchor, ArcDataGridColumn column)
    {
        var options = OptionsFor(column);
        var panel = new StackPanel();
        void Fill()
        {
            panel.Children.Clear();
            object? value = null;
            Filters?.TryGetValue(column.Key, out value);
            var keep = (value as IEnumerable<string>)?.ToHashSet(StringComparer.Ordinal);
            bool all = keep is null || keep.Count == options.Count;
            var list = new StackPanel();
            foreach (string option in options)
            {
                bool on = keep is null || keep.Contains(option);
                list.Children.Add(MenuRow(null, option, null, () =>
                {
                    var next = (keep ?? options.ToHashSet(StringComparer.Ordinal)).ToHashSet(StringComparer.Ordinal);
                    if (!next.Remove(option)) { next.Add(option); }
                    SetFilter(column.Key, next.Count == options.Count ? null : options.Where(next.Contains).ToArray());
                    RebuildFilterInputs();
                    Fill();
                }, isChecked: on, checkBox: true));
            }
            panel.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 240, Focusable = false });
            var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(4, 6, 4, 2) };
            var selectAll = FooterLink("Select all", !all, () => { SetFilter(column.Key, null); RebuildFilterInputs(); Fill(); });
            var clear = FooterLink("Clear", keep is null || keep.Count > 0, () => { SetFilter(column.Key, Array.Empty<string>()); RebuildFilterInputs(); Fill(); });
            DockPanel.SetDock(selectAll, Dock.Left);
            DockPanel.SetDock(clear, Dock.Right);
            footer.Children.Add(selectAll);
            footer.Children.Add(clear);
            var rule = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = footer, Margin = new Thickness(0, 4, 0, 0) };
            rule.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.DataGridLine");
            panel.Children.Add(rule);
        }
        Fill();
        ShowPopup(anchor, panel, -4, anchor.ActualHeight + 4, 208);
    }

    private NativeButton FooterLink(string text, bool enabled, Action action)
    {
        var link = new NativeButton { Content = text, IsEnabled = enabled, Style = GridStyle("MenuLink") };
        link.Click += (_, _) => action();
        return link;
    }
}
