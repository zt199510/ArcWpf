using System.Windows;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class IconMorphTests
{
    private static void Run(IconMorphName name, Action<IconMorph> action) => Sta.Run(() =>
    {
        var icon = new IconMorph { Icon = name, Size = 28, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = icon, Width = 300, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); action(icon); } finally { window.Close(); }
    });

    [TestMethod]
    public void TakesItsSizeAndIgnoresThePointer() => Run(IconMorphName.Menu, icon =>
    {
        Assert.AreEqual(28, icon.ActualWidth, .01);
        Assert.AreEqual(28, icon.ActualHeight, .01);
        Assert.IsFalse(icon.IsHitTestVisible);
    });

    [TestMethod]
    public void StateStartsTheSpringFromWhereItIs() => Run(IconMorphName.Plus, icon =>
    {
        icon.State = true;
        Assert.IsTrue(icon.ShapeForTest < .01, "the first frame has not moved yet");
        icon.FinishMotionForTest();
        Assert.AreEqual(1, icon.ShapeForTest, .001);
        icon.State = false;
        icon.FinishMotionForTest();
        Assert.AreEqual(0, icon.ShapeForTest, .001);
    });

    [TestMethod]
    public void InitialStateNeverAnimates() => Sta.Run(() =>
    {
        var icon = new IconMorph { Icon = IconMorphName.Bell, State = true };
        var window = new Window { Content = icon, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); Sta.Pump();
            Assert.AreEqual(1, icon.ShapeForTest, .001);
            Assert.AreEqual(1, icon.FadeForTest, .001);
            Assert.IsFalse(icon.IsPlayingOneShotForTest);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void OnlyTurningOnFiresBellAndHeart() => Run(IconMorphName.Heart, icon =>
    {
        icon.State = true;
        Assert.IsTrue(icon.IsPlayingOneShotForTest);
        icon.FinishMotionForTest();
        icon.State = false;
        Assert.IsFalse(icon.IsPlayingOneShotForTest);
    });

    [TestMethod]
    public void BellSwingFollowsTheKeyframes() => Run(IconMorphName.Bell, icon =>
    {
        Assert.AreEqual(0, icon.RingAngleForTest(0), .001);
        Assert.AreEqual(16, icon.RingAngleForTest(.15), .001);
        Assert.AreEqual(-13, icon.RingAngleForTest(.3), .001);
        Assert.AreEqual(0, icon.RingAngleForTest(.9), .001);
    });

    [TestMethod]
    public void OffMotionSnapsWithoutOneShots() => Run(IconMorphName.Bell, icon =>
    {
        ArcMotion.SetLevel(icon, ArcMotionLevel.Off);
        icon.State = true;
        Assert.AreEqual(1, icon.ShapeForTest, .001);
        Assert.IsFalse(icon.IsPlayingOneShotForTest);
    });

    [TestMethod]
    public void EveryIconRendersInBothStates()
    {
        foreach (var name in Enum.GetValues<IconMorphName>())
        {
            Run(name, icon =>
            {
                icon.FinishMotionForTest();
                icon.UpdateLayout();
                icon.State = true;
                icon.FinishMotionForTest();
                icon.UpdateLayout();
                Sta.Pump();
            });
        }
    }
}
