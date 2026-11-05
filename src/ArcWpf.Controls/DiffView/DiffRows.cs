using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

// One side of a row: what a line looks like on screen, worked out when it first scrolls into view.
internal sealed class DiffSide(DiffLine line, string? language)
{
    private string? _shown;
    private List<(int Start, int Length)>? _marks;
    private bool _changedKnown;
    private (int Start, int Length)? _changed;

    public DiffLine Line { get; } = line;
    public string? Language { get; } = language;
    public DiffSide? Partner { get; set; }
    public bool IsOldOfPair { get; set; }
    public FormattedText? Text { get; set; }
    public Geometry? ChangedShape { get; set; }
    public int TextVersion { get; set; } = -1;

    public string Shown => _shown ??= DiffText.Show(Line.Text, out _marks);
    public List<(int Start, int Length)>? Marks { get { _ = Shown; return _marks; } }

    public (int Start, int Length)? Changed
    {
        get
        {
            if (_changedKnown) { return _changed; }
            _changedKnown = true;
            if (Partner is null) { return null; }
            var ranges = IsOldOfPair ? DiffText.ChangedRanges(Shown, Partner.Shown) : DiffText.ChangedRanges(Partner.Shown, Shown);
            if (ranges is { } found) { _changed = IsOldOfPair ? found.Old : found.New; }
            return _changed;
        }
    }

    // Tabs make characters wider than one cell, and CJK takes about two; a little over is fine for a scroll range.
    public int Columns
    {
        get
        {
            int columns = 0;
            foreach (char c in Shown) { columns += c >= 'ᄀ' ? 2 : 1; }
            return columns;
        }
    }
}

internal sealed class DiffRow
{
    public DiffSide? Old { get; init; }
    public DiffSide? New { get; init; }
    public bool IsGap { get; init; }
    public int GapStart { get; init; }
    public int GapCount { get; init; }
    public bool CanExpand { get; init; }
    public bool IsChange => !IsGap && !ReferenceEquals(Old, New);
    public string GapText => CanExpand
        ? GapCount == 1 ? "1 unchanged line" : $"{GapCount.ToString("N0", CultureInfo.InvariantCulture)} unchanged lines"
        : GapCount == 1 ? "1 line not in this patch" : $"{GapCount.ToString("N0", CultureInfo.InvariantCulture)} lines not in this patch";

    public string Spoken
    {
        get
        {
            if (IsGap) { return GapText; }
            if (Old is { } a && New is { } b && !ReferenceEquals(a, b)) { return $"Removed line {a.Line.OldNumber}: {a.Shown}. Added line {b.Line.NewNumber}: {b.Shown}"; }
            if (Old is { } removed && New is null) { return $"Removed line {removed.Line.OldNumber}: {removed.Shown}"; }
            if (New is { } added && Old is null) { return $"Added line {added.Line.NewNumber}: {added.Shown}"; }
            return $"Line {New?.Line.NewNumber}: {New?.Shown}";
        }
    }
}

internal static class DiffRowBuilder
{
    public static List<DiffRow> Build(DiffFile file, bool split, int context, ISet<int> expanded, int maxRows, out int droppedChanges)
    {
        var rows = new List<DiffRow>();
        droppedChanges = 0;
        var lines = file.Lines;
        if (file.AddedLines + file.RemovedLines == 0 && file.Skipped.Count == 0) { return rows; }
        string? language = file.Language;
        var removed = new List<DiffLine>();
        var added = new List<DiffLine>();

        void Flush()
        {
            if (split)
            {
                for (int i = 0; i < Math.Max(removed.Count, added.Count); i++)
                {
                    var left = i < removed.Count ? new DiffSide(removed[i], language) { IsOldOfPair = true } : null;
                    var right = i < added.Count ? new DiffSide(added[i], language) : null;
                    if (left is not null && right is not null) { left.Partner = right; right.Partner = left; }
                    rows.Add(new DiffRow { Old = left, New = right });
                }
            }
            else
            {
                var sides = new DiffSide[added.Count];
                for (int i = 0; i < added.Count; i++) { sides[i] = new DiffSide(added[i], language); }
                for (int i = 0; i < removed.Count; i++)
                {
                    var side = new DiffSide(removed[i], language) { IsOldOfPair = true };
                    if (i < sides.Length) { side.Partner = sides[i]; sides[i].Partner = side; }
                    rows.Add(new DiffRow { Old = side });
                }
                foreach (var side in sides) { rows.Add(new DiffRow { New = side }); }
            }
            removed.Clear();
            added.Clear();
        }

        int at = 0;
        while (at < lines.Count)
        {
            if (rows.Count + removed.Count + added.Count >= maxRows) { break; }
            if (file.Skipped.TryGetValue(at, out int skipped)) { Flush(); rows.Add(new DiffRow { IsGap = true, GapStart = at, GapCount = skipped }); }
            var line = lines[at];
            if (line.Kind == DiffLineKind.Removed) { removed.Add(line); at++; continue; }
            if (line.Kind == DiffLineKind.Added) { added.Add(line); at++; continue; }
            Flush();

            int end = at + 1;
            while (end < lines.Count && lines[end].Kind == DiffLineKind.Context && !file.Skipped.ContainsKey(end)) { end++; }
            bool leading = at == 0 || file.Skipped.ContainsKey(at);
            bool trailing = end == lines.Count || file.Skipped.ContainsKey(end);
            int hideFrom = leading ? at : at + context, hideTo = trailing ? end : end - context;
            for (int i = at; i < end;)
            {
                if (i == hideFrom && hideTo - hideFrom >= 2 && !expanded.Contains(hideFrom))
                {
                    rows.Add(new DiffRow { IsGap = true, GapStart = hideFrom, GapCount = hideTo - hideFrom, CanExpand = true });
                    i = hideTo;
                    continue;
                }
                var side = new DiffSide(lines[i], language);
                rows.Add(new DiffRow { Old = side, New = side });
                i++;
                if (rows.Count >= maxRows) { end = i; break; }
            }
            at = end;
        }
        Flush();
        for (; at < lines.Count; at++) { if (lines[at].Kind != DiffLineKind.Context) { droppedChanges++; } }
        return rows;
    }
}

internal sealed class DiffPalette(FrameworkElement owner)
{
    public Brush Added { get; } = Find(owner, "DiffAdded");
    public Brush AddedWord { get; } = Find(owner, "DiffAddedWord");
    public Brush AddedStrong { get; } = Find(owner, "DiffAddedStrong");
    public Brush AddedSign { get; } = Find(owner, "ArcSuccess");
    public Brush Removed { get; } = Find(owner, "DiffRemoved");
    public Brush RemovedWord { get; } = Find(owner, "DiffRemovedWord");
    public Brush RemovedStrong { get; } = Find(owner, "DiffRemovedStrong");
    public Brush RemovedSign { get; } = Find(owner, "ArcDanger");
    public Brush Selection { get; } = Find(owner, "DiffSelection");
    public Brush Empty { get; } = Find(owner, "SurfaceMuted");
    public Brush Rule { get; } = Find(owner, "BorderSubtle");
    public Brush Number { get; } = Find(owner, "TextMuted");
    public Brush Ink { get; } = Find(owner, "Foreground");
    public Brush InkQuiet { get; } = Find(owner, "TextSecondary");
    public Brush Mark { get; } = Find(owner, "ArcWarning");
    private readonly FrameworkElement _owner = owner;
    private readonly Dictionary<string, Brush> _tokens = [];

    public Brush Token(string key)
    {
        if (!_tokens.TryGetValue(key, out var brush)) { brush = _owner.TryFindResource(key) as Brush ?? Ink; _tokens[key] = brush; }
        return brush;
    }

    private static Brush Find(FrameworkElement owner, string role) => owner.TryFindResource("ArcWpf.Brush." + role) as Brush ?? Brushes.Gray;
}

internal sealed class DiffRowList : DrawnRowList
{
    private DiffPalette? _palette;

    public DiffRowList() => ThemeWatch.Add(this, Repaint);

    public bool IsSplit { get; set; }
    public int NumberDigits { get; set; } = 1;
    public int TextVersion { get; private set; }
    public DiffPalette Palette => _palette ??= new DiffPalette(this);
    public double NumberWidth => Math.Ceiling(Cell * NumberDigits) + 12;
    public double UnifiedGutter => NumberWidth * 2 + SignWidth;
    public double SplitGutter => NumberWidth + SignWidth;
    public double SignWidth => Math.Ceiling(Cell) + 12;
    public event Action<DiffRow>? GapInvoked;
    /// <summary>Which version a copy takes in split view: the side last clicked.</summary>
    public bool CopiesOldSide { get; private set; }

    public void Repaint()
    {
        _palette = null;
        TextVersion++;
        InvalidateRows();
    }

    public void NoteClick(double x) { if (IsSplit) { CopiesOldSide = x < ActualWidth / 2; } }

    public void InvokeGap(DiffRow row) => GapInvoked?.Invoke(row);

    protected override DependencyObject GetContainerForItemOverride() => new DiffRowItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is DiffRowItem;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        var container = (DiffRowItem)element;
        var row = (DiffRow)item;
        container.Row = row;
        container.Cursor = row.IsGap && row.CanExpand ? Cursors.Hand : null;
        AutomationProperties.SetName(container, row.Spoken);
        base.PrepareContainerForItemOverride(element, item);
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        base.ClearContainerForItemOverride(element, item);
        ((DiffRowItem)element).Row = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && SelectedItem is DiffRow { IsGap: true, CanExpand: true } gap)
        {
            InvokeGap(gap);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

internal sealed class DiffRowItem : DrawnRowItem
{
    public DiffRow? Row { get; set; }

    private DiffRowList? List => ItemsControl.ItemsControlFromItemContainer(this) as DiffRowList;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        List?.NoteClick(e.GetPosition(this).X);
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Row is { IsGap: true, CanExpand: true } gap && IsMouseOver) { List?.InvokeGap(gap); e.Handled = true; }
    }

    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); if (Row is { IsGap: true }) { InvalidateVisual(); } }

    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); if (Row is { IsGap: true }) { InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        var list = List;
        var row = Row;
        double width = ActualWidth, height = ActualHeight;
        if (list is null || row is null || width <= 0) { return; }
        var palette = list.Palette;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        if (row.IsGap) { DrawGap(dc, list, row, palette, width, height); }
        else if (list.IsSplit)
        {
            double half = Math.Floor(width / 2);
            DrawSplitSide(dc, list, row.Old, true, 0, half, height);
            DrawSplitSide(dc, list, row.New, false, half, width - half, height);
            dc.DrawRectangle(palette.Rule, null, new Rect(half, 0, 1, height));
        }
        else { DrawUnified(dc, list, row, width, height); }
        if (IsSelected) { dc.DrawRectangle(palette.Selection, null, new Rect(0, 0, width, height)); }
    }

    private void DrawGap(DrawingContext dc, DiffRowList list, DiffRow row, DiffPalette palette, double width, double height)
    {
        dc.DrawRectangle(palette.Empty, null, new Rect(0, 0, width, height));
        bool hot = IsMouseOver && row.CanExpand;
        string text = "···  " + row.GapText;
        var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, list.Face, 12, hot ? palette.Ink : palette.Number, list.PixelsPerDip);
        dc.DrawText(label, new Point(14, Math.Round((height - label.Height) / 2)));
    }

    private void DrawUnified(DrawingContext dc, DiffRowList list, DiffRow row, double width, double height)
    {
        var palette = list.Palette;
        var side = row.Old ?? row.New!;
        var kind = side.Line.Kind;
        Fill(dc, palette, kind, 0, width, list.UnifiedGutter - 4, height);
        double number = list.NumberWidth;
        Number(dc, list, row.Old?.Line.OldNumber, number - 6, height);
        Number(dc, list, row.New?.Line.NewNumber, number * 2 - 6, height);
        Sign(dc, list, kind, number * 2 + 6, height);
        Body(dc, list, side, list.UnifiedGutter, width - list.UnifiedGutter, height);
    }

    private void DrawSplitSide(DrawingContext dc, DiffRowList list, DiffSide? side, bool old, double x, double width, double height)
    {
        var palette = list.Palette;
        if (side is null) { dc.DrawRectangle(palette.Empty, null, new Rect(x, 0, width, height)); return; }
        var kind = side.Line.Kind;
        Fill(dc, palette, kind, x, width, list.SplitGutter - 4, height);
        Number(dc, list, old ? side.Line.OldNumber : side.Line.NewNumber, x + list.NumberWidth - 6, height);
        Sign(dc, list, kind, x + list.NumberWidth + 6, height);
        Body(dc, list, side, x + list.SplitGutter, width - list.SplitGutter, height);
    }

    // code-suggestion .row: a tinted line under a slightly stronger gutter.
    private static void Fill(DrawingContext dc, DiffPalette palette, DiffLineKind kind, double x, double width, double gutter, double height)
    {
        if (kind == DiffLineKind.Context) { return; }
        dc.DrawRectangle(kind == DiffLineKind.Added ? palette.Added : palette.Removed, null, new Rect(x, 0, width, height));
        dc.DrawRectangle(kind == DiffLineKind.Added ? palette.AddedStrong : palette.RemovedStrong, null, new Rect(x, 0, gutter, height));
    }

    private static void Number(DrawingContext dc, DiffRowList list, int? number, double right, double height)
    {
        if (number is not { } value) { return; }
        var text = new FormattedText(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, list.Face, 12, list.Palette.Number, list.PixelsPerDip);
        dc.DrawText(text, new Point(right - text.WidthIncludingTrailingWhitespace, Math.Round((height - text.Height) / 2)));
    }

    private static void Sign(DrawingContext dc, DiffRowList list, DiffLineKind kind, double x, double height)
    {
        if (kind == DiffLineKind.Context) { return; }
        var palette = list.Palette;
        var text = new FormattedText(kind == DiffLineKind.Added ? "+" : "-", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, list.Face, DiffRowList.TextSize, kind == DiffLineKind.Added ? palette.AddedSign : palette.RemovedSign, list.PixelsPerDip);
        dc.DrawText(text, new Point(x, Math.Round((height - text.Height) / 2)));
    }

    private static void Body(DrawingContext dc, DiffRowList list, DiffSide side, double x, double width, double height)
    {
        if (width <= 0) { return; }
        EnsureText(list, side);
        var text = side.Text!;
        double originX = x - list.ScrollX, originY = Math.Round((height - text.Height) / 2);
        dc.PushClip(new RectangleGeometry(new Rect(x, 0, width, height)));
        if (side.ChangedShape is { } shape)
        {
            dc.PushTransform(new TranslateTransform(originX, originY));
            dc.DrawGeometry(side.IsOldOfPair ? list.Palette.RemovedWord : list.Palette.AddedWord, null, shape);
            dc.Pop();
        }
        dc.DrawText(text, new Point(originX, originY));
        dc.Pop();
    }

    private static void EnsureText(DiffRowList list, DiffSide side)
    {
        if (side.Text is not null && side.TextVersion == list.TextVersion) { return; }
        var palette = list.Palette;
        string shown = side.Shown;
        // code-suggestion: a removed line reads quietly, in one color.
        bool removed = side.Line.Kind == DiffLineKind.Removed;
        var text = new FormattedText(shown, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, list.Face, DiffRowList.TextSize, removed ? palette.InkQuiet : palette.Ink, list.PixelsPerDip);
        if (!removed && side.Language is { } language && shown.Length <= 2000) { ApplyTokens(text, palette, shown, language); }
        if (side.Marks is { } marks) { foreach (var (start, length) in marks) { text.SetForegroundBrush(palette.Mark, start, length); } }
        side.Text = text;
        side.ChangedShape = side.Changed is { Length: > 0 } changed ? text.BuildHighlightGeometry(new Point(0, 0), changed.Start, changed.Length) : null;
        side.TextVersion = list.TextVersion;
    }

    // Line by line, so a block comment or template string that spans lines isn't recognised past its first line.
    private static void ApplyTokens(FormattedText text, DiffPalette palette, string shown, string language)
    {
        foreach (System.Text.RegularExpressions.Match match in CodeTokens.Pattern.Matches(shown))
        {
            if (CodeTokens.Key(match.Value, shown, match.Index, language) is { } key) { text.SetForegroundBrush(palette.Token(key), match.Index, match.Length); }
        }
    }
}
