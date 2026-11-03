using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class RichTextMarkupTests
{
    private const string Demo = "<h1>Release notes</h1><p>Version 2.4 ships <strong>Thursday</strong>. Select any text to format it, or type <code>/</code> on an empty line for blocks.</p><h2>What changed</h2><ul><li>Exports stream up to <em>one million</em> rows</li><li>Shared views keep their filters</li><li>Comments on <a href=\"https://example.com/changelog\">dashboards</a></li></ul><blockquote>Keep the public note under 120 words.</blockquote>";

    [TestMethod] public void DemoHtmlSurvivesSanitizing() => Assert.AreEqual(Demo, RichTextMarkup.SanitizeHtml(Demo));

    [TestMethod] public void DemoMarkdownMatchesArcOutput() => Assert.AreEqual(
        "# Release notes\n\nVersion 2.4 ships **Thursday**. Select any text to format it, or type `/` on an empty line for blocks.\n\n## What changed\n\n- Exports stream up to _one million_ rows\n- Shared views keep their filters\n- Comments on [dashboards](https://example.com/changelog)\n\n> Keep the public note under 120 words.",
        RichTextMarkup.HtmlToMarkdown(Demo));

    [TestMethod] public void MarkdownRoundTrips()
    {
        string markdown = RichTextMarkup.HtmlToMarkdown(Demo);
        Assert.AreEqual(Demo, RichTextMarkup.MarkdownToHtml(markdown));
    }

    [DataRow("example.com/docs", "https://example.com/docs")]
    [DataRow("me@example.com", "mailto:me@example.com")]
    [DataRow("/docs", "/docs")]
    [DataRow("#top", "#top")]
    [DataRow("https://a.b", "https://a.b")]
    [DataRow("   ", "")]
    [TestMethod] public void NormalizeUrl(string input, string expected) => Assert.AreEqual(expected, RichTextMarkup.NormalizeUrl(input));

    [DataRow("<script>alert(1)</script><p>x</p>", "<p>x</p>")]
    [DataRow("<p onclick=\"x()\" class=\"y\">z</p>", "<p>z</p>")]
    [DataRow("<a href=\"javascript:alert(1)\">x</a>", "<p>x</p>")]
    [DataRow("<a href=\"https://a.b\" target=\"_blank\">x</a>", "<p><a href=\"https://a.b\">x</a></p>")]
    [DataRow("<b>x</b><i>y</i><strike>z</strike>", "<p><strong>x</strong><em>y</em><s>z</s></p>")]
    [DataRow("<span style=\"font-weight:700\">x</span>", "<p><strong>x</strong></p>")]
    [DataRow("<h5>x</h5>", "<h3>x</h3>")]
    [DataRow("<img src=x onerror=y><p>a</p>", "<p>a</p>")]
    [DataRow("<ul>\n  <li><p>a</p></li>\n</ul>", "<ul><li>a</li></ul>")]
    [TestMethod] public void Sanitize(string input, string expected) => Assert.AreEqual(expected, RichTextMarkup.SanitizeHtml(input));

    [DataRow("# a", "<h1>a</h1>")]
    [DataRow("###### a", "<h3>a</h3>")]
    [DataRow("---", "<hr>")]
    [DataRow("> a\n> b", "<blockquote>a<br>b</blockquote>")]
    [DataRow("```\na < b\n```", "<pre>a &lt; b</pre>")]
    [DataRow("1. a\n2. b", "<ol><li>a</li><li>b</li></ol>")]
    [DataRow("- a\n  - b", "<ul><li>a<ul><li>b</li></ul></li></ul>")]
    [DataRow("a\nb", "<p>a b</p>")]
    [DataRow("**a** ~~b~~ `c` _d_", "<p><strong>a</strong> <s>b</s> <code>c</code> <em>d</em></p>")]
    [DataRow("[a](javascript:x)", "<p>a</p>")]
    [DataRow("", "<p><br></p>")]
    [TestMethod] public void MarkdownToHtml(string input, string expected) => Assert.AreEqual(expected, RichTextMarkup.MarkdownToHtml(input));
}

[TestClass] public sealed class RichTextEditorTests
{
    private static void With(Action<RichTextEditor> action, string? html = null) => Sta.Run(() =>
    {
        var editor = new RichTextEditor { Width = 471.625, Value = html };
        var window = new Window { Content = editor, Width = 600, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            editor.BoxForTest!.Focus();
            action(editor);
        }
        finally { window.Close(); }
    });

    [TestMethod] public void EmptyEditorReportsEmpty() => With(editor =>
    {
        Assert.AreEqual("<p><br></p>", editor.GetHtml());
        Assert.AreEqual("", editor.GetText());
        Assert.AreEqual("", editor.GetMarkdown());
    });

    [DataRow("# ", "<h1>x</h1>")]
    [DataRow("## ", "<h2>x</h2>")]
    [DataRow("### ", "<h3>x</h3>")]
    [DataRow("- ", "<ul><li>x</li></ul>")]
    [DataRow("1. ", "<ol><li>x</li></ol>")]
    [DataRow("> ", "<blockquote>x</blockquote>")]
    [TestMethod] public void BlockShortcuts(string prefix, string expected) => With(editor =>
    {
        editor.TypeForTest(prefix + "x");
        Assert.AreEqual(expected, editor.GetHtml());
    });

    [TestMethod] public void RuleShortcutAddsAParagraphAfter() => With(editor =>
    {
        editor.TypeForTest("---x");
        Assert.AreEqual("<hr><p>x</p>", editor.GetHtml());
    });

    [TestMethod] public void FenceShortcutMakesACodeBlock() => With(editor =>
    {
        editor.TypeForTest("```x");
        Assert.AreEqual("<pre>x</pre>", editor.GetHtml());
    });

    [DataRow("**a** b", "<p><strong>a</strong> b</p>")]
    [DataRow("__a__ b", "<p><strong>a</strong> b</p>")]
    [DataRow("_a_ b", "<p><em>a</em> b</p>")]
    [DataRow("*a* b", "<p><em>a</em> b</p>")]
    [DataRow("~~a~~ b", "<p><s>a</s> b</p>")]
    [DataRow("`a` b", "<p><code>a</code> b</p>")]
    [TestMethod] public void InlineShortcutsExitTheMark(string typed, string expected) => With(editor =>
    {
        editor.TypeForTest(typed);
        Assert.AreEqual(expected, editor.GetHtml());
    });

    [TestMethod] public void EnterSplitsAParagraph() => With(editor =>
    {
        editor.SelectForTest(3, 3);
        editor.EnterForTest();
        Assert.AreEqual("<p>ab</p><p>cd</p>", editor.GetHtml());
    }, "<p>abcd</p>");

    [TestMethod] public void EnterAtTheEndOfAHeadingStartsAParagraph() => With(editor =>
    {
        editor.SelectForTest(3, 3);
        editor.EnterForTest();
        editor.TypeForTest("x");
        Assert.AreEqual("<h1>ab</h1><p>x</p>", editor.GetHtml());
    }, "<h1>ab</h1>");

    [TestMethod] public void EnterKeepsMarksOnBothSides() => With(editor =>
    {
        editor.SelectForTest(3, 3);
        editor.EnterForTest();
        Assert.AreEqual("<p><strong>ab</strong></p><p><strong>cd</strong></p>", editor.GetHtml());
    }, "<p><strong>abcd</strong></p>");

    [TestMethod] public void EnterOnAnEmptyItemLeavesTheList() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.EnterForTest();
        editor.EnterForTest();
        editor.TypeForTest("x");
        Assert.AreEqual("<ul><li>a</li></ul><p>x</p>", editor.GetHtml());
    }, "<ul><li>a</li></ul>");

    [TestMethod] public void TwoEntersLeaveACodeBlock() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.EnterForTest();
        editor.EnterForTest();
        editor.TypeForTest("x");
        Assert.AreEqual("<pre>a</pre><p>x</p>", editor.GetHtml());
    }, "<pre>a</pre>");

    [TestMethod] public void SoftEnterInsertsALineBreak() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.EnterForTest(soft: true);
        editor.TypeForTest("b");
        Assert.AreEqual("<p>a<br>b</p>", editor.GetHtml());
    }, "<p>a</p>");

    [TestMethod] public void BackspaceAtHeadingStartMakesAParagraph() => With(editor =>
    {
        editor.SelectForTest(1, 1);
        editor.BackspaceForTest();
        Assert.AreEqual("<p>a</p>", editor.GetHtml());
    }, "<h1>a</h1>");

    [TestMethod] public void BackspaceAtItemStartOutdents() => With(editor =>
    {
        editor.SelectForTest(1, 1);
        editor.BackspaceForTest();
        Assert.AreEqual("<p>a</p><ul><li>b</li></ul>", editor.GetHtml());
    }, "<ul><li>a</li><li>b</li></ul>");

    [TestMethod] public void HintShowsOnAnEmptyListItem() => With(editor =>
    {
        editor.SelectForTest(3, 3);
        editor.SyncForTest();
        Assert.IsTrue(editor.HintShownForTest);
    }, "<ul><li>a</li><li><br></li></ul>");

    [TestMethod] public void InlineShortcutWorksInAListItem() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.TypeForTest("**b**");
        Assert.AreEqual("<ul><li>a<strong>b</strong></li></ul>", editor.GetHtml());
    }, "<ul><li>a</li></ul>");

    [TestMethod] public void BackspaceAfterARuleRemovesIt() => With(editor =>
    {
        editor.SelectForTest(4, 4);
        editor.BackspaceForTest();
        Assert.AreEqual("<p>a</p><p>b</p>", editor.GetHtml());
    }, "<p>a</p><hr><p>b</p>");

    [TestMethod] public void IndentAndOutdentAListItem() => With(editor =>
    {
        editor.SelectForTest(4, 4);
        editor.IndentForTest();
        Assert.AreEqual("<ul><li>a<ul><li>b</li></ul></li></ul>", editor.GetHtml());
        editor.IndentForTest(outdent: true);
        Assert.AreEqual("<ul><li>a</li><li>b</li></ul>", editor.GetHtml());
    }, "<ul><li>a</li><li>b</li></ul>");

    [DataRow("bold", "<p><strong>ab</strong>c</p>")]
    [DataRow("italic", "<p><em>ab</em>c</p>")]
    [DataRow("strike", "<p><s>ab</s>c</p>")]
    [DataRow("code", "<p><code>ab</code>c</p>")]
    [TestMethod] public void MarksToggleOnAndOff(string mark, string on) => With(editor =>
    {
        editor.SelectForTest(1, 3);
        editor.ToggleMark(mark);
        Assert.AreEqual(on, editor.GetHtml());
        editor.ToggleMark(mark);
        Assert.AreEqual("<p>abc</p>", editor.GetHtml());
    }, "<p>abc</p>");

    [TestMethod] public void LinkIsAddedAndRemoved() => With(editor =>
    {
        editor.SelectForTest(1, 3);
        editor.OpenLink();
        Assert.AreEqual("link", editor.FaceForTest);
        editor.LinkDraftForTest = "example.com";
        editor.ApplyLink();
        Assert.AreEqual("<p><a href=\"https://example.com\">ab</a>c</p>", editor.GetHtml());
        editor.SelectForTest(2, 2);
        editor.OpenLink();
        Assert.AreEqual("https://example.com", editor.LinkDraftForTest);
        editor.ApplyLink(remove: true);
        Assert.AreEqual("<p>abc</p>", editor.GetHtml());
    }, "<p>abc</p>");

    [TestMethod] public void UndoAndRedoRestoreContent() => With(editor =>
    {
        bool canUndo = false;
        editor.HistoryChanged += (_, e) => canUndo = e.CanUndo;
        editor.SelectForTest(2, 2);
        editor.ToggleMark("bold");
        editor.SelectForTest(1, 2);
        editor.ToggleMark("bold");
        Assert.IsTrue(canUndo);
        Assert.AreEqual("<p><strong>a</strong></p>", editor.GetHtml());
        editor.Undo();
        Assert.AreEqual("<p>a</p>", editor.GetHtml());
        Assert.IsFalse(canUndo);
        editor.Redo();
        Assert.AreEqual("<p><strong>a</strong></p>", editor.GetHtml());
    }, "<p>a</p>");

    [TestMethod] public void SlashOpensFiltersAndChooses() => With(editor =>
    {
        editor.TypeForTest("/");
        Assert.IsTrue(editor.IsSlashShown);
        Assert.AreEqual(9, editor.SlashLabelsForTest.Count);
        editor.TypeForTest("quo");
        editor.SyncForTest();
        CollectionAssert.AreEqual(new[] { "Quote" }, editor.SlashLabelsForTest.ToArray());
        editor.ChooseSlashForTest("Quote");
        editor.TypeForTest("x");
        Assert.AreEqual("<blockquote>x</blockquote>", editor.GetHtml());
    });

    [TestMethod] public void SlashAfterAWordDoesNotOpen() => With(editor =>
    {
        editor.TypeForTest("a/");
        Assert.IsFalse(editor.IsSlashShown);
    });

    [TestMethod] public void SlashDividerInsertsARule() => With(editor =>
    {
        editor.TypeForTest("/");
        editor.ChooseSlashForTest("Divider");
        editor.TypeForTest("x");
        Assert.AreEqual("<hr><p>x</p>", editor.GetHtml());
    });

    [TestMethod] public void PasteSingleParagraphStaysInline() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.PasteContent("<p><b>x</b></p>", "x");
        Assert.AreEqual("<p>a<strong>x</strong>b</p>", editor.GetHtml());
    }, "<p>ab</p>");

    [TestMethod] public void PasteBlocksSplitTheLine() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.PasteContent("<h2>x</h2><p>y</p>", "");
        Assert.AreEqual("<p>a</p><h2>x</h2><p>y</p><p>b</p>", editor.GetHtml());
    }, "<p>ab</p>");

    [TestMethod] public void PasteMarkdownIsConverted() => With(editor =>
    {
        editor.SelectForTest(1, 1);
        editor.PasteContent("", "# t\n\n- a");
        Assert.AreEqual("<h1>t</h1><ul><li>a</li></ul>", editor.GetHtml());
    });

    [TestMethod] public void PasteIntoCodeKeepsNewlines() => With(editor =>
    {
        editor.SelectForTest(2, 2);
        editor.PasteContent("", "x\ny");
        Assert.AreEqual("<pre>ax\ny</pre>", editor.GetHtml());
    }, "<pre>a</pre>");

    [TestMethod] public void ValueRewritesOnlyWhenItDiffers() => With(editor =>
    {
        int changes = 0;
        editor.ValueChanged += (_, _) => changes++;
        editor.Value = editor.GetHtml();
        Assert.AreEqual(0, changes);
        editor.Value = "<p>z</p>";
        Assert.AreEqual(1, changes);
        Assert.AreEqual("<p>z</p>", editor.GetHtml());
    }, "<p>a</p>");

    [TestMethod] public void ToolbarOpensOverASelectionAndHidesInCode() => With(editor =>
    {
        editor.SelectForTest(1, 2);
        editor.SyncForTest();
        Assert.IsTrue(editor.IsToolbarShown);
        Assert.AreEqual(new Size(289, 40), editor.ToolbarSizeForTest);
        editor.SelectForTest(1, 1);
        editor.SyncForTest();
        Assert.IsFalse(editor.IsToolbarShown);
    }, "<p>abc</p>");

    [TestMethod] public void HintShowsOnAnEmptyLineOnly() => With(editor =>
    {
        editor.SelectForTest(3, 3);
        editor.SyncForTest();
        Assert.IsTrue(editor.HintShownForTest);
        Assert.AreEqual("Type / for blocks", editor.HintTextForTest);
        editor.SelectForTest(1, 1);
        editor.SyncForTest();
        Assert.IsFalse(editor.HintShownForTest);
    }, "<p>a</p><p><br></p>");
}
