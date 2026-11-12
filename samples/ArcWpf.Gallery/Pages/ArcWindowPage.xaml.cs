using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ArcWindowPage : UserControl
{
    public ArcWindowPage() => InitializeComponent();

    /// <summary>The last window this page opened, for the screenshot harness.</summary>
    public ArcWindow? LastWindow { get; private set; }

    private void OpenAgentClicked(object sender, RoutedEventArgs e) => Show(CreateAgentWindow());
    private void OpenFixedClicked(object sender, RoutedEventArgs e) => Show(new ArcWindow
    {
        Title = "Export settings", Width = 420, Height = 300, ResizeMode = ResizeMode.CanMinimize,
        Content = new TextBlock { Text = "A window that keeps its size. Maximize is disabled, as on a native frame.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) },
    });
    private void OpenPlainClicked(object sender, RoutedEventArgs e) => Show(new ArcWindow
    {
        Title = "Notes", Width = 520, Height = 360,
        Content = new TextBlock { Text = "Only a title and the caption buttons.", Margin = new Thickness(16) },
    });

    private void Show(ArcWindow window)
    {
        window.Owner = Window.GetWindow(this);
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.Closed += (_, _) => Status.Text = $"已关闭：{window.Title}";
        window.StateChanged += (_, _) => Status.Text = $"{window.Title}：{window.WindowState}";
        LastWindow = window;
        window.Show();
        Status.Text = $"已打开：{window.Title}";
    }

    // Follows and flips the app theme, so the frame colors can be checked in both.
    private static ThemeSwitch Switch()
    {
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().SingleOrDefault();
        var toggle = new ThemeSwitch { Label = "Switch theme", Theme = theme?.Mode == ArcThemeMode.Dark ? ThemeSwitchTheme.Dark : ThemeSwitchTheme.Light };
        toggle.ThemeChanged += (_, e) => { if (theme is not null) theme.Mode = e.Theme == ThemeSwitchTheme.Dark ? ArcThemeMode.Dark : ArcThemeMode.Light; };
        return toggle;
    }

    public static ArcWindow CreateAgentWindow()
    {
        var views = new SegmentedControl { Label = "View", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var name in new[] { "Chat", "Files", "Terminal" }) views.Items.Add(new SegmentedItem { Content = name, Tag = name });
        views.SelectedIndex = 0;
        var tree = new Controls.TreeView { Label = "Workspace", Margin = new Thickness(12, 0, 0, 12) };
        var src = new TreeNode { Id = "src", Label = "src" };
        foreach (var file in new[] { "agent.ts", "tools.ts", "styles.css" }) src.Children.Add(new TreeNode { Id = file, Label = file });
        tree.Nodes.Add(src); tree.Nodes.Add(new TreeNode { Id = "readme", Label = "README.md" }); tree.DefaultExpandedIds.Add("src");
        var body = new TextBlock { Text = "Pick a file on the left. The title bar above holds the view switch and the theme switch; everything else in it drags the window.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 8, 24, 24) };
        body.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextSecondary");
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.Children.Add(tree);
        Grid.SetColumn(body, 1); layout.Children.Add(body);
        return new ArcWindow
        {
            Title = "ArcWpf Agent", Width = 900, Height = 560, MinWidth = 480, MinHeight = 320,
            TitleBarContent = views,
            TitleBarActions = Switch(),
            Content = layout,
        };
    }
}
