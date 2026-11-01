using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class TagInputTests
{
    [TestMethod] public void AddTrimsAndAppends() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release"] };
        Assert.IsTrue(input.Add("  web "));
        CollectionAssert.AreEqual(new[] { "release", "web" }, input.Tags!.ToArray());
    });

    [TestMethod] public void DuplicateIgnoringCaseIsRefused() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release"] };
        Assert.IsFalse(input.Add("Release"));
        Assert.IsFalse(input.Add("   "));
        Assert.HasCount(1, input.Tags!);
    });

    [TestMethod] public void RemoveDropsTheTag() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release", "web"] };
        input.Remove("release");
        CollectionAssert.AreEqual(new[] { "web" }, input.Tags!.ToArray());
    });
}
