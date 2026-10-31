using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;
using ArcWpf.Gallery.Navigation;
using System.Windows.Input;

namespace ArcWpf.Gallery;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _pages = new(StringComparer.Ordinal);
    private bool _navigationVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        BuildNavigation();
        NavigationPanel.PageSelected += NavigationSelected;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) { SetNavigationVisible(true); NavigationPanel.FocusSearch(); e.Handled = true; } };
    }

    private void BuildNavigation()
    {
        var groups = new[]
        {
            Group("basic", "基础", ("button", "Button", new ButtonPage()), ("action-button", "ActionButton", new ActionButtonPage()), ("copy-button", "CopyButton", new CopyButtonPage()), ("split-button", "SplitButton", new SplitButtonPage()), ("button-group", "ButtonGroup", new ButtonGroupPage()), ("switch", "Switch", new SwitchPage()), ("input", "Input", new InputPage()), ("password-input", "PasswordInput", new PasswordInputPage()), ("otp-input", "OTPInput", new OTPInputPage()), ("segmented-control", "SegmentedControl", new SegmentedControlPage()), ("textarea", "Textarea", new TextareaPage()), ("search", "Search", new SearchPage()), ("calendar", "Calendar", new CalendarPage()), ("date-picker", "DatePicker", new DatePickerPage())),
            Group("feedback", "反馈", ("badge", "Badge", new BadgePage()), ("empty-state", "EmptyState", new EmptyStatePage()), ("skeleton", "Skeleton", new SkeletonPage()), ("filter-toolbar", "FilterToolbar", new FilterToolbarPage()), ("reorderable-list", "ReorderableList", new ReorderableListPage()), ("password-strength", "PasswordStrength", new PasswordStrengthPage()), ("money-input", "MoneyInput", new MoneyInputPage()), ("phone-input", "PhoneInput", new PhoneInputPage()), ("date-range-picker", "DateRangePicker", new DateRangePickerPage()), ("time-picker", "TimePicker", new TimePickerPage()), ("scroll-area", "ScrollArea", new ScrollAreaPage()), ("hold-to-confirm", "HoldToConfirm", new HoldToConfirmPage()), ("inline-edit", "InlineEdit", new InlineEditPage()), ("expanding-search", "ExpandingSearch", new ExpandingSearchPage()), ("avatar", "Avatar", new AvatarPage()), ("user-menu", "UserMenu", new UserMenuPage()), ("avatar-group", "AvatarGroup", new AvatarGroupPage()), ("presence-stack", "PresenceStack", new PresenceStackPage()), ("alert", "Alert", new AlertPage()), ("popover", "Popover", new PopoverPage()), ("dialog", "Dialog", new DialogPage()), ("tooltip", "Tooltip", new TooltipPage()), ("toast", "Toast", new ToastPage()), ("toast-stack", "ToastStack", new ToastStackPage())),
            Group("navigation", "选择与导航", ("checkbox", "CheckBox", new CheckBoxPage()), ("radio", "RadioButton", new RadioButtonPage()), ("select", "Select", new SelectPage()), ("multi-select", "MultiSelect", new MultiSelectPage()), ("combobox", "ComboBox", new ComboBoxPage()), ("tabs", "Tabs", new TabsPage()), ("drawer", "Drawer", new DrawerPage()), ("breadcrumb", "Breadcrumb", new BreadcrumbPage()), ("pagination", "Pagination", new PaginationPage()), ("tree-view", "TreeView", new TreeViewPage())),
            Group("layout", "布局", ("accordion", "Accordion", new AccordionPage()), ("expandable-card", "ExpandableCard", new ExpandableCardPage()), ("menu", "Menu", new MenuPage()), ("context-menu", "ContextMenu", new ContextMenuPage()), ("form", "Form", new FormLayoutPage()), ("list", "List", new ListPage()), ("progress", "Progress", new ProgressPage()), ("number-input", "NumberInput", new NumberInputPage()), ("slider", "Slider", new SliderPage()))
        };

        var roots = new ObservableCollection<NavigationNode>();
        foreach (var group in groups)
        {
            var parent = new NavigationNode { Id = group.Id, Label = group.Label };
            foreach (var item in group.Items)
            {
                parent.Children.Add(new NavigationNode { Id = item.Id, Label = item.Label });
                _pages[item.Id] = item.Page;
            }
            roots.Add(parent);
        }

        NavigationPanel.SetGroups(roots);
        var requestedPage = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--page=", StringComparison.Ordinal))?[7..];
        var selected = requestedPage is not null && _pages.ContainsKey(requestedPage) ? requestedPage : "button";
        PageHost.Content = _pages[selected];
        NavigationPanel.SelectPage(selected);
    }

    private void NavigationSelected(object? sender, string id)
    {
        if (_pages.TryGetValue(id, out var page)) PageHost.Content = page;
    }

    private void ToggleNavigation(object sender, RoutedEventArgs e)
    {
        SetNavigationVisible(!_navigationVisible);
    }

    private void SetNavigationVisible(bool visible)
    {
        if (!visible && NavigationPanel.IsKeyboardFocusWithin) Keyboard.ClearFocus();
        _navigationVisible = visible;
        NavigationPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        NavigationColumn.Width = new GridLength(visible ? 236 : 0);
        NavigationGap.Width = new GridLength(visible ? 20 : 0);
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single();
        theme.Mode = theme.Mode == ArcThemeMode.Light ? ArcThemeMode.Dark : ArcThemeMode.Light;
    }

    private void ChangeMotion(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox picker && picker.SelectedIndex >= 0) ArcMotion.SetLevel(this, (ArcMotionLevel)picker.SelectedIndex);
    }

    private static NavigationGroup Group(string id, string label, params (string Id, string Label, FrameworkElement Page)[] items) => new(id, label, items);
    private sealed record NavigationGroup(string Id, string Label, IReadOnlyList<(string Id, string Label, FrameworkElement Page)> Items);
}

