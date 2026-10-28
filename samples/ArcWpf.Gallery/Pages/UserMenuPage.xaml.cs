using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class UserMenuPage : UserControl
{
    public UserMenuPage()
    {
        InitializeComponent();
        Menu.User = new UserMenuUser { Name = "Lin Chen", Email = "lin@example.com", Plan = "Pro plan" };
        Menu.Items.Add(new UserMenuItem { Label = "Account settings", Icon = "⚙", Keys = "⌘ ," });
        Menu.Items.Add(new UserMenuItem { Label = "Billing", Icon = "＄" });
        Menu.Items.Add(new UserMenuItem { Label = "Help center", Icon = "?" });
        Menu.StatusChanged += (_, _) => Status.Text = $"状态：{Menu.Status}";
        Menu.ThemeChanged += (_, _) => Status.Text = $"主题：{Menu.Theme}";
        Menu.ItemInvoked += (_, e) => Status.Text = $"已执行：{e.Item.Label}";
    }
}
