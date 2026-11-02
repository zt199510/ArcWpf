using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcWpf.Controls;

public enum CountdownVariant { Large, Compact }

public class Countdown : Control
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(DateTime), typeof(Countdown), new FrameworkPropertyMetadata(DateTime.UtcNow, OnTargetChanged));
    public static readonly DependencyProperty TargetTextProperty = DependencyProperty.Register(nameof(TargetText), typeof(string), typeof(Countdown), new FrameworkPropertyMetadata(string.Empty, OnTargetChanged));
    public static readonly DependencyProperty TimeZoneIdProperty = DependencyProperty.Register(nameof(TimeZoneId), typeof(string), typeof(Countdown), new FrameworkPropertyMetadata("UTC", OnTargetChanged));
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(CountdownVariant), typeof(Countdown), new PropertyMetadata(CountdownVariant.Large));
    public static readonly DependencyProperty CompleteLabelProperty = DependencyProperty.Register(nameof(CompleteLabel), typeof(string), typeof(Countdown), new PropertyMetadata("Live now"));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Countdown), new PropertyMetadata("Time remaining"));
    private static readonly DependencyPropertyKey IsCompletePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsComplete), typeof(bool), typeof(Countdown), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompleteProperty = IsCompletePropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey RemainingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Remaining), typeof(TimeSpan), typeof(Countdown), new PropertyMetadata(TimeSpan.Zero));
    public static readonly DependencyProperty RemainingProperty = RemainingPropertyKey.DependencyProperty;
    public static readonly RoutedEvent CompletedEvent = EventManager.RegisterRoutedEvent(nameof(Completed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Countdown));
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
    private TextBlock? _days, _hours, _minutes, _seconds, _compact;
    private readonly Dictionary<TextBlock, string> _lastDigits = new();
    public DateTime Target { get => (DateTime)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public string TargetText { get => (string)GetValue(TargetTextProperty); set => SetValue(TargetTextProperty, value); }
    public string TimeZoneId { get => (string)GetValue(TimeZoneIdProperty); set => SetValue(TimeZoneIdProperty, value); }
    public CountdownVariant Variant { get => (CountdownVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public string CompleteLabel { get => (string)GetValue(CompleteLabelProperty); set => SetValue(CompleteLabelProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsComplete => (bool)GetValue(IsCompleteProperty);
    public TimeSpan Remaining => (TimeSpan)GetValue(RemainingProperty);
    public event RoutedEventHandler Completed { add => AddHandler(CompletedEvent, value); remove => RemoveHandler(CompletedEvent, value); }
    static Countdown() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Countdown), new FrameworkPropertyMetadata(typeof(Countdown)));
    public Countdown() { _timer.Tick += (_, _) => Update(); Loaded += (_, _) => { _timer.Start(); Update(); }; Unloaded += (_, _) => _timer.Stop(); }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _days = GetTemplateChild("Days") as TextBlock; _hours = GetTemplateChild("Hours") as TextBlock;
        _minutes = GetTemplateChild("Minutes") as TextBlock; _seconds = GetTemplateChild("Seconds") as TextBlock; _compact = GetTemplateChild("CompactText") as TextBlock; Update();
    }
    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Countdown)d).Update();
    private void Update()
    {
        var old = IsComplete; var target = ResolveTarget(); var left = target - DateTimeOffset.UtcNow; if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        SetValue(RemainingPropertyKey, left); SetValue(IsCompletePropertyKey, left == TimeSpan.Zero);
        SetDigits(_days, left.Days.ToString("00"));
        SetDigits(_hours, left.Hours.ToString("00"));
        SetDigits(_minutes, left.Minutes.ToString("00"));
        SetDigits(_seconds, left.Seconds.ToString("00"));
        if (_compact is not null)
        {
            var compact = left.TotalDays >= 1 ? $"{left.Days}d {left.Hours:00}h" : left.TotalHours >= 1 ? $"{left.Hours:00}h {left.Minutes:00}m" : $"{left.Minutes:00}m {left.Seconds:00}s";
            _compact.Text = IsComplete ? CompleteLabel : compact;
        }
        if (!old && IsComplete) RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
    }
    private void SetDigits(TextBlock? block, string value)
    {
        if (block is null) return;
        var old = _lastDigits.TryGetValue(block, out var previous) ? previous : value;
        block.Text = value; _lastDigits[block] = value;
        if (!IsLoaded || old == value || ArcMotion.GetLevel(this) != ArcMotionLevel.Full) return;
        var move = block.RenderTransform as TranslateTransform ?? new TranslateTransform();
        block.RenderTransform = move; move.Y = old.CompareTo(value) > 0 ? 7 : -7; block.Opacity = .45;
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(move.Y, 0, TimeSpan.FromSeconds(.2)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        block.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromSeconds(.2)));
    }
    private DateTimeOffset ResolveTarget()
    {
        if (string.IsNullOrWhiteSpace(TargetText)) return new DateTimeOffset(Target.ToUniversalTime());
        bool hasOffset = TargetText.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(TargetText, @"[+-]\d{2}:?\d{2}$");
        if (hasOffset && DateTimeOffset.TryParse(TargetText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var exact)) return exact.ToUniversalTime();
        if (DateTime.TryParse(TargetText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var wall))
        {
            try { var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), zone)); } catch { }
        }
        return new DateTimeOffset(Target.ToUniversalTime());
    }
}
