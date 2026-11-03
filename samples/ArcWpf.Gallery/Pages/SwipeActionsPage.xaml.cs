using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SwipeActionsPage : UserControl
{
    private static readonly (string Sender, string Time, string Subject, string Preview, string From, string To)[] Messages =
    [
        ("Emma Collins", "9:41", "Atlas launch checklist", "QA signed off. Two billing blockers left before Thursday.", "#C7B299", "#7A6A58"),
        ("Marcus Johnson", "8:15", "Invoice INV-2048 from Northwind", "$4,280.00 is due on October 3. The PDF is attached.", "#A8B9C9", "#5B6B7D"),
        ("Ava Mitchell", "Yesterday", "Research readout moved to Friday", "Room 4B, 2:00 to 3:30. Interview clips are in the deck.", "#D3B5BF", "#8C6577"),
        ("Sofia Ramirez", "Mon", "Design crit notes", "Navigation feedback, with an owner for each follow-up.", "#B7C7AE", "#6C7F62"),
    ];

    private SwipeActionsRow? _last;

    public SwipeActionsPage()
    {
        InitializeComponent();
        foreach (var message in Messages) { Inbox.Items.Add(BuildRow(message)); }
        UpdateCount();
    }

    private SwipeActionsRow BuildRow((string Sender, string Time, string Subject, string Preview, string From, string To) message)
    {
        var row = new SwipeActionsRow { RowLabel = message.Subject, Content = Body(message) };
        row.Leading.Add(new SwipeAction { Label = "Archive", Icon = "archive", Tone = SwipeActionTone.Accent, OnSelect = () => Remove(row, "Archived", message.Subject) });
        row.Trailing.Add(new SwipeAction { Label = "Snooze", Icon = "clock-3", OnSelect = () => Remove(row, "Snoozed", message.Subject) });
        row.Trailing.Add(new SwipeAction { Label = "Delete", Icon = "trash", Tone = SwipeActionTone.Danger, OnSelect = () => Remove(row, "Deleted", message.Subject) });
        return row;
    }

    private void Remove(SwipeActionsRow row, string verb, string subject)
    {
        row.Collapse();
        _last = row;
        Say($"{verb} “{subject}”");
        Undo.Visibility = Visibility.Visible;
        UpdateCount();
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_last is null) { return; }
        _last.Restore();
        Say($"Restored “{_last.RowLabel}”");
        Undo.Visibility = Visibility.Collapsed;
        _last = null;
        UpdateCount();
    }

    private void UpdateCount()
    {
        int left = Inbox.Items.OfType<SwipeActionsRow>().Count(row => !row.IsLeavingOrGone);
        Empty.Visibility = left == 0 ? Visibility.Visible : Visibility.Collapsed;
        Left.Inlines.Clear();
        var digit = new Run(left.ToString());
        digit.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        Left.Inlines.Add(digit);
        Left.Inlines.Add(new Run(" left to triage"));
    }

    private void Say(string text)
    {
        Status.Text = text;
        var rise = new TranslateTransform(0, 4);
        Status.RenderTransform = rise;
        rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.24)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        Status.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.24)));
    }

    private static FrameworkElement Body((string Sender, string Time, string Subject, string Preview, string From, string To) message)
    {
        var avatar = new Ellipse40(message.From, message.To);
        var time = Text(message.Time, 12, 16.8, "ArcWpf.Brush.TextMuted", false);
        time.VerticalAlignment = VerticalAlignment.Center;
        var sender = Text(message.Sender, 14, 19.6, "ArcWpf.Brush.Foreground", true);
        var top = new DockPanel { Height = 19.8 };
        DockPanel.SetDock(time, Dock.Right);
        top.Children.Add(time);
        top.Children.Add(sender);
        var preview = Text(message.Preview, 12, 16.8, "ArcWpf.Brush.TextMuted", false);
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        var subject = Text(message.Subject, 14, 19.6, "ArcWpf.Brush.Foreground", false);
        subject.TextTrimming = TextTrimming.CharacterEllipsis;
        subject.Margin = new Thickness(0, 2, 0, 0);
        var words = new StackPanel { Children = { top, subject, preview } };
        var body = new DockPanel();
        avatar.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(avatar, Dock.Left);
        body.Children.Add(avatar);
        body.Children.Add(words);
        return body;
    }

    private static TextBlock Text(string text, double size, double line, string brush, bool medium)
    {
        var block = new TextBlock { Text = text, FontSize = size, LineHeight = line, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = medium ? FontWeights.Medium : FontWeights.Normal };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    private sealed class Ellipse40 : Border
    {
        public Ellipse40(string from, string to)
        {
            Width = Height = 40;
            CornerRadius = new CornerRadius(20);
            VerticalAlignment = VerticalAlignment.Center;
            Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 45);
        }
    }
}
