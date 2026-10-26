using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]
public sealed class TooltipTests
{
    [TestMethod]
    public void WarmWindowCountsOpenTooltipsAndExpiresAt300Milliseconds()
    {
        var warm=new TooltipWarmth();Assert.IsFalse(warm.IsWarm(0));warm.Opened();warm.Opened();warm.Closed(10);Assert.IsTrue(warm.IsWarm(15));warm.Closed(20);Assert.IsTrue(warm.IsWarm(20.299));Assert.IsFalse(warm.IsWarm(20.301));
    }
    [TestMethod]
    public void TriggerLayoutAndBindingsAreNotChanged()=>Sta.Run(()=>
    {
        var button=new ArcWpf.Controls.Button{Content="Trigger",Width=110};var scale=new ScaleTransform(1.1,1.1);button.RenderTransform=scale;var tooltip=new Tooltip{Content=button,Tip="Help"};Layout(tooltip,110,44);
        Assert.AreEqual(110d,button.ActualWidth,.01);Assert.AreEqual(44d,button.ActualHeight,.01);Assert.AreSame(scale,button.RenderTransform);Assert.IsFalse(tooltip.Focusable);Assert.IsFalse(tooltip.IsTabStop);
        Assert.AreSame(button,tooltip.Content);tooltip.CloseImmediately();
    });
    [TestMethod]
    public void DefaultHelpTextTracksTipAndExplicitHelpIsPreserved()=>Sta.Run(()=>
    {
        var button=new System.Windows.Controls.Button{Content="Icon"};var tip=new Tooltip{Content=button,Tip="First"};Layout(tip);Assert.AreEqual("First",AutomationProperties.GetHelpText(button));tip.Tip="Second";Sta.Pump();Assert.AreEqual("Second",AutomationProperties.GetHelpText(button));
        tip.Content=new Border();Assert.AreEqual("",AutomationProperties.GetHelpText(button));AutomationProperties.SetHelpText(button,"Explicit");tip.Content=button;tip.Tip="Third";Sta.Pump();Assert.AreEqual("Explicit",AutomationProperties.GetHelpText(button));tip.CloseImmediately();
    });
    [TestMethod]
    public void BubbleGeometryAndLongTextUse240Width18RadiusAndPadding()=>Sta.Run(()=>
    {
        var tip=Make("A long explanatory tooltip message that wraps across several lines inside the maximum bubble width.");var surface=Surface(tip);MeasureBubble(surface);
        Assert.AreEqual(240d,surface.ActualWidth,.01);var bubble=Part<Border>(surface,"Bubble");Assert.AreEqual(new CornerRadius(18),bubble.CornerRadius);Assert.AreEqual(new Thickness(16,12,16,12),bubble.Padding);
        var text=Part<TooltipText>(tip,"PART_Text");Assert.AreEqual(17d,text.TranslatePoint(new Point(),surface).X,.01);Assert.AreEqual(13d,text.TranslatePoint(new Point(),surface).Y,.01);Assert.IsGreaterThan(45.6,surface.ActualHeight);tip.CloseImmediately();
    });
    [TestMethod]
    public void PopupIsNonCapturingAndBridgesContextMotionAndFlow()=>Sta.Run(()=>
    {
        var tip=Make("Help");var context=new object();tip.DataContext=context;tip.FlowDirection=FlowDirection.RightToLeft;ArcMotion.SetLevel(tip,ArcMotionLevel.Reduced);Sta.Pump();var popup=Part<Popup>(tip,"PART_Popup");var frame=(FrameworkElement)popup.Child;
        Assert.AreSame(context,frame.DataContext);Assert.AreEqual(FlowDirection.RightToLeft,frame.FlowDirection);Assert.AreEqual(ArcMotionLevel.Reduced,ArcMotion.GetLevel(frame));Assert.IsTrue(popup.StaysOpen);Assert.AreEqual(PopupAnimation.None,popup.PopupAnimation);Assert.IsFalse(popup.IsOpen);tip.CloseImmediately();
    });
    [TestMethod]
    public void EntranceExitAndRepeatedCloseRemoveRealClocks()=>Sta.Run(()=>
    {
        var tip=Make("Help");tip.BeginPresentation(false,false);Assert.IsTrue(tip.IsOpen);Assert.IsTrue(tip.IsPresented);Pump(270);Assert.AreEqual(1d,Surface(tip).Opacity,.001);Assert.IsFalse(tip.IsMotionRunning);
        tip.Close();tip.Close();Assert.IsFalse(tip.IsOpen);Assert.IsTrue(tip.IsPresented);Pump(230);Assert.IsFalse(tip.IsPresented);Assert.IsFalse(tip.IsMotionRunning);Assert.AreEqual(0d,Surface(tip).Opacity,.001);Assert.IsFalse(Surface(tip).HasAnimatedProperties);
        tip.BeginPresentation(true,false);Pump(230);Assert.AreEqual(1d,Surface(tip).Opacity,.001);tip.CloseImmediately();
    });
    [TestMethod]
    public void RetargetingExitKeepsTooltipOpenAndWarmEntranceHasNoTravel()=>Sta.Run(()=>
    {
        var tip=Make("Help");tip.BeginPresentation(false,false);Pump(60);tip.Close();Pump(30);tip.BeginPresentation(true,false);Pump(260);
        Assert.IsTrue(tip.IsOpen);Assert.IsTrue(tip.IsPresented);var transform=(TransformGroup)Surface(tip).RenderTransform;Assert.AreEqual(1d,((ScaleTransform)transform.Children[0]).ScaleX,.001);Assert.AreEqual(0d,((TranslateTransform)transform.Children[1]).Y,.001);tip.CloseImmediately();
    });
    [TestMethod]
    public void ReducedAndOffDropTransformsAndCleanUpPendingExit()=>Sta.Run(()=>
    {
        var tip=Make("Help");ArcMotion.SetLevel(tip,ArcMotionLevel.Reduced);tip.BeginPresentation(false,false);var transforms=(TransformGroup)Surface(tip).RenderTransform;Assert.AreEqual(1d,((ScaleTransform)transforms.Children[0]).ScaleX,.001);Assert.AreEqual(0d,((TranslateTransform)transforms.Children[1]).Y,.001);
        Pump(230);tip.Close();ArcMotion.SetLevel(tip,ArcMotionLevel.Off);Assert.IsFalse(tip.IsPresented);Assert.IsFalse(tip.IsMotionRunning);
    });
    [TestMethod]
    public void TextResizeCompletesRestoresAutoAndRemovesPreviousText()=>Sta.Run(()=>
    {
        var tip=Make("Saved");var text=Part<TooltipText>(tip,"PART_Text");MeasureBubble(Surface(tip));text.SetText("All workspace changes have been saved and are ready for review.",true);Assert.IsTrue(text.IsMotionRunning);Pump(560);
        var slot=Part<FrameworkElement>(text,"PART_Size");Assert.IsTrue(double.IsNaN(slot.Width));Assert.IsTrue(double.IsNaN(slot.Height));Assert.AreEqual("",Part<TextBlock>(text,"PART_Old").Text);Assert.AreEqual(1d,Part<TextBlock>(text,"PART_Current").Opacity,.001);Assert.IsFalse(text.IsMotionRunning);
        text.SetText("Saved",true);Pump(50);text.SetText("Latest",true);Pump(560);Assert.AreEqual("Latest",Part<TextBlock>(text,"PART_Current").Text);Assert.AreEqual("",Part<TextBlock>(text,"PART_Old").Text);tip.CloseImmediately();
    });
    [TestMethod]
    public void TemplateReplacementAndUnloadStopAllTimersAndMotion()=>Sta.Run(()=>
    {
        var tip=Make("Help");tip.BeginPresentation(false,false);tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(tip.IsOpen);Assert.IsFalse(tip.IsPresented);Assert.IsFalse(tip.HasPendingDelay);Assert.IsFalse(tip.IsMotionRunning);
        tip.BeginPresentation(false,false);var surface=Surface(tip);tip.Template=new ControlTemplate(typeof(Tooltip));tip.ApplyTemplate();Assert.IsFalse(tip.IsPresented);Assert.IsFalse(surface.HasAnimatedProperties);
    });
    [TestMethod]
    public void EmptyContentClosesAndCustomTemplateDoesNotUseTextAnimation()=>Sta.Run(()=>
    {
        var tip=Make("Help");tip.BeginPresentation(true,false);tip.Tip="";Pump(230);Assert.IsFalse(tip.IsPresented);
        var custom=new TextBlock{Text="Custom"};tip.Tip=custom;Assert.AreEqual(Visibility.Collapsed,Part<TooltipText>(tip,"PART_Text").Visibility);Assert.AreSame(custom,Part<ContentPresenter>(tip,"PART_Custom").Content);tip.CloseImmediately();
    });
    [TestMethod]
    public void SurfaceAutomationRoleAndDynamicThemeAreAvailable()=>Sta.Run(()=>
    {
        var tip=Make("Helpful text");var surface=Surface(tip);var peer=UIElementAutomationPeer.CreatePeerForElement(surface)!;Assert.AreEqual(AutomationControlType.ToolTip,peer.GetAutomationControlType());Assert.AreEqual("Helpful text",peer.GetName());
        var theme=new ArcThemeResources();tip.Resources.MergedDictionaries.Add(theme);MeasureBubble(surface);var before=((SolidColorBrush)surface.Background).Color;theme.Mode=ArcThemeMode.Dark;Sta.Pump();Assert.AreNotEqual(before,((SolidColorBrush)surface.Background).Color);tip.CloseImmediately();
    });
    [TestMethod]
    public void TooltipPlacementFlipsAndHonorsEightGapTwelveInset()
    {
        var result=PopoverPositioner.Calculate(new Rect(100,100,32,32),new Size(120,46),new Rect(0,0,800,600),PopoverSide.Top,PopoverAlignment.Center,8,12,false);Assert.AreEqual(46d,result.Bounds.Y,.01);
        var edge=PopoverPositioner.Calculate(new Rect(0,0,32,32),new Size(240,80),new Rect(0,0,800,600),PopoverSide.Top,PopoverAlignment.Center,8,12,false);Assert.AreEqual(PopoverSide.Bottom,edge.Side);Assert.AreEqual(12d,edge.Bounds.X,.01);Assert.AreEqual(40d,edge.Bounds.Y,.01);
    }
    [TestMethod]
    public void GalleryAndConsumerConstructWithoutOpeningWindows()=>Sta.Run(()=>
    {Layout(new ArcWpf.Gallery.Pages.TooltipPage(),800,900);var window=new ArcWpf.Consumer.MainWindow();Assert.IsNotNull(window.Content);window.Close();});
    [TestMethod]
    public void ColdHoverDelayCanBeCancelledBeforePresentation()=>Sta.Run(()=>
    {
        var tip=Make("Help");Pump(330);tip.RequestOpen(false);Assert.IsTrue(tip.HasPendingDelay);Pump(80);Assert.IsFalse(tip.IsPresented);tip.Close();Assert.IsFalse(tip.HasPendingDelay);Pump(280);Assert.IsFalse(tip.IsPresented);
        tip.RequestOpen(false);Assert.IsTrue(tip.HasPendingDelay);tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(tip.HasPendingDelay);Assert.IsFalse(tip.IsPresented);
    });
    private static Tooltip Make(object tip){var result=new Tooltip{Content=new System.Windows.Controls.Button{Content="Trigger"},Tip=tip};Layout(result);MeasureBubble(Surface(result));return result;}
    private static TooltipSurface Surface(Tooltip tip)=>Part<TooltipSurface>(tip,"PART_Surface");
    private static void MeasureBubble(TooltipSurface surface){surface.Measure(new Size(240,1000));surface.Arrange(new Rect(new Point(),surface.DesiredSize));surface.UpdateLayout();Sta.Pump();}
    private static void Layout(FrameworkElement element,double width=120,double height=44){if(!element.IsInitialized){element.BeginInit();element.EndInit();}element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();Sta.Pump();}
    private static T Part<T>(Control control,string name)where T:FrameworkElement=>(T)control.Template.FindName(name,control);
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}

