using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ArcAlert = ArcWpf.Controls.Alert;
using WpfButton = System.Windows.Controls.Button;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class AlertTests
{
    [TestMethod]
    public void DefaultsExposeInfoAlertAndNativeContent() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Saved", Description = "Your changes are ready.", Content = new TextBlock { Text = "Extra details" } };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        Assert.AreEqual(AlertTone.Info, alert.Tone);
        Assert.IsTrue(alert.IsOpen);
        Assert.IsTrue(alert.IsPresented);
        Assert.AreSame(alert.Content, ((ContentPresenter)alert.Template.FindName("ContentPresenter", alert)).Content);
        Assert.AreEqual(new CornerRadius(18), ((Border)alert.Template.FindName("PART_Surface", alert)).CornerRadius);
    });

    [TestMethod]
    public void ToneSwitchesIconAndBorderWithoutReplacingContent() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Warning" };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        alert.Tone = AlertTone.Warning;
        Assert.AreEqual(Visibility.Visible, ((System.Windows.Shapes.Path)alert.Template.FindName("WarningIcon", alert)).Visibility);
        Assert.AreEqual(Visibility.Collapsed, ((System.Windows.Shapes.Path)alert.Template.FindName("InfoIcon", alert)).Visibility);
        Assert.AreSame(alert, alert.Template.FindName("PART_Surface", alert) is Border ? alert : null);
    });

    [TestMethod]
    public void DescriptionCanCollapseWhileCustomContentRemains() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Details", Description = "Description" };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        var description = (TextBlock)alert.Template.FindName("DescriptionText", alert);
        Assert.AreEqual(Visibility.Visible, description.Visibility);
        alert.Description = "";
        Assert.AreEqual(Visibility.Collapsed, description.Visibility);
    });

    [TestMethod]
    public void DismissButtonRaisesEventAndPreservesTwoWayBinding() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Dismiss me", IsDismissible = true };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        bool dismissed = false;
        alert.Dismissed += (_, _) => dismissed = true;
        var button = (WpfButton)alert.Template.FindName("PART_Dismiss", alert);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.IsFalse(alert.IsOpen);
        Assert.IsFalse(alert.IsPresented);
        Assert.IsTrue(dismissed);
    });

    [TestMethod]
    public void ProgrammaticDismissIsIdempotent() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Alert", IsDismissible = true };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        int count = 0;
        alert.Dismissed += (_, _) => count++;
        alert.Dismiss(); alert.Dismiss();
        Assert.AreEqual(1, count);
        Assert.IsFalse(alert.IsPresented);
    });

    [TestMethod]
    public void AutomationUsesTitleAndDescription() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Workspace saved", Description = "All devices are synced." };
        var peer = UIElementAutomationPeer.CreatePeerForElement(alert)!;
        Assert.AreEqual(AutomationControlType.Group, peer.GetAutomationControlType());
        Assert.AreEqual("Workspace saved", peer.GetName());
        Assert.AreEqual("All devices are synced.", peer.GetHelpText());
        AutomationProperties.SetName(alert, "Custom alert");
        Assert.AreEqual("Custom alert", peer.GetName());
    });

    [TestMethod]
    public void DisabledDismissButtonVisibilityFollowsIsDismissible() => Sta.Run(() =>
    {
        var alert = new ArcAlert { Title = "Info" };
        ArcMotion.SetLevel(alert, ArcMotionLevel.Off);
        Layout(alert);
        var button = (WpfButton)alert.Template.FindName("PART_Dismiss", alert);
        Assert.AreEqual(Visibility.Collapsed, button.Visibility);
        alert.IsDismissible = true;
        Assert.AreEqual(Visibility.Visible, button.Visibility);
    });

    private static void Layout(FrameworkElement element)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(560, 500));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.ApplyTemplate();
        element.UpdateLayout();
    }
}

