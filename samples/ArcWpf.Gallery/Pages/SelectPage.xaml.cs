using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class SelectPage:UserControl
{
    private readonly SelectModel _model=new();
    public SelectPage(){InitializeComponent();DataContext=_model;}
    private void SelectResearch(object sender,RoutedEventArgs e)=>_model.Team="research";
}
public sealed record TeamOption(string Id,string Name);
public sealed class SelectModel:INotifyPropertyChanged
{
    private string _team="design";
    public string Team{get=>_team;set{if(_team==value)return;_team=value;PropertyChanged?.Invoke(this,new(nameof(Team)));}}
    public TeamOption[] Teams{get;}=[new("design","Design"),new("engineering","Engineering"),new("research","Research")];
    public string[] LongOptions{get;}=Enumerable.Range(1,25).Select(i=>$"Option {i:00}").ToArray();
    public event PropertyChangedEventHandler? PropertyChanged;
}
