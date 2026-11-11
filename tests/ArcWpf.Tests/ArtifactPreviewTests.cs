using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ArtifactPreviewTests
{
    private static void With(Action<ArtifactPreview> action) => Sta.Run(() =>
    {
        var preview = new ArtifactPreview { BodyMaxHeight = 300 };
        ArcMotion.SetLevel(preview, ArcMotionLevel.Off);
        var window = new Window { Content = preview, Width = 560, Height = 520, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(preview); }
        finally { window.Close(); }
    });

    private static void AddAll(ArtifactPreview preview)
    {
        preview.Items.Add(new ArtifactItem { Title = "Code", Kind = ArtifactKind.Code, FileName = "a.sql", Language = "sql", Content = "SELECT 1;" });
        preview.Items.Add(new ArtifactItem { Title = "JSON", Kind = ArtifactKind.Json, FileName = "a.json", Content = "{\"a\":1}" });
        preview.Items.Add(new ArtifactItem { Title = "Markdown", Kind = ArtifactKind.Markdown, FileName = "a.md", Content = "# Hi\n\nThere" });
        preview.Items.Add(new ArtifactItem { Title = "File", Kind = ArtifactKind.File, FileName = "a.csv", Detail = "2 KB", Content = "x,y" });
    }

    private static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T self) { yield return self; }
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++) { foreach (var found in All<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) { yield return found; } }
    }

    [TestMethod]
    public void EmptyShowsTheNoteInsteadOfTabs() => With(preview =>
    {
        Assert.IsTrue(preview.EmptyShownForTest);
        Assert.AreEqual(Visibility.Collapsed, preview.TabsForTest.Visibility);
        AddAll(preview);
        Sta.Pump();
        Assert.IsFalse(preview.EmptyShownForTest);
        Assert.AreEqual(Visibility.Visible, preview.TabsForTest.Visibility);
    });

    [TestMethod]
    public void EachKindGetsItsOwnView() => With(preview =>
    {
        AddAll(preview);
        Sta.Pump();
        Assert.IsInstanceOfType<CodeBlock>(preview.BodyForTest(0));
        Assert.IsInstanceOfType<JSONViewer>(preview.BodyForTest(1));
        Assert.HasCount(4, preview.TabsForTest.Items);
        Assert.AreEqual("Markdown", ((HeaderedContentControl)preview.TabsForTest.Items[2]!).Header);
    });

    [TestMethod]
    public void SelectedIndexAndTabsFollowEachOther() => With(preview =>
    {
        AddAll(preview);
        Sta.Pump();
        preview.SelectedIndex = 2;
        Sta.Pump();
        Assert.AreEqual(2, preview.TabsForTest.SelectedIndex);
        preview.TabsForTest.SelectedIndex = 3;
        Sta.Pump();
        Assert.AreEqual(3, preview.SelectedIndex);
    });

    [TestMethod]
    public void BodiesFillTheWidth() => With(preview =>
    {
        AddAll(preview);
        Sta.Pump();
        for (int i = 0; i < 4; i++)
        {
            preview.SelectedIndex = i;
            Sta.Pump();
            Assert.AreEqual(preview.ActualWidth, preview.BodyForTest(i).ActualWidth, 1, "tab " + i);
        }
    });

    [TestMethod]
    public void InvalidJsonShowsTheReasonAndTheRawText() => With(preview =>
    {
        preview.Items.Add(new ArtifactItem { Title = "JSON", Kind = ArtifactKind.Json, Content = "{oops" });
        Sta.Pump();
        var body = preview.BodyForTest(0);
        Assert.IsNotInstanceOfType<JSONViewer>(body);
        var code = All<CodeBlock>(body).Single();
        Assert.AreEqual("{oops", code.Code);
        Assert.IsTrue(All<TextBlock>(body).Any(text => text.Text.StartsWith("Invalid JSON", StringComparison.Ordinal)));
    });

    [TestMethod]
    public void MarkdownRendersWithoutTheReplyFoot() => With(preview =>
    {
        preview.Items.Add(new ArtifactItem { Title = "Doc", Kind = ArtifactKind.Markdown, Content = "# Title\n\nBody **bold**" });
        Sta.Pump();
        var message = All<StreamingMessage>(preview.BodyForTest(0)).Single();
        Assert.IsFalse(message.FootShownForTest);
        Assert.AreEqual(2, message.ViewCountForTest);
        Assert.AreEqual(StreamingBlockKind.Heading, message.KindForTest(0));
    });

    [TestMethod]
    public void FileActionsReportTheItem() => With(preview =>
    {
        var item = new ArtifactItem { Title = "File", Kind = ArtifactKind.File, FileName = "a.csv", Detail = "2 KB" };
        preview.Items.Add(item);
        Sta.Pump();
        ArtifactItem? opened = null, downloaded = null;
        preview.OpenRequested += (_, e) => opened = e.Item;
        preview.DownloadRequested += (_, e) => downloaded = e.Item;
        var buttons = All<ButtonBase>(preview.BodyForTest(0)).ToList();
        Assert.HasCount(2, buttons);
        buttons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        buttons[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreSame(item, opened);
        Assert.AreSame(item, downloaded);
    });

    [TestMethod]
    public void RemovingItemsClampsTheSelection() => With(preview =>
    {
        AddAll(preview);
        Sta.Pump();
        preview.SelectedIndex = 3;
        Sta.Pump();
        preview.Items.RemoveAt(3);
        preview.Items.RemoveAt(2);
        Sta.Pump();
        Assert.AreEqual(1, preview.TabsForTest.SelectedIndex);
        Assert.AreEqual(1, preview.SelectedIndex);
        preview.Items.Clear();
        Assert.AreEqual(0, preview.SelectedIndex);
    });
}
