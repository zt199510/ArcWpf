using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class ReorderableListTests
{
    private static ObservableCollection<ReorderableListItem> Items() =>
    [
        new() { Id = "one", Title = "Clarify onboarding", Detail = "Product brief" },
        new() { Id = "two", Title = "Review empty states", Detail = "Design system" },
        new() { Id = "three", Title = "Ship the update", Detail = "Release checklist" },
    ];

    [TestMethod]
    public void DefaultsFollowSource() => Sta.Run(() => Assert.AreEqual("Reorder items", new ReorderableList().Label));

    [TestMethod]
    public void ArrowKeysOnHandleSwapNeighbours() => Sta.Run(() =>
    {
        var items = Items();
        var list = new ReorderableList { ItemsSource = items };
        ArcMotion.SetLevel(list, ArcMotionLevel.Off);
        var window = new Window { Content = list, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            int moved = -1;
            list.ItemMoved += (_, e) => moved = e.NewIndex;
            var handle = FindHandle(list, 0);
            handle.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(handle)!, 0, Key.Down) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            CollectionAssert.AreEqual(new[] { "two", "one", "three" }, items.Select(item => item.Id).ToArray());
            Assert.AreEqual(1, moved);
            handle.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(handle)!, 0, Key.Up) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            CollectionAssert.AreEqual(new[] { "one", "two", "three" }, items.Select(item => item.Id).ToArray());
        }
        finally { window.Close(); Sta.Pump(); }
    });

    [TestMethod]
    public void HandleNameCarriesPosition() => Sta.Run(() =>
    {
        var list = new ReorderableList { ItemsSource = Items() };
        var window = new Window { Content = list, Width = 500, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            Assert.AreEqual("Reorder Review empty states. Position 2 of 3. Use arrow keys.", System.Windows.Automation.AutomationProperties.GetName(FindHandle(list, 1)));
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static System.Windows.Controls.Button FindHandle(ReorderableList list, int index) =>
        Descendants(list.Order[index]).OfType<System.Windows.Controls.Button>().First();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var inner in Descendants(child)) yield return inner;
        }
    }
}
