using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TabsPage:UserControl
{
    private readonly TabsModel _model=new();
    public TabsPage(){InitializeComponent();DataContext=_model;}
    private void OpenEditor(object sender,RoutedEventArgs e)=>_model.View="editor";
}
public sealed class TabsModel:INotifyPropertyChanged
{
    private string _view="summary";
    public string View{get=>_view;set{if(_view==value)return;_view=value;PropertyChanged?.Invoke(this,new(nameof(View)));}}
    public ArcWpf.Controls.TabItem[] ManyTabs{get;}=Enumerable.Range(1,9).Select(i=>new ArcWpf.Controls.TabItem{Header=$"View {i}",Content=$"Content for view {i}"}).ToArray();
    public event PropertyChangedEventHandler? PropertyChanged;
}
