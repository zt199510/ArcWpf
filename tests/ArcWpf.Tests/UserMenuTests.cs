using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class UserMenuTests
{
    [TestMethod]
    public void UserMenuExposesIdentityAndItems()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { User = new UserMenuUser { Name = "Ada", Email = "ada@example.com" } };
            menu.Items.Add(new UserMenuItem { Label = "Settings" });
            var window = new Window { Content = menu, Width = 360, Height = 220, ShowInTaskbar = false };
            try { window.Show(); Layout(menu); Assert.AreEqual("Ada", menu.User!.Name); Assert.HasCount(1, menu.Items); Assert.IsNotNull(menu.Template.FindName("PART_Trigger", menu)); }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void StatusAndThemeEventsKeepMenuContract()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu(); var status = false; var theme = false;
            menu.StatusChanged += (_, _) => status = true; menu.ThemeChanged += (_, _) => theme = true;
            menu.SetStatus(UserMenuStatus.Busy); menu.SetTheme(UserMenuTheme.Dark);
            Assert.IsTrue(status); Assert.IsTrue(theme); Assert.AreEqual(UserMenuStatus.Busy, menu.Status); Assert.AreEqual(UserMenuTheme.Dark, menu.Theme);
        });
    }

    [TestMethod]
    public void InvokingEnabledItemRaisesEventAndCloses()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { IsOpen = true }; var item = new UserMenuItem { Label = "Settings" }; UserMenuItem? received = null;
            menu.ItemInvoked += (_, e) => received = e.Item;
            menu.Invoke(item);
            Assert.AreSame(item, received); Assert.IsFalse(menu.IsOpen);
        });
    }

    [TestMethod]
    public void TriggerOpensPopupAndEscapeClosesIt()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { User = new UserMenuUser { Name = "Ada", Email = "ada@example.com" }, ShowStatus = true };
            menu.Items.Add(new UserMenuItem { Label = "Settings" });
            var window = new Window { Content = menu, Width = 420, Height = 260, ShowInTaskbar = false };
            try
            {
                window.Show(); Layout(menu);
                var trigger = (System.Windows.Controls.Button)menu.Template.FindName("PART_Trigger", menu)!;
                trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Sta.Pump(); Assert.IsTrue(menu.IsOpen); Assert.IsTrue(((System.Windows.Controls.Primitives.Popup)menu.Template.FindName("PART_Popup", menu)!).IsOpen);
                menu.IsOpen = false; Sta.Pump(); Assert.IsFalse(menu.IsOpen);
            }
            finally { window.Close(); }
        });
    }

    private static void Layout(FrameworkElement element) { element.Measure(new Size(360, 220)); element.Arrange(new Rect(0, 0, 360, 220)); element.UpdateLayout(); Sta.Pump(); }
}
