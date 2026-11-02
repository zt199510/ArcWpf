using System.Threading.Tasks;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ActionSwapPage : UserControl
{
    private const string Check = "M20,6 L9,17 L4,12";
    public ActionSwapPage()
    {
        InitializeComponent();
        Follow.States.Add(new ActionSwapState { Id = "follow", Label = "Follow", Icon = "M16,21 v-2 a4,4,0,0,0,-4,-4 H6 a4,4,0,0,0,-4,4 v2 M9,3 A4,4,0,1,1,9,11 A4,4,0,1,1,9,3 Z M19,8 V14 M22,11 H16", Tone = ActionSwapTone.Neutral });
        Follow.States.Add(new ActionSwapState { Id = "following", Label = "Following", Icon = Check, Tone = ActionSwapTone.Quiet });
        Library.States.Add(new ActionSwapState { Id = "add", Label = "Add to library", Icon = "M5,12 H19 M12,5 V19", Tone = ActionSwapTone.Quiet });
        Library.States.Add(new ActionSwapState { Id = "added", Label = "Added", Icon = Check, Tone = ActionSwapTone.Success });
        Library.OnAction = _ => Simulate(500);
        Invite.States.Add(new ActionSwapState { Id = "send", Label = "Send invite", Icon = "M14.536,21.686 a.5,.5,0,0,0,.937,-.024 l6.5,-19 a.496,.496,0,0,0,-.635,-.635 l-19,6.5 a.5,.5,0,0,0,-.024,.937 l7.93,3.18 a2,2,0,0,1,1.112,1.11 z M21.854,2.147 l-10.94,10.939", Tone = ActionSwapTone.Accent });
        Invite.States.Add(new ActionSwapState { Id = "sent", Label = "Sent", PendingLabel = "Sending", Icon = Check, Tone = ActionSwapTone.Success, HoldFor = 2400 });
        Invite.OnAction = next => next.Id == "sent" ? Simulate(900) : Task.CompletedTask;
    }
    private async Task Simulate(int milliseconds)
    {
        await Task.Delay(milliseconds);
        if (LostConnection.IsChecked == true) throw new System.InvalidOperationException("Simulated lost connection");
    }
}
