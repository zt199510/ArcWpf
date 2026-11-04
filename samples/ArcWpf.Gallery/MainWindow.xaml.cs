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
            Group("activity", "公告与活动", ("announcement-bar", "AnnouncementBar", new AnnouncementBarPage()), ("animated-counter", "AnimatedCounter", new AnimatedCounterPage()), ("countdown", "Countdown", new CountdownPage()), ("timeline", "Timeline", new TimelinePage()), ("avatar-upload", "AvatarUpload", new AvatarUploadPage()), ("file-dropzone", "FileDropzone", new FileDropzonePage()), ("rich-text-editor", "RichTextEditor", new RichTextEditorPage()), ("signature-pad", "SignaturePad", new SignaturePadPage()), ("command-menu", "CommandMenu", new CommandMenuPage()), ("onboarding-checklist", "OnboardingChecklist", new OnboardingChecklistPage()), ("billing-toggle", "BillingToggle", new BillingTogglePage()), ("chat-thread", "ChatThread", new ChatThreadPage()), ("comment-thread", "CommentThread", new CommentThreadPage()), ("image-compare", "ImageCompare", new ImageComparePage()), ("floating-button-group", "FloatingButtonGroup", new FloatingButtonGroupPage()), ("expanding-button-group", "ExpandingButtonGroup", new ExpandingButtonGroupPage()), ("card-stack", "CardStack", new CardStackPage()), ("liquid-tab-bar", "LiquidTabBar", new LiquidTabBarPage()), ("cover-flow", "CoverFlow", new CoverFlowPage()), ("text-morph", "TextMorph", new TextMorphPage()), ("text-reveal", "TextReveal", new TextRevealPage()), ("text-shimmer", "TextShimmer", new TextShimmerPage()), ("icon-morph", "IconMorph", new IconMorphPage()), ("in-view-title", "InViewTitle", new InViewTitlePage()), ("mention-input", "MentionInput", new MentionInputPage()), ("emoji-picker", "EmojiPicker", new EmojiPickerPage())),
            Group("data", "数据", ("sparkline", "Sparkline", new SparklinePage()), ("gauge", "Gauge", new GaugePage()), ("metric-ticker", "MetricTicker", new MetricTickerPage()), ("realtime-stream", "RealtimeStream", new RealtimeStreamPage()), ("wallet-stack", "WalletStack", new WalletStackPage()), ("carousel", "Carousel", new CarouselPage()), ("radio-cards", "RadioCards", new RadioCardsPage()), ("cookie-consent", "CookieConsent", new CookieConsentPage()), ("reactions", "Reactions", new ReactionsPage()), ("swipe-actions", "SwipeActions", new SwipeActionsPage()), ("code-block", "CodeBlock", new CodeBlockPage()), ("json-viewer", "JSONViewer", new JSONViewerPage()), ("sortable-data-table", "SortableDataTable", new SortableDataTablePage()), ("data-grid", "ArcDataGrid", new ArcDataGridPage()), ("tree-table", "TreeTable", new TreeTablePage())),
            Group("new-interactions", "新交互", ("scrub-input", "ScrubInput", new ScrubInputPage()), ("elastic-slider", "ElasticSlider", new ElasticSliderPage()), ("ruler-slider", "RulerSlider", new RulerSliderPage()), ("theme-switch", "ThemeSwitch", new ThemeSwitchPage()), ("toggle-group", "ToggleGroup", new ToggleGroupPage()), ("action-swap", "ActionSwap", new ActionSwapPage()), ("undo-toast", "UndoToast", new UndoToastPage()), ("expandable-text", "ExpandableText", new ExpandableTextPage())),
            Group("basic", "基础", ("button", "Button", new ButtonPage()), ("action-button", "ActionButton", new ActionButtonPage()), ("copy-button", "CopyButton", new CopyButtonPage()), ("split-button", "SplitButton", new SplitButtonPage()), ("button-group", "ButtonGroup", new ButtonGroupPage()), ("switch", "Switch", new SwitchPage()), ("input", "Input", new InputPage()), ("password-input", "PasswordInput", new PasswordInputPage()), ("otp-input", "OTPInput", new OTPInputPage()), ("segmented-control", "SegmentedControl", new SegmentedControlPage()), ("textarea", "Textarea", new TextareaPage()), ("search", "Search", new SearchPage()), ("calendar", "Calendar", new CalendarPage()), ("date-picker", "DatePicker", new DatePickerPage())),
            Group("feedback", "反馈", ("badge", "Badge", new BadgePage()), ("bottom-sheet", "BottomSheet", new BottomSheetPage()), ("hover-card", "HoverCard", new HoverCardPage()), ("resizable-panels", "ResizablePanels", new ResizablePanelsPage()), ("card", "Card", new CardPage()), ("stepper", "Stepper", new StepperPage()), ("usage-meter", "UsageMeter", new UsageMeterPage()), ("empty-state", "EmptyState", new EmptyStatePage()), ("skeleton", "Skeleton", new SkeletonPage()), ("filter-toolbar", "FilterToolbar", new FilterToolbarPage()), ("reorderable-list", "ReorderableList", new ReorderableListPage()), ("password-strength", "PasswordStrength", new PasswordStrengthPage()), ("money-input", "MoneyInput", new MoneyInputPage()), ("phone-input", "PhoneInput", new PhoneInputPage()), ("tag-input", "TagInput", new TagInputPage()), ("shortcut-recorder", "ShortcutRecorder", new ShortcutRecorderPage()), ("chip-group", "ChipGroup", new ChipGroupPage()), ("rating", "Rating", new RatingPage()), ("color-picker", "ColorPicker", new ColorPickerPage()), ("date-range-picker", "DateRangePicker", new DateRangePickerPage()), ("time-picker", "TimePicker", new TimePickerPage()), ("scroll-area", "ScrollArea", new ScrollAreaPage()), ("toc-progress", "TocProgress", new TocProgressPage()), ("hold-to-confirm", "HoldToConfirm", new HoldToConfirmPage()), ("confirm-popover", "ConfirmPopover", new ConfirmPopoverPage()), ("inline-edit", "InlineEdit", new InlineEditPage()), ("expanding-search", "ExpandingSearch", new ExpandingSearchPage()), ("avatar", "Avatar", new AvatarPage()), ("user-menu", "UserMenu", new UserMenuPage()), ("avatar-group", "AvatarGroup", new AvatarGroupPage()), ("presence-stack", "PresenceStack", new PresenceStackPage()), ("share-access", "ShareAccess", new ShareAccessPage()), ("alert", "Alert", new AlertPage()), ("popover", "Popover", new PopoverPage()), ("dialog", "Dialog", new DialogPage()), ("tooltip", "Tooltip", new TooltipPage()), ("toast", "Toast", new ToastPage()), ("toast-stack", "ToastStack", new ToastStackPage())),
            Group("navigation", "选择与导航", ("checkbox", "CheckBox", new CheckBoxPage()), ("radio", "RadioButton", new RadioButtonPage()), ("select", "Select", new SelectPage()), ("multi-select", "MultiSelect", new MultiSelectPage()), ("combobox", "ComboBox", new ComboBoxPage()), ("tabs", "Tabs", new TabsPage()), ("drawer", "Drawer", new DrawerPage()), ("breadcrumb", "Breadcrumb", new BreadcrumbPage()), ("pagination", "Pagination", new PaginationPage()), ("tree-view", "TreeView", new TreeViewPage())),
            Group("layout", "布局", ("accordion", "Accordion", new AccordionPage()), ("expandable-card", "ExpandableCard", new ExpandableCardPage()), ("menu", "Menu", new MenuPage()), ("context-menu", "ContextMenu", new ContextMenuPage()), ("dropdown-menu", "DropdownMenu", new DropdownMenuPage()), ("form", "Form", new FormLayoutPage()), ("list", "List", new ListPage()), ("progress", "Progress", new ProgressPage()), ("number-input", "NumberInput", new NumberInputPage()), ("slider", "Slider", new SliderPage()))
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
