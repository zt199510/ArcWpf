using System.Text.RegularExpressions;

namespace ArcWpf.Controls;

public enum PhoneStatus { Empty, Incomplete, Valid, TooLong }

/// <summary>
/// A country in the picker. Patterns format the national significant number (no trunk prefix); <c>#</c> is a digit. Valid lengths are the digit
/// capacities of the patterns; the example doubles as the typing guide.
/// </summary>
public sealed record PhoneCountry(string Iso, string Name, string Dial, IReadOnlyList<string> Patterns, string Example, string Trunk = "0", IReadOnlyList<string>? AreaCodes = null)
{
    public IReadOnlyList<int> Lengths => Patterns.Select(PhoneNumbers.Capacity).ToList();
}

/// <summary>The registry's compact phone table and formatter (phone-input.tsx, frozen 2026-10-31): no metadata download, no dependency.</summary>
public static class PhoneNumbers
{
    private static readonly string[] CaAreaCodes = "204 226 236 249 250 263 289 306 343 354 365 367 368 382 387 403 416 418 428 431 437 438 450 468 474 506 514 519 548 579 581 584 587 604 613 639 647 672 683 705 709 742 753 778 780 782 807 819 825 867 873 879 902 905".Split(' ');

    public static IReadOnlyList<PhoneCountry> Countries { get; } =
    [
        new("US", "United States", "1", ["(###) ###-####"], "2015550123", "1"),
        new("CA", "Canada", "1", ["(###) ###-####"], "5065550123", "1", CaAreaCodes),
        new("MX", "Mexico", "52", ["## #### ####"], "2221234567", ""),
        new("BR", "Brazil", "55", ["(##) ####-####", "(##) #####-####"], "11961234567"),
        new("BR", "Brazil", "55", ["(##) ####-####", "(##) #####-####"], "11961234567"),
        new("AR", "Argentina", "54", ["## ####-####"], "1123456789"),
        new("CO", "Colombia", "57", ["### ### ####"], "3211234567", ""),
        new("CL", "Chile", "56", ["# #### ####"], "221234567", ""),
        new("PE", "Peru", "51", ["### ### ###"], "912345678"),
        new("GB", "United Kingdom", "44", ["#### ######"], "7400123456"),
        new("IE", "Ireland", "353", ["## ### ####"], "850123456"),
        new("FR", "France", "33", ["# ## ## ## ##"], "612345678"),
        new("DE", "Germany", "49", ["### #######", "### ########"], "15123456789"),
        new("ES", "Spain", "34", ["### ## ## ##"], "612345678", ""),
        new("IT", "Italy", "39", ["### ### ###", "### ### ####"], "3123456789", ""),
        new("PT", "Portugal", "351", ["### ### ###"], "912345678", ""),
        new("NL", "Netherlands", "31", ["# ########"], "612345678"),
        new("BE", "Belgium", "32", ["### ## ## ##"], "470123456"),
        new("CH", "Switzerland", "41", ["## ### ## ##"], "781234567"),
        new("AT", "Austria", "43", ["### #######", "### ########"], "6641234567"),
        new("SE", "Sweden", "46", ["## ### ## ##"], "701234567"),
        new("NO", "Norway", "47", ["### ## ###"], "40612345", ""),
        new("DK", "Denmark", "45", ["## ## ## ##"], "32123456", ""),
        new("FI", "Finland", "358", ["## ### ####", "## ### #####"], "412345678"),
        new("PL", "Poland", "48", ["### ### ###"], "512345678", ""),
        new("CZ", "Czechia", "420", ["### ### ###"], "601123456", ""),
        new("GR", "Greece", "30", ["### ### ####"], "6912345678", ""),
        new("UA", "Ukraine", "380", ["## ### ## ##"], "501234567"),
        new("TR", "Turkey", "90", ["### ### ## ##"], "5012345678"),
        new("IL", "Israel", "972", ["##-###-####"], "502345678"),
        new("AE", "United Arab Emirates", "971", ["## ### ####"], "501234567"),
        new("SA", "Saudi Arabia", "966", ["## ### ####"], "512345678"),
        new("EG", "Egypt", "20", ["### ### ####"], "1001234567"),
        new("NG", "Nigeria", "234", ["### ### ####"], "8021234567"),
        new("KE", "Kenya", "254", ["### ######"], "712123456"),
        new("ZA", "South Africa", "27", ["## ### ####"], "711234567"),
        new("IN", "India", "91", ["#####-#####"], "8123456789"),
        new("PK", "Pakistan", "92", ["### #######"], "3012345678"),
        new("CN", "China", "86", ["### #### ####"], "13123456789"),
        new("JP", "Japan", "81", ["##-####-####"], "9012345678"),
        new("KR", "South Korea", "82", ["##-###-####", "##-####-####"], "1020000000"),
        new("TW", "Taiwan", "886", ["### ### ###"], "912345678"),
        new("HK", "Hong Kong", "852", ["#### ####"], "51234567", ""),
        new("SG", "Singapore", "65", ["#### ####"], "81234567", ""),
        new("PH", "Philippines", "63", ["### ### ####"], "9051234567"),
        new("ID", "Indonesia", "62", ["###-###-####", "###-####-####", "###-####-#####"], "81234567890"),
        new("TH", "Thailand", "66", ["## ### ####"], "812345678"),
        new("VN", "Vietnam", "84", ["## ### ## ##"], "912345678"),
        new("AU", "Australia", "61", ["### ### ###"], "412345678"),
        new("NZ", "New Zealand", "64", ["# ### ####", "## ### ####", "## #### ####"], "211234567"),
    ];

    public static PhoneCountry? ByIso(string? iso) => Countries.FirstOrDefault(entry => string.Equals(entry.Iso, iso, StringComparison.OrdinalIgnoreCase));

    public static int Capacity(string pattern) => pattern.Count(c => c == '#');

    public static string OnlyDigits(string text) => new(text.Where(char.IsAsciiDigit).ToArray());

    /// <summary>Regional indicator letters, as the source draws them. WPF renders no color emoji, so the picker shows the ISO letters instead.</summary>
    public static string FlagOf(string iso) => string.Concat(iso.ToUpperInvariant().Select(c => char.ConvertFromUtf32(0x1F1E6 + c - 'A')));

    public static (string Trunk, string National) SplitTrunk(PhoneCountry entry, string digits) =>
        entry.Trunk.Length > 0 && digits.StartsWith(entry.Trunk, StringComparison.Ordinal) ? (entry.Trunk, digits[entry.Trunk.Length..]) : ("", digits);

    /// <summary>A typed trunk prefix earns its own room; without one the longest valid length is the cap.</summary>
    public static string CapDigits(PhoneCountry entry, string digits) => digits[..Math.Min(digits.Length, SplitTrunk(entry, digits).Trunk.Length + entry.Lengths.Max())];

    /// <summary>Separators only print while digits follow, so a half typed number never ends in a dangling ") " or "-".</summary>
    public static string ApplyPattern(string digits, string pattern)
    {
        var output = new System.Text.StringBuilder();
        int index = 0;
        foreach (char c in pattern)
        {
            if (index >= digits.Length) break;
            output.Append(c == '#' ? digits[index++] : c);
        }
        return output + digits[index..];
    }

    private static string PatternFor(PhoneCountry entry, int length) => entry.Patterns.FirstOrDefault(pattern => Capacity(pattern) >= length) ?? entry.Patterns[^1];

    /// <summary>Formats national digits as typed, keeping a typed trunk prefix: "07911123456" in the UK reads "07911 123456".</summary>
    public static string FormatNational(PhoneCountry entry, string digits)
    {
        var (trunk, rest) = SplitTrunk(entry, digits);
        string pattern = PatternFor(entry, rest.Length);
        string body = ApplyPattern(rest, pattern);
        if (trunk.Length == 0) return body;
        if (rest.Length == 0) return trunk;
        return trunk + (pattern.StartsWith('#') ? "" : " ") + body;
    }

    public static PhoneStatus StatusOf(PhoneCountry entry, string digits)
    {
        int length = SplitTrunk(entry, digits).National.Length;
        var lengths = entry.Lengths;
        if (length == 0) return PhoneStatus.Empty;
        if (lengths.Contains(length)) return PhoneStatus.Valid;
        return length > lengths.Max() ? PhoneStatus.TooLong : PhoneStatus.Incomplete;
    }

    public static string ToE164(PhoneCountry entry, string digits)
    {
        string rest = SplitTrunk(entry, digits).National;
        return rest.Length > 0 ? $"+{entry.Dial}{rest}" : "";
    }

    /// <summary>Reads "+44 (0)7911 123456", "0044 7911…", or "+14165550123" into a country and national digits. Longest calling code wins.</summary>
    public static (PhoneCountry Country, string National)? Parse(string input, IReadOnlyList<PhoneCountry>? pool = null)
    {
        pool ??= Countries;
        string trimmed = input.Trim();
        string digits = OnlyDigits(trimmed);
        if (!trimmed.StartsWith('+'))
        {
            if (!trimmed.StartsWith("00", StringComparison.Ordinal)) return null;
            digits = digits[2..];
        }
        foreach (int size in new[] { 3, 2, 1 })
        {
            if (digits.Length < size) continue;
            string dial = digits[..size];
            var matches = pool.Where(entry => entry.Dial == dial).ToList();
            if (matches.Count == 0) continue;
            string rest = digits[size..];
            var entry = matches.FirstOrDefault(item => item.AreaCodes?.Any(code => rest.StartsWith(code, StringComparison.Ordinal)) == true) ?? matches.FirstOrDefault(item => item.AreaCodes is null) ?? matches[0];
            // "+44 (0)7911…" carries a trunk zero it should not; drop it when the rest is still a full number without it.
            if (entry.Trunk.Length > 0 && rest.StartsWith(entry.Trunk, StringComparison.Ordinal) && rest.Length - entry.Trunk.Length >= entry.Lengths.Min()) rest = rest[entry.Trunk.Length..];
            return (entry, CapDigits(entry, rest));
        }
        return null;
    }

    /// <summary>Formats an E.164 number for display, such as "+44 7400 123456". Returns the input when it cannot be read.</summary>
    public static string Format(string e164) => Parse(e164) is { } parsed ? $"+{parsed.Country.Dial} {FormatNational(parsed.Country, parsed.National)}" : e164;

    /// <summary>The rest of the example number, in the shape this country expects, drawn faintly after what was typed.</summary>
    public static string Guide(PhoneCountry entry, string digits)
    {
        var (trunk, rest) = SplitTrunk(entry, digits);
        if (rest.Length >= entry.Example.Length) return "";
        string formatted = FormatNational(entry, digits);
        string full = FormatNational(entry, trunk + rest + entry.Example[rest.Length..]);
        return full.StartsWith(formatted, StringComparison.Ordinal) ? full[formatted.Length..] : "";
    }

    /// <summary>"Numbers in Germany have 10 or 11 digits".</summary>
    public static string LengthCopy(PhoneCountry entry)
    {
        var lengths = entry.Lengths;
        return lengths.Count == 1 ? $"{lengths[0]}" : $"{string.Join(", ", lengths.Take(lengths.Count - 1))} or {lengths[^1]}";
    }

    public static bool Matches(PhoneCountry entry, string needle)
    {
        if (needle.Length == 0) return true;
        string digits = OnlyDigits(needle);
        if (digits.Length > 0 && Regex.IsMatch(needle, @"^[+\d\s()-]+$")) return entry.Dial.StartsWith(digits, StringComparison.Ordinal) || digits.StartsWith(entry.Dial, StringComparison.Ordinal);
        if (needle.Replace("+", "").Length == 0) return true;
        return entry.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) || entry.Iso.Equals(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Position in the formatted text just after the nth digit.</summary>
    public static int CaretAfterDigits(string text, int count)
    {
        if (count <= 0) { int first = text.IndexOfAny("0123456789".ToCharArray()); return first < 0 ? text.Length : first; }
        int seen = 0;
        for (int index = 0; index < text.Length; index++) if (char.IsAsciiDigit(text[index]) && ++seen == count) return index + 1;
        return text.Length;
    }
}
