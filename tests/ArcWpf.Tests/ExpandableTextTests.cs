using ArcWpf.Controls;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ExpandableTextTests
{
    [TestMethod] public void ExpandableTextTogglesState() => Sta.Run(() => { var c = new ExpandableText { Content = new TextBlock { Text = "Long text" }, Lines = 2 }; c.IsExpanded = true; Assert.IsTrue(c.IsExpanded); c.IsExpanded = false; Assert.IsFalse(c.IsExpanded); });
    [TestMethod] public void LabelsAreConfigurable() => Sta.Run(() => { var c = new ExpandableText { MoreLabel = "展开", LessLabel = "收起" }; Assert.AreEqual("展开", c.MoreLabel); Assert.AreEqual("收起", c.LessLabel); });
}
