using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
using ArcMenuItem = ArcWpf.Controls.MenuItem;

namespace ArcWpf.Gallery.Pages;

public partial class MenuPage : UserControl
{
    public MenuPage()
    {
        InitializeComponent();
        ActionsMenu.Items.Add(new ArcMenuItem { Label = "Rename" });
        ActionsMenu.Items.Add(new ArcMenuItem { Label = "Duplicate" });
        ActionsMenu.Items.Add(new ArcMenuItem { Label = "Delete", IsDestructive = true, SeparatorBefore = true });
        SecondaryMenu.Items.Add(new ArcMenuItem { Label = "Move to folder" });
        SecondaryMenu.Items.Add(new ArcMenuItem { Label = "Archive", IsEnabled = false });
    }

    private void OnItemInvoked(object sender, MenuItemInvokedEventArgs e) => ResultText.Text = $"已选择：{e.Item.Label}";
}
