using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class SkeletonTests
{
    [TestMethod]
    public void DefaultsFollowSource() => Sta.Run(() =>
    {
        var skeleton = new Skeleton();
        Assert.AreEqual("Loading content", skeleton.Label);
        Assert.AreEqual(3, skeleton.Lines);
        Assert.IsFalse(skeleton.HasAvatar);
        Assert.IsTrue(skeleton.IsLoading);
        Assert.IsTrue(skeleton.IsBusy);
    });

    [TestMethod]
    public void LinesClampToOneThroughSix() => Sta.Run(() =>
    {
        var skeleton = new Skeleton { Lines = 0 };
        Assert.AreEqual(1, skeleton.Lines);
        skeleton.Lines = 9;
        Assert.AreEqual(6, skeleton.Lines);
    });

    [TestMethod]
    public void ContentShowsOnlyWhenNotLoading() => Sta.Run(() =>
    {
        var skeleton = new Skeleton { Content = "Loaded", IsLoading = false };
        Assert.IsFalse(skeleton.IsBusy);
        skeleton.IsLoading = true;
        Assert.IsTrue(skeleton.IsBusy);
        Assert.IsTrue(new Skeleton { IsLoading = false }.IsBusy);
    });
}
