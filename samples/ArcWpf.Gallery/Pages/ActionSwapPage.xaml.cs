using System.Threading.Tasks;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ActionSwapPage : UserControl
{
    public ActionSwapPage()
    {
        InitializeComponent();
        Swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send", Tone = ActionSwapTone.Neutral });
        Swap.States.Add(new ActionSwapState { Id = "sent", Label = "Sent", PendingLabel = "Sending", Tone = ActionSwapTone.Success, HoldFor = 1800 });
        Swap.Value = "idle"; Swap.OnAction = _ => Task.Delay(500);
    }
}
