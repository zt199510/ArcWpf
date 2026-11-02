using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ArcWpf.Controls;

public sealed class ActionSwapState
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public object? Icon { get; set; }
    public ActionSwapTone Tone { get; set; } = ActionSwapTone.Neutral;
    public string? PendingLabel { get; set; }
    public int HoldFor { get; set; }
}
public enum ActionSwapTone { Neutral, Accent, Success, Warning, Quiet, Danger }

public class ActionSwap : Button
{
    public static readonly DependencyProperty StatesProperty = DependencyProperty.Register(nameof(States), typeof(ObservableCollection<ActionSwapState>), typeof(ActionSwap), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ActionSwap), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty FailureLabelProperty = DependencyProperty.Register(nameof(FailureLabel), typeof(string), typeof(ActionSwap), new FrameworkPropertyMetadata("Try again"));
    public static readonly DependencyProperty OnActionProperty = DependencyProperty.Register(nameof(OnAction), typeof(Func<ActionSwapState, Task>), typeof(ActionSwap), new FrameworkPropertyMetadata(null));
    public static readonly DependencyProperty IsWorkingProperty = DependencyProperty.Register(nameof(IsWorking), typeof(bool), typeof(ActionSwap), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(ActionSwapTone), typeof(ActionSwap), new FrameworkPropertyMetadata(ActionSwapTone.Neutral));
    public ObservableCollection<ActionSwapState> States { get => (ObservableCollection<ActionSwapState>)GetValue(StatesProperty); set => SetValue(StatesProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string FailureLabel { get => (string)GetValue(FailureLabelProperty); set => SetValue(FailureLabelProperty, value); }
    public Func<ActionSwapState, Task>? OnAction { get => (Func<ActionSwapState, Task>?)GetValue(OnActionProperty); set => SetValue(OnActionProperty, value); }
    public bool IsWorking { get => (bool)GetValue(IsWorkingProperty); private set => SetValue(IsWorkingProperty, value); }
    public ActionSwapTone Tone { get => (ActionSwapTone)GetValue(ToneProperty); private set => SetValue(ToneProperty, value); }
    public event EventHandler? ValueChanged;
    private DispatcherTimer? _holdTimer;
    static ActionSwap() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ActionSwap), new FrameworkPropertyMetadata(typeof(ActionSwap)));
    public ActionSwap() { SetValue(StatesProperty, new ObservableCollection<ActionSwapState>()); Click += (_, _) => Advance(); Loaded += (_, _) => Refresh(); Unloaded += (_, _) => _holdTimer?.Stop(); }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ActionSwap)d).Refresh();
    private ActionSwapState? Current => States.FirstOrDefault(s => s.Id == Value) ?? States.FirstOrDefault();
    private ActionSwapState? Next => States.Count == 0 ? null : States[(Math.Max(0, States.IndexOf(Current!)) + 1) % States.Count];
    private void Refresh() { if (Current is { } state) { SetCurrentValue(ContentProperty, state.Label); SetCurrentValue(ToneProperty, state.Tone); AutomationProperties.SetName(this, state.Label); } }
    public async void Advance()
    {
        if (!IsEnabled || IsWorking || States.Count < 2 || Next is not { } next) return;
        IsWorking = true; SetCurrentValue(ContentProperty, next.PendingLabel ?? "Working");
        try { if (OnAction is { } action) await action(next); SetCurrentValue(ValueProperty, next.Id); IsWorking = false; Refresh(); ValueChanged?.Invoke(this, EventArgs.Empty); if (next.HoldFor > 0) { _holdTimer?.Stop(); _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(next.HoldFor) }; _holdTimer.Tick += (_, _) => { _holdTimer!.Stop(); if (States.Count > 0) SetCurrentValue(ValueProperty, States[0].Id); }; _holdTimer.Start(); } }
        catch { IsWorking = false; SetCurrentValue(ContentProperty, FailureLabel); }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}
