using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcWpf.Controls;

[TemplatePart(Name = "PART_Faces", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_Popover", Type = typeof(Popover))]
[TemplatePart(Name = "PART_Label", Type = typeof(Border))]
public class PresenceStack : System.Windows.Controls.Button
{
    private static DependencyProperty Register(string name, Type type, object? value) => DependencyProperty.Register(name, type, typeof(PresenceStack), new FrameworkPropertyMetadata(value, Changed));
    public static readonly DependencyProperty PeopleProperty = Register(nameof(People), typeof(IEnumerable<PresencePerson>), null);
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(int), typeof(PresenceStack), new FrameworkPropertyMetadata(4, Changed, (_, v) => Math.Max((int)v, 0)));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(AvatarSize), typeof(PresenceStack), new FrameworkPropertyMetadata(AvatarSize.Medium, Changed), v => v is AvatarSize.Small or AvatarSize.Medium or AvatarSize.Large);
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(nameof(Align), typeof(PresenceStackAlignment), typeof(PresenceStack), new FrameworkPropertyMetadata(PresenceStackAlignment.Start, Changed), v => v is PresenceStackAlignment a && Enum.IsDefined(a));
    public static readonly DependencyProperty SelfIdProperty = Register(nameof(SelfId), typeof(string), null);
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), typeof(string), "People here now");
    public static readonly DependencyProperty HeadingProperty = Register(nameof(Heading), typeof(string), "Here now");
    public static readonly DependencyProperty AnnounceProperty = Register(nameof(Announce), typeof(bool), true);
    public static readonly DependencyProperty IsPersonSelectionEnabledProperty = Register(nameof(IsPersonSelectionEnabled), typeof(bool), false);
    public static readonly DependencyProperty RingBrushProperty = Register(nameof(RingBrush), typeof(Brush), null);
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PresenceStack), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OpenChanged));
    private static readonly DependencyPropertyKey CountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Count), typeof(int), typeof(PresenceStack), new PropertyMetadata(0));
    public static readonly DependencyProperty CountProperty = CountPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey OverflowCountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(OverflowCount), typeof(int), typeof(PresenceStack), new PropertyMetadata(0));
    public static readonly DependencyProperty OverflowCountProperty = OverflowCountPropertyKey.DependencyProperty;
    public static readonly RoutedEvent PersonSelectedEvent = EventManager.RegisterRoutedEvent(nameof(PersonSelected), RoutingStrategy.Bubble, typeof(EventHandler<PresencePersonSelectedEventArgs>), typeof(PresenceStack));
    public event EventHandler<PresencePersonSelectedEventArgs> PersonSelected { add => AddHandler(PersonSelectedEvent, value); remove => RemoveHandler(PersonSelectedEvent, value); }
    public IEnumerable<PresencePerson>? People { get => (IEnumerable<PresencePerson>?)GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public AvatarSize Size { get => (AvatarSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public PresenceStackAlignment Align { get => (PresenceStackAlignment)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }
    public string? SelfId { get => (string?)GetValue(SelfIdProperty); set => SetValue(SelfIdProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public bool Announce { get => (bool)GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }
    public bool IsPersonSelectionEnabled { get => (bool)GetValue(IsPersonSelectionEnabledProperty); set => SetValue(IsPersonSelectionEnabledProperty, value); }
    public Brush? RingBrush { get => (Brush?)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public int Count => (int)GetValue(CountProperty);
    public int OverflowCount => (int)GetValue(OverflowCountProperty);
    internal double RestStep => Math.Floor(Diameter * .68);
    internal double Diameter => Size == AvatarSize.Small ? 28 : Size == AvatarSize.Large ? 44 : 36;
    internal bool Animate => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
    internal readonly Dictionary<string, PresenceFace> Faces = [];
    internal readonly Dictionary<string, PresenceRow> Rows = [];
    internal bool MotionRunning => _motion.IsRunning || _titleCount?.IsRunning == true || Faces.Values.Any(f => f.IsRunning) || Rows.Values.Any(r => r.IsRunning);
    private readonly OwnedMotion _motion = new();
    private Canvas? _canvas;
    private Border? _label;
    private TextBlock? _labelText;
    private TranslateTransform _labelMove = new();
    private Popover? _popover;
    private StackPanel? _roster;
    private PresenceRoll? _titleCount;
    private TextBlock? _heading;
    private TextBlock? _empty;
    private INotifyCollectionChanged? _collection;
    private readonly HashSet<PresencePerson> _observed = [];
    private List<PresencePerson> _people = [];
    private int _active;
    private bool _keyboard;
    private bool _ready;
    internal string LastAnnouncement { get; private set; } = "";
    static PresenceStack() => DefaultStyleKeyProperty.OverrideMetadata(typeof(PresenceStack), new FrameworkPropertyMetadata(typeof(PresenceStack)));
    public PresenceStack()
    {
        SetCurrentValue(PeopleProperty, new ObservableCollection<PresencePerson>());
        Loaded += (_, _) => { _ready = true; Refresh(false); };
        Unloaded += (_, _) => { _ready = false; Unobserve(); SetCurrentValue(IsOpenProperty, false); Settle(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { SetCurrentValue(IsOpenProperty, false); Settle(); } else Refresh(false); };
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PresenceStack)d).Refresh(e.Property == PeopleProperty);
    private static void OpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var stack = (PresenceStack)d;
        stack.UpdateRoster();
        if (UIElementAutomationPeer.FromElement(stack) is PresenceStackPeer peer) peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, (bool)e.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, (bool)e.NewValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && _motion is not null) { if (!Animate) Settle(); Refresh(false); }
    }
    public override void OnApplyTemplate()
    {
        Settle(); if (_popover is not null) { _popover.Closed -= PopoverClosed; _popover.Close(); BindingOperations.ClearBinding(_popover, Popover.IsOpenProperty); }
        Faces.Clear(); Rows.Clear(); _roster = null; _heading = null; _titleCount = null; base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Faces") as Canvas;
        _label = GetTemplateChild("PART_Label") as Border;
        _labelText = _label?.Child as TextBlock;
        _labelMove = new(); if (_label is not null) _label.RenderTransform = _labelMove;
        _popover = GetTemplateChild("PART_Popover") as Popover;
        if (_popover is not null)
        {
            _popover.PlacementTarget = this;
            _popover.SetBinding(Popover.IsOpenProperty, new Binding(nameof(IsOpen)) { Source = this, Mode = BindingMode.TwoWay });
            _popover.Closed += PopoverClosed;
            var content = new PresenceRoster { Width = 274 };
            AutomationProperties.SetName(content, Heading);
            var header = new Grid { Margin = new Thickness(12, 8, 12, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _heading = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.Medium, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            _titleCount = new PresenceRoll { Width = 32, Height = 20, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(_titleCount, 1);
            _titleCount.SetResourceReference(TextElement.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            foreach (TextBlock text in _titleCount.Children) text.TextAlignment = TextAlignment.Right;
            header.Children.Add(_heading); header.Children.Add(_titleCount); content.Children.Add(header);
            _roster = new StackPanel(); content.Children.Add(new ScrollViewer { Content = _roster, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            _empty = new TextBlock { Text = "No one here yet", Margin = new Thickness(12), FontSize = 14 };
            _empty.SetResourceReference(TextBlock.ForegroundProperty, "ArcWpf.Brush.TextMuted");
            content.Children.Add(_empty);
            _popover.Content = content;
        }
        Refresh(false);
    }
    private void PopoverClosed(object? sender, PopoverClosedEventArgs e)
    {
        if (e.Reason is PopoverCloseReason.OutsidePointer or PopoverCloseReason.Escape)
            Dispatcher.BeginInvoke(new Action(() => { if (!IsOpen && IsLoaded && IsVisible && Window.GetWindow(this)?.IsActive == true) Focus(); }));
    }
    private void Unobserve()
    {
        if (_collection is not null) _collection.CollectionChanged -= CollectionChanged;
        _collection = null; foreach (var person in _observed) person.PropertyChanged -= PersonChanged; _observed.Clear();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh(true);
    private void PersonChanged(object? sender, PropertyChangedEventArgs e) => Refresh(false);
    private void Refresh(bool announce)
    {
        if (_motion is null) return;
        var old = _people;
        _people = People?.Where(p => p is not null).DistinctBy(p => p.Id).ToList() ?? [];
        Unobserve();
        if (_ready)
        {
            _collection = People as INotifyCollectionChanged; if (_collection is not null) _collection.CollectionChanged += CollectionChanged;
            foreach (var p in _people) { _observed.Add(p); p.PropertyChanged += PersonChanged; }
        }
        SetValue(CountPropertyKey, _people.Count); SetValue(OverflowCountPropertyKey, Math.Max(0, Count - Max));
        _active = Math.Clamp(_active, 0, Math.Max(0, Math.Min(Count, Max) - 1));
        if (announce && Announce && _ready)
        {
            var joined = _people.Where(p => old.All(o => o.Id != p.Id)).Select(p => p.Name + " joined");
            var left = old.Where(p => _people.All(o => o.Id != p.Id)).Select(p => p.Name + " left");
            LastAnnouncement = string.Join(", ", joined.Concat(left));
            if (LastAnnouncement.Length > 0 && UIElementAutomationPeer.FromElement(this) is { } peer)
                peer.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.MostRecent, LastAnnouncement, "PresenceMembership");
        }
        if (_canvas is null) return;
        _canvas.Width = Diameter + Max * RestStep; _canvas.Height = Diameter;
        var visible = _people.Take(Max).ToList(); var keys = visible.Select(p => p.Id).ToHashSet(); if (OverflowCount > 0) keys.Add("\0overflow");
        foreach (var pair in Faces.ToArray()) if (!keys.Contains(pair.Key)) RemoveFace(pair.Key, pair.Value);
        foreach (var person in visible) UpdateFace(person.Id, person);
        if (OverflowCount > 0) UpdateFace("\0overflow", null);
        LayoutFaces(); UpdateRoster();
        if (_popover is not null) { _popover.Alignment = Align == PresenceStackAlignment.Start ? PopoverAlignment.Start : PopoverAlignment.End; ArcMotion.SetLevel(_popover, ArcMotion.GetLevel(this) == ArcMotionLevel.Full ? ArcMotionLevel.Full : ArcMotionLevel.Off); }
        if (UIElementAutomationPeer.FromElement(this) is { } automation) { automation.ResetChildrenCache(); automation.InvalidatePeer(); }
    }
    private void UpdateFace(string key, PresencePerson? person)
    {
        if (!Faces.TryGetValue(key, out var face))
        {
            face = new PresenceFace(this); Faces.Add(key, face); _canvas!.Children.Add(face);
            face.MouseEnter += (_, _) => { var index = _people.FindIndex(p => p.Id == key); if (index >= 0) { _active = index; LayoutFaces(); } };
            face.Update(person); face.Enter();
        }
        else { face.Exiting = false; face.Update(person); face.Show(); }
    }
    private void RemoveFace(string key, PresenceFace face)
    {
        if (face.Exiting) return; face.Exiting = true; face.IsHitTestVisible = false;
        face.Exit(() => Dispatcher.BeginInvoke(new Action(() => { if (face.Exiting && Faces.GetValueOrDefault(key) == face) { face.Stop(); Faces.Remove(key); _canvas?.Children.Remove(face); } })));
    }
    private void LayoutFaces()
    {
        if (_canvas is null) return;
        bool fan = IsMouseOver || (_keyboard && IsKeyboardFocused);
        var live = _people.Take(Max).Select(p => Faces.GetValueOrDefault(p.Id)).Where(f => f is not null).Cast<PresenceFace>().ToList();
        if (OverflowCount > 0 && Faces.TryGetValue("\0overflow", out var overflow)) live.Add(overflow);
        double step = fan ? Diameter + 4 : RestStep;
        double start = Align == PresenceStackAlignment.End ? _canvas.Width - Diameter - Math.Max(0, live.Count - 1) * step : 0;
        for (int i = 0; i < live.Count; i++) { Panel.SetZIndex(live[i], i == _active && fan ? 100 : live.Count - i); live[i].Move(start + i * step, fan && i == _active ? -2 : 0, fan); }
        if (_label is null || _labelText is null) return;
        Canvas.SetTop(_label, Diameter + 8);
        bool show = fan && _people.Count > 0 && Max > 0;
        _motion.Double(_label, OpacityProperty, show ? 1 : 0, .15, Animate);
        if (!show) return;
        _labelText.FontSize = Size == AvatarSize.Small ? 11 : Size == AvatarSize.Large ? 13 : 12;
        _labelText.Text = PersonName(_people[_active]);
        _labelText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = _labelText.DesiredSize.Width + _label.Padding.Left + _label.Padding.Right;
        bool initial = double.IsNaN(_label.Width) || _label.Opacity == 0;
        _motion.Double(_label, WidthProperty, width, .32, Animate && !initial, true);
        _motion.Double(_labelMove, TranslateTransform.XProperty, start + _active * step + Diameter / 2 - width / 2, .32, Animate && !initial, true);
    }
    internal string PersonName(PresencePerson p) => p.Name + (p.Id == SelfId ? " (you)" : "");
    internal static string StatusText(PresencePerson p) => p.Status switch { PresenceStatus.Idle => "Idle", PresenceStatus.Typing => "Typing", _ => "Active now" };
    private void UpdateRoster()
    {
        if (_roster is null) return;
        if (_empty is not null) _empty.Visibility = Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _heading!.Text = Heading; _titleCount!.Set(Count.ToString(), Animate);
        AutomationProperties.SetName((DependencyObject)_popover!.Content, Heading);
        foreach (var pair in Rows.ToArray()) if (_people.All(p => p.Id != pair.Key))
        {
            var row = pair.Value; row.Exit(() => Dispatcher.BeginInvoke(new Action(() => { if (row.Exiting && Rows.GetValueOrDefault(pair.Key) == row) { row.Stop(); Rows.Remove(pair.Key); _roster?.Children.Remove(row); } })));
        }
        for (int i = 0; i < _people.Count; i++)
        {
            var person = _people[i];
            if (!Rows.TryGetValue(person.Id, out var row)) { row = new PresenceRow(this); Rows.Add(person.Id, row); _roster.Children.Insert(Math.Min(i, _roster.Children.Count), row); row.Update(person); row.Enter(); }
            else { row.Update(person); if (_roster.Children.IndexOf(row) != i) { _roster.Children.Remove(row); _roster.Children.Insert(i, row); } }
        }
    }
    internal void SelectPerson(PresencePerson person)
    {
        if (!IsPersonSelectionEnabled || !IsEnabled) return;
        SetCurrentValue(IsOpenProperty, false); RaiseEvent(new PresencePersonSelectedEventArgs(PersonSelectedEvent, person)); Focus();
    }
    private void Settle()
    {
        _motion.Stop(); _titleCount?.Stop();
        foreach (var pair in Faces.ToArray()) { pair.Value.Stop(); if (pair.Value.Exiting) { _canvas?.Children.Remove(pair.Value); Faces.Remove(pair.Key); } }
        foreach (var pair in Rows.ToArray()) { pair.Value.Stop(); if (pair.Value.Exiting) { _roster?.Children.Remove(pair.Value); Rows.Remove(pair.Key); } }
    }
    protected override void OnClick() { base.OnClick(); SetCurrentValue(IsOpenProperty, !IsOpen); }
    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); LayoutFaces(); }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); LayoutFaces(); }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e) { _keyboard = false; base.OnPreviewMouseDown(e); LayoutFaces(); }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); _keyboard = Mouse.LeftButton != MouseButtonState.Pressed; LayoutFaces(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); LayoutFaces(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsKeyboardFocused && e.Key is (Key.Left or Key.Right)) { _keyboard = true; int count = Math.Min(Max, Count); bool forward = (e.Key == Key.Right) != (FlowDirection == FlowDirection.RightToLeft); if (count > 0) _active = (_active + (forward ? 1 : count - 1)) % count; LayoutFaces(); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new PresenceStackPeer(this);
    private sealed class PresenceStackPeer(PresenceStack owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        protected override string GetClassNameCore() => nameof(PresenceStack);
        protected override string GetNameCore() => $"{owner.Label}, {owner.Count}";
        protected override List<AutomationPeer>? GetChildrenCore() => null;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        public ExpandCollapseState ExpandCollapseState => owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() { if (!owner.IsEnabled) throw new ElementNotEnabledException(); owner.SetCurrentValue(IsOpenProperty, true); }
        public void Collapse() { if (!owner.IsEnabled) throw new ElementNotEnabledException(); owner.SetCurrentValue(IsOpenProperty, false); }
    }
}
