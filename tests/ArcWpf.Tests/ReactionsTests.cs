using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ReactionsTests
{
    private static Reactions Bar()
    {
        var reactions = new Reactions { Width = 390 };
        reactions.Options.Add(new ReactionOption { Id = "like", Label = "Like", Icon = "thumbs-up" });
        reactions.Options.Add(new ReactionOption { Id = "love", Label = "Love", Icon = "heart" });
        return reactions;
    }

    [TestMethod]
    public void ToggleAddsAndRemovesReaction() => Sta.Run(() =>
    {
        var reactions = Bar();
        reactions.ToggleForTest("like");
        Assert.AreEqual(1, reactions.Values.Single().Count);
        Assert.IsTrue(reactions.Values.Single().Reacted);
        reactions.ToggleForTest("like");
        Assert.IsEmpty(reactions.Values);
    });

    [TestMethod]
    public void ExistingCountChangesWithReaction() => Sta.Run(() =>
    {
        var reactions = Bar();
        reactions.Values.Add(new Reaction { Id = "like", Count = 3, Reacted = true });
        reactions.ToggleForTest("like");
        Assert.AreEqual(2, reactions.Values.Single().Count);
        Assert.IsFalse(reactions.Values.Single().Reacted);
    });

    [TestMethod]
    public void PillsShowCountsNotTypeNames() => Sta.Run(() =>
    {
        var reactions = Bar();
        reactions.Values.Add(new Reaction { Id = "like", Count = 12 });
        var window = new Window { Content = reactions, Width = 500, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); reactions.UpdateLayout();
            var pill = reactions.Pills.Single();
            Assert.AreEqual("12", pill.CountText);
            Assert.AreEqual(32d, pill.ActualHeight, .01);
            Assert.AreEqual(38d, reactions.AddButton.ActualWidth, .01);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void AddButtonGlyphTracksPickerAndPickReacts() => Sta.Run(() =>
    {
        var reactions = Bar();
        var window = new Window { Content = reactions, Width = 500, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.AreEqual("smile-plus", reactions.AddButton.GlyphName);
            reactions.OpenPicker();
            Assert.IsTrue(reactions.IsPickerOpen);
            Assert.AreEqual("x", reactions.AddButton.GlyphName);
            Assert.AreEqual("Like", reactions.Picker!.CaptionText);
            reactions.Picker.HoverForTest(1);
            Assert.AreEqual(42d, reactions.Picker.HighlightTarget, .01, "the highlight travels one 40 wide option plus the 2 gap");
            reactions.ClosePicker(false);
            Assert.AreEqual("smile-plus", reactions.AddButton.GlyphName);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void ChangeReportsAnnouncementAndEvent() => Sta.Run(() =>
    {
        var reactions = Bar();
        ReactionChangedEventArgs? seen = null;
        reactions.ValueChanged += (_, e) => seen = e;
        reactions.ToggleForTest("love");
        Assert.AreEqual("love", seen!.Id);
        Assert.IsTrue(seen.Reacted);
        Assert.AreEqual("Added Love. 1 reaction.", reactions.Announcement);
    });

    [TestMethod]
    public void ExternalCountUpdateRollsWithoutTheChangeEvent() => Sta.Run(() =>
    {
        var reactions = Bar();
        var reaction = new Reaction { Id = "like", Count = 9 };
        reactions.Values.Add(reaction);
        int events = 0;
        reactions.ValueChanged += (_, _) => events++;
        var window = new Window { Content = reactions, Width = 500, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); reactions.UpdateLayout();
            reaction.Count = 10;
            Assert.AreEqual("10", reactions.Pills.Single().CountText);
            Assert.AreEqual(0, events);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void AReactionThatReachesZeroLeavesTheBar() => Sta.Run(() =>
    {
        var reactions = Bar();
        reactions.Values.Add(new Reaction { Id = "like", Count = 1, Reacted = true });
        var window = new Window { Content = reactions, Width = 500, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump(); reactions.UpdateLayout();
            reactions.ToggleForTest("like");
            Assert.IsEmpty(reactions.Values);
            Assert.IsEmpty(reactions.Pills.ToList());
            Assert.AreEqual("Removed Like.", reactions.Announcement);
        }
        finally { window.Close(); }
    });
}
