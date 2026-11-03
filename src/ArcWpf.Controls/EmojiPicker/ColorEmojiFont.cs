using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcWpf.Controls;

/// <summary>The parts of an OpenType colour font this control draws from: COLR v0 layers, the CPAL palette, GSUB ligatures and TrueType outlines.</summary>
internal sealed class ColorEmojiTables
{
    /// <summary>Palette index meaning "use the text colour".</summary>
    public const ushort ForegroundIndex = 0xFFFF;
    /// <summary>Outlines are built at this many units per em, so curve tolerances stay small next to the shape.</summary>
    public const double OutlineUnits = 256;
    private const int MaxCompositeDepth = 6;
    private readonly byte[] _font;
    private readonly (int Offset, int Length) _glyf, _loca;
    private readonly int _glyphCount, _unitsPerEm;
    private readonly bool _longLoca;
    private readonly Dictionary<ushort, (ushort Glyph, ushort Palette)[]> _layers;
    private readonly Color[] _palette;
    private readonly Dictionary<ushort, (ushort[] Tail, ushort Result)[]>[] _lookups;

    private ColorEmojiTables(byte[] font, Dictionary<string, (int Offset, int Length)> tables, Dictionary<ushort, (ushort, ushort)[]> layers, Color[] palette, Dictionary<ushort, (ushort[], ushort)[]>[] lookups)
    {
        _font = font;
        _glyf = tables["glyf"];
        _loca = tables["loca"];
        _glyphCount = U16(font, tables["maxp"].Offset + 4);
        _unitsPerEm = U16(font, tables["head"].Offset + 18);
        _longLoca = U16(font, tables["head"].Offset + 50) != 0;
        if (_unitsPerEm == 0) throw new ArgumentException("A font needs a non-zero em size.", nameof(font));
        _layers = layers;
        _palette = palette;
        _lookups = lookups;
    }

    public bool TryGetLayers(ushort glyph, out (ushort Glyph, ushort Palette)[] layers) => _layers.TryGetValue(glyph, out layers!);

    public Color? PaletteColor(ushort index) => index == ForegroundIndex || index >= _palette.Length ? null : _palette[index];

    /// <summary>Reads a font file. Returns null when it has no usable colour tables, or they are malformed, so callers fall back to plain text.</summary>
    public static ColorEmojiTables? Parse(byte[] font)
    {
        try
        {
            var tables = ReadDirectory(font);
            if (!tables.TryGetValue("COLR", out var colr) || !tables.TryGetValue("CPAL", out var cpal)) return null;
            var layers = ReadColr(font.AsSpan(colr.Offset, colr.Length));
            if (layers.Count == 0) return null;
            var lookups = tables.TryGetValue("GSUB", out var gsub) ? ReadLigatures(font.AsSpan(gsub.Offset, gsub.Length)) : [];
            return new ColorEmojiTables(font, tables, layers, ReadCpal(font.AsSpan(cpal.Offset, cpal.Length)), lookups);
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or OverflowException or KeyNotFoundException) { return null; }
    }

    /// <summary>Applies the ligature lookups in order, longest match first, which is how ZWJ sequences, skin tones and keycaps become one glyph.</summary>
    public ushort[] Shape(IReadOnlyList<ushort> glyphs)
    {
        var current = glyphs.ToList();
        foreach (var lookup in _lookups)
        {
            for (int at = 0; at < current.Count; at++)
            {
                if (!lookup.TryGetValue(current[at], out var candidates)) continue;
                foreach (var (rest, result) in candidates)
                {
                    if (at + rest.Length >= current.Count) continue;
                    bool match = true;
                    for (int i = 0; i < rest.Length && match; i++) match = current[at + 1 + i] == rest[i];
                    if (!match) continue;
                    current.RemoveRange(at, rest.Length + 1);
                    current.Insert(at, result);
                    break;
                }
            }
        }
        return [.. current];
    }

    /// <summary>The glyph's outline with the baseline at y = 0 and y pointing down, or null when it is empty or malformed.</summary>
    public Geometry? Outline(ushort glyph)
    {
        try
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            bool drawn = false;
            using (var context = geometry.Open()) drawn = DrawGlyph(context, glyph, new Matrix(OutlineUnits / _unitsPerEm, 0, 0, -OutlineUnits / _unitsPerEm, 0, 0), 0);
            if (!drawn) return null;
            geometry.Freeze();
            // Single-point contours are anchors, so a glyph made only of them has nothing to draw.
            return geometry.Bounds.IsEmpty ? null : geometry;
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or OverflowException) { return null; }
    }

    private bool DrawGlyph(StreamGeometryContext context, int glyph, Matrix transform, int depth)
    {
        if (glyph >= _glyphCount || depth > MaxCompositeDepth) return false;
        int start = _longLoca ? U32(_font, _loca.Offset + glyph * 4) : U16(_font, _loca.Offset + glyph * 2) * 2;
        int end = _longLoca ? U32(_font, _loca.Offset + (glyph + 1) * 4) : U16(_font, _loca.Offset + (glyph + 1) * 2) * 2;
        if (end <= start || end > _glyf.Length) return false;
        var data = _font.AsSpan(_glyf.Offset + start, end - start);
        int contours = (short)U16(data, 0);
        return contours >= 0 ? DrawSimple(context, data, contours, transform) : DrawComposite(context, data, transform, depth);
    }

    private static bool DrawSimple(StreamGeometryContext context, ReadOnlySpan<byte> data, int contours, Matrix transform)
    {
        const byte OnCurve = 1, ShortX = 2, ShortY = 4, Repeat = 8, SameX = 16, SameY = 32;
        if (contours == 0) return false;
        int points = U16(data, 10 + (contours - 1) * 2) + 1;
        int at = 10 + contours * 2;
        at += 2 + U16(data, at);
        var flags = new byte[points];
        for (int i = 0; i < points; i++)
        {
            flags[i] = data[at++];
            if ((flags[i] & Repeat) == 0) continue;
            for (int repeat = data[at++]; repeat > 0; repeat--) { flags[i + 1] = flags[i]; i++; }
        }
        var location = new Point[points];
        int value = 0;
        for (int i = 0; i < points; i++)
        {
            if ((flags[i] & ShortX) != 0) { int delta = data[at++]; value += (flags[i] & SameX) != 0 ? delta : -delta; }
            else if ((flags[i] & SameX) == 0) { value += (short)U16(data, at); at += 2; }
            location[i].X = value;
        }
        value = 0;
        for (int i = 0; i < points; i++)
        {
            if ((flags[i] & ShortY) != 0) { int delta = data[at++]; value += (flags[i] & SameY) != 0 ? delta : -delta; }
            else if ((flags[i] & SameY) == 0) { value += (short)U16(data, at); at += 2; }
            location[i].Y = value;
        }
        transform.Transform(location);
        int first = 0;
        for (int contour = 0; contour < contours; contour++)
        {
            int last = U16(data, 10 + contour * 2);
            if (last < first) throw new ArgumentException("Contour ends must increase.", nameof(data));
            DrawContour(context, location.AsSpan(first, last - first + 1), flags.AsSpan(first, last - first + 1), OnCurve);
            first = last + 1;
        }
        return true;
    }

    // TrueType curves are quadratic, and two control points in a row imply an on-curve point halfway between them.
    private static void DrawContour(StreamGeometryContext context, ReadOnlySpan<Point> points, ReadOnlySpan<byte> flags, byte onCurve)
    {
        static Point Middle(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        int count = points.Length;
        if (count < 2) return;
        bool firstOn = (flags[0] & onCurve) != 0, lastOn = (flags[count - 1] & onCurve) != 0;
        var start = firstOn ? points[0] : lastOn ? points[count - 1] : Middle(points[0], points[count - 1]);
        context.BeginFigure(start, true, true);
        Point? control = null;
        for (int i = firstOn ? 1 : 0; i < count; i++)
        {
            bool on = (flags[i] & onCurve) != 0;
            if (on && control is { } pending) { context.QuadraticBezierTo(pending, points[i], true, false); control = null; }
            else if (on) context.LineTo(points[i], true, false);
            else
            {
                if (control is { } previous) context.QuadraticBezierTo(previous, Middle(previous, points[i]), true, false);
                control = points[i];
            }
        }
        if (control is { } closing) context.QuadraticBezierTo(closing, start, true, false);
    }

    private bool DrawComposite(StreamGeometryContext context, ReadOnlySpan<byte> data, Matrix transform, int depth)
    {
        const int WordArgs = 0x1, XyArgs = 0x2, Scale = 0x8, More = 0x20, XyScale = 0x40, TwoByTwo = 0x80;
        static double F2Dot14(ReadOnlySpan<byte> bytes, int at) => (short)U16(bytes, at) / 16384d;
        bool drawn = false;
        int at = 10, flags;
        do
        {
            flags = U16(data, at);
            int component = U16(data, at + 2);
            at += 4;
            double dx = 0, dy = 0;
            if ((flags & WordArgs) != 0) { if ((flags & XyArgs) != 0) { dx = (short)U16(data, at); dy = (short)U16(data, at + 2); } at += 4; }
            else { if ((flags & XyArgs) != 0) { dx = (sbyte)data[at]; dy = (sbyte)data[at + 1]; } at += 2; }
            double m11 = 1, m12 = 0, m21 = 0, m22 = 1;
            if ((flags & Scale) != 0) { m11 = m22 = F2Dot14(data, at); at += 2; }
            else if ((flags & XyScale) != 0) { m11 = F2Dot14(data, at); m22 = F2Dot14(data, at + 2); at += 4; }
            else if ((flags & TwoByTwo) != 0) { m11 = F2Dot14(data, at); m12 = F2Dot14(data, at + 2); m21 = F2Dot14(data, at + 4); m22 = F2Dot14(data, at + 6); at += 8; }
            drawn |= DrawGlyph(context, component, new Matrix(m11, m12, m21, m22, dx, dy) * transform, depth + 1);
        }
        while ((flags & More) != 0);
        return drawn;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int at) => BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
    private static int U32(ReadOnlySpan<byte> data, int at) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[at..]));

    private static Dictionary<string, (int Offset, int Length)> ReadDirectory(ReadOnlySpan<byte> font)
    {
        int start = 0;
        // A collection starts with its own header; the emoji face is the first one in the fonts this reads.
        if (font.Length >= 16 && Encoding.ASCII.GetString(font[..4]) == "ttcf") start = U32(font, 12);
        int count = U16(font, start + 4);
        var tables = new Dictionary<string, (int, int)>(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            int record = start + 12 + i * 16;
            tables[Encoding.ASCII.GetString(font.Slice(record, 4))] = (U32(font, record + 8), U32(font, record + 12));
        }
        return tables;
    }

    // COLR v1 fonts keep this v0 list as their fallback, and glyphs only in the v1 paint graph render as plain outlines.
    private static Dictionary<ushort, (ushort, ushort)[]> ReadColr(ReadOnlySpan<byte> colr)
    {
        int baseCount = U16(colr, 2), baseOffset = U32(colr, 4), layerOffset = U32(colr, 8);
        var result = new Dictionary<ushort, (ushort, ushort)[]>(baseCount);
        for (int i = 0; i < baseCount; i++)
        {
            int record = baseOffset + i * 6;
            int first = U16(colr, record + 2), count = U16(colr, record + 4);
            var layers = new (ushort, ushort)[count];
            for (int layer = 0; layer < count; layer++)
            {
                int at = layerOffset + (first + layer) * 4;
                layers[layer] = (U16(colr, at), U16(colr, at + 2));
            }
            result[U16(colr, record)] = layers;
        }
        return result;
    }

    private static Color[] ReadCpal(ReadOnlySpan<byte> cpal)
    {
        int entries = U16(cpal, 2), array = U32(cpal, 8), first = U16(cpal, 12);
        var colors = new Color[entries];
        for (int i = 0; i < entries; i++)
        {
            int at = array + (first + i) * 4;
            colors[i] = Color.FromArgb(cpal[at + 3], cpal[at + 2], cpal[at + 1], cpal[at]);
        }
        return colors;
    }

    private static Dictionary<ushort, (ushort[], ushort)[]>[] ReadLigatures(ReadOnlySpan<byte> gsub)
    {
        int list = U16(gsub, 8), count = U16(gsub, list);
        var lookups = new List<Dictionary<ushort, (ushort[], ushort)[]>>();
        for (int i = 0; i < count; i++)
        {
            int lookup = list + U16(gsub, list + 2 + i * 2);
            int type = U16(gsub, lookup), subtables = U16(gsub, lookup + 4);
            var merged = new Dictionary<ushort, List<(ushort[], ushort)>>();
            for (int s = 0; s < subtables; s++)
            {
                int subtable = lookup + U16(gsub, lookup + 6 + s * 2), actual = type;
                if (type == 7)
                {
                    actual = U16(gsub, subtable + 2);
                    subtable += U32(gsub, subtable + 4);
                }
                if (actual == 4 && U16(gsub, subtable) == 1) ReadLigatureSubtable(gsub, subtable, merged);
            }
            if (merged.Count > 0) lookups.Add(merged.ToDictionary(pair => pair.Key, pair => pair.Value.OrderByDescending(item => item.Item1.Length).ToArray()));
        }
        return [.. lookups];
    }

    private static void ReadLigatureSubtable(ReadOnlySpan<byte> gsub, int subtable, Dictionary<ushort, List<(ushort[], ushort)>> into)
    {
        var covered = ReadCoverage(gsub, subtable + U16(gsub, subtable + 2));
        int sets = U16(gsub, subtable + 4);
        for (int i = 0; i < sets && i < covered.Count; i++)
        {
            int set = subtable + U16(gsub, subtable + 6 + i * 2), ligatures = U16(gsub, set);
            if (!into.TryGetValue(covered[i], out var bucket)) into[covered[i]] = bucket = [];
            for (int l = 0; l < ligatures; l++)
            {
                int at = set + U16(gsub, set + 2 + l * 2);
                var rest = new ushort[U16(gsub, at + 2) - 1];
                for (int c = 0; c < rest.Length; c++) rest[c] = U16(gsub, at + 4 + c * 2);
                bucket.Add((rest, U16(gsub, at)));
            }
        }
    }

    private static List<ushort> ReadCoverage(ReadOnlySpan<byte> gsub, int at)
    {
        int format = U16(gsub, at), count = U16(gsub, at + 2);
        var glyphs = new List<ushort>(count);
        for (int i = 0; i < count; i++)
        {
            if (format == 1) { glyphs.Add(U16(gsub, at + 4 + i * 2)); continue; }
            int record = at + 4 + i * 6, first = U16(gsub, record), last = U16(gsub, record + 2);
            for (int glyph = first; glyph <= last; glyph++) glyphs.Add((ushort)glyph);
        }
        return glyphs;
    }
}

/// <summary>An emoji resolved to frozen, positioned outlines, one per colour layer, in <see cref="ColorEmojiTables.OutlineUnits"/>. The ink box is in ems.</summary>
internal sealed record ColorEmojiShape((Geometry Outline, Brush? Fill)[] Layers, double Advance, Rect Ink)
{
    public bool HasColor { get; } = Layers.Any(layer => layer.Fill is not null);

    /// <summary>Draws at the given em size with the ink centred on a point, because emoji sit differently on the baseline from font to font.</summary>
    public void Draw(DrawingContext context, Point centre, double size, Brush foreground, bool grayscale = false)
    {
        context.PushTransform(new TranslateTransform(Math.Round(centre.X - (Ink.X + Ink.Width / 2) * size), Math.Round(centre.Y - (Ink.Y + Ink.Height / 2) * size)));
        context.PushTransform(new ScaleTransform(size / ColorEmojiTables.OutlineUnits, size / ColorEmojiTables.OutlineUnits));
        foreach (var (outline, fill) in Layers) context.DrawGeometry(grayscale ? Desaturated(fill ?? foreground) : fill ?? foreground, null, outline);
        context.Pop();
        context.Pop();
    }

    private static Brush Desaturated(Brush fill)
    {
        if (fill is not SolidColorBrush { Color: var color }) return fill;
        byte luminance = (byte)Math.Round(.2126 * color.R + .7152 * color.G + .0722 * color.B);
        var gray = new SolidColorBrush(Color.FromArgb(color.A, luminance, luminance, luminance));
        gray.Freeze();
        return gray;
    }
}

/// <summary>
/// A colour font on this machine. WPF's text stack would draw these glyphs in one colour, so the layers are drawn as geometry.
/// Outlines come from the font data, because GlyphTypeface.GetGlyphOutline ended the process with an access violation on some Twemoji glyphs under Wine.
/// </summary>
internal sealed class ColorEmojiFont
{
    private const int ZeroWidthJoiner = 0x200D, EmojiPresentation = 0xFE0F;
    private static readonly Dictionary<string, ColorEmojiFont?> Fonts = new(StringComparer.Ordinal);
    private readonly GlyphTypeface _face;
    private readonly ColorEmojiTables _tables;
    private readonly Dictionary<string, ColorEmojiShape?> _shapes = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, Brush> _brushes = [];

    private ColorEmojiFont(GlyphTypeface face, ColorEmojiTables tables)
    {
        _face = face;
        _tables = tables;
    }

    /// <summary>Returns the family's colour font, or null when it is missing or has no colour layers.</summary>
    public static ColorEmojiFont? For(FontFamily family)
    {
        string key = $"{family.BaseUri}|{family.Source}";
        lock (Fonts)
        {
            if (Fonts.TryGetValue(key, out var known)) return known;
            ColorEmojiFont? font = null;
            if (new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var face))
            {
                using var stream = face.GetFontStream();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                if (ColorEmojiTables.Parse(copy.ToArray()) is { } tables) font = new ColorEmojiFont(face, tables);
            }
            return Fonts[key] = font;
        }
    }

    public ColorEmojiShape? Shape(string text)
    {
        lock (_shapes)
        {
            if (_shapes.TryGetValue(text, out var known)) return known;
            return _shapes[text] = Build(text);
        }
    }

    /// <summary>The shape when the font draws this text with colour layers, so plain characters the font also covers stay with the text font.</summary>
    public ColorEmojiShape? ColorShape(string text)
    {
        if (!_face.CharacterToGlyphMap.ContainsKey(text.EnumerateRunes().First().Value)) return null;
        return Shape(text) is { HasColor: true } shape ? shape : null;
    }

    private ColorEmojiShape? Build(string text)
    {
        var glyphs = Resolve(text);
        if (glyphs.Length == 0) return null;
        var layers = new List<(Geometry, Brush?)>();
        var ink = Rect.Empty;
        double x = 0;
        foreach (ushort glyph in glyphs)
        {
            if (glyph >= _face.GlyphCount) return null;
            if (_tables.TryGetLayers(glyph, out var parts))
                foreach (var (layer, palette) in parts) Add(layer, Fill(palette), x);
            else Add(glyph, null, x);
            x += _face.AdvanceWidths[glyph];
        }
        if (layers.Count == 0) return null;
        return new ColorEmojiShape([.. layers], x, new Rect(ink.X / ColorEmojiTables.OutlineUnits, ink.Y / ColorEmojiTables.OutlineUnits, ink.Width / ColorEmojiTables.OutlineUnits, ink.Height / ColorEmojiTables.OutlineUnits));

        void Add(ushort glyph, Brush? fill, double offset)
        {
            if (_tables.Outline(glyph) is not { } outline) return;
            var bounds = outline.Bounds;
            bounds.Offset(offset * ColorEmojiTables.OutlineUnits, 0);
            ink.Union(bounds);
            if (offset == 0) { layers.Add((outline, fill)); return; }
            var placed = new GeometryGroup { Children = { outline }, Transform = new TranslateTransform(offset * ColorEmojiTables.OutlineUnits, 0) };
            placed.Freeze();
            layers.Add((placed, fill));
        }
    }

    private Brush? Fill(ushort palette)
    {
        if (_tables.PaletteColor(palette) is not { } color) return null;
        if (_brushes.TryGetValue(palette, out var known)) return known;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return _brushes[palette] = brush;
    }

    // Fonts differ on whether a sequence keeps its variation selector, so both spellings are shaped and the shorter result wins.
    private ushort[] Resolve(string text)
    {
        var points = text.EnumerateRunes().Select(rune => rune.Value).ToArray();
        var full = _tables.Shape(Glyphs(points));
        if (full.Length <= 1 || !points.Contains(EmojiPresentation)) return full;
        var bare = _tables.Shape(Glyphs(points.Where(point => point != EmojiPresentation)));
        return bare.Length < full.Length ? bare : full;
    }

    private List<ushort> Glyphs(IEnumerable<int> points)
    {
        var glyphs = new List<ushort>();
        foreach (int point in points)
            if (_face.CharacterToGlyphMap.TryGetValue(point, out ushort glyph)) glyphs.Add(glyph);
            else if (point is not (ZeroWidthJoiner or EmojiPresentation)) glyphs.Add(0);
        return glyphs;
    }
}

/// <summary>One emoji in colour. Without a colour font it draws the same text in the family's plain glyphs.</summary>
internal sealed class ColorEmojiGlyph : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ColorEmojiGlyph), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty EmojiSizeProperty = DependencyProperty.Register(nameof(EmojiSize), typeof(double), typeof(ColorEmojiGlyph), new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty EmojiFamilyProperty = DependencyProperty.Register(nameof(EmojiFamily), typeof(FontFamily), typeof(ColorEmojiGlyph), new FrameworkPropertyMetadata(new FontFamily("Segoe UI Emoji"), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(ColorEmojiGlyph), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GrayscaleProperty = DependencyProperty.Register(nameof(Grayscale), typeof(bool), typeof(ColorEmojiGlyph), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double EmojiSize { get => (double)GetValue(EmojiSizeProperty); set => SetValue(EmojiSizeProperty, value); }
    public FontFamily EmojiFamily { get => (FontFamily)GetValue(EmojiFamilyProperty); set => SetValue(EmojiFamilyProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Draws the layers without colour, like the CSS filter grayscale(1).</summary>
    public bool Grayscale { get => (bool)GetValue(GrayscaleProperty); set => SetValue(GrayscaleProperty, value); }

    public ColorEmojiGlyph() => IsHitTestVisible = false;

    protected override Size MeasureOverride(Size availableSize) => new(EmojiSize * 1.25, EmojiSize * 1.25);

    protected override void OnRender(DrawingContext context)
    {
        if (string.IsNullOrEmpty(Text)) return;
        if (ColorEmojiFont.For(EmojiFamily)?.Shape(Text) is { } shape)
        {
            shape.Draw(context, new Point(ActualWidth / 2, ActualHeight / 2), EmojiSize, Foreground, Grayscale);
            return;
        }
        var text = new FormattedText(Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(EmojiFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), EmojiSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(text, new Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
    }
}
