using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class MultiSelectPage : UserControl
{
    public MultiSelectPage()
    {
        InitializeComponent();DataContext=new MultiSelectModel();
        Reference.Options=[new("bug","Bug"),new("feature","Feature"),new("docs","Docs")];Reference.SelectedValues=new[]{"bug"};
        var options=new[]{new MultiSelectOption("bug","Bug"),new("feature","Feature"),new("docs","Docs"),new("long","这是一个用于检验截断与操作位置的非常长的标签"),new("disabled","不可选项",false)}.Concat(Enumerable.Range(1,24).Select(i=>new MultiSelectOption("tag"+i,"Tag "+i))).ToArray();
        foreach(var picker in new[]{Picker,Narrow,ReadOnlyPicker,DisabledPicker,InvalidPicker})picker.Options=options;
        Narrow.SelectedValues=new[]{"long","bug","docs","feature"};ReadOnlyPicker.SelectedValues=new[]{"docs"};DisabledPicker.SelectedValues=new[]{"bug"};
        InvalidPicker.SetBinding(MultiSelect.SelectedValuesProperty,new Binding(nameof(MultiSelectModel.Values)){Source=new MultiSelectModel(),Mode=BindingMode.TwoWay});
        Validation.MarkInvalid(InvalidPicker.GetBindingExpression(MultiSelect.SelectedValuesProperty),new ValidationError(new ExceptionValidationRule(),InvalidPicker.GetBindingExpression(MultiSelect.SelectedValuesProperty),"请选择至少一个项目标签",null));
    }
    private void ReplaceValues(object sender,RoutedEventArgs e)=>((MultiSelectModel)DataContext).Values=new[]{"feature","docs","long"};
    private void ClearValues(object sender,RoutedEventArgs e)=>Picker.ClearSelection();
}
public sealed class MultiSelectModel : INotifyPropertyChanged
{
    private IList<string> _values=new[]{"bug"};
    public IList<string> Values {get=>_values;set{_values=value;PropertyChanged?.Invoke(this,new(nameof(Values)));PropertyChanged?.Invoke(this,new(nameof(Summary)));}}
    public string Summary=>"SelectedValues: "+string.Join(", ",Values);
    public event PropertyChangedEventHandler? PropertyChanged;
}
