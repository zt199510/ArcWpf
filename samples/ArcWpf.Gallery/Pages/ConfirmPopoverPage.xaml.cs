using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ConfirmPopoverPage : UserControl
{
    public ConfirmPopoverPage()
    {
        InitializeComponent();
        Remove.ConfirmAction = context => Simulate(context, "The server didn't respond. Nobody was removed.", "Jordan was removed (simulated)");
        Delete.ConfirmAction = context => Simulate(context, "The server didn't respond. Nothing was changed.", "Atlas was deleted (simulated)");
    }

    private async Task Simulate(ConfirmPopoverContext context, string failure, string done)
    {
        bool fail = Fail.IsChecked == true;
        for (int step = 1; step <= 3; step++)
        {
            await Task.Delay(420, context.Cancellation);
            context.SetProgress(step / 3d);
            if (fail && step == 2) { Fail.IsChecked = false; throw new InvalidOperationException(failure); }
        }
        Log.Text = done;
    }
}
