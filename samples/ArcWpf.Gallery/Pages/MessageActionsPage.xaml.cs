using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class MessageActionsPage : UserControl
{
    private bool _failNext;

    public MessageActionsPage()
    {
        InitializeComponent();
        Reply.Text = "Most likely the planner now prefers the new index for ORDER BY created_at, then filters status = 'paid' row by row. With 4.2M orders and only 3% paid, that walks most of the table.";
        Reply.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.Foreground");
        Actions.Text = Reply.Text;
        Actions.ThanksMessage = "Simulated, nothing was sent.";
        Actions.RegenerateRequested += (_, _) => { Log.Text = "Regenerate"; SetGenerating(true); };
        Actions.StopRequested += (_, _) => { Log.Text = "Stop"; SetGenerating(false); };
        Actions.QuoteRequested += (_, text) => Log.Text = "Quote: " + text[..Math.Min(40, text.Length)] + "...";
        Actions.RatingChanged += (_, rating) => Log.Text = "Rating " + rating;
        Actions.FeedbackSubmitted += (_, feedback) => Log.Text = $"Feedback {feedback.Rating}: {string.Join(", ", feedback.Reasons)} {feedback.Note}";
        Actions.SubmitFeedback = async _ =>
        {
            await Task.Delay(500);
            if (_failNext) { _failNext = false; FailButton.Content = "Fail the next send"; throw new InvalidOperationException("Simulated failure"); }
        };
    }

    private void SetGenerating(bool on)
    {
        Actions.IsGenerating = on;
        GenerateButton.Content = on ? "Finish generating" : "Start generating";
    }

    private void OnGenerate(object sender, RoutedEventArgs e) => SetGenerating(!Actions.IsGenerating);

    private void OnFail(object sender, RoutedEventArgs e)
    {
        _failNext = !_failNext;
        FailButton.Content = _failNext ? "The next send will fail" : "Fail the next send";
    }
}
