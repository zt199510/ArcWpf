using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class CookieConsentTests
{
 [TestMethod] public void AcceptAllMarksRequiredAndOptional(){Sta.Run(()=>{var c=new CookieConsent();int calls=0;c.ConsentChanged+=(_,_)=>calls++;c.CommitForTest(true);Assert.IsNotNull(c.Consent);Assert.IsTrue(c.Consent!.Values.All(v=>v));Assert.AreEqual(1,calls);});}
 [TestMethod] public void RejectAllKeepsRequiredOn(){Sta.Run(()=>{var c=new CookieConsent();c.CommitForTest(false);Assert.IsTrue(c.Consent!["necessary"]);Assert.IsFalse(c.Consent["analytics"]);});}
 [TestMethod] public void PositionAndReopenVisibilityRefreshAfterTemplate(){Sta.Run(()=>{var c=new CookieConsent{Width=360,Position=CookieConsentPosition.BottomLeft};var w=new System.Windows.Window{Content=c,Width=500,Height=260,ShowInTaskbar=false};try{w.Show();Sta.Pump();var bar=(System.Windows.FrameworkElement)c.Template.FindName("PART_Bar",c)!;Assert.AreEqual(System.Windows.HorizontalAlignment.Left,bar.HorizontalAlignment);c.CommitForTest(true);c.ShowReopenButton=false;c.UpdateLayout();var chip=(System.Windows.FrameworkElement)c.Template.FindName("PART_Chip",c)!;Assert.AreEqual(System.Windows.Visibility.Collapsed,chip.Visibility);}finally{w.Close();}});}
 [TestMethod] public void CategoryCollectionChangesRefreshTheTemplateList(){Sta.Run(()=>{var c=new CookieConsent();var w=new System.Windows.Window{Content=c,Width=500,Height=260,ShowInTaskbar=false};try{w.Show();Sta.Pump();var list=(System.Windows.Controls.ItemsControl)c.Template.FindName("PART_CategoryList",c)!;var before=list.Items.Count;c.Categories.Add(new CookieCategory{Id="extra",Label="Extra"});Sta.Pump();Assert.AreEqual(before+1,list.Items.Count);}finally{w.Close();}});}
}
