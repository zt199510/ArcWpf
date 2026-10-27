using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class AvatarGroupMember : INotifyPropertyChanged
{
    private string _displayName = "";
    private ImageSource? _source;
    private AvatarStatus _status;
    public string DisplayName { get => _displayName; set { if (_displayName == value) return; _displayName = value; Changed(nameof(DisplayName)); } }
    public ImageSource? Source { get => _source; set { if (ReferenceEquals(_source, value)) return; _source = value; Changed(nameof(Source)); } }
    public AvatarStatus Status { get => _status; set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); if (_status == value) return; _status = value; Changed(nameof(Status)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[TemplatePart(Name = "PART_Items", Type = typeof(AvatarGroupPanel))]
public class AvatarGroup : Control
{
    public static readonly DependencyProperty MembersProperty = DependencyProperty.Register(nameof(Members), typeof(IEnumerable<AvatarGroupMember>), typeof(AvatarGroup), new PropertyMetadata(null, MembersChanged));
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(int), typeof(AvatarGroup), new FrameworkPropertyMetadata(4, (d, _) => ((AvatarGroup)d).Refresh(true), (_, v) => Math.Max(0, (int)v)));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(AvatarSize), typeof(AvatarGroup), new PropertyMetadata(AvatarSize.Medium, (d, _) => ((AvatarGroup)d).Refresh(false)), v => v is AvatarSize.Small or AvatarSize.Medium or AvatarSize.Large);
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(AvatarGroup), new PropertyMetadata("Team members", (d, _) => ((AvatarGroup)d).UpdateLabels()));
    private static readonly DependencyPropertyKey OverflowCountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(OverflowCount), typeof(int), typeof(AvatarGroup), new PropertyMetadata(0));
    public static readonly DependencyProperty OverflowCountProperty = OverflowCountPropertyKey.DependencyProperty;
    public IEnumerable<AvatarGroupMember>? Members { get => (IEnumerable<AvatarGroupMember>?)GetValue(MembersProperty); set => SetValue(MembersProperty, value); }
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public AvatarSize Size { get => (AvatarSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public int OverflowCount => (int)GetValue(OverflowCountProperty);
    private AvatarGroupPanel? _panel;
    private INotifyCollectionChanged? _collection;
    private readonly List<AvatarGroupItem> _slots = [];
    private AvatarGroupItem? _hovered;
    private bool _over;
    internal IReadOnlyList<AvatarGroupItem> Slots => _slots;
    internal bool IsMotionRunning => _slots.Any(s => s.IsMotionRunning);
    internal double Diameter => Size switch { AvatarSize.Small => 28, AvatarSize.Large => 48, _ => 36 };
    private bool Animate => IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
    static AvatarGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(typeof(AvatarGroup)));
        FocusableProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(AvatarGroup), new FrameworkPropertyMetadata(false));
    }
    public AvatarGroup()
    {
        SetCurrentValue(MembersProperty, new ObservableCollection<AvatarGroupMember>());
        Loaded += (_, _) => { Observe(); Refresh(false); };
        Unloaded += (_, _) => { Unobserve(); Settle(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Settle(); else Refresh(false); };
    }
    private static void MembersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    { var group = (AvatarGroup)d; group.Unobserve(); group.Observe(); group.Refresh(true); }
    private void Observe() { if (!IsLoaded || _collection is not null) return; _collection = Members as INotifyCollectionChanged; if (_collection is not null) _collection.CollectionChanged += CollectionChanged; }
    private void Unobserve() { if (_collection is not null) _collection.CollectionChanged -= CollectionChanged; _collection = null; }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh(true);
    public override void OnApplyTemplate() { Release(); base.OnApplyTemplate(); _panel = GetTemplateChild("PART_Items") as AvatarGroupPanel; Observe(); Refresh(false); }
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate) { Release(); base.OnTemplateChanged(oldTemplate, newTemplate); }
    private void Release()
    {
        Unobserve(); foreach (var slot in _slots) slot.DisposeVisual(); _slots.Clear(); _panel?.Children.Clear(); _panel = null; _hovered = null; _over = false;
    }
    private void Refresh(bool requested)
    {
        var members = Members?.Where(m => m is not null).ToList() ?? [];
        int visible = Math.Min(Max, members.Count); int overflow = members.Count - visible;
        SetValue(OverflowCountPropertyKey, overflow);
        if (_panel is null) return;
        bool animate = requested && Animate;
        _panel.ItemHeight = Diameter;
        var remaining = _slots.ToList(); var target = new List<AvatarGroupItem>();
        for (int i = 0; i < visible + (overflow > 0 ? 1 : 0); i++)
        {
            var member = i < visible ? members[i] : null;
            var item = remaining.FirstOrDefault(s => ReferenceEquals(s.Member, member));
            bool created = item is null;
            if (created) item = new AvatarGroupItem(this, member);
            else remaining.Remove(item!);
            item!.IsExiting = false; item.SetGeometry(Diameter, Size);
            target.Add(item);
            if (member is null) item.SetCount(overflow, requested && IsLoaded && IsVisible);
            item.SetPresence(true, animate && (created || item.IsClosing), created);
        }
        foreach (var removed in remaining)
        {
            removed.IsExiting = true;
            if (!animate) { _panel.Children.Remove(removed); removed.DisposeVisual(); continue; }
            int index = Math.Min(_slots.IndexOf(removed), target.Count); target.Insert(Math.Max(0, index), removed);
            if (!removed.IsClosing) removed.SetPresence(false, true, false, () => Dispatcher.BeginInvoke(new Action(() => RemoveExit(removed))));
        }
        _slots.Clear(); _slots.AddRange(target);
        for (int i = 0; i < target.Count; i++)
        {
            if (i < _panel.Children.Count && ReferenceEquals(_panel.Children[i], target[i])) continue;
            _panel.Children.Remove(target[i]); _panel.Children.Insert(i, target[i]);
        }
        UpdateLabels(); UpdateHover(requested); InvalidateGroupPeer();
    }
    private void RemoveExit(AvatarGroupItem item)
    { if (!item.IsExiting) return; _panel?.Children.Remove(item); _slots.Remove(item); item.DisposeVisual(); if (ReferenceEquals(_hovered, item)) _hovered = null; UpdateHover(true); InvalidateGroupPeer(); }
    private void UpdateLabels() { foreach (var slot in _slots) { slot.GroupLabel = Label ?? ""; UIElementAutomationPeer.FromElement(slot)?.InvalidatePeer(); } InvalidateGroupPeer(); }
    private void InvalidateGroupPeer()
    {
        if (UIElementAutomationPeer.FromElement(this) is not { } peer) return;
        peer.ResetChildrenCache(); peer.InvalidatePeer();
    }
    private void Settle()
    {
        _over = false; _hovered = null;
        foreach (var slot in _slots.ToArray()) { if (slot.IsExiting) RemoveExit(slot); else slot.Settle(); }
        UpdateHover(false);
    }
    internal void Pointer(AvatarGroupItem? item, bool entered)
    { if (entered) { _over = true; _hovered = item; } else if (ReferenceEquals(item, _hovered)) _hovered = null; UpdateHover(true); }
    protected override void OnMouseEnter(MouseEventArgs e) { base.OnMouseEnter(e); if (e.StylusDevice is null) { _over = true; UpdateHover(true); } }
    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); _over = false; _hovered = null; UpdateHover(true); }
    private void UpdateHover(bool animate)
    {
        var active = _slots.Where(s => !s.IsExiting).ToList(); int hovered = active.IndexOf(_hovered!);
        for (int i = 0; i < active.Count; i++)
        {
            bool selected = hovered == i; double x = _over ? (i - (active.Count - 1) / 2d) * 4 : 0;
            if (hovered >= 0 && !selected) x += i < hovered ? -3 : 3;
            active[i].SetHover(x, selected ? -2 : 0, selected, animate);
            Panel.SetZIndex(active[i], selected ? 10000 : i);
        }
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    { base.OnPropertyChanged(e); if (_slots is not null && e.Property == ArcMotion.LevelProperty) { Settle(); Refresh(false); } }
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);
    private sealed class GroupPeer(AvatarGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(AvatarGroup);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() { string explicitName = base.GetNameCore(); return string.IsNullOrEmpty(explicitName) ? owner.Label ?? "" : explicitName; }
        protected override List<AutomationPeer> GetChildrenCore() => owner._slots.Where(s => !s.IsExiting).Select(s => UIElementAutomationPeer.CreatePeerForElement(s)!).ToList();
    }
}

public sealed class AvatarGroupPanel : Panel
{
    private double _itemHeight = 36;
    internal double ItemHeight { get => _itemHeight; set { if (_itemHeight == value) return; _itemHeight = value; InvalidateMeasure(); } }
    protected override Size MeasureOverride(Size available)
    { double width = 6; foreach (UIElement child in InternalChildren) { child.Measure(new Size(double.PositiveInfinity, ItemHeight)); width += child.DesiredSize.Width; } return new Size(width, ItemHeight); }
    protected override Size ArrangeOverride(Size finalSize)
    { double x = 6; foreach (UIElement child in InternalChildren) { child.Arrange(new Rect(x, 0, child.DesiredSize.Width, ItemHeight)); x += child.DesiredSize.Width; } return finalSize; }
}
