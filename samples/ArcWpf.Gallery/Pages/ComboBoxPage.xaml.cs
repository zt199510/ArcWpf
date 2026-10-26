using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ComboBoxPage : UserControl
{
    public ComboBoxPage()
    {
        InitializeComponent();DataContext=new ComboBoxModel();
        ComboBoxOption[] options=[new("utc","UTC"),new("cet","Central European",Keywords:["zurich","berlin"]),new("pst","Pacific",Keywords:["san francisco"])];
        Reference.Options=options;
        var expanded=options.Concat(new ComboBoxOption[]{new("asia","中国标准时间",Keywords:["北京","上海","beijing"]),new("disabled","不可选时区",false),new("long","A very long time zone label for testing native text viewport clipping")}).Concat(Enumerable.Range(1,24).Select(i=>new ComboBoxOption("zone"+i,"Example zone "+i))).ToArray();
        foreach(var picker in new[]{Picker,Narrow,ReadOnlyPicker,DisabledPicker,InvalidPicker})picker.Options=expanded;
        Narrow.SelectedValue="long";ReadOnlyPicker.SelectedValue="cet";DisabledPicker.SelectedValue="utc";
    }
    private void SelectPacific(object sender,RoutedEventArgs e)=>((ComboBoxModel)DataContext).Zone="pst";
    private void ClearZone(object sender,RoutedEventArgs e)=>Picker.ClearSelection();
}
public sealed class ComboBoxModel : INotifyPropertyChanged
{
    private string _zone="";
    public string Zone {get=>_zone;set{_zone=value;PropertyChanged?.Invoke(this,new(nameof(Zone)));PropertyChanged?.Invoke(this,new(nameof(Summary)));}}
    public string Summary=>"SelectedValue: "+(Zone.Length==0?"(empty)":Zone);
    public event PropertyChangedEventHandler? PropertyChanged;
}
