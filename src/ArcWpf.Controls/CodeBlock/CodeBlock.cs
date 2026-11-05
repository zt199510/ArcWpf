using System.Globalization;
using System.Text.RegularExpressions;
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
    public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register(nameof(ShowLineNumbers), typeof(bool), typeof(CodeBlock), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(nameof(MaxLines), typeof(int), typeof(CodeBlock), new FrameworkPropertyMetadata(0, Changed), value => value is int lines && lines >= 0);
    private static readonly DependencyPropertyKey IsExpandedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsExpanded), typeof(bool), typeof(CodeBlock), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsExpandedProperty = IsExpandedPropertyKey.DependencyProperty;
    public static readonly RoutedUICommand ToggleExpandedCommand = new("Toggle code block", nameof(ToggleExpandedCommand), typeof(CodeBlock));

    // code-block.module.css .pre: 14px at line-height 1.7 with padding 20.
    private const double FontSizeOfSource = 14, LineHeight = 23.8, SourcePadding = 20, BaselineLift = 2;

    private TextBlock? _filename;
    private TextBlock? _language;
    private CopyButton? _copy;
    private System.Windows.Controls.Button? _expand;
    private RichTextBox? _editor;
    private bool _expanded;
    private double _fullHeight;
    private double _collapsedHeight;
    private readonly OwnedMotion _motion = new();

    public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public string Filename { get => (string)GetValue(FilenameProperty); set => SetValue(FilenameProperty, value); }
    public new string Language { get => (string)GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public int MaxLines { get => (int)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }
    /// <summary>Extension beyond Arc, whose source has no line numbers; off by default so the block matches it.</summary>
    public bool ShowLineNumbers { get => (bool)GetValue(ShowLineNumbersProperty); set => SetValue(ShowLineNumbersProperty, value); }
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
        if (_editor is not null) _editor.PreviewMouseWheel += SourceWheel;
        Rebuild();
    }

    private void ReleaseParts()
    {
        StopEditorAnimations();
        if (_expand is not null) _expand.Click -= ExpandClicked;
        if (_editor is not null) _editor.PreviewMouseWheel -= SourceWheel;
        _filename = null; _language = null; _copy = null; _expand = null; _editor = null;
    }
    // The source never scrolls vertically; hand the wheel to whatever scrolls around the block.
    private void SourceWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
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
        string code = (Code ?? string.Empty).Replace("\r\n", "\n");
        string[] lines = code.Split('\n');
        var document = new FlowDocument { FontSize = FontSizeOfSource, LineHeight = LineHeight, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextAlignment = TextAlignment.Left };
        int width = lines.Length.ToString(CultureInfo.InvariantCulture).Length;
        var paragraphs = new Paragraph[lines.Length];
        for (int index = 0; index < lines.Length; index++)
        {
            var paragraph = paragraphs[index] = new Paragraph { Margin = new Thickness(0), KeepTogether = true };
            if (ShowLineNumbers) paragraph.Inlines.Add(Token($"{(index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width)}  ", "ArcWpf.Brush.TextMuted"));
            document.Blocks.Add(paragraph);
        }
        AddHighlightedRuns(paragraphs, code, NormalizeLanguage(Language));
        _editor.Document = document;
        // RichTextBox replaces the page padding when a document attaches, so the source's padding goes on afterwards. It scrolls with the text, like CSS padding.
        // A block line height sets the baseline about 2 DIP lower than CSS half-leading, whatever the mono font; the padding gives it back.
        document.PagePadding = new Thickness(SourcePadding, SourcePadding - BaselineLift, SourcePadding, SourcePadding + BaselineLift);
        // A pre keeps its natural width. RichTextBox only honors PageWidth when the column is that wide as well.
        document.PageWidth = document.ColumnWidth = MeasureSourceWidth(lines, width) + SourcePadding * 2;
        document.ColumnGap = 0;
        _fullHeight = Math.Round(lines.Length * LineHeight + SourcePadding * 2);
        _collapsedHeight = MaxLines > 0 ? Math.Min(_fullHeight, Math.Round(MaxLines * LineHeight + SourcePadding * 2)) : _fullHeight;
        _editor.Height = _expanded || !CanCollapse ? _fullHeight : _collapsedHeight;
    }

    // The document is a pre: it keeps its natural width, so a long line scrolls inside the block instead of wrapping.
    private double MeasureSourceWidth(string[] lines, int numberWidth)
    {
        var typeface = new Typeface(_editor!.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip, widest = 0;
        foreach (string line in lines)
        {
            string text = ShowLineNumbers ? new string('0', numberWidth + 2) + line : line;
            widest = Math.Max(widest, new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSizeOfSource, Brushes.Black, dip).WidthIncludingTrailingWhitespace);
        }
        return Math.Ceiling(widest) + 2;
    }

    // code-block.tsx highlight(): one pass over the whole source, so block comments and template strings can span lines.
    private void AddHighlightedRuns(Paragraph[] paragraphs, string code, string language)
    {
        int cursor = 0, line = 0;
        void Emit(string text, string? key)
        {
            string[] parts = text.Split('\n');
            for (int index = 0; index < parts.Length; index++)
            {
                if (index > 0) line++;
                if (parts[index].Length > 0)
                {
                    var run = key is null ? new Run(parts[index]) : Token(parts[index], key);
                    if (key == "ArcWpf.Brush.TextMuted") run.FontStyle = FontStyles.Italic;
                    paragraphs[line].Inlines.Add(run);
                }
            }
        }
        foreach (Match match in CodeTokens.Pattern.Matches(code))
        {
            if (cursor < match.Index) Emit(code[cursor..match.Index], null);
            Emit(match.Value, CodeTokens.Key(match.Value, code, match.Index, language));
            cursor = match.Index + match.Length;
        }
        if (cursor < code.Length) Emit(code[cursor..], null);
    }

    private static Run Token(string text, string key)
    {
        var run = new Run(text);
        run.SetResourceReference(TextElement.ForegroundProperty, key);
        return run;
    }

    private static string NormalizeLanguage(string? language) => (language ?? "tsx").Trim().TrimStart('.').ToLowerInvariant();

    private void UpdateExpandVisual(bool animate)
    {
        if (_expand is null || !CanCollapse) { if (_expand is not null) _expand.Visibility = Visibility.Collapsed; return; }
        _expand.Visibility = Visibility.Visible;
        string showAll = $"Show all {LineCount} lines";
        _expand.Content = _expanded ? "Show fewer lines" : showAll;
        _expand.Tag = showAll;
        AutomationProperties.SetName(_expand, (string)_expand.Content);
        if (_editor is null) return;
        bool motion = animate && ArcMotion.GetLevel(this) == ArcMotionLevel.Full && IsLoaded;
        double from = _editor.ActualHeight > 0 ? _editor.ActualHeight : _editor.Height;
        _editor.Height = from;
        _motion.Double(_editor, HeightProperty, _expanded ? _fullHeight : _collapsedHeight, Tween.Smooth, motion, spring: true);
        _expand.ApplyTemplate();
        if (_expand.Template.FindName("Spin", _expand) is RotateTransform spin) _motion.Double(spin, RotateTransform.AngleProperty, _expanded ? 180 : 0, Tween.Snappy, motion, spring: true);
    }

    private void StopEditorAnimations() => _motion.Stop();
}
