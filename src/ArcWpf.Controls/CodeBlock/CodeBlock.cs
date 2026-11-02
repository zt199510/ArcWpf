using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_FileName", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_Language", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_CopyButton", Type = typeof(CopyButton))]
[TemplatePart(Name = "PART_ExpandButton", Type = typeof(System.Windows.Controls.Button))]
[TemplatePart(Name = "PART_CodeEditor", Type = typeof(RichTextBox))]
public class CodeBlock : Control
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(string), typeof(CodeBlock), new FrameworkPropertyMetadata(string.Empty, Changed));
    public static readonly DependencyProperty FilenameProperty = DependencyProperty.Register(nameof(Filename), typeof(string), typeof(CodeBlock), new FrameworkPropertyMetadata("Source code", Changed));
    public static new readonly DependencyProperty LanguageProperty = DependencyProperty.Register(nameof(Language), typeof(string), typeof(CodeBlock), new FrameworkPropertyMetadata("tsx", Changed));
    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(nameof(MaxLines), typeof(int), typeof(CodeBlock), new FrameworkPropertyMetadata(0, Changed), value => value is int lines && lines >= 0);
    private static readonly DependencyPropertyKey IsExpandedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsExpanded), typeof(bool), typeof(CodeBlock), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsExpandedProperty = IsExpandedPropertyKey.DependencyProperty;
    public static readonly RoutedUICommand ToggleExpandedCommand = new("Toggle code block", nameof(ToggleExpandedCommand), typeof(CodeBlock));

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "async", "await", "break", "case", "catch", "class", "const", "continue", "default", "delete", "do", "else", "enum", "export", "extends", "finally", "for", "from", "function", "get", "if", "implements", "import", "in", "instanceof", "interface", "let", "new", "of", "private", "protected", "public", "readonly", "return", "set", "static", "switch", "throw", "try", "type", "typeof", "var", "void", "while", "with", "yield", "true", "false", "null", "undefined", "NaN", "Infinity", "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE"
    };
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "Array", "Boolean", "Date", "Error", "Map", "Number", "Promise", "Record", "Set", "String", "ReactNode", "HTMLElement", "Event", "unknown", "never", "void", "any", "boolean", "number", "string", "object"
    };
    private static readonly HashSet<char> Punctuation = new("{}[]();,.<>:=+*/!?|&-".ToCharArray());

    private TextBlock? _filename;
    private TextBlock? _language;
    private CopyButton? _copy;
    private System.Windows.Controls.Button? _expand;
    private RichTextBox? _editor;
    private bool _expanded;
    private double _fullHeight;
    private double _collapsedHeight;

    public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public string Filename { get => (string)GetValue(FilenameProperty); set => SetValue(FilenameProperty, value); }
    public new string Language { get => (string)GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public int MaxLines { get => (int)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }
    public bool IsExpanded => (bool)GetValue(IsExpandedProperty);

    static CodeBlock()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CodeBlock), new FrameworkPropertyMetadata(typeof(CodeBlock)));
        CommandManager.RegisterClassCommandBinding(typeof(CodeBlock), new CommandBinding(ToggleExpandedCommand, (sender, _) => ((CodeBlock)sender).ToggleExpanded()));
    }

    public CodeBlock()
    {
        AutomationProperties.SetName(this, "Code block");
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => StopEditorAnimations();
    }

    public void ToggleExpanded()
    {
        if (!CanCollapse) return;
        _expanded = !_expanded;
        SetValue(IsExpandedPropertyKey, _expanded);
        UpdateExpandVisual(true);
    }

    private bool CanCollapse => MaxLines > 0 && LineCount > MaxLines;
    private int LineCount => (Code ?? string.Empty).Replace("\r\n", "\n").Split('\n').Length;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var block = (CodeBlock)d;
        if (e.Property == MaxLinesProperty && !block.CanCollapse) { block._expanded = false; block.SetValue(IsExpandedPropertyKey, false); }
        block.Rebuild();
    }

    public override void OnApplyTemplate()
    {
        ReleaseParts();
        base.OnApplyTemplate();
        _filename = GetTemplateChild("PART_FileName") as TextBlock;
        _language = GetTemplateChild("PART_Language") as TextBlock;
        _copy = GetTemplateChild("PART_CopyButton") as CopyButton;
        _expand = GetTemplateChild("PART_ExpandButton") as System.Windows.Controls.Button;
        _editor = GetTemplateChild("PART_CodeEditor") as RichTextBox;
        if (_expand is not null) _expand.Click += ExpandClicked;
        Rebuild();
    }

    private void ReleaseParts()
    {
        StopEditorAnimations();
        if (_expand is not null) _expand.Click -= ExpandClicked;
        _filename = null; _language = null; _copy = null; _expand = null; _editor = null;
    }
    private void ExpandClicked(object sender, RoutedEventArgs e) { ToggleExpanded(); e.Handled = true; }

    private void Rebuild()
    {
        if (_editor is null || _filename is null || _language is null || _copy is null) return;
        _filename.Text = string.IsNullOrWhiteSpace(Filename) ? "Source code" : Filename;
        _language.Text = NormalizeLanguage(Language);
        _copy.Value = Code ?? string.Empty;
        BuildDocument();
        UpdateExpandVisual(false);
    }

    private void BuildDocument()
    {
        if (_editor is null) return;
        StopEditorAnimations();
        var document = new FlowDocument { FontFamily = new FontFamily("Cascadia Mono"), FontSize = 13, LineHeight = 22, PagePadding = new Thickness(16, 14, 16, 14) };
        string[] lines = (Code ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        int width = Math.Max(2, lines.Length.ToString(CultureInfo.InvariantCulture).Length);
        for (int index = 0; index < lines.Length; index++)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0), KeepTogether = true };
            paragraph.Inlines.Add(new Run((index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width + 1)) { Foreground = FindBrush("ArcWpf.Brush.TextMuted") });
            paragraph.Inlines.Add(new Run("  ") { Foreground = FindBrush("ArcWpf.Brush.BorderStrong") });
            AddHighlightedRuns(paragraph, lines[index], NormalizeLanguage(Language));
            document.Blocks.Add(paragraph);
        }
        _editor.Document = document;
        _editor.IsReadOnly = true;
        _editor.IsUndoEnabled = false;
        _editor.Background = Brushes.Transparent;
        _editor.Foreground = FindBrush("ArcWpf.Brush.Foreground");
        _editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _editor.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _fullHeight = Math.Max(30, lines.Length * 22 + 28);
        _collapsedHeight = Math.Max(30, Math.Min(_fullHeight, Math.Max(1, MaxLines) * 22 + 28));
        _editor.Height = _expanded || !CanCollapse ? _fullHeight : _collapsedHeight;
    }

    private void AddHighlightedRuns(Paragraph paragraph, string line, string language)
    {
        int index = 0;
        while (index < line.Length)
        {
            int start = index;
            string kind;
            if (line[index] == '/' && index + 1 < line.Length && line[index + 1] == '/') { paragraph.Inlines.Add(new Run(line[index..]) { Foreground = FindBrush("ArcWpf.Brush.TextMuted") }); break; }
            if (line[index] == '"' || line[index] == '\'' || line[index] == '`')
            {
                char quote = line[index++];
                while (index < line.Length && line[index] != quote) { if (line[index] == '\\') index++; index++; }
                if (index < line.Length) index++;
                kind = "String";
            }
            else if (char.IsDigit(line[index]))
            {
                index++; while (index < line.Length && (char.IsDigit(line[index]) || line[index] == '.')) index++;
                kind = "Number";
            }
            else if (char.IsLetter(line[index]) || line[index] is '_' or '$')
            {
                index++; while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] is '_' or '$' or '-')) index++;
                string word = line[start..index];
                kind = Keywords.Contains(word) ? "Keyword" : Types.Contains(word) ? "Type" : line[index..].TrimStart().StartsWith('(') && language != "json" ? "Function" : null!;
            }
            else
            {
                index++;
                kind = Punctuation.Contains(line[start]) ? "Punctuation" : null!;
            }
            paragraph.Inlines.Add(new Run(line[start..index]) { Foreground = TokenBrush(kind) });
        }
    }

    private static string NormalizeLanguage(string? language) => (language ?? "tsx").Trim().TrimStart('.').ToLowerInvariant();
    private Brush TokenBrush(string? kind) => FindBrush(kind switch
    {
        "String" => "ArcWpf.Code.String", "Number" => "ArcWpf.Code.Number", "Keyword" => "ArcWpf.Code.Keyword", "Type" => "ArcWpf.Code.Type", "Function" => "ArcWpf.Code.Function", "Punctuation" => "ArcWpf.Brush.TextSecondary", _ => "ArcWpf.Brush.Foreground"
    });
    private Brush FindBrush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;

    private void UpdateExpandVisual(bool animate)
    {
        if (_expand is null || !CanCollapse) { if (_expand is not null) _expand.Visibility = Visibility.Collapsed; return; }
        _expand.Visibility = Visibility.Visible;
        _expand.Content = _expanded ? "Show fewer lines" : $"Show all {LineCount} lines";
        AutomationProperties.SetName(_expand, (string)_expand.Content);
        if (_editor is null) return;
        double target = _expanded ? _fullHeight : _collapsedHeight;
        _editor.BeginAnimation(HeightProperty, null);
        bool motion = animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded;
        if (motion)
        {
            double from = _editor.ActualHeight > 0 ? _editor.ActualHeight : _editor.Height;
            var animation = new System.Windows.Media.Animation.DoubleAnimation(from, target, TimeSpan.FromSeconds(.42))
            {
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd,
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            animation.Completed += (_, _) => { _editor.BeginAnimation(HeightProperty, null); _editor.Height = target; };
            _editor.BeginAnimation(HeightProperty, animation);
        }
        else _editor.Height = target;
    }
    private void StopEditorAnimations() => _editor?.BeginAnimation(HeightProperty, null);
}
