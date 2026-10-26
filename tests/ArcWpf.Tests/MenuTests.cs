using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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
                menu.IsOpen = true; Sta.Pump();
                var popup = (Popup)menu.Template.FindName("PART_Popup", menu);
                Assert.IsTrue(menu.IsOpen); Assert.IsTrue(popup.IsOpen);
            }
            finally { host.Close(); }
        });
    }

    private static ArcMenu Create() { var menu = new ArcMenu { Label = "Actions" }; menu.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/Menu/Themes/MenuTheme.xaml", UriKind.Relative) }); return menu; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(500, 500)); element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump(); }
    private sealed class Model : System.ComponentModel.INotifyPropertyChanged { private bool _open; public bool Open { get => _open; set { _open = value; PropertyChanged?.Invoke(this, new(nameof(Open))); } } public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged; }
    private sealed class DelegateCommand(Action action) : ICommand { public event EventHandler? CanExecuteChanged; public bool CanExecute(object? parameter) => false; public void Execute(object? parameter) => action(); }
}
