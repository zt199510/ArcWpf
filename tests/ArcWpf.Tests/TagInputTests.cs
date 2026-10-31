using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class TagInputTests
{
    [TestMethod] public void TagsCollectionIsBindable() => Sta.Run(() => { var input = new TagInput(); input.Tags.Add("Design"); Assert.HasCount(1, input.Tags); });
    [TestMethod] public void MaxTagsIsConfigurable() => Sta.Run(() => { var input = new TagInput { MaxTags = 3 }; Assert.AreEqual(3, input.MaxTags); });
    [TestMethod] public void ReadOnlyCanBeEnabled() => Sta.Run(() => { var input = new TagInput { IsReadOnly = true }; Assert.IsTrue(input.IsReadOnly); });
}
