using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class UserMenuPage : UserControl
{
    // lucide-react UserRound, Settings and CreditCard on a 24 grid, as in Arc's usage example.
    private const string UserRound = "M7,8 a5,5 0 1 0 10,0 a5,5 0 1 0 -10,0 M20,21 a8,8 0 0 0 -16,0";
    private const string Settings = "M12.22,2 h-.44 a2,2 0 0 0 -2,2 v.18 a2,2 0 0 1 -1,1.73 l-.43,.25 a2,2 0 0 1 -2,0 l-.15,-.08 a2,2 0 0 0 -2.73,.73 l-.22,.38 a2,2 0 0 0 .73,2.73 l.15,.1 a2,2 0 0 1 1,1.72 v.51 a2,2 0 0 1 -1,1.74 l-.15,.09 a2,2 0 0 0 -.73,2.73 l.22,.38 a2,2 0 0 0 2.73,.73 l.15,-.08 a2,2 0 0 1 2,0 l.43,.25 a2,2 0 0 1 1,1.73 V20 a2,2 0 0 0 2,2 h.44 a2,2 0 0 0 2,-2 v-.18 a2,2 0 0 1 1,-1.73 l.43,-.25 a2,2 0 0 1 2,0 l.15,.08 a2,2 0 0 0 2.73,-.73 l.22,-.39 a2,2 0 0 0 -.73,-2.73 l-.15,-.08 a2,2 0 0 1 -1,-1.74 v-.5 a2,2 0 0 1 1,-1.74 l.15,-.09 a2,2 0 0 0 .73,-2.73 l-.22,-.38 a2,2 0 0 0 -2.73,-.73 l-.15,.08 a2,2 0 0 1 -2,0 l-.43,-.25 a2,2 0 0 1 -1,-1.73 V4 a2,2 0 0 0 -2,-2 z M9,12 a3,3 0 1 0 6,0 a3,3 0 1 0 -6,0";
    private const string CreditCard = "M4,5 h16 a2,2 0 0 1 2,2 v10 a2,2 0 0 1 -2,2 h-16 a2,2 0 0 1 -2,-2 v-10 a2,2 0 0 1 2,-2 z M2,10 h20";

    public UserMenuPage()
    {
        InitializeComponent();
        var user = new UserMenuUser { Name = "Emma Collins", Email = "emma@example.com", Plan = "Pro" };
        MainMenu.User = user;
        AddAccountItems(MainMenu);
        // A returned task keeps the menu open with a spinner until it settles.
        MainMenu.SignOutAsync = async () => { MainNote.Text = "Signing out…"; await Task.Delay(1400); MainNote.Text = "Signed out"; };
        MainMenu.ThemeChanged += (_, _) => ApplyTheme(MainMenu, MainNote);
        MainMenu.ItemInvoked += (_, e) => MainNote.Text = $"Opened {e.Item.Label}";

        StatusMenu.User = user;
        AddAccountItems(StatusMenu);
        StatusMenu.SignOutKeys = "⇧ ⌘ Q";
        StatusMenu.StatusChanged += (_, _) => StatusNote.Text = $"Status: {StatusMenu.Status}";
        StatusMenu.ThemeChanged += (_, _) => ApplyTheme(StatusMenu, StatusNote);
        StatusMenu.ItemInvoked += (_, e) => StatusNote.Text = $"Opened {e.Item.Label}";
    }

    private static void AddAccountItems(UserMenu menu)
    {
        menu.Items.Add(new UserMenuItem { Label = "Profile", Icon = Icon(UserRound) });
        menu.Items.Add(new UserMenuItem { Label = "Settings", Icon = Icon(Settings), Keys = "⌘ ," });
        menu.Items.Add(new UserMenuItem { Label = "Billing", Icon = Icon(CreditCard) });
    }

    // onThemeChange only reports; the app applies it. System keeps the Gallery's current palette.
    private static void ApplyTheme(UserMenu menu, TextBlock note)
    {
        var resources = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        if (menu.Theme == UserMenuTheme.Dark) resources.Mode = ArcThemeMode.Dark;
        else if (menu.Theme == UserMenuTheme.Light) resources.Mode = ArcThemeMode.Light;
        note.Text = $"Theme: {menu.Theme}";
    }

    private static Viewbox Icon(string data)
    {
        var path = new Path { Data = Geometry.Parse(data), Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 2.625, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        path.SetBinding(Shape.StrokeProperty, new Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = RelativeSource.Self });
        return new Viewbox { Width = 16, Height = 16, Child = path };
    }
}
