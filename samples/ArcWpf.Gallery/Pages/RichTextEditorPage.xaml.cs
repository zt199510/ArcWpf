using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class RichTextEditorPage : UserControl
{
    private const string Demo = "<h1>Release notes</h1><p>Version 2.4 ships <strong>Thursday</strong>. Select any text to format it, or type <code>/</code> on an empty line for blocks.</p><h2>What changed</h2><ul><li>Exports stream up to <em>one million</em> rows</li><li>Shared views keep their filters</li><li>Comments on <a href=\"https://example.com/changelog\">dashboards</a></li></ul><blockquote>Keep the public note under 120 words.</blockquote>";

    private RichTextValue? _value;

    public RichTextEditorPage()
    {
        InitializeComponent();
        Editor.Value = Demo;
        ShowSource();
    }

    private void OnCardSize(object sender, SizeChangedEventArgs e) => Card.Clip = new RectangleGeometry(new Rect(e.NewSize), 33, 33);

    private void OnValueChanged(object? sender, RichTextValueChangedEventArgs e)
    {
        _value = e.Value;
        int words = Regex.Matches(e.Value.Text, @"\S+").Count;
        WordCount.Text = $"{words} {(words == 1 ? "word" : "words")}";
        ShowSource();
    }

    private void OnHistoryChanged(object? sender, RichTextHistoryEventArgs e)
    {
        UndoButton.IsEnabled = e.CanUndo;
        RedoButton.IsEnabled = e.CanRedo;
    }

    private void OnUndo(object sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Editor.Redo();
    private void OnReset(object sender, RoutedEventArgs e) => Editor.SetHtml(Demo);
    private void OnTab(object sender, RoutedEventArgs e) => ShowSource();

    private void ShowSource()
    {
        if (Source is null || _value is null) { return; }
        Source.Text = HtmlTab.IsChecked == true ? Pretty(_value.Html) : _value.Markdown;
    }

    // One block per line, the way the demo prints its HTML tab.
    private static string Pretty(string html) => Regex.Replace(html, @"(</(?:h1|h2|h3|p|li|ul|ol|blockquote|pre)>|<hr>|<ul>|<ol>)(?=.)", "$1\n");

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Source.Text);
        CopyText.Text = "Copied";
        await Task.Delay(1400);
        CopyText.Text = "Copy";
    }
}
