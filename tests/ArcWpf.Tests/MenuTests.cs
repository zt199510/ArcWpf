using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcWpf.Controls;
using ArcMenuItem = ArcWpf.Controls.MenuItem;
using ArcMenu = ArcWpf.Controls.Menu;
using WpfButton = System.Windows.Controls.Button;

namespace ArcWpf.Tests;

[TestClass]
public sealed class MenuTests
{
    [TestMethod]
    public void DefaultsAndBindingMatchContract() => Sta.Run(() =>
    {
        var menu = new ArcMenu { Label = "Actions" };
        Assert.IsFalse(menu.IsOpen);
        Assert.IsNotNull(menu.Items);
        Assert.IsTrue(((FrameworkPropertyMetadata)ArcMenu.IsOpenProperty.GetMetadata(typeof(ArcMenu))).BindsTwoWayByDefault);
        var model = new Model();
        var binding = menu.SetBinding(ArcMenu.IsOpenProperty, new Binding(nameof(Model.Open)) { Source = model });
        model.Open = true;
        Assert.IsTrue(menu.IsOpen);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(menu, ArcMenu.IsOpenProperty));
    });

    [TestMethod]
    public void TemplateReservesTriggerAndPopupWithArcGeometry() => Sta.Run(() =>
    {
        var menu = Create(); Layout(menu);
        var trigger = (WpfButton)menu.Template.FindName("PART_Trigger", menu);
        var popup = (Popup)menu.Template.FindName("PART_Popup", menu);
        var surface = (Border)menu.Template.FindName("PART_Surface", menu);
        Assert.AreEqual("Actions", menu.Label);
        Assert.IsTrue(popup.StaysOpen);
        Assert.AreEqual(192d, surface.MinWidth);
        Assert.AreEqual(new CornerRadius(26), surface.CornerRadius);
        Assert.AreEqual(5d, surface.Padding.Left);
    });

    [TestMethod]
    public void ItemsKeepOrderAndInvocationRaisesTypedEvent() => Sta.Run(() =>
    {
        var menu = Create();
        var first = new ArcMenuItem { Label = "Rename" };
        var second = new ArcMenuItem { Label = "Delete", IsDestructive = true, SeparatorBefore = true };
        menu.Items.Add(first); menu.Items.Add(second);
        ArcMenuItem? invoked = null;
        menu.ItemInvoked += (_, e) => invoked = e.Item;
        Assert.AreEqual("Rename", menu.Items[0].Label);
        Assert.IsTrue(menu.Items[1].IsDestructive && menu.Items[1].SeparatorBefore);
        menu.RaiseEvent(new MenuItemInvokedEventArgs(ArcMenu.ItemInvokedEvent, menu, second));
        Assert.AreSame(second, invoked);
    });

    [TestMethod]
    public void DisabledAndCommandItemsExposeNativeSemantics() => Sta.Run(() =>
    {
        var menu = Create(); var executed = false;
        var item = new ArcMenuItem { Label = "Archive", IsEnabled = false, Command = new DelegateCommand(() => executed = true) };
        menu.Items.Add(item);
        Assert.IsFalse(item.IsEnabled); Assert.IsFalse(item.Command!.CanExecute(null)); Assert.IsFalse(executed);
    });

    [TestMethod]
    public void GalleryAndConsumerConstructWithoutOpeningWindows() => Sta.Run(() =>
    {
        var page = new ArcWpf.Gallery.Pages.MenuPage(); Assert.IsNotNull(page.Content);
        var consumer = new ArcWpf.Consumer.MainWindow(); Assert.IsNotNull(consumer.Content); consumer.Close();
    });

    [TestMethod]
    public void PopupRemainsPresentedAfterTriggerClick()
    {
        Sta.Run(() =>
        {
            var menu = Create(); menu.Items.Add(new ArcMenuItem { Label = "Rename" });
            var host = new Window { Width = 400, Height = 240, Content = menu, ShowInTaskbar = false };
            try
            {
                host.Show(); host.UpdateLayout();
                // Go through the real trigger: a cast to the wrong Button type once left Click unwired.
                var trigger = (WpfButton)menu.Template.FindName("PART_Trigger", menu);
                trigger.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, trigger)); Sta.Pump();
                var popup = (Popup)menu.Template.FindName("PART_Popup", menu);
                Assert.IsTrue(menu.IsOpen); Assert.IsTrue(popup.IsOpen);
                Assert.AreSame(trigger, popup.PlacementTarget);
                var surface = (Border)menu.Template.FindName("PART_Surface", menu);
                Assert.IsGreaterThanOrEqualTo(trigger.PointToScreen(new Point(0, trigger.ActualHeight)).Y, surface.PointToScreen(new Point(0, 0)).Y, "Popup opens below the trigger, not over it.");
                trigger.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, trigger)); Sta.Pump();
                Assert.IsFalse(menu.IsOpen);
                WaitUntil(() => !popup.IsOpen);
                Assert.IsFalse(popup.IsOpen, "The popup closes once the exit fade ends.");
            }
            finally { host.Close(); }
        });
    }

    [TestMethod]
    public void ItemContainersUseSharedHighlightWithoutSystemSelectionTemplate() => Sta.Run(() =>
    {
        var menu = Create();
        menu.Items.Add(new ArcMenuItem { Label = "Rename" });
        Layout(menu);
        var style = (Style)menu.FindResource("ArcWpf.Menu.ItemContainerStyle");
        Assert.IsTrue(style.Setters.OfType<Setter>().Any(setter => setter.Property == Control.OverridesDefaultStyleProperty && Equals(setter.Value, true)));
        Assert.IsNull(style.Setters.OfType<Setter>().First(setter => setter.Property == Control.FocusVisualStyleProperty).Value);
    });

    [TestMethod]
    public void HighlightCoversSelectedRowAndLabelsStartAtItemPadding()
    {
        Sta.Run(() =>
        {
            var menu = Create();
            menu.Items.Add(new ArcMenuItem { Label = "Rename" });
            menu.Items.Add(new ArcMenuItem { Label = "Duplicate" });
            menu.Items.Add(new ArcMenuItem { Label = "Delete", IsDestructive = true, SeparatorBefore = true });
            var host = new Window { Width = 400, Height = 320, Content = menu, ShowInTaskbar = false };
            try
            {
                host.Show(); host.UpdateLayout();
                menu.IsOpen = true; Sta.Pump();
                var list = (ListBox)menu.Template.FindName("PART_ItemsHost", menu);
                var highlight = (Border)menu.Template.FindName("PART_Highlight", menu);
                list.SelectedIndex = 2; Sta.Pump();
                var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(2);
                var row = (FrameworkElement)container.Template.FindName("PART_Row", container);
                // Measured on screen: a stretch-aligned highlight used to sit re-centered in the list and over the separator.
                // Rows enter with a -4 offset; wait for that to settle, then the highlight must already be where the row rests.
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while ((container.RenderTransform is TranslateTransform { Y: not 0 } || highlight.RenderTransform.HasAnimatedProperties) && DateTime.UtcNow < deadline) Sta.Pump();
                var rowTop = row.PointToScreen(new Point(0, 0));
                var shapeTop = highlight.PointToScreen(new Point(0, 0));
                Assert.AreEqual(rowTop.Y, shapeTop.Y, .5);
                Assert.AreEqual(rowTop.X, shapeTop.X, .5);
                Assert.AreEqual(row.ActualHeight, highlight.ActualHeight, .5);
                Assert.AreEqual(row.ActualWidth, highlight.ActualWidth, .5);
                Assert.IsLessThan(container.ActualHeight, row.ActualHeight, "The separator sits above the row, outside the highlight.");
                // No icon: the label starts at the 11px item padding, not after an empty icon column.
                var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var label = FindText(first, "Rename")!;
                Assert.AreEqual(11d, label.TranslatePoint(new Point(0, 0), first).X, .5);
            }
            finally { menu.IsOpen = false; host.Close(); }
        });
    }

    [TestMethod]
    public void CloseFadesOutBeforeThePopupLeavesAndReopenReverses()
    {
        Sta.Run(() =>
        {
            var menu = Create(); menu.Items.Add(new ArcMenuItem { Label = "Rename" });
            var host = new Window { Width = 400, Height = 240, Content = menu, ShowInTaskbar = false };
            try
            {
                host.Show(); host.UpdateLayout();
                var popup = (Popup)menu.Template.FindName("PART_Popup", menu);
                var surface = (Border)menu.Template.FindName("PART_Surface", menu);
                menu.IsOpen = true; Sta.Pump();
                WaitUntil(() => !surface.HasAnimatedProperties);
                menu.IsOpen = false;
                // .menu[data-state="closed"]: still mounted, fading, and no longer hit-testable.
                Assert.IsTrue(popup.IsOpen); Assert.IsTrue(surface.HasAnimatedProperties); Assert.IsFalse(surface.IsHitTestVisible);
                menu.IsOpen = true; Sta.Pump();
                Assert.IsTrue(popup.IsOpen); Assert.IsTrue(surface.IsHitTestVisible);
                WaitUntil(() => !surface.HasAnimatedProperties);
                Assert.IsTrue(popup.IsOpen, "A reopen during the exit keeps the popup."); Assert.AreEqual(1d, surface.Opacity, .001);
                menu.IsOpen = false;
                WaitUntil(() => !popup.IsOpen);
                Assert.IsFalse(popup.IsOpen); Assert.AreEqual(1d, surface.Opacity, .001, "Reset for the next open.");
                ArcMotion.SetLevel(menu, ArcMotionLevel.Off);
                menu.IsOpen = true; Sta.Pump(); menu.IsOpen = false;
                Assert.IsFalse(popup.IsOpen, "Off closes at once.");
            }
            finally { menu.IsOpen = false; host.Close(); }
        });
    }

    internal static void WaitUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!done() && DateTime.UtcNow < deadline) Sta.Pump();
    }

    private static TextBlock? FindText(DependencyObject root, string text)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock block && block.Text == text) return block;
            if (FindText(child, text) is { } found) return found;
        }
        return null;
    }

    private static ArcMenu Create() { var menu = new ArcMenu { Label = "Actions" }; menu.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Menu/Themes/MenuTheme.xaml", UriKind.Relative) }); return menu; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(500, 500)); element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump(); }
    private sealed class Model : System.ComponentModel.INotifyPropertyChanged { private bool _open; public bool Open { get => _open; set { _open = value; PropertyChanged?.Invoke(this, new(nameof(Open))); } } public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged; }
    private sealed class DelegateCommand(Action action) : ICommand { public event EventHandler? CanExecuteChanged; public bool CanExecute(object? parameter) => false; public void Execute(object? parameter) => action(); }
}


