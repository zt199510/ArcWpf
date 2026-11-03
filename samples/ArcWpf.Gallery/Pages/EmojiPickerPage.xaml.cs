using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

    private void OnMessageChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized) return;
        MessagePlaceholder.Visibility = Message.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Send.IsEnabled = !string.IsNullOrWhiteSpace(Message.Text);
    }

    // Enter sends and Shift+Enter starts a new line. Enter that confirms an input method arrives as ImeProcessed, so it never sends.
    private void OnMessageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        e.Handled = true;
        SendMessage();
    }

    private void OnSend(object sender, RoutedEventArgs e) => SendMessage();

    private void SendMessage()
    {
        string text = Message.Text.Trim();
        if (text.Length == 0) return;
        Messages.Children.Add(MakeOwnMessage(text));
        Message.Clear();
        Message.Focus();
        MessageScroller.UpdateLayout();
        MessageScroller.ScrollToEnd();
    }

    private Grid MakeOwnMessage(string text)
    {
        var row = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new Ellipse
        {
            Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left,
            Fill = new LinearGradientBrush(Color.FromRgb(0x6F, 0x6C, 0xF0), Color.FromRgb(0xEE, 0x7A, 0x3C), new Point(.3, 0), new Point(.7, 1))
        });
        var body = new StackPanel();
        Grid.SetColumn(body, 1);
        body.Children.Add(new TextBlock { Text = "You", FontWeight = FontWeights.Medium, LineHeight = 19.6, LineStackingStrategy = LineStackingStrategy.BlockLineHeight });
        var line = new TextBox { Text = text, Style = (Style)FindResource("SentText") };
        AutomationProperties.SetName(line, "You: " + text);
        body.Children.Add(line);
        row.Children.Add(body);
        return row;
    }
}
