using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class RadioCardOption
{
    public string Value { get; set; } = string.Empty;
    public object? Label { get; set; }
    public object? Description { get; set; }
    public object? Meta { get; set; }
    public object? Icon { get; set; }
    public bool Disabled { get; set; }
    public object? DisabledReason { get; set; }
}

public enum RadioCardsLayout { Grid, List }

[TemplatePart(Name = "PART_Items", Type = typeof(ItemsControl))]
public class RadioCards : Control
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(ObservableCollection<RadioCardOption>), typeof(RadioCards), new FrameworkPropertyMetadata(null, OnOptionsChanged));
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(string), typeof(RadioCards), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedChanged));
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(RadioCardsLayout), typeof(RadioCards), new FrameworkPropertyMetadata(RadioCardsLayout.Grid, OnLayoutChanged));
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth), typeof(double), typeof(RadioCards), new FrameworkPropertyMetadata(180d, OnMinColumnWidthChanged, CoerceMinColumnWidth));
    public static readonly DependencyProperty GroupLabelProperty = DependencyProperty.Register(nameof(GroupLabel), typeof(string), typeof(RadioCards), new FrameworkPropertyMetadata("Options"));
    public static readonly DependencyProperty IsGroupDisabledProperty = DependencyProperty.Register(nameof(IsGroupDisabled), typeof(bool), typeof(RadioCards), new FrameworkPropertyMetadata(false, OnGroupDisabledChanged));
    public ObservableCollection<RadioCardOption> Options { get => (ObservableCollection<RadioCardOption>)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public string? SelectedValue { get => (string?)GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }
    public RadioCardsLayout Layout { get => (RadioCardsLayout)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    public string GroupLabel { get => (string)GetValue(GroupLabelProperty); set => SetValue(GroupLabelProperty, value); }
    public bool IsGroupDisabled { get => (bool)GetValue(IsGroupDisabledProperty); set => SetValue(IsGroupDisabledProperty, value); }
    public event EventHandler? SelectedValueChanged;
    static RadioCards() { DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioCards), new FrameworkPropertyMetadata(typeof(RadioCards))); }
    private Canvas? _overlay; private Border? _ring;
    public RadioCards() { Options = new ObservableCollection<RadioCardOption>(); Loaded += (_, _) => Refresh(); }
    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((RadioCards)d).Refresh();
    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((RadioCards)d).Refresh();
    internal void Select(RadioCardOption option) { if (IsGroupDisabled || option.Disabled || SelectedValue == option.Value) return; SelectedValue = option.Value; SelectedValueChanged?.Invoke(this, EventArgs.Empty); }
    public void SelectOption(RadioCardOption option) => Select(option);
    internal bool Usable(int index) => index >= 0 && index < Options.Count && !IsGroupDisabled && !Options[index].Disabled;
    internal bool IsOptionTabStop(RadioCardOption option)
    {
        if (IsGroupDisabled || option.Disabled) return false;
        var selected = Options.FirstOrDefault(candidate => candidate.Value == SelectedValue);
        var target = selected is not null && !selected.Disabled ? selected : Options.FirstOrDefault(candidate => !candidate.Disabled);
        return ReferenceEquals(target, option);
    }
    internal void Move(int from, int delta) { if (Options.Count == 0) return; int at = from; for (int i = 0; i < Options.Count; i++) { at = (at + delta + Options.Count) % Options.Count; if (Usable(at)) { Select(Options[at]); if (Template.FindName("PART_Items", this) is ItemsControl list && list.ItemContainerGenerator.ContainerFromIndex(at) is RadioCardItem item) item.Focus(); return; } } }
    private void MoveToBoundary(bool first)
    {
        var range = first ? Enumerable.Range(0, Options.Count) : Enumerable.Range(0, Options.Count).Reverse();
        foreach (var index in range) if (Usable(index)) { Select(Options[index]); if (Template?.FindName("PART_Items", this) is ItemsControl list && list.ItemContainerGenerator.ContainerFromIndex(index) is RadioCardItem item) item.Focus(); break; }
    }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); _overlay = Template.FindName("PART_Overlay", this) as Canvas; _ring = Template.FindName("PART_Ring", this) as Border; Refresh(); }
    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((RadioCards)d).Refresh();
    private static void OnMinColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((RadioCards)d).Refresh();
    private static void OnGroupDisabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs _) => ((RadioCards)d).Refresh();
    private static object CoerceMinColumnWidth(DependencyObject _, object value) => Math.Max(1d, (double)value);
    private void Refresh() { if (Template is null) return; if (Template.FindName("PART_Items", this) is RadioCardsItems list) { list.Owner = this; list.ItemsSource = Options; list.ItemsPanel = CreatePanel(Layout); AutomationProperties.SetName(list, GroupLabel); list.InvalidateMeasure(); } UpdateRing(); }
    private void UpdateRing()
    {
        if (_overlay is null || _ring is null) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_overlay is null || _ring is null) return;
            var list = Template?.FindName("PART_Items", this) as RadioCardsItems;
            var index = Options.ToList().FindIndex(x => x.Value == SelectedValue);
            var card = index < 0 || list is null ? null : list.ItemContainerGenerator.ContainerFromIndex(index) as RadioCardItem;
            if (card is null || card.ActualWidth <= 0 || card.ActualHeight <= 0) { _ring.Visibility = Visibility.Collapsed; return; }
            var point = card.TranslatePoint(new Point(0, 0), _overlay);
            var level = ArcMotion.GetLevel(this);
            _ring.Visibility = Visibility.Visible;
            MoveRing(Canvas.LeftProperty, point.X, level);
            MoveRing(Canvas.TopProperty, point.Y, level);
            MoveRing(FrameworkElement.WidthProperty, card.ActualWidth, level);
            MoveRing(FrameworkElement.HeightProperty, card.ActualHeight, level);
        }));
    }
    private void MoveRing(DependencyProperty property, double value, ArcMotionLevel level)
    {
        if (_ring is null) return;
        var current = property == Canvas.LeftProperty ? Canvas.GetLeft(_ring)
            : property == Canvas.TopProperty ? Canvas.GetTop(_ring)
            : (double)_ring.GetValue(property);
        if (double.IsNaN(current) || double.IsInfinity(current))
        {
            if (property == Canvas.LeftProperty) Canvas.SetLeft(_ring, value);
            else if (property == Canvas.TopProperty) Canvas.SetTop(_ring, value);
            else _ring.SetValue(property, value);
            current = value;
        }
        if (level == ArcMotionLevel.Off || !IsLoaded) { if (property == Canvas.LeftProperty) Canvas.SetLeft(_ring, value); else if (property == Canvas.TopProperty) Canvas.SetTop(_ring, value); else _ring.SetValue(property, value); return; }
        var animation = new DoubleAnimation(current, value, TimeSpan.FromMilliseconds(level == ArcMotionLevel.Full ? 280 : 120)) { EasingFunction = level == ArcMotionLevel.Full ? new BackEase { Amplitude = .12, EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseOut } };
        _ring.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private static ItemsPanelTemplate CreatePanel(RadioCardsLayout layout)
    {
        var root = layout == RadioCardsLayout.List ? new FrameworkElementFactory(typeof(StackPanel)) : new FrameworkElementFactory(typeof(WrapPanel));
        if (layout == RadioCardsLayout.List) root.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        return new ItemsPanelTemplate(root);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e) { base.OnPreviewKeyDown(e); if (e.Handled || e.OriginalSource is not RadioCardItem item) return; int index = Options.IndexOf(item.Option!); if (e.Key is Key.Left or Key.Up) { Move(index, -1); e.Handled = true; } else if (e.Key is Key.Right or Key.Down) { Move(index, 1); e.Handled = true; } else if (e.Key == Key.Home) { MoveToBoundary(true); e.Handled = true; } else if (e.Key == Key.End) { MoveToBoundary(false); e.Handled = true; } }
}

public sealed class RadioCardsItems : ItemsControl
{
    public RadioCards? Owner { get; set; }
    protected override DependencyObject GetContainerForItemOverride() => new RadioCardItem();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is RadioCardItem;
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item) { base.PrepareContainerForItemOverride(element, item); if (element is RadioCardItem card) { card.Option = item as RadioCardOption; card.Owner = Owner; card.IsSelected = card.Option?.Value == Owner?.SelectedValue; card.MinWidth = Owner?.MinColumnWidth ?? 180; card.IsEnabled = Owner is not null && !Owner.IsGroupDisabled && card.Option?.Disabled != true; card.IsTabStop = card.Option is not null && Owner?.IsOptionTabStop(card.Option) == true; card.DisplayDescription = card.Option?.Disabled == true && card.Option.DisabledReason is not null ? card.Option.DisabledReason : card.Option?.Description; } }
}

public sealed class RadioCardItem : ContentControl
{
    public static readonly DependencyProperty OptionProperty = DependencyProperty.Register(nameof(Option), typeof(RadioCardOption), typeof(RadioCardItem));
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(RadioCardItem), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(RadioCards), typeof(RadioCardItem));
    public static readonly DependencyProperty DisplayDescriptionProperty = DependencyProperty.Register(nameof(DisplayDescription), typeof(object), typeof(RadioCardItem));
    public RadioCardOption? Option { get => (RadioCardOption?)GetValue(OptionProperty); set => SetValue(OptionProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public RadioCards? Owner { get => (RadioCards?)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    public object? DisplayDescription { get => GetValue(DisplayDescriptionProperty); set => SetValue(DisplayDescriptionProperty, value); }
    static RadioCardItem() { DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioCardItem), new FrameworkPropertyMetadata(typeof(RadioCardItem))); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); if (Owner is not null && Option is not null) Owner.Select(Option); Focus(); e.Handled = true; }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key is Key.Enter or Key.Space && Owner is not null && Option is not null) { Owner.Select(Option); e.Handled = true; } }
}
