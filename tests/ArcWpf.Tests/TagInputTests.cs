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

    // Picking a tag springs the ring; its transforms used to come from the template, where they are frozen, and the first pick threw.
    [TestMethod] public void PickingATagMovesAnAnimatableRing() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release", "web"] };
        Assert.IsTrue(input.ApplyTemplate());
        var ring = (System.Windows.Controls.Border)input.Template.FindName("PART_Ring", input)!;
        Assert.IsFalse(ring.RenderTransform.IsFrozen);
        input.Pick("web");
        Assert.AreEqual("web", input.Picked);
        input.Pick(null);
    });

    // The editor stays the panel's last child while chips are inserted before it (it is no longer pulled out and re-added on every change).
    [TestMethod] public void AddingKeepsTheEditorInPlace() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release"] };
        Assert.IsTrue(input.ApplyTemplate());
        var panel = (System.Windows.Controls.WrapPanel)input.Template.FindName("PART_Tags", input)!;
        var editor = (System.Windows.Controls.TextBox)input.Template.FindName("PART_Input", input)!;
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(editor);
        editor.Text = "web";
        Assert.IsTrue(input.Add(editor.Text));
        Assert.IsTrue(input.Add("docs"));
        Assert.AreSame(parent, System.Windows.Media.VisualTreeHelper.GetParent(editor));
        Assert.AreEqual(4, panel.Children.Count);
        Assert.AreSame(editor, panel.Children[^1]);
        CollectionAssert.AreEqual(new[] { "release", "web", "docs" }, input.Tags!.ToArray());
    });

    [TestMethod] public void RemoveDropsTheTag() => Sta.Run(() =>
    {
        var input = new TagInput { Tags = ["release", "web"] };
        input.Remove("release");
        CollectionAssert.AreEqual(new[] { "web" }, input.Tags!.ToArray());
    });
}
