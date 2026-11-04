using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class PromptInputTests
{
    private static readonly PromptCommand[] Commands = [new("summarize", "summarize", "Condense"), new("fix", "fix", "Find a bug")];
    private static readonly PromptMentionGroup[] Mentions = [new("Files", [new("checkout", "checkout.tsx", "src/app")]), new("Docs", [new("plan", "Launch plan", "Notion")])];

    private static void Run(Action<PromptInput> action, double width = 640)
    {
        Sta.Run(() =>
        {
            var input = new PromptInput { Commands = Commands, Mentions = Mentions };
            ArcMotion.SetLevel(input, ArcMotionLevel.Off);
            var window = new Window { Content = input, Width = width, Height = 500, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(input); }
            finally { window.Close(); }
        });
    }

    private static void Type(PromptInput input, string text)
    {
        var box = input.TextBoxForTest;
        foreach (char c in text)
        {
            box.SelectedText = c.ToString();
            box.CaretIndex = box.SelectionStart + box.SelectionLength;
            input.SuggestForTest();
        }
    }

    [TestMethod]
    public void SlashOpensCommandsOnlyAtTheStartOfALine() => Run(input =>
    {
        Type(input, "/");
        Assert.AreEqual("Command", input.ListKindForTest);
        Assert.IsTrue(input.ListOpenForTest);
        CollectionAssert.AreEqual(new[] { "/summarize", "/fix" }, input.ListLabelsForTest);
        Type(input, "f");
        CollectionAssert.AreEqual(new[] { "/fix" }, input.ListLabelsForTest);
        Type(input, " ");
        Assert.IsFalse(input.ListOpenForTest);
        Type(input, "and /");
        Assert.IsFalse(input.ListOpenForTest);
    });

    [TestMethod]
    public void AtOpensMentionsAfterASpaceOnly() => Run(input =>
    {
        Type(input, "mail@");
        Assert.IsFalse(input.ListOpenForTest);
        Type(input, " @");
        Assert.AreEqual("Mention", input.ListKindForTest);
        CollectionAssert.AreEqual(new[] { "checkout.tsx", "Launch plan" }, input.ListLabelsForTest);
        Type(input, "lau");
        CollectionAssert.AreEqual(new[] { "Launch plan" }, input.ListLabelsForTest);
    });

    [TestMethod]
    public void ArrowsMoveEnterInsertsAnAtomicToken() => Run(input =>
    {
        Type(input, "/");
        Assert.IsTrue(input.KeyForTest(Key.Down));
        Assert.AreEqual(1, input.SelectedForTest);
        Assert.IsTrue(input.KeyForTest(Key.Down));
        Assert.AreEqual(0, input.SelectedForTest, "The list wraps.");
        Assert.IsTrue(input.KeyForTest(Key.Enter));
        Assert.AreEqual("/summarize ", input.Text);
        Assert.IsFalse(input.ListOpenForTest);
        Assert.HasCount(1, input.Tokens);
        Assert.AreEqual(PromptTokenKind.Command, input.Tokens[0].Kind);
    });

    [TestMethod]
    public void TabInsertsAndAClickOnARowInsertsToo() => Run(input =>
    {
        Type(input, "@");
        Assert.IsTrue(input.KeyForTest(Key.Tab));
        Assert.AreEqual("@checkout.tsx ", input.Text);
        Type(input, "and @");
        input.ClickListRowForTest(1);
        Assert.AreEqual("@checkout.tsx and @Launch plan ", input.Text);
        Assert.HasCount(2, input.Tokens);
        Assert.AreEqual(18, input.Tokens[1].Start);
    });

    [TestMethod]
    public void EscapeClosesTheListAndItStaysClosedForThatTrigger() => Run(input =>
    {
        Type(input, "/");
        Assert.IsTrue(input.KeyForTest(Key.Escape));
        Assert.IsFalse(input.ListOpenForTest);
        Type(input, "f");
        Assert.IsFalse(input.ListOpenForTest);
    });

    [TestMethod]
    public void BackspaceAndDeleteRemoveAWholeTokenAndArrowsHopIt() => Run(input =>
    {
        Type(input, "/");
        input.KeyForTest(Key.Enter);
        var box = input.TextBoxForTest;
        box.CaretIndex = input.Tokens[0].End;
        Assert.IsTrue(input.KeyForTest(Key.Left));
        Assert.AreEqual(0, box.CaretIndex);
        Assert.IsTrue(input.KeyForTest(Key.Right));
        Assert.AreEqual(input.Tokens[0].End, box.CaretIndex);
        Assert.IsTrue(input.KeyForTest(Key.Back));
        Assert.AreEqual(" ", input.Text);
        Assert.IsEmpty(input.Tokens);
    });

    [TestMethod]
    public void EditsBeforeATokenShiftItAndEditsInsideDissolveIt() => Run(input =>
    {
        Type(input, "x @");
        input.KeyForTest(Key.Enter);
        var box = input.TextBoxForTest;
        int start = input.Tokens[0].Start;
        box.CaretIndex = 0;
        Type(input, "ab");
        Assert.AreEqual(start + 2, input.Tokens[0].Start);
        Assert.AreEqual("@checkout.tsx", input.Text.Substring(input.Tokens[0].Start, input.Tokens[0].Text.Length));
        box.Select(input.Tokens[0].Start + 3, 2);
        box.SelectedText = "Q";
        Assert.IsEmpty(input.Tokens);
    });

    [TestMethod]
    public void EnterSendsTheMessageAndClearsTheBox() => Run(input =>
    {
        PromptMessage? sent = null;
        input.Submitted += (_, e) => sent = e.Message;
        input.Tools = [new PromptTool("web", "Web", Lucide.Globe) { IsOn = true }, new PromptTool("off", "Off", Lucide.Brain)];
        input.Attachments.Add(new PromptAttachment("a.txt"));
        Type(input, "/");
        input.KeyForTest(Key.Enter);
        Type(input, "the plan");
        Assert.IsTrue(input.KeyForTest(Key.Enter));
        Assert.AreEqual("/summarize the plan", sent!.Text);
        Assert.HasCount(1, sent.Tokens);
        Assert.HasCount(1, sent.Attachments);
        CollectionAssert.AreEqual(new[] { "web" }, sent.Tools.ToArray());
        Assert.AreEqual("", input.Text);
        Assert.IsEmpty(input.Tokens);
        Assert.IsEmpty(input.Attachments);
    });

    [TestMethod]
    public void ShiftEnterIsLeftToTheFieldAndAnEmptyBoxWillNotSend() => Run(input =>
    {
        int sends = 0;
        input.Submitted += (_, _) => sends++;
        Assert.IsTrue(input.KeyForTest(Key.Enter));
        Assert.AreEqual(0, sends);
        Assert.IsFalse(input.CanSendForTest);
        Type(input, "hi");
        Assert.IsFalse(input.KeyForTest(Key.Enter, true));
        Assert.AreEqual(0, sends);
    });

    [TestMethod]
    public void TheSendButtonNamesItsState() => Run(input =>
    {
        Assert.AreEqual("Send message", input.SendNameForTest);
        Type(input, "hi");
        Assert.IsTrue(input.CanSendForTest);
        var upload = new PromptAttachment("big.pdf") { Status = PromptAttachmentStatus.Uploading, Progress = .4 };
        input.Attachments.Add(upload);
        Assert.AreEqual("Send after uploads finish", input.SendNameForTest);
        Assert.IsFalse(input.CanSendForTest);
        upload.Status = PromptAttachmentStatus.Done;
        Assert.IsTrue(input.CanSendForTest);
        input.MaxLength = 1;
        Assert.AreEqual("Send with the message over the limit", input.SendNameForTest);
        Assert.IsFalse(input.CanSendForTest);
        input.MaxLength = 100;
        input.IsGenerating = true;
        Assert.AreEqual("Stop generating", input.SendNameForTest);
        Assert.IsTrue(input.SendIsStopForTest);
    });

    [TestMethod]
    public void EscapeStopsARunningReplyOnlyAfterTheListCloses() => Run(input =>
    {
        int stops = 0;
        input.StopRequested += (_, _) => stops++;
        input.IsGenerating = true;
        Type(input, "/");
        input.KeyForTest(Key.Escape);
        Assert.AreEqual(0, stops);
        input.KeyForTest(Key.Escape);
        Assert.AreEqual(1, stops);
        input.SendForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(2, stops);
    });

    [TestMethod]
    public void WhileGeneratingEnterDoesNotSend() => Run(input =>
    {
        int sends = 0;
        input.Submitted += (_, _) => sends++;
        Type(input, "next");
        input.IsGenerating = true;
        input.KeyForTest(Key.Enter);
        Assert.AreEqual(0, sends);
        Assert.AreEqual("next", input.Text);
    });

    [TestMethod]
    public void TheCounterAppearsAtEightyPercentAndWarnsAtNinetyFive() => Run(input =>
    {
        input.MaxLength = 100;
        input.TextBoxForTest.Text = new string('a', 79);
        Assert.IsFalse(input.CounterShownForTest);
        input.TextBoxForTest.Text = new string('a', 80);
        Assert.IsTrue(input.CounterShownForTest);
        Assert.AreEqual("TextMuted", input.CounterRoleForTest);
        Assert.AreEqual("20", input.CounterTextForTest);
        input.TextBoxForTest.Text = new string('a', 95);
        Assert.AreEqual("Warning", input.CounterRoleForTest);
        input.TextBoxForTest.Text = new string('a', 101);
        Assert.AreEqual("Danger", input.CounterRoleForTest);
        Assert.AreEqual("-1", input.CounterTextForTest);
    });

    [TestMethod]
    public void TheFieldGrowsToEightLinesThenScrolls() => Run(input =>
    {
        double rest = input.FieldHeightForTest;
        Assert.AreEqual(40, rest, 1);
        input.TextBoxForTest.Text = "1\n2\n3";
        Sta.Pump();
        Assert.AreEqual(24 * 3 + 16, input.FieldHeightForTest, 1);
        input.TextBoxForTest.Text = string.Join("\n", Enumerable.Range(0, 30));
        Sta.Pump();
        Assert.AreEqual(24 * 8 + 16, input.FieldHeightForTest, 1);
    });

    [TestMethod]
    public void ALongPasteBecomesAnAttachmentAndAShortOneFillsTheField() => Run(input =>
    {
        input.LongPasteLength = 20;
        PromptAttachment? added = null;
        input.AttachmentAdded += (_, e) => added = e.Attachment;
        input.PasteForTest("short");
        Assert.AreEqual("short", input.Text);
        Assert.IsEmpty(input.Attachments);
        string longText = new('x', 25);
        input.PasteForTest(longText);
        Assert.HasCount(1, input.Attachments);
        Assert.AreEqual(longText, added!.PastedText);
        Assert.AreEqual("short", input.Text);
    });

    [TestMethod]
    public void ChipsFollowUploadsAndRemoveReportsTheFile() => Run(input =>
    {
        PromptAttachment? removed = null;
        input.AttachmentRemoved += (_, e) => removed = e.Attachment;
        var file = new PromptAttachment("a.pdf") { Status = PromptAttachmentStatus.Uploading, Progress = .2 };
        input.Attachments.Add(file);
        Assert.AreEqual(1, input.ChipCountForTest);
        Assert.AreEqual("Cancel upload", AutomationName(input.RemoveForTest(0)!));
        file.Status = PromptAttachmentStatus.Error;
        file.Error = "Too large";
        Assert.AreEqual("Error", input.ChipStateForTest(0));
        Assert.AreEqual("Remove a.pdf", AutomationName(input.RemoveForTest(0)!));
        input.RemoveForTest(0)!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreSame(file, removed);
        Assert.AreEqual(0, input.ChipCountForTest);
    });

    [TestMethod]
    public void AFailedFileIsNotSent() => Run(input =>
    {
        PromptMessage? sent = null;
        input.Submitted += (_, e) => sent = e.Message;
        input.Attachments.Add(new PromptAttachment("bad.pdf") { Status = PromptAttachmentStatus.Error });
        Assert.IsFalse(input.CanSendForTest, "A failed file alone is nothing to send.");
        Type(input, "hi");
        input.KeyForTest(Key.Enter);
        Assert.IsEmpty(sent!.Attachments);
    });

    [TestMethod]
    public void ThePlusMenuListsActionsAndRaisesThem() => Run(input =>
    {
        PromptAttachEventArgs? asked = null;
        input.AttachRequested += (_, e) => { asked = e; e.Handled = true; };
        input.AttachActions = [new("drive", "Add from Drive", Lucide.Layers)];
        input.PlusForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        Assert.IsTrue(input.PlusOpenForTest);
        CollectionAssert.AreEqual(new[] { "Add from Drive", "Mention a file or doc" }, input.PlusLabelsForTest);
        input.ClickPlusRowForTest("Add from Drive");
        Assert.AreEqual("drive", asked!.ActionId);
        Assert.IsFalse(input.PlusOpenForTest);
        input.PlusForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Sta.Pump();
        input.ClickPlusRowForTest("Mention a file or doc");
        Assert.AreEqual("@", input.Text);
        Assert.AreEqual("Mention", input.ListKindForTest);
    });

    [TestMethod]
    public void TheListsOpenClearOfTheField() => Sta.Run(() =>
    {
        var input = new PromptInput { Commands = Commands, Mentions = Mentions, VerticalAlignment = VerticalAlignment.Bottom };
        ArcMotion.SetLevel(input, ArcMotionLevel.Off);
        var window = new Window { Content = input, Left = 0, Top = 500, Width = 640, Height = 200, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Type(input, "/");
            Sta.Pump();
            var surface = input.ListSurfaceForTest;
            double bottom = surface.PointToScreen(new Point(0, surface.ActualHeight)).Y;
            double line = input.TextBoxForTest.PointToScreen(new Point(0, 8)).Y;
            Assert.IsTrue(bottom <= line, $"The list ends at {bottom}, the line starts at {line}.");
            input.KeyForTest(Key.Escape);
            input.PlusForTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Sta.Pump();
            var plus = input.PlusSurfaceForTest;
            Assert.AreEqual(input.PlusForTest.PointToScreen(new Point()).Y - 8, plus.PointToScreen(new Point(0, plus.ActualHeight)).Y, 1.5);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void AStarterPromptBecomesTokens() => Run(input =>
    {
        input.SetPrompt("/fix the retry in @checkout.tsx please");
        Assert.HasCount(2, input.Tokens);
        Assert.AreEqual(PromptTokenKind.Command, input.Tokens[0].Kind);
        Assert.AreEqual(18, input.Tokens[1].Start);
        Assert.IsFalse(input.ListOpenForTest);
    });

    [TestMethod]
    public void TheErrorLineFollowsErrorTextAndSendingClearsIt() => Run(input =>
    {
        input.ErrorText = "The last message failed.";
        Assert.IsTrue(input.ErrorShownForTest);
        Assert.AreEqual("The last message failed.", input.ErrorForTest);
        Type(input, "again");
        input.KeyForTest(Key.Enter);
        Assert.IsFalse(input.ErrorShownForTest);
    });

    [TestMethod]
    public void TheReplyRingShowsProgressWhileGenerating() => Run(input =>
    {
        input.IsGenerating = true;
        input.Progress = .4;
        Assert.IsTrue(input.ReplyRingShownForTest);
        Assert.AreEqual(.4, input.ReplyRingValueForTest, .001);
        input.IsGenerating = false;
        Assert.IsFalse(input.ReplyRingShownForTest);
    });

    [TestMethod]
    public void ToolsToggleAndTheModelChipFollowsTheProperty() => Run(input =>
    {
        var tool = new PromptTool("web", "Web", Lucide.Globe);
        input.Tools = [tool];
        input.ToolButtonsForTest[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.IsTrue(tool.IsOn);
        input.Models = [new("a", "Model A", "p") { Short = "A" }, new("b", "Model B", "p") { Short = "B" }];
        input.Model = "b";
        Sta.Pump();
        Assert.AreEqual("B", input.PickerForTest.PillNameForTest);
        input.PickerForTest.Value = "a";
        Assert.AreEqual("a", input.Model);
    });

    [TestMethod]
    public void TheToolbarFoldsWithItsContainer()
    {
        Run(input => Assert.AreEqual(0, input.PickerForTest.FoldLevel), 700);
        Run(input => Assert.AreEqual(1, input.PickerForTest.FoldLevel), 520);
        Run(input => Assert.AreEqual(2, input.PickerForTest.FoldLevel), 380);
    }

    [TestMethod]
    public void TheSampleMarksParse()
    {
        Sta.Run(() =>
        {
            var path = "M4.459 4.208c.746.606 1.026.56 2.428.466z";
            Assert.IsNotNull(Geometry.Parse(path));
            Assert.IsNotNull(Geometry.Parse("m4.7144 15.9555 4.7174-2.6471z"));
        });
    }

    private static string AutomationName(DependencyObject element) => System.Windows.Automation.AutomationProperties.GetName(element);
}
