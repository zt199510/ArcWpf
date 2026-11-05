using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum DiffViewMode { Unified, Split }

/// <summary>
/// Shows what changed in one or more files, for reviewing an agent's edits before they are allowed. Takes its look from Arc's Code suggestion, which reviews one file's hunks;
/// its docs leave large and side-by-side diffs to a dedicated tool, and this is that tool.
/// A reader decides from this view, so whatever can't be shown as a line is stated in a note rather than left out.
/// </summary>
[TemplatePart(Name = "PART_Path", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Stats", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Actions", Type = typeof(Panel))]
[TemplatePart(Name = "PART_FileBar", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Files", Type = typeof(Panel))]
[TemplatePart(Name = "PART_Notes", Type = typeof(Panel))]
[TemplatePart(Name = "PART_RowsHost", Type = typeof(Decorator))]
[TemplatePart(Name = "PART_Empty", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_HorizontalBar", Type = typeof(ScrollBar))]
public class DiffView : Control
{
    // A virtualizing list measured with unlimited height would realize every row, so an unbounded parent gets this instead.
    private const double UnboundedBodyHeight = 480;

    public static readonly DependencyProperty FilesProperty = DependencyProperty.Register(nameof(Files), typeof(IEnumerable<DiffFile>), typeof(DiffView), new FrameworkPropertyMetadata(null, OnFilesChanged));
    public static readonly DependencyProperty SelectedFileProperty = DependencyProperty.Register(nameof(SelectedFile), typeof(DiffFile), typeof(DiffView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DiffView)d).Rebuild(false)));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(DiffViewMode), typeof(DiffView), new FrameworkPropertyMetadata(DiffViewMode.Unified, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DiffView)d).Rebuild(true)));
    private static readonly DependencyPropertyKey EffectiveModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EffectiveMode), typeof(DiffViewMode), typeof(DiffView), new FrameworkPropertyMetadata(DiffViewMode.Unified));
    public static readonly DependencyProperty EffectiveModeProperty = EffectiveModePropertyKey.DependencyProperty;
    public static readonly DependencyProperty MinSplitWidthProperty = DependencyProperty.Register(nameof(MinSplitWidth), typeof(double), typeof(DiffView), new FrameworkPropertyMetadata(720d, (d, _) => ((DiffView)d).Rebuild(true)));
    public static readonly DependencyProperty ContextLinesProperty = DependencyProperty.Register(nameof(ContextLines), typeof(int), typeof(DiffView), new FrameworkPropertyMetadata(3, (d, _) => ((DiffView)d).Rebuild(true)), value => value is >= 0);
    public static readonly DependencyProperty MaxRowsProperty = DependencyProperty.Register(nameof(MaxRows), typeof(int), typeof(DiffView), new FrameworkPropertyMetadata(20000, (d, _) => ((DiffView)d).Rebuild(true)), value => value is > 0);
    public static readonly DependencyProperty MaxBodyHeightProperty = DependencyProperty.Register(nameof(MaxBodyHeight), typeof(double), typeof(DiffView), new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(DiffView), new FrameworkPropertyMetadata("No changes", (d, _) => ((DiffView)d).Rebuild(true)));
    private static readonly DependencyPropertyKey ChangeCountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ChangeCount), typeof(int), typeof(DiffView), new FrameworkPropertyMetadata(0));
    public static readonly DependencyProperty ChangeCountProperty = ChangeCountPropertyKey.DependencyProperty;

    private readonly DiffRowList _rows = new();
    private readonly ConditionalWeakTable<DiffFile, HashSet<int>> _expanded = [];
    private readonly List<int> _changeStarts = [];
    private readonly ToolbarButton _previous, _next, _modeToggle, _expandAll;
    private readonly List<(DiffFile File, ToolbarButton Tab)> _tabs = [];
    private List<DiffRow> _current = [];
    private TextBlock? _path, _stats, _empty;
    private Panel? _actions, _files, _notes;
    private FrameworkElement? _fileBar, _clip;
    private Decorator? _rowsHost;
    private ScrollBar? _bar;
    private double _textWidest;
    private bool _syncing;

    static DiffView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(DiffView), new FrameworkPropertyMetadata(typeof(DiffView)));

    public DiffView()
    {
        _previous = AiUi.Icon(Lucide.ChevronUp, 28, 16, "Previous change");
        _next = AiUi.Icon(Lucide.ChevronDown, 28, 16, "Next change");
        _expandAll = AiUi.Icon(Lucide.UnfoldVertical, 28, 16, "Show all lines");
        _modeToggle = AiUi.Icon(Lucide.Columns2, 28, 16, "Split view");
        _previous.ToolTip = "Previous change (Shift+F7)";
        _next.ToolTip = "Next change (F7)";
        _expandAll.ToolTip = "Show all lines";
        _previous.Click += (_, _) => PreviousChange();
        _next.Click += (_, _) => NextChange();
        _expandAll.Click += (_, _) => ExpandAll();
        _modeToggle.Click += (_, _) => SetCurrentValue(ModeProperty, Mode == DiffViewMode.Split ? DiffViewMode.Unified : DiffViewMode.Split);
        _rows.GapInvoked += Expand;
        _rows.HorizontalRequested += ScrollBy;
        _rows.CopyRequested += CopySelection;
        AddHandler(KeyDownEvent, new KeyEventHandler(ChangeKeys), true);
        SizeChanged += (_, _) => { ClipFrame(); if (ResolveMode() != EffectiveMode) { Rebuild(true); } else { UpdateBar(); UpdateActions(); } };
        Loaded += (_, _) => Rebuild(true);
    }

    public IEnumerable<DiffFile>? Files { get => (IEnumerable<DiffFile>?)GetValue(FilesProperty); set => SetValue(FilesProperty, value); }
    public DiffFile? SelectedFile { get => (DiffFile?)GetValue(SelectedFileProperty); set => SetValue(SelectedFileProperty, value); }
    /// <summary>The layout asked for. See <see cref="EffectiveMode"/> for the one on screen.</summary>
    public DiffViewMode Mode { get => (DiffViewMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    /// <summary>Unified when the view is narrower than <see cref="MinSplitWidth"/> or the file only gains or only loses lines.</summary>
    public DiffViewMode EffectiveMode => (DiffViewMode)GetValue(EffectiveModeProperty);
    public double MinSplitWidth { get => (double)GetValue(MinSplitWidthProperty); set => SetValue(MinSplitWidthProperty, value); }
    /// <summary>Unchanged lines kept on each side of a change.</summary>
    public int ContextLines { get => (int)GetValue(ContextLinesProperty); set => SetValue(ContextLinesProperty, value); }
    /// <summary>Rows drawn per file. Past this a note says how many changed lines are not shown.</summary>
    public int MaxRows { get => (int)GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }
    public double MaxBodyHeight { get => (double)GetValue(MaxBodyHeightProperty); set => SetValue(MaxBodyHeightProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    /// <summary>Separate changes in the selected file: what the previous and next buttons step through.</summary>
    public int ChangeCount => (int)GetValue(ChangeCountProperty);

    internal DiffRowList RowsForTest => _rows;
    internal IReadOnlyList<DiffRow> CurrentRowsForTest => _current;
    internal string CopyTextForTest() => SelectionText();

    public override void OnApplyTemplate()
    {
        if (_rowsHost is not null) { _rowsHost.Child = null; }
        _actions?.Children.Clear();
        if (_bar is not null) { _bar.ValueChanged -= BarMoved; }
        base.OnApplyTemplate();
        _path = GetTemplateChild("PART_Path") as TextBlock;
        _stats = GetTemplateChild("PART_Stats") as TextBlock;
        _empty = GetTemplateChild("PART_Empty") as TextBlock;
        _actions = GetTemplateChild("PART_Actions") as Panel;
        _files = GetTemplateChild("PART_Files") as Panel;
        _notes = GetTemplateChild("PART_Notes") as Panel;
        _fileBar = GetTemplateChild("PART_FileBar") as FrameworkElement;
        _clip = GetTemplateChild("PART_Clip") as FrameworkElement;
        _rowsHost = GetTemplateChild("PART_RowsHost") as Decorator;
        _bar = GetTemplateChild("PART_HorizontalBar") as ScrollBar;
        if (_rowsHost is not null) { _rowsHost.Child = _rows; }
        if (_bar is not null) { _bar.ValueChanged += BarMoved; }
        if (_actions is not null) { foreach (var button in new[] { _previous, _next, _expandAll, _modeToggle }) { _actions.Children.Add(button); } }
        if (_clip is not null) { _clip.SizeChanged += (_, _) => ClipFrame(); }
        RebuildTabs();
        Rebuild(false);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _rows.MaxHeight = double.IsInfinity(constraint.Height) ? Math.Min(MaxBodyHeight, UnboundedBodyHeight) : MaxBodyHeight;
        return base.MeasureOverride(constraint);
    }

    /// <summary>Moves to the next change. Does nothing past the last one.</summary>
    public void NextChange()
    {
        int anchor = Anchor();
        foreach (int start in _changeStarts) { if (start > anchor) { GoTo(start); return; } }
    }

    /// <summary>Moves to the previous change. Does nothing before the first one.</summary>
    public void PreviousChange()
    {
        int anchor = Anchor();
        for (int i = _changeStarts.Count - 1; i >= 0; i--) { if (_changeStarts[i] < anchor) { GoTo(_changeStarts[i]); return; } }
    }

    /// <summary>Unfolds every hidden stretch of unchanged lines in the selected file.</summary>
    public void ExpandAll()
    {
        if (SelectedFile is not { } file) { return; }
        var set = _expanded.GetOrCreateValue(file);
        foreach (var row in _current) { if (row is { IsGap: true, CanExpand: true }) { set.Add(row.GapStart); } }
        Rebuild(true);
    }

    private int Anchor()
    {
        if (_rows.SelectedIndex >= 0) { return _rows.SelectedIndex; }
        return _rows.Scroller is { } scroller && scroller.VerticalOffset > 0 ? (int)scroller.VerticalOffset : -1;
    }

    private void GoTo(int index)
    {
        bool hadFocus = _rows.IsKeyboardFocusWithin;
        SelectOnly(index);
        _rows.UpdateLayout();
        // Leave a little of what comes before the change in view.
        _rows.Scroller?.ScrollToVerticalOffset(Math.Max(0, index - 3));
        _rows.UpdateLayout();
        if (hadFocus && _rows.ItemContainerGenerator.ContainerFromIndex(index) is UIElement item) { item.Focus(); }
        // Focus moving with Shift held (Shift+F7) extends the selection, but jumping selects just the change.
        SelectOnly(index);
    }

    // SelectedIndex alone does nothing when the index is already the first of several selected rows.
    private void SelectOnly(int index)
    {
        _rows.UnselectAll();
        _rows.SelectedIndex = index;
    }

    private void ChangeKeys(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F7 || !IsKeyboardFocusWithin) { return; }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) { PreviousChange(); } else { NextChange(); }
        e.Handled = true;
    }

    private void Expand(DiffRow row)
    {
        if (SelectedFile is not { } file) { return; }
        _expanded.GetOrCreateValue(file).Add(row.GapStart);
        int index = _current.IndexOf(row);
        bool hadFocus = _rows.IsKeyboardFocusWithin;
        Rebuild(true);
        if (index < 0 || index >= _current.Count) { return; }
        SelectOnly(index);
        _rows.UpdateLayout();
        if (hadFocus && _rows.ItemContainerGenerator.ContainerFromIndex(index) is UIElement item) { item.Focus(); }
    }

    private static void OnFilesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DiffView)d;
        if (e.OldValue is INotifyCollectionChanged before) { CollectionChangedEventManager.RemoveHandler(before, view.FilesMutated); }
        if (e.NewValue is INotifyCollectionChanged after) { CollectionChangedEventManager.AddHandler(after, view.FilesMutated); }
        view.FilesReplaced();
    }

    private void FilesMutated(object? sender, NotifyCollectionChangedEventArgs e) => FilesReplaced();

    private void FilesReplaced()
    {
        var files = Files?.ToList() ?? [];
        RebuildTabs();
        var selected = SelectedFile is { } current && files.Contains(current) ? current : files.FirstOrDefault();
        if (!ReferenceEquals(selected, SelectedFile)) { SetCurrentValue(SelectedFileProperty, selected); }
        else { Rebuild(true); }
    }

    private void RebuildTabs()
    {
        if (_files is null) { return; }
        _files.Children.Clear();
        _tabs.Clear();
        var files = Files?.ToList() ?? [];
        foreach (var file in files)
        {
            var tab = new ToolbarButton(8) { Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 4, 0), ToolTip = DiffText.Show(file.Path, out _) };
            tab.Roles = self => (self.IsPressed || ReferenceEquals(SelectedFile, file) ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, ReferenceEquals(SelectedFile, file) ? "Foreground" : "TextSecondary");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(AiUi.Line(DiffText.Show(System.IO.Path.GetFileName(file.Path), out _), 12, 16.8, "Foreground", false, false));
            var stats = new TextBlock { Margin = new Thickness(8, 0, 0, 0), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            FillStats(stats, file);
            row.Children.Add(stats);
            tab.Content = row;
            AutomationProperties.SetName(tab, $"{file.Path}, {file.AddedLines} added, {file.RemovedLines} removed");
            tab.Click += (_, _) => SetCurrentValue(SelectedFileProperty, file);
            _files.Children.Add(tab);
            _tabs.Add((file, tab));
        }
        if (_fileBar is not null) { _fileBar.Visibility = files.Count > 1 ? Visibility.Visible : Visibility.Collapsed; }
    }

    private static void FillStats(TextBlock block, DiffFile file)
    {
        block.Inlines.Clear();
        if (file.IsBinary) { return; }
        var plus = new Run("+" + file.AddedLines.ToString("N0", CultureInfo.InvariantCulture));
        plus.SetResourceReference(TextElement.ForegroundProperty, file.AddedLines > 0 ? "ArcWpf.Brush.ArcSuccess" : "ArcWpf.Brush.TextMuted");
        var minus = new Run("  −" + file.RemovedLines.ToString("N0", CultureInfo.InvariantCulture));
        minus.SetResourceReference(TextElement.ForegroundProperty, file.RemovedLines > 0 ? "ArcWpf.Brush.ArcDanger" : "ArcWpf.Brush.TextMuted");
        block.Inlines.Add(plus);
        block.Inlines.Add(minus);
    }

    private DiffViewMode ResolveMode()
    {
        bool wide = ActualWidth <= 0 || ActualWidth >= MinSplitWidth;
        return Mode == DiffViewMode.Split && wide && SelectedFile is { AddedLines: > 0, RemovedLines: > 0 } ? DiffViewMode.Split : DiffViewMode.Unified;
    }

    private void Rebuild(bool keepPlace)
    {
        if (_syncing || _rowsHost is null) { return; }
        _syncing = true;
        try
        {
            var file = SelectedFile;
            if (file is null && Files?.FirstOrDefault() is { } first) { SetCurrentValue(SelectedFileProperty, first); file = first; }
            var mode = ResolveMode();
            SetValue(EffectiveModePropertyKey, mode);
            (int? Old, int? New)? anchor = keepPlace ? TopLine() : null;
            int dropped = 0;
            bool split = mode == DiffViewMode.Split;
            _current = file is null ? [] : DiffRowBuilder.Build(file, split, ContextLines, _expanded.GetOrCreateValue(file), MaxRows, out dropped);

            _rows.IsSplit = split;
            _rows.MeasureCell();
            _rows.NumberDigits = file is null || file.Lines.Count == 0 ? 1 : Math.Max(1, Math.Max(file.Lines.Max(line => line.OldNumber ?? 0), file.Lines.Max(line => line.NewNumber ?? 0)).ToString(CultureInfo.InvariantCulture).Length);
            _textWidest = _current.Count == 0 ? 0 : _current.Max(row => Math.Max(row.Old?.Columns ?? 0, row.New?.Columns ?? 0)) * _rows.Cell + 20;
            _rows.ItemsSource = _current;
            _rows.Visibility = _current.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _rows.Repaint();

            _changeStarts.Clear();
            for (int i = 0; i < _current.Count; i++)
            {
                if (_current[i].IsChange && (i == 0 || !_current[i - 1].IsChange)) { _changeStarts.Add(i); }
            }
            SetValue(ChangeCountPropertyKey, _changeStarts.Count);

            UpdateHeader(file);
            UpdateNotes(file, dropped);
            if (_empty is not null)
            {
                bool showEmpty = _current.Count == 0;
                _empty.Visibility = showEmpty ? Visibility.Visible : Visibility.Collapsed;
                // With a note above, "No changes" would contradict it: something did change, just not the text.
                _empty.Text = file is { IsBinary: true } ? "Binary file" : file is { Notes.Count: > 0 } ? "The text itself is unchanged." : EmptyText;
            }
            UpdateActions();
            UpdateBar();
            foreach (var (_, tab) in _tabs) { tab.Refresh(false); }
            if (_tabs.Find(entry => ReferenceEquals(entry.File, file)).Tab is { } active) { active.BringIntoView(); }
            RestorePlace(anchor);
        }
        finally { _syncing = false; }
    }

    private (int? Old, int? New)? TopLine()
    {
        if (_rows.Scroller is not { VerticalOffset: > 0 } scroller) { return null; }
        int top = (int)scroller.VerticalOffset;
        for (int i = top; i < _current.Count; i++)
        {
            if (!_current[i].IsGap) { return (_current[i].Old?.Line.OldNumber, _current[i].New?.Line.NewNumber); }
        }
        return null;
    }

    private void RestorePlace((int? Old, int? New)? anchor)
    {
        if (_rows.Scroller is not { } scroller) { return; }
        int target = 0;
        if (anchor is { } line)
        {
            target = _current.FindIndex(row => !row.IsGap && ((line.New is { } n && row.New?.Line.NewNumber >= n) || (line.New is null && line.Old is { } o && row.Old?.Line.OldNumber >= o)));
            if (target < 0) { target = 0; }
        }
        _rows.UpdateLayout();
        scroller.ScrollToVerticalOffset(target);
    }

    private void UpdateHeader(DiffFile? file)
    {
        if (_path is not null)
        {
            _path.Text = file is null ? "" : DiffText.Show(file.Status == DiffFileStatus.Renamed ? $"{file.OldPath} → {file.Path}" : file.Path, out _);
            _path.ToolTip = string.IsNullOrEmpty(_path.Text) ? null : _path.Text;
        }
        if (_stats is not null) { if (file is null) { _stats.Inlines.Clear(); } else { FillStats(_stats, file); } }
        AutomationProperties.SetName(this, file is null ? "Diff" : $"Diff of {file.Path}: {file.AddedLines} added, {file.RemovedLines} removed");
    }

    private void UpdateNotes(DiffFile? file, int dropped)
    {
        if (_notes is null) { return; }
        _notes.Children.Clear();
        if (file is null) { return; }
        foreach (string note in file.Notes) { _notes.Children.Add(Note(note)); }
        if (dropped > 0) { _notes.Children.Add(Note($"Only the first {MaxRows.ToString("N0", CultureInfo.InvariantCulture)} rows are shown. {dropped.ToString("N0", CultureInfo.InvariantCulture)} changed lines are not.")); }
    }

    private static Border Note(string text)
    {
        var note = new Border { Padding = new Thickness(14, 6, 14, 6), BorderThickness = new Thickness(0, 0, 0, 1) };
        note.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
        note.SetResourceReference(Border.BorderBrushProperty, "ArcWpf.Brush.BorderSubtle");
        note.Child = AiUi.Wrapped(text, 12, 16.8, "TextSecondary");
        return note;
    }

    private void UpdateActions()
    {
        var file = SelectedFile;
        bool any = _changeStarts.Count > 0;
        _previous.Visibility = _next.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        _expandAll.Visibility = _current.Exists(row => row is { IsGap: true, CanExpand: true }) ? Visibility.Visible : Visibility.Collapsed;
        bool canSplit = ActualWidth >= MinSplitWidth && file is { AddedLines: > 0, RemovedLines: > 0 };
        _modeToggle.Visibility = canSplit ? Visibility.Visible : Visibility.Collapsed;
        bool split = EffectiveMode == DiffViewMode.Split;
        _modeToggle.Content = Lucide.Glyph(split ? Lucide.Rows2 : Lucide.Columns2, 16);
        _modeToggle.ToolTip = split ? "Unified view" : "Split view";
        AutomationProperties.SetName(_modeToggle, split ? "Unified view" : "Split view");
    }

    private double TextViewport()
    {
        double width = _rows.ActualWidth > 0 ? _rows.ActualWidth : ActualWidth;
        return Math.Max(0, _rows.IsSplit ? Math.Floor(width / 2) - _rows.SplitGutter : width - _rows.UnifiedGutter);
    }

    private void UpdateBar()
    {
        if (_bar is null) { return; }
        double viewport = TextViewport();
        double range = Math.Max(0, _textWidest - viewport);
        _bar.ViewportSize = viewport;
        _bar.Maximum = range;
        _bar.Visibility = range > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_bar.Value > range) { _bar.Value = range; }
        _rows.ScrollX = Math.Min(_rows.ScrollX, range);
    }

    private void ScrollBy(double delta)
    {
        if (_bar is null) { return; }
        _bar.Value = Math.Clamp(_bar.Value + delta, 0, _bar.Maximum);
    }

    private void BarMoved(object sender, RoutedPropertyChangedEventArgs<double> e) => _rows.ScrollX = e.NewValue;

    private void ClipFrame()
    {
        if (_clip is null || _clip.ActualWidth <= 0) { return; }
        // The rows are drawn right up to the frame, so they need cutting to its inner radius.
        _clip.Clip = new RectangleGeometry(new Rect(0, 0, _clip.ActualWidth, _clip.ActualHeight), 13, 13);
    }

    private string SelectionText()
    {
        var selected = _rows.SelectedItems.Cast<DiffRow>().ToHashSet();
        var lines = new List<string>();
        foreach (var row in _current)
        {
            if (!selected.Contains(row) || row.IsGap) { continue; }
            // Split view has a version per column, so the column last clicked says which one. Unified rows have one line each.
            var side = _rows.IsSplit ? (_rows.CopiesOldSide ? row.Old : row.New) : row.Old ?? row.New;
            if (side is not null) { lines.Add(side.Line.Text); }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private void CopySelection()
    {
        string text = SelectionText();
        if (text.Length == 0) { return; }
        // Another program can be holding the clipboard open; losing one copy is better than crashing the host.
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }
}
