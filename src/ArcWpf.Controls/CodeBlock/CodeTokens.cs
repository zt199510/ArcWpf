using System.Text.RegularExpressions;

namespace ArcWpf.Controls;

// code-block.tsx highlight(): the token pattern and the color each token takes. Shared with DiffView.
internal static class CodeTokens
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "async", "await", "break", "case", "catch", "class", "const", "continue", "default", "delete", "do", "else", "enum", "export", "extends", "finally", "for", "from", "function", "get", "if", "implements", "import", "in", "instanceof", "interface", "let", "new", "of", "private", "protected", "public", "readonly", "return", "set", "static", "switch", "throw", "try", "type", "typeof", "var", "void", "while", "with", "yield", "true", "false", "null", "undefined", "NaN", "Infinity", "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE"
    };
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "Array", "Boolean", "Date", "Error", "Map", "Number", "Promise", "Record", "Set", "String", "ReactNode", "HTMLElement", "HTMLButtonElement", "Event", "unknown", "never", "void", "any", "boolean", "number", "string", "object"
    };
    private static readonly HashSet<char> Punctuation = new("{}[]();,.<>:=+*/!?|&-".ToCharArray());
    public static readonly Regex Pattern = new(@"/\*[\s\S]*?\*/|//[^\n]*|<!--[\s\S]*?-->|`(?:\\.|[^`\\])*`|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|\b\d+(?:\.\d+)?\b|\b[A-Za-z_$][\w$-]*\b|[{}[\]();,.<>:=+*/!?|&-]", RegexOptions.CultureInvariant);

    public static string? Key(string value, string source, int index, string language)
    {
        if (value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/*", StringComparison.Ordinal) || value.StartsWith("<!--", StringComparison.Ordinal)) return "ArcWpf.Brush.TextMuted";
        string after = source[(index + value.Length)..];
        if (value[0] is '"' or '\'' or '`') return language == "json" && after.TrimStart().StartsWith(':') ? "ArcWpf.Brush.CodeProperty" : "ArcWpf.Brush.CodeString";
        if (char.IsAsciiDigit(value[0])) return "ArcWpf.Brush.CodeNumber";
        if (Keywords.Contains(value)) return "ArcWpf.Brush.CodeKeyword";
        if (Types.Contains(value)) return "ArcWpf.Brush.CodeType";
        if (value.Length == 1 && Punctuation.Contains(value[0])) return "ArcWpf.Brush.TextSecondary";
        string before = source[..index];
        if (language is "tsx" or "jsx" or "html" or "vue" && (before.EndsWith('<') || before.EndsWith("</", StringComparison.Ordinal))) return "ArcWpf.Brush.CodeType";
        if (after.TrimStart().StartsWith('(') && language != "json") return "ArcWpf.Brush.CodeFunction";
        if (language is "css" or "tsx" or "jsx" && after.TrimStart() is [':' or '=', ..]) return "ArcWpf.Brush.CodeProperty";
        return null;
    }
}
