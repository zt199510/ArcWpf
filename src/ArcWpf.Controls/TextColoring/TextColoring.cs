using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>A range of UTF-16 characters drawn in its own brush.</summary>
public readonly record struct TextSegment(int Start, int Length, Brush Foreground);

/// <summary>
/// Draws a TextBox's text in several colours while the TextBox keeps editing, selection, undo and input methods.
/// The native glyphs are hidden and redrawn at the positions the TextBox reports, inside its own scroll presenter.
/// Text containing right-to-left characters stays native, because runs can't be placed one by one there.
/// </summary>
public static class TextColoring
{
    private const int FirstEmojiPlane = 0x1F000, EmojiPresentation = 0xFE0F;
    private static readonly HashSet<string> CatalogEmoji = EmojiCatalog.Items.Select(item => item.Emoji).ToHashSet(StringComparer.Ordinal);

    /// <summary>Returns the segments for the current text. It runs on the UI thread after every text change; where segments overlap the later one wins.</summary>
    public static readonly DependencyProperty SegmenterProperty = DependencyProperty.RegisterAttached("Segmenter", typeof(Func<string, IEnumerable<TextSegment>>), typeof(TextColoring), new PropertyMetadata(null, OnOptionChanged));
    /// <summary>Draws emoji with the colour layers of <see cref="EmojiFontFamilyProperty"/>.</summary>
    public static readonly DependencyProperty ColorEmojiProperty = DependencyProperty.RegisterAttached("ColorEmoji", typeof(bool), typeof(TextColoring), new PropertyMetadata(false, OnOptionChanged));
    public static readonly DependencyProperty EmojiFontFamilyProperty = DependencyProperty.RegisterAttached("EmojiFontFamily", typeof(FontFamily), typeof(TextColoring), new PropertyMetadata(new FontFamily("Segoe UI Emoji"), OnOptionChanged));
    private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached("Session", typeof(TextColoringSession), typeof(TextColoring));

    public static Func<string, IEnumerable<TextSegment>>? GetSegmenter(TextBox box) => (Func<string, IEnumerable<TextSegment>>?)box.GetValue(SegmenterProperty);
    public static void SetSegmenter(TextBox box, Func<string, IEnumerable<TextSegment>>? value) => box.SetValue(SegmenterProperty, value);
    public static bool GetColorEmoji(TextBox box) => (bool)box.GetValue(ColorEmojiProperty);
    public static void SetColorEmoji(TextBox box, bool value) => box.SetValue(ColorEmojiProperty, value);
    public static FontFamily GetEmojiFontFamily(TextBox box) => (FontFamily)box.GetValue(EmojiFontFamilyProperty);
    public static void SetEmojiFontFamily(TextBox box, FontFamily value) => box.SetValue(EmojiFontFamilyProperty, value);

    /// <summary>Runs the segmenter again without a text change, for example after a theme switch.</summary>
    public static void Refresh(TextBox box) => ((TextColoringSession?)box.GetValue(SessionProperty))?.Refresh();

    internal static bool IsDrawing(TextBox box) => ((TextColoringSession?)box.GetValue(SessionProperty))?.IsDrawing == true;

    internal static Brush?[] ResolveBrushes(int length, IEnumerable<TextSegment> segments)
    {
        var brushes = new Brush?[length];
        foreach (var segment in segments)
        {
            int start = Math.Clamp(segment.Start, 0, length), end = (int)Math.Clamp((long)segment.Start + segment.Length, start, length);
            Array.Fill(brushes, segment.Foreground, start, end - start);
        }
        return brushes;
    }

    internal static bool ContainsRightToLeft(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF or >= 0x10800 and <= 0x10FFF or >= 0x1E800 and <= 0x1EFFF) return true;
        return false;
    }

    /// <summary>Text-default characters such as ↔ are emoji only with a variation selector, or when the catalog lists them bare, as it does ✅.</summary>
    internal static bool PrefersEmoji(string element) => element.EnumerateRunes().First().Value >= FirstEmojiPlane || element.Contains((char)EmojiPresentation) || CatalogEmoji.Contains(element);

    private static void OnOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        var session = (TextColoringSession?)box.GetValue(SessionProperty);
        bool wanted = GetSegmenter(box) is not null || GetColorEmoji(box);
        if (wanted && session is null) box.SetValue(SessionProperty, session = new TextColoringSession(box));
        else if (!wanted && session is not null) { session.Dispose(); box.ClearValue(SessionProperty); return; }
        session?.Refresh();
    }
}

internal sealed class TextColoringSession : IDisposable
{
    private readonly TextBox _box;
    private FrameworkElement? _view;
    private TextColoringAdorner? _adorner;

    public TextColoringSession(TextBox box)
    {
        _box = box;
        _box.Loaded += OnLoaded;
        _box.TextChanged += OnTextChanged;
        _box.SizeChanged += OnSizeChanged;
        _box.AddHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnScrollChanged);
    }

    public bool IsDrawing => _adorner is not null && _view?.Opacity == 0;

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();
    private void OnTextChanged(object sender, TextChangedEventArgs e) => Refresh();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => _adorner?.InvalidateVisual();
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => _adorner?.InvalidateVisual();

    public void Refresh()
    {
        Install();
        if (_adorner is null || _view is null) return;
        string text = _box.Text;
        bool native = _box.FlowDirection == FlowDirection.RightToLeft || TextColoring.ContainsRightToLeft(text);
        _adorner.Update(native ? null : TextColoring.GetSegmenter(_box)?.Invoke(text) is { } segments ? TextColoring.ResolveBrushes(text.Length, segments) : null, native);
        _view.Opacity = native ? 1 : 0;
    }

    private void Install()
    {
        if (_adorner is not null || !_box.IsLoaded) return;
        if (_box.Template?.FindName("PART_ContentHost", _box) is not ScrollViewer { Content: FrameworkElement view }) return;
        // Asked of the text view, this finds the layer inside the TextBox's scroll presenter, which clips to the viewport and stays below anything covering the TextBox.
        if (AdornerLayer.GetAdornerLayer(view) is not { } layer) return;
        _view = view;
        _adorner = new TextColoringAdorner(view, _box);
        layer.Add(_adorner);
    }

    public void Dispose()
    {
        _box.Loaded -= OnLoaded;
        _box.TextChanged -= OnTextChanged;
        _box.SizeChanged -= OnSizeChanged;
        _box.RemoveHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnScrollChanged);
        if (_adorner is not null && _view is not null) AdornerLayer.GetAdornerLayer(_view)?.Remove(_adorner);
        _view?.ClearValue(UIElement.OpacityProperty);
        _adorner = null;
        _view = null;
    }
}

internal sealed class TextColoringAdorner : Adorner
{
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty.AddOwner(typeof(TextColoringAdorner), new FrameworkPropertyMetadata(FontStretches.Normal, FrameworkPropertyMetadataOptions.AffectsRender));
    private readonly TextBox _box;
    private Brush?[]? _brushes;
    private bool _native;

    public TextColoringAdorner(UIElement adorned, TextBox box) : base(adorned)
    {
        _box = box;
        IsHitTestVisible = false;
        IsClipEnabled = true;
        foreach (var (target, source) in new[] { (ForegroundProperty, Control.ForegroundProperty), (FontFamilyProperty, Control.FontFamilyProperty), (FontSizeProperty, Control.FontSizeProperty), (FontWeightProperty, Control.FontWeightProperty), (FontStyleProperty, Control.FontStyleProperty), (FontStretchProperty, Control.FontStretchProperty) })
            SetBinding(target, new System.Windows.Data.Binding { Source = box, Path = new PropertyPath(source) });
    }

    public void Update(Brush?[]? brushes, bool native)
    {
        _brushes = brushes;
        _native = native;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        string text = _box.Text;
        if (_native || text.Length == 0) return;
        int first = _box.GetFirstVisibleLineIndex(), last = _box.GetLastVisibleLineIndex();
        if (first < 0 || _box.TransformToDescendant(this) is not { } toLocal) return;
        var pen = new RunPen(this, context, text, toLocal, TextColoring.GetColorEmoji(_box) ? ColorEmojiFont.For(TextColoring.GetEmojiFontFamily(_box)) : null, _brushes);
        for (int line = first; line <= last; line++)
        {
            int start = _box.GetCharacterIndexFromLineIndex(line);
            if (start >= 0) pen.DrawLine(_box, start, start + _box.GetLineLength(line));
        }
    }

    private sealed class RunPen(TextColoringAdorner owner, DrawingContext context, string text, GeneralTransform toLocal, ColorEmojiFont? emoji, Brush?[]? brushes)
    {
        private readonly Typeface _typeface = new((FontFamily)owner.GetValue(FontFamilyProperty), (FontStyle)owner.GetValue(FontStyleProperty), (FontWeight)owner.GetValue(FontWeightProperty), (FontStretch)owner.GetValue(FontStretchProperty));
        private readonly double _size = (double)owner.GetValue(FontSizeProperty), _pixelsPerDip = VisualTreeHelper.GetDpi(owner).PixelsPerDip;
        private readonly Brush _foreground = (Brush)owner.GetValue(ForegroundProperty);
        private readonly TextFormattingMode _mode = TextOptions.GetTextFormattingMode(owner);
        private readonly TextDecorationCollection? _decorations = owner._box.TextDecorations;

        public void DrawLine(TextBox box, int start, int end)
        {
            int run = start, at = start;
            while (at < end)
            {
                int length = StringInfo.GetNextTextElementLength(text.AsSpan(at, end - at));
                char head = text[at];
                if (head is '\r' or '\n' or '\t')
                {
                    Flush(box, run, at);
                    run = at += length;
                    continue;
                }
                if (EmojiAt(at, length) is { } shape)
                {
                    Flush(box, run, at);
                    DrawEmoji(box, shape, at, length, end);
                    run = at += length;
                    continue;
                }
                if (at > run && BrushAt(at) != BrushAt(run))
                {
                    Flush(box, run, at);
                    run = at;
                }
                at += length;
            }
            Flush(box, run, end);
        }

        private Brush BrushAt(int index) => brushes?[index] ?? _foreground;

        private ColorEmojiShape? EmojiAt(int at, int length)
        {
            if (emoji is null || (length == 1 && text[at] < 0x80)) return null;
            string element = text.Substring(at, length);
            return TextColoring.PrefersEmoji(element) ? emoji.ColorShape(element) : null;
        }

        private static bool Usable(Rect rect) => !rect.IsEmpty && !double.IsNaN(rect.X) && !double.IsNaN(rect.Y);

        private void Flush(TextBox box, int start, int end)
        {
            if (end <= start) return;
            var rect = box.GetRectFromCharacterIndex(start, false);
            if (!Usable(rect)) return;
            var run = new FormattedText(text[start..end], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, _size, BrushAt(start), null, _mode, _pixelsPerDip);
            if (_decorations is not null) run.SetTextDecorations(_decorations);
            context.DrawText(run, toLocal.Transform(new Point(rect.X, rect.Y + (rect.Height - run.Height) / 2)));
        }

        private void DrawEmoji(TextBox box, ColorEmojiShape shape, int at, int length, int lineEnd)
        {
            var leading = box.GetRectFromCharacterIndex(at, false);
            if (!Usable(leading)) return;
            int next = at + length;
            var trailing = next < lineEnd && text[next] is not ('\r' or '\n') ? box.GetRectFromCharacterIndex(next, false) : box.GetRectFromCharacterIndex(next - 1, true);
            double width = Usable(trailing) && Math.Abs(trailing.Y - leading.Y) < .5 ? Math.Abs(trailing.X - leading.X) : 0;
            // Native layout may have measured a narrower fallback glyph; the caret still has to land beside the emoji.
            double size = width > 0 && shape.Ink.Width > 0 ? Math.Min(_size, width / shape.Ink.Width) : _size;
            double centre = leading.X + (width > 0 ? width : shape.Ink.Width * size) / 2;
            shape.Draw(context, toLocal.Transform(new Point(centre, leading.Y + leading.Height / 2)), size, BrushAt(at));
        }
    }
}
