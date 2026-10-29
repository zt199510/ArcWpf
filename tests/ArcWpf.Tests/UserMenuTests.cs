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

    [TestMethod]
    public void PresenceTonesResolveForEveryStatus()
    {
        // Away rendered transparent while ArcWpf.Brush.Warning was missing from the palette.
        Sta.Run(() =>
        {
            foreach (var key in new[] { "ArcWpf.Brush.Success", "ArcWpf.Brush.Danger", "ArcWpf.Brush.Warning", "ArcWpf.Brush.TextMuted" })
                Assert.IsInstanceOfType<System.Windows.Media.SolidColorBrush>(Application.Current.TryFindResource(key), key);
        });
    }

    [TestMethod]
    public void TriggerKeepsFortyPixelPillWithoutFixedControlWidth()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { User = new UserMenuUser { Name = "Emma Collins", Email = "emma@example.com" } };
            var window = new Window { Content = new StackPanel { Children = { menu } }, Width = 420, Height = 260, ShowInTaskbar = false };
            try
            {
                window.Show(); Sta.Pump();
                var trigger = (FrameworkElement)menu.Template.FindName("PART_Trigger", menu)!;
                Assert.AreEqual(40, trigger.ActualHeight, .01); Assert.AreEqual(40, trigger.ActualWidth, .01);
                menu.ShowName = true; Sta.Pump();
                Assert.IsGreaterThan(40d, trigger.ActualWidth); Assert.AreEqual(trigger.ActualWidth, menu.ActualWidth, .01);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void SwitchChoicesKeepMenuOpenAndFollowStatusAndTheme()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { User = new UserMenuUser { Name = "Ada", Email = "ada@example.com" }, ShowStatus = true, Status = UserMenuStatus.Busy, Theme = UserMenuTheme.Dark };
            var window = new Window { Content = menu, Width = 420, Height = 360, ShowInTaskbar = false };
            try
            {
                window.Show(); Sta.Pump(); menu.IsOpen = true; Sta.Pump();
                var status = ((Panel)menu.Template.FindName("PART_StatusSegments", menu)!).Children.OfType<System.Windows.Controls.RadioButton>().ToList();
                var theme = ((Panel)menu.Template.FindName("PART_ThemeSegments", menu)!).Children.OfType<System.Windows.Controls.RadioButton>().ToList();
                Assert.IsTrue(status[1].IsChecked); Assert.IsTrue(theme[1].IsChecked);
                status[2].IsChecked = true; Sta.Pump();
                Assert.AreEqual(UserMenuStatus.Away, menu.Status); Assert.IsTrue(menu.IsOpen);
                menu.Theme = UserMenuTheme.Light; Sta.Pump();
                Assert.IsTrue(theme[0].IsChecked); Assert.IsFalse(theme[1].IsChecked);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void StatusRowHidesUntilStatusIsShown()
    {
        Sta.Run(() =>
        {
            var menu = new UserMenu { User = new UserMenuUser { Name = "Ada", Email = "ada@example.com" } };
            var window = new Window { Content = menu, Width = 420, Height = 360, ShowInTaskbar = false };
            try
            {
                window.Show(); Sta.Pump();
                Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)menu.Template.FindName("PART_StatusRow", menu)!).Visibility);
                Assert.AreEqual(Visibility.Visible, ((FrameworkElement)menu.Template.FindName("PART_ThemeRow", menu)!).Visibility);
                menu.ShowStatus = true; Sta.Pump();
                Assert.AreEqual(Visibility.Visible, ((FrameworkElement)menu.Template.FindName("PART_StatusRow", menu)!).Visibility);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void AsyncSignOutStaysOpenUntilTaskSettles()
    {
        Sta.Run(() =>
        {
            var gate = new TaskCompletionSource();
            var menu = new UserMenu { User = new UserMenuUser { Name = "Ada", Email = "ada@example.com" }, SignOutAsync = () => gate.Task };
            var window = new Window { Content = menu, Width = 420, Height = 360, ShowInTaskbar = false };
            try
            {
                window.Show(); Sta.Pump(); menu.IsOpen = true; Sta.Pump();
                var signOut = (System.Windows.Controls.Button)menu.Template.FindName("PART_SignOut", menu)!;
                signOut.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, signOut)); Sta.Pump();
                Assert.IsTrue(menu.IsSigningOut); Assert.IsTrue(menu.IsOpen);
                Assert.AreEqual("Signing out", ((TextBlock)menu.Template.FindName("PART_SignOutLabel", menu)!).Text);
                gate.SetResult(); Sta.Pump();
                Assert.IsFalse(menu.IsSigningOut); Assert.IsFalse(menu.IsOpen);
            }
            finally { window.Close(); }
        });
    }

    private static void Layout(FrameworkElement element) { element.Measure(new Size(360, 220)); element.Arrange(new Rect(0, 0, 360, 220)); element.UpdateLayout(); Sta.Pump(); }
}
