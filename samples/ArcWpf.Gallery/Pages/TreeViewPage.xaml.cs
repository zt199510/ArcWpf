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
        BuildLazy(); BuildLarge();
    }

    private readonly HashSet<string> _denied = new() { "restricted" };
    private int _largeRows;

    private void BuildLazy()
    {
        Lazy.Nodes.Add(Folder("src", "src")); Lazy.Nodes.Add(Folder("tests", "tests")); Lazy.Nodes.Add(Folder("restricted", "restricted")); Lazy.Nodes.Add(new TreeNode { Id = "lazy-readme", Label = "README.md" });
        Lazy.ChildrenLoader = ReadFolder;
    }

    // Stands in for a disk or network read; the delay is what makes the loading state visible.
    private async Task<IEnumerable<TreeNode>> ReadFolder(TreeNode folder, CancellationToken cancel)
    {
        await Task.Delay(700, cancel);
        if (_denied.Remove(folder.Id)) throw new UnauthorizedAccessException($"无权读取 {folder.Label}");
        int depth = folder.Id.Count(character => character == '/');
        var children = new List<TreeNode>();
        if (depth < 2) for (int i = 1; i <= 2; i++) children.Add(Folder($"{folder.Id}/dir{i}", $"{folder.Label}-{i}"));
        for (int i = 1; i <= 3; i++) children.Add(new TreeNode { Id = $"{folder.Id}/file{i}", Label = $"{folder.Label}.part{i}.cs" });
        return children;
    }

    private static TreeNode Folder(string id, string label) => new() { Id = id, Label = label, HasLazyChildren = true };
    private void ReloadSource(object sender, RoutedEventArgs e) { Lazy.Reload(Lazy.Nodes[0]); LazyResult.Text = "src 已标记为未读取；展开着就立即重新读取，旧的子项保留到新结果到达。"; }
    private void OnLoadFailed(object sender, TreeLoadFailedEventArgs e) => LazyResult.Text = $"读取失败：{e.Error.Message}。再展开一次重试。";
    private void OnLazyExpanded(object sender, TreeExpandedChangedEventArgs e) => LazyResult.Text = $"已展开 {e.ExpandedIds.Count} 个文件夹";

    private void BuildLarge()
    {
        var open = new List<string>();
        for (int folder = 1; folder <= 100; folder++)
        {
            var node = new TreeNode { Id = $"pkg{folder}", Label = $"package-{folder:000}" };
            for (int file = 1; file <= 100; file++) node.Children.Add(new TreeNode { Id = $"pkg{folder}/{file}", Label = $"module-{file:000}.cs" });
            Large.Nodes.Add(node); open.Add(node.Id);
        }
        foreach (var id in open) Large.DefaultExpandedIds.Add(id);
        _largeRows = 100 * 101;
    }

    private void OnLargeScrolled(object sender, ScrollChangedEventArgs e) => LargeResult.Text = $"共 {_largeRows:N0} 行，当前只生成了 {CountRows(Large)} 行的界面元素；滚动或按 End 试试。";
    private static int CountRows(DependencyObject root)
    {
        int count = root is System.Windows.Controls.Button { Tag: not null } ? 1 : 0;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++) count += CountRows(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
        return count;
    }

    private void OnSelected(object sender, TreeNodeEventArgs e) => Result.Text = $"已选择 {e.Node.Label}";
    private void OnExpandedChanged(object sender, TreeExpandedChangedEventArgs e) => Result.Text = $"已展开 {e.ExpandedIds.Count} 个文件夹";
}
