using System.Windows;using System.Windows.Controls;using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass]public sealed class FormLayoutTests
{
 [TestMethod]public void VerticalSpacingMaxWidthAndCollapsedChildrenUseNativeLayout()=>Sta.Run(()=>{var form=new FormLayout{Spacing=20,MaxContentWidth=540};var a=new Border{Height=44};var b=new Border{Height=36,Visibility=Visibility.Collapsed};var c=new Border{Height=44};form.Children.Add(a);form.Children.Add(b);form.Children.Add(c);Layout(form,800);Assert.AreEqual(540d,form.ActualWidth,.01);Assert.AreEqual(108d,form.ActualHeight,.01);Assert.AreEqual(64d,c.TranslatePoint(new Point(),form).Y,.01);});
 [TestMethod]public void NativeBindingsAndChildrenRemainIntact()=>Sta.Run(()=>{var input=new Input{Label="Name"};var form=new FormLayout{Children={input}};Layout(form);Assert.AreSame(input,form.Children[0]);Assert.AreEqual("Name",input.Label);});
 static void Layout(FrameworkElement e,double w=800){e.Measure(new Size(w,800));e.Arrange(new Rect(0,0,w,e.DesiredSize.Height));e.UpdateLayout();Sta.Pump();}
}
