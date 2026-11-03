using FlowItem = System.Windows.Documents.ListItem;
using FlowList = System.Windows.Documents.List;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcWpf.Controls;

// The block operations of rich-text-editor.tsx, on the node tree instead of the DOM.
internal static class RichTextOps
{
    private const string ZWSP = "\u200b";
    public static bool IsList(RichNode node) => node.Tag is "ul" or "ol";
    public static string Visible(RichNode node) => node.Content.Replace(ZWSP, "", StringComparison.Ordinal);
    public static RichNode Empty(string tag) => RichNode.Of(tag, new RichNode("br"));

    public static RichNode? Parent(RichNode root, RichNode node)
    {
        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, node)) { return root; }
            if (Parent(child, node) is { } found) { return found; }
        }
        return null;
    }

    public static void InsertAfter(RichNode root, RichNode anchor, RichNode node)
    {
        var parent = Parent(root, anchor)!;
        parent.Children.Insert(parent.Children.IndexOf(anchor) + 1, node);
    }

    public static void Replace(RichNode root, RichNode old, RichNode next)
    {
        var parent = Parent(root, old)!;
        parent.Children[parent.Children.IndexOf(old)] = next;
    }

    public static void EnsureFilled(RichNode node)
    {
        if (node.Tag == "hr") { return; }
        if (Visible(node).Length == 0 && !Contains(node, "br", "hr", "ul", "ol")) { node.Children.Add(new RichNode("br")); }
    }

    public static bool Contains(RichNode node, params string[] tags) => node.Children.Any(child => tags.Contains(child.Tag) || Contains(child, tags));

    public static int Length(RichNode node) => node.IsText ? node.Text.Length : node.Tag == "br" ? 1 : IsList(node) ? 0 : node.Children.Sum(Length);

    // Cuts inline content at a character offset. Lists nested in an item always go with the part after the caret.
    public static (List<RichNode> Head, List<RichNode> Tail) Split(IEnumerable<RichNode> nodes, int offset)
    {
        var head = new List<RichNode>();
        var tail = new List<RichNode>();
        int left = offset;
        bool done = false;
        foreach (var node in nodes)
        {
            if (done) { tail.Add(node); continue; }
            if (IsList(node) || left == 0) { done = true; tail.Add(node); continue; }
            int length = Length(node);
            if (left >= length) { head.Add(node); left -= length; continue; }
            done = true;
            if (node.IsText)
            {
                head.Add(RichNode.OfText(node.Text[..left]));
                tail.Add(RichNode.OfText(node.Text[left..]));
                continue;
            }
            var (innerHead, innerTail) = Split(node.Children, left);
            var before = new RichNode(node.Tag) { Href = node.Href };
            before.Children.AddRange(innerHead);
            var after = new RichNode(node.Tag) { Href = node.Href };
            after.Children.AddRange(innerTail);
            head.Add(before);
            tail.Add(after);
        }
        return (head, tail);
    }

    private static void SetChildren(RichNode node, IEnumerable<RichNode> children)
    {
        var list = children.ToList();
        node.Children.Clear();
        node.Children.AddRange(list);
    }

    // Plain text of a block, with line breaks for <br>, for moving content into a code block.
    public static string PlainText(RichNode node)
    {
        var text = new System.Text.StringBuilder();
        Walk(node);
        return text.ToString().TrimEndOnce('\n');

        void Walk(RichNode current)
        {
            foreach (var child in current.Children)
            {
                if (child.IsText) { text.Append(child.Text.Replace(ZWSP, "", StringComparison.Ordinal)); }
                else if (child.Tag == "br") { text.Append('\n'); }
                else { Walk(child); }
            }
        }
    }

    private static string TrimEndOnce(this string value, char c) => value.EndsWith(c) ? value[..^1] : value;

    private static List<RichNode> LinesToNodes(string text)
    {
        var nodes = new List<RichNode>();
        string[] lines = text.TrimEndOnce('\n').Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (index > 0) { nodes.Add(new RichNode("br")); }
            if (lines[index].Length > 0) { nodes.Add(RichNode.OfText(lines[index])); }
        }
        return nodes;
    }

    private static RichNode MergeAdjacentLists(RichNode root, RichNode list)
    {
        var parent = Parent(root, list)!;
        int at = parent.Children.IndexOf(list);
        if (at + 1 < parent.Children.Count && parent.Children[at + 1].Tag == list.Tag)
        {
            list.Children.AddRange(parent.Children[at + 1].Children);
            parent.Children.RemoveAt(at + 1);
        }
        if (at > 0 && parent.Children[at - 1].Tag == list.Tag)
        {
            parent.Children[at - 1].Children.AddRange(list.Children);
            parent.Children.RemoveAt(at);
            return parent.Children[at - 1];
        }
        return list;
    }

    // Moves a list item one level out. A top level item leaves the list and becomes a paragraph between its halves.
    public static RichNode Outdent(RichNode root, RichNode item)
    {
        var list = Parent(root, item)!;
        var parentItem = Parent(root, list);
        int at = list.Children.IndexOf(item);
        var following = list.Children.Skip(at + 1).ToList();
        if (parentItem?.Tag == "li")
        {
            list.Children.RemoveRange(at, list.Children.Count - at);
            if (following.Count > 0)
            {
                var sub = new RichNode(list.Tag);
                sub.Children.AddRange(following);
                item.Children.Add(sub);
            }
            InsertAfter(root, parentItem, item);
            if (list.Children.Count == 0) { parentItem.Children.Remove(list); }
            return item;
        }
        var paragraph = new RichNode("p");
        var nested = new List<RichNode>();
        foreach (var child in item.Children)
        {
            if (IsList(child)) { nested.Add(child); } else { paragraph.Children.Add(child); }
        }
        EnsureFilled(paragraph);
        RichNode? tail = null;
        if (following.Count > 0 || nested.Count > 0)
        {
            tail = new RichNode(list.Tag);
            foreach (var sub in nested) { tail.Children.AddRange(sub.Children); }
            tail.Children.AddRange(following);
        }
        InsertAfter(root, list, paragraph);
        if (tail is not null) { InsertAfter(root, paragraph, tail); }
        list.Children.RemoveRange(at, list.Children.Count - at);
        if (list.Children.Count == 0) { Parent(root, list)!.Children.Remove(list); }
        return paragraph;
    }

    public static bool Indent(RichNode root, RichNode item)
    {
        var list = Parent(root, item)!;
        int at = list.Children.IndexOf(item);
        if (at == 0 || list.Children[at - 1].Tag != "li") { return false; }
        var previous = list.Children[at - 1];
        var sub = previous.Children.Count > 0 && IsList(previous.Children[^1]) ? previous.Children[^1] : null;
        if (sub is null)
        {
            sub = new RichNode(list.Tag);
            previous.Children.Add(sub);
        }
        list.Children.RemoveAt(at);
        sub.Children.Add(item);
        return true;
    }

    // Turns one block into another type, keeping its content. Choosing the type a block already has turns it back into a paragraph.
    public static RichNode Convert(RichNode root, RichNode block, string type)
    {
        if (block.Tag == "li")
        {
            var list = Parent(root, block)!;
            if (type is "ul" or "ol")
            {
                if (list.Tag == type) { return Outdent(root, block); }
                list.Tag = type;
                return block;
            }
            var paragraph = Outdent(root, block);
            return paragraph.Tag == "p" && type != "p" ? Convert(root, paragraph, type) : paragraph;
        }
        if (type is "ul" or "ol")
        {
            var item = new RichNode("li");
            if (block.Tag == "pre") { item.Children.AddRange(LinesToNodes(block.Content)); } else { item.Children.AddRange(block.Children); }
            EnsureFilled(item);
            var list = RichNode.Of(type, item);
            Replace(root, block, list);
            MergeAdjacentLists(root, list);
            return item;
        }
        string tag = block.Tag == type ? "p" : type;
        var next = new RichNode(tag);
        if (tag == "pre")
        {
            string text = PlainText(block);
            if (text.Length > 0) { next.Children.Add(RichNode.OfText(text)); }
        }
        else if (block.Tag == "pre") { next.Children.AddRange(LinesToNodes(block.Content)); }
        else { next.Children.AddRange(block.Children); }
        EnsureFilled(next);
        Replace(root, block, next);
        return next;
    }

    // Makes sure a paragraph follows a block the caret cannot type after, such as a rule or a code block at the end.
    public static RichNode ParagraphAfter(RichNode root, RichNode block)
    {
        var parent = Parent(root, block)!;
        int at = parent.Children.IndexOf(block);
        if (at + 1 < parent.Children.Count && parent.Children[at + 1].Tag == "p") { return parent.Children[at + 1]; }
        var paragraph = Empty("p");
        parent.Children.Insert(at + 1, paragraph);
        return paragraph;
    }

    // Enter. Returns the block and local offset the caret lands in.
    public static (RichNode Node, int Offset) Enter(RichNode root, RichNode block, int offset, bool soft)
    {
        if (block.Tag == "pre")
        {
            string full = block.Content;
            string head = full[..offset], tail = full[offset..];
            if (tail.TrimEndOnce('\n').Length == 0 && head.EndsWith('\n') && !soft)
            {
                SetChildren(block, head.Length > 1 ? [RichNode.OfText(head[..^1])] : []);
                EnsureFilled(block);
                var after = Empty("p");
                InsertAfter(root, block, after);
                return (after, 0);
            }
            SetChildren(block, [RichNode.OfText(head + "\n" + (tail.Length > 0 ? tail : "\n"))]);
            return (block, offset + 1);
        }

        var (before, rest) = Split(block.Children, offset);
        if (soft)
        {
            var joined = new List<RichNode>(before) { new("br") };
            if (rest.Sum(Length) == 0 && !rest.Exists(IsList)) { joined.Add(new RichNode("br")); }
            joined.AddRange(rest);
            SetChildren(block, joined);
            return (block, offset + 1);
        }

        if (block.Tag == "li" && Visible(block).Length == 0 && !Contains(block, "ul", "ol")) { return (Outdent(root, block), 0); }
        if (block.Tag == "blockquote" && Visible(block).Length == 0) { return (Convert(root, block, "p"), 0); }

        bool atEnd = string.Concat(rest.Where(node => !IsList(node)).Select(Visible)).Length == 0 && !rest.Exists(IsList);
        bool heading = block.Tag is "h1" or "h2" or "h3";
        var next = new RichNode(heading && atEnd ? "p" : block.Tag);
        if (!(heading && atEnd)) { next.Children.AddRange(rest); }
        SetChildren(block, before);
        InsertAfter(root, block, next);
        if (Visible(block).Length > 0 && block.Children.Count > 0 && block.Children[^1].Tag == "br") { block.Children.RemoveAt(block.Children.Count - 1); }
        EnsureFilled(block);
        // Formatting open at the caret carries into the new line only when there is text to carry.
        if (Visible(next).Length == 0) { next.Children.RemoveAll(child => child.Tag is "strong" or "em" or "s" or "code" or "a"); }
        EnsureFilled(next);
        return (next, 0);
    }

    public static IEnumerable<RichNode> Descendants(RichNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var inner in Descendants(child)) { yield return inner; }
        }
    }

    // The block type a node shows in the toolbar: the list tag for an item.
    public static string CurrentType(RichNode root, RichNode block) => block.Tag == "li" ? Parent(root, block)!.Tag : block.Tag;

    private static void Unwrap(List<RichNode> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Tag == "a")
            {
                var inner = nodes[i].Children;
                nodes.RemoveAt(i);
                nodes.InsertRange(i, inner);
                i--;
                continue;
            }
            Unwrap(nodes[i].Children);
        }
    }

    // The link covering a local offset, if any.
    public static RichNode? AnchorAt(RichNode block, int offset)
    {
        int left = offset;
        foreach (var child in block.Children)
        {
            int length = Length(child);
            if (child.Tag == "a" && left < length) { return child; }
            if (left < length) { return child.IsText ? null : AnchorAt(child, left); }
            left -= length;
        }
        return null;
    }

    public static void UnlinkRange(RichNode block, int from, int to)
    {
        var (head, rest) = Split(block.Children, from);
        var (middle, tail) = Split(rest, to - from);
        Unwrap(middle);
        SetChildren(block, head.Concat(middle).Concat(tail));
    }

    public static void RemoveAnchor(RichNode block, RichNode anchor)
    {
        foreach (var parent in Descendants(block).Append(block).ToList())
        {
            int at = parent.Children.IndexOf(anchor);
            if (at < 0) { continue; }
            parent.Children.RemoveAt(at);
            parent.Children.InsertRange(at, anchor.Children);
            return;
        }
    }

    public static void LinkRange(RichNode block, int from, int to, string url)
    {
        var (head, rest) = Split(block.Children, from);
        var (middle, tail) = Split(rest, to - from);
        Unwrap(middle);
        var anchor = new RichNode("a") { Href = url };
        anchor.Children.AddRange(middle);
        SetChildren(block, head.Append(anchor).Concat(tail));
    }

    public static void InsertInline(RichNode block, int offset, IEnumerable<RichNode> nodes)
    {
        var (head, tail) = Split(block.Children, offset);
        SetChildren(block, head.Concat(nodes).Concat(tail));
    }
}

// The node tree in and out of a FlowDocument. Marks live on runs as ordinary formatting, so native typing and selection keep working.
internal sealed class RichTextFlow(FrameworkElement owner)
{
    private const double BodySize = 16;
    private readonly Dictionary<RichNode, Paragraph> _byNode = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Paragraph, RichNode> _byParagraph = [];
    public RichNode Root { get; private set; } = new("#root");

    public static double SizeOf(string tag) => tag switch { "h1" => 28, "h2" => 22, "h3" => 18, "pre" => 14, _ => BodySize };
    private static double LineOf(string tag) => tag switch { "h1" => 30.8, "h2" => 24.2, "h3" => 19.8, "pre" => 22.4, _ => 26.4 };
    public FontFamily Mono => owner.TryFindResource("ArcWpf.Font.Mono") as FontFamily ?? new FontFamily("Consolas");

    public Paragraph? ParagraphOf(RichNode node) => _byNode.GetValueOrDefault(node);
    public RichNode? NodeOf(Paragraph? paragraph) => paragraph is null ? null : _byParagraph.GetValueOrDefault(paragraph);

    // ---- tree to document ----

    public void Build(FlowDocument document, RichNode root)
    {
        _byNode.Clear();
        _byParagraph.Clear();
        Root = root;
        document.Blocks.Clear();
        foreach (var node in root.Children)
        {
            if (TopBlock(node) is { } block) { document.Blocks.Add(block); }
        }
        if (document.Blocks.Count == 0)
        {
            var empty = RichTextOps.Empty("p");
            root.Children.Add(empty);
            document.Blocks.Add(MakeParagraph(empty));
        }
        ApplyMargins(document);
    }

    private Block? TopBlock(RichNode node)
    {
        if (node.Tag == "hr")
        {
            var line = new Border { Height = 1, IsHitTestVisible = false };
            line.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Border");
            return new BlockUIContainer(line) { Tag = "hr" };
        }
        if (RichTextOps.IsList(node)) { return MakeList(node, 0); }
        return MakeParagraph(node);
    }

    private FlowList MakeList(RichNode node, int depth)
    {
        // Disc at the top, circle below, the padding of ul plus the item's .2em. Markers take the list's colour, text its paragraph's.
        var list = new FlowList { Tag = node.Tag, Padding = new Thickness(26.4, 0, 0, 0), Margin = new Thickness(0), MarkerOffset = 13.2, MarkerStyle = node.Tag == "ol" ? TextMarkerStyle.Decimal : depth > 0 ? TextMarkerStyle.Circle : TextMarkerStyle.Disc };
        list.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
        foreach (var child in node.Children.Where(child => child.Tag == "li"))
        {
            var item = new FlowItem();
            item.Blocks.Add(MakeParagraph(child));
            foreach (var sub in child.Children.Where(RichTextOps.IsList)) { item.Blocks.Add(MakeList(sub, depth + 1)); }
            list.ListItems.Add(item);
        }
        return list;
    }

    private Paragraph MakeParagraph(RichNode node)
    {
        string tag = node.Tag is "h1" or "h2" or "h3" or "pre" or "blockquote" or "li" ? node.Tag : "p";
        var paragraph = new Paragraph { Tag = tag, Margin = new Thickness(0), Padding = new Thickness(0), FontSize = SizeOf(tag), LineHeight = LineOf(tag), LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextAlignment = TextAlignment.Left };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, tag == "blockquote" ? "ArcWpf.Brush.TextSecondary" : "ArcWpf.Brush.Foreground");
        if (tag is "h1" or "h2" or "h3") { paragraph.FontWeight = FontWeights.Medium; }
        if (tag == "blockquote")
        {
            paragraph.Padding = new Thickness(14, 0, 0, 0);
            paragraph.BorderThickness = new Thickness(2, 0, 0, 0);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "ArcWpf.Brush.BorderStrong");
        }
        if (tag == "pre")
        {
            // The rounded box is painted behind the text; the padding and 1 border of the source only reserve space here.
            paragraph.Padding = new Thickness(15, 13, 15, 13);
            paragraph.FontFamily = Mono;
            string text = RichTextMarkup.PreText(node);
            if (text.EndsWith('\n')) { text = text[..^1]; }
            string[] lines = text.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (index > 0) { paragraph.Inlines.Add(new LineBreak()); }
                if (lines[index].Length > 0) { paragraph.Inlines.Add(new Run(lines[index])); }
            }
        }
        else
        {
            var children = node.Children.Where(child => !RichTextOps.IsList(child)).ToList();
            // A <br> that ends a block draws no line in a browser; a LineBreak does.
            if (children.Count > 0 && children[^1].Tag == "br") { children.RemoveAt(children.Count - 1); }
            AddInlines(paragraph.Inlines, children, new Marks(), SizeOf(tag), tag);
        }
        _byNode[node] = paragraph;
        _byParagraph[paragraph] = node;
        return paragraph;
    }

    private readonly record struct Marks(bool Strong = false, bool Em = false, bool Strike = false, bool Code = false);

    private void AddInlines(InlineCollection into, IEnumerable<RichNode> nodes, Marks marks, double size, string blockTag)
    {
        foreach (var node in nodes)
        {
            if (node.IsText)
            {
                if (node.Text.Length > 0) { into.Add(MakeRun(node.Text, marks, size)); }
                continue;
            }
            switch (node.Tag)
            {
                case "br": into.Add(new LineBreak()); break;
                case "strong": AddInlines(into, node.Children, marks with { Strong = true }, size, blockTag); break;
                case "em": AddInlines(into, node.Children, marks with { Em = true }, size, blockTag); break;
                case "s": AddInlines(into, node.Children, marks with { Strike = true }, size, blockTag); break;
                case "code": AddInlines(into, node.Children, marks with { Code = true }, size, blockTag); break;
                case "a":
                {
                    var link = new Hyperlink { Tag = node.Href ?? "", TextDecorations = LinkLine(), Cursor = null };
                    link.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.AccentStrong");
                    AddInlines(link.Inlines, node.Children, marks, size, blockTag);
                    into.Add(link);
                    break;
                }
                // Blocks nested in a quote read as lines of the quote.
                case "p": case "h1": case "h2": case "h3": case "blockquote": case "li":
                    if (into.Count > 0) { into.Add(new LineBreak()); }
                    AddInlines(into, node.Children, marks, size, blockTag);
                    break;
                default: AddInlines(into, node.Children, marks, size, blockTag); break;
            }
        }
    }

    private TextDecorationCollection LinkLine()
    {
        var pen = new Pen(owner.TryFindResource("ArcWpf.Brush.EditorLinkLine") as Brush ?? Brushes.Gray, 1);
        return [new TextDecoration(TextDecorationLocation.Underline, pen, 3, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel)];
    }

    private Run MakeRun(string text, Marks marks, double size)
    {
        var run = new Run(text);
        if (marks.Strong) { run.FontWeight = FontWeights.SemiBold; }
        if (marks.Em) { run.FontStyle = FontStyles.Italic; }
        if (marks.Strike)
        {
            run.TextDecorations = [new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(owner.TryFindResource("ArcWpf.Brush.TextSecondary") as Brush ?? Brushes.Gray, 1), 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.Pixel)];
            run.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        }
        if (marks.Code)
        {
            run.FontFamily = Mono;
            run.FontSize = size * .86;
        }
        return run;
    }

    // Vertical rhythm of the stylesheet. Margins between neighbours collapse in FlowDocument as they do in CSS.
    public static void ApplyMargins(FlowDocument document)
    {
        Block? previous = null;
        foreach (var block in document.Blocks)
        {
            string tag = block.Tag as string ?? "p";
            double em = SizeOf(tag);
            // ".editor ul, .editor ol { margin-block: 0 }" outranks the sibling rules, so lists sit flush.
            double top = previous is null || block is FlowList ? 0
                : tag == "hr" ? 17.6
                : previous.Tag is "h1" or "h2" or "h3" ? .45 * em
                : tag is "h1" or "h2" or "h3" ? 1.1 * em
                // ".editor p { margin: 0 }" outranks ".editor > * + *" too; only a heading above gives a paragraph space.
                : tag == "p" ? 0
                : .6 * em;
            double bottom = tag == "hr" ? 17.6 : 0;
            Set(block, new Thickness(0, top, 0, bottom));
            if (block is FlowList list) { ApplyListMargins(list); }
            previous = block;
        }

        void ApplyListMargins(FlowList list)
        {
            bool first = true;
            foreach (var item in list.ListItems)
            {
                bool head = true;
                foreach (var inner in item.Blocks)
                {
                    if (inner is FlowList sub) { Set(sub, new Thickness(0, 3.2, 0, 0)); ApplyListMargins(sub); }
                    else { Set(inner, new Thickness(0, head && !first ? 3.2 : 0, 0, 0)); }
                    head = false;
                }
                first = false;
            }
        }

        void Set(Block block, Thickness margin) { if (block.Margin != margin) { block.Margin = margin; } }
    }

    // ---- document to tree ----

    public RichNode Read(FlowDocument document)
    {
        _byNode.Clear();
        _byParagraph.Clear();
        var root = new RichNode("#root");
        foreach (var block in document.Blocks)
        {
            if (block is BlockUIContainer) { root.Children.Add(new RichNode("hr")); }
            else if (block is FlowList list) { root.Children.Add(ReadList(list)); }
            else if (block is Paragraph paragraph) { root.Children.Add(ReadParagraph(paragraph, null)); }
        }
        if (root.Children.Count == 0) { root.Children.Add(RichTextOps.Empty("p")); }
        Root = root;
        return root;
    }

    private RichNode ReadList(FlowList list)
    {
        var node = new RichNode(list.Tag as string ?? (list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul"));
        foreach (var item in list.ListItems)
        {
            RichNode? li = null;
            foreach (var inner in item.Blocks)
            {
                if (inner is FlowList sub)
                {
                    li ??= RichNode.Of("li");
                    li.Children.Add(ReadList(sub));
                }
                else if (inner is Paragraph paragraph)
                {
                    if (li is null) { li = ReadParagraph(paragraph, "li"); }
                    else { li.Children.Add(new RichNode("br")); ReadInlines(paragraph.Inlines, li); _byParagraph[paragraph] = li; }
                }
            }
            if (li is not null) { node.Children.Add(li); }
        }
        return node;
    }

    private RichNode ReadParagraph(Paragraph paragraph, string? forced)
    {
        var node = new RichNode(forced ?? paragraph.Tag as string ?? "p");
        if (node.Tag == "li" && forced is null) { node.Tag = "p"; }
        if (node.Tag == "pre")
        {
            var text = new System.Text.StringBuilder();
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is Run run) { text.Append(run.Text); }
                else if (inline is LineBreak) { text.Append('\n'); }
            }
            if (paragraph.Inlines.LastInline is LineBreak) { text.Append('\n'); }
            if (text.Length > 0) { node.Children.Add(RichNode.OfText(text.ToString())); } else { node.Children.Add(new RichNode("br")); }
        }
        else
        {
            ReadInlines(paragraph.Inlines, node);
            if (paragraph.Inlines.LastInline is LineBreak || RichTextOps.Length(node) == 0) { node.Children.Add(new RichNode("br")); }
        }
        _byNode[node] = paragraph;
        _byParagraph[paragraph] = node;
        return node;
    }

    private void ReadInlines(InlineCollection inlines, RichNode into, string? href = null)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Hyperlink link: ReadInlines(link.Inlines, into, link.Tag as string ?? ""); break;
                case Span span: ReadInlines(span.Inlines, into, href); break;
                case LineBreak: into.Children.Add(new RichNode("br")); break;
                case Run run when run.Text.Length > 0:
                {
                    var current = into;
                    if (href is not null) { current = Open(current, "a", href); }
                    if (run.FontWeight.ToOpenTypeWeight() >= 600) { current = Open(current, "strong"); }
                    if (run.FontStyle == FontStyles.Italic) { current = Open(current, "em"); }
                    if (run.TextDecorations?.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough) == true) { current = Open(current, "s"); }
                    if (IsCode(run)) { current = Open(current, "code"); }
                    if (current.Children.Count > 0 && current.Children[^1].IsText) { current.Children[^1].Text += run.Text; }
                    else { current.Children.Add(RichNode.OfText(run.Text)); }
                    break;
                }
            }
        }
    }

    private static RichNode Open(RichNode parent, string tag, string? href = null)
    {
        if (parent.Children.Count > 0 && parent.Children[^1] is { IsText: false } last && last.Tag == tag && last.Href == href) { return last; }
        var next = new RichNode(tag) { Href = href };
        parent.Children.Add(next);
        return next;
    }

    public bool IsCode(Run run) => (run.Parent as Paragraph)?.Tag as string != "pre" && run.FontFamily.Source == Mono.Source;

    // ---- selection as character counts: a block start and a line break count one, as readOffsets does ----

    public static int Count(FlowDocument document, TextPointer target) => CountFrom(document.ContentStart, target);

    public static int LocalOffset(Paragraph paragraph, TextPointer target) => CountFrom(paragraph.ContentStart, target);

    private static int CountFrom(TextPointer start, TextPointer target)
    {
        int count = 0;
        var at = start;
        while (at is not null && at.CompareTo(target) < 0)
        {
            switch (at.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                {
                    int run = at.GetTextRunLength(LogicalDirection.Forward);
                    int reach = at.GetOffsetToPosition(target);
                    if (reach < run) { return count + reach; }
                    count += run;
                    break;
                }
                case TextPointerContext.ElementStart:
                    if (at.GetAdjacentElement(LogicalDirection.Forward) is Paragraph or LineBreak or BlockUIContainer) { count++; }
                    break;
            }
            at = at.GetNextContextPosition(LogicalDirection.Forward);
        }
        return count;
    }

    // The text between two positions. TextRange.Text is no use here: it writes list markers at the start of an item.
    public static string TextBetween(TextPointer start, TextPointer end)
    {
        var text = new System.Text.StringBuilder();
        var at = start;
        while (at is not null && at.CompareTo(end) < 0)
        {
            switch (at.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                {
                    string run = at.GetTextInRun(LogicalDirection.Forward);
                    int reach = at.GetOffsetToPosition(end);
                    text.Append(reach < run.Length ? run[..reach] : run);
                    break;
                }
                case TextPointerContext.ElementStart:
                    if (at.GetAdjacentElement(LogicalDirection.Forward) is LineBreak) { text.Append('\n'); }
                    break;
            }
            at = at.GetNextContextPosition(LogicalDirection.Forward);
        }
        return text.ToString();
    }

    public static TextPointer At(FlowDocument document, int offset) => PointerFrom(document.ContentStart, offset, document.ContentEnd);

    public static TextPointer InParagraph(Paragraph paragraph, int offset) => PointerFrom(paragraph.ContentStart, offset, paragraph.ContentEnd);

    private static TextPointer PointerFrom(TextPointer start, int offset, TextPointer end)
    {
        if (offset <= 0) { return start.GetInsertionPosition(LogicalDirection.Forward); }
        int count = 0;
        var at = start;
        while (at is not null && at.CompareTo(end) < 0)
        {
            switch (at.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                {
                    int run = at.GetTextRunLength(LogicalDirection.Forward);
                    if (count + run >= offset) { return at.GetPositionAtOffset(offset - count, LogicalDirection.Forward); }
                    count += run;
                    break;
                }
                case TextPointerContext.ElementStart:
                {
                    var element = at.GetAdjacentElement(LogicalDirection.Forward);
                    if (element is Paragraph or LineBreak or BlockUIContainer)
                    {
                        count++;
                        if (count >= offset && element is Paragraph paragraph) { return paragraph.ContentStart; }
                        if (count >= offset && element is LineBreak lineBreak) { return lineBreak.ElementEnd.GetInsertionPosition(LogicalDirection.Forward); }
                    }
                    break;
                }
            }
            at = at.GetNextContextPosition(LogicalDirection.Forward);
        }
        return end.GetInsertionPosition(LogicalDirection.Backward);
    }
}
