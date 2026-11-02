using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class CodeBlockTests
{
    [TestMethod]
    public void CodeBlockDefaultsAndBindableMetadata() => Sta.Run(() =>
    {
        var block = new CodeBlock { Code = "const answer = 42;", Filename = "answer.ts", Language = "ts", MaxLines = 4 };
        Assert.AreEqual("answer.ts", block.Filename);
        Assert.AreEqual("ts", block.Language);
        Assert.AreEqual(4, block.MaxLines);
        Assert.IsFalse(block.IsExpanded);
    });

    [TestMethod]
    public void HeaderAndSourceFollowArcGeometry() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 521.6, VerticalAlignment = VerticalAlignment.Top, Filename = "button.tsx", Language = "tsx", Code = "<Button variant=\"secondary\">Review changes</Button>" };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            Assert.AreEqual(64, editor.ActualHeight, .01);
            Assert.AreEqual(119, block.ActualHeight, .51);
            var copy = (CopyButton)block.Template.FindName("PART_CopyButton", block)!;
            Assert.AreEqual(36, copy.ActualHeight, .01);
            Assert.AreEqual("<Button variant=\"secondary\">Review changes</Button>", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.TrimEnd('\r', '\n'));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void TokensUseTheArcKinds() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 521.6, Language = "tsx", Code = "<Button variant=\"secondary\">" };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            var runs = ((Paragraph)editor.Document.Blocks.First()).Inlines.OfType<Run>().ToDictionary(run => run.Text, run => ((SolidColorBrush)run.Foreground).Color);
            Assert.AreEqual(((SolidColorBrush)block.FindResource("ArcWpf.Brush.CodeType")).Color, runs["Button"]);
            Assert.AreEqual(((SolidColorBrush)block.FindResource("ArcWpf.Brush.CodeProperty")).Color, runs["variant"]);
            Assert.AreEqual(((SolidColorBrush)block.FindResource("ArcWpf.Brush.CodeString")).Color, runs["\"secondary\""]);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void LongLinesScrollInsteadOfWrapping() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 320, VerticalAlignment = VerticalAlignment.Top, Code = new string('x', 200) };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout(); Sta.Pump();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            Assert.IsGreaterThan(editor.ViewportWidth, editor.ExtentWidth);
            Assert.AreEqual(64, editor.ActualHeight, .01);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void WindowBuildsLineNumbersAndTokenRuns() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 520, ShowLineNumbers = true, Code = "const answer = 42;\nreturn \"ok\";", Language = "ts" };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            Assert.HasCount(2, editor.Document.Blocks);
            Assert.AreEqual("1  const answer = 42;", new TextRange(editor.Document.Blocks.First().ContentStart, editor.Document.Blocks.First().ContentEnd).Text.TrimEnd('\r', '\n'));
            Assert.IsNotNull(block.Template.FindName("PART_CopyButton", block));
            Assert.IsNotNull(block.Template.FindName("PART_FileName", block));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void MaxLinesExpandsAndCollapsesInsideTheBlock() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 520, MaxLines = 2, Code = "one\ntwo\nthree\nfour" };
        var window = new Window { Content = block, Width = 620, Height = 320, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            var expand = (System.Windows.Controls.Button)block.Template.FindName("PART_ExpandButton", block)!;
            Assert.AreEqual(Visibility.Visible, expand.Visibility);
            double collapsed = editor.ActualHeight;
            expand.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, expand));
            Pump(520);
            Assert.IsTrue(block.IsExpanded);
            Assert.IsGreaterThan(collapsed, editor.ActualHeight);
            expand.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, expand));
            Assert.IsFalse(block.IsExpanded);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void CopyButtonCopiesRawCodeWithoutLineNumbers() => Sta.Run(() =>
    {
        var code = "const answer = 42;\nreturn answer;";
        var block = new CodeBlock { Width = 520, Code = code };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            var copy = (CopyButton)block.Template.FindName("PART_CopyButton", block)!;
            copy.Copy();
            Assert.AreEqual(code, Clipboard.GetText());
            Assert.IsTrue(copy.IsCopied);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void GalleryPageConstructsWithCodeBlock() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.CodeBlockPage();
        Assert.IsNotNull(page.Content);
    });

    private static void Pump(int milliseconds)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
