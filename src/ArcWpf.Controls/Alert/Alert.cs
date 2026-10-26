using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum AlertTone { Info, Success, Warning, Danger }

[TemplatePart(Name = "PART_Surface", Type = typeof(Border))]
[TemplatePart(Name = "PART_Dismiss", Type = typeof(ButtonBase))]
public class Alert : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Alert), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Alert), new FrameworkPropertyMetadata(string.Empty));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(AlertTone), typeof(Alert), new FrameworkPropertyMetadata(AlertTone.Info));
    public static readonly DependencyProperty IsDismissibleProperty = DependencyProperty.Register(nameof(IsDismissible), typeof(bool), typeof(Alert), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Alert), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));
    private static readonly DependencyPropertyKey IsPresentedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsPresented), typeof(bool), typeof(Alert), new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty IsPresentedProperty = IsPresentedPropertyKey.DependencyProperty;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public AlertTone Tone { get => (AlertTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool IsDismissible { get => (bool)GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool IsPresented => (bool)GetValue(IsPresentedProperty);
    public event EventHandler? Dismissed;

    private Border? _surface;
    private ButtonBase? _dismiss;
    private TranslateTransform? _move;
    private readonly OwnedMotion _motion = new();
    private int _transition;

    static Alert() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(typeof(Alert)));

    public Alert() => Loaded += (_, _) => Synchronize();

    public void Dismiss()
    {
        if (!IsOpen && !IsPresented) return;
        SetCurrentValue(IsOpenProperty, false);
        Transition(false);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Alert)d).Synchronize();

    private void Synchronize()
    {
        if (!IsLoaded || _surface is null) return;
        if (IsOpen) Present(); else Transition(false);
    }

    private void Present()
    {
        _motion.Stop();
        SetValue(IsPresentedPropertyKey, true);
        _surface!.Visibility = Visibility.Visible;
        _surface.IsHitTestVisible = true;
        _surface.Opacity = 0;
        if (_move is not null) _move.Y = 6;
        var level = ArcMotion.GetLevel(this);
        bool animate = level != ArcMotionLevel.Off;
        bool spring = level == ArcMotionLevel.Full;
        _motion.Double(_surface, UIElement.OpacityProperty, 1, spring ? .28 : .12, animate, completed: null);
        if (_move is not null) _motion.Double(_move, TranslateTransform.YProperty, 0, spring ? .28 : .12, animate, spring: spring);
    }

    private void Transition(bool open)
    {
        if (_surface is null) return;
        if (open) { Present(); return; }
        int token = ++_transition;
        _surface.IsHitTestVisible = false;
        var level = ArcMotion.GetLevel(this);
        bool animate = level != ArcMotionLevel.Off;
        _motion.Double(_surface, UIElement.OpacityProperty, 0, level == ArcMotionLevel.Full ? .16 : .12, animate, completed: () =>
        {
            if (token != _transition || IsOpen) return;
            SetValue(IsPresentedPropertyKey, false);
            _surface.Visibility = Visibility.Collapsed;
            Dismissed?.Invoke(this, EventArgs.Empty);
        });
        if (_move is not null) _motion.Double(_move, TranslateTransform.YProperty, 6, .16, animate, spring: level == ArcMotionLevel.Full);
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplate();
        base.OnApplyTemplate();
        _surface = GetTemplateChild("PART_Surface") as Border;
        _dismiss = GetTemplateChild("PART_Dismiss") as ButtonBase;
        if (_surface is not null)
        {
            _move = new TranslateTransform();
            _surface.RenderTransform = _move;
            _surface.RenderTransformOrigin = new Point(.5, .5);
            if (IsOpen) Present(); else { _surface.Visibility = Visibility.Collapsed; SetValue(IsPresentedPropertyKey, false); }
        }
        if (_dismiss is not null) _dismiss.Click += DismissClick;
    }

    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        ReleaseTemplate();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    private void DismissClick(object sender, RoutedEventArgs e) { Dismiss(); e.Handled = true; }
    private void ReleaseTemplate()
    {
        if (_dismiss is not null) _dismiss.Click -= DismissClick;
        _motion.Stop(); _surface = null; _dismiss = null; _move = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AlertPeer(this);
    private sealed class AlertPeer(Alert owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => AutomationProperties.GetName(owner) is { Length: > 0 } explicitName ? explicitName : owner.Title;
        protected override string GetHelpTextCore() => owner.Description;
    }
}


