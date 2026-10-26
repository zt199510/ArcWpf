using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using ArcTextarea = ArcWpf.Controls.Textarea;

namespace ArcWpf.Tests;

[TestClass]
public sealed class TextareaTests
{
    [TestMethod]
    public void DefaultsPreserveNativeMultilineEditing() => Sta.Run(() =>
    {
        var area = new ArcTextarea();
        Assert.IsTrue(area.AcceptsReturn);
        Assert.AreEqual(TextWrapping.Wrap, area.TextWrapping);
        Assert.AreEqual(ScrollBarVisibility.Auto, area.VerticalScrollBarVisibility);
        Assert.AreEqual(VerticalAlignment.Top, area.VerticalContentAlignment);
        var model = new Model { Value = "before" };
        area.DataContext = model;
        var binding = area.SetBinding(TextBox.TextProperty, new Binding(nameof(Model.Value)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        area.SetCurrentValue(TextBox.TextProperty, "line one\nline two");
        Assert.AreEqual("line one\nline two", model.Value);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(area, TextBox.TextProperty));
    });

    [TestMethod]
    public void TemplateKeepsNativeContentHostAndArcFieldRows() => Sta.Run(() =>
    {
        var area = new ArcTextarea { Label = "Notes", Description = "Add context.", ErrorMessage = "Required" };
        Layout(area);
        Assert.IsInstanceOfType<ScrollViewer>(area.Template.FindName("PART_ContentHost", area));
        Assert.AreEqual(new CornerRadius(18), ((Border)area.Template.FindName("Surface", area)).CornerRadius);
        Assert.AreEqual(110d, ((Border)area.Template.FindName("Surface", area)).MinHeight);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)area.Template.FindName("DescriptionText", area)).Visibility);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)area.Template.FindName("ErrorText", area)).Visibility);
        Assert.IsNull(Validation.GetErrorTemplate(area));
    });

    [TestMethod]
    public void PlaceholderIsPresentationOnlyAndTracksMultilineText() => Sta.Run(() =>
    {
        var area = new ArcTextarea { Width = 320, Placeholder = "Write a note" };
        Layout(area);
        Assert.IsTrue(area.IsPlaceholderVisible);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)area.Template.FindName("Watermark", area)).Visibility);
        area.Text = "first\nsecond";
        Assert.IsFalse(area.IsPlaceholderVisible);
        Assert.AreEqual(Visibility.Collapsed, ((TextBlock)area.Template.FindName("Watermark", area)).Visibility);
        area.Text = "";
        Assert.IsTrue(area.IsPlaceholderVisible);
    });

    [TestMethod]
    public void ExplicitAndValidationErrorsKeepEditedText() => Sta.Run(() =>
    {
        var area = new ArcTextarea { Text = "draft" };
        var binding = (BindingExpression)area.SetBinding(TextBox.TextProperty, new Binding(nameof(Model.Value)) { Source = new Model(), NotifyOnValidationError = false });
        area.SetCurrentValue(TextBox.TextProperty, "draft");
        area.ErrorMessage = "Explicit error";
        Assert.IsTrue(area.HasError);
        Assert.AreEqual("Explicit error", area.EffectiveErrorMessage);
        area.ErrorMessage = "";
        Validation.MarkInvalid(binding, new ValidationError(new Rule(), binding, "Server error", null));
        Assert.IsTrue(area.HasError);
        Assert.AreEqual("Server error", area.EffectiveErrorMessage);
        Assert.AreEqual("draft", area.Text);
        Validation.ClearInvalid(binding);
        Assert.IsFalse(area.HasError);
    });

    [TestMethod]
    public void AutomationUsesLabelDescriptionAndError() => Sta.Run(() =>
    {
        var area = new ArcTextarea { Label = "Description", Description = "Up to 500 characters.", ErrorMessage = "Too long." };
        var peer = UIElementAutomationPeer.CreatePeerForElement(area)!;
        Assert.AreEqual("Description", peer.GetName());
        Assert.AreEqual("Up to 500 characters. Too long.", peer.GetHelpText());
        area.SetCurrentValue(AutomationProperties.NameProperty, "Custom description");
        Assert.AreEqual("Custom description", peer.GetName());
    });

    [TestMethod]
    public void ReadOnlyAndDisabledPreserveValueAndState() => Sta.Run(() =>
    {
        var area = new ArcTextarea { Text = "saved\ntext", IsReadOnly = true, IsEnabled = false };
        Layout(area);
        Assert.IsTrue(area.IsReadOnly);
        Assert.IsFalse(area.IsEnabled);
        Assert.AreEqual("saved\ntext", area.Text);
        Assert.AreEqual(.5, area.Opacity, .001);
    });

    private static void Layout(FrameworkElement element)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(500, 600));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.ApplyTemplate();
        element.UpdateLayout();
    }

    private sealed class Model : INotifyPropertyChanged
    {
        public string Value { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Rule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo) => ValidationResult.ValidResult;
    }
}


