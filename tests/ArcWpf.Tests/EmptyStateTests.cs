using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;
namespace ArcWpf.Tests;

[TestClass]
public sealed class EmptyStateTests
{
    [TestMethod]
    public void DefaultsFollowSource() => Sta.Run(() =>
    {
        var state = new EmptyState();
        Assert.AreEqual(string.Empty, state.Title);
        Assert.IsNull(state.Icon);
        Assert.IsNull(state.Action);
        Assert.AreEqual(1.5, state.IconStrokeThickness);
    });

    [TestMethod]
    public void CopyChangeReplacesLineWithMotionOff() => Sta.Run(() =>
    {
        var state = new EmptyState { Title = "No saved views", Description = "Create a view." };
        ArcMotion.SetLevel(state, ArcMotionLevel.Off);
        var window = new System.Windows.Window { Content = state, Width = 420, Height = 300, ShowInTaskbar = false };
        window.Show();
        Sta.Pump();
        state.Title = "View created";
        var title = (Grid)state.Template.FindName("PART_Title", state);
        var texts = title.Children.OfType<TextBlock>().ToList();
        Assert.AreEqual(1, texts.Count);
        Assert.AreEqual("View created", texts[0].Text);
        window.Close();
    });

    [TestMethod]
    public void NullIconDrawsFolder() => Sta.Run(() =>
    {
        var state = new EmptyState();
        var window = new System.Windows.Window { Content = state, Width = 420, Height = 300, ShowInTaskbar = false };
        window.Show();
        Sta.Pump();
        var glyphs = (Grid)state.Template.FindName("PART_Glyphs", state);
        Assert.AreSame(EmptyState.FolderIcon, ((System.Windows.Shapes.Path)glyphs.Children[0]).Data);
        window.Close();
    });
}
