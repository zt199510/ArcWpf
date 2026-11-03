using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class CommentThreadTests
{
    private static readonly CommentAuthor Emma = new() { Id = "emma", Name = "Emma Collins" }, Jasmine = new() { Id = "jasmine", Name = "Jasmine Brooks" }, Marcus = new() { Id = "marcus", Name = "Marcus Johnson" };

    private static ThreadComment C(string id, CommentAuthor author, string body = "hello", params ThreadComment[] replies) => new() { Id = id, Author = author, Body = body, CreatedAt = "1h", Replies = replies };

    private static List<ThreadComment> Tree() => [C("root", Jasmine, "Ask @Emma Collins", C("mine", Emma), C("other", Marcus))];

    private static void With(Action<CommentThread> action, bool always = false) => Sta.Run(() =>
    {
        var thread = new CommentThread { CurrentUser = Emma, Title = "Hero headline", DefaultComments = Tree(), Width = 520, AlwaysShowActions = always };
        ArcMotion.SetLevel(thread, ArcMotionLevel.Off);
        var window = new Window { Content = thread, Width = 600, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(thread); }
        finally { window.Close(); }
    });

    [TestMethod] public void CountSkipsDeletedComments()
    {
        ThreadComment[] list = [C("a", Emma, "x", C("b", Marcus)) with { Deleted = true }];
        Assert.AreEqual(1, CommentTree.Count(list));
        Assert.AreEqual("marcus", CommentTree.Authors(list).Single().Id);
    }

    [TestMethod] public void PathRunsFromTheRoot() => CollectionAssert.AreEqual(new[] { "root", "mine" }, CommentTree.PathTo(Tree(), "mine"));

    [TestMethod] public void MentionsMatchTheLongestKnownName()
    {
        var parts = CommentTree.Parts("Hi @Emma Collins and @Emma", ["Emma", "Emma Collins"]).ToList();
        Assert.AreEqual(("@Emma Collins", true), parts[1]);
        Assert.AreEqual(("@Emma", true), parts[3]);
    }

    [TestMethod] public void QueryStartsAtAnAtSignAfterASpace()
    {
        Assert.AreEqual((3, "Em"), CommentTree.Query("hi @Em", 6));
        Assert.AreEqual((0, ""), CommentTree.Query("@", 1));
        Assert.IsNull(CommentTree.Query("mail@Em", 7));
        Assert.IsNull(CommentTree.Query("hi @Em ma", 9));
    }

    [TestMethod] public void SuggestionsMatchAnyWordAndStopAtFive()
    {
        Assert.AreEqual("marcus", CommentTree.Suggest([Emma, Marcus], "joh").Single().Id);
        var many = Enumerable.Range(0, 8).Select(i => new CommentAuthor { Id = $"p{i}", Name = $"Pat {i}" });
        Assert.HasCount(5, CommentTree.Suggest(many, "pat"));
    }

    [TestMethod] public void SubtitleCountsEveryLiveComment() => With(thread => Assert.AreEqual("3 comments", thread.SubtitleForTest));

    [TestMethod] public void ReplyPastMaxDepthAttachesToTheDeepestAllowedParent() => With(thread =>
    {
        thread.MaxDepth = 1;
        thread.StartReplyForTest("mine", "Emma Collins");
        Sta.Pump();
        var box = thread.ComposerForTest!.BoxForTest;
        box.Text = "nested";
        Sta.Pump();
        thread.ComposerForTest.SendForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        var root = thread.CurrentForTest[0];
        Assert.HasCount(3, root.Replies!);
        Assert.AreEqual("nested", root.Replies![2].Body);
        Assert.IsNull(thread.ReplyToForTest);
    });

    [TestMethod] public void EditMarksTheCommentEdited() => With(thread =>
    {
        thread.EditForTest("mine", "changed");
        Sta.Pump();
        var edited = CommentTree.Find(thread.CurrentForTest, "mine")!;
        Assert.IsTrue(edited.Edited);
        Assert.AreEqual("changed", thread.BodyTextForTest("mine"));
    });

    [TestMethod] public void DeletingALeafRemovesItAndAParentKeepsAPlaceholder() => With(thread =>
    {
        thread.RemoveForTest("mine");
        Sta.Pump();
        Assert.IsNull(CommentTree.Find(thread.CurrentForTest, "mine"));
        thread.RemoveForTest("root");
        Sta.Pump();
        var root = CommentTree.Find(thread.CurrentForTest, "root")!;
        Assert.IsTrue(root.Deleted);
        Assert.AreEqual(string.Empty, root.Body);
    });

    [TestMethod] public void ReactionTogglesForTheCurrentUserAndEmptiesAway() => With(thread =>
    {
        thread.ReactForTest("other", "🎉");
        Assert.AreEqual("emma", CommentTree.Find(thread.CurrentForTest, "other")!.Reactions!.Single().Users.Single());
        thread.ReactForTest("other", "🎉");
        Assert.IsEmpty(CommentTree.Find(thread.CurrentForTest, "other")!.Reactions!);
    });

    [TestMethod] public void ChangesReportTheFullTree() => With(thread =>
    {
        CommentsChangedEventArgs? change = null;
        thread.CommentsChanged += (_, e) => change = e;
        thread.ReactForTest("other", "👀");
        Assert.AreEqual(CommentThreadEventType.React, change?.Type);
        Assert.IsTrue(change!.Added);
        Assert.AreSame(thread.CurrentForTest[0].Replies![1].Reactions, CommentTree.Find(change.Comments, "other")!.Reactions);
    });

    [TestMethod] public void MentionsInTheBodyAreHighlighted() => With(thread => CollectionAssert.AreEqual(new[] { "@Emma Collins" }, thread.MentionsForTest("root").ToArray()));

    [TestMethod] public void CollapsingHidesRepliesAndRemembers() => With(thread =>
    {
        thread.ToggleRepliesForTest("root");
        Sta.Pump();
        Assert.IsTrue(thread.RepliesCollapsedForTest("root"));
        thread.StartReplyForTest("root", "Jasmine Brooks");
        thread.ComposerForTest!.BoxForTest.Text = "open up";
        Sta.Pump();
        thread.ComposerForTest.SendForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsFalse(thread.RepliesCollapsedForTest("root"));
    });

    [TestMethod] public void ResolvingFoldsToTheChipAndClearsOpenWork() => With(thread =>
    {
        thread.StartEditForTest("mine");
        Sta.Pump();
        thread.ResolveForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(thread.IsResolved);
        Assert.IsNull(thread.EditingForTest);
        Assert.AreEqual("3 comments · 3 people", thread.ResolvedMetaForTest);
        thread.ReopenForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsFalse(thread.IsResolved);
    });

    [TestMethod] public void ActionsHideUntilHoverUnlessAlwaysShown()
    {
        With(thread => Assert.AreEqual(0d, thread.ActionsOpacityForTest("other")));
        With(thread => Assert.AreEqual(1d, thread.ActionsOpacityForTest("other")), always: true);
    }

    [TestMethod] public void ResolveIsOffWithNothingToResolve() => Sta.Run(() =>
    {
        var thread = new CommentThread { CurrentUser = Emma, Width = 520 };
        var window = new Window { Content = thread, Width = 600, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); Assert.IsFalse(thread.ResolveForTest!.IsEnabled); }
        finally { window.Close(); }
    });
}
