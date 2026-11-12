using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class TabsPage:UserControl
{
    private readonly TabsModel _model=new();
    public TabsPage(){InitializeComponent();DataContext=_model;}
    private void OpenEditor(object sender,RoutedEventArgs e)=>_model.View="editor";
    private int _created;
    private void AddDocument(object sender,RoutedEventArgs e)
    {
        var document=new TabDocument($"untitled-{++_created}.cs","新建的空白文档。",true);_model.Documents.Add(document);Documents.SelectedItem=document;
        _model.DocumentStatus=$"已新建 {document.Name}";
    }
    private void OnTabClosing(object sender,ArcWpf.Controls.TabClosingEventArgs e)
    {
        // The first attempt on a document with changes is turned down, the way an unsaved-changes prompt would.
        if(e.Item is not TabDocument{HasChanges:true} document)return;
        document.HasChanges=false;e.Cancel=true;_model.DocumentStatus=$"{document.Name} 有未保存的修改，已拦截这次关闭；再关一次即可关闭。";
    }
    private void OnTabClosed(object sender,ArcWpf.Controls.TabEventArgs e)=>_model.DocumentStatus=$"已关闭 {((TabDocument)e.Item).Name}（原位置 {e.Index+1}），剩余 {_model.Documents.Count} 个";
    private void OnTabMoved(object sender,ArcWpf.Controls.TabMovedEventArgs e)=>_model.DocumentStatus=$"{((TabDocument)e.Item).Name} 从位置 {e.OldIndex+1} 移到 {e.NewIndex+1}";
}
public sealed class TabsModel:INotifyPropertyChanged
{
    private string _view="summary";
    public string View{get=>_view;set{if(_view==value)return;_view=value;PropertyChanged?.Invoke(this,new(nameof(View)));}}
    public ArcWpf.Controls.TabItem[] ManyTabs{get;}=Enumerable.Range(1,9).Select(i=>new ArcWpf.Controls.TabItem{Header=$"View {i}",Content=$"Content for view {i}"}).ToArray();
    private string _documentStatus="拖动标签或按 Ctrl+Shift+←/→ 调整顺序；点 ×、鼠标中键或 Ctrl+F4 关闭。README.md 不可关闭。";
    public string DocumentStatus{get=>_documentStatus;set{_documentStatus=value;PropertyChanged?.Invoke(this,new(nameof(DocumentStatus)));}}
    public System.Collections.ObjectModel.ObservableCollection<TabDocument> Documents{get;}=new()
    {
        new("README.md","固定标签：没有关闭按钮，但可以拖动。",false),new("Tabs.cs","切换、关闭和重排都不会重建其他标签的内容。",true),
        new("notes.md","这个文档有未保存的修改，第一次关闭会被 TabClosing 拦截。",true){HasChanges=true},new("TreeView.cs","把它拖到最前面试试。",true),
    };
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed record TabDocument(string Name,string Summary,bool CanClose){public bool HasChanges{get;set;}}
