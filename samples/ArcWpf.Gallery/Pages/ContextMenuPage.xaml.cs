using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ContextMenuPage : UserControl
{
    public ContextMenuPage()
    {
        InitializeComponent();
        ArcWpf.Controls.ContextMenu.SetMenu(Target, Menu);
        Menu.ItemInvoked += (_, e) => Status.Text = $"已执行：{e.Item.Label}";
    }
}
