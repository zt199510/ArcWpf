using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcWpf.Controls;

/// <summary>A filled 24×24 glyph for a tool, a model or a source. The library ships no brand marks; pass your own.</summary>
public sealed record AiMark(string PathData, Color? Color = null);

/// <summary>Stroked 24×24 glyphs for commands, attach actions and tools. Any stroked path data works the same way.</summary>
public static class AiGlyphs
{
    public const string FileText = Lucide.FileText, Languages = Lucide.Languages, Lightbulb = Lucide.Lightbulb, Bug = Lucide.Bug, Paperclip = Lucide.Paperclip, Layers = Lucide.Layers, Globe = Lucide.Globe, Camera = Lucide.Camera, Brain = Lucide.Brain, Pencil = Lucide.Pencil, AtSign = Lucide.AtSign, Wrench = Lucide.Wrench;
}

// Shared pieces of the AI surfaces: PromptInput, StreamingMessage, ToolCallCard, ReasoningBlock, CitationList and ModelPicker.
internal static class AiUi
{
    public static FrameworkElement Mark(AiMark? mark, double size, string fallback)
    {
        if (mark is null)
        {
            var disc = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2) };
            disc.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.SurfaceMuted");
            var letter = new TextBlock { Text = fallback.Length > 0 ? char.ToUpperInvariant(fallback[0]).ToString() : "", FontSize = Math.Round(size * .62), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            letter.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
            disc.Child = letter;
            disc.IsHitTestVisible = false;
            return disc;
        }
        var path = new Path { Data = Geometry.Parse(mark.PathData), Width = 24, Height = 24, Stretch = Stretch.None };
        if (mark.Color is { } color) { path.Fill = new SolidColorBrush(color); } else { path.SetResourceReference(Shape.FillProperty, "ArcWpf.Brush.Foreground"); }
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }

    public static TextBlock Line(string text, double size, double lineHeight, string role = "Foreground", bool medium = false, bool trim = true)
    {
        var block = ArcText.Block(text, size, lineHeight, role, medium);
        block.VerticalAlignment = VerticalAlignment.Center;
        block.TextWrapping = TextWrapping.NoWrap;
        if (trim) { block.TextTrimming = TextTrimming.CharacterEllipsis; }
        return block;
    }

    public static TextBlock Wrapped(string text, double size, double lineHeight, string role = "Foreground", bool medium = false)
    {
        var block = ArcText.Block(text, size, lineHeight, role, medium);
        block.TextWrapping = TextWrapping.Wrap;
        return block;
    }

    public static TextBlock Mono(string text, double size, double lineHeight, string role = "Foreground")
    {
        var block = Line(text, size, lineHeight, role);
        block.SetResourceReference(TextBlock.FontFamilyProperty, "ArcWpf.Font.Mono");
        return block;
    }

    public static Viewbox Glyph(string data, double size, string role, double stroke = 1.75)
    {
        var glyph = Lucide.Glyph(data, size, stroke);
        glyph.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush." + role);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        return glyph;
    }

    // Arc's quiet secondary action: Retry, Deny, Search again.
    public static ToolbarButton Outline(string text, string? glyph = null, double height = 28)
    {
        var button = new ToolbarButton(height / 2, 1) { Height = height, Padding = new Thickness(glyph is null ? 12 : 10, 0, 12, 0), Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, "Border", "Foreground") };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph is not null) { var icon = Lucide.Glyph(glyph, 14); icon.Margin = new Thickness(0, 0, 6, 0); row.Children.Add(icon); }
        row.Children.Add(Line(text, 12, 16.8, "Foreground", true, false));
        button.Content = row;
        return button;
    }

    // The filled primary action: Allow.
    public static ToolbarButton Solid(string text, double height = 28)
    {
        var button = new ToolbarButton(height / 2) { Height = height, Padding = new Thickness(12, 0, 12, 0), Roles = self => (self.IsMouseOver ? "AccentStrong" : "Accent", null, "AccentForeground") };
        button.Content = Line(text, 12, 16.8, "AccentForeground", true, false);
        return button;
    }

    public static ToolbarButton Icon(string glyph, double hit, double size, string name)
    {
        var button = new ToolbarButton(hit / 3) { Width = hit, Height = hit, Padding = new Thickness(0), Content = Lucide.Glyph(glyph, size), Roles = self => (self.IsPressed ? "ControlTrack" : self.IsMouseOver ? "SurfaceMuted" : null, null, self.IsMouseOver ? "Foreground" : "TextSecondary") };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        return button;
    }

    public static string Duration(double milliseconds)
    {
        if (milliseconds < 1000) { return $"{Math.Round(milliseconds):0}ms"; }
        return (milliseconds / 1000).ToString(milliseconds >= 100000 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }

    public static string Seconds(double seconds)
    {
        int whole = (int)Math.Round(seconds);
        return whole < 60 ? $"{whole}s" : whole % 60 == 0 ? $"{whole / 60}m" : $"{whole / 60}m {whole % 60}s";
    }

    public static bool Moving(FrameworkElement element) => element.IsLoaded && Tween.Level(element) == ArcMotionLevel.Full;
}

// The registry's label swap: out in 100 ms, in over 160 ms from a 2 px rise, in one slot.
internal sealed class FadeSwap : Panel
{
    private UIElement? _current;

    public UIElement? Current => _current;

    public void Set(FrameworkElement view)
    {
        var old = _current as FrameworkElement;
        _current = view;
        Children.Add(view);
        InvalidateMeasure();
        if (old is null) { return; }
        if (!IsLoaded || Tween.Level(this) == ArcMotionLevel.Off) { Children.Remove(old); return; }
        var rise = new TranslateTransform(0, 2);
        view.RenderTransform = rise;
        view.Opacity = 0;
        Tween.To(view, OpacityProperty, 1, .16, ArcEasing.Enter);
        Tween.To(rise, TranslateTransform.YProperty, 0, .16, ArcEasing.Enter);
        old.IsHitTestVisible = false;
        Tween.To(old, OpacityProperty, 0, .1, ArcEasing.Standard, 0, () => Children.Remove(old));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children) { child.Measure(availableSize); }
        return _current?.DesiredSize ?? new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in Children) { child.Arrange(new Rect(0, 0, ReferenceEquals(child, _current) ? finalSize.Width : Math.Max(finalSize.Width, child.DesiredSize.Width), finalSize.Height)); }
        return finalSize;
    }
}

// Follows its content's width on the morph spring, so a new label glides instead of jumping.
internal sealed class SpringWidthBox : Decorator
{
    private readonly SpringDriver _driver;
    private readonly ControlSpring _width;
    private bool _measured;
    private double _content;

    public SpringWidthBox(double visualDuration = Tween.Morph)
    {
        _driver = new SpringDriver(InvalidateMeasure);
        _width = _driver.Add(0, ArcSpring.Of(visualDuration));
        ClipToBounds = true;
        Unloaded += (_, _) => _driver.Finish();
    }

    internal double WidthForTest => _width.Position;

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) { return new Size(); }
        Child.Measure(new Size(double.PositiveInfinity, constraint.Height));
        var desired = Child.DesiredSize;
        if (!_measured || !AiUi.Moving(this)) { _measured = true; _content = desired.Width; _driver.Stop(); _width.Snap(_content); }
        else if (Math.Abs(desired.Width - _content) > .01) { _content = desired.Width; _driver.Animate(_width, _content, Tween.Morph); }
        return new Size(Math.Min(constraint.Width, Math.Max(0, _width.Position)), desired.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, Math.Max(arrangeSize.Width, _content), arrangeSize.Height));
        return arrangeSize;
    }
}

// A chevron that turns on the shared spring.
internal sealed class TurningChevron : Decorator
{
    private readonly RotateTransform _turn = new();
    private bool _up;

    public TurningChevron(double size = 16, string role = "TextMuted")
    {
        var glyph = AiUi.Glyph(Lucide.ChevronDown, size, role);
        glyph.RenderTransform = _turn;
        glyph.RenderTransformOrigin = new Point(.5, .5);
        Child = glyph;
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public void Point(bool up)
    {
        if (_up == up) { return; }
        _up = up;
        if (AiUi.Moving(this)) { Tween.Spring(_turn, RotateTransform.AngleProperty, up ? 180 : 0, Tween.Snappy); } else { Tween.Set(_turn, RotateTransform.AngleProperty, up ? 180 : 0); }
    }
}

// lucide's loader ring, turning while it is on screen. Without motion it holds still.
internal sealed class AiSpinner : FrameworkElement
{
    private readonly RotateTransform _turn = new();
    private readonly double _size;
    private readonly string _role;

    public AiSpinner(double size = 16, string role = "TextMuted")
    {
        (_size, _role) = (size, role);
        Width = Height = size;
        IsHitTestVisible = false;
        RenderTransform = _turn;
        RenderTransformOrigin = new Point(.5, .5);
        Loaded += (_, _) => Sync();
        Unloaded += (_, _) => _turn.BeginAnimation(RotateTransform.AngleProperty, null);
        IsVisibleChanged += (_, _) => Sync();
        ThemeWatch.Add(this, InvalidateVisual);
    }

    internal bool TurningForTest { get; private set; }

    private void Sync()
    {
        TurningForTest = AiUi.Moving(this) && IsVisible;
        if (TurningForTest) { _turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.8)) { RepeatBehavior = RepeatBehavior.Forever }); }
        else { _turn.BeginAnimation(RotateTransform.AngleProperty, null); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double stroke = 1.75 * _size / 16, r = (_size - stroke) / 2;
        var ink = new SolidColorBrush(Tween.Resolve(this, "ArcWpf.Brush." + _role));
        var pen = new Pen(ink, stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var figure = new PathFigure { StartPoint = new Point(_size / 2, _size / 2 - r) };
        figure.Segments.Add(new ArcSegment(new Point(_size / 2 - r, _size / 2), new Size(r, r), 0, true, SweepDirection.Clockwise, true));
        dc.DrawGeometry(null, pen, new PathGeometry([figure]));
    }
}

// Copies a value, and shows a check for a moment without changing width.
internal sealed class CopyAction : ContentControl
{
    private readonly ToolbarButton _button;
    private readonly FadeSwap _face = new() { Width = 16, Height = 16 };
    private readonly DispatcherTimer _reset = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Func<string> _value;
    internal string? CopiedForTest { get; private set; }
    internal bool ShowsCheckForTest { get; private set; }
    internal Action<string>? WriteForTest { get; set; }

    public CopyAction(Func<string> value, string name, double hit = 28)
    {
        _value = value;
        _button = AiUi.Icon(Lucide.Copy, hit, 16, name);
        _face.Set(Lucide.Glyph(Lucide.Copy, 16));
        _button.Content = _face;
        _button.Click += (_, _) => Copy();
        _reset.Tick += (_, _) => { _reset.Stop(); ShowsCheckForTest = false; _face.Set(Lucide.Glyph(Lucide.Copy, 16)); };
        Unloaded += (_, _) => _reset.Stop();
        Focusable = false;
        Content = _button;
    }

    internal void Copy()
    {
        string text = _value();
        try { if (WriteForTest is { } write) { write(text); } else { Clipboard.SetText(text); } }
        catch (System.Runtime.InteropServices.ExternalException) { Announcer.Say(_button, "Copy failed"); return; }
        CopiedForTest = text;
        ShowsCheckForTest = true;
        _face.Set(AiUi.Glyph(Lucide.Check, 16, "Success"));
        Announcer.Say(_button, "Copied");
        _reset.Stop();
        _reset.Start();
    }
}

// Appends streamed text in chunks: each new run fades in once, then joins the theme like plain text.
internal static class ChunkFade
{
    public static Run Add(InlineCollection inlines, string text, string role, FrameworkElement owner, double seconds, FontWeight? weight = null)
    {
        var run = new Run(text);
        if (weight is { } bold) { run.FontWeight = bold; }
        Apply(run, role, owner, seconds);
        inlines.Add(run);
        return run;
    }

    public static void Apply(TextElement element, string role, FrameworkElement owner, double seconds)
    {
        string key = "ArcWpf.Brush." + role;
        if (!AiUi.Moving(owner) || seconds <= 0) { element.SetResourceReference(TextElement.ForegroundProperty, key); return; }
        var ink = Tween.Resolve(owner, key);
        var brush = new SolidColorBrush(OkColor.Alpha(ink, 0));
        element.Foreground = brush;
        var fade = new ColorAnimation(ink, TimeSpan.FromSeconds(seconds)) { EasingFunction = ArcEasing.Standard };
        fade.Completed += (_, _) => element.SetResourceReference(TextElement.ForegroundProperty, key);
        brush.BeginAnimation(SolidColorBrush.ColorProperty, fade);
    }
}
