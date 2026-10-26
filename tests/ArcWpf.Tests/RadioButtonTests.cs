using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
using Radio=ArcWpf.Controls.RadioButton;
namespace ArcWpf.Tests;
[TestClass]
public sealed class RadioButtonTests
{
    [TestMethod]
    public void NativeGroupingRepeatClickAndBindingSurvive() => Sta.Run(()=>
    {
        var a=new Probe { GroupName="test-native" };var b=new Probe { GroupName="test-native" };var parent=new StackPanel();parent.Children.Add(a);parent.Children.Add(b);
        var model=new Model();var binding=a.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(Model.Checked)){Source=model});
        a.ClickNow();a.ClickNow();Assert.IsTrue(a.IsChecked);Assert.IsTrue(model.Checked);
        b.ClickNow();Assert.IsTrue(b.IsChecked);Assert.IsFalse(a.IsChecked);Assert.IsFalse(model.Checked);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(a,ToggleButton.IsCheckedProperty));
        a.IsChecked=null;a.IsThreeState=true;Assert.IsFalse(a.IsChecked);Assert.IsFalse(a.IsThreeState);
    });
    [TestMethod]
    public void GroupSelectionUpdatesModelAndKeepsIndependentGroupsIsolated() => Sta.Run(()=>
    {
        var first=Group();var second=Group();var model=new Model{Value="a"};int changes=0;
        var binding=first.SetBinding(RadioGroup.SelectedValueProperty,new Binding(nameof(Model.Value)){Source=model});
        first.SelectedValueChanged+=(_,_)=>changes++;
        var a=first.OrderedRows()[0];var b=first.OrderedRows()[1];
        Assert.IsTrue(a.IsChecked);b.SetCurrentValue(ToggleButton.IsCheckedProperty,true);
        Assert.AreEqual("b",model.Value);Assert.IsFalse(a.IsChecked);Assert.AreEqual(1,changes);
        Assert.IsTrue(second.OrderedRows()[0].IsChecked);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(first,RadioGroup.SelectedValueProperty));
    });
    [TestMethod]
    public void MissingValueAppearsLaterAndRemovalHidesHighlight() => Sta.Run(()=>
    {
        var items=new ObservableCollection<string>{"first"};var group=new RadioGroup{ItemsSource=items,SelectedValue="later"};Layout(group);
        Assert.IsFalse(group.OrderedRows()[0].IsChecked);
        items.Add("later");Layout(group);Assert.IsTrue(group.OrderedRows()[1].IsChecked);
        items.Remove("later");Layout(group);Assert.AreEqual("later",group.SelectedValue);Assert.IsFalse(group.OrderedRows()[0].IsChecked);
        Assert.AreEqual(0d,Part<Border>(group,"PART_Highlight").Opacity);
    });
    [TestMethod]
    public void RemovingContainerRestoresNativeGroupAndTabSettings() => Sta.Run(()=>
    {
        var row=new Radio{Content="Reusable",Value="row",GroupName="original",IsTabStop=true};
        var group=new RadioGroup();group.Items.Add(row);Layout(group);
        Assert.AreNotEqual("original",row.GroupName);
        group.Items.Remove(row);Layout(group);
        Assert.AreEqual("original",row.GroupName);Assert.IsTrue(row.IsTabStop);Assert.IsNull(row.GroupOwner);
        var second=new RadioGroup{SelectedValue="row"};second.Items.Add(row);Layout(second);Assert.IsTrue(row.IsChecked);
    });
    [TestMethod]
    public void GeneratedContainerValueBindingAndDuplicateValuesSelectFirst() => Sta.Run(()=>
    {
        var model=new Model{Value="bound"};var group=new RadioGroup{SelectedValue="bound"};
        var style=new Style(typeof(Radio));style.Setters.Add(new Setter(Radio.ValueProperty,new Binding(nameof(Model.Value))));
        group.ItemContainerStyle=style;group.Items.Add(model);Layout(group);Assert.IsTrue(group.OrderedRows()[0].IsChecked);
        model.Value="changed";Sta.Pump();Assert.IsFalse(group.OrderedRows()[0].IsChecked);group.SelectedValue="changed";Assert.IsTrue(group.OrderedRows()[0].IsChecked);
        group.Items.Add(new Radio{Content="Duplicate",Value="changed"});Layout(group);
        Assert.AreEqual(1,group.OrderedRows().Count(row=>row.IsChecked==true));Assert.IsTrue(group.OrderedRows()[0].IsChecked);
    });
    [TestMethod]
    public void DirectionNavigationWrapsSkipsUnavailableAndTabHasOneEntry() => Sta.Run(()=>
    {
        var group=Group();var rows=group.OrderedRows();
        Assert.AreSame(rows[1],group.FindNext(rows[0],true));Assert.AreSame(rows[0],group.FindNext(rows[1],true));
        Assert.AreEqual(1,rows.Count(KeyboardNavigation.GetIsTabStop));
        rows[1].IsEnabled=false;Assert.AreSame(rows[0],group.FindNext(rows[0],true));
        rows[1].IsEnabled=true;rows[0].Visibility=Visibility.Collapsed;Assert.AreSame(rows[1],group.FindNext(rows[1],false));
    });
    [TestMethod]
    public void RowMarkDotAndTextHaveMeasuredGeometry() => Sta.Run(()=>
    {
        var row=new Radio{Content="Focused review",Description="One reviewer at a time.",IsChecked=true};Layout(row);
        var mark=Part<Border>(row,"PART_Mark");var dot=Part<Border>(row,"PART_Dot");var label=Part<ContentPresenter>(row,"PART_Label");var desc=Part<TextBlock>(row,"DescriptionText");
        Assert.AreEqual(new Size(17,17),mark.RenderSize);Assert.AreEqual(new Size(7,7),dot.RenderSize);
        Assert.AreEqual(new Point(8.5,8.5),dot.TranslatePoint(new Point(3.5,3.5),mark));
        Assert.AreEqual(row.ActualHeight/2,mark.TranslatePoint(new Point(8.5,8.5),row).Y,.01);
        Assert.AreEqual(12d,label.TranslatePoint(new Point(),row).X-mark.TranslatePoint(new Point(17,0),row).X,.01);
        Assert.AreEqual(label.TranslatePoint(new Point(),row).X,desc.TranslatePoint(new Point(),row).X,.01);
    });
    [TestMethod]
    public void SharedHighlightTracksSelectionAndResizedRowWithoutChangingRowLayout() => Sta.Run(()=>
    {
        var group=Group();var rows=group.OrderedRows();group.SelectedValue="b";Layout(group);
        var highlight=Part<Border>(group,"PART_Highlight");var host=Part<FrameworkElement>(group,"PART_ItemsHost");
        Assert.AreEqual(8d,rows[1].TranslatePoint(new Point(),host).Y-rows[0].TranslatePoint(new Point(0,rows[0].ActualHeight),host).Y,.01);
        Assert.AreEqual(rows[0].ActualHeight+8+rows[1].ActualHeight,host.ActualHeight,.01,"No extra gap after the last row.");
        Assert.AreEqual(rows[1].ActualHeight,highlight.Height,.01);
        Assert.AreEqual(rows[1].TranslatePoint(new Point(),host).Y,((TranslateTransform)highlight.RenderTransform).Y,.01);
        rows[1].Description="A much longer description which must wrap when the column becomes narrow and should change the selected row height.";
        Layout(group,180);group.Place(false);
        Assert.AreEqual(rows[1].ActualHeight,highlight.Height,.01);Assert.IsFalse(group.IsMotionRunning);
        group.SelectedValue=null;Assert.AreEqual(0d,highlight.Opacity);
    });
    [TestMethod]
    public void AutomationUsesSelectionItemAndExposesDescription() => Sta.Run(()=>
    {
        var row=new Radio {Content="Choice",Description="Help"};var peer=UIElementAutomationPeer.CreatePeerForElement(row)!;
        Assert.AreEqual(AutomationControlType.RadioButton,peer.GetAutomationControlType());Assert.AreEqual("Choice",peer.GetName());Assert.AreEqual("Help",peer.GetHelpText());
        var selection=(ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)!;
        selection.Select();Assert.IsTrue(selection.IsSelected);
        AutomationProperties.SetHelpText(row,"Explicit");Assert.AreEqual("Explicit",peer.GetHelpText());
        row.IsEnabled=false;Assert.ThrowsExactly<ElementNotEnabledException>(()=>selection.Select());
        var group=Group();group.Header="Group title";Assert.AreEqual("Group title",UIElementAutomationPeer.CreatePeerForElement(group)!.GetName());
    });
    [TestMethod]
    public void DisabledCommandCannotExecuteOrSelect() => Sta.Run(()=>
    {
        var command=new TestCommand();var row=new Probe{Command=command,CommandParameter="parameter"};row.ClickNow();Assert.AreEqual("parameter",command.Last);
        row.IsChecked=false;command.Enabled=false;command.Notify();row.ClickNow();Assert.IsFalse(row.IsChecked);Assert.AreEqual(1,command.Count);
    });
    [TestMethod]
    public void DotFadeCompletesWithoutFlashAndLaterUpdatesStayTransparent() => Sta.Run(()=>
    {
        var row=new Radio{Content="Fade"};Layout(row);var dot=Part<Border>(row,"PART_Dot");dot.Opacity=1;
        row.UpdateVisual(true);Pump(250);Assert.AreEqual(0d,dot.Opacity,.001);Assert.IsFalse(dot.HasAnimatedProperties);
        row.UpdateVisual(true);Pump(200);Assert.AreEqual(0d,dot.Opacity,.001);
    });
    [TestMethod]
    public void OwnedMotionReversalAndCancellationKeepDurableBase()
        =>Sta.Run(()=>
    {
        var target=new Border{Opacity=0};var motion=new OwnedMotion();
        motion.Double(target,UIElement.OpacityProperty,1,.16,true);Pump(50);double midway=target.Opacity;
        Assert.IsGreaterThan(0d,midway);Assert.IsLessThan(1d,midway);
        motion.Double(target,UIElement.OpacityProperty,0,.12,true);Pump(240);
        Assert.AreEqual(0d,target.Opacity,.001);Assert.IsFalse(motion.IsRunning);
        motion.Double(target,UIElement.OpacityProperty,1,.16,true);motion.Stop();Assert.AreEqual(1d,target.Opacity);Assert.IsFalse(target.HasAnimatedProperties);
    });
    [TestMethod]
    public void UnloadOffAndTemplateReplacementRemoveActiveAnimations() => Sta.Run(()=>
    {
        var row=new Radio{Content="Cleanup"};Layout(row);var dot=Part<Border>(row,"PART_Dot");dot.Opacity=1;row.UpdateVisual(true);Assert.IsTrue(row.IsMotionRunning);
        row.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(row.IsMotionRunning);Assert.AreEqual(0d,dot.Opacity);
        dot.Opacity=1;row.UpdateVisual(true);ArcMotion.SetLevel(row,ArcMotionLevel.Off);Assert.IsFalse(row.IsMotionRunning);
        ArcMotion.SetLevel(row,ArcMotionLevel.Full);dot.Opacity=1;row.UpdateVisual(true);row.Template=new ControlTemplate(typeof(Radio));row.ApplyTemplate();Assert.IsFalse(row.IsMotionRunning);Assert.IsFalse(dot.HasAnimatedProperties);
        row.IsChecked=true;
    });
    [TestMethod]
    public void ThemeRtlAndDescriptionBindingsRemainDynamic() => Sta.Run(()=>
    {
        var row=new Radio{Content="Theme",IsChecked=true,FlowDirection=FlowDirection.RightToLeft};var theme=new ArcThemeResources();row.Resources.MergedDictionaries.Add(theme);Layout(row);
        var mark=Part<Border>(row,"PART_Mark");var color=((SolidColorBrush)mark.Background).Color;
        theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(color,((SolidColorBrush)mark.Background).Color);
        var label=Part<ContentPresenter>(row,"PART_Label");Assert.AreEqual(FlowDirection.RightToLeft,label.FlowDirection);
        var source=new TextBlock{Text="description"};row.SetBinding(Radio.DescriptionProperty,new Binding(nameof(TextBlock.Text)){Source=source});source.Text="updated";Sta.Pump();Assert.AreEqual("updated",Part<TextBlock>(row,"DescriptionText").Text);
    });
    private static RadioGroup Group()
    {var group=new RadioGroup{Header="Group",SelectedValue="a"};group.Items.Add(new Radio{Content="A",Description="first",Value="a"});group.Items.Add(new Radio{Content="B",Description="second",Value="b"});Layout(group);return group;}
    private static void Layout(FrameworkElement element,double width=380)
    {if(!element.IsInitialized){element.BeginInit();element.EndInit();}element.Measure(new Size(width,1000));element.Arrange(new Rect(0,0,width,element.DesiredSize.Height));element.UpdateLayout();Sta.Pump();}
    private static T Part<T>(Control control,string name) where T:FrameworkElement =>(T)control.Template.FindName(name,control);
    private static void Pump(int milliseconds)
    {var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private sealed class Probe:Radio{internal void ClickNow()=>OnClick();}
    private sealed class Model:INotifyPropertyChanged
    {private bool? _checked=false;private object? _value;public bool? Checked{get=>_checked;set{_checked=value;PropertyChanged?.Invoke(this,new(nameof(Checked)));}}public object? Value{get=>_value;set{_value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}public event PropertyChangedEventHandler? PropertyChanged;}
    private sealed class TestCommand:ICommand
    {public bool Enabled=true;public object? Last;public int Count;public bool CanExecute(object? p)=>Enabled;public void Execute(object? p){Last=p;Count++;}public event EventHandler? CanExecuteChanged;public void Notify()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);}
}
