using System.Globalization;
using System.Text;

namespace ArcWpf.Controls;

/// <summary>A currency as the en-US formatter reads it: narrow symbol, minor-unit digits, and display name.</summary>
public sealed record MoneyCurrency(string Code, string Symbol, int Digits, string Name, bool SymbolFirst = true)
{
    /// <summary>"$2,480.00", "¥88,000": the full amount for a value in minor units.</summary>
    public string Format(long minor) => Compose(MoneyMath.Display(MoneyMath.FromMinor(minor, Digits)), minor < 0);

    /// <summary>"+$10" style whole amounts for quick add chips.</summary>
    public string FormatWhole(decimal major) => Compose(MoneyMath.GroupInteger(Math.Round(Math.Abs(major), MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)), major < 0);

    private string Compose(string number, bool negative)
    {
        // Intl separates letter codes such as "CHF" from the number with a no-break space.
        string gap = Symbol.Any(char.IsLetter) ? " " : "";
        string body = SymbolFirst ? Symbol + gap + number : number + gap + Symbol;
        return negative ? "-" + body : body;
    }
}

/// <summary>
/// The registry's draft arithmetic (money-input.tsx, frozen 2026-10-31). A draft is the amount as typed in a canonical form: digits with an optional
/// ".", such as "1234.5". Values leave the field in minor units, so $12.50 is 1250. Formatting is fixed to en-US like the source's default locale.
/// </summary>
public static class MoneyMath
{
    public const int MaxIntegerDigits = 12;
    public const string Group = ",", Decimal = ".";

    private static readonly Dictionary<string, MoneyCurrency> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = new("USD", "$", 2, "US dollar"), ["EUR"] = new("EUR", "€", 2, "Euro"), ["GBP"] = new("GBP", "£", 2, "British pound"),
        ["JPY"] = new("JPY", "¥", 0, "Japanese yen"), ["CAD"] = new("CAD", "$", 2, "Canadian dollar"), ["AUD"] = new("AUD", "$", 2, "Australian dollar"),
        ["CHF"] = new("CHF", "CHF", 2, "Swiss franc"), ["INR"] = new("INR", "₹", 2, "Indian rupee"), ["CNY"] = new("CNY", "¥", 2, "Chinese yuan"),
        ["KRW"] = new("KRW", "₩", 0, "South Korean won"), ["MXN"] = new("MXN", "$", 2, "Mexican peso"), ["BRL"] = new("BRL", "R$", 2, "Brazilian real"),
    };

    /// <summary>Codes outside the built-in table keep the code as their symbol and two digits.</summary>
    public static MoneyCurrency Currency(string? code) =>
        Known.TryGetValue(code ?? "", out var info) ? info : new((code ?? "USD").ToUpperInvariant(), (code ?? "USD").ToUpperInvariant(), 2, (code ?? "USD").ToUpperInvariant());

    public static long Pow10(int digits) => (long)Math.Pow(10, digits);

    public static long? ToMinor(string draft, int digits)
    {
        if (string.IsNullOrEmpty(draft)) return null;
        var pieces = draft.Split('.');
        string whole = pieces[0], fraction = pieces.Length > 1 ? pieces[1] : "";
        long major = whole.Length == 0 ? 0 : long.Parse(whole, CultureInfo.InvariantCulture);
        string cents = fraction.PadRight(digits, '0')[..digits];
        return major * Pow10(digits) + (cents.Length == 0 ? 0 : long.Parse(cents, CultureInfo.InvariantCulture));
    }

    public static string FromMinor(long? minor, int digits)
    {
        if (minor is null) return "";
        string text = Math.Abs(minor.Value).ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        return digits > 0 ? $"{text[..^digits]}.{text[^digits..]}" : text;
    }

    /// <summary>Settles a draft the way it will be read back: fraction padded to the currency, leading zeros gone.</summary>
    public static string Settle(string draft, int digits) => string.IsNullOrEmpty(draft) ? "" : FromMinor(ToMinor(draft, digits), digits);

    /// <summary>Carries an amount across currencies with different minor units, keeping the major amount.</summary>
    public static string ConvertDraft(string draft, int digits)
    {
        if (string.IsNullOrEmpty(draft)) return "";
        var pieces = draft.Split('.');
        string whole = pieces[0];
        if (digits == 0) return whole.Length > 0 ? whole : "0";
        return pieces.Length == 1 ? whole : $"{whole}.{pieces[1][..Math.Min(pieces[1].Length, digits)]}";
    }

    public static string GroupInteger(string whole)
    {
        var builder = new StringBuilder();
        for (int index = 0; index < whole.Length; index++)
        {
            int place = whole.Length - 1 - index;
            builder.Append(whole[index]);
            if (place > 0 && place % 3 == 0) builder.Append(Group);
        }
        return builder.ToString();
    }

    /// <summary>What the input holds: grouped whole digits plus the fraction as typed.</summary>
    public static string Display(string draft)
    {
        if (string.IsNullOrEmpty(draft)) return "";
        var pieces = draft.Split('.');
        return GroupInteger(pieces[0]) + (pieces.Length > 1 ? Decimal + pieces[1] : "");
    }

    /// <summary>A mirrored character: keyed by place value (i0, g3, d, f1) so 9 → 10 keeps the ones column the ones column.</summary>
    public readonly record struct Part(string Key, string Char, bool Ghost);

    /// <summary>Columns for a draft; missing fraction digits are ghosts that hold their width.</summary>
    public static IReadOnlyList<Part> Parts(string draft, int digits)
    {
        var pieces = string.IsNullOrEmpty(draft) ? [] : draft.Split('.');
        string whole = pieces.Length > 0 ? pieces[0] : "";
        string? fraction = pieces.Length > 1 ? pieces[1] : null;
        var parts = new List<Part>();
        string integer = whole.Length > 0 ? whole : "0";
        bool ghostInteger = whole.Length == 0;
        for (int index = 0; index < integer.Length; index++)
        {
            int place = integer.Length - 1 - index;
            parts.Add(new($"i{place}", integer[index].ToString(), ghostInteger));
            if (place > 0 && place % 3 == 0) parts.Add(new($"g{place}", Group, ghostInteger));
        }
        if (digits > 0)
        {
            parts.Add(new("d", Decimal, fraction is null));
            for (int index = 0; index < digits; index++)
                parts.Add(new($"f{index}", fraction is not null && index < fraction.Length ? fraction[index].ToString() : "0", fraction is null || index >= fraction.Length));
        }
        return parts;
    }

    private static bool IsSignificant(char c) => char.IsAsciiDigit(c) || c.ToString() == Decimal;

    /// <summary>Where the caret goes after <paramref name="count"/> digits and decimal points.</summary>
    public static int PositionAfter(string text, int count)
    {
        if (count <= 0) return 0;
        int seen = 0;
        for (int index = 0; index < text.Length; index++) if (IsSignificant(text[index]) && ++seen == count) return index + 1;
        return text.Length;
    }

    /// <summary>Significant characters before <paramref name="caret"/>, used to put a refused keystroke's caret back.</summary>
    public static int SignificantBefore(string raw, int caret) => raw[..Math.Clamp(caret, 0, raw.Length)].Count(c => IsSignificant(c) || c == '.');

    /// <summary>
    /// Reads what the input holds into a draft and counts the digits and decimal before the caret, so the caret can return to the same spot.
    /// Null refuses the keystroke: too many fraction digits or more than twelve whole digits.
    /// </summary>
    public static (string Draft, int Before)? Sanitize(string raw, int caret, int digits)
    {
        var output = new StringBuilder();
        int before = 0;
        bool dot = false;
        for (int index = 0; index < raw.Length; index++)
        {
            char c = raw[index];
            string kept = "";
            if (char.IsAsciiDigit(c)) kept = c.ToString();
            else if (c == '.' && digits > 0 && !dot) { kept = "."; dot = true; }
            if (kept.Length == 0) continue;
            output.Append(kept);
            if (index < caret) before++;
        }
        var pieces = output.ToString().Split('.');
        string whole = pieces[0];
        string? fraction = pieces.Length > 1 ? pieces[1] : null;
        int stripped = whole.Length - whole.TrimStart('0').Length;
        whole = whole.TrimStart('0');
        int shift = -Math.Min(stripped, before);
        if (fraction is not null && whole.Length == 0) { whole = "0"; shift += 1; }
        if (fraction is null && whole.Length == 0 && stripped > 0) { whole = "0"; shift += 1; }
        if (fraction is not null && fraction.Length > digits) return null;
        if (whole.Length > MaxIntegerDigits) return null;
        return (fraction is not null ? $"{whole}.{fraction}" : whole, Math.Max(0, before + shift));
    }

    /// <summary>"$1,234.56", "1.234,56 €", "1234.5": a final separator followed by one or two digits is the decimal; the rest are groups.</summary>
    public static string? ParsePaste(string text, int digits)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"[.,](\d{1,2})\s*\D*$");
        string whole = new((match.Success ? text[..match.Index] : text).Where(char.IsAsciiDigit).ToArray());
        string pasted = match.Success && digits > 0 ? $"{whole}.{match.Groups[1].Value}" : whole;
        return pasted.Any(char.IsAsciiDigit) ? pasted : null;
    }
}
