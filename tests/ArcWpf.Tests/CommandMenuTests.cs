using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class CommandMenuTests
{
    private static readonly CommandMenuItem[] Items =
    [
        new() { Id = "new", Label = "New project", Description = "Start from a blank setup", Group = "Create", Shortcut = "N" },
        new() { Id = "doc", Label = "New document", Description = "Write a brief or a spec", Group = "Create", Shortcut = "Shift+N" },
        new() { Id = "invite", Label = "Invite teammate", Description = "Send an invite by email", Group = "Create", Keywords = ["people"] },
        new() { Id = "share", Label = "Share project", Description = "Invite someone to collaborate", Group = "Project" },
    ];

    private static List<string> Rank(string query) => CommandMenuSearch.Rank(Items, query).Select(entry => entry.Item.Id).ToList();

    [TestMethod] public void EmptyQueryKeepsOrder() => CollectionAssert.AreEqual(new[] { "new", "doc", "invite", "share" }, Rank("  "));
    [TestMethod] public void LabelPrefixRanksFirst() => Assert.AreEqual("invite", Rank("inv")[0]);
    [TestMethod] public void DescriptionMatchesAtAWordStartOnly()
    {
        CollectionAssert.Contains(Rank("invite"), "share");
        CollectionAssert.DoesNotContain(Rank("rief"), "doc");
    }
    [TestMethod] public void KeywordsMatch() => CollectionAssert.AreEqual(new[] { "invite" }, Rank("people"));
    [TestMethod] public void LettersInOrderFindALabel() => CollectionAssert.Contains(Rank("shpr"), "share");
    [TestMethod] public void EveryWordMustMatch() => CollectionAssert.AreEqual(new[] { "new" }, Rank("new project"));
    [TestMethod] public void NoMatchIsEmpty() => Assert.AreEqual(0, Rank("zzzz").Count);
    [TestMethod] public void ShortcutsSplitIntoKeycaps()
    {
        CollectionAssert.AreEqual(new[] { "Shift", "D" }, CommandMenuSearch.KeysOf("Shift+D"));
        CollectionAssert.AreEqual(new[] { "⌘", "K" }, CommandMenuSearch.KeysOf("⌘ K"));
    }

    private static void With(Action<CommandMenu> action, double windowWidth = 800) => Sta.Run(() =>
    {
        var menu = new CommandMenu { Width = 440 };
        foreach (var item in Items) { menu.Items.Add(item); }
        var window = new Window { Content = menu, Width = windowWidth, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(menu); }
        finally { window.Close(); }
    });

    [TestMethod] public void QueryFiltersAndShowsClear() => With(menu =>
    {
        Assert.IsFalse(menu.HasClearButton);
        menu.Query = "inv";
        Sta.Pump();
        Assert.IsTrue(menu.HasClearButton);
        Assert.AreEqual("invite", menu.FilteredForTest[0].Id);
        Assert.AreEqual(0, menu.ActiveIndexForTest);
    });

    [TestMethod] public void NoResultsShowEmptyState() => With(menu =>
    {
        menu.Query = "zzzz";
        Sta.Pump();
        Assert.IsTrue(menu.HasEmptyState);
        Assert.AreEqual(0, menu.FilteredForTest.Count);
    });

    [TestMethod] public void ChooseRaisesItemSelected() => With(menu =>
    {
        CommandMenuItem? picked = null;
        menu.ItemSelected += (_, e) => picked = e.Item;
        menu.Choose(Items[1]);
        Assert.AreEqual("doc", picked?.Id);
    });
}
