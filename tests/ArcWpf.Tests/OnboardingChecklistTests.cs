using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class OnboardingChecklistTests
{
    private static void With(Action<OnboardingChecklist> action, params string[] done) => Sta.Run(() =>
    {
        var checklist = new OnboardingChecklist { Width = 440 };
        foreach (string id in new[] { "a", "b", "c" }) { checklist.Steps.Add(new OnboardingStep { Id = id, Title = id.ToUpperInvariant(), Description = "About " + id, ActionLabel = "Do" }); }
        foreach (string id in done) { checklist.DefaultCompleted.Add(id); }
        var window = new Window { Content = checklist, Width = 500, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(checklist); }
        finally { window.Close(); }
    });

    [TestMethod] public void FirstUnfinishedStepStartsOpen() => With(checklist => Assert.AreEqual("b", checklist.ExpandedId), "a");

    [TestMethod] public void InitiallyDoneStepsAreSettled() => With(checklist => CollectionAssert.AreEqual(new[] { "a" }, checklist.SettledIds.ToArray()), "a");

    [TestMethod] public void CheckReportsCompletedBeforeTheStepSettles() => With(checklist =>
    {
        IReadOnlyList<string>? reported = null;
        checklist.CompletedChanged += (_, ids) => reported = ids.ToList();
        checklist.Check("a");
        CollectionAssert.AreEqual(new[] { "a" }, reported!.ToArray());
        Assert.AreEqual(0, checklist.SettledIds.Count());
        Assert.AreEqual(720, OnboardingChecklist.SETTLE_MS);
    });

    [TestMethod] public void SettlingHandsThePanelToTheNextStep() => With(checklist =>
    {
        checklist.Check("a");
        checklist.Settle("a");
        Assert.AreEqual("b", checklist.ExpandedId);
        CollectionAssert.AreEqual(new[] { "a" }, checklist.SettledIds.ToArray());
    });

    [TestMethod] public void SettlingWrapsToAnEarlierOpenStep() => With(checklist =>
    {
        checklist.Check("b");
        checklist.Settle("b");
        checklist.Check("c");
        Assert.AreEqual("a", checklist.ExpandedId == "c" ? Settled(checklist) : checklist.ExpandedId);
    });

    private static string? Settled(OnboardingChecklist checklist)
    {
        checklist.Settle("c");
        return checklist.ExpandedId;
    }

    [TestMethod] public void UncheckBringsASettledStepBack() => With(checklist =>
    {
        checklist.Uncheck("a");
        Assert.AreEqual(0, checklist.SettledIds.Count());
    }, "a");

    [TestMethod] public void GroupOnlyShowsOnceStepsHaveSettled() => With(checklist =>
    {
        Assert.IsFalse(checklist.GroupVisible);
        checklist.Check("a");
        checklist.Settle("a");
        Assert.IsTrue(checklist.GroupVisible);
    });

    [TestMethod] public void ResetReturnsToDefaults() => With(checklist =>
    {
        checklist.Check("b");
        checklist.Settle("b");
        checklist.Reset();
        CollectionAssert.AreEqual(new[] { "a" }, checklist.SettledIds.ToArray());
        Assert.AreEqual("b", checklist.ExpandedId);
    }, "a");
}
