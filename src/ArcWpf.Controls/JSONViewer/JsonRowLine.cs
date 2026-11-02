using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>Copy icon button of a JSON row or the path bar: its glyph swaps to a check or an x for 1.4 seconds without changing width.</summary>
public sealed class JsonActionButton : System.Windows.Controls.Button
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Geometry), typeof(JsonActionButton));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(JsonActionButton), new FrameworkPropertyMetadata("value"));
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(string), typeof(JsonActionButton), new FrameworkPropertyMetadata("idle"));
    private DispatcherTimer? _timer;

    static JsonActionButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(JsonActionButton), new FrameworkPropertyMetadata(typeof(JsonActionButton)));

    public Geometry? Icon { get => (Geometry?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string State { get => (string)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    internal void Flash(bool success)
    {
        State = success ? "done" : "error";
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        _timer.Stop();
        _timer.Tick -= Finished;
        _timer.Tick += Finished;
        _timer.Start();
    }

    private void Finished(object? sender, EventArgs e) { _timer!.Stop(); State = "idle"; }
}

/// <summary>The flex line of a JSON row (json-viewer.module.css .line): chevron, key, colon, then a value or a summary, all in one 30 high row.</summary>
public sealed class JsonRowLine : Panel
{
    public static readonly DependencyProperty IsHotProperty = DependencyProperty.Register(nameof(IsHot), typeof(bool), typeof(JsonRowLine), new FrameworkPropertyMetadata(false, (d, _) => ((JsonRowLine)d).PaintChevron()));
    private const double Height30 = 30, Gap = 6, ChevronSize = 14, Indent = 16, LeftBase = 4, RightPadding = 6;
    private FrameworkElement? _chevron;
    private TextBlock? _key, _colon, _value, _preview, _count, _moreLabel, _moreHidden;
    private double _left;
    private JsonRowInfo? _info;
    private readonly OwnedMotion _motion = new();

    public bool IsHot { get => (bool)GetValue(IsHotProperty); set => SetValue(IsHotProperty, value); }

    public JsonRowLine()
    {
        DataContextChanged += (_, _) => Build();
        Loaded += (_, _) => Unfold();
        Unloaded += (_, _) => _motion.Stop();
    }

    private void Build()
    {
        Children.Clear();
        _chevron = null; _key = _colon = _value = _preview = _count = _moreLabel = _moreHidden = null;
        if (DataContext is not TreeRow { Node.Data: JsonRowInfo info } row) { _info = null; return; }
        _info = info;
        _left = LeftBase + (row.Depth - 1) * Indent;
        if (info.IsMore)
        {
            _moreLabel = Body($"Show {info.Page} more", 14, "ArcWpf.Brush.Foreground");
            _moreLabel.FontWeight = FontWeights.Medium;
            _moreHidden = Body($"{info.Hidden} hidden", 12, "ArcWpf.Brush.TextMuted");
            Children.Add(_moreLabel); Children.Add(_moreHidden);
            return;
        }
        bool branch = JSONViewer.IsBranch(info.Type);
        var chevron = new Border { Width = ChevronSize, Height = ChevronSize };
        if (branch)
        {
            var glyph = Lucide.Glyph(ChevronRight, ChevronSize);
            var turn = new RotateTransform(info.Open && !info.Turning ? 90 : info.Open || !info.Turning ? 0 : 90);
            glyph.RenderTransform = turn;
            glyph.RenderTransformOrigin = new Point(.5, .5);
            chevron.Child = glyph;
            if (info.Turning) _motion.Double(turn, RotateTransform.AngleProperty, info.Open ? 90 : 0, Tween.Snappy, true, spring: true);
        }
        _chevron = chevron;
        Children.Add(chevron);
        PaintChevron();
        _key = Mono(info.Name is int ? "ArcWpf.Brush.TextMuted" : "ArcWpf.Brush.Foreground");
        if (info.Name is string name) AddMarked(_key, name, info);
        else _key.Text = info.Name?.ToString() ?? info.RootName;
        Children.Add(_key);
        _colon = Mono("ArcWpf.Brush.TextMuted");
        _colon.Text = info.Name is null && branch ? string.Empty : ":";
        Children.Add(_colon);
        if (branch)
        {
            if (!info.Open)
            {
                _preview = Mono("ArcWpf.Brush.TextMuted");
                _preview.Text = info.Type == JsonType.Array ? "[…]" : Preview(info);
                Children.Add(_preview);
            }
            _count = Body(JSONViewer.CountLabel(info), 12, "ArcWpf.Brush.TextMuted");
            Children.Add(_count);
            return;
        }
        _value = Mono(info.Type switch { JsonType.String => "ArcWpf.Brush.JsonString", JsonType.Number => "ArcWpf.Brush.Accent", JsonType.Boolean => "ArcWpf.Brush.JsonBoolean", _ => "ArcWpf.Brush.TextMuted" });
        string text = JSONViewer.PrimitiveText(info.Value, info.Type).ReplaceLineEndings(" ");
        if (info.Type == JsonType.String) { _value.Inlines.Add("\""); AddMarked(_value, text, info); _value.Inlines.Add("\""); if (text.Length > 40) _value.ToolTip = text; }
        else AddMarked(_value, text, info);
        Children.Add(_value);
    }

    private static string Preview(JsonRowInfo info)
    {
        var keys = ((Dictionary<string, object?>)info.Value!).Keys.Take(3);
        return $"{{ {string.Join(", ", keys)}{(info.Count > 3 ? ", …" : string.Empty)} }}";
    }

    private static TextBlock Mono(string brush)
    {
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        text.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return text;
    }

    private static TextBlock Body(string value, double size, string brush)
    {
        var text = new TextBlock { Text = value, FontSize = size };
        text.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Body");
        text.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return text;
    }

    private static void AddMarked(TextBlock target, string text, JsonRowInfo info)
    {
        if (info.Needle.Length == 0) { target.Inlines.Add(text); return; }
        string lower = text.ToLowerInvariant();
        int from = 0, at = lower.IndexOf(info.Needle, StringComparison.Ordinal);
        while (at >= 0)
        {
            if (at > from) target.Inlines.Add(text[from..at]);
            var mark = new Run(text.Substring(at, info.Needle.Length));
            mark.SetResourceReference(TextElement.BackgroundProperty, info.IsCurrentMatch ? "ArcWpf.Brush.JsonMarkCurrent" : "ArcWpf.Brush.JsonMark");
            target.Inlines.Add(mark);
            from = at + info.Needle.Length;
            at = lower.IndexOf(info.Needle, from, StringComparison.Ordinal);
        }
        if (from < text.Length) target.Inlines.Add(text[from..]);
    }

    private void PaintChevron()
    {
        if (_chevron is null) return;
        bool strong = IsHot || DataContext is TreeRow { IsSelected: true };
        _chevron.SetResourceReference(TextElement.ForegroundProperty, strong ? "ArcWpf.Brush.Foreground" : "ArcWpf.Brush.TextMuted");
    }

    // Each row unfolds its own height, so nested branches, pages and expand all share one motion.
    private void Unfold()
    {
        if (_info is not { Entering: true } info) return;
        info.Entering = false;
        DependencyObject? parent = this;
        while (parent is not null && parent is not ButtonBase) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not ButtonBase row) return;
        row.Opacity = 0;
        row.Height = 0;
        _motion.Double(row, HeightProperty, Height30, Tween.Smooth, true, spring: true);
        _motion.Double(row, OpacityProperty, 1, Tween.Fast, true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, inner = Math.Max(0, width - _left - RightPadding), used = 0;
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        if (_moreLabel is not null) { _moreLabel.Measure(free); _moreHidden!.Measure(free); return new Size(width, Height30); }
        if (_chevron is null) return new Size(width, Height30);
        _chevron.Measure(new Size(ChevronSize, ChevronSize));
        used += ChevronSize + Gap;
        _key!.Measure(new Size(inner * .45, double.PositiveInfinity));
        used += _key.DesiredSize.Width + Gap;
        _colon!.Measure(free);
        used += _colon.DesiredSize.Width - 5 + Gap;
        double rest = Math.Max(0, inner - used);
        if (_value is not null) _value.Measure(new Size(rest, double.PositiveInfinity));
        else
        {
            _count!.Measure(free);
            _preview?.Measure(new Size(Math.Max(0, rest - _count.DesiredSize.Width - 8), double.PositiveInfinity));
        }
        return new Size(width, Height30);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = _left;
        void Center(UIElement child)
        {
            child.Arrange(new Rect(x, (Height30 - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
            x += child.DesiredSize.Width;
        }
        if (_moreLabel is not null)
        {
            x += 20;
            Center(_moreLabel);
            x += 10;
            double baseline = (Height30 - _moreLabel.DesiredSize.Height) / 2 + _moreLabel.BaselineOffset;
            _moreHidden!.Arrange(new Rect(x, baseline - _moreHidden.BaselineOffset, _moreHidden.DesiredSize.Width, _moreHidden.DesiredSize.Height));
            return finalSize;
        }
        if (_chevron is null) return finalSize;
        Center(_chevron);
        x += Gap;
        Center(_key!);
        x += Gap - 5;
        Center(_colon!);
        x += Gap;
        if (_value is not null) { Center(_value); return finalSize; }
        if (_preview is not null)
        {
            double top = (Height30 - _preview.DesiredSize.Height) / 2;
            Center(_preview);
            x += 8;
            _count!.Arrange(new Rect(x, top + _preview.BaselineOffset - _count!.BaselineOffset, _count.DesiredSize.Width, _count.DesiredSize.Height));
        }
        else Center(_count!);
        return finalSize;
    }

    private const string ChevronRight = "M9,18 L15,12 L9,6";
}
