using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class SkeletonTests
{
    [TestMethod] public void ShapesAndSizeAreBindable() => Sta.Run(() => { var skeleton = new Skeleton { Shape = SkeletonShape.Circle, Width = 40, Height = 40 }; Assert.AreEqual(SkeletonShape.Circle, skeleton.Shape); Assert.AreEqual(40d, skeleton.Width); });
    [TestMethod] public void ShimmerCanBeDisabled() => Sta.Run(() => { var skeleton = new Skeleton { IsShimmering = false }; Assert.IsFalse(skeleton.IsShimmering); });
    [TestMethod] public void DefaultShapeIsRectangle() => Sta.Run(() => Assert.AreEqual(SkeletonShape.Rectangle, new Skeleton().Shape));
}
