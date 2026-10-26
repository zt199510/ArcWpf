using System.Windows.Automation.Peers;
using System.Windows.Controls;
namespace ArcWpf.Controls;
/// <summary>Decorative outgoing text; the ComboBox peer exposes the real selected value.</summary>
public sealed class SelectSnapshot:TextBlock
{
    protected override AutomationPeer? OnCreateAutomationPeer()=>null;
}
