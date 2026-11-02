using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Collections.Specialized;

namespace ArcWpf.Controls;

public sealed class AnnouncementItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Message { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public DateTimeOffset? CountdownTarget { get; set; }
}

public class AnnouncementBar : Control
{
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(nameof(Messages), typeof(ObservableCollection<string>), typeof(AnnouncementBar), new PropertyMetadata(null, OnMessagesChanged));
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(nameof(Index), typeof(int), typeof(AnnouncementBar), new PropertyMetadata(0, OnIndexChanged));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true, OnOpenChanged));
    public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(nameof(AutoPlay), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true, OnAutoPlayChanged));
    public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register(nameof(Interval), typeof(TimeSpan), typeof(AnnouncementBar), new PropertyMetadata(TimeSpan.FromSeconds(6), OnAutoPlayChanged));
    public static readonly DependencyProperty DismissibleProperty = DependencyProperty.Register(nameof(Dismissible), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(true));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(string), typeof(AnnouncementBar), new PropertyMetadata("neutral"));
    public static readonly DependencyProperty AnnouncementsProperty = DependencyProperty.Register(nameof(Announcements), typeof(ObservableCollection<AnnouncementItem>), typeof(AnnouncementBar), new PropertyMetadata(null, OnMessagesChanged));
    public static readonly DependencyProperty ControlsProperty = DependencyProperty.Register(nameof(Controls), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(AnnouncementBar), new PropertyMetadata(false, OnPausedChanged));
    public ObservableCollection<string> Messages { get => (ObservableCollection<string>)GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool AutoPlay { get => (bool)GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }
    public TimeSpan Interval { get => (TimeSpan)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public bool Dismissible { get => (bool)GetValue(DismissibleProperty); set => SetValue(DismissibleProperty, value); }
    public string Tone { get => (string)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public ObservableCollection<AnnouncementItem> Announcements { get => (ObservableCollection<AnnouncementItem>)GetValue(AnnouncementsProperty); set => SetValue(AnnouncementsProperty, value); }
    public bool Controls { get => (bool)GetValue(ControlsProperty); set => SetValue(ControlsProperty, value); }
    public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    private TextBlock? _message; private TextBlock? _countdown; private Border? _root; private readonly DispatcherTimer _timer = new(); private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) }; private INotifyCollectionChanged? _source;
    static AnnouncementBar() => DefaultStyleKeyProperty.OverrideMetadata(typeof(AnnouncementBar), new FrameworkPropertyMetadata(typeof(AnnouncementBar)));
    public event EventHandler<AnnouncementItem>? ActionInvoked;
    public AnnouncementBar() { Messages = new ObservableCollection<string>(); Announcements = new ObservableCollection<AnnouncementItem>(); _timer.Tick += (_, _) => Next(); _countdownTimer.Tick += (_, _) => UpdateCountdown(); Loaded += (_, _) => { Start(); _countdownTimer.Start(); }; Unloaded += (_, _) => { _timer.Stop(); _countdownTimer.Stop(); }; MouseEnter += (_, _) => _timer.Stop(); MouseLeave += (_, _) => Start(); GotKeyboardFocus += (_, _) => _timer.Stop(); LostKeyboardFocus += (_, _) => Start(); }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); _message = GetTemplateChild("PART_Message") as TextBlock; _countdown = GetTemplateChild("PART_Countdown") as TextBlock; _root = GetTemplateChild("Root") as Border;
        if (GetTemplateChild("PART_Previous") is System.Windows.Controls.Button previous) previous.Click += (_, _) => Previous();
        if (GetTemplateChild("PART_Next") is System.Windows.Controls.Button next) next.Click += (_, _) => Next();
        if (GetTemplateChild("PART_Pause") is System.Windows.Controls.Button pause) pause.Click += (_, _) => IsPaused = !IsPaused;
        if (GetTemplateChild("PART_Action") is System.Windows.Controls.Button action) action.Click += (_, _) => { var item = CurrentItem; if (item is not null) ActionInvoked?.Invoke(this, item); };
        if (GetTemplateChild("PART_Close") is System.Windows.Controls.Button close) close.Click += (_, _) => Close();
        AttachSource(Announcements.Count > 0 ? (INotifyCollectionChanged)Announcements : Messages);
        if (GetTemplateChild("PART_Action") is System.Windows.Controls.Button actionButton) actionButton.Content = CurrentItem?.ActionLabel;
        Update(); UpdateCountdown(); UpdateOpen(false);
    }
    private void AttachSource(INotifyCollectionChanged source)
    {
        if (_source is not null) _source.CollectionChanged -= OnCollectionChanged;
        _source = source; _source.CollectionChanged += OnCollectionChanged;
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { Update(); Start(); }
    private static void OnMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bar = (AnnouncementBar)d;
        if (bar.Announcements is { } announcements) bar.AttachSource(announcements.Count > 0 ? (INotifyCollectionChanged)announcements : bar.Messages);
        else if (bar.Messages is { } messages) bar.AttachSource(messages);
        bar.Update();
    }
    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AnnouncementBar)d).Update();
    private static void OnAutoPlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AnnouncementBar)d).Start();
    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var bar = (AnnouncementBar)d; bar.UpdateOpen(true); if (bar.IsOpen) bar.Start(); else bar._timer.Stop(); }
    private static void OnPausedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) { var bar = (AnnouncementBar)d; if (bar.IsPaused) bar._timer.Stop(); else bar.Start(); }
    private AnnouncementItem? CurrentItem => Announcements is { Count: > 0 } ? Announcements[Math.Clamp(Index, 0, Announcements.Count - 1)] : null;
    private int Count => Announcements is { Count: > 0 } ? Announcements.Count : Messages.Count;
    private void Update()
    {
        if (_message is not null) _message.Text = CurrentItem?.Message ?? (Messages is { Count: > 0 } ? Messages[Math.Clamp(Index, 0, Messages.Count - 1)] : string.Empty);
        if (GetTemplateChild("PART_Action") is System.Windows.Controls.Button action)
        {
            action.Content = CurrentItem?.ActionLabel;
            action.Visibility = string.IsNullOrWhiteSpace(CurrentItem?.ActionLabel) ? Visibility.Collapsed : Visibility.Visible;
        }
        UpdateCountdown();
    }
    private void UpdateCountdown()
    {
        if (_countdown is null) return;
        var target = CurrentItem?.CountdownTarget;
        if (target is null) { _countdown.Visibility = Visibility.Collapsed; _countdown.Text = string.Empty; return; }
        var remaining = target.Value - DateTimeOffset.UtcNow; if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        _countdown.Text = remaining.TotalDays >= 1 ? $"{remaining.Days}d {remaining.Hours:00}h" : $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        _countdown.Visibility = Visibility.Visible;
    }
    private void Start() { _timer.Stop(); if (IsLoaded && AutoPlay && !IsPaused && IsOpen && Count > 1) { _timer.Interval = Interval <= TimeSpan.Zero ? TimeSpan.FromSeconds(6) : Interval; _timer.Start(); } }
    private void Next() { if (Count > 0) Index = (Index + 1) % Count; }
    public void Previous() { if (Count > 0) Index = (Index - 1 + Count) % Count; }
    public void Close() { IsOpen = false; _timer.Stop(); }
    private void UpdateOpen(bool animate)
    {
        if (_root is null) return;
        _root.Visibility = Visibility.Visible;
        var level = ArcMotion.GetLevel(this);
        if (!IsOpen)
        {
            var height = _root.ActualHeight;
            _root.BeginAnimation(FrameworkElement.HeightProperty, null);
            if (animate && IsLoaded && level == ArcMotionLevel.Full && height > 0)
            {
                var collapse = new DoubleAnimation(height, 0, TimeSpan.FromSeconds(.28)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                collapse.Completed += (_, _) => _root.Visibility = Visibility.Collapsed;
                _root.BeginAnimation(FrameworkElement.HeightProperty, collapse);
            }
            else { _root.Height = 0; _root.Visibility = Visibility.Collapsed; }
        }
        else
        {
            _root.Visibility = Visibility.Visible; _root.Height = double.NaN;
        }
    }
}
