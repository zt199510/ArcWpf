using System.Globalization;
using System.Text;

namespace ArcWpf.Controls;

public enum DiffFileStatus { Modified, Added, Deleted, Renamed }

public enum DiffLineKind { Context, Added, Removed }

public readonly record struct DiffLine(DiffLineKind Kind, int? OldNumber, int? NewNumber, string Text);

/// <summary>One file's changes. Immutable, so it can be built on a background thread.</summary>
public sealed class DiffFile
{
    // Myers keeps one row per edit: past this many edits two files are too different for the alignment to help.
    private const int MaxEdits = 2048;
    private static readonly HashSet<string> PlainLanguages = new(StringComparer.Ordinal) { "", "txt", "md", "log", "csv" };

    private DiffFile(string path, string? oldPath, DiffFileStatus status, string? language, bool isBinary, IReadOnlyList<DiffLine> lines, IReadOnlyList<string> notes, IReadOnlyDictionary<int, int> skipped)
    {
        Path = path;
        OldPath = oldPath;
        Status = status;
        Language = language;
        IsBinary = isBinary;
        Lines = lines;
        Notes = notes;
        Skipped = skipped;
        AddedLines = lines.Count(line => line.Kind == DiffLineKind.Added);
        RemovedLines = lines.Count(line => line.Kind == DiffLineKind.Removed);
    }

    public string Path { get; }
    /// <summary>The previous path, when the file moved.</summary>
    public string? OldPath { get; }
    public DiffFileStatus Status { get; }
    /// <summary>Tokenizer language, or null for plain text.</summary>
    public string? Language { get; }
    public bool IsBinary { get; }
    public int AddedLines { get; }
    public int RemovedLines { get; }
    /// <summary>Facts a reader needs but the lines can't show: line endings, invisible characters, a patch that couldn't be read.</summary>
    public IReadOnlyList<string> Notes { get; }
    /// <summary>Every line known. A patch only knows its hunks.</summary>
    public IReadOnlyList<DiffLine> Lines { get; }
    // Index in Lines before which a patch skipped content, and how many lines it skipped.
    internal IReadOnlyDictionary<int, int> Skipped { get; }

    /// <param name="oldText">Null when the file is new.</param>
    /// <param name="newText">Null when the file was deleted.</param>
    public static DiffFile FromText(string path, string? oldText, string? newText, string? oldPath = null, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var status = oldText is null ? DiffFileStatus.Added : newText is null ? DiffFileStatus.Deleted : oldPath is not null && oldPath != path ? DiffFileStatus.Renamed : DiffFileStatus.Modified;
        var notes = new List<string>();
        if (status == DiffFileStatus.Added) { notes.Add("New file."); }
        if (status == DiffFileStatus.Deleted) { notes.Add("File deleted."); }
        language ??= LanguageOf(path);
        if (LooksBinary(oldText) || LooksBinary(newText))
        {
            notes.Add("Binary file; contents are not shown.");
            return new DiffFile(path, oldPath, status, null, true, [], notes, new Dictionary<int, int>());
        }
        bool oldMark = oldText is ['\uFEFF', ..], newMark = newText is ['\uFEFF', ..];
        if (oldText is not null && newText is not null && oldMark != newMark) { notes.Add(newMark ? "A byte order mark was added." : "The byte order mark was removed."); }
        var before = Split(oldMark ? oldText![1..] : oldText);
        var after = Split(newMark ? newText![1..] : newText);
        var lines = Align(before, after, notes);
        AddHiddenCharacterNote(lines, notes);
        return new DiffFile(path, oldPath, status, language, false, lines, notes, new Dictionary<int, int>());
    }

    /// <summary>Reads a unified diff or git patch. Content that can't be read comes back as a note, never as a file with no changes.</summary>
    public static IReadOnlyList<DiffFile> ParsePatch(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var files = new PatchReader(patch).Read();
        if (files.Count == 0 && !string.IsNullOrWhiteSpace(patch))
        {
            files.Add(new DiffFile("patch", null, DiffFileStatus.Modified, null, false, [], ["This text is not a unified diff, so its changes can't be shown."], new Dictionary<int, int>()));
        }
        return files;
    }

    private static bool LooksBinary(string? text)
    {
        if (text is null) { return false; }
        return text.AsSpan(0, Math.Min(text.Length, 8000)).IndexOf('\0') >= 0;
    }

    internal static string? LanguageOf(string path)
    {
        string extension = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return PlainLanguages.Contains(extension) ? null : extension;
    }

    private readonly record struct SourceLine(string Text, bool Crlf, bool Terminated);

    private static List<SourceLine> Split(string? text)
    {
        var lines = new List<SourceLine>();
        if (string.IsNullOrEmpty(text)) { return lines; }
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0) { lines.Add(new SourceLine(text[start..], false, false)); break; }
            bool crlf = end > start && text[end - 1] == '\r';
            lines.Add(new SourceLine(text[start..(crlf ? end - 1 : end)], crlf, true));
            start = end + 1;
        }
        return lines;
    }

    private static List<DiffLine> Align(List<SourceLine> before, List<SourceLine> after, List<string> notes)
    {
        int head = 0, tail = 0;
        while (head < before.Count && head < after.Count && before[head].Text == after[head].Text) { head++; }
        while (tail < before.Count - head && tail < after.Count - head && before[before.Count - 1 - tail].Text == after[after.Count - 1 - tail].Text) { tail++; }
        int[] left = Intern(before, after, head, tail, out int[] right);
        var edits = Myers(left, right);
        if (edits is null)
        {
            notes.Add("The two versions are too different to align, so the changed part is shown as a full replacement.");
            edits = [.. Enumerable.Repeat(Edit.Remove, left.Length), .. Enumerable.Repeat(Edit.Add, right.Length)];
        }

        var lines = new List<DiffLine>(before.Count + after.Count);
        int oldIndex = 0, newIndex = 0, endingChanges = 0;
        void Same()
        {
            var a = before[oldIndex]; var b = after[newIndex];
            if (a.Terminated && b.Terminated && a.Crlf != b.Crlf) { endingChanges++; }
            lines.Add(new DiffLine(DiffLineKind.Context, oldIndex + 1, newIndex + 1, b.Text));
            oldIndex++; newIndex++;
        }
        for (int i = 0; i < head; i++) { Same(); }
        for (int e = 0; e < edits.Count;)
        {
            if (edits[e] == Edit.Same) { Same(); e++; continue; }
            // A change reads removed lines first, then added ones, however the edit path happened to interleave them.
            int removes = 0, adds = 0;
            for (; e < edits.Count && edits[e] != Edit.Same; e++) { if (edits[e] == Edit.Remove) { removes++; } else { adds++; } }
            for (int i = 0; i < removes; i++) { lines.Add(new DiffLine(DiffLineKind.Removed, oldIndex + 1, null, before[oldIndex].Text)); oldIndex++; }
            for (int i = 0; i < adds; i++) { lines.Add(new DiffLine(DiffLineKind.Added, null, newIndex + 1, after[newIndex].Text)); newIndex++; }
        }
        for (int i = 0; i < tail; i++) { Same(); }

        if (endingChanges > 0) { notes.Add(endingChanges == 1 ? "Line endings changed on 1 line." : $"Line endings changed on {endingChanges.ToString("N0", CultureInfo.InvariantCulture)} lines."); }
        if (before.Count > 0 && after.Count > 0 && before[^1].Terminated != after[^1].Terminated)
        {
            notes.Add(after[^1].Terminated ? "A newline was added at the end of the file." : "The newline at the end of the file was removed.");
        }
        return lines;
    }

    private static int[] Intern(List<SourceLine> before, List<SourceLine> after, int head, int tail, out int[] right)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        int Id(string text) { if (!ids.TryGetValue(text, out int id)) { id = ids.Count; ids[text] = id; } return id; }
        int[] left = new int[before.Count - head - tail];
        for (int i = 0; i < left.Length; i++) { left[i] = Id(before[head + i].Text); }
        right = new int[after.Count - head - tail];
        for (int i = 0; i < right.Length; i++) { right[i] = Id(after[head + i].Text); }
        return left;
    }

    private enum Edit : byte { Same, Remove, Add }

    // Myers' greedy O(ND) diff, keeping one row of furthest points per edit so the path can be walked back. Null when it would take more than MaxEdits.
    private static List<Edit>? Myers(int[] a, int[] b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0 || m == 0) { return [.. Enumerable.Repeat(Edit.Remove, n), .. Enumerable.Repeat(Edit.Add, m)]; }
        int limit = Math.Min(n + m, MaxEdits);
        int[] furthest = new int[2 * limit + 3];
        var history = new List<int[]>();
        int found = -1;
        for (int d = 0; d <= limit && found < 0; d++)
        {
            for (int k = -d; k <= d; k += 2)
            {
                int offset = k + limit + 1;
                int x = k == -d || (k != d && furthest[offset - 1] < furthest[offset + 1]) ? furthest[offset + 1] : furthest[offset - 1] + 1;
                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                furthest[offset] = x;
                if (x >= n && y >= m) { found = d; break; }
            }
            int[] row = new int[d + 1];
            for (int k = -d, i = 0; k <= d; k += 2, i++) { row[i] = furthest[k + limit + 1]; }
            history.Add(row);
        }
        if (found < 0) { return null; }

        var path = new List<Edit>(n + m);
        int px = n, py = m;
        for (int d = found; d > 0; d--)
        {
            int k = px - py;
            int[] previous = history[d - 1];
            int At(int diagonal) => previous[(diagonal + d - 1) / 2];
            bool down = k == -d || (k != d && At(k - 1) < At(k + 1));
            int fromK = down ? k + 1 : k - 1;
            int fromX = At(fromK), fromY = fromX - fromK;
            int startX = down ? fromX : fromX + 1;
            for (; px > startX; px--, py--) { path.Add(Edit.Same); }
            path.Add(down ? Edit.Add : Edit.Remove);
            px = fromX; py = fromY;
        }
        for (; px > 0; px--, py--) { path.Add(Edit.Same); }
        path.Reverse();
        return path;
    }

    private static void AddHiddenCharacterNote(IReadOnlyList<DiffLine> lines, List<string> notes)
    {
        foreach (var line in lines)
        {
            if (DiffText.HasHidden(line.Text))
            {
                notes.Add("Contains invisible or bidirectional characters, shown as [U+XXXX]. They can make code read differently from what runs.");
                return;
            }
        }
    }

    private sealed class PatchReader(string patch)
    {
        private readonly string[] _lines = patch.Replace("\r\n", "\n").Split('\n');
        private readonly List<DiffFile> _files = [];
        private int _at;

        // One file's header and hunks as they accumulate.
        private string? _gitOld, _gitNew, _minusPath, _plusPath, _renameFrom, _renameTo, _oldMode;
        private bool _open, _isNew, _isDeleted, _isBinary;
        private List<DiffLine> _body = [];
        private List<string> _notes = [];
        private Dictionary<int, int> _skipped = [];
        private int _lastOld;

        public List<DiffFile> Read()
        {
            while (_at < _lines.Length)
            {
                string line = _lines[_at];
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) { Close(); _open = true; ReadGitNames(line); _at++; }
                else if (line.StartsWith("--- ", StringComparison.Ordinal) && _at + 1 < _lines.Length && _lines[_at + 1].StartsWith("+++ ", StringComparison.Ordinal))
                {
                    if (_open && (_minusPath is not null || _body.Count > 0)) { Close(); }
                    _open = true;
                    _minusPath = HeaderPath(line[4..]);
                    _plusPath = HeaderPath(_lines[_at + 1][4..]);
                    _at += 2;
                }
                else if (_open && line.StartsWith("@@ ", StringComparison.Ordinal)) { ReadHunk(line); }
                else if (_open) { ReadExtendedHeader(line); _at++; }
                else { _at++; }
            }
            Close();
            return _files;
        }

        private void ReadExtendedHeader(string line)
        {
            if (line.StartsWith("new file mode", StringComparison.Ordinal)) { _isNew = true; }
            else if (line.StartsWith("deleted file mode", StringComparison.Ordinal)) { _isDeleted = true; }
            else if (line.StartsWith("rename from ", StringComparison.Ordinal)) { _renameFrom = Unquote(line[12..]); }
            else if (line.StartsWith("rename to ", StringComparison.Ordinal)) { _renameTo = Unquote(line[10..]); }
            else if (line.StartsWith("old mode ", StringComparison.Ordinal)) { _oldMode = line[9..]; }
            else if (line.StartsWith("new mode ", StringComparison.Ordinal)) { _notes.Add($"File mode changed from {_oldMode ?? "unknown"} to {line[9..]}."); }
            else if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line.StartsWith("GIT binary patch", StringComparison.Ordinal)) { _isBinary = true; }
        }

        private void ReadGitNames(string line)
        {
            string rest = line["diff --git ".Length..];
            if (rest.StartsWith('"'))
            {
                int close = ClosingQuote(rest);
                if (close > 0 && close + 2 <= rest.Length) { _gitOld = StripPrefix(Unquote(rest[..(close + 1)])); _gitNew = StripPrefix(Unquote(rest[(close + 2)..])); }
                return;
            }
            // The same name twice, so a path containing spaces can still be split down the middle.
            int middle = (rest.Length - 1) / 2;
            if (rest.Length % 2 == 1 && rest[middle] == ' ' && StripPrefix(rest[..middle]) == StripPrefix(rest[(middle + 1)..]))
            {
                _gitOld = _gitNew = StripPrefix(rest[..middle]);
                return;
            }
            int split = rest.IndexOf(" b/", StringComparison.Ordinal);
            if (split < 0) { split = rest.IndexOf(' '); }
            if (split > 0) { _gitOld = StripPrefix(rest[..split]); _gitNew = StripPrefix(Unquote(rest[(split + 1)..])); }
        }

        private void ReadHunk(string header)
        {
            if (!TryHunkHeader(header, out int oldStart, out int oldCount, out int newStart, out int newCount))
            {
                _notes.Add("A hunk header could not be read, so part of this file's changes is missing.");
                _at++;
                return;
            }
            int hidden = oldStart - _lastOld - 1;
            if (hidden > 0) { _skipped[_body.Count] = hidden; }
            _at++;
            int oldNo = oldStart, newNo = newStart, oldLeft = oldCount, newLeft = newCount;
            while ((oldLeft > 0 || newLeft > 0) && _at < _lines.Length)
            {
                string line = _lines[_at];
                // A blank line in a hunk is an empty context line whose leading space was stripped along the way.
                char mark = line.Length == 0 ? ' ' : line[0];
                string text = line.Length == 0 ? "" : line[1..];
                if (mark == ' ' && oldLeft > 0 && newLeft > 0) { _body.Add(new DiffLine(DiffLineKind.Context, oldNo++, newNo++, text)); oldLeft--; newLeft--; }
                else if (mark == '-' && oldLeft > 0) { _body.Add(new DiffLine(DiffLineKind.Removed, oldNo++, null, text)); oldLeft--; }
                else if (mark == '+' && newLeft > 0) { _body.Add(new DiffLine(DiffLineKind.Added, null, newNo++, text)); newLeft--; }
                else if (mark == '\\') { NoteMissingNewline(); }
                else { break; }
                _at++;
            }
            if (oldLeft > 0 || newLeft > 0) { _notes.Add("A hunk ended early, so part of this file's changes is missing."); }
            while (_at < _lines.Length && _lines[_at].StartsWith('\\')) { NoteMissingNewline(); _at++; }
            _lastOld = oldNo - 1;
        }

        private void NoteMissingNewline()
        {
            const string note = "A line has no newline at the end of the file.";
            if (!_notes.Contains(note)) { _notes.Add(note); }
        }

        private static bool TryHunkHeader(string header, out int oldStart, out int oldCount, out int newStart, out int newCount)
        {
            oldStart = oldCount = newStart = newCount = 0;
            int end = header.IndexOf(" @@", 3, StringComparison.Ordinal);
            if (end < 0) { return false; }
            string[] parts = header[3..end].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && parts[0].StartsWith('-') && parts[1].StartsWith('+') && TryRange(parts[0][1..], out oldStart, out oldCount) && TryRange(parts[1][1..], out newStart, out newCount);
        }

        private static bool TryRange(string range, out int start, out int count)
        {
            count = 1;
            int comma = range.IndexOf(',');
            if (comma >= 0 && !int.TryParse(range.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count)) { start = 0; return false; }
            return int.TryParse(comma >= 0 ? range.AsSpan(0, comma) : range, NumberStyles.None, CultureInfo.InvariantCulture, out start);
        }

        private void Close()
        {
            if (_open)
            {
                string? before = _minusPath ?? _gitOld ?? _renameFrom;
                string? after = _plusPath ?? _gitNew ?? _renameTo;
                bool added = _isNew || before == "/dev/null";
                bool deleted = _isDeleted || after == "/dev/null";
                string path = _renameTo ?? (deleted ? before : after) ?? before ?? "patch";
                string? oldPath = _renameFrom;
                var status = added ? DiffFileStatus.Added : deleted ? DiffFileStatus.Deleted : oldPath is not null && oldPath != path ? DiffFileStatus.Renamed : DiffFileStatus.Modified;
                if (added) { _notes.Insert(0, "New file."); }
                if (deleted) { _notes.Insert(0, "File deleted."); }
                if (_isBinary) { _notes.Add("Binary file; contents are not shown."); }
                AddHiddenCharacterNote(_body, _notes);
                _files.Add(new DiffFile(path, oldPath, status, _isBinary ? null : LanguageOf(path), _isBinary, _body, _notes, _skipped));
            }
            _gitOld = _gitNew = _minusPath = _plusPath = _renameFrom = _renameTo = _oldMode = null;
            _open = _isNew = _isDeleted = _isBinary = false;
            _body = [];
            _notes = [];
            _skipped = [];
            _lastOld = 0;
        }

        private static string HeaderPath(string value)
        {
            int tab = value.IndexOf('\t');
            return StripPrefix(Unquote((tab >= 0 ? value[..tab] : value).Trim()));
        }

        private static string StripPrefix(string path) => path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal) ? path[2..] : path;

        private static int ClosingQuote(string text)
        {
            for (int i = 1; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; }
                else if (text[i] == '"') { return i; }
            }
            return -1;
        }

        // Git quotes unusual paths C-style, with UTF-8 bytes as octal escapes.
        private static string Unquote(string value)
        {
            if (value.Length < 2 || value[0] != '"' || value[^1] != '"') { return value; }
            var bytes = new List<byte>(value.Length);
            for (int i = 1; i < value.Length - 1; i++)
            {
                char c = value[i];
                if (c != '\\' || i + 1 >= value.Length - 1) { bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString())); continue; }
                c = value[++i];
                if (c is >= '0' and <= '7' && i + 2 < value.Length - 1)
                {
                    bytes.Add((byte)(((c - '0') << 6) | ((value[i + 1] - '0') << 3) | (value[i + 2] - '0')));
                    i += 2;
                    continue;
                }
                bytes.Add((byte)(c switch { 'n' => '\n', 't' => '\t', 'r' => '\r', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v', _ => c }));
            }
            return Encoding.UTF8.GetString([.. bytes]);
        }
    }
}

// What a line looks like on screen. The clipboard always gets the original.
internal static class DiffText
{
    public const int MaxShown = 4000;
    private const int TabSize = 4;

    public static bool IsHidden(char c) => c is (>= '\u007F' and <= '\u009F') or '\u00AD' or '\u061C' or (>= '\u200B' and <= '\u200F') or (>= '\u2028' and <= '\u202E') or (>= '\u2060' and <= '\u2069') or '\uFEFF';

    public static bool HasHidden(string text)
    {
        foreach (char c in text) { if (IsHidden(c) || (c < ' ' && c != '\t')) { return true; } }
        return false;
    }

    /// <summary>Tabs become spaces, control characters become visible marks, and over-long lines are cut with a count.</summary>
    /// <param name="marks">Where the visible marks ended up, so they can be colored.</param>
    public static string Show(string text, out List<(int Start, int Length)>? marks)
    {
        marks = null;
        bool plain = text.Length <= MaxShown;
        if (plain) { foreach (char c in text) { if (c < ' ' || IsHidden(c)) { plain = false; break; } } }
        if (plain) { return text; }
        var shown = new StringBuilder(Math.Min(text.Length, MaxShown) + 16);
        int limit = Math.Min(text.Length, MaxShown);
        for (int i = 0; i < limit; i++)
        {
            char c = text[i];
            if (c == '\t') { shown.Append(' ', TabSize - shown.Length % TabSize); }
            else if (c < ' ' || IsHidden(c))
            {
                string mark = c == '\u007F' ? "␡" : c < ' ' ? ((char)(0x2400 + c)).ToString() : $"[U+{(int)c:X4}]";
                (marks ??= []).Add((shown.Length, mark.Length));
                shown.Append(mark);
            }
            else { shown.Append(c); }
        }
        if (text.Length > MaxShown) { shown.Append(" … ").Append((text.Length - MaxShown).ToString("N0", CultureInfo.InvariantCulture)).Append(" more characters"); }
        return shown.ToString();
    }

    /// <summary>The changed middle of two similar lines, or nothing when they share too little for it to mean anything.</summary>
    public static ((int Start, int Length) Old, (int Start, int Length) New)? ChangedRanges(string before, string after)
    {
        int head = 0, tail = 0;
        int shortest = Math.Min(before.Length, after.Length);
        while (head < shortest && before[head] == after[head]) { head++; }
        if (head > 0 && char.IsHighSurrogate(before[head - 1])) { head--; }
        while (tail < shortest - head && before[before.Length - 1 - tail] == after[after.Length - 1 - tail]) { tail++; }
        if (tail > 0 && char.IsLowSurrogate(before[before.Length - tail])) { tail--; }
        if (head + tail < Math.Max(before.Length, after.Length) / 4 + 1) { return null; }
        return ((head, before.Length - head - tail), (head, after.Length - head - tail));
    }
}
