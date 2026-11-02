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
    public void WindowBuildsLineNumbersAndTokenRuns() => Sta.Run(() =>
    {
        var block = new CodeBlock { Width = 520, Code = "const answer = 42;\nreturn \"ok\";", Language = "ts" };
        var window = new Window { Content = block, Width = 620, Height = 260, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); block.UpdateLayout();
            var editor = (RichTextBox)block.Template.FindName("PART_CodeEditor", block)!;
            Assert.HasCount(2, editor.Document.Blocks);
            Assert.AreEqual("  1  const answer = 42;", new TextRange(editor.Document.Blocks.First().ContentStart, editor.Document.Blocks.First().ContentEnd).Text.TrimEnd('\r', '\n'));
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
