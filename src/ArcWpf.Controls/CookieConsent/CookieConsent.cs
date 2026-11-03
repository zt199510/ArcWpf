using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class CookieCategory
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
    public bool DefaultEnabled { get; set; }
    /// <summary>偏好面板当前正在编辑的值；提交前不会影响已保存的 Consent。</summary>
    public bool DraftEnabled { get; set; }
}
public enum CookieConsentAction { AcceptAll, RejectAll, Save, Reset }
public sealed class CookieConsentChangedEventArgs(CookieConsentAction action, IReadOnlyDictionary<string, bool>? value) : EventArgs { public CookieConsentAction Action { get; } = action; public IReadOnlyDictionary<string, bool>? Value { get; } = value; public DateTime UpdatedAt { get; } = DateTime.UtcNow; }

[TemplatePart(Name="PART_Bar", Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Panel", Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_Chip", Type=typeof(FrameworkElement))]
[TemplatePart(Name="PART_CategoryList", Type=typeof(ItemsControl))]
public class CookieConsent : Control
{
    public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(nameof(Categories), typeof(ObservableCollection<CookieCategory>), typeof(CookieConsent), new FrameworkPropertyMetadata(null, OnCategoriesChanged));
    public static readonly DependencyProperty ConsentProperty = DependencyProperty.Register(nameof(Consent), typeof(IReadOnlyDictionary<string,bool>), typeof(CookieConsent), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnConsentChanged));
    public static readonly DependencyProperty StorageKeyProperty = DependencyProperty.Register(nameof(StorageKey), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(nameof(Version), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("1"));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(CookieConsentPosition), typeof(CookieConsent), new FrameworkPropertyMetadata(CookieConsentPosition.Bottom, OnPositionChanged));
    public static readonly DependencyProperty ShowReopenButtonProperty = DependencyProperty.Register(nameof(ShowReopenButton), typeof(bool), typeof(CookieConsent), new FrameworkPropertyMetadata(true, OnShowReopenChanged));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("We use essential cookies to run this site. With your OK, we’ll also use optional ones to remember settings and learn what helps."));
    public static readonly DependencyProperty PreferencesTitleProperty = DependencyProperty.Register(nameof(PreferencesTitle), typeof(string), typeof(CookieConsent), new FrameworkPropertyMetadata("Cookie preferences"));
    public ObservableCollection<CookieCategory> Categories { get => (ObservableCollection<CookieCategory>)GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
    public IReadOnlyDictionary<string,bool>? Consent { get => (IReadOnlyDictionary<string,bool>?)GetValue(ConsentProperty); set => SetValue(ConsentProperty, value); }
    public string? StorageKey { get => (string?)GetValue(StorageKeyProperty); set => SetValue(StorageKeyProperty, value); }
    public string Version { get => (string)GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    public CookieConsentPosition Position { get => (CookieConsentPosition)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public bool ShowReopenButton { get => (bool)GetValue(ShowReopenButtonProperty); set => SetValue(ShowReopenButtonProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string PreferencesTitle { get => (string)GetValue(PreferencesTitleProperty); set => SetValue(PreferencesTitleProperty, value); }
    public bool IsDecided => Consent is not null;
    public event EventHandler<CookieConsentChangedEventArgs>? ConsentChanged;
    public static readonly ObservableCollection<CookieCategory> DefaultCategories = new() { new(){Id="necessary",Label="Strictly necessary",Description="Sign-in, security, and your cart.",Required=true}, new(){Id="preferences",Label="Preferences",Description="Remember language and display settings."}, new(){Id="analytics",Label="Analytics",Description="Count visits in aggregate."}, new(){Id="marketing",Label="Marketing",Description="Measure campaigns and relevant ads."} };
    private FrameworkElement? _bar, _panel, _chip; private ItemsControl? _list; private ObservableCollection<CookieCategory>? _observedCategories; private bool _panelOpen; private IInputElement? _focusReturn;
    static CookieConsent() { DefaultStyleKeyProperty.OverrideMetadata(typeof(CookieConsent), new FrameworkPropertyMetadata(typeof(CookieConsent))); }
    public CookieConsent() { Categories = new ObservableCollection<CookieCategory>(DefaultCategories.Select(c => new CookieCategory { Id=c.Id, Label=c.Label, Description=c.Description, Required=c.Required, DefaultEnabled=c.DefaultEnabled })); ObserveCategories(Categories); Loaded += (_, _) => { LoadStored(); Refresh(); }; }
    private static void OnCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var control = (CookieConsent)d; if (e.OldValue is ObservableCollection<CookieCategory> old) old.CollectionChanged -= control.CategoriesChanged; if (e.NewValue is ObservableCollection<CookieCategory> next) control.ObserveCategories(next); control.Refresh(); }
    private void ObserveCategories(ObservableCollection<CookieCategory> categories) { if (ReferenceEquals(_observedCategories, categories)) return; if (_observedCategories is not null) _observedCategories.CollectionChanged -= CategoriesChanged; _observedCategories = categories; categories.CollectionChanged += CategoriesChanged; }
    private void CategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private static void OnConsentChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((CookieConsent)d).Refresh();
    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((CookieConsent)d).ApplyPosition();
    private static void OnShowReopenChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((CookieConsent)d).Refresh();
    public override void OnApplyTemplate() { Release(); base.OnApplyTemplate(); _bar=GetTemplateChild("PART_Bar") as FrameworkElement; _panel=GetTemplateChild("PART_Panel") as FrameworkElement; _chip=GetTemplateChild("PART_Chip") as FrameworkElement; _list=GetTemplateChild("PART_CategoryList") as ItemsControl; if (_list is not null) _list.ItemsSource=Categories; ApplyPosition(); WireButtons(); Refresh(); }
    private void Release() { if (_bar is null && _panel is null && _chip is null) return; foreach (var root in new[]{_bar,_panel,_chip}) if(root is not null) { foreach(var button in Find<ButtonBase>(root)) button.Click-=ButtonClick; foreach(var toggle in Find<ToggleButton>(root)) { toggle.Checked-=ToggleChanged; toggle.Unchecked-=ToggleChanged; } } _bar=_panel=_chip=null; _list=null; }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject { for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i); if(c is T t) yield return t; foreach(var x in Find<T>(c)) yield return x;} }
    private void WireButtons() { foreach(var root in new[]{_bar,_panel,_chip}) if(root is not null) { foreach(var button in Find<ButtonBase>(root)) button.Click+=ButtonClick; foreach(var toggle in Find<ToggleButton>(root)) { toggle.Checked+=ToggleChanged; toggle.Unchecked+=ToggleChanged; } } }
    private void ToggleChanged(object sender, RoutedEventArgs e) { if(sender is ToggleButton toggle && toggle.DataContext is CookieCategory c) c.DraftEnabled=toggle.IsChecked==true; }
    private void ButtonClick(object sender, RoutedEventArgs e) { if(sender is not ButtonBase b) return; switch(b.Tag as string) { case "customize": OpenPreferences(); break; case "accept": Commit(true, CookieConsentAction.AcceptAll); break; case "reject": Commit(false, CookieConsentAction.RejectAll); break; case "save": CommitDraft(); break; case "close": ClosePreferences(); break; case "reopen": OpenPreferences(); break; case "reset": Reset(); break; } }
    public void OpenPreferences() { _focusReturn=Keyboard.FocusedElement; _panelOpen=true; foreach(var c in Categories) c.DraftEnabled=c.Required || (Consent is not null && Consent.TryGetValue(c.Id,out var on) ? on : c.DefaultEnabled); Refresh(); FocusFirst(_panel); }
    public void ClosePreferences() { _panelOpen=false; Refresh(); Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if(_focusReturn is UIElement element) element.Focus(); _focusReturn=null; })); }
    public void Reset() { Consent=null; _panelOpen=false; _focusReturn=null; DeleteStored(); ConsentChanged?.Invoke(this,new CookieConsentChangedEventArgs(CookieConsentAction.Reset,null)); Refresh(); }
    public void CommitForTest(bool acceptAll) => Commit(acceptAll, acceptAll ? CookieConsentAction.AcceptAll : CookieConsentAction.RejectAll);
    private void Commit(bool all, CookieConsentAction action) { var value=Categories.ToDictionary(c=>c.Id,c=>c.Required||all); Consent=value; SaveStored(value); _panelOpen=false; ConsentChanged?.Invoke(this,new CookieConsentChangedEventArgs(action,value)); Refresh(); FocusReopen(); }
    private void CommitDraft() { var value=Categories.ToDictionary(c=>c.Id,c=>c.Required||c.DraftEnabled); Consent=value; SaveStored(value); _panelOpen=false; ConsentChanged?.Invoke(this,new CookieConsentChangedEventArgs(CookieConsentAction.Save,value)); Refresh(); FocusReopen(); }
    private void FocusFirst(FrameworkElement? root) { if(root is null) return; Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Find<ButtonBase>(root).FirstOrDefault(b => b.IsVisible && b.IsEnabled)?.Focus())); }
    private void FocusReopen() { _focusReturn=null; Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { var root=IsDecided&&ShowReopenButton?_chip:_bar; FocusFirst(root); })); }
    private void Refresh() { if(_bar is null) return; SetFace(_bar,!IsDecided&&!_panelOpen); SetFace(_panel,_panelOpen); SetFace(_chip,IsDecided&&!_panelOpen&&ShowReopenButton); if(_list is not null){_list.ItemsSource=null;_list.ItemsSource=Categories;} SetValue(AutomationProperties.NameProperty, PreferencesTitle); }
    private void ApplyPosition()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Margin = new Thickness(16);
        var align = Position switch { CookieConsentPosition.BottomLeft => HorizontalAlignment.Left, CookieConsentPosition.BottomRight => HorizontalAlignment.Right, _ => HorizontalAlignment.Center };
        if (_bar is not null) _bar.HorizontalAlignment = align;
        if (_panel is not null) _panel.HorizontalAlignment = align;
        if (_chip is not null) _chip.HorizontalAlignment = Position == CookieConsentPosition.BottomRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }
    private void SetFace(FrameworkElement? face, bool visible)
    {
        if (face is null) return;
        var level = ArcMotion.GetLevel(this);
        if (!visible)
        {
            face.BeginAnimation(OpacityProperty, null);
            face.Visibility = Visibility.Collapsed;
            face.Opacity = 1;
            return;
        }
        face.Visibility = Visibility.Visible;
        face.RenderTransformOrigin = new Point(.5, 1);
        if (!IsLoaded || level == ArcMotionLevel.Off) { face.Opacity = 1; face.RenderTransform = null; return; }
        var scale = face.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        face.RenderTransform = scale;
        face.Opacity = 0;
        scale.ScaleX = level == ArcMotionLevel.Full ? .98 : 1;
        scale.ScaleY = level == ArcMotionLevel.Full ? .98 : 1;
        face.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(level == ArcMotionLevel.Full ? 180 : 120)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        if (level == ArcMotionLevel.Full)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new BackEase { Amplitude = .15, EasingMode = EasingMode.EaseOut } });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new BackEase { Amplitude = .15, EasingMode = EasingMode.EaseOut } });
        }
    }
    private string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcWpf", (StorageKey??"cookie-consent").Replace('/','_')+".json");
    private void LoadStored(){if(string.IsNullOrWhiteSpace(StorageKey)||Consent is not null)return; try{var path=FilePath;if(!File.Exists(path))return;var text=File.ReadAllText(path);var data=JsonSerializer.Deserialize<StoredConsent>(text);if(data?.Version==Version)Consent=data.Value;}catch{} }
    private void SaveStored(IReadOnlyDictionary<string,bool> value){if(string.IsNullOrWhiteSpace(StorageKey))return;try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath,JsonSerializer.Serialize(new StoredConsent(value,Version,DateTime.UtcNow)));}catch{}}
    private void DeleteStored(){if(string.IsNullOrWhiteSpace(StorageKey))return;try{if(File.Exists(FilePath))File.Delete(FilePath);}catch{}}
    private sealed record StoredConsent(IReadOnlyDictionary<string,bool> Value,string Version,DateTime UpdatedAt);
}
public enum CookieConsentPosition { Bottom, BottomLeft, BottomRight }
