using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;

namespace ArcWpf.Gallery;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _pages = new(StringComparer.Ordinal);
    private bool _navigationVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        BuildNavigation();
        NavigationTree.Selected += NavigationSelected;
    }

    private void BuildNavigation()
    {
        var groups = new[]
        {
            Group("basic", "基础", ("button", "Button", new ButtonPage()), ("switch", "Switch", new SwitchPage()), ("input", "Input", new InputPage()), ("textarea", "Textarea", new TextareaPage()), ("date-picker", "DatePicker", new DatePickerPage())),
            Group("feedback", "反馈", ("alert", "Alert", new AlertPage()), ("popover", "Popover", new PopoverPage()), ("dialog", "Dialog", new DialogPage()), ("tooltip", "Tooltip", new TooltipPage()), ("toast", "Toast", new ToastPage()), ("toast-stack", "ToastStack", new ToastStackPage())),
            Group("navigation", "选择与导航", ("checkbox", "CheckBox", new CheckBoxPage()), ("radio", "RadioButton", new RadioButtonPage()), ("select", "Select", new SelectPage()), ("tabs", "Tabs", new TabsPage()), ("drawer", "Drawer", new DrawerPage()), ("breadcrumb", "Breadcrumb", new BreadcrumbPage()), ("tree-view", "TreeView", new TreeViewPage())),
            Group("layout", "布局", ("accordion", "Accordion", new AccordionPage()), ("expandable-card", "ExpandableCard", new ExpandableCardPage()), ("menu", "Menu", new MenuPage()), ("form", "Form", new FormLayoutPage()), ("list", "List", new ListPage()), ("progress", "Progress", new ProgressPage()), ("number-input", "NumberInput", new NumberInputPage()))
        };

        var roots = new ObservableCollection<TreeNode>();
        foreach (var group in groups)
        {
            var parent = new TreeNode { Id = group.Id, Label = group.Label };
            foreach (var item in group.Items)
            {
                parent.Children.Add(new TreeNode { Id = item.Id, Label = item.Label });
                _pages[item.Id] = item.Page;
            }
            roots.Add(parent);
        }

        NavigationTree.Nodes = roots;
        NavigationTree.DefaultExpandedIds = new ObservableCollection<string>(roots.Select(x => x.Id));
        PageHost.Content = _pages["button"];
    }

    private void NavigationSelected(object? sender, TreeNodeEventArgs e)
    {
        if (_pages.TryGetValue(e.Node.Id, out var page)) PageHost.Content = page;
    }

    private void ToggleNavigation(object sender, RoutedEventArgs e)
    {
        _navigationVisible = !_navigationVisible;
        NavigationColumn.Width = _navigationVisible ? new GridLength(236) : new GridLength(0);
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = theme.Mode == ArcThemeMode.Light ? ArcThemeMode.Dark : ArcThemeMode.Light;
    }

    private void ChangeMotion(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox picker && picker.SelectedIndex >= 0) ArcMotion.SetLevel(this, (ArcMotionLevel)picker.SelectedIndex);
    }

    private static NavigationGroup Group(string id, string label, params (string Id, string Label, FrameworkElement Page)[] items) => new(id, label, items);
    private sealed record NavigationGroup(string Id, string Label, IReadOnlyList<(string Id, string Label, FrameworkElement Page)> Items);
}

