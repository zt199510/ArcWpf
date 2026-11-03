using System.Threading.Tasks;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ExpandingButtonGroupPage : UserControl
{
    public ExpandingButtonGroupPage()
    {
        InitializeComponent();
        Mail.Items =
        [
            new() { Id = "reply", Label = "Reply", DoneLabel = "Replied", Icon = "reply" },
            new() { Id = "forward", Label = "Forward", DoneLabel = "Forwarded", Icon = "forward" },
            new() { Id = "archive", Label = "Archive", DoneLabel = "Archived", Icon = "archive", Run = Slowly },
            new() { Id = "snooze", Label = "Snooze", DoneLabel = "Snoozed", Icon = "clock-3" },
            new() { Id = "delete", Label = "Delete", DoneLabel = "Deleted", Icon = "trash-2", Tone = ExpandingButtonGroupTone.Danger },
        ];
        File.Items =
        [
            new() { Id = "download", Label = "Download", DoneLabel = "Downloaded", Icon = "download" },
            new() { Id = "copy", Label = "Copy link", DoneLabel = "Copied", Icon = "link-2" },
            new() { Id = "rename", Label = "Rename", Icon = "pencil-line" },
        ];
        Page.Items =
        [
            new() { Id = "publish", Label = "Publish", DoneLabel = "Published", Icon = "globe" },
            new() { Id = "duplicate", Label = "Duplicate", DoneLabel = "Duplicated", Icon = "copy" },
            new() { Id = "pin", Label = "Pin", DoneLabel = "Pinned", Icon = "pin" },
            new() { Id = "delete", Label = "Delete", Icon = "trash-2", Tone = ExpandingButtonGroupTone.Danger, IsDisabled = true },
        ];
    }

    private static async Task<bool> Slowly() { await Task.Delay(500); return true; }
}
