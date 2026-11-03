using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class EmojiPickerPage : UserControl
{
    public EmojiPickerPage() => InitializeComponent();
    private void OnPicked(object? sender, string emoji)
    {
        int caret = Message.CaretIndex;
        Message.Text = Message.Text.Insert(caret, emoji);
        Message.CaretIndex = caret + emoji.Length;
    }
}
