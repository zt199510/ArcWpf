using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TreeViewPage : UserControl
{
    public TreeViewPage()
    {
        InitializeComponent();
        var app = new TreeNode { Id = "app", Label = "app" };
        app.Children.Add(new TreeNode { Id = "page", Label = "page.tsx" });
        app.Children.Add(new TreeNode { Id = "layout", Label = "layout.tsx" });
        Files.Nodes.Add(app); Files.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" }); Files.DefaultExpandedIds.Add("app");

        var src = new TreeNode { Id = "src", Label = "src" }; var controls = new TreeNode { Id = "controls", Label = "controls" }; controls.Children.Add(new TreeNode { Id = "long", Label = "A very long component path that clips on narrow panels.cs" }); src.Children.Add(controls); Extended.Nodes.Add(src); Extended.Nodes.Add(new TreeNode { Id = "ignored", Label = "Archived folder", IsEnabled = false }); Extended.DefaultExpandedIds.Add("src"); Extended.DefaultExpandedIds.Add("controls");
    }

    private void OnSelected(object sender, TreeNodeEventArgs e) => Result.Text = $"已选择 {e.Node.Label}";
    private void OnExpandedChanged(object sender, TreeExpandedChangedEventArgs e) => Result.Text = $"已展开 {e.ExpandedIds.Count} 个文件夹";
}
