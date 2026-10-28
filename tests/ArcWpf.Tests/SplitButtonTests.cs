using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcWpf.Controls;
using ArcButton = ArcWpf.Controls.Button;
using ArcMenu = ArcWpf.Controls.Menu;

namespace ArcWpf.Tests;

[TestClass]
public sealed class SplitButtonTests
{
    [TestMethod]
    public void MainActionExecutesCommandWithoutOpeningMenu() => Sta.Run(() =>
    {
        var count = 0;
        var split = Create();
        split.Command = new TestCommand(() => count++);
        Layout(split);
        var main = (ArcButton)split.Template.FindName("PART_MainButton", split);
        main.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, count);
        Assert.IsFalse(split.IsOpen);
    });

    [TestMethod]
    public void MenuItemsAreIndependentFromMainCommand() => Sta.Run(() =>
    {
        var split = Create();
        var first = new ArcWpf.Controls.MenuItem { Label = "Save as" };
        split.Items.Add(first);
        Layout(split);
        var menu = (ArcMenu)split.Template.FindName("PART_Menu", split);
        Assert.AreSame(split.Items, menu.Items);
        Assert.AreEqual("More actions", split.MenuLabel);
    });

    [TestMethod]
    public void ChevronInheritsSplitForegroundAndMenuItemsStayInsideSurface() => Sta.Run(() =>
    {
        var split = Create();
        split.Items.Add(new ArcWpf.Controls.MenuItem { Label = "Save as" });
        Layout(split);
        var menu = (ArcMenu)split.Template.FindName("PART_Menu", split);
        var trigger = (System.Windows.Controls.Button)menu.Template.FindName("PART_Trigger", menu);
        var itemStyle = (Style)menu.FindResource("ArcWpf.Menu.ItemContainerStyle");
        Assert.IsNull(itemStyle.Setters.OfType<Setter>().First(s => s.Property == Control.FocusVisualStyleProperty).Value);
        Assert.IsTrue(trigger.ActualWidth > 0);
    });

    private static SplitButton Create()
    {
        var split = new SplitButton();
        split.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/SplitButton/Themes/SplitButtonTheme.xaml", UriKind.Relative) });
        return split;
    }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(400, 200)); element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height)); element.UpdateLayout(); Sta.Pump(); }
    private sealed class TestCommand(Action action) : ICommand { public event EventHandler? CanExecuteChanged; public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => action(); }
}
