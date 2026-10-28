using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcContextMenu = ArcWpf.Controls.ContextMenu;
using ArcMenuItem = ArcWpf.Controls.MenuItem;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ContextMenuTests
{
    [TestMethod]
    public void AttachedMenuOpensForOwnerAndUsesItems()
    {
        Sta.Run(() =>
        {
            var owner = new Border { Width = 120, Height = 80 };
            var menu = Create();
            ArcContextMenu.SetMenu(owner, menu);
            Assert.AreSame(menu, ArcContextMenu.GetMenu(owner));
            menu.OpenFor(owner);
            Layout(menu);
            Assert.IsTrue(menu.IsOpen);
            Assert.HasCount(2, menu.Items);
            var host = (ListBox)menu.Template.FindName("PART_ItemsHost", menu);
            Assert.IsNotNull(host.ItemTemplate);
        });
    }

    [TestMethod]
    public void KeyboardEscapeClosesAndItemInvocationRaisesEvent()
    {
        Sta.Run(() =>
        {
            var menu = Create();
            ArcMenuItem? invoked = null;
            menu.ItemInvoked += (_, e) => invoked = e.Item;
            var window = new Window { Content = menu, Width = 400, Height = 240, ShowInTaskbar = false };
            try
            {
                window.Show(); Layout(menu);
                menu.OpenAtMouse(); Sta.Pump();
                var host = (ListBox)menu.Template.FindName("PART_ItemsHost", menu);
                host.SelectedIndex = 1;
                host.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host)!, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.AreSame(menu.Items[1], invoked);
                menu.OpenAtMouse(); Sta.Pump();
                host.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.IsFalse(menu.IsOpen);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void DisabledItemsDoNotInvoke()
    {
        Sta.Run(() =>
        {
            var menu = Create(); menu.Items[0].IsEnabled = false;
            var invoked = false; menu.ItemInvoked += (_, _) => invoked = true;
            var window = new Window { Content = menu, Width = 400, Height = 240, ShowInTaskbar = false };
            try
            {
                window.Show(); menu.OpenAtMouse(); Layout(menu);
                var host = (ListBox)menu.Template.FindName("PART_ItemsHost", menu);
                host.SelectedIndex = 0;
                host.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host)!, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.IsFalse(invoked);
            }
            finally { window.Close(); }
        });
    }

    private static ArcContextMenu Create()
    {
        var menu = new ArcContextMenu();
        menu.Items.Add(new ArcMenuItem { Label = "Rename" });
        menu.Items.Add(new ArcMenuItem { Label = "Delete", IsDestructive = true });
        menu.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/ContextMenu/Themes/ContextMenuTheme.xaml", UriKind.Relative) });
        return menu;
    }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(400, 300)); element.Arrange(new Rect(0, 0, 400, 300)); element.UpdateLayout(); Sta.Pump(); }
}
