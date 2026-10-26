using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class RadioButtonPage:UserControl
{
    private readonly RadioModel _model=new();
    public RadioButtonPage(){InitializeComponent();DataContext=_model;}
    private void SelectOpen(object sender,RoutedEventArgs e)=>_model.ReviewMode="open";
    private void AddOption(object sender,RoutedEventArgs e)=>_model.Options.Add($"Option {_model.Options.Count+1}");
}
public sealed class RadioModel:INotifyPropertyChanged
{
    private string _reviewMode="focused",_independent="enabled",_dataChoice="Option 1",_status="";
    public string ReviewMode {get=>_reviewMode;set{if(Set(ref _reviewMode,value))Status=$"Selected: {value}";}}
    public string Independent {get=>_independent;set=>Set(ref _independent,value);}
    public string DataChoice {get=>_dataChoice;set=>Set(ref _dataChoice,value);}
    public string Status {get=>_status;private set=>Set(ref _status,value);}
    public ObservableCollection<string> Options {get;}=["Option 1","Option 2"];
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field,T value,[CallerMemberName]string name=""){if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));return true;}
}
