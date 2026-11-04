using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ToolCallCardPage : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(900) };
    private int _step;

    public ToolCallCardPage()
    {
        InitializeComponent();
        Group.Calls = AiDemoData.Calls;
        Group.IsActive = false;
        ShowApproval(ToolCallStatus.Approval);
        _timer.Tick += (_, _) => Step();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void ShowApproval(ToolCallStatus status) => Approval.Call = new ToolCallData("refund", "Stripe", "Refund the second charge", status)
    {
        Message = "Refunds $48.00 to the card ending 4242",
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Approval] = "Wants to refund the second charge", [ToolCallStatus.Denied] = "Refund denied", [ToolCallStatus.Running] = "Refunding the second charge", [ToolCallStatus.Success] = "Refunded the second charge" },
        Input = [new("charge", "ch_3PqL2"), new("amount", "$48.00")],
    };

    private void OnRun(object sender, RoutedEventArgs e)
    {
        _step = 0;
        Group.IsActive = true;
        Group.IsOpen = null;
        Group.Calls = [AiDemoData.Calls[0] with { Status = ToolCallStatus.Running, Output = null, DurationMs = null }];
        _timer.Start();
    }

    private void Step()
    {
        _step++;
        var calls = AiDemoData.Calls;
        Group.Calls = _step switch
        {
            1 => [calls[0], calls[1] with { Status = ToolCallStatus.Running, Output = null, DurationMs = null, Summary = null }],
            2 => [calls[0], calls[1]],
            _ => calls,
        };
        if (_step >= 3) { _timer.Stop(); Group.IsActive = false; }
    }

    private void OnRetry(object? sender, ToolCallEventArgs e) => Log.Text = $"RetryRequested: {e.Call.Id}";
    private void OnAsk(object sender, RoutedEventArgs e) { ShowApproval(ToolCallStatus.Approval); Log.Text = ""; }
    private void OnAllow(object? sender, ToolCallEventArgs e) { ShowApproval(ToolCallStatus.Success); Log.Text = "ApproveRequested"; }
    private void OnDeny(object? sender, ToolCallEventArgs e) { ShowApproval(ToolCallStatus.Denied); Log.Text = "DenyRequested"; }
}
