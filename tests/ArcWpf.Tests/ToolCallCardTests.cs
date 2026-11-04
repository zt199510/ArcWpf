using System.Windows;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ToolCallCardTests
{
    private static ToolCallData Call(ToolCallStatus status = ToolCallStatus.Success, string id = "a") => new(id, "GitHub", "Searched issues", status)
    {
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Running] = "Searching issues" },
        DurationMs = 1200,
        Input = [new("repo", "arc-ui/web")],
        Output = new ToolOutput.List([new("#1", "One"), new("#2", "Two")]),
    };

    private static void Card(ToolCallData data, Action<ToolCallCard> action)
    {
        Sta.Run(() =>
        {
            var card = new ToolCallCard { Call = data };
            ArcMotion.SetLevel(card, ArcMotionLevel.Off);
            var window = new Window { Content = card, Width = 560, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(card); }
            finally { window.Close(); }
        });
    }

    private static void Group(ToolCallData[] calls, Action<ToolCallGroup> action, bool? active = null)
    {
        Sta.Run(() =>
        {
            var group = new ToolCallGroup { IsActive = active, Calls = calls };
            ArcMotion.SetLevel(group, ArcMotionLevel.Off);
            var window = new Window { Content = group, Width = 560, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(group); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void TheTitleFollowsTheStatusAndTheMetaCountsResults()
    {
        Assert.AreEqual("Searching issues", Call(ToolCallStatus.Running).ShownTitle);
        Assert.AreEqual("Searched issues", Call().ShownTitle);
        Assert.AreEqual("2 results", Call().ShownSummary);
        Card(Call(), card =>
        {
            StringAssert.StartsWith(card.TitleForTest, "Searched issues");
            StringAssert.Contains(card.MetaForTest + string.Empty, "2 results");
            Assert.IsFalse(card.IsShimmeringForTest);
        });
    }

    [TestMethod]
    public void RunningShimmersAndOpeningShowsInputAndOutput() => Card(Call(ToolCallStatus.Running), card =>
    {
        Assert.IsTrue(card.IsShimmeringForTest);
        Assert.IsFalse(card.PanelShownForTest);
        card.IsOpen = true;
        Sta.Pump();
        Assert.IsTrue(card.PanelShownForTest);
        Assert.IsTrue(card.PanelForTest.Children.Count >= 1);
    });

    [TestMethod]
    public void ApprovalRaisesAllowAndDeny() => Card(Call(ToolCallStatus.Approval), card =>
    {
        int allowed = 0, denied = 0;
        card.ApproveRequested += (_, _) => allowed++;
        card.DenyRequested += (_, _) => denied++;
        var actions = card.ActionsForTest.ToList();
        Assert.HasCount(2, actions);
        foreach (var action in actions) { action.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        Assert.AreEqual(1, allowed);
        Assert.AreEqual(1, denied);
    });

    [TestMethod]
    public void AnErrorShowsItsReasonAndRetry() => Card(Call(ToolCallStatus.Error) with { Message = "Timed out after 5s", Output = null }, card =>
    {
        int retries = 0;
        card.RetryRequested += (_, _) => retries++;
        Assert.AreEqual("Timed out after 5s", card.ReasonForTest);
        card.ActionsForTest.First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, retries);
    });

    [TestMethod]
    public void TheGroupFoldsAfterSuccessAndStaysOpenOnFailure()
    {
        Group([Call(ToolCallStatus.Running)], group =>
        {
            Assert.IsTrue(group.ListOpenForTest);
            group.Calls = [Call()];
            group.IsActive = false;
            Sta.Pump();
            Assert.IsTrue(group.FoldPendingForTest);
            group.FoldNowForTest();
            Sta.Pump();
            Assert.IsFalse(group.ListOpenForTest);
        }, true);
        Group([Call(ToolCallStatus.Running)], group =>
        {
            group.Calls = [Call(ToolCallStatus.Error) with { Output = null, Message = "Timed out" }];
            group.IsActive = false;
            Sta.Pump();
            group.FoldNowForTest();
            Sta.Pump();
            Assert.IsTrue(group.ListOpenForTest);
            Assert.IsFalse(string.IsNullOrEmpty(group.FailedForTest));
        }, true);
    }

    [TestMethod]
    public void TheGroupCountsItsCallsAndKeepsCardsById() => Group([Call(id: "a"), Call(id: "b")], group =>
    {
        StringAssert.Contains(group.LabelForTest, "2");
        Assert.AreEqual(1, group.MarksForTest, "The stack shows each tool once.");
        var first = group.CardsForTest[0];
        group.Calls = [Call(id: "a"), Call(id: "b"), Call(id: "c")];
        Sta.Pump();
        Assert.HasCount(3, group.CardsForTest);
        Assert.AreSame(first, group.CardsForTest[0]);
    });

    [TestMethod]
    public void ThePointerChoiceStopsTheAutoFold() => Group([Call(ToolCallStatus.Running)], group =>
    {
        group.ToggleForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        group.ToggleForTest!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        group.Calls = [Call()];
        group.IsActive = false;
        Sta.Pump();
        group.FoldNowForTest();
        Sta.Pump();
        Assert.IsTrue(group.ListOpenForTest);
    }, true);
}
