using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ExpandingSearchPage : UserControl
{
    public ExpandingSearchPage() { InitializeComponent(); Search.Items.Add(new ExpandingSearchItem { Title = "Release checklist", Meta = "Engineering · Edited Sep 18", IconGlyph = "▤" }); Search.Items.Add(new ExpandingSearchItem { Title = "Billing migration", Meta = "Payments · Due Oct 14", IconGlyph = "▥" }); Search.Items.Add(new ExpandingSearchItem { Title = "Pricing page copy", Meta = "Marketing · Edited Sep 20", IconGlyph = "▤" }); }
    private void Submitted(object sender, RoutedEventArgs e) => Status.Text = string.IsNullOrWhiteSpace(Search.Query) ? "等待搜索" : $"搜索：{Search.Query}";
}
