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
using ArcCheckBox = ArcWpf.Controls.CheckBox;
namespace ArcWpf.Tests;

[TestClass]
public sealed class CheckBoxTests
{
    [TestMethod]
    public void NativeTwoAndThreeStateCyclesPreserveBindingAndEvents() => Sta.Run(() =>
    {
        var model=new Model(); var box=new Probe { DataContext=model }; int clicks=0, checks=0, mixed=0, unchecks=0;
        var binding=box.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(Model.Value)));
        box.Click+=(_,_)=>clicks++; box.Checked+=(_,_)=>checks++; box.Indeterminate+=(_,_)=>mixed++; box.Unchecked+=(_,_)=>unchecks++;
        box.ClickNow(); Assert.IsTrue(model.Value); box.ClickNow(); Assert.IsFalse(model.Value);
        box.IsThreeState=true; box.ClickNow(); box.ClickNow(); Assert.IsNull(model.Value); box.ClickNow(); Assert.IsFalse(model.Value);
        Assert.AreEqual(5,clicks); Assert.AreEqual(2,checks); Assert.AreEqual(1,mixed); Assert.AreEqual(2,unchecks);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(box,ToggleButton.IsCheckedProperty));
        model.Value=null; box.IsThreeState=false; box.ClickNow(); Assert.IsFalse(model.Value);
    });
    [TestMethod]
    public void CommandParameterAndCanExecuteAreNative() => Sta.Run(() =>
    {
        var command=new Command(); var box=new Probe { Command=command, CommandParameter="payload" };
        box.ClickNow(); Assert.AreEqual("payload",command.Last); Assert.AreEqual(1,command.Count);
        command.Enabled=false; command.Notify(); Assert.IsFalse(box.IsEnabled);
        box.ClickNow(); Assert.AreEqual(1,command.Count); Assert.IsTrue(box.IsChecked);
    });
    [TestMethod]
    public void AutomationReportsThreeStatesNameAndDescription() => Sta.Run(() =>
    {
        var box=new ArcCheckBox { Content="Label", Description="Helpful", IsThreeState=true };
        var peer=UIElementAutomationPeer.CreatePeerForElement(box)!;
        Assert.AreEqual(AutomationControlType.CheckBox,peer.GetAutomationControlType());
        Assert.AreEqual("Label",peer.GetName()); Assert.AreEqual("Helpful",peer.GetHelpText());
        var toggle=(IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!;
        Assert.AreEqual(ToggleState.Off,toggle.ToggleState); toggle.Toggle(); Assert.AreEqual(ToggleState.On,toggle.ToggleState);
        toggle.Toggle(); Assert.AreEqual(ToggleState.Indeterminate,toggle.ToggleState);
        AutomationProperties.SetName(box,"Explicit"); AutomationProperties.SetHelpText(box,"Override");
        Assert.AreEqual("Explicit",peer.GetName()); Assert.AreEqual("Override",peer.GetHelpText());
        box.IsEnabled=false; Assert.ThrowsExactly<ElementNotEnabledException>(()=>toggle.Toggle());
    });
    [TestMethod]
    public void FinalLayoutAlignsSquareWithFirstLineAndKeepsDescriptionStart() => Sta.Run(() =>
    {
        var box=Create("Include archived projects","Show projects that are no longer active.");
        var square=Part<Border>(box,"PART_Box"); var label=Part<ContentPresenter>(box,"Label"); var description=Part<TextBlock>(box,"DescriptionText");
        Point origin=square.TranslatePoint(new Point(),box), labelOrigin=label.TranslatePoint(new Point(),box), descOrigin=description.TranslatePoint(new Point(),box);
        Assert.AreEqual(new Size(18,18),square.RenderSize);
        Assert.AreEqual(13d,origin.X,.01); Assert.AreEqual(13d,origin.Y,.01);
        Assert.AreEqual(45d,labelOrigin.X,.01); Assert.AreEqual(labelOrigin.X,descOrigin.X,.01);
        Assert.AreEqual(origin.Y+9,labelOrigin.Y+19.6/2,.1);
        Assert.AreEqual(2d,descOrigin.Y-labelOrigin.Y-label.ActualHeight,.1);
    });
    [TestMethod]
    public void WrappingDoesNotMoveTheSquareAndEmptyContentHasNoPhantomGap() => Sta.Run(() =>
    {
        var longBox=Create("A long label which must wrap across several lines and still align to the first line.","A description which also wraps over several lines.",180);
        Assert.IsGreaterThan(19.6,Part<ContentPresenter>(longBox,"Label").ActualHeight);
        Assert.AreEqual(13d,Part<Border>(longBox,"PART_Box").TranslatePoint(new Point(),longBox).Y,.01);
        var empty=Create(null,""); Assert.AreEqual(44d,empty.DesiredSize.Width,.01); Assert.AreEqual(44d,empty.DesiredSize.Height,.01);
        var onlyDescription=Create(null,"Description only"); Assert.AreEqual(Visibility.Collapsed,Part<ContentPresenter>(onlyDescription,"Label").Visibility);
        Assert.AreEqual(12.2,Part<TextBlock>(onlyDescription,"DescriptionText").TranslatePoint(new Point(),onlyDescription).Y,.01);
    });
    [TestMethod]
    public void PathsUseOriginalCoordinatesAndMorphRatherThanSwap()
    {
        var check=ArcCheckBox.CreateMark(0,1).Bounds; var dash=ArcCheckBox.CreateMark(1,1).Bounds; var middle=ArcCheckBox.CreateMark(.5,1).Bounds;
        Assert.AreEqual(new Rect(4.25,5.75,9.5,6.5),check);
        Assert.AreEqual(new Rect(4.75,9,8.5,0),dash);
        Assert.AreEqual(4.5,middle.Left,.001); Assert.AreEqual(3.25,middle.Height,.001);
        Assert.IsTrue(ArcCheckBox.CreateMark(0,0).IsEmpty());
        Assert.IsLessThan(check.Width,ArcCheckBox.CreateMark(0,.2).Bounds.Width);
    }
    [TestMethod]
    public void MotionReversesFromCurrentValueAndOffUnsubscribes() => Sta.Run(() =>
    {
        var box=Create("Motion",""); box.IsChecked=true; box.UpdateVisual(true);
        // State was snapped while unloaded; set an animated target directly through the helper.
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var draw=(ArcWpf.Controls.ControlSpring)typeof(ArcCheckBox).GetField("_draw",flags)!.GetValue(box)!;
        draw.Snap(0); box.UpdateVisual(true); box.AdvanceMotion(.04); double before=box.DrawProgress;
        Assert.IsGreaterThan(0d,before); Assert.IsLessThan(1d,before);
        draw.Target=0; box.AdvanceMotion(.01); Assert.IsGreaterThan(0d,box.DrawProgress);
        ArcMotion.SetLevel(box,ArcMotionLevel.Off); Assert.IsFalse(box.IsMotionRunning); Assert.AreEqual(1d,box.DrawProgress);
    });
    [TestMethod]
    public void UnloadAndTemplateReplacementCleanActiveMotion() => Sta.Run(() =>
    {
        var box=Create("Motion","");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var draw=(ArcWpf.Controls.ControlSpring)typeof(ArcCheckBox).GetField("_draw",flags)!.GetValue(box)!;
        draw.Snap(1); box.UpdateVisual(true); Assert.IsTrue(box.IsMotionRunning);
        box.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Assert.IsFalse(box.IsMotionRunning);
        draw.Snap(1); box.UpdateVisual(true); Assert.IsTrue(box.IsMotionRunning);
        box.Template=new ControlTemplate(typeof(ArcCheckBox)); box.ApplyTemplate(); Assert.IsFalse(box.IsMotionRunning);
        box.IsChecked=null; box.UpdateVisual(true); Assert.IsFalse(box.IsMotionRunning);
    });
    [TestMethod]
    public void ReducedMotionSnapsMorphAndDisabledOnlyDimsHitBox() => Sta.Run(() =>
    {
        var box=Create("Label","Description"); ArcMotion.SetLevel(box,ArcMotionLevel.Reduced);
        box.IsChecked=null; box.UpdateVisual(true); Assert.AreEqual(1d,box.MorphProgress); Assert.AreEqual(1d,box.DrawProgress); Assert.IsFalse(box.IsMotionRunning);
        box.IsEnabled=false; Assert.AreEqual(.5,Part<Grid>(box,"HitBox").Opacity); Assert.AreEqual(1d,Part<StackPanel>(box,"Copy").Opacity);
    });
    [TestMethod]
    public void RtlKeepsTextBesideHitBoxAndMarkInItsOwnCoordinateSystem() => Sta.Run(() =>
    {
        var box=Create("Label","Description"); box.FlowDirection=FlowDirection.RightToLeft; box.IsChecked=true;
        box.Measure(new Size(300,200)); box.Arrange(new Rect(new Point(),box.DesiredSize)); box.UpdateLayout();
        var mark=Part<System.Windows.Shapes.Path>(box,"PART_Mark");
        Assert.AreEqual(new Size(18,18),mark.RenderSize);
        Assert.AreEqual(FlowDirection.LeftToRight,mark.FlowDirection);
        Assert.AreEqual(ArcCheckBox.CreateMark(0,1).Bounds,mark.RenderedGeometry.Bounds);
        var label=Part<ContentPresenter>(box,"Label"); var description=Part<TextBlock>(box,"DescriptionText");
        Assert.AreEqual(label.TranslatePoint(new Point(),box).X,description.TranslatePoint(new Point(),box).X,.01);
    });
    [TestMethod]
    public void ExplicitContentTemplateAndDescriptionBindingRemainUsable() => Sta.Run(() =>
    {
        var box=Create("Initial",""); var source=new TextBlock { Text="Bound description" };
        var binding=box.SetBinding(ArcCheckBox.DescriptionProperty,new Binding(nameof(TextBlock.Text)) { Source=source });
        box.ContentTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}' FontSize='16'/></DataTemplate>");
        box.Content="Custom label"; box.UpdateLayout();
        Assert.AreEqual("Bound description",Part<TextBlock>(box,"DescriptionText").Text);
        source.Text="Updated"; Sta.Pump(); Assert.AreEqual("Updated",Part<TextBlock>(box,"DescriptionText").Text);
        Assert.AreSame(binding,BindingOperations.GetBindingExpression(box,ArcCheckBox.DescriptionProperty));
        Assert.AreSame(box.ContentTemplate,Part<ContentPresenter>(box,"Label").ContentTemplate);
    });
    [TestMethod]
    public void UncheckFadeStaysTransparentAfterAnimationCompletes() => Sta.Run(() =>
    {
        var box=Create("Unchecked","");
        var fill=Part<Border>(box,"PART_Fill");
        var mark=Part<System.Windows.Shapes.Path>(box,"PART_Mark");
        fill.SetCurrentValue(UIElement.OpacityProperty,1d);
        mark.SetCurrentValue(UIElement.OpacityProperty,1d);
        box.UpdateVisual(true);
        PumpFor(TimeSpan.FromMilliseconds(300));
        Assert.AreEqual(0d,fill.Opacity,.001,"The fill must not return to its template default after fading out.");
        Assert.AreEqual(0d,mark.Opacity,.001);
        Assert.IsFalse(fill.HasAnimatedProperties);
        Assert.IsFalse(mark.HasAnimatedProperties);
        box.UpdateVisual(true);
        PumpFor(TimeSpan.FromMilliseconds(300));
        Assert.AreEqual(0d,fill.Opacity,.001,"A later border/press update must not flash the fill again.");
    });
    private static void PumpFor(TimeSpan duration)
    {
        var frame=new System.Windows.Threading.DispatcherFrame();
        var timer=new System.Windows.Threading.DispatcherTimer { Interval=duration };
        timer.Tick+=(_,_)=> { timer.Stop(); frame.Continue=false; };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    [TestMethod]
    public void LocalThemeUpdatesWithoutMutatingSharedBrush() => Sta.Run(() =>
    {
        var box=Create("Theme",""); var theme=new ArcThemeResources(); box.Resources.MergedDictionaries.Add(theme);
        box.IsChecked=true; var shared=(SolidColorBrush)theme["ArcWpf.Brush.ControlOn"]; Color light=shared.Color;
        theme.Mode=ArcThemeMode.Dark; Sta.Pump();
        Assert.AreNotEqual(light,((SolidColorBrush)Part<Border>(box,"PART_Box").BorderBrush).Color);
        Assert.AreEqual(light,shared.Color); Assert.IsTrue(shared.IsFrozen);
    });
    private static T Part<T>(ArcCheckBox box,string name) where T:FrameworkElement => (T)box.Template.FindName(name,box);
    private static ArcCheckBox Create(object? content,string description,double width=500)
    {
        var box=new ArcCheckBox { Content=content, Description=description };
        box.BeginInit(); box.EndInit(); box.Measure(new Size(width,500)); box.Arrange(new Rect(new Point(),box.DesiredSize)); box.UpdateLayout(); return box;
    }
    private sealed class Probe:ArcCheckBox { public void ClickNow()=>OnClick(); }
    private sealed class Model:INotifyPropertyChanged
    {
        private bool? _value=false;
        public bool? Value {get=>_value;set{_value=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Value)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Command:ICommand
    {
        public bool Enabled=true; public object? Last; public int Count;
        public bool CanExecute(object? parameter)=>Enabled;
        public void Execute(object? parameter){Last=parameter;Count++;}
        public event EventHandler? CanExecuteChanged;
        public void Notify()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
    }
}

