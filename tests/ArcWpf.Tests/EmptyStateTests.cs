using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class EmptyStateTests
{
    [TestMethod] public void ContentPropertiesAreBindable() => Sta.Run(() => { var state = new EmptyState { Title = "Empty", Description = "Nothing" }; Assert.AreEqual("Empty", state.Title); Assert.AreEqual("Nothing", state.Description); });
    [TestMethod] public void ActionCanBeHidden() => Sta.Run(() => { var state = new EmptyState { IsActionVisible = false, ActionContent = "Create" }; Assert.IsFalse(state.IsActionVisible); });
    [TestMethod] public void AutomationPeerUsesTitle() => Sta.Run(() => { var state = new EmptyState { Title = "No projects" }; Assert.AreEqual("No projects", state.Title); });
}
