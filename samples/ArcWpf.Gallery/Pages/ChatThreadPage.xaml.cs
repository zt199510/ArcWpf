using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ChatThreadPage : UserControl
{
    private readonly List<ChatMessage> _messages;
    private readonly ChatParticipant _chloe = new() { Id = "chloe", Name = "Chloe Nguyen" }, _daniel = new() { Id = "daniel", Name = "Daniel Kim" }, _me = new() { Id = "me", Name = "Emma Collins" };
    private bool _offline, _replied;
    private int _serial;

    public ChatThreadPage()
    {
        InitializeComponent();
        foreach (var (who, at) in new[] { (_chloe, 0), (_daniel, 1) })
        {
            var face = new Avatar { DisplayName = who.Name, Width = 28, Height = 28, FontSize = 10.64, BorderThickness = new Thickness(0) };
            var ring = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Padding = new Thickness(2), Child = face, Margin = new Thickness(at == 0 ? -2 : -12, 0, 0, 0) };
            ring.SetResourceReference(Border.BackgroundProperty, "ArcWpf.Brush.Surface");
            Faces.Children.Add(ring);
        }
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        _messages =
        [
            new ChatMessage { Id = "m1", AuthorId = "chloe", CreatedAt = yesterday.AddHours(16).AddMinutes(12), Text = "Hi! Our CSV export from the revenue dashboard stops at 10,000 rows. Is that a limit on our plan?" },
            new ChatMessage { Id = "m2", AuthorId = "me", CreatedAt = yesterday.AddHours(16).AddMinutes(20), Text = "Hi Chloe, thanks for flagging it. That isn't a plan limit. Looping in Daniel from our backend team." },
            new ChatMessage { Id = "m3", AuthorId = "daniel", CreatedAt = yesterday.AddHours(16).AddMinutes(41), Text = "Found it. The export job still used the old page size. A fix goes out tomorrow morning." },
            new ChatMessage { Id = "m4", AuthorId = "daniel", CreatedAt = today.AddHours(9).AddMinutes(18), Text = "Fix is live. Exports now stream up to one million rows.", Reactions = [new ChatReaction("🎉", 2)] },
            new ChatMessage { Id = "m4b", AuthorId = "daniel", CreatedAt = today.AddHours(9).AddMinutes(18), Attachments = [new ChatAttachment { Id = "a1", Name = "export-check-q3.csv", Size = (long)(2.4 * 1024 * 1024) }] },
            new ChatMessage { Id = "m5", AuthorId = "chloe", CreatedAt = today.AddHours(9).AddMinutes(31), Text = "All 48,210 rows came through. Thank you both!" },
            new ChatMessage { Id = "m6", AuthorId = "chloe", CreatedAt = today.AddHours(9).AddMinutes(32), Text = "The view from our offsite, since it's finally a calm day", Reactions = [new ChatReaction("❤️", 1, true)], Attachments = [new ChatAttachment { Id = "a2", Name = "offsite.png", Kind = ChatAttachmentKind.Image, Image = Sky(), PixelWidth = 800, PixelHeight = 1200, Alt = "A calm sky over water" }] },
            new ChatMessage { Id = "m7", AuthorId = "me", CreatedAt = today.AddHours(9).AddMinutes(34), Text = "So glad it worked. I'll close this out, reply here anytime.", Status = ChatMessageStatus.Delivered },
        ];
        Thread.Participants = [_chloe, _daniel, _me];
        Thread.Messages = _messages;
        Thread.ReadBy = new Dictionary<string, string> { ["chloe"] = "m7" };
        Thread.Reacted += (_, e) => { Replace(e.MessageId, message => ChatRules_Toggle(message, e.Emoji)); };
        Thread.Messages = _messages.ToList();
    }

    private static ChatMessage ChatRules_Toggle(ChatMessage message, string emoji)
    {
        var list = (message.Reactions ?? []).ToList();
        int at = list.FindIndex(entry => entry.Emoji == emoji);
        if (at < 0) { list.Add(new ChatReaction(emoji, 1, true)); }
        else if (list[at].Mine) { list[at] = list[at] with { Count = list[at].Count - 1, Mine = false }; }
        else { list[at] = list[at] with { Count = list[at].Count + 1, Mine = true }; }
        return message with { Reactions = list.Where(entry => entry.Count > 0).ToList() };
    }

    private void Replace(string id, Func<ChatMessage, ChatMessage> change)
    {
        int at = _messages.FindIndex(message => message.Id == id);
        if (at < 0) { return; }
        _messages[at] = change(_messages[at]);
        Thread.Messages = _messages.ToList();
    }

    private void OnOffline(object sender, RoutedEventArgs e)
    {
        _offline = !_offline;
        OfflineText.Text = _offline ? "Offline" : "Go offline";
    }

    private async void OnSent(object? sender, ChatDraftEventArgs e)
    {
        string id = $"sent-{++_serial}";
        _messages.Add(new ChatMessage { Id = id, AuthorId = "me", CreatedAt = DateTime.Now, Text = e.Draft.Text, Status = _offline ? ChatMessageStatus.Sending : ChatMessageStatus.Sent });
        Thread.Messages = _messages.ToList();
        if (_offline) { await Task.Delay(1400); Replace(id, message => message with { Status = ChatMessageStatus.Failed }); return; }
        await Task.Delay(600);
        Replace(id, message => message with { Status = ChatMessageStatus.Delivered });
        await Task.Delay(800);
        Thread.ReadBy = new Dictionary<string, string> { ["daniel"] = id, ["chloe"] = id };
        if (_replied) { return; }
        _replied = true;
        Thread.Typing = ["chloe"];
        await Task.Delay(1600);
        Thread.Typing = [];
        _messages.Add(new ChatMessage { Id = $"reply-{_serial}", AuthorId = "chloe", CreatedAt = DateTime.Now, Text = "Perfect, thank you. Should I expect the same for scheduled exports?" });
        Thread.Messages = _messages.ToList();
    }

    private static ImageSource Sky()
    {
        var drawing = new DrawingGroup();
        var sky = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#F4C9A6"), (Color)ColorConverter.ConvertFromString("#5E8FB5"), 90);
        var water = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#46708F"), (Color)ColorConverter.ConvertFromString("#21394D"), 90);
        drawing.Children.Add(new GeometryDrawing(sky, null, new RectangleGeometry(new Rect(0, 0, 800, 760))));
        drawing.Children.Add(new GeometryDrawing(water, null, new RectangleGeometry(new Rect(0, 760, 800, 440))));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xFB, 0xE9, 0xC6)), null, new EllipseGeometry(new Point(560, 420), 70, 70)));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
