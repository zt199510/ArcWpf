using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ArcWpf.Controls;

/// <summary>A node of the small document tree behind the editor's output: an element, or text when <see cref="Tag"/> is "#text".</summary>
internal sealed class RichNode(string tag)
{
    public string Tag { get; set; } = tag;
    public string Text { get; set; } = "";
    public string? Href { get; set; }
    public List<RichNode> Children { get; } = [];
    public bool IsText => Tag == "#text";
    public static RichNode OfText(string text) => new("#text") { Text = text };
    public static RichNode Of(string tag, params RichNode[] children) { var node = new RichNode(tag); node.Children.AddRange(children); return node; }
    // textContent
    public string Content => IsText ? Text : string.Concat(Children.Select(child => child.Content));
    public RichNode Clone()
    {
        var copy = new RichNode(Tag) { Text = Text, Href = Href };
        copy.Children.AddRange(Children.Select(child => child.Clone()));
        return copy;
    }
}

/// <summary>Markdown and HTML converters for the subset the editor writes, the same small rules as rich-text-editor.tsx.</summary>
public static class RichTextMarkup
{
    private const string ZWSP = "\u200b";
    private static readonly HashSet<string> Void = ["br", "hr", "img", "meta", "link", "input", "area", "base", "col", "embed", "source", "track", "wbr"];
    private static readonly HashSet<string> Raw = ["script", "style", "title", "textarea"];
    private static readonly Dictionary<string, string> InlineMap = new() { ["b"] = "strong", ["strong"] = "strong", ["i"] = "em", ["em"] = "em", ["s"] = "s", ["strike"] = "s", ["del"] = "s", ["code"] = "code", ["a"] = "a", ["br"] = "br" };
    private static readonly Dictionary<string, string> BlockMap = new() { ["p"] = "p", ["h1"] = "h1", ["h2"] = "h2", ["h3"] = "h3", ["h4"] = "h3", ["h5"] = "h3", ["h6"] = "h3", ["ul"] = "ul", ["ol"] = "ol", ["li"] = "li", ["blockquote"] = "blockquote", ["pre"] = "pre", ["hr"] = "hr" };
    private static readonly HashSet<string> Drop = ["script", "style", "meta", "link", "title", "template", "iframe", "object", "embed", "svg", "canvas", "img", "video", "audio", "input", "button", "select", "textarea", "head"];
    internal static readonly HashSet<string> BlockTags = ["p", "h1", "h2", "h3", "li", "pre", "blockquote", "div", "ul", "ol", "hr"];

    private static string EscapeHtml(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
    private static bool SafeUrl(string url) => Regex.IsMatch(url.Trim(), "^(https?:|mailto:|tel:|/|#)", RegexOptions.IgnoreCase);

    /// <summary>Adds a scheme to bare domains so example.com/docs becomes a working link.</summary>
    public static string NormalizeUrl(string input)
    {
        string url = input.Trim();
        if (url.Length == 0) { return ""; }
        if (Regex.IsMatch(url, @"^[a-z][a-z\d+.-]*:", RegexOptions.IgnoreCase) || url.StartsWith('/') || url.StartsWith('#')) { return url; }
        if (Regex.IsMatch(url, @"^[^\s@]+@[^\s@]+\.[^\s@]+$")) { return "mailto:" + url; }
        return "https://" + url;
    }

    private static string InlineMarkdown(string text)
    {
        var codes = new List<string>();
        string output = Regex.Replace(EscapeHtml(text), "`([^`]+)`", match => { codes.Add(match.Groups[1].Value); return $"\u0000{codes.Count - 1}\u0000"; });
        output = Regex.Replace(output, @"\[([^\]]+)\]\(([^)\s]+)\)", match => SafeUrl(match.Groups[2].Value) ? $"<a href=\"{match.Groups[2].Value}\">{match.Groups[1].Value}</a>" : match.Groups[1].Value);
        output = Regex.Replace(output, @"(\*\*|__)(?=\S)([\s\S]*?\S)\1", "<strong>$2</strong>");
        output = Regex.Replace(output, @"~~(?=\S)([\s\S]*?\S)~~", "<s>$1</s>");
        output = Regex.Replace(output, @"(^|[^\w*])\*(?=\S)([^*]*?\S)\*(?!\*)", "$1<em>$2</em>", RegexOptions.ECMAScript);
        output = Regex.Replace(output, @"(^|[^\w_])_(?=\S)([^_]*?\S)_(?![\w_])", "$1<em>$2</em>", RegexOptions.ECMAScript);
        return Regex.Replace(output, "\u0000(\\d+)\u0000", match => $"<code>{codes[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]}</code>");
    }

    private static string ListMarkdown(List<string> lines)
    {
        var html = new StringBuilder();
        var stack = new List<(int Indent, string Tag)>();
        foreach (string line in lines)
        {
            var match = Regex.Match(line, @"^(\s*)([-*+]|\d+[.)])\s+(.*)$");
            int indent = match.Groups[1].Value.Replace("\t", "  ", StringComparison.Ordinal).Length;
            string tag = Regex.IsMatch(match.Groups[2].Value, @"\d") ? "ol" : "ul";
            while (stack.Count > 1 && indent < stack[^1].Indent) { html.Append($"</li></{stack[^1].Tag}>"); stack.RemoveAt(stack.Count - 1); }
            if (stack.Count == 0 || indent > stack[^1].Indent) { html.Append($"<{tag}><li>"); stack.Add((indent, tag)); }
            else if (stack[^1].Tag != tag) { html.Append($"</li></{stack[^1].Tag}><{tag}><li>"); stack[^1] = (stack[^1].Indent, tag); }
            else { html.Append("</li><li>"); }
            html.Append(InlineMarkdown(match.Groups[3].Value));
        }
        for (int i = stack.Count - 1; i >= 0; i--) { html.Append($"</li></{stack[i].Tag}>"); }
        return html.ToString();
    }

    /// <summary>Converts Markdown to the editor's HTML: headings (three levels), lists, quotes, fenced code, rules, and inline marks.</summary>
    public static string MarkdownToHtml(string markdown)
    {
        string[] lines = Regex.Replace(markdown, @"\r\n?", "\n").Split('\n');
        var output = new List<string>();
        var paragraph = new List<string>();
        void Flush() { if (paragraph.Count > 0) { output.Add($"<p>{InlineMarkdown(string.Join(' ', paragraph))}</p>"); paragraph.Clear(); } }
        static bool IsItem(string line) => Regex.IsMatch(line, @"^\s*([-*+]|\d+[.)])\s+");
        for (int index = 0; index < lines.Length;)
        {
            string line = lines[index];
            if (Regex.IsMatch(line, @"^\s*```"))
            {
                Flush();
                var code = new List<string>();
                index++;
                while (index < lines.Length && !Regex.IsMatch(lines[index], @"^\s*```")) { code.Add(lines[index++]); }
                index++;
                string body = EscapeHtml(string.Join('\n', code));
                output.Add($"<pre>{(body.Length > 0 ? body : "<br>")}</pre>");
                continue;
            }
            if (line.Trim().Length == 0) { Flush(); index++; continue; }
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (heading.Success)
            {
                Flush();
                int level = Math.Min(3, heading.Groups[1].Length);
                output.Add($"<h{level}>{InlineMarkdown(heading.Groups[2].Value)}</h{level}>");
                index++;
                continue;
            }
            if (Regex.IsMatch(line, @"^ {0,3}([-*_])( *\1){2,} *$")) { Flush(); output.Add("<hr>"); index++; continue; }
            if (Regex.IsMatch(line, @"^\s*>"))
            {
                Flush();
                var quote = new List<string>();
                while (index < lines.Length && Regex.IsMatch(lines[index], @"^\s*>")) { quote.Add(InlineMarkdown(Regex.Replace(lines[index++], @"^\s*>\s?", ""))); }
                output.Add($"<blockquote>{string.Join("<br>", quote)}</blockquote>");
                continue;
            }
            if (IsItem(line))
            {
                Flush();
                var items = new List<string>();
                while (index < lines.Length && IsItem(lines[index])) { items.Add(lines[index++]); }
                output.Add(ListMarkdown(items));
                continue;
            }
            paragraph.Add(line.Trim());
            index++;
        }
        Flush();
        return output.Count > 0 ? string.Concat(output) : "<p><br></p>";
    }

    private static string WrapMark(string inner, string mark)
    {
        var match = Regex.Match(inner, @"^(\s*)([\s\S]*?)(\s*)$");
        return match.Groups[2].Length > 0 ? $"{match.Groups[1].Value}{mark}{match.Groups[2].Value}{mark}{match.Groups[3].Value}" : inner;
    }

    private static string InlineToMarkdown(RichNode node)
    {
        var output = new StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.IsText) { output.Append(child.Text.Replace(ZWSP, "", StringComparison.Ordinal)); continue; }
            switch (child.Tag)
            {
                case "br": output.Append('\n'); break;
                case "strong": case "b": output.Append(WrapMark(InlineToMarkdown(child), "**")); break;
                case "em": case "i": output.Append(WrapMark(InlineToMarkdown(child), "_")); break;
                case "s": case "strike": case "del": output.Append(WrapMark(InlineToMarkdown(child), "~~")); break;
                case "code": output.Append('`').Append(child.Content.Replace(ZWSP, "", StringComparison.Ordinal)).Append('`'); break;
                case "a": output.Append('[').Append(InlineToMarkdown(child)).Append("](").Append(child.Href ?? "").Append(')'); break;
                case "ul": case "ol": break;
                default: output.Append(InlineToMarkdown(child)); break;
            }
        }
        return output.ToString();
    }

    private static string ListToMarkdown(RichNode list, string indent)
    {
        int number = 1;
        return string.Join('\n', list.Children.Where(item => item.Tag == "li").Select(item =>
        {
            string marker = list.Tag == "ol" ? $"{number++}. " : "- ";
            var nested = item.Children.Where(child => child.Tag is "ul" or "ol").Select(child => ListToMarkdown(child, indent + new string(' ', marker.Length)));
            return string.Join('\n', new[] { $"{indent}{marker}{InlineToMarkdown(item).Trim()}" }.Concat(nested));
        }));
    }

    private static string BlockToMarkdown(RichNode node)
    {
        if (node.IsText) { return node.Text.Replace(ZWSP, "", StringComparison.Ordinal).Trim(); }
        switch (node.Tag)
        {
            case "h1": return "# " + InlineToMarkdown(node).Trim();
            case "h2": return "## " + InlineToMarkdown(node).Trim();
            case "h3": return "### " + InlineToMarkdown(node).Trim();
            case "hr": return "---";
            case "pre": return $"```\n{Regex.Replace(node.Content.Replace(ZWSP, "", StringComparison.Ordinal), @"\n$", "")}\n```";
            case "ul": case "ol": return ListToMarkdown(node, "");
            case "blockquote":
            {
                bool blocks = node.Children.Any(child => !child.IsText && BlockTags.Contains(child.Tag));
                string body = blocks ? string.Join("\n\n", node.Children.Select(BlockToMarkdown).Where(part => part.Length > 0)) : InlineToMarkdown(node).Trim();
                return string.Join('\n', body.Split('\n').Select(line => line.Length > 0 ? "> " + line : ">"));
            }
            default: return InlineToMarkdown(node).Trim();
        }
    }

    internal static string TreeToMarkdown(RichNode root) => string.Join("\n\n", root.Children.Select(BlockToMarkdown).Where(part => part.Length > 0));

    /// <summary>Converts editor HTML to Markdown.</summary>
    public static string HtmlToMarkdown(string html) => TreeToMarkdown(Parse(html));

    // ---- A tolerant HTML reader, enough for pasted fragments and the editor's own output ----

    internal static RichNode Parse(string html)
    {
        var root = new RichNode("#root");
        var stack = new List<RichNode> { root };
        int index = 0;
        void AddText(string raw)
        {
            if (raw.Length == 0) { return; }
            var parent = stack[^1];
            string text = WebUtility.HtmlDecode(raw);
            if (parent.Children.Count > 0 && parent.Children[^1].IsText) { parent.Children[^1].Text += text; }
            else { parent.Children.Add(RichNode.OfText(text)); }
        }
        void CloseTo(string tag)
        {
            int at = stack.FindLastIndex(node => node.Tag == tag);
            if (at > 0) { stack.RemoveRange(at, stack.Count - at); }
        }
        while (index < html.Length)
        {
            int open = html.IndexOf('<', index);
            if (open < 0) { AddText(html[index..]); break; }
            AddText(html[index..open]);
            if (string.CompareOrdinal(html, open, "<!--", 0, 4) == 0)
            {
                int end = html.IndexOf("-->", open, StringComparison.Ordinal);
                index = end < 0 ? html.Length : end + 3;
                continue;
            }
            int close = TagEnd(html, open);
            if (close < 0) { AddText(html[open..]); break; }
            string inner = html[(open + 1)..close];
            index = close + 1;
            if (inner.StartsWith('!') || inner.StartsWith('?')) { continue; }
            if (inner.StartsWith('/'))
            {
                CloseTo(inner[1..].Trim().ToLowerInvariant());
                continue;
            }
            var nameMatch = Regex.Match(inner, @"^[a-zA-Z][\w:-]*");
            if (!nameMatch.Success) { AddText(html[open..(close + 1)]); continue; }
            string name = nameMatch.Value.ToLowerInvariant();
            // A new block closes an open paragraph, and a list item closes the last one, as an HTML parser does.
            if (BlockMap.ContainsKey(name) && name != "li" && stack.Exists(node => node.Tag == "p")) { CloseTo("p"); }
            if (name == "li" && stack[^1].Tag != "ul" && stack[^1].Tag != "ol")
            {
                int item = stack.FindLastIndex(node => node.Tag == "li");
                int list = stack.FindLastIndex(node => node.Tag is "ul" or "ol");
                if (item > list && item > 0) { stack.RemoveRange(item, stack.Count - item); }
            }
            var element = new RichNode(name);
            string attributes = inner[nameMatch.Length..];
            var href = Regex.Match(attributes, @"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (href.Success) { element.Href = WebUtility.HtmlDecode(href.Groups[1].Success ? href.Groups[1].Value : href.Groups[2].Success ? href.Groups[2].Value : href.Groups[3].Value); }
            var style = Regex.Match(attributes, @"\bstyle\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
            if (style.Success) { element.Text = style.Groups[1].Success ? style.Groups[1].Value : style.Groups[2].Value; }
            stack[^1].Children.Add(element);
            if (Raw.Contains(name))
            {
                int end = html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase);
                if (end < 0) { index = html.Length; continue; }
                element.Children.Add(RichNode.OfText(html[index..end]));
                int after = html.IndexOf('>', end);
                index = after < 0 ? html.Length : after + 1;
                continue;
            }
            if (!Void.Contains(name) && !inner.EndsWith('/')) { stack.Add(element); }
        }
        return root;
    }

    private static int TagEnd(string html, int open)
    {
        char quote = '\0';
        for (int i = open + 1; i < html.Length; i++)
        {
            char c = html[i];
            if (quote != '\0') { if (c == quote) { quote = '\0'; } }
            else if (c is '"' or '\'') { quote = c; }
            else if (c == '>') { return i; }
        }
        return -1;
    }

    private static string StyleOf(RichNode element) => element.Text;

    /// <summary>Keeps only the tags the editor understands and drops every attribute except safe link targets. Bold and italic spans from other editors become marks.</summary>
    public static string SanitizeHtml(string html) => Write(SanitizeTree(html));

    internal static RichNode SanitizeTree(string html)
    {
        var source = Parse(html);
        var bodyNode = FindFirst(source, "body");
        var output = new RichNode("#root");
        Convert(bodyNode ?? source, output);

        static void Convert(RichNode from, RichNode into)
        {
            foreach (var child in from.Children)
            {
                if (child.IsText) { into.Children.Add(RichNode.OfText(child.Text)); continue; }
                if (Drop.Contains(child.Tag)) { continue; }
                string style = StyleOf(child);
                string tag = InlineMap.GetValueOrDefault(child.Tag) ?? BlockMap.GetValueOrDefault(child.Tag) ?? "";
                if (child.Tag == "b" && Regex.IsMatch(style, @"font-weight:\s*(normal|[1-4]00)")) { tag = ""; }
                if (child.Tag == "span") { tag = Regex.IsMatch(style, @"font-weight:\s*(bold|[6-9]00)") ? "strong" : Regex.IsMatch(style, @"font-style:\s*italic") ? "em" : style.Contains("line-through", StringComparison.Ordinal) ? "s" : ""; }
                if (tag.Length == 0) { Convert(child, into); continue; }
                var next = new RichNode(tag);
                if (tag == "a")
                {
                    string href = child.Href ?? "";
                    if (!SafeUrl(href)) { Convert(child, into); continue; }
                    next.Href = href;
                }
                if (tag == "pre") { next.Children.Add(RichNode.OfText(PreText(child))); }
                else if (tag is not "br" and not "hr") { Convert(child, next); }
                into.Children.Add(next);
            }
        }

        // Inline runs at the top level become paragraphs; whitespace between blocks disappears.
        var fixedRoot = new RichNode("#root");
        RichNode? run = null;
        foreach (var node in output.Children)
        {
            if (!node.IsText && BlockTags.Contains(node.Tag)) { run = null; fixedRoot.Children.Add(node); continue; }
            if (node.IsText && node.Text.Trim().Length == 0 && run is null) { continue; }
            if (run is null) { run = new RichNode("p"); fixedRoot.Children.Add(run); }
            run.Children.Add(node);
        }
        Unwrap(fixedRoot);
        Strip(fixedRoot);
        return fixedRoot;

        static void Unwrap(RichNode parent)
        {
            for (int i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                if (parent.Tag == "li" && child.Tag == "p")
                {
                    parent.Children.RemoveAt(i);
                    parent.Children.InsertRange(i, child.Children);
                    i--;
                    continue;
                }
                Unwrap(child);
            }
        }

        // The editor keeps typed whitespace, so formatting whitespace between blocks and list items has to go.
        static void Strip(RichNode parent)
        {
            bool container = parent.Tag is "#root" or "ul" or "ol" or "blockquote" or "li";
            for (int i = parent.Children.Count - 1; container && i >= 0; i--)
            {
                var node = parent.Children[i];
                if (!node.IsText || node.Text.Trim().Length > 0) { continue; }
                var previous = i > 0 ? parent.Children[i - 1] : null;
                var next = i + 1 < parent.Children.Count ? parent.Children[i + 1] : null;
                bool nearBlock = (previous is { IsText: false } && BlockTags.Contains(previous.Tag)) || (next is { IsText: false } && BlockTags.Contains(next.Tag));
                if (parent.Tag is "#root" or "ul" or "ol" || nearBlock) { parent.Children.RemoveAt(i); }
            }
            foreach (var child in parent.Children) { Strip(child); }
        }
    }

    internal static string PreText(RichNode node)
    {
        var text = new StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.IsText) { text.Append(child.Text); }
            else if (child.Tag == "br") { text.Append('\n'); }
            else { text.Append(PreText(child)); }
        }
        return text.ToString();
    }

    private static RichNode? FindFirst(RichNode node, string tag)
    {
        foreach (var child in node.Children)
        {
            if (child.Tag == tag) { return child; }
            if (FindFirst(child, tag) is { } found) { return found; }
        }
        return null;
    }

    // innerHTML of a tree: text escapes &, < and >, attributes also quotes.
    internal static string Write(RichNode root)
    {
        var html = new StringBuilder();
        foreach (var child in root.Children) { WriteNode(child, html); }
        return html.ToString();
    }

    private static void WriteNode(RichNode node, StringBuilder html)
    {
        if (node.IsText)
        {
            html.Append(node.Text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\u00a0", "&nbsp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal));
            return;
        }
        html.Append('<').Append(node.Tag);
        if (node.Href is not null) { html.Append(" href=\"").Append(node.Href.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal)).Append('"'); }
        html.Append('>');
        if (node.Tag is "br" or "hr") { return; }
        foreach (var child in node.Children) { WriteNode(child, html); }
        html.Append("</").Append(node.Tag).Append('>');
    }

    private static string VisibleText(RichNode node) => node.Content.Replace(ZWSP, "", StringComparison.Ordinal);

    // A clean copy for output: no invisible helpers, no trailing empty lines.
    internal static RichNode CleanClone(RichNode root)
    {
        var clone = root.Clone();
        Clean(clone);
        while (clone.Children.Count > 1 && clone.Children[^1].Tag != "hr" && VisibleText(clone.Children[^1]).Length == 0) { clone.Children.RemoveAt(clone.Children.Count - 1); }
        return clone;

        static void Clean(RichNode node)
        {
            foreach (var child in node.Children) { if (child.IsText) { child.Text = child.Text.Replace(ZWSP, "", StringComparison.Ordinal); } else { Clean(child); } }
            node.Children.RemoveAll(child => child.IsText && child.Text.Length == 0);
            if (node.Tag is "p" or "h1" or "h2" or "h3" or "li" or "blockquote" && node.Children.Count > 0 && node.Children[^1].Tag == "br" && VisibleText(node).Length > 0) { node.Children.RemoveAt(node.Children.Count - 1); }
            if (node.Tag == "pre")
            {
                string text = Regex.Replace(node.Content, @"\n$", "");
                node.Children.Clear();
                if (text.Length > 0) { node.Children.Add(RichNode.OfText(text)); }
            }
        }
    }

    internal static string PlainOutput(RichNode clone)
    {
        var lines = new List<string>();
        Walk(clone);
        return string.Join('\n', lines);

        void Walk(RichNode node)
        {
            foreach (var child in node.Children)
            {
                if (child.IsText) { continue; }
                if (child.Tag is "p" or "h1" or "h2" or "h3" or "li" or "blockquote" or "pre" && !HasDescendant(child))
                {
                    string text = child.Content.Trim();
                    if (text.Length > 0) { lines.Add(text); }
                }
                Walk(child);
            }
        }
        static bool HasDescendant(RichNode node) => node.Children.Any(child => child.Tag is "p" or "li" || HasDescendant(child));
    }
}
