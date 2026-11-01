using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages; public partial class BottomSheetPage : UserControl { private readonly BottomSheet _sheet = new() { Title = "Bottom sheet", Description = "Drawer-backed bottom surface" }; public BottomSheetPage() { InitializeComponent(); Host.Drawer = _sheet; } private void Open(object sender, RoutedEventArgs e) => _sheet.Show(this); }
