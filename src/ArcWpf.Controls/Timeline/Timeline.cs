using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;

namespace ArcWpf.Controls;

public sealed class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime At { get; set; } = DateTime.Now;
    public string Title { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral";
    public bool IsExpanded { get; set; }
    public DateTime DayKey => At.ToLocalTime().Date;
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
