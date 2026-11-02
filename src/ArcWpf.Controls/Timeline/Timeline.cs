using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;

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
    public string DisplayTime { get; private set; } = string.Empty;
    public string FullTime { get; private set; } = string.Empty;
    internal void SetDisplayTime(string display, string full) { DisplayTime = display; FullTime = full; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullTime))); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); if (name == nameof(At)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayKey))); }
}

public class Timeline : Control
{
    public static readonly DependencyProperty EventsProperty = DependencyProperty.Register(nameof(Events), typeof(ObservableCollection<TimelineEvent>), typeof(Timeline), new PropertyMetadata(null));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Timeline), new PropertyMetadata("Activity timeline"));
    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(nameof(Now), typeof(DateTime), typeof(Timeline), new PropertyMetadata(DateTime.UtcNow, OnTimeChanged));
    public static readonly DependencyProperty TimeZoneIdProperty = DependencyProperty.Register(nameof(TimeZoneId), typeof(string), typeof(Timeline), new PropertyMetadata("UTC", OnTimeChanged));
    public static readonly DependencyProperty LocaleProperty = DependencyProperty.Register(nameof(Locale), typeof(string), typeof(Timeline), new PropertyMetadata("en-US", OnTimeChanged));
    public static readonly DependencyProperty DefaultExpandedProperty = DependencyProperty.Register(nameof(DefaultExpanded), typeof(ObservableCollection<string>), typeof(Timeline), new PropertyMetadata(null, OnDefaultExpandedChanged));
    public ObservableCollection<TimelineEvent> Events { get => (ObservableCollection<TimelineEvent>)GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public DateTime Now { get => (DateTime)GetValue(NowProperty); set => SetValue(NowProperty, value); }
    public string TimeZoneId { get => (string)GetValue(TimeZoneIdProperty); set => SetValue(TimeZoneIdProperty, value); }
    public string Locale { get => (string)GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }
    public ObservableCollection<string> DefaultExpanded { get => (ObservableCollection<string>)GetValue(DefaultExpandedProperty); set => SetValue(DefaultExpandedProperty, value); }
    static Timeline() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(typeof(Timeline)));
    public Timeline() { Events = new ObservableCollection<TimelineEvent>(); DefaultExpanded = new ObservableCollection<string>(); Focusable = true; PreviewKeyDown += OnPreviewKeyDown; }
    private static void OnDefaultExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).ApplyDefaultExpanded();
    private void ApplyDefaultExpanded()
    {
        var ids = DefaultExpanded is null ? new HashSet<string>() : DefaultExpanded.ToHashSet(StringComparer.Ordinal);
        foreach (var item in Events) item.IsExpanded = ids.Contains(item.Id);
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyDefaultExpanded(); UpdateTimeLabels();
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
    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Timeline)d).UpdateTimeLabels();
    private void UpdateTimeLabels()
    {
        if (Events is null) return;
        TimeZoneInfo zone; try { zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); } catch { zone = TimeZoneInfo.Utc; }
        CultureInfo culture; try { culture = CultureInfo.GetCultureInfo(Locale); } catch { culture = CultureInfo.InvariantCulture; }
        var now = new DateTimeOffset(Now.ToUniversalTime());
        foreach (var item in Events)
        {
            var instant = new DateTimeOffset(item.At.ToUniversalTime()); var minutes = Math.Max(0, (int)(now - instant).TotalMinutes);
            var display = minutes < 1 ? "Now" : minutes < 60 ? $"{minutes}m" : minutes < 1440 ? $"{minutes / 60}h" : TimeZoneInfo.ConvertTime(instant, zone).ToString("t", culture);
            item.SetDisplayTime(display, TimeZoneInfo.ConvertTime(instant, zone).ToString("f", culture));
        }
    }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End or Key.Enter or Key.Space)) return;
        var focusable = FindVisualChildren<Expander>(this).Where(expander => expander.IsKeyboardFocusWithin || Keyboard.FocusedElement == expander).ToArray();
        var all = FindVisualChildren<Expander>(this).Where(expander => expander.IsEnabled).ToArray();
        if (all.Length == 0) return;
        var current = Array.IndexOf(all, focusable.FirstOrDefault());
        if (e.Key is Key.Enter or Key.Space && current >= 0) { all[current].IsExpanded = !all[current].IsExpanded; e.Handled = true; return; }
        var next = e.Key == Key.Home ? 0 : e.Key == Key.End ? all.Length - 1 : Math.Clamp(current + (e.Key == Key.Down ? 1 : -1), 0, all.Length - 1); all[next].Focus(); e.Handled = true;
    }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject { if (root is null) yield break; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in FindVisualChildren<T>(child)) yield return nested; } }
}
