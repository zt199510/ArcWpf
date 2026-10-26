using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcSelect=ArcWpf.Controls.Select;
namespace ArcWpf.Tests;
[TestClass]
public sealed class SelectTests
{
    [TestMethod]
    public void NativeSelectedValueBindingsAndGeneratedItemsRemainIntact()=>Sta.Run(()=>
    {
        var model=new Model{Value="b"};var control=new ArcSelect{ItemsSource=new[]{new Option("a","Alpha"),new Option("b","Beta")},SelectedValuePath="Id",DisplayMemberPath="Name"};
        var binding=control.SetBinding(Selector.SelectedValueProperty,new Binding(nameof(Model.Value)){Source=model});Layout(control);
        Assert.AreEqual(1,control.SelectedIndex);Assert.AreEqual("Beta",control.Text);
        control.SetCurrentValue(Selector.SelectedIndexProperty,0);Sta.Pump();Assert.AreEqual("a",model.Value);Assert.AreEqual("Alpha",control.Text);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(control,Selector.SelectedValueProperty));
        Assert.IsFalse(control.IsEditable);control.IsEditable=true;Assert.IsFalse(control.IsEditable);
    });
    [TestMethod]
    public void ItemsChangesAndProgrammaticDisabledSelectionUseNativeSemantics()=>Sta.Run(()=>
    {
        var items=new ObservableCollection<string>{"one","two"};var control=new ArcSelect{ItemsSource=items,SelectedIndex=1};Layout(control);
        items.Remove("two");Sta.Pump();Assert.AreEqual(-1,control.SelectedIndex);
        control.ItemsSource=null;control.Items.Add(new SelectItem{Content="Disabled",IsEnabled=false});control.SelectedIndex=0;Sta.Pump();Assert.AreEqual(0,control.SelectedIndex);
    });
    [TestMethod]
    public void TriggerAndFieldLayoutUseSeparatePaddingAndGaps()=>Sta.Run(()=>
    {
        var control=new ArcSelect{Label="Team",Description="Choose the team that owns this work."};control.Items.Add("Design");control.SelectedIndex=0;Layout(control);
        var trigger=Part<Border>(control,"PART_Trigger");var label=Part<TextBlock>(control,"LabelText");var description=Part<TextBlock>(control,"DescriptionText");var text=Part<TextBlock>(control,"SelectedText");
        Assert.AreEqual(44d,trigger.ActualHeight,.01);
        Assert.AreEqual(8d,trigger.TranslatePoint(new Point(),control).Y-label.ActualHeight,.01);
        Assert.AreEqual(8d,description.TranslatePoint(new Point(),control).Y-trigger.TranslatePoint(new Point(0,44),control).Y,.01);
        Assert.AreEqual(13d,text.TranslatePoint(new Point(),trigger).X,.01);
        Assert.AreEqual(TextTrimming.CharacterEllipsis,text.TextTrimming);Assert.AreEqual("Design",text.Text);
    });
    [TestMethod]
    public void PlaceholderAndEmptyFieldHaveNoPhantomLabelSpacing()=>Sta.Run(()=>
    {
        var control=new ArcSelect{Placeholder="Pick one"};Layout(control,160);
        Assert.AreEqual(44d,control.DesiredSize.Height,.01);Assert.AreEqual(Visibility.Visible,Part<TextBlock>(control,"PlaceholderText").Visibility);
        Assert.AreEqual(Visibility.Collapsed,Part<TextBlock>(control,"SelectedText").Visibility);
        control.Items.Add("A very long selection that must fit inside a narrow field");control.SelectedIndex=0;Layout(control,160);
        Assert.AreEqual(Visibility.Collapsed,Part<TextBlock>(control,"PlaceholderText").Visibility);Assert.IsLessThanOrEqualTo(160d,control.DesiredSize.Width);
    });
    [TestMethod]
    public void TemplateUsesNativePopupAndBridgesContextWithoutOpeningWindow()=>Sta.Run(()=>
    {
        var control=new ArcSelect();Layout(control);var popup=Part<Popup>(control,"PART_Popup");var model=new object();control.DataContext=model;control.FlowDirection=FlowDirection.RightToLeft;ArcMotion.SetLevel(control,ArcMotionLevel.Reduced);Sta.Pump();
        var root=(FrameworkElement)popup.Child;Assert.AreSame(model,root.DataContext);Assert.AreEqual(FlowDirection.RightToLeft,root.FlowDirection);Assert.AreEqual(ArcMotionLevel.Reduced,ArcMotion.GetLevel(root));
        Assert.IsFalse(popup.IsOpen);Assert.IsFalse(control.IsPresented);Assert.AreEqual(PopupAnimation.None,popup.PopupAnimation);
        var surface=Part<Border>(control,"PART_MenuSurface");Assert.AreEqual(new CornerRadius(20),surface.CornerRadius);Assert.AreEqual(new Thickness(6),surface.Padding);
    });
    [TestMethod]
    public void SelectItemGeometryAndIndicatorFadeKeepCorrectEndState()=>Sta.Run(()=>
    {
        var item=new SelectItem{Content="Option"};Layout(item,220);var indicator=Part<FrameworkElement>(item,"PART_Indicator");
        Assert.AreEqual(36d,item.ActualHeight,.01);Assert.AreEqual(new Size(16,16),indicator.RenderSize);
        Assert.AreEqual(10d,item.ActualWidth-indicator.TranslatePoint(new Point(16,0),item).X,.01);
        item.IsSelected=true;indicator.Opacity=0;item.UpdateVisual(true);Pump(360);Assert.AreEqual(1d,indicator.Opacity,.001);Assert.IsFalse(indicator.HasAnimatedProperties);
        item.IsSelected=false;item.UpdateVisual(true);Pump(200);Assert.AreEqual(0d,indicator.Opacity,.001);
    });
    [TestMethod]
    public void ValueTransitionClearsOldSnapshotAndKeepsNewValueOpaque()=>Sta.Run(()=>
    {
        var control=new ArcSelect();control.Items.Add("one");control.Items.Add("two");control.SelectedIndex=0;Layout(control);
        control.SelectedIndex=1;Sta.Pump();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(ArcSelect).GetField("_lastText",flags)!.SetValue(control,"one");typeof(ArcSelect).GetField("_lastIndex",flags)!.SetValue(control,0);
        control.RefreshValue(true);Assert.IsTrue(control.IsMotionRunning);Pump(360);
        Assert.AreEqual("",Part<TextBlock>(control,"PART_OldValue").Text);Assert.AreEqual(0d,Part<TextBlock>(control,"PART_OldValue").Opacity,.001);Assert.AreEqual(1d,Part<FrameworkElement>(control,"PART_Value").Opacity,.001);Assert.IsFalse(control.IsMotionRunning);
        control.RefreshValue(false);Assert.AreEqual(1d,Part<FrameworkElement>(control,"PART_Value").Opacity,.001);
    });
    [TestMethod]
    public void OffAndTemplateReplacementClearActiveValueAnimation()=>Sta.Run(()=>
    {
        var control=new ArcSelect();control.Items.Add("one");control.SelectedIndex=0;Layout(control);
        var field=typeof(ArcSelect).GetField("_lastText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        field.SetValue(control,"old");control.RefreshValue(true);Assert.IsTrue(control.IsMotionRunning);
        ArcMotion.SetLevel(control,ArcMotionLevel.Off);Assert.IsFalse(control.IsMotionRunning);
        ArcMotion.SetLevel(control,ArcMotionLevel.Full);field.SetValue(control,"other");control.RefreshValue(true);var old=Part<TextBlock>(control,"PART_OldValue");
        control.Template=new ControlTemplate(typeof(ArcSelect));control.ApplyTemplate();Assert.IsFalse(control.IsMotionRunning);Assert.IsFalse(old.HasAnimatedProperties);
    });
    [TestMethod]
    public void ThemeAndPublicBackgroundBindingRemainAvailable()=>Sta.Run(()=>
    {
        var control=new ArcSelect();var theme=new ArcThemeResources();control.Resources.MergedDictionaries.Add(theme);Layout(control);
        var trigger=Part<Border>(control,"PART_Trigger");var light=((SolidColorBrush)trigger.Background).Color;theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(light,((SolidColorBrush)trigger.Background).Color);
        var source=new Border{Background=Brushes.Red};var binding=control.SetBinding(Control.BackgroundProperty,new Binding(nameof(Border.Background)){Source=source});Assert.AreEqual(Colors.Red,((SolidColorBrush)trigger.Background).Color);
        source.Background=Brushes.Blue;Sta.Pump();Assert.AreEqual(Colors.Blue,((SolidColorBrush)trigger.Background).Color);Assert.AreSame(binding,BindingOperations.GetBindingExpression(control,Control.BackgroundProperty));
    });
    [TestMethod]
    public void AutomationExposesComboBoxAndExplicitNamesTakePriority()=>Sta.Run(()=>
    {
        var control=new ArcSelect{Label="Team",Description="Hint"};var peer=UIElementAutomationPeer.CreatePeerForElement(control)!;
        Assert.AreEqual(AutomationControlType.ComboBox,peer.GetAutomationControlType());Assert.AreEqual("Team",peer.GetName());Assert.AreEqual("Hint",peer.GetHelpText());Assert.IsNotNull(peer.GetPattern(PatternInterface.ExpandCollapse));Assert.IsNotNull(peer.GetPattern(PatternInterface.Selection));
        AutomationProperties.SetName(control,"Explicit");Assert.AreEqual("Explicit",peer.GetName());Assert.IsNull(UIElementAutomationPeer.CreatePeerForElement(new SelectSnapshot()));
    });
    [TestMethod]
    public void RetargetedMenuMotionDoesNotRunStaleCloseCompletion()=>Sta.Run(()=>
    {
        var surface=new Border{Opacity=1};var motion=new OwnedMotion();int closed=0;
        motion.Double(surface,UIElement.OpacityProperty,0,.12,true,completed:()=>closed++);
        Pump(40);
        motion.Double(surface,UIElement.OpacityProperty,1,.24,true,enter:true);
        Pump(340);
        Assert.AreEqual(0,closed);Assert.AreEqual(1d,surface.Opacity,.001);Assert.IsFalse(motion.IsRunning);
        motion.Double(surface,UIElement.OpacityProperty,0,.12,true,completed:()=>closed++);Pump(220);
        Assert.AreEqual(1,closed);Assert.AreEqual(0d,surface.Opacity,.001);
    });
    [TestMethod]
    public void PopupGeometryUsesFourDipGapAndTwelveDipCollisionInset()
    {
        var result=PopoverPositioner.Calculate(new Rect(100,100,240,44),new Size(240,180),new Rect(0,0,800,600),PopoverSide.Bottom,PopoverAlignment.Start,4,12,false);
        Assert.AreEqual(new Rect(100,148,240,180),result.Bounds);
        var edge=PopoverPositioner.Calculate(new Rect(560,550,240,44),new Size(240,180),new Rect(0,0,800,600),PopoverSide.Bottom,PopoverAlignment.Start,4,12,false);
        Assert.AreEqual(PopoverSide.Top,edge.Side);Assert.IsTrue(new Rect(12,12,776,576).Contains(edge.Bounds));
    }
    private static void Layout(FrameworkElement element,double width=380){if(!element.IsInitialized){element.BeginInit();element.EndInit();}element.Measure(new Size(width,1000));element.Arrange(new Rect(0,0,width,element.DesiredSize.Height));element.UpdateLayout();Sta.Pump();}
    private static T Part<T>(Control control,string name)where T:FrameworkElement=>(T)control.Template.FindName(name,control);
    private static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private sealed record Option(string Id,string Name);
    private sealed class Model:INotifyPropertyChanged{private string _value="";public string Value{get=>_value;set{_value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}public event PropertyChangedEventHandler? PropertyChanged;}
}
