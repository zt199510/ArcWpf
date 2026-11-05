using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ApprovalCardTests
{
    private static ApprovalRequest Sample(ApprovalRisk risk = ApprovalRisk.High) => new()
    {
        AgentName = "Deploy agent", ModelName = "Claude Opus 5.5", Asked = "2 min ago", Title = "Run `vercel deploy --prod`", Reason = "Checks passed.", Risk = risk,
        Arguments = [new("Project", "northwind-web"), new("Commit", "8f3c2a1") { IsEditable = false }, new("Statement", "a\nb") { IsMultiline = true }],
    };

    private static void With(Action<ApprovalCard, Window> action) => Sta.Run(() =>
    {
        var card = new ApprovalCard { Request = Sample(), Workspace = "Northwind" };
        ArcMotion.SetLevel(card, ArcMotionLevel.Off);
        var window = new Window { Content = card, Width = 560, Height = 500, Left = 200, Top = 100, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(card, window); }
        finally { window.Close(); }
    });

    private static void Press(System.Windows.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Key(UIElement element, Key key)
    {
        var source = PresentationSource.FromVisual(element)!;
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }

    [TestMethod]
    public void RiskMapsToShieldWordAndColor()
    {
        Assert.AreEqual(("Low", "Success"), Parts(ApprovalRisk.Low));
        Assert.AreEqual(("Medium", "Warning"), Parts(ApprovalRisk.Medium));
        Assert.AreEqual(("High", "Danger"), Parts(ApprovalRisk.High));
        static (string, string) Parts(ApprovalRisk risk) { var look = ApprovalCard.RiskLook(risk); return (look.Word, look.Role); }
    }

    [TestMethod]
    public void ApproveAndDenySettleWithoutASender() => With((card, _) =>
    {
        ApprovalDecision? got = null;
        card.Decided += (_, decision) => got = decision;
        Press(card.ApproveForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Approved, card.Status);
        Assert.AreEqual(ApprovalOutcome.Approved, got?.Outcome);
        Assert.IsFalse(got!.Edited);
        Assert.AreEqual("Approved", card.DecisionTextForTest);
        Assert.IsNull(card.AgainForTest);
        card.Request = Sample();
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Pending, card.Status);
        Press(card.DenyForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Denied, card.Status);
        Assert.AreEqual("Denied", card.DecisionTextForTest);
    });

    [TestMethod]
    public void SendingShowsShimmerAndNoButtonsUntilAnswered() => With((card, _) =>
    {
        var answer = new TaskCompletionSource();
        card.SendDecision = _ => answer.Task;
        Press(card.ApproveForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Sending, card.Status);
        Assert.AreEqual("Sending your approval to Deploy agent", card.DecisionTextForTest);
        Assert.IsNull(card.ApproveForTest);
        Assert.IsNull(card.DenyForTest);
        card.Decide(ApprovalOutcome.Denied); // A second answer while one is on its way is ignored.
        answer.SetResult();
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Approved, card.Status);
    });

    [TestMethod]
    public void FailedSendOffersRetryAndDeny() => With((card, _) =>
    {
        int attempts = 0;
        var outcomes = new List<ApprovalOutcome>();
        card.SendDecision = decision => { outcomes.Add(decision.Outcome); return ++attempts == 1 ? Task.FromException(new InvalidOperationException("Couldn't reach it.")) : Task.CompletedTask; };
        Press(card.ApproveForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Failed, card.Status);
        Assert.AreEqual("Couldn't reach it.", card.DecisionTextForTest);
        Assert.IsNotNull(card.DenyForTest);
        Press(card.RetryForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Approved, card.Status);
        CollectionAssert.AreEqual(new[] { ApprovalOutcome.Approved, ApprovalOutcome.Approved }, outcomes);
    });

    [TestMethod]
    public void CancelledSendEndsAsNotSentAndCanBeRequestedAgain() => With((card, _) =>
    {
        card.SendDecision = _ => Task.FromCanceled(new CancellationToken(true));
        Press(card.ApproveForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.NotSent, card.Status);
        Assert.AreEqual("Not sent", card.DecisionTextForTest);
        ApprovalRequest? again = null;
        card.RequestedAgain += (_, request) => again = request;
        Press(card.AgainForTest!);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Pending, card.Status);
        Assert.AreSame(card.Request, again);
        Assert.IsNotNull(card.ApproveForTest);
    });

    [TestMethod]
    public void RequestAgainAfterDenialDropsTheEdits() => With((card, _) =>
    {
        Press(card.ValueForTest("Project")!);
        card.EditorForTest("Project")!.Text = "other";
        Key(card.EditorForTest("Project")!, System.Windows.Input.Key.Enter);
        Press(card.DenyForTest!);
        Sta.Pump();
        Press(card.AgainForTest!);
        Sta.Pump();
        Assert.AreEqual("northwind-web", card.Request!.Arguments[0].Value);
        Assert.IsNull(card.RestoreForTest("Project"));
    });

    [TestMethod]
    public void EnterSavesAnEditAndTheDecisionCarriesIt() => With((card, _) =>
    {
        ApprovalDecision? got = null;
        card.Decided += (_, decision) => got = decision;
        Press(card.ValueForTest("Project")!);
        card.EditorForTest("Project")!.Text = "other";
        Key(card.EditorForTest("Project")!, System.Windows.Input.Key.Enter);
        Assert.AreEqual("other", card.Request!.Arguments[0].Value);
        Assert.IsNotNull(card.RestoreForTest("Project"));
        Press(card.ApproveForTest!);
        Sta.Pump();
        Assert.IsTrue(got!.Edited);
        Assert.AreEqual("other", got.Arguments["Project"]);
    });

    [TestMethod]
    public void EscapeAndRestoreBringBackTheValue() => With((card, _) =>
    {
        Press(card.ValueForTest("Project")!);
        card.EditorForTest("Project")!.Text = "other";
        Key(card.EditorForTest("Project")!, System.Windows.Input.Key.Escape);
        Assert.AreEqual("northwind-web", card.Request!.Arguments[0].Value);
        Assert.IsNull(card.RestoreForTest("Project"));
        Press(card.ValueForTest("Project")!);
        card.EditorForTest("Project")!.Text = "other";
        Key(card.EditorForTest("Project")!, System.Windows.Input.Key.Enter);
        Press(card.RestoreForTest("Project")!);
        Assert.AreEqual("northwind-web", card.Request!.Arguments[0].Value);
    });

    [TestMethod]
    public void MultilineEnterStaysInTheField() => With((card, _) =>
    {
        Press(card.ValueForTest("Statement")!);
        var editor = card.EditorForTest("Statement")!;
        Key(editor, System.Windows.Input.Key.Enter);
        Assert.IsNotNull(card.EditorForTest("Statement"));
    });

    [TestMethod]
    public void ReadOnlyArgumentHasNoEditButton() => With((card, _) => Assert.IsNull(card.ValueForTest("Commit")));

    [TestMethod]
    public void ShortcutsApproveAndDeny() => With((card, _) =>
    {
        card.CardForTest.Focus();
        Key(card.CardForTest, System.Windows.Input.Key.D);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Denied, card.Status);
        card.Request = Sample();
        Sta.Pump();
        Key(card.CardForTest, System.Windows.Input.Key.A);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Approved, card.Status);
    });

    [TestMethod]
    public void ShortcutsDoNothingOnceAnswered() => With((card, _) =>
    {
        Press(card.DenyForTest!);
        Sta.Pump();
        Key(card.CardForTest, System.Windows.Input.Key.A);
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Denied, card.Status);
    });

    [TestMethod]
    public void AlwaysAllowMenuSitsUnderTheTriggerAndSettlesAsRule() => With((card, window) =>
    {
        window.Activate();
        Sta.Pump();
        ApprovalDecision? got = null;
        card.Decided += (_, decision) => got = decision;
        var trigger = card.AlwaysForTest!;
        Press(trigger);
        Sta.Pump();
        Assert.IsTrue(card.MenuOpenForTest);
        var surface = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetChild(card.MenuForTest, 0);
        var at = trigger.PointToScreen(new Point());
        var menu = surface.PointToScreen(new Point());
        Assert.AreEqual(at.X + trigger.ActualWidth, menu.X + surface.ActualWidth, 1.5);
        Assert.AreEqual(at.Y + trigger.ActualHeight + 6, menu.Y, 1.5);
        Assert.HasCount(2, card.MenuRowsForTest);
        Press(card.MenuRowsForTest[1]);
        Sta.Pump();
        Assert.IsFalse(card.MenuOpenForTest);
        Assert.AreEqual(ApprovalStatus.AllowedByRule, card.Status);
        Assert.AreEqual(ApprovalOutcome.AllowedForWorkspace, got?.Outcome);
    });

    [TestMethod]
    public void ANewRequestAbandonsTheAnswerStillOnItsWay() => With((card, _) =>
    {
        var answer = new TaskCompletionSource();
        int decided = 0;
        card.Decided += (_, _) => decided++;
        card.SendDecision = _ => answer.Task;
        Press(card.ApproveForTest!);
        Sta.Pump();
        card.Request = Sample(ApprovalRisk.Low);
        Sta.Pump();
        answer.SetResult();
        Sta.Pump();
        Assert.AreEqual(ApprovalStatus.Pending, card.Status);
        Assert.AreEqual(0, decided);
        Assert.IsNotNull(card.ApproveForTest);
    });
}
