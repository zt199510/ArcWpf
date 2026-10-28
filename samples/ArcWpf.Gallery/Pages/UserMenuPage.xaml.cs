using System.Windows.Controls;
using System.Windows;
using ArcWpf.Controls;
using ArcWpf;
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
        Menu.ThemeChanged += (_, _) =>
        {
            var resources = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
            if (Menu.Theme == UserMenuTheme.Dark) resources.Mode = ArcThemeMode.Dark;
            else if (Menu.Theme == UserMenuTheme.Light) resources.Mode = ArcThemeMode.Light;
            Status.Text = $"主题：{Menu.Theme}";
        };
        Menu.ItemInvoked += (_, e) => Status.Text = $"已执行：{e.Item.Label}";
    }
}
