using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class PaginationPage : UserControl
{
    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(PaginationPage), new PropertyMetadata(1));
    public int CurrentPage { get => (int)GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public PaginationPage() => InitializeComponent();
}
