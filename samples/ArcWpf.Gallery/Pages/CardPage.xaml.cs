using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages; public partial class CardPage : UserControl { public CardPage() { InitializeComponent(); } private void OpenProject(object sender, System.Windows.RoutedEventArgs e) => ProjectCard.IsOpen = true; }
