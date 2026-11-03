using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace ArcWpf.Controls;

public enum ArcDataGridColumnType { Text, Number, Currency, Percent, Select, Date }
public enum ArcDataGridAggregate { Sum, Average, Min, Max, Count, None }
public enum ArcDataGridDensity { Compact, Standard, Comfortable }
public enum ArcDataGridExportFormat { Csv, Xlsx }

/// <summary>One sort key. Sort lists run in priority order.</summary>
public readonly record struct ArcDataGridSort(string Key, bool Descending = false);

public sealed class ArcDataGridColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public double Width { get; set; } = 140;
    public double MinWidth { get; set; } = 72;
    public ArcDataGridColumnType Type { get; set; }
    /// <summary>.NET date format for date columns, always shown in en-US.</summary>
    public string DateFormat { get; set; } = "MMM d, yyyy";
    public int Decimals { get; set; }
    public bool Editable { get; set; } = true;
    /// <summary>Choices of a select column, which also set its sort order. Defaults to the distinct values in the column.</summary>
    public IReadOnlyList<string>? Options { get; set; }
    public bool Pinned { get; set; }
    public bool Hidden { get; set; }
    public ArcDataGridAggregate? Aggregate { get; set; }
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public bool? Groupable { get; set; }
    public bool IsNumeric => Type is ArcDataGridColumnType.Number or ArcDataGridColumnType.Currency or ArcDataGridColumnType.Percent;
    public bool CanGroup => Groupable ?? !IsNumeric;
}

/// <summary>A row: a stable id plus values keyed by column. Values are a string, a double, or null.</summary>
public sealed class ArcDataGridRow(string id)
{
    public string Id { get; } = id;
    public Dictionary<string, object?> Values { get; } = [];
    public object? this[string key]
    {
        get => Values.GetValueOrDefault(key);
        set => Values[key] = value is IConvertible and not string ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : value;
    }
    public ArcDataGridRow Clone()
    {
        var copy = new ArcDataGridRow(Id);
        foreach (var pair in Values) { copy.Values[pair.Key] = pair.Value; }
        return copy;
    }
}

public sealed class ArcDataGridBulkAction
{
    public string Label { get; set; } = string.Empty;
    /// <summary>An icon name from <see cref="ArcDataGridIcon"/>.</summary>
    public string? Icon { get; set; }
    public bool IsDanger { get; set; }
    public Action<IReadOnlyList<ArcDataGridRow>>? Invoked { get; set; }
}

/// <summary>Pure formatting, parsing, filtering and clipboard rules. They hold no UI state, so tests and servers can use them.</summary>
public static partial class ArcDataGridModel
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private const string Minus = "−";

    public static string Format(object? value, ArcDataGridColumn column)
    {
        if (value is null) { return string.Empty; }
        if (column.Type == ArcDataGridColumnType.Date) { return value is string iso && IsIsoDay(iso) ? FormatDay(iso, column.DateFormat) : Text(value); }
        return column.IsNumeric && value is double number ? FormatNumber(number, column.Type, column.Decimals) : Text(value);
    }

    private static string Text(object value) => value is double number ? number.ToString("0.###", En) : value.ToString() ?? string.Empty;

    // Exact halves round up, as they do in a browser; the framework's formatter sends them to the even digit.
    private static decimal RoundHalfUp(double magnitude, int decimals) =>
        magnitude < 1e15 ? Math.Round(decimal.Parse(magnitude.ToString("F20", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), decimals, MidpointRounding.AwayFromZero) : (decimal)magnitude;

    private static string FormatNumber(double number, ArcDataGridColumnType type, int decimals)
    {
        string body = RoundHalfUp(Math.Abs(number), decimals).ToString("N" + decimals, En);
        body = type switch { ArcDataGridColumnType.Currency => "$" + body, ArcDataGridColumnType.Percent => body + "%", _ => body };
        return number < 0 ? Minus + body : body;
    }

    /// <summary>The whole value of a group subtotal, where an average keeps one decimal.</summary>
    public static string FormatSubtotal(double value, ArcDataGridColumn column, ArcDataGridAggregate kind)
        => FormatNumber(value, kind == ArcDataGridAggregate.Count ? ArcDataGridColumnType.Number : column.Type, kind == ArcDataGridAggregate.Average ? Math.Max(1, column.Decimals) : kind == ArcDataGridAggregate.Count ? 0 : column.Decimals);

    /// <summary>The short totals-row text: 2.4M, $783.1M, 67.3%.</summary>
    public static string FormatTotal(double value, ArcDataGridColumn column, ArcDataGridAggregate kind)
    {
        var type = kind == ArcDataGridAggregate.Count ? ArcDataGridColumnType.Number : column.Type;
        if (type == ArcDataGridColumnType.Percent) { return FormatNumber(value, type, Math.Max(1, column.Decimals)); }
        double size = Math.Abs(value);
        (double scale, string suffix)? unit = size >= 1e12 ? (1e12, "T") : size >= 1e9 ? (1e9, "B") : size >= 1e6 ? (1e6, "M") : size >= 1e3 ? (1e3, "K") : null;
        if (unit is null) { return FormatNumber(value, type, kind == ArcDataGridAggregate.Average ? Math.Max(1, column.Decimals) : column.Decimals); }
        string body = (size / unit.Value.scale).ToString("0.#", En) + unit.Value.suffix;
        if (type == ArcDataGridColumnType.Currency) { body = "$" + body; }
        return value < 0 ? Minus + body : body;
    }

    public static string TotalLabel(ArcDataGridAggregate kind) => kind switch
    {
        ArcDataGridAggregate.Sum => "Sum", ArcDataGridAggregate.Average => "Avg", ArcDataGridAggregate.Min => "Min",
        ArcDataGridAggregate.Max => "Max", ArcDataGridAggregate.Count => "Count", _ => string.Empty
    };

    /// <summary>Reads typed or pasted text. Returns false when it does not fit the column.</summary>
    public static bool TryParse(string text, ArcDataGridColumn column, IReadOnlyList<string>? options, out object? value)
    {
        string trimmed = text.Trim();
        value = null;
        if (column.IsNumeric)
        {
            if (trimmed.Length == 0) { return true; }
            bool negative = trimmed.StartsWith('(') && trimmed.EndsWith(')');
            string digits = trimmed.Trim('(', ')').Replace("$", "").Replace("%", "").Replace(",", "").Replace(" ", "").Replace(Minus, "-");
            if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) { return false; }
            value = negative ? -number : number;
            return true;
        }
        if (column.Type == ArcDataGridColumnType.Date)
        {
            if (trimmed.Length == 0) { return true; }
            value = ParseDay(trimmed);
            return value is not null;
        }
        if (column.Type == ArcDataGridColumnType.Select && options is { Count: > 0 })
        {
            if (trimmed.Length == 0) { return true; }
            value = options.FirstOrDefault(option => string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
            return value is not null;
        }
        value = text;
        return true;
    }

    public static bool IsIsoDay(string text) => IsoDay().IsMatch(text) && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    public static string FormatDay(string iso, string format = "MMM d, yyyy") => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString(format, En);

    /// <summary>Reads 2026-01-31, 2026/1/31, Jan 31 2026 or 31 January 2026. All-number forms like 01/02/2026 read differently by country, so they are refused.</summary>
    public static string? ParseDay(string text)
    {
        string trimmed = text.Trim().Replace(",", "");
        string[] forms = ["yyyy-M-d", "yyyy/M/d", "MMM d yyyy", "MMMM d yyyy", "d MMM yyyy", "d MMMM yyyy"];
        return DateOnly.TryParseExact(trimmed, forms, En, DateTimeStyles.None, out var day) ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    }

    /// <summary>&gt;100, &gt;=100, &lt;5, !=0, =12, 12, or 10..50. Null when empty; false from <paramref name="valid"/> when unreadable.</summary>
    public static Func<double, bool>? NumberFilter(string expression, out bool valid)
    {
        valid = true;
        string text = expression.Trim().Replace(",", "").Replace("$", "").Replace("%", "");
        if (text.Length == 0) { return null; }
        int range = text.IndexOf("..", StringComparison.Ordinal);
        if (range >= 0)
        {
            if (Num(text[..range], out double low) && Num(text[(range + 2)..], out double high)) { return value => value >= low && value <= high; }
            valid = false;
            return null;
        }
        var (op, rest) = Operator(text);
        if (!Num(rest, out double target)) { valid = false; return null; }
        return op switch
        {
            ">" => value => value > target, ">=" => value => value >= target, "<" => value => value < target,
            "<=" => value => value <= target, "!=" => value => value != target, _ => value => value == target
        };
        static bool Num(string part, out double number) => double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>A day, month (2026-03) or year (2026) with the same operators, or a range like 2026-01-01..2026-03-31.</summary>
    public static Func<string, bool>? DateFilter(string expression, out bool valid)
    {
        valid = true;
        string text = expression.Trim();
        if (text.Length == 0) { return null; }
        int range = text.IndexOf("..", StringComparison.Ordinal);
        if (range >= 0)
        {
            if (Span(text[..range].Trim(), out string low, out _) && Span(text[(range + 2)..].Trim(), out _, out string high)) { return day => string.CompareOrdinal(day, low) >= 0 && string.CompareOrdinal(day, high) <= 0; }
            valid = false;
            return null;
        }
        var (op, rest) = Operator(text);
        if (!Span(rest.Trim(), out string first, out string last)) { valid = false; return null; }
        return op switch
        {
            ">" => day => string.CompareOrdinal(day, last) > 0, ">=" => day => string.CompareOrdinal(day, first) >= 0,
            "<" => day => string.CompareOrdinal(day, first) < 0, "<=" => day => string.CompareOrdinal(day, last) <= 0,
            "!=" => day => string.CompareOrdinal(day, first) < 0 || string.CompareOrdinal(day, last) > 0,
            _ => day => string.CompareOrdinal(day, first) >= 0 && string.CompareOrdinal(day, last) <= 0
        };
        static bool Span(string part, out string first, out string last)
        {
            first = last = string.Empty;
            if (Regex.IsMatch(part, @"^\d{4}$")) { first = part + "-01-01"; last = part + "-12-31"; return true; }
            if (Regex.IsMatch(part, @"^\d{4}-\d{2}$") && DateOnly.TryParseExact(part + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            {
                first = month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                last = month.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            }
            string? day = ParseDay(part);
            if (day is null) { return false; }
            first = last = day;
            return true;
        }
    }

    private static (string Op, string Tail) Operator(string text)
    {
        foreach (string op in new[] { ">=", "<=", "!=", ">", "<", "=" })
        {
            if (text.StartsWith(op, StringComparison.Ordinal)) { return (op, text[op.Length..]); }
        }
        return ("=", text);
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) { builder.Append(char.ToLowerInvariant(c)); }
        }
        return builder.ToString();
    }

    /// <summary>One predicate for all column filters and the quick search, or null when nothing filters.</summary>
    public static Func<ArcDataGridRow, bool>? RowMatcher(IReadOnlyList<ArcDataGridColumn> columns, IReadOnlyDictionary<string, object>? filters, string? search, IReadOnlyList<ArcDataGridColumn>? searchColumns = null)
    {
        var tests = new List<Func<ArcDataGridRow, bool>>();
        foreach (var column in columns)
        {
            if (filters is null || !filters.TryGetValue(column.Key, out object? filter)) { continue; }
            if (filter is IEnumerable<string> list)
            {
                var keep = list.ToHashSet(StringComparer.Ordinal);
                tests.Add(row => row[column.Key] is string value ? keep.Contains(value) : keep.Contains(string.Empty));
                continue;
            }
            string text = filter as string ?? string.Empty;
            if (text.Trim().Length == 0) { continue; }
            if (column.IsNumeric)
            {
                var test = NumberFilter(text, out _);
                if (test is not null) { tests.Add(row => row[column.Key] is double value && test(value)); }
            }
            else if (column.Type == ArcDataGridColumnType.Date)
            {
                var test = DateFilter(text, out _);
                if (test is not null) { tests.Add(row => row[column.Key] is string value && test(value)); }
            }
            else
            {
                bool exact = text.StartsWith('='), exclude = text.StartsWith('!');
                string query = Fold(exact || exclude ? text[1..] : text);
                tests.Add(row =>
                {
                    string value = Fold(row[column.Key]?.ToString() ?? string.Empty);
                    return exact ? value == query : exclude ? !value.Contains(query, StringComparison.Ordinal) : value.Contains(query, StringComparison.Ordinal);
                });
            }
        }
        var words = Fold(search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            var scope = searchColumns ?? columns;
            tests.Add(row =>
            {
                var cells = scope.Select(column => Fold(Format(row[column.Key], column))).ToArray();
                return words.All(word => cells.Any(cell => cell.Contains(word, StringComparison.Ordinal)));
            });
        }
        if (tests.Count == 0) { return null; }
        return row => { foreach (var test in tests) { if (!test(row)) { return false; } } return true; };
    }

    /// <summary>Stable multi-key sort. Empty cells sink to the bottom whichever way a key runs.</summary>
    public static List<ArcDataGridRow> SortRows(IEnumerable<ArcDataGridRow> rows, IReadOnlyList<ArcDataGridSort> sort, IReadOnlyList<ArcDataGridColumn> columns)
    {
        var result = rows.ToList();
        if (sort.Count == 0) { return result; }
        var keys = sort.Select(key => (key.Descending, Column: columns.FirstOrDefault(column => column.Key == key.Key))).Where(key => key.Column is not null).ToArray();
        var orders = keys.Select(key => key.Column!.Type == ArcDataGridColumnType.Select && key.Column.Options is { } options ? options.Select((option, index) => (option, index)).ToDictionary(pair => pair.option, pair => pair.index) : null).ToArray();
        var indexed = result.Select((row, index) => (row, index)).ToArray();
        Array.Sort(indexed, (a, b) =>
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i].Column!.Key;
                object? x = a.row[key], y = b.row[key];
                bool emptyX = x is null or "", emptyY = y is null or "";
                if (emptyX || emptyY)
                {
                    if (emptyX != emptyY) { return emptyX ? 1 : -1; }
                    continue;
                }
                int compared = x is double p && y is double q ? p.CompareTo(q)
                    : orders[i] is { } order && x is string s && y is string t && order.TryGetValue(s, out int si) && order.TryGetValue(t, out int ti) ? si.CompareTo(ti)
                    : string.Compare(x!.ToString(), y!.ToString(), StringComparison.OrdinalIgnoreCase);
                if (compared != 0) { return keys[i].Descending ? -compared : compared; }
            }
            return a.index.CompareTo(b.index);
        });
        return indexed.Select(pair => pair.row).ToList();
    }

    public static ArcDataGridAggregate DefaultAggregate(ArcDataGridColumn column) => column.Aggregate ?? column.Type switch
    {
        ArcDataGridColumnType.Number or ArcDataGridColumnType.Currency => ArcDataGridAggregate.Sum,
        ArcDataGridColumnType.Percent => ArcDataGridAggregate.Average,
        _ => ArcDataGridAggregate.None
    };

    public static double? Aggregate(IEnumerable<ArcDataGridRow> rows, ArcDataGridColumn column, ArcDataGridAggregate kind)
    {
        if (kind == ArcDataGridAggregate.None) { return null; }
        if (kind == ArcDataGridAggregate.Count) { return rows.Count(row => row[column.Key] is not (null or "")); }
        double sum = 0, min = double.MaxValue, max = double.MinValue;
        int count = 0;
        foreach (var row in rows)
        {
            if (row[column.Key] is not double value) { continue; }
            sum += value; count++;
            if (value < min) { min = value; }
            if (value > max) { max = value; }
        }
        if (count == 0) { return null; }
        return kind switch { ArcDataGridAggregate.Sum => sum, ArcDataGridAggregate.Average => sum / count, ArcDataGridAggregate.Min => min, _ => max };
    }

    /// <summary>A1 column letters: 0 is A, 26 is AA.</summary>
    public static string ColumnLetters(int index)
    {
        string letters = string.Empty;
        for (int value = index + 1; value > 0; value = (value - 1) / 26) { letters = (char)('A' + (value - 1) % 26) + letters; }
        return letters;
    }

    public static string ToTsv(IEnumerable<IReadOnlyList<string>> matrix)
    {
        static string Cell(string cell) => cell.IndexOfAny(['\t', '\n', '\r', '"']) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
        return string.Join("\n", matrix.Select(row => string.Join("\t", row.Select(Cell))));
    }

    public static List<string[]> ParseTsv(string text) => ParseDelimited(text, '\t');

    private static List<string[]> ParseDelimited(string text, char separator)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c != '"') { cell.Append(c); continue; }
                if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; continue; }
                quoted = false;
            }
            else if (c == '"' && cell.Length == 0) { quoted = true; }
            else if (c == separator) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') { i++; }
                row.Add(cell.ToString()); cell.Clear();
                rows.Add([.. row]); row.Clear();
            }
            else { cell.Append(c); }
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return rows;
    }

    public static string ToHtml(IEnumerable<IReadOnlyList<string>> matrix)
        => "<table>" + string.Concat(matrix.Select(row => "<tr>" + string.Concat(row.Select(cell => "<td>" + WebUtility.HtmlEncode(cell) + "</td>")) + "</tr>")) + "</table>";

    public static List<string[]>? ParseHtmlTable(string html)
    {
        var rows = new List<string[]>();
        foreach (Match row in HtmlRow().Matches(html))
        {
            rows.Add(HtmlCell().Matches(row.Groups[1].Value).Select(cell => WebUtility.HtmlDecode(HtmlTag().Replace(cell.Groups[1].Value, string.Empty)).Trim()).ToArray());
        }
        return rows.Count == 0 ? null : rows;
    }

    /// <summary>RFC 4180 CSV with a byte order mark so Excel reads UTF-8. Numbers and days stay raw.</summary>
    public static string ToCsv(IReadOnlyList<ArcDataGridColumn> columns, IEnumerable<ArcDataGridRow> rows)
    {
        static string Cell(string cell) => cell.IndexOfAny([',', '\n', '\r', '"']) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
        var builder = new StringBuilder("﻿");
        builder.Append(string.Join(",", columns.Select(column => Cell(column.Header)))).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(string.Join(",", columns.Select(column => Cell(row[column.Key] switch { null => string.Empty, double number => number.ToString("R", CultureInfo.InvariantCulture), var value => value.ToString() ?? string.Empty })))).Append("\r\n");
        }
        return builder.ToString();
    }

    /// <summary>Reads back <see cref="ToCsv"/> output, or any CSV with a header row, into rows for the given columns.</summary>
    public static List<ArcDataGridRow> ParseCsv(string csv, IReadOnlyList<ArcDataGridColumn> columns)
    {
        var table = ParseDelimited(csv.TrimStart('﻿'), ',');
        var rows = new List<ArcDataGridRow>(Math.Max(0, table.Count - 1));
        for (int r = 1; r < table.Count; r++)
        {
            var row = new ArcDataGridRow("r" + r);
            for (int c = 0; c < columns.Count && c < table[r].Length; c++)
            {
                string cell = table[r][c];
                row.Values[columns[c].Key] = columns[c].IsNumeric ? double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : null : cell.Length == 0 ? null : cell;
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Fill handle values: an evenly spaced number run continues its series; days continue by days, or by months when they share a day or are all month ends; anything else repeats.</summary>
    public static List<object?> Extend(IReadOnlyList<object?> source, int count, bool forward, ArcDataGridColumnType type = ArcDataGridColumnType.Text)
    {
        var result = new List<object?>(count);
        if (source.Count == 0) { return result; }
        if (source.Count >= 2 && source.All(value => value is double))
        {
            var numbers = source.Cast<double>().ToArray();
            double step = numbers[1] - numbers[0];
            bool even = true;
            for (int i = 2; i < numbers.Length; i++) { even &= Math.Abs(numbers[i] - numbers[i - 1] - step) < 1e-9; }
            if (even)
            {
                for (int i = 1; i <= count; i++) { result.Add(forward ? numbers[^1] + step * i : numbers[0] - step * i); }
                return result;
            }
        }
        if (type == ArcDataGridColumnType.Date && source.Count >= 2 && source.All(value => value is string day && IsIsoDay(day)))
        {
            var days = source.Cast<string>().Select(day => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray();
            int dayStep = days[1].DayNumber - days[0].DayNumber;
            if (Enumerable.Range(1, days.Length - 1).All(i => days[i].DayNumber - days[i - 1].DayNumber == dayStep) && days.Select(day => day.Day).Distinct().Count() > 1 && !days.All(IsMonthEnd))
            {
                for (int i = 1; i <= count; i++) { result.Add((forward ? days[^1].AddDays(dayStep * i) : days[0].AddDays(-dayStep * i)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
                return result;
            }
            int Months(DateOnly day) => day.Year * 12 + day.Month;
            int monthStep = Months(days[1]) - Months(days[0]);
            bool monthly = Enumerable.Range(1, days.Length - 1).All(i => Months(days[i]) - Months(days[i - 1]) == monthStep);
            if (monthly && (days.All(IsMonthEnd) || days.Select(day => day.Day).Distinct().Count() == 1))
            {
                bool ends = days.All(IsMonthEnd);
                for (int i = 1; i <= count; i++)
                {
                    var next = forward ? days[^1].AddMonths(monthStep * i) : days[0].AddMonths(-monthStep * i);
                    if (ends) { next = new DateOnly(next.Year, next.Month, DateTime.DaysInMonth(next.Year, next.Month)); }
                    result.Add(next.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                return result;
            }
        }
        for (int i = 0; i < count; i++)
        {
            int index = forward ? i % source.Count : source.Count - 1 - i % source.Count;
            result.Add(source[index]);
        }
        return result;
        static bool IsMonthEnd(DateOnly day) => day.Day == DateTime.DaysInMonth(day.Year, day.Month);
    }

    /// <summary>Writes an .xlsx workbook with no dependencies: typed cells, column widths, a frozen header and pinned columns, and a bold totals row.</summary>
    public static void WriteXlsx(Stream stream, IReadOnlyList<ArcDataGridColumn> columns, IEnumerable<ArcDataGridRow> rows, string sheetName = "Sheet1", int freezeColumns = 0)
    {
        var list = rows as IReadOnlyList<ArcDataGridRow> ?? rows.ToList();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        void Part(string name, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.NoCompression).Open(), new UTF8Encoding(false));
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            writer.Write(xml);
        }
        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", Pack = "http://schemas.openxmlformats.org/package/2006/relationships";
        Part("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
        Part("_rels/.rels", $"<Relationships xmlns=\"{Pack}\"><Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Part("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{Pack}\"><Relationship Id=\"rId1\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"{Rel}/styles\" Target=\"styles.xml\"/></Relationships>");
        Part("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets><sheet name=\"{SecurityElement.Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        // Styles: 0 general, 1 bold, 2 grouped number, 3 currency, 4 percent (values are whole percents), 5 date; 6-9 are the same in bold.
        Part("xl/styles.xml", $"<styleSheet xmlns=\"{Main}\"><numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"&quot;$&quot;#,##0\"/><numFmt numFmtId=\"165\" formatCode=\"0.0&quot;%&quot;\"/></numFmts><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"9\"><xf/><xf fontId=\"1\" applyFont=\"1\"/><xf numFmtId=\"3\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/><xf numFmtId=\"14\" applyNumberFormat=\"1\"/><xf numFmtId=\"3\" fontId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\"/><xf numFmtId=\"164\" fontId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\"/><xf numFmtId=\"165\" fontId=\"1\" applyNumberFormat=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
        var sheet = new StringBuilder($"<worksheet xmlns=\"{Main}\"><sheetViews><sheetView workbookViewId=\"0\"><pane {(freezeColumns > 0 ? $"xSplit=\"{freezeColumns}\" " : "")}ySplit=\"1\" topLeftCell=\"{ColumnLetters(freezeColumns)}2\" activePane=\"{(freezeColumns > 0 ? "bottomRight" : "bottomLeft")}\" state=\"frozen\"/></sheetView></sheetViews><cols>");
        for (int c = 0; c < columns.Count; c++) { sheet.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{Math.Round(columns[c].Width / 7, 1).ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>"); }
        sheet.Append("</cols><sheetData><row r=\"1\">");
        for (int c = 0; c < columns.Count; c++) { sheet.Append($"<c r=\"{ColumnLetters(c)}1\" s=\"1\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(columns[c].Header)}</t></is></c>"); }
        sheet.Append("</row>");
        int number = 2;
        foreach (var row in list)
        {
            sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{number}\">");
            for (int c = 0; c < columns.Count; c++)
            {
                var column = columns[c];
                string cell = ColumnLetters(c) + number.ToString(CultureInfo.InvariantCulture);
                switch (row[column.Key])
                {
                    case double value:
                        sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{cell}\" s=\"{(column.Type == ArcDataGridColumnType.Currency ? 3 : column.Type == ArcDataGridColumnType.Percent ? 4 : 2)}\"><v>{value.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    case string day when column.Type == ArcDataGridColumnType.Date && IsIsoDay(day):
                        double serial = DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).DayNumber - new DateOnly(1899, 12, 30).DayNumber;
                        sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{cell}\" s=\"5\"><v>{serial}</v></c>");
                        break;
                    case { } other:
                        sheet.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(other.ToString())}</t></is></c>");
                        break;
                }
            }
            sheet.Append("</row>");
            number++;
        }
        if (list.Count > 0)
        {
            sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{number}\">");
            for (int c = 0; c < columns.Count; c++)
            {
                var kind = DefaultAggregate(columns[c]);
                if (kind == ArcDataGridAggregate.None || Aggregate(list, columns[c], kind) is not { } total) { continue; }
                int style = columns[c].Type == ArcDataGridColumnType.Currency ? 7 : columns[c].Type == ArcDataGridColumnType.Percent ? 8 : 6;
                sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{ColumnLetters(c)}{number}\" s=\"{style}\"><v>{total.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");
        Part("xl/worksheets/sheet1.xml", sheet.ToString());
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")] private static partial Regex IsoDay();
    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex HtmlRow();
    [GeneratedRegex(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex HtmlCell();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex HtmlTag();
}
