using System.Globalization;
using System.Text;

namespace ArcWpf.Controls;

[Flags]
internal enum TerminalFlags : byte { None = 0, Bold = 1, Dim = 2, Italic = 4, Underline = 8, Inverse = 16, Strike = 32, Mark = 64 }

// A color is -1 for the default, 0..15 for the themed ANSI palette, or RgbBase + 0xRRGGBB.
internal readonly record struct TerminalStyle(int Foreground, int Background, TerminalFlags Flags)
{
    public const int RgbBase = 0x1000000;
    public static readonly TerminalStyle Default = new(-1, -1, TerminalFlags.None);
}

internal sealed class TerminalLine
{
    private readonly List<char> _chars = [];
    private readonly List<int> _styles = [];
    private string? _text;

    public int Version { get; private set; }
    public int Length => _chars.Count;
    public int Overflow { get; set; }
    public string Text => _text ??= new string(_chars.ToArray()) + (Overflow > 0 ? $" … {Overflow.ToString("N0", CultureInfo.InvariantCulture)} more characters" : "");
    // Drawn with the line, so each change to it is seen once.
    public object? Drawn { get; set; }
    public int DrawnVersion { get; set; } = -1;
    public int DrawnTheme { get; set; } = -1;

    // Fonts draw CJK about two cells wide, and the bar's range has to reach the end of such a line.
    public int Cells()
    {
        int cells = _chars.Count;
        foreach (char c in _chars) { if (c >= (char)0x1100) { cells++; } }
        return cells;
    }

    public int StyleAt(int index) => index < _styles.Count ? _styles[index] : 0;

    public void Put(int column, char c, int style)
    {
        while (_chars.Count < column) { _chars.Add(' '); _styles.Add(0); }
        if (column == _chars.Count) { _chars.Add(c); _styles.Add(style); }
        else { _chars[column] = c; _styles[column] = style; }
        Touch();
    }

    public void EraseFrom(int column)
    {
        if (column >= _chars.Count) { return; }
        _chars.RemoveRange(column, _chars.Count - column);
        _styles.RemoveRange(column, _styles.Count - column);
        Overflow = 0;
        Touch();
    }

    public void EraseTo(int column)
    {
        int end = Math.Min(column + 1, _chars.Count);
        for (int i = 0; i < end; i++) { _chars[i] = ' '; _styles[i] = 0; }
        if (end > 0) { Touch(); }
    }

    private void Touch() { _text = null; Version++; }
}

// Turns a program's output into styled lines. It handles what programs write to a pipe or a simple terminal: colors, carriage returns for progress, and erasing a line.
// It is not a terminal emulator: anything that draws a full screen is reduced to its text, and UsedScreenControls says so.
internal sealed class TerminalBuffer
{
    public const int MaxLineLength = 16000;
    private const int MaxStyles = 4096, MaxEscape = 256, MaxStringEscape = 65536, TabStop = 8;
    private const char Bel = (char)0x07, Backspace = (char)0x08, Tab = (char)0x09, Newline = (char)0x0A, Return = (char)0x0D, Escape = (char)0x1B, Delete = (char)0x7F;
    private const char C1Dcs = (char)0x90, C1Sos = (char)0x98, C1Csi = (char)0x9B, C1St = (char)0x9C, C1Osc = (char)0x9D, C1Pm = (char)0x9E, C1Apc = (char)0x9F;

    private enum Mode { Ground, Escape, EscapeIntermediate, Csi, Text, TextEscape }

    private readonly List<TerminalStyle> _styles = [TerminalStyle.Default];
    private readonly Dictionary<TerminalStyle, int> _styleIds = new() { [TerminalStyle.Default] = 0 };
    private readonly StringBuilder _escape = new();
    private TerminalStyle _pen = TerminalStyle.Default;
    private int _penId, _row, _column, _stringLength;
    private Mode _mode;

    public TerminalBuffer() => Lines.Add(new TerminalLine());

    public List<TerminalLine> Lines { get; } = [];
    public bool UsedScreenControls { get; private set; }
    public TerminalStyle StyleOf(int id) => _styles[id];

    public void Clear()
    {
        Lines.Clear();
        Lines.Add(new TerminalLine());
        _pen = TerminalStyle.Default;
        _penId = _row = _column = 0;
        _mode = Mode.Ground;
        _escape.Clear();
        UsedScreenControls = false;
    }

    /// <summary>Lines the view should list: a line the cursor has only just moved to has nothing to show yet.</summary>
    public int ShownCount => Lines[^1].Length == 0 ? Lines.Count - 1 : Lines.Count;

    public void TrimHead(int count)
    {
        count = Math.Min(count, Lines.Count - 1);
        if (count <= 0) { return; }
        Lines.RemoveRange(0, count);
        _row = Math.Max(0, _row - count);
    }

    // State survives between calls, so an escape sequence split across two chunks still works.
    public void Feed(ReadOnlySpan<char> text)
    {
        foreach (char c in text) { Step(c); }
    }

    private void Step(char c)
    {
        switch (_mode)
        {
            case Mode.Ground: Ground(c); break;
            case Mode.Escape: AfterEscape(c); break;
            case Mode.EscapeIntermediate: if (c is >= (char)0x30 and <= (char)0x7E) { _mode = Mode.Ground; } else if (c == Escape) { _mode = Mode.Escape; } break;
            case Mode.Csi: InCsi(c); break;
            case Mode.Text: InString(c); break;
            case Mode.TextEscape:
                if (c == '\\') { _mode = Mode.Ground; }
                else { _mode = Mode.Escape; AfterEscape(c); }
                break;
        }
    }

    private void Ground(char c)
    {
        switch (c)
        {
            case Escape: _mode = Mode.Escape; return;
            case C1Csi: BeginCsi(); return;
            case C1Osc or C1Dcs or C1Sos or C1Pm or C1Apc: BeginString(); return;
            case Newline: NextLine(); return;
            case Return: _column = 0; return;
            case Backspace: _column = Math.Max(0, _column - 1); return;
            case Tab: TabForward(); return;
            case Bel or Delete or C1St: return;
        }
        if (c < ' ') { return; }
        if (DiffText.IsHidden(c)) { WriteMark(c); return; }
        Put(c, _penId);
    }

    private void Put(char c, int style)
    {
        var line = Lines[_row];
        if (_column >= MaxLineLength) { line.Overflow++; return; }
        line.Put(_column, c, style);
        _column++;
    }

    // A control character that changes how text reads becomes something a person can see. Copying gives what is shown.
    private void WriteMark(char c)
    {
        int style = StyleId(_pen with { Flags = _pen.Flags | TerminalFlags.Mark });
        foreach (char shown in $"[U+{(int)c:X4}]") { Put(shown, style); }
    }

    private void TabForward()
    {
        int next = (_column / TabStop + 1) * TabStop;
        var line = Lines[_row];
        for (; _column < next && _column < MaxLineLength; _column++)
        {
            if (_column >= line.Length) { line.Put(_column, ' ', 0); }
        }
    }

    private void NextLine()
    {
        _column = 0;
        if (_row == Lines.Count - 1) { Lines.Add(new TerminalLine()); }
        _row++;
    }

    private void AfterEscape(char c)
    {
        switch (c)
        {
            case '[': BeginCsi(); break;
            case ']' or 'P' or 'X' or '^' or '_': BeginString(); break;
            case Escape: break;
            case >= ' ' and <= '/': _mode = Mode.EscapeIntermediate; break;
            default: _mode = Mode.Ground; break;
        }
    }

    private void BeginCsi() { _escape.Clear(); _mode = Mode.Csi; }

    private void BeginString() { _stringLength = 0; _mode = Mode.Text; }

    // Window titles, links and the like carry no text to show. One left open must not swallow everything after it, so it gives up after a while.
    private void InString(char c)
    {
        if (c is Bel or C1St) { _mode = Mode.Ground; }
        else if (c == Escape) { _mode = Mode.TextEscape; }
        else if (++_stringLength > MaxStringEscape) { _mode = Mode.Ground; }
    }

    private void InCsi(char c)
    {
        if (c == Escape) { _mode = Mode.Escape; return; }
        if (c < ' ') { return; }
        if (c is >= '@' and <= '~') { _mode = Mode.Ground; Dispatch(c); return; }
        if (_escape.Length >= MaxEscape) { _mode = Mode.Ground; return; }
        _escape.Append(c);
    }

    private void Dispatch(char final)
    {
        string body = _escape.ToString();
        if (body.Length > 0 && body[0] is '?' or '>' or '<' or '=')
        {
            // Private modes switch to a full-screen program's alternate screen, among other things.
            if (body.StartsWith("?1049", StringComparison.Ordinal) || body.StartsWith("?47", StringComparison.Ordinal) || body.StartsWith("?1047", StringComparison.Ordinal)) { UsedScreenControls = true; }
            return;
        }
        string[] parts = body.Split(';', ':');
        int Arg(int index, int fallback) => index < parts.Length && int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;
        switch (final)
        {
            case 'm': Colors(parts); break;
            case 'K': EraseLine(Arg(0, 0)); break;
            case 'G': _column = Math.Min(Arg(0, 1) - 1, MaxLineLength); break;
            case 'C': _column = Math.Min(_column + Arg(0, 1), MaxLineLength); break;
            case 'D': _column = Math.Max(0, _column - Arg(0, 1)); break;
            case 'A': _row = Math.Max(0, _row - Arg(0, 1)); break;
            case 'F': _row = Math.Max(0, _row - Arg(0, 1)); _column = 0; break;
            case 'B': _row = Math.Min(Lines.Count - 1, _row + Arg(0, 1)); break;
            case 'E': _row = Math.Min(Lines.Count - 1, _row + Arg(0, 1)); _column = 0; break;
            // Earlier output stays: nothing a program prints should be able to wipe the record of what it did.
            case 'H' or 'f' or 'J' or 'r' or 'S' or 'T' or 'L' or 'M': UsedScreenControls = true; break;
        }
    }

    private void EraseLine(int mode)
    {
        var line = Lines[_row];
        if (mode == 0) { line.EraseFrom(_column); }
        else if (mode == 1) { line.EraseTo(_column); }
        else { line.EraseFrom(0); }
    }

    private void Colors(string[] parts)
    {
        int fg = _pen.Foreground, bg = _pen.Background;
        var flags = _pen.Flags;
        int At(int index) => index < parts.Length && int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : 0;
        for (int i = 0; i < parts.Length; i++)
        {
            int code = At(i);
            switch (code)
            {
                case 0: fg = bg = -1; flags = TerminalFlags.None; break;
                case 1: flags |= TerminalFlags.Bold; break;
                case 2: flags |= TerminalFlags.Dim; break;
                case 3: flags |= TerminalFlags.Italic; break;
                case 4: flags |= TerminalFlags.Underline; break;
                case 7: flags |= TerminalFlags.Inverse; break;
                case 9: flags |= TerminalFlags.Strike; break;
                case 22: flags &= ~(TerminalFlags.Bold | TerminalFlags.Dim); break;
                case 23: flags &= ~TerminalFlags.Italic; break;
                case 24: flags &= ~TerminalFlags.Underline; break;
                case 27: flags &= ~TerminalFlags.Inverse; break;
                case 29: flags &= ~TerminalFlags.Strike; break;
                case >= 30 and <= 37: fg = code - 30; break;
                case 39: fg = -1; break;
                case >= 40 and <= 47: bg = code - 40; break;
                case 49: bg = -1; break;
                case >= 90 and <= 97: fg = code - 90 + 8; break;
                case >= 100 and <= 107: bg = code - 100 + 8; break;
                case 38 or 48:
                    int color = -1;
                    if (At(i + 1) == 5) { color = Indexed(At(i + 2)); i += 2; }
                    else if (At(i + 1) == 2) { color = TerminalStyle.RgbBase + ((Math.Min(At(i + 2), 255) << 16) | (Math.Min(At(i + 3), 255) << 8) | Math.Min(At(i + 4), 255)); i += 4; }
                    if (code == 38) { fg = color; } else { bg = color; }
                    break;
            }
        }
        _pen = new TerminalStyle(fg, bg, flags);
        _penId = StyleId(_pen);
    }

    // The 256-color table: 16 named colors, a 6x6x6 cube, then 24 grays.
    private static int Indexed(int index)
    {
        index = Math.Clamp(index, 0, 255);
        if (index < 16) { return index; }
        int r, g, b;
        if (index >= 232) { r = g = b = 8 + (index - 232) * 10; }
        else
        {
            int cube = index - 16;
            static int Level(int step) => step == 0 ? 0 : 55 + step * 40;
            r = Level(cube / 36); g = Level(cube / 6 % 6); b = Level(cube % 6);
        }
        return TerminalStyle.RgbBase + ((r << 16) | (g << 8) | b);
    }

    private int StyleId(TerminalStyle style)
    {
        if (_styleIds.TryGetValue(style, out int id)) { return id; }
        // A program cycling through true colors shouldn't grow this without end. Text past the limit keeps its characters and loses its color.
        if (_styles.Count >= MaxStyles) { return 0; }
        id = _styles.Count;
        _styles.Add(style);
        _styleIds[style] = id;
        return id;
    }
}
