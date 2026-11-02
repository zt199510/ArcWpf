using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class ToggleGroupTests
{
    private static ToggleGroup Group(ToggleGroupType type, int count = 3)
    {
        var group = new ToggleGroup { Type = type };
        ArcMotion.SetLevel(group, ArcMotionLevel.Off);
        for (int i = 0; i < count; i++) group.Items.Add(new ToggleGroupItem { Value = $"v{i}", Icon = "M3,5 h18" });
        return group;
    }
    private static void Shown(FrameworkElement content, Action body)
    {
        var window = new Window { Width = 400, Height = 300, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try { window.Show(); window.UpdateLayout(); Sta.Pump(); body(); }
        finally { window.Close(); }
    }

    [TestMethod]
    public void SingleGroupKeepsOneSelection() => Sta.Run(() =>
    {
        var g = new ToggleGroup { Type = ToggleGroupType.Single };
        var a = new ToggleGroupItem { Value = "a" };
        var b = new ToggleGroupItem { Value = "b" };
        g.Items.Add(a); g.Items.Add(b);
        a.IsSelected = true; b.IsSelected = true;
        Assert.IsFalse(a.IsSelected);
        Assert.IsTrue(b.IsSelected);
    });

    [TestMethod]
    public void MultipleGroupAllowsIndependentSelection() => Sta.Run(() =>
    {
        var g = new ToggleGroup { Type = ToggleGroupType.Multiple };
        var a = new ToggleGroupItem { Value = "a" };
        var b = new ToggleGroupItem { Value = "b" };
        g.Items.Add(a); g.Items.Add(b);
        a.IsSelected = true; b.IsSelected = true;
        Assert.IsTrue(a.IsSelected);
        Assert.IsTrue(b.IsSelected);
    });

    [TestMethod]
    public void MatchesSourceGeometry() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Multiple);
        Shown(g, () =>
        {
            Assert.AreEqual(120, g.ActualWidth, "3 x 36 + 2 gaps of 2 + padding 3 + border 1 on both sides");
            Assert.AreEqual(44, g.ActualHeight);
            var second = (ToggleGroupItem)g.Items[1];
            Assert.AreEqual(36, second.ActualHeight);
            Assert.AreEqual(42, second.TransformToAncestor(g).Transform(new Point()).X);
        });
    });

    [TestMethod]
    public void SmallSizeUsesSmallItems() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Single);
        g.Size = ToggleGroupSize.Small;
        Shown(g, () =>
        {
            var first = (ToggleGroupItem)g.Items[0];
            Assert.AreEqual(28, first.ActualHeight);
            Assert.AreEqual(30, first.ActualWidth, "padding 8 on both sides of a 14 icon, as in toggle-group.module.css");
        });
    });

    [TestMethod]
    public void PressingThePressedOptionKeepsItInSingleMode() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Single);
        var first = (ToggleGroupItem)g.Items[0];
        Shown(g, () =>
        {
            g.Press(first);
            g.Press(first);
            Assert.IsTrue(first.IsSelected);
            Assert.AreEqual("v0", g.Value);
        });
    });

    [TestMethod]
    public void PressingTogglesInMultipleMode() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Multiple);
        var first = (ToggleGroupItem)g.Items[0];
        var third = (ToggleGroupItem)g.Items[2];
        Shown(g, () =>
        {
            g.Press(first);
            g.Press(third);
            CollectionAssert.AreEquivalent(new[] { "v0", "v2" }, g.Values.ToArray());
            g.Press(first);
            Assert.IsFalse(first.IsSelected);
        });
    });

    [TestMethod]
    public void NeighboursJoinAcrossTheGap() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Multiple);
        var first = (ToggleGroupItem)g.Items[0];
        var second = (ToggleGroupItem)g.Items[1];
        Shown(g, () =>
        {
            first.IsSelected = true;
            second.IsSelected = true;
            Sta.Pump();
            var left = (Border)first.Template.FindName("PART_ItemFill", first);
            var right = (Border)second.Template.FindName("PART_ItemFill", second);
            Assert.AreEqual(-2, left.Margin.Right);
            Assert.AreEqual(0, left.CornerRadius.TopRight);
            Assert.AreEqual(15, left.CornerRadius.TopLeft);
            Assert.AreEqual(0, right.CornerRadius.TopLeft);
            Assert.AreEqual(1, right.Opacity);
        });
    });

    [TestMethod]
    public void SingleFillSitsUnderThePressedOption() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Single);
        Shown(g, () =>
        {
            ((ToggleGroupItem)g.Items[2]).IsSelected = true;
            Sta.Pump();
            var fill = (FrameworkElement)g.Template.FindName("PART_Fill", g);
            Assert.AreEqual(Visibility.Visible, fill.Visibility);
            Assert.AreEqual(76, ((TranslateTransform)fill.RenderTransform).X);
        });
    });

    [TestMethod]
    public void ValueSelectsTheMatchingOption() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Single);
        Shown(g, () =>
        {
            g.Value = "v1";
            Assert.IsTrue(((ToggleGroupItem)g.Items[1]).IsSelected);
        });
    });

    [TestMethod]
    public void OnlyOneItemIsATabStop() => Sta.Run(() =>
    {
        var g = Group(ToggleGroupType.Single);
        Shown(g, () =>
        {
            ((ToggleGroupItem)g.Items[1]).IsSelected = true;
            Sta.Pump();
            Assert.AreEqual(1, g.Items.Cast<ToggleGroupItem>().Count(i => i.IsTabStop));
            Assert.IsTrue(((ToggleGroupItem)g.Items[1]).IsTabStop);
        });
    });
}
