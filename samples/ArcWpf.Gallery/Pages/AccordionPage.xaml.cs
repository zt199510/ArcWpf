using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class AccordionPage : UserControl
{
    public AccordionPage() => InitializeComponent();
    private void OpenSecond(object sender, RoutedEventArgs e) => BoundAccordion.ExpandedIndex = 1;
}
