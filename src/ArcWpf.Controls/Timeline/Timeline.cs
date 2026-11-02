using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Runtime.CompilerServices;

namespace ArcWpf.Controls;

public sealed class TimelineEvent : INotifyPropertyChanged
{
    private DateTime _at = DateTime.Now; private string _title = string.Empty, _actor = string.Empty, _meta = string.Empty, _detail = string.Empty, _tone = "neutral"; private bool _isExpanded;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime At { get => _at; set => Set(ref _at, value); }
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Actor { get => _actor; set => Set(ref _actor, value); }
    public string Meta { get => _meta; set => Set(ref _meta, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string Tone { get => _tone; set => Set(ref _tone, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }
    public DateTime DayKey => At.ToLocalTime().Date;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); if (name == nameof(At)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayKey))); }
}

public class Timeline : Control
{
    public static readonly DependencyProperty EventsProperty = DependencyProperty.Register(nameof(Events), typeof(ObservableCollection<TimelineEvent>), typeof(Timeline), new PropertyMetadata(null));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Timeline), new PropertyMetadata("Activity timeline"));
    public static readonly DependencyProperty DefaultExpandedProperty = DependencyProperty.Register(nameof(DefaultExpanded), typeof(ObservableCollection<string>), typeof(Timeline), new PropertyMetadata(null, OnDefaultExpandedChanged));
    public ObservableCollection<TimelineEvent> Events { get => (ObservableCollection<TimelineEvent>)GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ObservableCollection<string> DefaultExpanded { get => (ObservableCollection<string>)GetValue(DefaultExpandedProperty); set => SetValue(DefaultExpandedProperty, value); }
    static Timeline() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(typeof(Timeline)));
    public Timeline() { Events = new ObservableCollection<TimelineEvent>(); DefaultExpanded = new ObservableCollection<string>(); }
    private static void OnDefaultExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).ApplyDefaultExpanded();
    private void ApplyDefaultExpanded()
    {
        var ids = DefaultExpanded is null ? new HashSet<string>() : DefaultExpanded.ToHashSet(StringComparer.Ordinal);
        foreach (var item in Events) item.IsExpanded = ids.Contains(item.Id);
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyDefaultExpanded();
        if (GetTemplateChild("PART_Events") is ItemsControl items)
        {
            var view = new ListCollectionView(Events);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(nameof(TimelineEvent.At), ListSortDirection.Descending));
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TimelineEvent.DayKey)));
            items.ItemsSource = view;
        }
    }
}
