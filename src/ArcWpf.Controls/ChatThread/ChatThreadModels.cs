using System.Globalization;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum ChatAttachmentKind { Image, File }
public enum ChatMessageStatus { Sending, Sent, Delivered, Read, Failed }

public sealed class ChatParticipant
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Portrait. Initials show when it is missing.</summary>
    public ImageSource? Avatar { get; init; }
}

public sealed class ChatAttachment
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ChatAttachmentKind Kind { get; init; } = ChatAttachmentKind.File;
    public ImageSource? Image { get; init; }
    /// <summary>Where the app keeps it. The control only hands it back through <see cref="ChatThread.AttachmentInvoked"/>.</summary>
    public string? Path { get; init; }
    /// <summary>Bytes.</summary>
    public long? Size { get; init; }
    /// <summary>Intrinsic image size, used to reserve space before the image loads.</summary>
    public double? PixelWidth { get; init; }
    public double? PixelHeight { get; init; }
    public string? Alt { get; init; }
}

public sealed record ChatReaction(string Emoji, int Count, bool Mine = false);

public sealed record ChatMessage
{
    public required string Id { get; init; }
    public required string AuthorId { get; init; }
    public string? Text { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ChatAttachment>? Attachments { get; init; }
    public IReadOnlyList<ChatReaction>? Reactions { get; init; }
    /// <summary>Delivery state of the current user's own messages.</summary>
    public ChatMessageStatus? Status { get; init; }
}

/// <summary>What the composer sends: its text and the paths of the files waiting to go.</summary>
public sealed record ChatDraft(string Text, IReadOnlyList<string> Files);

public sealed class ChatDraftEventArgs(ChatDraft draft) : EventArgs { public ChatDraft Draft { get; } = draft; }
public sealed class ChatReactEventArgs(string messageId, string emoji) : EventArgs { public string MessageId { get; } = messageId; public string Emoji { get; } = emoji; }
public sealed class ChatMessageEventArgs(string messageId) : EventArgs { public string MessageId { get; } = messageId; }
public sealed class ChatAttachmentEventArgs(ChatMessage message, ChatAttachment attachment) : EventArgs { public ChatMessage Message { get; } = message; public ChatAttachment Attachment { get; } = attachment; }

/// <summary>Words the composer shows or announces. Each has an English default.</summary>
public class ChatComposerLabels
{
    public string Send { get; init; } = "Send";
    public string Attach { get; init; } = "Attach files";
    public string Attachments { get; init; } = "Attachments";
    public Func<string, string> RemoveFile { get; init; } = name => $"Remove {name}";
}

/// <summary>Every word the thread shows or announces. Words with a count or names are functions, so an app can apply its own plural rules.</summary>
public sealed class ChatThreadLabels : ChatComposerLabels
{
    private static string First(string name) => name.Split(' ')[0];
    public string Sending { get; init; } = "Sending";
    public string Sent { get; init; } = "Sent";
    public string Delivered { get; init; } = "Delivered";
    public string Read { get; init; } = "Read";
    public string Failed { get; init; } = "Not delivered";
    public string Retry { get; init; } = "Retry";
    public Func<IReadOnlyList<string>, string> ReadBy { get; init; } = names => $"Read by {string.Join(", ", names)}";
    public string Today { get; init; } = "Today";
    public string Yesterday { get; init; } = "Yesterday";
    public Func<IReadOnlyList<string>, string> Typing { get; init; } = names => names.Count == 1 ? $"{First(names[0])} is typing" : names.Count == 2 ? $"{First(names[0])} and {First(names[1])} are typing" : "Several people are typing";
    public Func<int, string> NewMessages { get; init; } = count => $"{count} new {(count == 1 ? "message" : "messages")}";
    public string JumpToLatest { get; init; } = "Jump to latest";
    public string UnknownAuthor { get; init; } = "Unknown";
    public string AddReaction { get; init; } = "Add reaction";
    public string Reactions { get; init; } = "Reactions";
    public Func<string, string> ReactWith { get; init; } = emoji => $"React with {emoji}";
    public Func<string, int, bool, string> ReactionCount { get; init; } = (emoji, count, mine) => $"{emoji} {count}{(mine ? ", including you" : "")}";
}

// The thread's pure rules, kept apart from its visuals.
internal static class ChatRules
{
    public abstract record Row(string Key);
    public sealed record DayRow(string Key, string Label) : Row(Key);
    public sealed record MessageRow(string Key, ChatMessage Message, ChatParticipant Author, bool Mine, bool First, bool Last, int Index) : Row(Key);

    public static List<Row> Rows(IReadOnlyList<ChatMessage> messages, IReadOnlyDictionary<string, ChatParticipant> people, string currentUser, TimeSpan window, CultureInfo culture, ChatThreadLabels labels, DateTime now)
    {
        var rows = new List<Row>();
        DateTime? lastDay = null;
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var day = message.CreatedAt.Date;
            if (day != lastDay) { rows.Add(new DayRow($"day-{day:yyyy-M-d}", DayLabel(message.CreatedAt, culture, labels, now))); lastDay = day; }
            bool Joins(ChatMessage? other) => other is not null && other.AuthorId == message.AuthorId && other.CreatedAt.Date == day && (other.CreatedAt - message.CreatedAt).Duration() <= window;
            rows.Add(new MessageRow(message.Id, message, people.TryGetValue(message.AuthorId, out var author) ? author : new ChatParticipant { Id = message.AuthorId, Name = labels.UnknownAuthor }, message.AuthorId == currentUser, !Joins(i > 0 ? messages[i - 1] : null), !Joins(i + 1 < messages.Count ? messages[i + 1] : null), i));
        }
        return rows;
    }

    public static string DayLabel(DateTime date, CultureInfo culture, ChatThreadLabels labels, DateTime now)
    {
        int days = (int)Math.Round((now.Date - date.Date).TotalDays);
        if (days == 0) { return labels.Today; }
        if (days == 1) { return labels.Yesterday; }
        if (days is > 1 and < 7) { return date.ToString("dddd", culture); }
        return date.ToString(date.Year == now.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy", culture);
    }

    /// <summary>Who has read up to each message. A reader's avatar under their own message says nothing new, so it stays hidden until they read past it.</summary>
    public static Dictionary<string, List<ChatParticipant>> Receipts(IReadOnlyList<ChatMessage> messages, IReadOnlyDictionary<string, string>? readBy, IReadOnlyDictionary<string, ChatParticipant> people, string currentUser)
    {
        var map = new Dictionary<string, List<ChatParticipant>>();
        if (readBy is null) { return map; }
        foreach (var (person, messageId) in readBy)
        {
            if (!people.TryGetValue(person, out var who) || person == currentUser) { continue; }
            var read = messages.FirstOrDefault(message => message.Id == messageId);
            if (read is null || read.AuthorId == person) { continue; }
            if (!map.TryGetValue(messageId, out var list)) { map[messageId] = list = []; }
            list.Add(who);
        }
        return map;
    }

    public static ChatMessage Toggle(ChatMessage message, string emoji)
    {
        var list = (message.Reactions ?? []).ToList();
        int at = list.FindIndex(entry => entry.Emoji == emoji);
        if (at < 0) { list.Add(new ChatReaction(emoji, 1, true)); }
        else if (list[at].Mine) { list[at] = list[at] with { Count = list[at].Count - 1, Mine = false }; }
        else { list[at] = list[at] with { Count = list[at].Count + 1, Mine = true }; }
        return message with { Reactions = list.Where(entry => entry.Count > 0).ToList() };
    }

    public static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{Math.Round(bytes / 1024d)} KB" : $"{bytes / (1024d * 1024d):0.0} MB";
}
