using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

public sealed class ReactionOption { public string Id { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public object? Icon { get; set; } }
public sealed class Reaction : INotifyPropertyChanged
{
    private string _id = string.Empty; private string? _label; private object? _icon; private int _count; private bool _reacted;
    public string Id { get => _id; set => Set(ref _id, value); }
    public string? Label { get => _label; set => Set(ref _label, value); }
    public object? Icon { get => _icon; set => Set(ref _icon, value); }
    public int Count { get => _count; set => Set(ref _count, value); }
    public bool Reacted { get => _reacted; set => Set(ref _reacted, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
public sealed class ReactionChangedEventArgs(string id, bool reacted, IReadOnlyList<Reaction> reactions) : EventArgs { public string Id { get; } = id; public bool Reacted { get; } = reacted; public IReadOnlyList<Reaction> Reactions { get; } = reactions; }

[TemplatePart(Name="PART_Pills", Type=typeof(ItemsControl))]
[TemplatePart(Name="PART_Add", Type=typeof(System.Windows.Controls.Button))]
[TemplatePart(Name="PART_Popup", Type=typeof(Popup))]
[TemplatePart(Name="PART_Picker", Type=typeof(ItemsControl))]
[TemplatePart(Name="PART_Status", Type=typeof(TextBlock))]
public class Reactions : Control
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(ObservableCollection<ReactionOption>), typeof(Reactions), new FrameworkPropertyMetadata(null, OnCollectionChanged));
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(ObservableCollection<Reaction>), typeof(Reactions), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCollectionChanged));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Reactions), new FrameworkPropertyMetadata("Reactions"));
    public ObservableCollection<ReactionOption> Options { get => (ObservableCollection<ReactionOption>)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public ObservableCollection<Reaction> Values { get => (ObservableCollection<Reaction>)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public event EventHandler<ReactionChangedEventArgs>? ValueChanged;
    private ItemsControl? _pills, _picker; private System.Windows.Controls.Button? _add; private Popup? _popup; private TextBlock? _status; private FrameworkElement? _highlight; private Panel? _highlightHost; private int _pickerIndex;
    static Reactions() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Reactions), new FrameworkPropertyMetadata(typeof(Reactions))); }
    public Reactions() { Options = new ObservableCollection<ReactionOption>(); Values = new ObservableCollection<Reaction>(); Loaded += (_, _) => Refresh(); Unloaded += (_, _) => ClosePicker(); }
    private static void OnCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((Reactions)d).Refresh();
    public override void OnApplyTemplate() { Release(); base.OnApplyTemplate(); _pills=GetTemplateChild("PART_Pills") as ItemsControl; _picker=GetTemplateChild("PART_Picker") as ItemsControl; _highlight=GetTemplateChild("PART_Highlight") as FrameworkElement; _highlightHost=_highlight?.Parent as Panel; _add=GetTemplateChild("PART_Add") as System.Windows.Controls.Button; _popup=GetTemplateChild("PART_Popup") as Popup; _status=GetTemplateChild("PART_Status") as TextBlock; if(_add is not null){_add.Click+=AddClick;_add.PreviewKeyDown+=AddKeyDown;} if(_popup is not null){_popup.CustomPopupPlacementCallback=PlacePopup;_popup.Closed+=PopupClosed;} if(_pills is not null)_pills.ItemsSource=Values; if(_picker is not null){_picker.ItemsSource=Options;_picker.ItemContainerGenerator.StatusChanged+=PickerReady;} Refresh(); }
    private void Release(){if(_add is not null){_add.Click-=AddClick;_add.PreviewKeyDown-=AddKeyDown;} if(_picker is not null){_picker.ItemContainerGenerator.StatusChanged-=PickerReady;UnwirePickerButtons();} if(_popup is not null){_popup.Closed-=PopupClosed;_popup.CustomPopupPlacementCallback=null;} _pills=_picker=null;_add=null;_popup=null;_status=null;_highlight=null;_highlightHost=null;}
    private void PickerReady(object? s, EventArgs e){if(_picker?.ItemContainerGenerator.Status==System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated){WirePickerButtons();FocusPicker();}}
    private void WirePickerButtons()
    {
        if (_picker is null) return;
        foreach (var button in Enumerable.Range(0, Options.Count).Select(i => _picker.ItemContainerGenerator.ContainerFromIndex(i)).OfType<ReactionOptionButton>())
        {
            button.MouseEnter -= PickerMouseChanged; button.GotKeyboardFocus -= PickerFocusChanged;
            button.MouseEnter += PickerMouseChanged; button.GotKeyboardFocus += PickerFocusChanged;
        }
    }
    private void UnwirePickerButtons()
    {
        if (_picker is null) return;
        foreach (var button in Enumerable.Range(0, Options.Count).Select(i => _picker.ItemContainerGenerator.ContainerFromIndex(i)).OfType<ReactionOptionButton>())
        { button.MouseEnter -= PickerMouseChanged; button.GotKeyboardFocus -= PickerFocusChanged; }
    }
    private void PickerMouseChanged(object sender, MouseEventArgs e) => MovePickerHighlight(sender as FrameworkElement);
    private void PickerFocusChanged(object sender, KeyboardFocusChangedEventArgs e) => MovePickerHighlight(sender as FrameworkElement);
    private void MovePickerHighlight(FrameworkElement? target)
    {
        if (_highlight is null || _highlightHost is null || target is null) return;
        var point = target.TransformToAncestor(_highlightHost).Transform(new Point(0, 0));
        if (_highlight.RenderTransform is not TranslateTransform move) _highlight.RenderTransform = move = new TranslateTransform();
        var level = ArcMotion.GetLevel(this);
        if (level == ArcMotionLevel.Full && _highlight.IsLoaded) { Tween.Spring(move, TranslateTransform.XProperty, point.X, Tween.Morph); Tween.Spring(move, TranslateTransform.YProperty, point.Y, Tween.Morph); }
        else { move.X = point.X; move.Y = point.Y; }
        _highlight.Opacity = 1;
    }
    private void Refresh(){if(_pills is not null && !ReferenceEquals(_pills.ItemsSource, Values))_pills.ItemsSource=Values;if(_picker is not null){_picker.ItemsSource=null;_picker.ItemsSource=Options;}AutomationProperties.SetName(this,Label);}
    internal bool IsReacted(string id) => Values.FirstOrDefault(item => item.Id == id)?.Reacted == true;
    private void AddClick(object sender,RoutedEventArgs e){if(_popup is null)return;_popup.IsOpen=!_popup.IsOpen;SetAddGlyph(_popup.IsOpen);if(_popup.IsOpen){_pickerIndex=0;FocusPicker();}e.Handled=true;}
    private void AddKeyDown(object sender,KeyEventArgs e){if(e.Key is Key.Enter or Key.Space){AddClick(sender,e);e.Handled=true;}}
    private void FocusPicker(){if(_picker?.ItemContainerGenerator.ContainerFromIndex(_pickerIndex) is Control item){item.Focus();}}
    private void PopupClosed(object? sender, EventArgs e){SetAddGlyph(false);_add?.Focus();}
    private void SetAddGlyph(bool open)
    {
        if (_add is null) return;
        _add.Content = open ? "×" : "＋";
        _add.ToolTip = open ? "Close reaction picker" : "Add reaction";
        AutomationProperties.SetName(_add, open ? "Close reaction picker" : "Add reaction");
    }
    private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
    {
        if (_add is null) return new[] { new CustomPopupPlacement(new Point(0, targetSize.Height + 6), PopupPrimaryAxis.Vertical) };
        var screen = _add.PointToScreen(new Point(0, 0));
        var work = SystemParameters.WorkArea;
        bool above = screen.Y + targetSize.Height + popupSize.Height + 6 > work.Bottom && screen.Y - popupSize.Height - 6 >= work.Top;
        return new[] { new CustomPopupPlacement(above ? new Point(0, -popupSize.Height - 6) : new Point(0, targetSize.Height + 6), PopupPrimaryAxis.Vertical) };
    }
    private void ClosePicker(){if(_popup is not null)_popup.IsOpen=false;SetAddGlyph(false);if(_highlight is not null)_highlight.Opacity=0;_add?.Focus();}
    internal void Toggle(string id){var option=Options.FirstOrDefault(o=>o.Id==id);if(option is null)return;var existing=Values.FirstOrDefault(r=>r.Id==id);bool reacted=existing?.Reacted!=true;if(existing is null){if(!reacted)return;Values.Add(new Reaction{Id=id,Label=option.Label,Icon=option.Icon,Count=1,Reacted=true});}else{existing.Label=option.Label;existing.Icon=option.Icon;existing.Reacted=reacted;existing.Count=Math.Max(0,existing.Count+(reacted?1:-1));if(existing.Count==0)Values.Remove(existing);} _status?.SetCurrentValue(TextBlock.TextProperty,$"{option.Label} { (reacted?"added":"removed") }");ValueChanged?.Invoke(this,new ReactionChangedEventArgs(id,reacted,Values.ToList()));Refresh();ClosePicker();}
    public void ToggleForTest(string id) => Toggle(id);
    protected override void OnPreviewKeyDown(KeyEventArgs e){base.OnPreviewKeyDown(e);if(e.Handled||!(_popup?.IsOpen==true))return;int delta=e.Key is Key.Right or Key.Down?1:e.Key is Key.Left or Key.Up?-1:0;if(e.Key==Key.Escape){ClosePicker();e.Handled=true;return;}if(e.Key==Key.Home){_pickerIndex=0;FocusPicker();e.Handled=true;return;}if(e.Key==Key.End){_pickerIndex=Math.Max(0,Options.Count-1);FocusPicker();e.Handled=true;return;}if(delta!=0&&Options.Count>0){_pickerIndex=(_pickerIndex+delta+Options.Count)%Options.Count;FocusPicker();e.Handled=true;}}
}

public sealed class ReactionPill : Button
{
    public static readonly DependencyProperty ReactionProperty=DependencyProperty.Register(nameof(Reaction),typeof(Reaction),typeof(ReactionPill),new FrameworkPropertyMetadata(null,OnReactionChanged));
    public Reaction? Reaction{get=>(Reaction?)GetValue(ReactionProperty);set=>SetValue(ReactionProperty,value);}
    private static void OnReactionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pill = (ReactionPill)d;
        if (e.OldValue is Reaction oldReaction) oldReaction.PropertyChanged -= pill.ReactionPropertyChanged;
        if (e.NewValue is Reaction newReaction) newReaction.PropertyChanged += pill.ReactionPropertyChanged;
    }
    private void ReactionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(Reaction.Count) and not nameof(Reaction.Reacted)) return;
        if (RenderTransform is TransformGroup group && group.Children.OfType<ScaleTransform>().FirstOrDefault() is ScaleTransform scale && IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = .16, EasingMode = EasingMode.EaseOut } });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = .16, EasingMode = EasingMode.EaseOut } });
        }
    }
    protected override void OnClick(){base.OnClick();FindOwner()?.Toggle(Reaction?.Id??string.Empty);}
    private Reactions? FindOwner(){for(DependencyObject? n=this;n is not null;n=VisualTreeHelper.GetParent(n))if(n is Reactions r)return r;return null;}
}

public sealed class ReactionOptionButton : Button
{
    public static readonly DependencyProperty IsReactedProperty = DependencyProperty.Register(nameof(IsReacted), typeof(bool), typeof(ReactionOptionButton), new FrameworkPropertyMetadata(false));
    public bool IsReacted { get => (bool)GetValue(IsReactedProperty); set => SetValue(IsReactedProperty, value); }
    public ReactionOption? Option { get; set; }
    protected override void OnClick(){base.OnClick();for(DependencyObject? n=this;n is not null;n=VisualTreeHelper.GetParent(n))if(n is Reactions r){r.Toggle(Option?.Id??string.Empty);return;}}
}
public sealed class ReactionCountText : TextBlock
{
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(nameof(Count), typeof(int), typeof(ReactionCountText), new FrameworkPropertyMetadata(0, OnCountChanged));
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }
    private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (ReactionCountText)d;
        var old = (int)e.OldValue; var next = (int)e.NewValue;
        text.Text = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!text.IsLoaded || ArcMotion.GetLevel(text) == ArcMotionLevel.Off) return;
        var transform = text.RenderTransform as TranslateTransform ?? new TranslateTransform();
        text.RenderTransform = transform;
        var direction = next >= old ? 1 : -1;
        transform.Y = direction * 7;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(direction * 7, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
public sealed class ReactionItems : ItemsControl { protected override DependencyObject GetContainerForItemOverride()=>new ReactionPill(); protected override bool IsItemItsOwnContainerOverride(object item)=>item is ReactionPill; protected override void PrepareContainerForItemOverride(DependencyObject e, object item){base.PrepareContainerForItemOverride(e,item);if(e is ReactionPill p)p.Reaction=item as Reaction;} }
public sealed class ReactionPickerItems : ItemsControl { protected override DependencyObject GetContainerForItemOverride()=>new ReactionOptionButton(); protected override bool IsItemItsOwnContainerOverride(object item)=>item is ReactionOptionButton; protected override void PrepareContainerForItemOverride(DependencyObject e, object item){base.PrepareContainerForItemOverride(e,item);if(e is ReactionOptionButton p){p.Option=item as ReactionOption;for(DependencyObject? n=this;n is not null;n=VisualTreeHelper.GetParent(n))if(n is Reactions owner){p.IsReacted=p.Option is not null&&owner.IsReacted(p.Option.Id);break;}}} }
