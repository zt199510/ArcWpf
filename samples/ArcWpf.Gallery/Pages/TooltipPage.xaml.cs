using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
namespace ArcWpf.Gallery.Pages;
public partial class TooltipPage:UserControl
{
    private readonly TooltipModel _model=new();private bool _long;
    public TooltipPage(){InitializeComponent();DataContext=_model;}
    private void ChangeTip(object sender,KeyEventArgs e){if(e.Key!=Key.F2)return;_long=!_long;_model.Status=_long?"All workspace changes have been saved and are ready for your team to review.":"Saved";e.Handled=true;}
}
public sealed class TooltipModel:INotifyPropertyChanged
{
    private string _status="Saved";
    public string Status{get=>_status;set{if(_status==value)return;_status=value;PropertyChanged?.Invoke(this,new(nameof(Status)));}}
    public event PropertyChangedEventHandler? PropertyChanged;
}
