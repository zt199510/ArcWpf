using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ShareAccessTests
{
    private static List<ShareAccessMember> People() =>
    [
        new() { Id = "o", Name = "Jasmine Brooks", Email = "jasmine@fieldwork.example", Role = ShareRole.Owner },
        new() { Id = "n", Name = "Nathan Cole", Email = "nathan@fieldwork.example", Role = ShareRole.Editor },
        new() { Id = "c", Name = "Chloe Nguyen", Email = "chloe@fieldwork.example", Role = ShareRole.Commenter },
    ];

    private static List<ShareAccessContact> Directory() =>
    [
        new() { Name = "Marcus Johnson", Email = "marcus@fieldwork.example" },
        new() { Name = "Ava Mitchell", Email = "ava@fieldwork.example" },
        new() { Name = "Mateo Alvarez", Email = "mateo@fieldwork.example" },
        new() { Name = "Nathan Cole", Email = "nathan@fieldwork.example" },
    ];

    private static void Run(Action<ShareAccess> action)
    {
        Sta.Run(() =>
        {
            var panel = new ShareAccess { Title = "Q3 launch plan", Members = People(), Directory = Directory(), SelfId = "o", Link = "https://fieldwork.example/d/1", Width = 440, InviteHandler = (_, _) => Task.CompletedTask };
            var window = new Window { Content = panel, Width = 600, Height = 800, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(panel); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void TypingSuggestsFromTheStartOfAnyNamePartAndLeavesOutMembers() => Run(panel =>
    {
        panel.TypeForTest("m");
        Sta.Pump();
        CollectionAssert.AreEqual(new[] { "Marcus Johnson", "Ava Mitchell", "Mateo Alvarez" }, panel.MatchesForTest.ToArray());
        Assert.IsTrue(panel.SuggestionsOpenForTest, "Regression: placing the list must not throw.");
        panel.TypeForTest("nathan");
        Assert.IsFalse(panel.SuggestionsOpenForTest);
    });

    [TestMethod]
    public void ArrowsMoveTheHighlightAndEnterAddsItAsAChip() => Run(panel =>
    {
        panel.TypeForTest("m");
        panel.KeyForTest(Key.Down);
        Assert.AreEqual(1, panel.HighlightForTest);
        panel.KeyForTest(Key.Up);
        panel.KeyForTest(Key.Up);
        Assert.AreEqual(2, panel.HighlightForTest, "The highlight wraps.");
        panel.KeyForTest(Key.Enter);
        CollectionAssert.AreEqual(new[] { "Mateo Alvarez" }, panel.ChipsForTest.ToArray());
        Assert.AreEqual("", panel.InputForTest.Text);
        Assert.IsFalse(panel.SuggestionsOpenForTest);
        Assert.IsTrue(panel.InviteRowOpenForTest);
    });

    [TestMethod]
    public void AnInvalidAddressBlocksSendingAndExplainsWhy() => Run(panel =>
    {
        panel.TypeForTest("ava");
        panel.KeyForTest(Key.Enter);
        panel.TypeForTest("bad@");
        panel.KeyForTest(Key.Enter);
        CollectionAssert.AreEqual(new[] { "Ava Mitchell", "!bad@" }, panel.ChipsForTest.ToArray());
        StringAssert.Contains(panel.ErrorForTest, "bad@");
        Assert.AreEqual("Invite 2", panel.InviteTextForTest);
        Assert.IsFalse(panel.InviteEnabledForTest);
        panel.KeyForTest(Key.Back);
        Assert.IsTrue(panel.MarkedForTest, "The first Backspace only marks the chip.");
        Assert.HasCount(2, panel.ChipsForTest);
        panel.KeyForTest(Key.Back);
        Assert.HasCount(1, panel.ChipsForTest);
        Assert.IsTrue(panel.InviteEnabledForTest);
    });

    [TestMethod]
    public void SuccessfulInviteAddsPendingMembersWithTheChosenRole() => Run(panel =>
    {
        IReadOnlyList<ShareAccessMember>? changed = null;
        panel.MembersChanged += (_, e) => changed = e.Members;
        panel.TypeForTest("ava");
        panel.KeyForTest(Key.Enter);
        panel.OpenInviteMenuForTest();
        panel.PickForTest(1);
        Assert.AreEqual(ShareRole.Commenter, panel.InviteRoleForTest);
        panel.SendForTest().GetAwaiter().GetResult();
        Assert.HasCount(4, changed!);
        Assert.AreEqual(ShareRole.Commenter, changed![3].Role);
        Assert.IsTrue(changed![3].Pending);
        Assert.AreEqual("Invited · ava@fieldwork.example", panel.PersonMetaForTest(3));
        Assert.AreEqual("4", panel.CountForTest);
        Assert.IsEmpty(panel.ChipsForTest);
    });

    [TestMethod]
    public void ARejectedInviteKeepsTheChipsAndSaysSo() => Run(panel =>
    {
        panel.InviteHandler = (_, _) => Task.FromException(new InvalidOperationException());
        panel.TypeForTest("ava");
        panel.KeyForTest(Key.Enter);
        panel.SendForTest().GetAwaiter().GetResult();
        Assert.HasCount(1, panel.ChipsForTest);
        StringAssert.Contains(panel.ErrorForTest, "Couldn’t send");
        Assert.HasCount(3, panel.Members!);
    });

    [TestMethod]
    public void RoleMenuChangesARoleAndRemovingCanBeUndone() => Run(panel =>
    {
        panel.OpenRoleMenuForTest(1);
        Assert.IsTrue(panel.MenuOpenForTest);
        CollectionAssert.AreEqual(new[] { "Can view", "Can comment", "*Can edit", "Remove access" }, panel.MenuForTest.ToArray());
        panel.PickForTest(0);
        Assert.IsFalse(panel.MenuOpenForTest);
        Assert.AreEqual(ShareRole.Viewer, panel.Members![1].Role);
        panel.OpenRoleMenuForTest(1);
        panel.PickForTest(3);
        Assert.HasCount(2, panel.Members!);
        Assert.AreEqual("Removed Nathan Cole", panel.UndoTextForTest);
        Assert.AreEqual("2", panel.CountForTest);
        panel.UndoForTest();
        Assert.AreEqual("n", panel.Members![1].Id, "Undo restores the same place.");
        Assert.IsFalse(panel.UndoShownForTest);
    });

    [TestMethod]
    public void MenuKeysMoveLettersJumpAndEscapeCloses() => Run(panel =>
    {
        panel.OpenRoleMenuForTest(1);
        Assert.AreEqual(2, panel.MenuActiveForTest, "The menu opens on the current role.");
        panel.MenuKeyForTest(Key.Down);
        Assert.AreEqual(3, panel.MenuActiveForTest);
        panel.MenuKeyForTest(Key.Down);
        Assert.AreEqual(0, panel.MenuActiveForTest);
        panel.TypeAheadForTest('r');
        Assert.AreEqual(3, panel.MenuActiveForTest);
        panel.MenuKeyForTest(Key.Escape);
        Assert.IsFalse(panel.MenuOpenForTest);
    });

    [TestMethod]
    public void GeneralAccessShowsTheLinkRoleAndDescribesIt() => Run(panel =>
    {
        Assert.AreEqual("Only people with access can open", panel.HintForTest);
        Assert.IsFalse(panel.LinkRoleShownForTest);
        panel.OpenAccessMenuForTest();
        panel.PickForTest(1);
        Assert.AreEqual(ShareGeneralAccess.Link, panel.Access);
        Assert.AreEqual("Anyone who has the link can view", panel.HintForTest);
        Assert.IsTrue(panel.LinkRoleShownForTest);
        panel.OpenLinkMenuForTest();
        panel.PickForTest(2);
        Assert.AreEqual(ShareRole.Editor, panel.LinkRole);
        Assert.AreEqual("Anyone who has the link can edit", panel.HintForTest);
    });

    [TestMethod]
    public void CopyAnnouncesTheOutcomeAndFailureIsNotReportedAsCopied() => Run(panel =>
    {
        string? copied = null;
        panel.LinkCopied += (_, e) => copied = e.Link;
        panel.CopyForTestAction(false);
        Assert.AreEqual("Couldn’t copy", panel.CopyTextForTest);
        Assert.IsNull(copied);
        panel.CopyForTestAction(true);
        Assert.AreEqual("Link copied", panel.CopyTextForTest);
        Assert.AreEqual("https://fieldwork.example/d/1", copied);
    });

    [TestMethod]
    public void DoneOnlyAppearsWhenHandled() => Run(panel =>
    {
        var done = (FrameworkElement)typeof(ShareAccess).GetField("_doneButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(panel)!;
        Assert.AreEqual(Visibility.Collapsed, done.Visibility);
        panel.Done += (_, _) => { };
        Assert.AreEqual(Visibility.Visible, done.Visibility);
    });
}
