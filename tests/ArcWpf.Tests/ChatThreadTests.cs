using System.Globalization;
using System.Windows;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass] public sealed class ChatThreadTests
{
    private static readonly ChatParticipant Me = new() { Id = "me", Name = "Emma Collins" }, Chloe = new() { Id = "chloe", Name = "Chloe Nguyen" }, Daniel = new() { Id = "daniel", Name = "Daniel Kim" };
    private static readonly Dictionary<string, ChatParticipant> People = new[] { Me, Chloe, Daniel }.ToDictionary(person => person.Id);
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0);
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static ChatMessage Message(string id, string author, DateTime at, string? text = "hi", ChatMessageStatus? status = null, params ChatReaction[] reactions) => new() { Id = id, AuthorId = author, CreatedAt = at, Text = text, Status = status, Reactions = reactions };

    private static List<ChatRules.Row> Rows(params ChatMessage[] messages) => ChatRules.Rows(messages, People, "me", TimeSpan.FromMinutes(5), Us, new ChatThreadLabels(), Now);

    private static void With(IReadOnlyList<ChatMessage> messages, Action<ChatThread> action, bool controlled = false) => Sta.Run(() =>
    {
        var thread = new ChatThread { CurrentUserId = "me", Participants = [Me, Chloe, Daniel], Width = 520, Height = 420 };
        if (controlled) { thread.Messages = messages; } else { thread.DefaultMessages = messages; }
        ArcMotion.SetLevel(thread, ArcMotionLevel.Off);
        var window = new Window { Content = thread, Width = 600, Height = 500, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(thread); }
        finally { window.Close(); }
    });

    [TestMethod] public void CloseMessagesFromOnePersonShareAGroup()
    {
        var rows = Rows(Message("a", "chloe", Now), Message("b", "chloe", Now.AddMinutes(4)), Message("c", "chloe", Now.AddMinutes(10))).OfType<ChatRules.MessageRow>().ToList();
        Assert.IsTrue(rows[0].First); Assert.IsFalse(rows[0].Last);
        Assert.IsFalse(rows[1].First); Assert.IsTrue(rows[1].Last);
        Assert.IsTrue(rows[2].First); Assert.IsTrue(rows[2].Last);
    }

    [TestMethod] public void ANewAuthorStartsANewGroup()
    {
        var rows = Rows(Message("a", "chloe", Now), Message("b", "me", Now)).OfType<ChatRules.MessageRow>().ToList();
        Assert.IsTrue(rows[0].Last);
        Assert.IsTrue(rows[1].First);
        Assert.IsTrue(rows[1].Mine);
    }

    [TestMethod] public void DaysGetOneSeparatorEach()
    {
        var rows = Rows(Message("a", "chloe", Now.AddDays(-1)), Message("b", "chloe", Now.AddDays(-1).AddMinutes(1)), Message("c", "me", Now));
        CollectionAssert.AreEqual(new[] { "Yesterday", "Today" }, rows.OfType<ChatRules.DayRow>().Select(day => day.Label).ToArray());
    }

    [TestMethod] public void DayLabelsFollowTheSource()
    {
        var labels = new ChatThreadLabels();
        Assert.AreEqual("Today", ChatRules.DayLabel(Now, Us, labels, Now));
        Assert.AreEqual("Yesterday", ChatRules.DayLabel(Now.AddDays(-1), Us, labels, Now));
        Assert.AreEqual("Saturday", ChatRules.DayLabel(new DateTime(2026, 10, 31), Us, labels, Now));
        Assert.AreEqual("Mon, Mar 2", ChatRules.DayLabel(new DateTime(2026, 3, 2), Us, labels, Now));
        Assert.AreEqual("Wed, Dec 3, 2025", ChatRules.DayLabel(new DateTime(2025, 12, 3), Us, labels, Now));
    }

    [TestMethod] public void ReaderUnderTheirOwnMessageStaysHidden()
    {
        ChatMessage[] messages = [Message("a", "chloe", Now), Message("b", "me", Now)];
        var receipts = ChatRules.Receipts(messages, new Dictionary<string, string> { ["chloe"] = "a", ["daniel"] = "b", ["me"] = "b" }, People, "me");
        Assert.IsFalse(receipts.ContainsKey("a"));
        Assert.AreEqual("daniel", receipts["b"].Single().Id);
    }

    [TestMethod] public void ReactionTogglesAddAndRemove()
    {
        var message = Message("a", "chloe", Now, "hi", null, new ChatReaction("🎉", 2));
        var added = ChatRules.Toggle(message, "🎉");
        Assert.AreEqual(new ChatReaction("🎉", 3, true), added.Reactions![0]);
        Assert.AreEqual(new ChatReaction("🎉", 2, false), ChatRules.Toggle(added, "🎉").Reactions![0]);
        var fresh = ChatRules.Toggle(message, "👀");
        Assert.AreEqual(2, fresh.Reactions!.Count);
        Assert.IsEmpty(ChatRules.Toggle(fresh, "👀").Reactions!.Where(reaction => reaction.Emoji == "👀"));
    }

    [TestMethod] public void BytesFormatLikeTheSource()
    {
        Assert.AreEqual("512 B", ChatRules.FormatBytes(512));
        Assert.AreEqual("2 KB", ChatRules.FormatBytes(2048));
        Assert.AreEqual("2.4 MB", ChatRules.FormatBytes((long)(2.4 * 1024 * 1024)));
    }

    [TestMethod] public void StatusSitsOnlyUnderTheLastOwnMessageAndReadWins() => With([Message("a", "me", Now, status: ChatMessageStatus.Delivered), Message("b", "me", Now.AddMinutes(10), status: ChatMessageStatus.Delivered)], thread =>
    {
        Assert.AreEqual(string.Empty, thread.StatusForTest("a"));
        Assert.AreEqual("Delivered", thread.StatusForTest("b"));
        thread.ReadBy = new Dictionary<string, string> { ["chloe"] = "b" };
        Sta.Pump();
        Assert.AreEqual("Read", thread.StatusForTest("b"));
        Assert.AreEqual(1, thread.ReadersForTest("b"));
    });

    [TestMethod] public void UncontrolledThreadAppendsWhatYouSend() => With([Message("a", "chloe", Now)], thread =>
    {
        ChatDraft? sent = null;
        thread.Sent += (_, e) => sent = e.Draft;
        thread.ComposerForTest!.TextForTest!.Text = "  thanks  ";
        Sta.Pump();
        thread.ComposerForTest.Send();
        Sta.Pump();
        Assert.AreEqual("thanks", sent?.Text);
        Assert.AreEqual(2, thread.CurrentForTest.Count);
        Assert.AreEqual("thanks", thread.CurrentForTest[1].Text);
        Assert.AreEqual(string.Empty, thread.ComposerForTest.Text);
    });

    [TestMethod] public void ControlledThreadOnlyReports() => With([Message("a", "chloe", Now)], thread =>
    {
        thread.ComposerForTest!.TextForTest!.Text = "thanks";
        Sta.Pump();
        thread.ComposerForTest.Send();
        thread.ReactForTest("a", "👍");
        Sta.Pump();
        Assert.AreEqual(1, thread.CurrentForTest.Count);
        Assert.IsNull(thread.CurrentForTest[0].Reactions?.FirstOrDefault(reaction => reaction.Emoji == "👍"));
    }, controlled: true);

    [TestMethod] public void BlankComposerDoesNotSend() => With([], thread =>
    {
        int sends = 0;
        thread.Sent += (_, _) => sends++;
        thread.ComposerForTest!.TextForTest!.Text = "   ";
        Sta.Pump();
        thread.ComposerForTest.Send();
        Assert.AreEqual(0, sends);
    });

    [TestMethod] public void BubbleCornersTightenInsideAGroup() => With([Message("a", "chloe", Now), Message("b", "chloe", Now.AddMinutes(1)), Message("c", "me", Now.AddMinutes(1)), Message("d", "me", Now.AddMinutes(2))], thread =>
    {
        Assert.AreEqual(new CornerRadius(18, 18, 18, 6), thread.BubbleForTest("a")!.CornerRadius);
        Assert.AreEqual(new CornerRadius(6, 18, 18, 18), thread.BubbleForTest("b")!.CornerRadius);
        Assert.AreEqual(new CornerRadius(18, 18, 6, 18), thread.BubbleForTest("c")!.CornerRadius);
        Assert.AreEqual(new CornerRadius(18, 6, 18, 18), thread.BubbleForTest("d")!.CornerRadius);
    });

    [TestMethod] public void TypingNamesFollowTheSource()
    {
        var labels = new ChatThreadLabels();
        Assert.AreEqual("Chloe is typing", labels.Typing(["Chloe Nguyen"]));
        Assert.AreEqual("Chloe and Daniel are typing", labels.Typing(["Chloe Nguyen", "Daniel Kim"]));
        Assert.AreEqual("Several people are typing", labels.Typing(["A", "B", "C"]));
    }

    [TestMethod] public void TypingRowAppearsAndAnnouncesExceptForYourself() => With([Message("a", "chloe", Now)], thread =>
    {
        thread.Typing = ["me"];
        Sta.Pump();
        Assert.IsFalse(thread.TypingVisibleForTest);
        thread.Typing = ["chloe"];
        Sta.Pump();
        Assert.IsTrue(thread.TypingVisibleForTest);
        Assert.AreEqual("Chloe is typing", thread.StatusAnnouncementForTest);
    });

    [TestMethod] public void PickerOpensForOneMessageAtATime() => With([Message("a", "chloe", Now), Message("b", "me", Now)], thread =>
    {
        thread.TogglePickerForTest("a");
        Assert.AreEqual("a", thread.PickerForTest);
        thread.TogglePickerForTest("b");
        Assert.AreEqual("b", thread.PickerForTest);
        thread.TogglePickerForTest("b");
        Assert.IsNull(thread.PickerForTest);
    });
}
