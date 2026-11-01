using System.Windows;
using System.Windows.Data;

namespace ArcWpf.Controls;

public class BottomSheet : Drawer
{
    public static readonly DependencyProperty SnapHeightProperty = DependencyProperty.Register(nameof(SnapHeight), typeof(double), typeof(BottomSheet), new FrameworkPropertyMetadata(420d), value => value is double height && double.IsFinite(height) && height >= 0);
    public double SnapHeight { get => (double)GetValue(SnapHeightProperty); set => SetValue(SnapHeightProperty, value); }
    static BottomSheet() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BottomSheet), new FrameworkPropertyMetadata(typeof(Drawer))); }
    public BottomSheet() { SetCurrentValue(SideProperty, DrawerSide.Bottom); }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Surface") is not FrameworkElement surface) return;
        BindingOperations.SetBinding(surface, HeightProperty, new Binding(nameof(SnapHeight)) { Source = this });
        BindingOperations.SetBinding(surface, MaxHeightProperty, new Binding(nameof(ActualHeight))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DrawerHost), 1)
        });
    }
}
