using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages; public partial class ReorderableListPage : UserControl { public ReorderableListPage() { InitializeComponent(); } private void Moved(object sender, ReorderableListItemMovedEventArgs e) => Status.Text = $"已移动：{e.OldIndex + 1} → {e.NewIndex + 1}"; }
