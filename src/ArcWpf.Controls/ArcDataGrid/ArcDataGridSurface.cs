using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>
/// Draws the header, filter row, body, selection and totals. Only rows in view are drawn, so scroll cost does not grow with
/// the row count, and rows are plain drawing rather than elements, so they can slide, flash and fade without layout.
/// </summary>
internal sealed class ArcDataGridSurface : FrameworkElement
{
    private enum Zone { None, HeadSort, HeadMenu, HeadResize, HeadCheck, GutterCheck, GroupToggle, Cell, FillHandle, Filter, Totals }
    private readonly record struct Hit(Zone Zone, int Column, int Row);

    private readonly ArcDataGrid _grid;
    private readonly ControlSpringSet _selection, _activeRect;
    private readonly Dictionary<string, Fade> _fades = [];
    private readonly Dictionary<string, Roll> _rolls = [];
    private readonly Dictionary<(string, double, bool, Brush, double, TextAlignment), FormattedText> _texts = [];
    private Dictionary<string, (double From, bool FadeIn)>? _slide;
    private long _slideStart;
    private readonly List<int> _slideExtra = [];
    private bool _loop;
    private long _lastFrame;
    private double _pinShadow;
    private Hit _hover;
    private Hit _press;
    private bool _selecting, _filling, _resizing;
    private (int Column, int Row) _fillTarget;
    private double _resizeStart, _resizeWidth, _resizeDrag;
    private int _resizeColumn = -1;
    private Point _lastMouse;
    private readonly DispatcherTimer _autoScroll;
    private bool _pressWasActive;
    internal int MenuColumn { get; set; } = -1;

    internal double ScrollX { get; private set; }
    internal double ScrollY { get; private set; }
    internal double HeadHeight => 40 + (_grid.FilterRowOpen ? 40 : 0);
    internal double FootHeight => _grid.Totals ? 40 : 0;
    internal double BodyHeight => Math.Max(0, ActualHeight - HeadHeight - FootHeight);

    public ArcDataGridSurface(ArcDataGrid grid)
    {
        _grid = grid;
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        _selection = new ControlSpringSet(this, .26);
        _activeRect = new ControlSpringSet(this, .26);
        _autoScroll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(40) };
        _autoScroll.Tick += (_, _) => AutoScrollTick();
        System.Windows.Automation.AutomationProperties.SetName(this, grid.Label);
        Unloaded += (_, _) => StopLoops();
    }

    internal void StopLoops()
    {
        if (_loop) { CompositionTarget.Rendering -= Frame; _loop = false; }
        _autoScroll.Stop();
        _selection.Finish();
        _activeRect.Finish();
    }

    protected override Size MeasureOverride(Size available)
    {
        double body = _grid.IsLoading ? 6 * _grid.RowHeight : _grid.View.Count > 0 ? _grid.View.Count * _grid.RowHeight : 176;
        double wanted = HeadHeight + body + FootHeight;
        double limit = Math.Min(_grid.MaxGridHeight, double.IsInfinity(available.Height) ? double.MaxValue : available.Height);
        return new Size(double.IsInfinity(available.Width) ? 520 : available.Width, Math.Min(wanted, limit));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _grid.LayoutColumns();
        ClampScroll();
        SnapSelection();
        _grid.AfterScroll();
    }

    // ---- Scrolling ----
    internal void ScrollTo(double x, double y)
    {
        double maxX = Math.Max(0, _grid.ContentWidth - ActualWidth), maxY = Math.Max(0, _grid.View.Count * _grid.RowHeight - BodyHeight);
        x = Math.Round(Math.Clamp(x, 0, maxX));
        y = Math.Round(Math.Clamp(y, 0, maxY));
        if (x == ScrollX && y == ScrollY) { return; }
        ScrollX = x;
        ScrollY = y;
        _hover = _hover with { Zone = Zone.None };
        WakeLoop();
        InvalidateVisual();
        _grid.AfterScroll();
    }

    internal void ClampScroll() => ScrollTo(ScrollX, ScrollY);

    // The header menu button takes focus, which brings a part-hidden column fully into view first.
    private void RevealColumn(int column)
    {
        if (column < _grid.PinnedCount) { return; }
        double overflow = _grid.ColX[column] + _grid.ColW[column] - ScrollX - ActualWidth;
        if (overflow > 0) { ScrollTo(ScrollX + overflow, ScrollY); }
    }

    internal void Reveal(int column, int row)
    {
        double x = ScrollX, y = ScrollY;
        double top = row * _grid.RowHeight;
        if (top < y) { y = top; } else if (top + _grid.RowHeight > y + BodyHeight) { y = top + _grid.RowHeight - BodyHeight; }
        if (column >= _grid.PinnedCount && column < _grid.ColX.Length)
        {
            double left = _grid.ColX[column], right = left + _grid.ColW[column];
            if (left - x < _grid.PinEdge) { x = left - _grid.PinEdge; } else if (right - x > ActualWidth) { x = right - ActualWidth; }
        }
        ScrollTo(x, y);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        bool horizontal = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (horizontal) { ScrollTo(ScrollX - e.Delta / 120.0 * 80, ScrollY); }
        else { ScrollTo(ScrollX, ScrollY - e.Delta / 120.0 * _grid.RowHeight * 3); }
        e.Handled = true;
    }

    // ---- Motion ----
    private sealed class Fade(double value) { public double Value = value; public double Target = value; }
    private sealed class Roll { public string From = string.Empty, To = string.Empty; public double Progress = 1; public int Direction = 1; }

    private double FadeTo(string key, double target, double initial = 0)
    {
        if (!_fades.TryGetValue(key, out var fade)) { _fades[key] = fade = new Fade(initial); }
        if (fade.Target != target) { fade.Target = target; WakeLoop(); }
        if (ArcMotion.GetLevel(_grid) == ArcMotionLevel.Off) { fade.Value = target; }
        return fade.Value;
    }

    internal void WakeLoop()
    {
        if (_loop || !IsLoaded) { return; }
        _loop = true;
        _lastFrame = Environment.TickCount64;
        CompositionTarget.Rendering += Frame;
    }

    private void Frame(object? sender, EventArgs e)
    {
        long now = Environment.TickCount64;
        double dt = Math.Min((now - _lastFrame) / 1000.0, .05);
        _lastFrame = now;
        bool busy = StepAnimations(dt, now);
        InvalidateVisual();
        if (!busy) { CompositionTarget.Rendering -= Frame; _loop = false; }
    }

    /// <summary>Advances every drawn animation. Returns true while any still runs.</summary>
    internal bool StepAnimations(double dt, long now)
    {
        bool busy = false;
        foreach (string key in _fades.Keys.ToArray())
        {
            var fade = _fades[key];
            if (fade.Value == fade.Target) { continue; }
            double step = dt / .16;
            fade.Value = fade.Value < fade.Target ? Math.Min(fade.Target, fade.Value + step) : Math.Max(fade.Target, fade.Value - step);
            if (fade.Value != fade.Target) { busy = true; }
        }
        foreach (var roll in _rolls.Values)
        {
            if (roll.Progress >= 1) { continue; }
            roll.Progress = Math.Min(1, roll.Progress + dt / .4);
            busy |= roll.Progress < 1;
        }
        if (_slide is not null)
        {
            if (now - _slideStart >= 700) { _slide = null; _slideExtra.Clear(); }
            else { busy = true; }
        }
        double pinTarget = ScrollX > 0 ? 1 : 0;
        if (_pinShadow != pinTarget)
        {
            _pinShadow = _pinShadow < pinTarget ? Math.Min(1, _pinShadow + dt / .16) : Math.Max(0, _pinShadow - dt / .16);
            busy |= _pinShadow != pinTarget;
        }
        if (_grid.Flashes.Count > 0)
        {
            foreach (var key in _grid.Flashes.Where(pair => now - pair.Value >= 900).Select(pair => pair.Key).ToArray()) { _grid.Flashes.Remove(key); }
            busy |= _grid.Flashes.Count > 0;
        }
        return busy;
    }

    /// <summary>After a sort, rows slide to their new place, and rows arriving from off screen fade in.</summary>
    internal void BeginRowSlide(Dictionary<string, int> oldIndex)
    {
        var slide = new Dictionary<string, (double, bool)>();
        _slideExtra.Clear();
        double rowHeight = _grid.RowHeight, top = ScrollY, bottom = ScrollY + BodyHeight;
        for (int i = 0; i < _grid.View.Count; i++)
        {
            if (_grid.View[i].Row is not { } row) { continue; }
            double newY = i * rowHeight;
            bool visibleNow = newY + rowHeight > top && newY < bottom;
            bool had = oldIndex.TryGetValue(row.Id, out int old);
            double oldY = had ? old * rowHeight : double.NaN;
            bool wasVisible = had && oldY + rowHeight > top && oldY < bottom;
            if (!visibleNow && !wasVisible) { continue; }
            slide[row.Id] = wasVisible ? (oldY - newY, false) : (0, true);
            if (!visibleNow) { _slideExtra.Add(i); }
        }
        _slide = slide;
        _slideStart = Environment.TickCount64;
        WakeLoop();
    }

    private double SlideProgress()
    {
        double t = Math.Clamp((Environment.TickCount64 - _slideStart) / 700.0, 0, 1);
        // The critically damped curve Arc's spring settles on, reaching rest at the end of the 700 ms window.
        double omega = 3.89 / .35;
        double p = 1 - (1 + omega * t * .7) * Math.Exp(-omega * t * .7);
        return t >= 1 ? 1 : p;
    }

    // ---- Selection springs ----
    internal void AnimateSelection()
    {
        SetSelectionTargets(ArcMotion.GetLevel(_grid) == ArcMotionLevel.Full && IsLoaded);
        InvalidateVisual();
    }

    internal void SnapSelection() => SetSelectionTargets(false);

    private void SetSelectionTargets(bool animate)
    {
        if (_grid.View.Count == 0 || _grid.Visible.Count == 0 || _grid.ColX.Length != _grid.Visible.Count) { return; }
        var (l, t, r, b) = _grid.Range;
        r = Math.Min(r, _grid.Visible.Count - 1); l = Math.Min(l, r);
        var range = new Rect(_grid.ColX[l], t * _grid.RowHeight, _grid.ColX[r] + _grid.ColW[r] - _grid.ColX[l], (b - t + 1) * _grid.RowHeight);
        int ac = Math.Min(_grid.Active.Column, _grid.Visible.Count - 1);
        var active = new Rect(_grid.ColX[ac], _grid.Active.Row * _grid.RowHeight, _grid.ColW[ac], _grid.RowHeight);
        _selection.Set(range, animate);
        _activeRect.Set(active, animate);
    }

    // ---- Rendering ----
    private Brush B(string token) => TryFindResource("ArcWpf.Brush." + token) as Brush ?? Brushes.Magenta;

    private FormattedText Text(string text, double size, bool medium, Brush brush, double maxWidth, TextAlignment alignment = TextAlignment.Left)
    {
        var key = (text, size, medium, brush, maxWidth, alignment);
        if (_texts.TryGetValue(key, out var cached)) { return cached; }
        if (_texts.Count > 4000) { _texts.Clear(); }
        var typeface = new Typeface(_grid.FontFamily, FontStyles.Normal, medium ? FontWeights.Medium : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("en-US"), FlowDirection.LeftToRight, typeface, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = alignment, MaxTextWidth = Math.Max(1, maxWidth)
        };
        return _texts[key] = formatted;
    }

    private void DrawCentered(DrawingContext dc, FormattedText text, double x, double centerY)
        => dc.DrawText(text, new Point(x, centerY - text.Height / 2));

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        dc.DrawRectangle(B("Surface"), null, new Rect(size));
        if (_grid.Visible.Count == 0 || _grid.ColX.Length != _grid.Visible.Count) { return; }
        double head = HeadHeight, foot = FootHeight, bodyBottom = size.Height - foot, pin = _grid.PinEdge;
        var bodyClip = new Rect(0, head, size.Width, Math.Max(0, bodyBottom - head));

        if (_grid.IsLoading) { DrawSkeleton(dc, head); }
        else if (_grid.View.Count == 0) { DrawEmpty(dc, head); }
        else
        {
            dc.PushClip(new RectangleGeometry(new Rect(pin, head, Math.Max(0, size.Width - pin), bodyClip.Height)));
            DrawRows(dc, head, false);
            DrawSelection(dc, head, false);
            dc.Pop();
            dc.PushClip(new RectangleGeometry(new Rect(0, head, pin, bodyClip.Height)));
            DrawRows(dc, head, true);
            DrawSelection(dc, head, true);
            dc.Pop();
            dc.PushClip(new RectangleGeometry(bodyClip));
            DrawHandle(dc, head);
            dc.Pop();
        }
        DrawHead(dc);
        if (_grid.FilterRowOpen) { DrawFilterRow(dc); }
        if (_grid.Totals) { DrawTotals(dc, bodyBottom); }
        if (_pinShadow > 0 && _grid.PinnedCount > 0)
        {
            var shadow = new LinearGradientBrush(Color.FromArgb((byte)(255 * .06 * _pinShadow), 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0);
            dc.DrawRectangle(shadow, null, new Rect(pin, 0, 10, size.Height));
        }
    }

    private void VisibleRows(double bodyHeight, out int first, out int last)
    {
        first = Math.Max(0, (int)(ScrollY / _grid.RowHeight));
        last = Math.Min(_grid.View.Count - 1, (int)((ScrollY + bodyHeight) / _grid.RowHeight));
    }

    private void DrawRows(DrawingContext dc, double head, bool pinned)
    {
        VisibleRows(BodyHeight, out int first, out int last);
        double progress = _slide is null ? 1 : SlideProgress();
        for (int y = first; y <= last; y++) { DrawRow(dc, y, head, pinned, progress); }
        foreach (int y in _slideExtra) { if (y < first || y > last) { DrawRow(dc, y, head, pinned, progress); } }
    }

    private void DrawRow(DrawingContext dc, int y, double head, bool pinned, double progress)
    {
        if (y >= _grid.View.Count) { return; }
        var item = _grid.View[y];
        double rowHeight = _grid.RowHeight, top = head + y * rowHeight - ScrollY, alpha = 1;
        if (_slide is not null && item.Row is { } sliding && _slide.TryGetValue(sliding.Id, out var motion))
        {
            top += motion.From * (1 - progress);
            if (motion.FadeIn) { alpha = progress; }
        }
        if (alpha < 1) { dc.PushOpacity(alpha); }
        int from = pinned ? 0 : _grid.PinnedCount, to = pinned ? _grid.PinnedCount : _grid.Visible.Count;
        double shift = pinned ? 0 : ScrollX;
        bool isChecked = item.Row is { } row && _grid.Checked.Contains(row.Id);
        Brush line = B("DataGridLine"), rowFill = isChecked ? B("DataGridChecked") : B("Surface"), groupFill = B("DataGridHeadActive");
        for (int c = from; c < to; c++)
        {
            double x = _grid.ColX[c] - shift, width = _grid.ColW[c];
            if (x + width < 0 || x > ActualWidth) { continue; }
            var column = _grid.Visible[c];
            var cell = new Rect(x, top, width, rowHeight);
            dc.DrawRectangle(item.IsGroup ? groupFill : rowFill, null, cell);
            dc.DrawRectangle(line, null, new Rect(x, top + rowHeight - 1, width, 1));
            dc.DrawRectangle(c == _grid.PinnedCount - 1 ? B("Border") : line, null, new Rect(x + width - 1, top, 1, rowHeight - 1));
            if (item.IsGroup) { DrawGroupCell(dc, item, c, x, top, width); }
            else if (_grid.EditingCell is not { } editing || editing.Column != c || editing.RowId != item.Row!.Id) { DrawValue(dc, item.Row!, column, c, x, top, width); }
            if (item.Row is { } flashed && _grid.Flashes.TryGetValue((flashed.Id, column.Key), out long start))
            {
                double remaining = 1 - Math.Clamp((Environment.TickCount64 - start) / 900.0, 0, 1);
                var wash = ((SolidColorBrush)B("DataGridAccent")).Color;
                wash.A = (byte)(255 * .18 * remaining);
                dc.DrawRectangle(new SolidColorBrush(wash), null, cell);
            }
        }
        if (pinned && _grid.GutterWidth > 0) { DrawGutter(dc, item, y, top, isChecked); }
        if (alpha < 1) { dc.Pop(); }
    }

    private void DrawValue(DrawingContext dc, ArcDataGridRow row, ArcDataGridColumn column, int c, double x, double top, double width)
    {
        string text = ArcDataGridModel.Format(row[column.Key], column);
        if (text.Length == 0) { return; }
        bool medium = c < _grid.PinnedCount;
        var formatted = Text(text, 14, medium, B("Foreground"), Math.Max(1, width - 24), column.IsNumeric ? TextAlignment.Right : TextAlignment.Left);
        DrawCentered(dc, formatted, x + 12, top + (_grid.RowHeight - 1) / 2);
    }

    private void DrawGroupCell(DrawingContext dc, ArcDataGrid.ViewItem item, int c, double x, double top, double width)
    {
        double center = top + (_grid.RowHeight - 1) / 2;
        var column = _grid.Visible[c];
        if (c == 0)
        {
            double indent = x + 4 + item.Level * 16;
            string key = "chev:" + item.GroupKey;
            double turn = FadeTo(key, item.Expanded ? 1 : 0, item.Expanded ? 1 : 0);
            ArcDataGridIcon.Draw(dc, "chevron-right", new Rect(indent, center - 7, 14, 14), B("TextSecondary"), 1.75, 90 * turn);
            var label = Text(item.GroupValue, 14, true, B("Foreground"), Math.Max(1, x + width - indent - 20 - 60));
            DrawCentered(dc, label, indent + 20, center);
            var count = Text(item.GroupRows!.Count.ToString("N0", CultureInfo.InvariantCulture), 12, false, B("TextMuted"), 80);
            DrawCentered(dc, count, indent + 20 + label.WidthIncludingTrailingWhitespace + 6, center);
            return;
        }
        var kind = ArcDataGridModel.DefaultAggregate(column);
        if (kind == ArcDataGridAggregate.None || ArcDataGridModel.Aggregate(item.GroupRows!, column, kind) is not { } total) { return; }
        var formatted = Text(ArcDataGridModel.FormatSubtotal(total, column, kind), 14, true, B("Foreground"), Math.Max(1, width - 24), TextAlignment.Right);
        DrawCentered(dc, formatted, x + 12, center);
    }

    private void DrawGutter(DrawingContext dc, ArcDataGrid.ViewItem item, int y, double top, bool isChecked)
    {
        double width = _grid.GutterWidth, rowHeight = _grid.RowHeight;
        var (_, t, _, b) = _grid.Range;
        bool inRange = !item.IsGroup && y >= t && y <= b;
        dc.DrawRectangle(isChecked ? B("DataGridChecked") : inRange ? B("DataGridHeadActive") : B("Surface"), null, new Rect(0, top, width, rowHeight));
        dc.DrawRectangle(B("DataGridLine"), null, new Rect(0, top + rowHeight - 1, width, 1));
        dc.DrawRectangle(B("Border"), null, new Rect(width - 1, top, 1, rowHeight - 1));
        if (item.IsGroup) { return; }
        string id = item.Row!.Id;
        bool gutterHot = _hover.Zone == Zone.GutterCheck && _hover.Row == y;
        bool rowHot = _hover.Zone != Zone.None && _hover.Row == y;
        bool always = !_grid.RowNumbers || _grid.Checked.Count > 0;
        double mark = FadeTo("box:" + id, always || rowHot || gutterHot ? 1 : 0, always ? 1 : 0);
        double center = top + (rowHeight - 1) / 2;
        if (_grid.RowNumbers && mark < 1)
        {
            if (mark > 0) { dc.PushOpacity(1 - mark); }
            var number = Text(item.Number.ToString("N0", CultureInfo.InvariantCulture), 12, false, inRange ? B("Foreground") : B("TextMuted"), width, TextAlignment.Center);
            DrawCentered(dc, number, 0, center);
            if (mark > 0) { dc.Pop(); }
        }
        if (mark > 0)
        {
            if (mark < 1) { dc.PushOpacity(mark); }
            DrawCheckBox(dc, width / 2, center, isChecked ? true : false, gutterHot);
            if (mark < 1) { dc.Pop(); }
        }
    }

    private void DrawCheckBox(DrawingContext dc, double cx, double cy, bool? state, bool hot)
    {
        bool on = state != false;
        var box = new Rect(cx - 8, cy - 8, 16, 16);
        Brush edge = on ? B("ControlOn") : hot ? B("TextMuted") : B("BorderStrong");
        dc.DrawRoundedRectangle(on ? B("ControlOn") : B("Surface"), null, box, 5, 5);
        var pen = new Pen(edge, 1);
        dc.DrawRoundedRectangle(null, pen, new Rect(box.X + .5, box.Y + .5, 15, 15), 4.5, 4.5);
        if (on) { ArcDataGridIcon.Draw(dc, state == true ? "check" : "minus", new Rect(cx - 6, cy - 6, 12, 12), B("ControlGlyph"), 2.25); }
    }

    private void DrawHead(DrawingContext dc)
    {
        double width = ActualWidth, pin = _grid.PinEdge;
        const double H = 39;
        dc.PushClip(new RectangleGeometry(new Rect(pin, 0, Math.Max(0, width - pin), 40)));
        dc.DrawRectangle(B("Surface"), null, new Rect(pin, 0, Math.Max(0, width - pin), H));
        for (int c = _grid.PinnedCount; c < _grid.Visible.Count; c++) { DrawHeadCell(dc, c, _grid.ColX[c] - ScrollX, H); }
        dc.Pop();
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, pin, 40)));
        dc.DrawRectangle(B("Surface"), null, new Rect(0, 0, pin, H));
        for (int c = 0; c < _grid.PinnedCount; c++) { DrawHeadCell(dc, c, _grid.ColX[c], H); }
        dc.Pop();
        if (_grid.GutterWidth > 0)
        {
            double gutter = _grid.GutterWidth;
            dc.DrawRectangle(B("Surface"), null, new Rect(0, 0, gutter, H));
            dc.DrawRectangle(B("Border"), null, new Rect(gutter - 1, 0, 1, H));
            bool all = _grid.Filtered.Count > 0 && _grid.Filtered.All(row => _grid.Checked.Contains(row.Id));
            bool any = _grid.Checked.Count > 0;
            DrawCheckBox(dc, gutter / 2, H / 2, all ? true : any ? null : false, _hover.Zone == Zone.HeadCheck);
        }
        dc.DrawRectangle(B("Border"), null, new Rect(0, H, width, 1));
    }

    private void DrawHeadCell(DrawingContext dc, int c, double x, double height)
    {
        var column = _grid.Visible[c];
        double width = _grid.ColW[c];
        if (x + width < 0 || x > ActualWidth) { return; }
        var (l, _, r, _) = _grid.Range;
        bool selected = _grid.View.Count > 0 && c >= l && c <= r;
        bool cellHot = _hover.Column == c && _hover.Zone is Zone.HeadSort or Zone.HeadMenu or Zone.HeadResize;
        bool menuOpen = MenuColumn == c;
        int sortIndex = -1;
        bool descending = false;
        for (int i = 0; i < _grid.SortKeys.Count; i++) { if (_grid.SortKeys[i].Key == column.Key) { sortIndex = i; descending = _grid.SortKeys[i].Descending; } }
        bool sorted = sortIndex >= 0;
        dc.DrawRectangle(selected ? B("DataGridHeadActive") : B("Surface"), null, new Rect(x, 0, width, height));
        dc.DrawRectangle(c == _grid.PinnedCount - 1 ? B("Border") : B("DataGridLine"), null, new Rect(x + width - 1, 0, 1, height));
        double center = height / 2;
        bool strong = selected || sorted || cellHot || menuOpen;
        var color = strong ? B("Foreground") : B("TextSecondary");
        bool hasOrder = sorted && _grid.SortKeys.Count > 1;
        double iconBox = hasOrder ? 23 : 14;
        double labelRoom = Math.Max(1, width - 30 - 12 - 4 - iconBox - 4);
        var label = Text(column.Header, 14, true, color, labelRoom, column.IsNumeric ? TextAlignment.Right : TextAlignment.Left);
        double textWidth = Math.Min(label.WidthIncludingTrailingWhitespace, labelRoom);
        double labelX, iconX;
        if (column.IsNumeric)
        {
            double end = x + width - 30 - 4;
            labelX = end - labelRoom;
            iconX = end - textWidth - 4 - iconBox;
        }
        else
        {
            labelX = x + 12;
            iconX = labelX + textWidth + 4;
        }
        DrawCentered(dc, label, labelX, center);
        // The sort arrow: a faint hint on hover, solid once sorted, flipped for descending.
        double arrow = FadeTo("arrow:" + column.Key, sorted ? 1 : cellHot && column.Sortable ? .35 : 0);
        if (arrow > 0)
        {
            double flip = FadeTo("flip:" + column.Key, descending ? 1 : 0);
            double scale = sorted ? 1 : .8 + .2 * Math.Min(1, arrow / .35);
            dc.PushOpacity(arrow);
            double size = 14 * scale;
            ArcDataGridIcon.Draw(dc, "arrow-up", new Rect(iconX + (14 - size) / 2, center - size / 2, size, size), B("Foreground"), 1.75, 180 * flip);
            dc.Pop();
            if (hasOrder)
            {
                var order = Text((sortIndex + 1).ToString(CultureInfo.InvariantCulture), 12, true, B("TextSecondary"), 12);
                DrawCentered(dc, order, iconX + 15, center);
            }
        }
        double menu = FadeTo("menu:" + column.Key, cellHot || menuOpen ? 1 : 0);
        if (menu > 0)
        {
            var box = MenuButton(x, width);
            bool hot = _hover.Zone == Zone.HeadMenu && _hover.Column == c || menuOpen;
            dc.PushOpacity(menu);
            if (hot) { dc.DrawRoundedRectangle(B("SurfaceMuted"), null, box, 8, 8); }
            ArcDataGridIcon.Draw(dc, "chevron-down", new Rect(box.X + 5, box.Y + 5, 14, 14), hot ? B("Foreground") : B("TextMuted"));
            dc.Pop();
        }
        if (_hover.Zone == Zone.HeadResize && _hover.Column == c || _resizing && _resizeColumn == c)
        {
            dc.PushOpacity(_resizing ? 1 : .45);
            dc.DrawRoundedRectangle(B("Foreground"), null, new Rect(x + width - 1, 9, 2, 21), 1, 1);
            dc.Pop();
        }
    }

    private static Rect MenuButton(double x, double width) => new(x + width - 30, 7.5, 24, 24);

    private void DrawFilterRow(DrawingContext dc)
    {
        double width = ActualWidth, pin = _grid.PinEdge;
        const double Top = 40, H = 39;
        dc.DrawRectangle(B("Surface"), null, new Rect(0, Top, width, H));
        dc.PushClip(new RectangleGeometry(new Rect(pin, Top, Math.Max(0, width - pin), H)));
        for (int c = _grid.PinnedCount; c < _grid.Visible.Count; c++)
        {
            double x = _grid.ColX[c] - ScrollX;
            dc.DrawRectangle(B("DataGridLine"), null, new Rect(x + _grid.ColW[c] - 1, Top, 1, H));
        }
        dc.Pop();
        for (int c = 0; c < _grid.PinnedCount; c++)
        {
            dc.DrawRectangle(c == _grid.PinnedCount - 1 ? B("Border") : B("DataGridLine"), null, new Rect(_grid.ColX[c] + _grid.ColW[c] - 1, Top, 1, H));
        }
        if (_grid.GutterWidth > 0) { dc.DrawRectangle(B("Border"), null, new Rect(_grid.GutterWidth - 1, Top, 1, H)); }
        dc.DrawRectangle(B("Border"), null, new Rect(0, Top + H, width, 1));
    }

    private void DrawTotals(DrawingContext dc, double top)
    {
        double width = ActualWidth, pin = _grid.PinEdge;
        const double H = 39;
        dc.DrawRectangle(B("Surface"), null, new Rect(0, top, width, 40));
        dc.DrawRectangle(B("Border"), null, new Rect(0, top, width, 1));
        double y = top + 1;
        dc.PushClip(new RectangleGeometry(new Rect(pin, y, Math.Max(0, width - pin), H)));
        for (int c = _grid.PinnedCount; c < _grid.Visible.Count; c++) { DrawTotalCell(dc, c, _grid.ColX[c] - ScrollX, y, H); }
        dc.Pop();
        dc.PushClip(new RectangleGeometry(new Rect(0, y, pin, H)));
        for (int c = 0; c < _grid.PinnedCount; c++) { DrawTotalCell(dc, c, _grid.ColX[c], y, H); }
        dc.Pop();
        if (_grid.GutterWidth > 0) { dc.DrawRectangle(B("Border"), null, new Rect(_grid.GutterWidth - 1, y, 1, H)); }
    }

    private void DrawTotalCell(DrawingContext dc, int c, double x, double y, double height)
    {
        double width = _grid.ColW[c];
        if (x + width < 0 || x > ActualWidth) { return; }
        var column = _grid.Visible[c];
        dc.DrawRectangle(c == _grid.PinnedCount - 1 ? B("Border") : B("DataGridLine"), null, new Rect(x + width - 1, y, 1, height));
        double center = y + height / 2;
        if (c == 0)
        {
            int shown = _grid.Filtered.Count, all = _grid.Rows?.Count ?? 0;
            string text = shown == all ? $"{all:N0} {(all == 1 ? "row" : "rows")}" : $"{shown:N0} of {all:N0} rows";
            DrawCentered(dc, Text(text, 12, false, B("TextMuted"), Math.Max(1, width - 24)), x + 12, center);
            return;
        }
        if (!_grid.TotalsCache.TryGetValue(column.Key, out double total)) { return; }
        var kind = ArcDataGridModel.DefaultAggregate(column);
        DrawCentered(dc, Text(ArcDataGridModel.TotalLabel(kind), 12, false, B("TextMuted"), Math.Max(1, width - 24)), x + 12, center);
        DrawRolling(dc, column.Key, ArcDataGridModel.FormatTotal(total, column, kind), x + width - 12, center, y, height);
    }

    // Totals roll digit by digit as they change, like the animated counter they are based on.
    private void DrawRolling(DrawingContext dc, string key, string text, double right, double center, double top, double height)
    {
        if (!_rolls.TryGetValue(key, out var roll)) { _rolls[key] = roll = new Roll { From = text, To = text }; }
        if (roll.To != text)
        {
            roll.From = roll.To;
            roll.To = text;
            roll.Progress = ArcMotion.GetLevel(_grid) == ArcMotionLevel.Full ? 0 : 1;
            WakeLoop();
        }
        var foreground = B("Foreground");
        if (roll.Progress >= 1)
        {
            var whole = Text(text, 14, true, foreground, 200, TextAlignment.Right);
            dc.DrawText(whole, new Point(right - 200, center - whole.Height / 2));
            return;
        }
        double p = 1 - Math.Pow(1 - roll.Progress, 3);
        dc.PushClip(new RectangleGeometry(new Rect(right - 200, top, 200, height)));
        double x = right;
        for (int i = 1; i <= Math.Max(roll.To.Length, roll.From.Length); i++)
        {
            char now = i <= roll.To.Length ? roll.To[^i] : '\0', old = i <= roll.From.Length ? roll.From[^i] : '\0';
            double advance = now != '\0' ? Text(now.ToString(), 14, true, foreground, 40).WidthIncludingTrailingWhitespace : 0;
            if (now == old) { Draw(now, 0, 1); }
            else
            {
                if (old != '\0') { Draw(old, -14 * p, 1 - p); }
                if (now != '\0') { Draw(now, 14 * (1 - p), p); }
            }
            x -= advance;
            void Draw(char glyph, double dy, double opacity)
            {
                var formatted = Text(glyph.ToString(), 14, true, foreground, 40);
                dc.PushOpacity(opacity);
                dc.DrawText(formatted, new Point(x - formatted.WidthIncludingTrailingWhitespace, center - formatted.Height / 2 + dy));
                dc.Pop();
            }
        }
        dc.Pop();
    }

    private void DrawEmpty(DrawingContext dc, double head)
    {
        bool filtered = (_grid.Rows?.Count ?? 0) > 0;
        var title = Text(filtered ? "No rows match" : _grid.EmptyMessage, 14, false, B("TextSecondary"), ActualWidth, TextAlignment.Center);
        double y = (filtered ? head + 66 : head + 88) - title.Height / 2;
        dc.DrawText(title, new Point(0, y));
    }

    private void DrawSkeleton(DrawingContext dc, double head)
    {
        double pulse = ArcMotion.GetLevel(_grid) == ArcMotionLevel.Full ? .65 + .35 * Math.Cos(Environment.TickCount64 / 1000.0 * Math.PI) : 1;
        dc.PushOpacity(pulse);
        for (int y = 0; y < 6; y++)
        {
            double top = head + y * _grid.RowHeight;
            dc.DrawRectangle(B("DataGridLine"), null, new Rect(0, top + _grid.RowHeight - 1, ActualWidth, 1));
            for (int c = 0; c < _grid.Visible.Count; c++)
            {
                double x = _grid.ColX[c] - (c < _grid.PinnedCount ? 0 : ScrollX), width = _grid.ColW[c];
                dc.DrawRoundedRectangle(B("SkeletonFill"), null, new Rect(x + 12, top + (_grid.RowHeight - 1) / 2 - 6, Math.Max(0, (width - 24) * (.55 + .1 * ((y + c) % 3))), 12), 6, 6);
            }
        }
        dc.Pop();
        if (pulse != 1) { Dispatcher.BeginInvoke(DispatcherPriority.Background, InvalidateVisual); }
    }

    private (Rect Range, Rect Active) SelectionScreen(double head)
    {
        var (l, _, r, _) = _grid.Range;
        r = Math.Min(r, _grid.Visible.Count - 1);
        l = Math.Min(l, r);
        double Sx(int column) => column < _grid.PinnedCount ? 0 : ScrollX;
        var range = _selection.Value;
        var active = _activeRect.Value;
        int ac = Math.Min(_grid.Active.Column, _grid.Visible.Count - 1);
        double left = range.X - Sx(l), right = range.Right - Sx(r);
        return (new Rect(left, range.Y + head - ScrollY, Math.Max(0, right - left), range.Height), new Rect(active.X - Sx(ac), active.Y + head - ScrollY, active.Width, active.Height));
    }

    private void DrawSelection(DrawingContext dc, double head, bool pinnedLayer)
    {
        if (_grid.View.Count == 0) { return; }
        var (range, active) = SelectionScreen(head);
        var accent = B("DataGridAccent");
        bool editing = _grid.IsEditing;
        if (!editing)
        {
            dc.DrawRoundedRectangle(B("DataGridRange"), new Pen(accent, 1), new Rect(range.X + .5, range.Y + .5, Math.Max(0, range.Width - 1), Math.Max(0, range.Height - 1)), 1.5, 1.5);
        }
        dc.DrawRoundedRectangle(null, new Pen(accent, 2), new Rect(active.X + 1, active.Y + 1, Math.Max(0, active.Width - 2), Math.Max(0, active.Height - 2)), 1, 1);
        if (_filling)
        {
            var (l, t, r, b) = _grid.Range;
            int tc = Math.Clamp(_fillTarget.Column, 0, _grid.Visible.Count - 1), tr = Math.Clamp(_fillTarget.Row, 0, _grid.View.Count - 1);
            int nl = Math.Min(l, tc), nr = Math.Max(r, tc), nt = Math.Min(t, tr), nb = Math.Max(b, tr);
            if (Math.Max(tr - b, t - tr) >= Math.Max(tc - r, l - tc)) { nl = l; nr = r; } else { nt = t; nb = b; }
            double Sx(int column) => column < _grid.PinnedCount ? 0 : ScrollX;
            double px = _grid.ColX[nl] - Sx(nl), pr = _grid.ColX[nr] + _grid.ColW[nr] - Sx(nr), py = head + nt * _grid.RowHeight - ScrollY, pb = head + (nb + 1) * _grid.RowHeight - ScrollY;
            var dash = new Pen(accent, 1) { DashStyle = new DashStyle([3, 3], 0) };
            dc.DrawRectangle(null, dash, new Rect(px + .5, py + .5, Math.Max(0, pr - px - 1), Math.Max(0, pb - py - 1)));
        }
    }

    private void DrawHandle(DrawingContext dc, double head)
    {
        if (_grid.View.Count == 0 || _grid.IsEditing) { return; }
        var (range, _) = SelectionScreen(head);
        var (_, _, r, _) = _grid.Range;
        // The handle belongs to its range's layer: it slides under the pinned columns with the cells it marks.
        if (Math.Min(r, _grid.Visible.Count - 1) >= _grid.PinnedCount && range.Right < _grid.PinEdge) { return; }
        var center = new Point(range.Right, range.Bottom);
        dc.DrawRoundedRectangle(B("Surface"), null, new Rect(center.X - 6, center.Y - 6, 12, 12), 4, 4);
        dc.DrawRoundedRectangle(B("DataGridAccent"), null, new Rect(center.X - 4, center.Y - 4, 8, 8), 2, 2);
    }

    // ---- Hit testing ----
    private Hit HitTest(Point p)
    {
        double head = HeadHeight, footTop = ActualHeight - FootHeight;
        if (p.Y < 40)
        {
            if (p.X < _grid.GutterWidth) { return new Hit(_grid.RowSelection ? Zone.HeadCheck : Zone.None, -1, -1); }
            for (int c = _grid.Visible.Count - 1; c >= 0; c--)
            {
                bool pinned = c < _grid.PinnedCount;
                double x = _grid.ColX[c] - (pinned ? 0 : ScrollX), width = _grid.ColW[c];
                if (!pinned && x + width < _grid.PinEdge) { continue; }
                if (p.X >= x + width - 6 && p.X <= x + width + 6 && (pinned || p.X > _grid.PinEdge) && !(c == _grid.Visible.Count - 1 && p.X > x + width)) { return new Hit(Zone.HeadResize, c, -1); }
            }
            int column = ColumnAt(p.X);
            if (column < 0) { return default; }
            double cellX = _grid.ColX[column] - (column < _grid.PinnedCount ? 0 : ScrollX);
            return MenuButton(cellX, _grid.ColW[column]).Contains(p) ? new Hit(Zone.HeadMenu, column, -1) : new Hit(Zone.HeadSort, column, -1);
        }
        if (p.Y < head) { return new Hit(Zone.Filter, ColumnAt(p.X), -1); }
        if (p.Y >= footTop) { return new Hit(Zone.Totals, ColumnAt(p.X), -1); }
        if (_grid.View.Count == 0) { return default; }
        // The fill handle overhangs its corner, so it wins over the cell beneath.
        if (!_grid.IsEditing)
        {
            var (range, _) = SelectionScreen(head);
            if (Math.Abs(p.X - range.Right) <= 6 && Math.Abs(p.Y - range.Bottom) <= 6) { return new Hit(Zone.FillHandle, -1, -1); }
        }
        int row = (int)((p.Y - head + ScrollY) / _grid.RowHeight);
        if (row < 0 || row >= _grid.View.Count) { return default; }
        var item = _grid.View[row];
        if (p.X < _grid.GutterWidth) { return new Hit(item.IsGroup || !_grid.RowSelection ? Zone.None : Zone.GutterCheck, -1, row); }
        int col = ColumnAt(p.X);
        if (item.IsGroup) { return new Hit(col == 0 ? Zone.GroupToggle : Zone.None, col, row); }
        return new Hit(col < 0 ? Zone.None : Zone.Cell, col, row);
    }

    private int ColumnAt(double x)
    {
        if (x < _grid.PinEdge)
        {
            for (int c = 0; c < _grid.PinnedCount; c++) { if (x >= _grid.ColX[c] && x < _grid.ColX[c] + _grid.ColW[c]) { return c; } }
            return -1;
        }
        double content = x + ScrollX;
        for (int c = _grid.PinnedCount; c < _grid.Visible.Count; c++) { if (content >= _grid.ColX[c] && content < _grid.ColX[c] + _grid.ColW[c]) { return c; } }
        return -1;
    }

    private (int Column, int Row) CellNear(Point p)
    {
        double x = Math.Clamp(p.X, _grid.GutterWidth, ActualWidth - 1);
        int column = ColumnAt(x);
        if (column < 0) { column = p.X < _grid.PinEdge ? 0 : _grid.Visible.Count - 1; }
        int row = (int)Math.Floor((Math.Clamp(p.Y, HeadHeight, ActualHeight - FootHeight - 1) - HeadHeight + ScrollY) / _grid.RowHeight);
        return (column, Math.Clamp(row, 0, Math.Max(0, _grid.View.Count - 1)));
    }

    // ---- Mouse ----
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        _lastMouse = p;
        if (_resizing)
        {
            _resizeDrag = p.X - _resizeStart;
            _grid.ResizeColumn(_grid.Visible[_resizeColumn], _resizeWidth + _resizeDrag);
            return;
        }
        if (_selecting)
        {
            var (column, row) = CellNear(p);
            _grid.Select(column, row, true, reveal: false);
            return;
        }
        if (_filling)
        {
            _fillTarget = CellNear(p);
            InvalidateVisual();
            return;
        }
        var hit = HitTest(p);
        if (hit != _hover)
        {
            _hover = hit;
            Cursor = hit.Zone switch
            {
                Zone.HeadResize => Cursors.SizeWE, Zone.FillHandle => Cursors.Cross, Zone.HeadSort or Zone.HeadMenu or Zone.HeadCheck or Zone.GutterCheck or Zone.GroupToggle => Cursors.Hand, _ => null
            };
            WakeLoop();
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = default;
        WakeLoop();
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var p = e.GetPosition(this);
        var hit = HitTest(p);
        _press = hit;
        if (hit.Zone == Zone.Filter || hit.Zone == Zone.Totals) { return; }
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (_grid.IsEditing) { _grid.CommitEdit(0, 0, keepOpenOnError: false); }
        Focus();
        switch (hit.Zone)
        {
            case Zone.HeadResize:
                _resizing = true;
                _resizeColumn = hit.Column;
                _resizeStart = p.X;
                _resizeWidth = _grid.ColW[hit.Column];
                CaptureMouse();
                break;
            case Zone.FillHandle:
                _filling = true;
                var (_, _, right, bottom) = _grid.Range;
                _fillTarget = (right, bottom);
                CaptureMouse();
                _autoScroll.Start();
                break;
            case Zone.Cell:
                _pressWasActive = _grid.Active == new ArcDataGrid.CellRef(hit.Column, hit.Row) && !IsRange();
                if (e.ClickCount == 2) { _grid.Select(hit.Column, hit.Row, false); _grid.BeginEdit(null, false); return; }
                _grid.Select(hit.Column, hit.Row, shift);
                _selecting = true;
                CaptureMouse();
                _autoScroll.Start();
                break;
        }
        e.Handled = true;
    }

    private bool IsRange()
    {
        var (l, t, r, b) = _grid.Range;
        return l != r || t != b;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var p = e.GetPosition(this);
        var hit = HitTest(p);
        bool wasFilling = _filling, wasSelecting = _selecting;
        _autoScroll.Stop();
        if (IsMouseCaptured) { ReleaseMouseCapture(); }
        _selecting = _filling = false;
        if (_resizing) { _resizing = false; _resizeColumn = -1; InvalidateVisual(); return; }
        if (wasFilling) { _grid.FillTo(_fillTarget.Column, _fillTarget.Row); return; }
        if (wasSelecting)
        {
            // A second press on the active cell opens its editor.
            if (_pressWasActive && hit.Zone == Zone.Cell && new ArcDataGrid.CellRef(hit.Column, hit.Row) == _grid.Active && !IsRange()) { _grid.BeginEdit(null, false); }
            return;
        }
        if (hit.Zone != _press.Zone || hit.Column != _press.Column || hit.Row != _press.Row) { return; }
        switch (hit.Zone)
        {
            case Zone.HeadSort: _grid.CycleSort(_grid.Visible[hit.Column], (Keyboard.Modifiers & ModifierKeys.Shift) != 0); break;
            case Zone.HeadMenu:
                RevealColumn(hit.Column);
                double x = _grid.ColX[hit.Column] - (hit.Column < _grid.PinnedCount ? 0 : ScrollX);
                _grid.OpenHeaderMenu(hit.Column, MenuButton(x, _grid.ColW[hit.Column]));
                InvalidateVisual();
                break;
            case Zone.HeadCheck: _grid.ToggleAllChecked(); break;
            case Zone.GutterCheck: if (_grid.View[hit.Row].Row is { } row) { _grid.ToggleChecked(row.Id); } break;
            case Zone.GroupToggle: _grid.ToggleGroup(_grid.View[hit.Row]); break;
        }
    }

    internal void MenuClosed()
    {
        MenuColumn = -1;
        InvalidateVisual();
    }

    private void AutoScrollTick()
    {
        if (!_selecting && !_filling) { _autoScroll.Stop(); return; }
        double dx = 0, dy = 0;
        if (_lastMouse.Y < HeadHeight) { dy = -_grid.RowHeight / 2; }
        else if (_lastMouse.Y > ActualHeight - FootHeight) { dy = _grid.RowHeight / 2; }
        if (_lastMouse.X > ActualWidth) { dx = 24; }
        else if (_lastMouse.X < _grid.PinEdge && ScrollX > 0) { dx = -24; }
        if (dx == 0 && dy == 0) { return; }
        ScrollTo(ScrollX + dx, ScrollY + dy);
        var (column, row) = CellNear(_lastMouse);
        if (_selecting) { _grid.Select(column, row, true, reveal: false); } else { _fillTarget = (column, row); InvalidateVisual(); }
    }

    // ---- Keyboard ----
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled) { _grid.HandleKey(e); }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text) || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0 && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != (ModifierKeys.Control | ModifierKeys.Alt)) { return; }
        if (char.IsControl(e.Text[0]) || e.Text == " " && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) { return; }
        _grid.BeginEdit(e.Text, true);
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    /// <summary>Four springs for one rectangle, in content coordinates, so scrolling moves it at once and only selection changes animate.</summary>
    private sealed class ControlSpringSet
    {
        private readonly SpringDriver _driver;
        private readonly ControlSpring _x, _y, _w, _h;
        private readonly double _duration;
        public ControlSpringSet(UIElement owner, double visualDuration)
        {
            _duration = ControlSpring.Physical(visualDuration);
            _driver = new SpringDriver(owner.InvalidateVisual);
            _x = _driver.Add(0, visualDuration);
            _y = _driver.Add(0, visualDuration);
            _w = _driver.Add(0, visualDuration);
            _h = _driver.Add(0, visualDuration);
        }
        public Rect Value => new(_x.Position, _y.Position, Math.Max(0, _w.Position), Math.Max(0, _h.Position));
        public void Set(Rect target, bool animate)
        {
            if (!animate)
            {
                _driver.Stop();
                _x.Snap(target.X); _y.Snap(target.Y); _w.Snap(target.Width); _h.Snap(target.Height);
                return;
            }
            _driver.Animate(_x, target.X, _duration);
            _driver.Animate(_y, target.Y, _duration);
            _driver.Animate(_w, target.Width, _duration);
            _driver.Animate(_h, target.Height, _duration);
        }
        public void Finish() => _driver.Finish();
    }
}
