using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class SearchPage : UserControl
{
    private static readonly string[] Members = ["Alex Morgan · alex@example.com", "Lin Chen · lin@example.com", "Sam Rivera · sam@example.com"];
    public ObservableCollection<string> Matches { get; } = new(Members);
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string), typeof(SearchPage), new PropertyMetadata(string.Empty, (d, _) => ((SearchPage)d).Filter()));
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    public SearchPage() { InitializeComponent(); }
    private void Filter()
    {
        Matches.Clear();
        foreach (var member in Members.Where(member => member.Contains(Query ?? string.Empty, StringComparison.CurrentCultureIgnoreCase))) Matches.Add(member);
        if (EmptyState is not null) EmptyState.Visibility = Matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
