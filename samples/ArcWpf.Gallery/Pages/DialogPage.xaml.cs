using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class DialogPage:UserControl
{
    private int _step;
    public DialogPage()=>InitializeComponent();
    private Dialog Get(string key)=>(Dialog)Resources[key];
    private void OpenPublish(object sender,RoutedEventArgs e)=>Get("PublishDialog").Show(this);
    private void OpenForm(object sender,RoutedEventArgs e)=>Get("FormDialog").Show(this);
    private void OpenSteps(object sender,RoutedEventArgs e)=>Get("StepsDialog").Show(this);
    private void OpenGuard(object sender,RoutedEventArgs e)=>Get("GuardDialog").Show(this);
    private void NextStep(object sender,RoutedEventArgs e)
    {
        var dialog=Get("StepsDialog");bool next=++_step%2!=0;
        dialog.Title=next?"Ready to publish?":"Review changes";dialog.Description=next?"Your team will see these changes immediately.":"Check the details before publishing.";
    }
    private void GuardClosing(object? sender,DialogClosingEventArgs e)
    {var content=(StackPanel)Get("GuardDialog").Content;e.Cancel=((ArcWpf.Controls.CheckBox)content.Children[0]).IsChecked==true;}
    private void OpenLong(object sender,RoutedEventArgs e)
    {
        var content=new StackPanel();for(int i=1;i<=24;i++)content.Children.Add(new TextBlock{Text=$"Section {i}: Review the workspace settings and confirm the details before continuing.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
        content.Children.Add(new ArcWpf.Controls.Button{Content="Done",Command=Dialog.CloseCommand,HorizontalAlignment=HorizontalAlignment.Right});
        new Dialog{Title="Workspace details",Description="Scroll to review all sections.",Content=content}.Show(this);
    }
}
