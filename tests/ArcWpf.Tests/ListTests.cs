using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ArcList = ArcWpf.Controls.List;
using ArcListItem = ArcWpf.Controls.ListItem;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ListTests
{
    [TestMethod]
    public void UsesSingleSelectionAndAccessibleLabel() => Sta.Run(() =>
    {
        var list = Create(); list.Label = "Project list"; list.ItemsSource = new ObservableCollection<ArcListItem> { new() { Id = "one", Title = "One" }, new() { Id = "two", Title = "Two" } }; Layout(list);
        Assert.AreEqual(SelectionMode.Single, list.SelectionMode); Assert.AreEqual("Project list", System.Windows.Automation.AutomationProperties.GetName(list)); Assert.AreEqual(2, list.Items.Count);
    });

    [TestMethod]
    public void SelectsItemWithNativeListBoxBehavior() => Sta.Run(() =>
    {
        var list = Create(); var item = new ArcListItem { Id = "one", Title = "One" }; list.Items.Add(item); Layout(list); list.SelectedIndex = 0;
        Assert.AreSame(item, list.SelectedItem);
    });

    [TestMethod]
    public void DisabledItemCannotBeEnabled() => Sta.Run(() =>
    {
        var list = Create(); list.ItemsSource = new[] { new ArcListItem { Id = "disabled", Title = "Disabled", IsEnabled = false } }; Layout(list);
        var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)!; Assert.IsFalse(container.IsEnabled);
    });

    private static ArcList Create() { var list = new ArcList(); list.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ArcWpf.Controls;component/List/Themes/ListTheme.xaml", UriKind.Relative) }); return list; }
    private static void Layout(FrameworkElement element) { element.Measure(new Size(600, 400)); element.Arrange(new Rect(0, 0, 600, 400)); element.UpdateLayout(); Sta.Pump(); }
}
