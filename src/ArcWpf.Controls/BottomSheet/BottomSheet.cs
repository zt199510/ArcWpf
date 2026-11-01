using System.Windows;
using System.Windows.Automation.Peers;

namespace ArcWpf.Controls;

public class BottomSheet : Drawer
{
    public static readonly DependencyProperty SnapHeightProperty = DependencyProperty.Register(nameof(SnapHeight), typeof(double), typeof(BottomSheet), new FrameworkPropertyMetadata(420d));
    public double SnapHeight { get => (double)GetValue(SnapHeightProperty); set => SetValue(SnapHeightProperty, value); }
    static BottomSheet() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BottomSheet), new FrameworkPropertyMetadata(typeof(Drawer))); }
    public BottomSheet() { SetCurrentValue(SideProperty, DrawerSide.Bottom); }
}
