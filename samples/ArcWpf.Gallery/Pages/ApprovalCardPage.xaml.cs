using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ApprovalCardPage : UserControl
{
    private bool _failNext;
    private int _index;

    public ApprovalCardPage()
    {
        InitializeComponent();
        Card.Request = Requests(0);
        Card.SendDecision = async _ =>
        {
            await Task.Delay(900);
            if (_failNext) { _failNext = false; FailButton.Content = "Fail the next send"; throw new InvalidOperationException("Couldn't reach Deploy agent."); }
        };
        Card.Decided += (_, decision) => Log.Text = $"{decision.Outcome}{(decision.Edited ? ", edited" : "")}: {string.Join(", ", decision.Arguments.Select(pair => pair.Key + "=" + pair.Value))}";
        Card.RequestedAgain += (_, request) => Log.Text = "Requested again: " + request.Title.Replace("`", "");
    }

    private static ApprovalRequest Requests(int index) => (index % 2) switch
    {
        0 => new ApprovalRequest
        {
            AgentName = "Deploy agent", ModelName = "Claude Opus 5.5", ModelMark = AiDemoData.Claude, Asked = "2 min ago", ToolMark = AiDemoData.Vercel,
            Title = "Run `vercel deploy --prod`", Reason = "The preview passed all 214 checks and the release window opened at 14:00.", Risk = ApprovalRisk.High,
            Arguments = [new("Project", "northwind-web"), new("Commit", "8f3c2a1 Ship pricing v3") { IsEditable = false }, new("Target", "production"), new("Regions", "fra1, iad1")],
        },
        _ => new ApprovalRequest
        {
            AgentName = "Data agent", ModelName = "Gemini 4 Argon", ModelMark = AiDemoData.Gemini, Asked = "11 min ago", ToolMark = AiDemoData.Postgres,
            Title = "`UPDATE` 1,284 rows in PostgreSQL", Reason = "Moves legacy team plans to the new plan before the 02:00 billing run.", Risk = ApprovalRisk.Medium,
            Arguments = [new("Database", "billing-prod"), new("Statement", "UPDATE subscriptions SET plan = 'team'\nWHERE plan = 'team_legacy';") { IsMultiline = true }, new("Rows", "1,284") { IsMono = false, IsEditable = false }],
        },
    };

    private void OnFail(object sender, RoutedEventArgs e)
    {
        _failNext = !_failNext;
        FailButton.Content = _failNext ? "The next send will fail" : "Fail the next send";
    }

    private void OnNext(object sender, RoutedEventArgs e) => Card.Request = Requests(++_index);
}
