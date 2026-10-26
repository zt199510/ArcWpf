using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using Tab=ArcWpf.Controls.TabItem;
namespace ArcWpf.Tests;
[TestClass]
public sealed class TabsTests
{
    [TestMethod]
    public void NativeSelectionBindingAndContentIdentitySurviveRoundTrip()=>Sta.Run(()=>
    {
        var input=new TextBox{Text="original"};var model=new Model{Index=0};var tabs=Make(input);
        var binding=tabs.SetBinding(Selector.SelectedIndexProperty,new Binding(nameof(Model.Index)){Source=model});Layout(tabs);
        Assert.AreEqual(0,tabs.SelectedIndex);Assert.AreSame(input,tabs.SelectedContent);
        input.Text="edited";tabs.SetCurrentValue(Selector.SelectedIndexProperty,1);Layout(tabs);Assert.AreEqual(1,model.Index);
        model.Index=0;Layout(tabs);Assert.AreSame(input,tabs.SelectedContent);Assert.AreEqual("edited",input.Text);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(tabs,Selector.SelectedIndexProperty));
    });
    [TestMethod]
    public void HeaderGeometryMatchesSourceAndPanelGapIsSixteen()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);var shell=Part<Border>(tabs,"HeaderShell");var first=(Tab)tabs.Items[0];var second=(Tab)tabs.Items[1];
        Assert.AreEqual(46d,shell.ActualHeight,.01);Assert.AreEqual(36d,first.ActualHeight,.01);Assert.AreEqual(88d,first.ActualWidth,.01);
        Assert.AreEqual(4d,second.TranslatePoint(new Point(),first).X-first.ActualWidth,.01);
        Assert.AreEqual(5d,first.TranslatePoint(new Point(),shell).X,.01);
        var content=Part<ContentPresenter>(tabs,"PART_SelectedContentHost");Assert.AreEqual(16d,content.TranslatePoint(new Point(),tabs).Y-shell.ActualHeight,.01);
        Assert.AreEqual(new CornerRadius(18),shell.CornerRadius);Assert.AreEqual(new CornerRadius(15),Part<Border>(tabs,"PART_Selection").CornerRadius);
    });
    [TestMethod]
    public void IndicatorFollowsSelectionAndDoesNotLeakToAnotherTabs()=>Sta.Run(()=>
    {
        var a=Make();var b=Make();Layout(a);Layout(b);a.SelectedIndex=1;Layout(a);
        var indicator=Part<Border>(a,"PART_Selection");Assert.AreEqual(92d,((TranslateTransform)indicator.RenderTransform).X,.01);
        Assert.AreEqual(0d,((TranslateTransform)Part<Border>(b,"PART_Selection").RenderTransform).X,.01);
    });
    [TestMethod]
    public void NavigationWrapsSkipsDisabledAndRespectsRtl()=>Sta.Run(()=>
    {
        var tabs=Make();tabs.Items.Add(new Tab{Header="Last"});((Tab)tabs.Items[1]).IsEnabled=false;Layout(tabs);
        Assert.AreEqual(2,tabs.FindHeader(0,Key.Right));Assert.AreEqual(0,tabs.FindHeader(2,Key.Right));Assert.AreEqual(2,tabs.FindHeader(0,Key.End));
        tabs.FlowDirection=FlowDirection.RightToLeft;Layout(tabs);Assert.AreEqual(2,tabs.FindHeader(0,Key.Right));
        ((Tab)tabs.Items[2]).Visibility=Visibility.Collapsed;Layout(tabs);Assert.AreEqual(0,tabs.FindHeader(0,Key.End));
    });
    [TestMethod]
    public void OverflowRevealsSelectedLastHeaderAndClearsWhenItemsShrink()=>Sta.Run(()=>
    {
        var tabs=Make();for(int i=0;i<8;i++)tabs.Items.Add(new Tab{Header=$"Tab {i}"});Layout(tabs,300);
        Assert.IsTrue(tabs.HasOverflow);Assert.IsTrue(tabs.CanScrollForward);tabs.SelectedIndex=9;Layout(tabs,300);Layout(tabs,300);
        var scroll=Part<ScrollViewer>(tabs,"PART_HeaderScroll");Assert.IsGreaterThan(0d,scroll.HorizontalOffset);Assert.IsFalse(tabs.CanScrollForward);
        tabs.Items.Clear();tabs.Items.Add(new Tab{Header="Only"});Layout(tabs,300);Layout(tabs,300);Assert.IsFalse(tabs.HasOverflow);Assert.IsNull(scroll.OpacityMask);
    });
    [TestMethod]
    public void PanelClocksCompleteClearSnapshotAndRestoreAutoHeight()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);var frame=Part<FrameworkElement>(tabs,"PART_PanelFrame");var content=Part<ContentPresenter>(tabs,"PART_SelectedContentHost");var old=Part<Image>(tabs,"PART_Outgoing");
        tabs.BeginPanelTransition(20,120,Snapshot(),1,true);Assert.IsTrue(tabs.IsMotionRunning);Assert.IsNotNull(old.Source);
        for(int i=0;i<300;i++)tabs.AdvanceMotion(.016);Pump(380);
        Assert.IsTrue(double.IsNaN(frame.Height));Assert.AreEqual(1d,content.Opacity,.001);Assert.AreEqual(0d,old.Opacity,.001);Assert.IsNull(old.Source);Assert.IsFalse(content.HasAnimatedProperties);Assert.IsFalse(tabs.IsMotionRunning);
        tabs.SelectedIndex=1;Layout(tabs);Assert.AreEqual(1d,content.Opacity,.001);Assert.IsNull(old.Source);
    });
    [TestMethod]
    public void RapidRetargetOnlyKeepsLatestSnapshotAndReducedDoesNotSlide()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);ArcMotion.SetLevel(tabs,ArcMotionLevel.Reduced);Sta.Pump();var old=Part<Image>(tabs,"PART_Outgoing");
        tabs.BeginPanelTransition(20,100,Snapshot(),1,true);Pump(35);var latest=Snapshot();tabs.BeginPanelTransition(100,20,latest,-1,true);
        Assert.AreSame(latest,old.Source);Assert.IsTrue(double.IsNaN(Part<FrameworkElement>(tabs,"PART_PanelFrame").Height));
        Assert.AreEqual(0d,((TranslateTransform)Part<ContentPresenter>(tabs,"PART_SelectedContentHost").RenderTransform).X,.001);
        Pump(250);Assert.IsNull(old.Source);Assert.IsFalse(tabs.IsMotionRunning);
    });
    [TestMethod]
    public void OffUnloadAndTemplateReplacementCancelInternalMotion()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);tabs.BeginPanelTransition(20,100,Snapshot(),1,true);ArcMotion.SetLevel(tabs,ArcMotionLevel.Off);Assert.IsFalse(tabs.IsMotionRunning);
        Assert.IsTrue(double.IsNaN(Part<FrameworkElement>(tabs,"PART_PanelFrame").Height));
        ArcMotion.SetLevel(tabs,ArcMotionLevel.Full);Sta.Pump();tabs.BeginPanelTransition(20,100,Snapshot(),1,true);tabs.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(tabs.IsMotionRunning);
        tabs.BeginPanelTransition(20,100,Snapshot(),1,true);var old=Part<Image>(tabs,"PART_Outgoing");tabs.Template=new ControlTemplate(typeof(Tabs));tabs.ApplyTemplate();Assert.IsFalse(tabs.IsMotionRunning);Assert.IsNull(old.Source);Assert.IsFalse(old.HasAnimatedProperties);
    });
    [TestMethod]
    public void GeneratedContainersValueSelectionAndItemsRemovalUseNativeBehavior()=>Sta.Run(()=>
    {
        var tabs=new Tabs{ItemsSource=new[]{"One","Two"}};Layout(tabs);Assert.IsInstanceOfType<Tab>(tabs.ItemContainerGenerator.ContainerFromIndex(0));
        tabs.ItemsSource=null;tabs.Items.Add(new Tab{Header="First",Tag="a"});tabs.Items.Add(new Tab{Header="Second",Tag="b"});tabs.SelectedValuePath="Tag";tabs.SelectedValue="b";Layout(tabs);Assert.AreEqual(1,tabs.SelectedIndex);
        tabs.Items.RemoveAt(1);Layout(tabs);Assert.AreEqual(0,tabs.SelectedIndex);tabs.TabStripPlacement=Dock.Left;Assert.AreEqual(Dock.Top,tabs.TabStripPlacement);
    });
    [TestMethod]
    public void ThemeChangesAndPublicSizingTransformBindingsArePreserved()=>Sta.Run(()=>
    {
        var tabs=Make();var theme=new ArcThemeResources();tabs.Resources.MergedDictionaries.Add(theme);Layout(tabs);var shell=Part<Border>(tabs,"HeaderShell");var light=((SolidColorBrush)shell.Background).Color;
        theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(light,((SolidColorBrush)shell.Background).Color);
        var source=new Border{Height=200};var binding=tabs.SetBinding(FrameworkElement.HeightProperty,new Binding(nameof(Border.Height)){Source=source});var transform=new ScaleTransform(1.1,1.1);tabs.RenderTransform=transform;
        tabs.BeginPanelTransition(20,90,Snapshot(),1,true);ArcMotion.SetLevel(tabs,ArcMotionLevel.Off);Assert.AreSame(binding,BindingOperations.GetBindingExpression(tabs,FrameworkElement.HeightProperty));Assert.AreSame(transform,tabs.RenderTransform);Assert.AreEqual(200d,tabs.Height);
    });
    [TestMethod]
    public void AutomationRetainsTabSelectionAndSnapshotHasNoPeer()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);var peer=UIElementAutomationPeer.CreatePeerForElement(tabs)!;Assert.AreEqual(AutomationControlType.Tab,peer.GetAutomationControlType());Assert.IsNotNull(peer.GetPattern(PatternInterface.Selection));
        var itemPeer=peer.GetChildren().First(p=>p.GetAutomationControlType()==AutomationControlType.TabItem);Assert.IsNotNull(itemPeer.GetPattern(PatternInterface.SelectionItem));Assert.IsNull(UIElementAutomationPeer.CreatePeerForElement(new TabsSnapshot()));
    });
    [TestMethod]
    public void GalleryPageAndConsumerConstructWithoutOpeningWindows()=>Sta.Run(()=>
    {var page=new ArcWpf.Gallery.Pages.TabsPage();Layout(page,800);var consumer=new ArcWpf.Consumer.MainWindow();Assert.IsNotNull(consumer.Content);consumer.Close();});
    [TestMethod]
    public void IndicatorSpringRetargetsAndSettlesOnLatestHeader()=>Sta.Run(()=>
    {
        var tabs=Make();Layout(tabs);tabs.SelectedIndex=1;tabs.PlaceIndicator(true);
        tabs.AdvanceMotion(.016);double moving=((TranslateTransform)Part<Border>(tabs,"PART_Selection").RenderTransform).X;
        Assert.IsGreaterThan(0d,moving);Assert.IsLessThan(92d,moving);
        tabs.SelectedIndex=0;tabs.PlaceIndicator(true);for(int i=0;i<300;i++)tabs.AdvanceMotion(.016);Sta.Pump();
        Assert.AreEqual(0d,((TranslateTransform)Part<Border>(tabs,"PART_Selection").RenderTransform).X,.01);Assert.IsFalse(tabs.IsMotionRunning);
    });
    [TestMethod]
    public void RtlOverflowCanRevealLastItemWithoutExpandingControl()=>Sta.Run(()=>
    {
        var tabs=Make();tabs.FlowDirection=FlowDirection.RightToLeft;for(int i=0;i<7;i++)tabs.Items.Add(new Tab{Header=$"Tab {i}"});Layout(tabs,260);tabs.SelectedIndex=8;Layout(tabs,260);Layout(tabs,260);
        var scroll=Part<ScrollViewer>(tabs,"PART_HeaderScroll");Assert.IsTrue(tabs.HasOverflow);Assert.AreEqual(260d,tabs.ActualWidth,.01);Assert.IsGreaterThan(0d,scroll.HorizontalOffset);
        var last=(Tab)tabs.Items[8];double x=last.TranslatePoint(new Point(),scroll).X;Assert.IsTrue(x>=-1&&x+last.ActualWidth<=scroll.ActualWidth+1,$"Header outside viewport: {x}, viewport {scroll.ActualWidth}");
    });
    private static Tabs Make(object? content=null){var tabs=new Tabs();tabs.Items.Add(new Tab{Header="One",Content=content??new TextBlock{Text="Content one"}});tabs.Items.Add(new Tab{Header="Two",Content=new TextBlock{Text="Content two"}});return tabs;}
    private static BitmapSource Snapshot(){var bitmap=new RenderTargetBitmap(10,10,96,96,PixelFormats.Pbgra32);bitmap.Freeze();return bitmap;}
    private static void Layout(FrameworkElement element,double width=420){if(!element.IsInitialized){element.BeginInit();element.EndInit();}element.Measure(new Size(width,1000));element.Arrange(new Rect(0,0,width,element.DesiredSize.Height));element.UpdateLayout();Sta.Pump();}
    private static T Part<T>(Control control,string name)where T:FrameworkElement=>(T)control.Template.FindName(name,control);
    private static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private sealed class Model:INotifyPropertyChanged{private int _index;public int Index{get=>_index;set{_index=value;PropertyChanged?.Invoke(this,new(nameof(Index)));}}public event PropertyChangedEventHandler? PropertyChanged;}
}


