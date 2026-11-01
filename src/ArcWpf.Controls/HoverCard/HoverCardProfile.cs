using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcWpf.Controls;

public sealed class HoverCardStat
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public class HoverCardProfile : Control
{
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(nameof(Role), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty BioProperty = DependencyProperty.Register(nameof(Bio), typeof(string), typeof(HoverCardProfile), new PropertyMetadata(""));
    public static readonly DependencyProperty AvatarSourceProperty = DependencyProperty.Register(nameof(AvatarSource), typeof(ImageSource), typeof(HoverCardProfile));
    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(nameof(Meta), typeof(object), typeof(HoverCardProfile));

    public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public string Role { get => (string)GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string Bio { get => (string)GetValue(BioProperty); set => SetValue(BioProperty, value); }
    public ImageSource? AvatarSource { get => (ImageSource?)GetValue(AvatarSourceProperty); set => SetValue(AvatarSourceProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public ObservableCollection<HoverCardStat> Stats { get; } = [];
    private readonly OwnedMotion _motion = new();
    private FrameworkElement?[] _rows = [];
    private TranslateTransform?[] _moves = [];

    static HoverCardProfile() => DefaultStyleKeyProperty.OverrideMetadata(typeof(HoverCardProfile), new FrameworkPropertyMetadata(typeof(HoverCardProfile)));
    public HoverCardProfile()
    {
        Loaded += (_, _) => Settle();
        Unloaded += (_, _) => { _motion.Stop(); ResetRows(); };
    }
    public override void OnApplyTemplate()
    {
        _motion.Stop();
        base.OnApplyTemplate();
        _rows = [GetTemplateChild("PART_Head") as FrameworkElement, GetTemplateChild("Bio") as FrameworkElement,
            GetTemplateChild("Stats") as FrameworkElement, GetTemplateChild("Meta") as FrameworkElement];
        _moves = new TranslateTransform?[_rows.Length];
        for (int index = 0; index < _rows.Length; index++)
        {
            if (_rows[index] is null) continue;
            _moves[index] = new TranslateTransform();
            _rows[index]!.RenderTransform = _moves[index];
        }
        if (IsLoaded) Settle();
    }
    private void Settle()
    {
        _motion.Stop();
        bool animate = IsLoaded && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        // 源码中的头像、简介、数据与尾注按 35ms 顺序进入。
        for (int index = 0; index < _rows.Length; index++)
        {
            if (_rows[index] is not { Visibility: Visibility.Visible } row || _moves[index] is not { } move) continue;
            row.Opacity = animate ? 0 : 1;
            move.Y = animate ? 4 : 0;
            double delay = .04 + index * .035;
            _motion.Double(row, OpacityProperty, 1, .24, animate, delay: delay, enter: true);
            _motion.Double(move, TranslateTransform.YProperty, 0, .24, animate, delay: delay, enter: true);
        }
    }
    private void ResetRows()
    {
        for (int index = 0; index < _rows.Length; index++)
        {
            if (_rows[index] is not null) _rows[index]!.Opacity = 1;
            if (_moves[index] is not null) _moves[index]!.Y = 0;
        }
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ArcMotion.LevelProperty && ArcMotion.GetLevel(this) != ArcMotionLevel.Full)
        {
            _motion.Stop();
            ResetRows();
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ProfilePeer(this);
    private sealed class ProfilePeer(HoverCardProfile owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetNameCore() => owner.DisplayName;
        protected override string GetHelpTextCore() => string.Join(". ", new[] { owner.Role, owner.Bio }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
