using System.Windows;
using System.Windows.Controls.Primitives;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ConversationSidebarTests
{
    private static void With(Action<ConversationSidebar> action) => Sta.Run(() =>
    {
        var bar = new ConversationSidebar();
        ArcMotion.SetLevel(bar, ArcMotionLevel.Off);
        bar.Conversations.Add(new ConversationItem("a", "Add region", "Today"));
        bar.Conversations.Add(new ConversationItem("b", "Partition events", "Today"));
        bar.Conversations.Add(new ConversationItem("c", "Churn themes", "Older"));
        bar.SelectedId = "a";
        var window = new Window { Content = bar, Width = 272, Height = 520, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(bar); }
        finally { window.Close(); }
    });

    private static void Press(System.Windows.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [TestMethod]
    public void ListsChatsUnderTheirGroupsInOrder() => With(bar =>
    {
        CollectionAssert.AreEqual(new[] { "Add region", "Partition events", "Churn themes" }, bar.VisibleTitlesForTest.ToArray());
        CollectionAssert.AreEqual(new[] { "Today", "Older" }, bar.GroupsForTest.ToArray());
    });

    [TestMethod]
    public void NewRaisesTheRequest() => With(bar =>
    {
        int asked = 0;
        bar.NewRequested += (_, _) => asked++;
        Press(bar.NewForTest);
        Assert.AreEqual(1, asked);
    });

    [TestMethod]
    public void AddedConversationAppearsFirst() => With(bar =>
    {
        bar.Conversations.Insert(0, new ConversationItem("n", "New chat", "Today"));
        Sta.Pump();
        Assert.AreEqual("New chat", bar.VisibleTitlesForTest[0]);
    });

    [TestMethod]
    public void PressingARowSelectsIt() => With(bar =>
    {
        ConversationItem? got = null;
        bar.Selected += (_, e) => got = e.Item;
        Press(bar.RowForTest("b"));
        Assert.AreEqual("b", bar.SelectedId);
        Assert.AreEqual("b", got?.Id);
    });

    [TestMethod]
    public void SearchFiltersRowsAndEmptiesGroupsAway() => With(bar =>
    {
        bar.SearchForTest.Text = "CHURN";
        Sta.Pump();
        Assert.AreEqual("CHURN", bar.Query);
        CollectionAssert.AreEqual(new[] { "Churn themes" }, bar.VisibleTitlesForTest.ToArray());
        CollectionAssert.AreEqual(new[] { "Older" }, bar.GroupsForTest.ToArray());
        bar.SearchForTest.Text = "zzz";
        Sta.Pump();
        Assert.IsEmpty(bar.VisibleTitlesForTest);
        StringAssert.Contains(bar.EmptyForTest, "zzz");
        Press(bar.ClearForTest);
        Sta.Pump();
        Assert.AreEqual("", bar.SearchForTest.Text);
        Assert.HasCount(3, bar.VisibleTitlesForTest);
    });

    [TestMethod]
    public void RenameSavesTrimmedTitle() => With(bar =>
    {
        ConversationRenamedEventArgs? got = null;
        bar.Renamed += (_, e) => got = e;
        Press(bar.RenameForTest("b"));
        var editor = bar.EditorForTest("b")!;
        Assert.AreEqual("Partition events", editor.Text);
        editor.Text = "  Events  ";
        Commit(editor, System.Windows.Input.Key.Enter);
        Assert.IsNull(bar.EditorForTest("b"));
        Assert.AreEqual("Events", bar.VisibleTitlesForTest[1]);
        Assert.AreEqual("Partition events", got?.OldTitle);
    });

    [TestMethod]
    public void EscapeAndEmptyKeepTheOldTitle() => With(bar =>
    {
        int renamed = 0;
        bar.Renamed += (_, _) => renamed++;
        Press(bar.RenameForTest("b"));
        bar.EditorForTest("b")!.Text = "Changed";
        Commit(bar.EditorForTest("b")!, System.Windows.Input.Key.Escape);
        Assert.AreEqual("Partition events", bar.VisibleTitlesForTest[1]);
        Press(bar.RenameForTest("b"));
        bar.EditorForTest("b")!.Text = "   ";
        Commit(bar.EditorForTest("b")!, System.Windows.Input.Key.Enter);
        Assert.AreEqual("Partition events", bar.VisibleTitlesForTest[1]);
        Assert.AreEqual(0, renamed);
    });

    [TestMethod]
    public void ArchiveMovesTheRowAndRestoreBringsItBack() => With(bar =>
    {
        ConversationItem? archived = null, restored = null;
        bar.Archived += (_, e) => archived = e.Item;
        bar.Restored += (_, e) => restored = e.Item;
        Press(bar.ArchiveForTest("b"));
        Sta.Pump();
        Assert.AreEqual("b", archived?.Id);
        CollectionAssert.AreEqual(new[] { "Add region", "Churn themes" }, bar.VisibleTitlesForTest.ToArray());
        CollectionAssert.AreEqual(new[] { "Partition events" }, bar.ArchivedTitlesForTest.ToArray());
        Assert.AreEqual(Visibility.Visible, bar.ArchiveToggleForTest.Visibility);
        Press(bar.ArchiveToggleForTest);
        Sta.Pump();
        Assert.IsTrue(bar.ArchiveOpenForTest);
        Assert.Contains("Partition events", bar.VisibleTitlesForTest);
        Press(bar.ArchiveForTest("b"));
        Sta.Pump();
        Assert.AreEqual("b", restored?.Id);
        Assert.IsEmpty(bar.ArchivedTitlesForTest);
        Assert.AreEqual(Visibility.Collapsed, bar.ArchiveToggleForTest.Visibility);
        Assert.IsFalse(bar.ArchiveOpenForTest);
    });

    [TestMethod]
    public void ArchivingTheOpenChatHandsSelectionToTheNext() => With(bar =>
    {
        var picked = new List<string>();
        bar.Selected += (_, e) => picked.Add(e.Item.Id);
        Press(bar.ArchiveForTest("a"));
        Sta.Pump();
        Assert.AreEqual("b", bar.SelectedId);
        CollectionAssert.AreEqual(new[] { "b" }, picked);
    });

    [TestMethod]
    public void ArchivingTheLastChatClearsSelection() => With(bar =>
    {
        foreach (var id in new[] { "c", "b", "a" }) { Press(bar.ArchiveForTest(id)); Sta.Pump(); }
        Assert.IsNull(bar.SelectedId);
        Assert.AreEqual("No chats yet", bar.EmptyForTest);
    });

    [TestMethod]
    public void RemovedConversationLeavesTheList() => With(bar =>
    {
        bar.Conversations.RemoveAt(1);
        Sta.Pump();
        Assert.HasCount(2, bar.VisibleTitlesForTest);
    });

    private static void Commit(System.Windows.Controls.TextBox box, System.Windows.Input.Key key)
    {
        var source = PresentationSource.FromVisual(box)!;
        box.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
    }
}
