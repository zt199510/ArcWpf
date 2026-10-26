using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using ArcInput = ArcWpf.Controls.Input;

namespace ArcWpf.Tests;

[TestClass]
public sealed class InputTests
{
    [TestMethod]
    public void DefaultTextMetadataAndExplicitUpdateKeepBinding() => Sta.Run(() =>
    {
        var metadata = (FrameworkPropertyMetadata)TextBox.TextProperty.GetMetadata(typeof(ArcInput));
        Assert.IsTrue(metadata.BindsTwoWayByDefault);
        Assert.AreEqual(UpdateSourceTrigger.LostFocus, metadata.DefaultUpdateSourceTrigger);
        var model = new Model { Value = "before" };
        var input = new ArcInput { DataContext = model };
        var binding = input.SetBinding(TextBox.TextProperty, new Binding(nameof(Model.Value)));
        input.SetCurrentValue(TextBox.TextProperty, "after");
        Assert.AreEqual("before", model.Value);
        ((BindingExpression)binding).UpdateSource();
        Assert.AreEqual("after", model.Value);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(input, TextBox.TextProperty));
    });

    [TestMethod]
    public void PropertyChangedBindingUpdatesWithoutReplacingDataContext() => Sta.Run(() =>
    {
        var model = new Model();
        var input = new ArcInput { DataContext = model };
        input.SetBinding(TextBox.TextProperty, new Binding(nameof(Model.Value)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        input.SetCurrentValue(TextBox.TextProperty, "中文 text");
        Assert.AreEqual("中文 text", model.Value);
        Assert.AreSame(model, input.DataContext);
    });

    [TestMethod]
    public void ExplicitErrorOverridesValidationAndClearingFallsBackWithoutNotificationFlag() => Sta.Run(() =>
    {
        var input = new ArcInput();
        var binding = Bind(input);
        Validation.MarkInvalid(binding, new ValidationError(new Rule(), binding, "源验证错误", null));
        Assert.IsTrue(input.HasError);
        Assert.AreEqual("源验证错误", input.EffectiveErrorMessage);
        input.ErrorMessage = "显式错误";
        Assert.AreEqual("显式错误", input.EffectiveErrorMessage);
        input.ErrorMessage = " ";
        Assert.AreEqual("源验证错误", input.EffectiveErrorMessage);
        Assert.HasCount(1, Validation.GetErrors(input));
        Validation.ClearInvalid(binding);
        Assert.IsFalse(input.HasError);
        Assert.AreEqual("", input.EffectiveErrorMessage);
    });

    [TestMethod]
    public void NotifyDataErrorsArriveAndClearWithoutRewritingEditedText() => Sta.Run(() =>
    {
        var source = new NotifyModel();
        var input = new ArcInput();
        input.SetBinding(TextBox.TextProperty, new Binding(nameof(NotifyModel.Value))
        { Source = source, ValidatesOnNotifyDataErrors = true, NotifyOnValidationError = false });
        input.SetCurrentValue(TextBox.TextProperty, "pending edit");
        source.SetError("服务端验证错误");
        Sta.Pump();
        Assert.IsTrue(input.HasError);
        Assert.AreEqual("服务端验证错误", input.EffectiveErrorMessage);
        Assert.AreEqual("pending edit", input.Text);
        source.SetError(null);
        Sta.Pump();
        Assert.IsFalse(input.HasError);
        Assert.AreEqual("pending edit", input.Text);
    });

    [TestMethod]
    public void ValidationFallbackNeverDisplaysExceptionDetails() => Sta.Run(() =>
    {
        var input = new ArcInput();
        input.Resources["ArcWpf.String.InvalidInput"] = "Invalid input";
        var binding = Bind(input);
        Validation.MarkInvalid(binding, new ValidationError(new Rule(), binding, new Exception("private stack"), null));
        Assert.IsTrue(input.HasError);
        Assert.AreEqual("Invalid input", input.EffectiveErrorMessage);
    });

    [TestMethod]
    public void ValidationReplacementAndUnloadReloadPreserveTextSelection() => Sta.Run(() =>
    {
        var input = new ArcInput();
        var binding = Bind(input);
        input.SetCurrentValue(TextBox.TextProperty, "abcdef");
        input.Select(2, 3);
        Validation.MarkInvalid(binding, new ValidationError(new Rule(), binding, "one", null));
        Validation.MarkInvalid(binding, new ValidationError(new Rule(), binding, "two", null));
        Assert.AreEqual("two", input.EffectiveErrorMessage);
        input.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Validation.ClearInvalid(binding);
        input.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.IsFalse(input.HasError);
        Assert.AreEqual("abcdef", input.Text);
        Assert.AreEqual(2, input.SelectionStart);
        Assert.AreEqual(3, input.SelectionLength);
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(input, TextBox.TextProperty));
    });

    [TestMethod]
    public void DefaultTemplateKeepsNativeEditorAndSeparatesLabelAndMessages() => Sta.Run(() =>
    {
        var input = new ArcInput { Label = "Email", Description = "Description", ErrorMessage = "Error", Placeholder = "example" };
        Layout(input);
        Assert.IsInstanceOfType<ScrollViewer>(input.Template.FindName("PART_ContentHost", input));
        var surface = (Border)input.Template.FindName("Surface", input);
        Assert.AreEqual(44d, surface.MinHeight);
        Assert.AreEqual(new CornerRadius(18), surface.CornerRadius);
        Assert.IsGreaterThan(44d, input.DesiredSize.Height);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)input.Template.FindName("DescriptionText", input)).Visibility);
        Assert.AreEqual(Visibility.Visible, ((TextBlock)input.Template.FindName("ErrorText", input)).Visibility);
        Assert.IsNull(Validation.GetErrorTemplate(input));
    });

    [TestMethod]
    public void PlaceholderAndTypedTextStartAtSamePosition() => Sta.Run(() =>
    {
        var input = new ArcInput { Width = 300, Placeholder = "Sample" };
        Layout(input);
        var watermark = (TextBlock)input.Template.FindName("Watermark", input);
        var origin = watermark.TransformToAncestor(input).Transform(new Point());
        input.Text = "Sample";
        Layout(input);
        var text = input.GetRectFromCharacterIndex(0);
        var host = (ScrollViewer)input.Template.FindName("PART_ContentHost", input);
        Assert.AreEqual(origin.X, text.X, .5, $"Placeholder={origin}; text={text}; host padding={host.Padding}; host margin={host.Margin}");
        Assert.AreEqual(origin.Y, text.Y, .5, $"Placeholder={origin}; text={text}");
    });

    [TestMethod]
    public void PlaceholderIsPresentationOnlyAndTracksEmptyText() => Sta.Run(() =>
    {
        var input = new ArcInput { Placeholder = "hint" };
        Layout(input);
        Assert.IsTrue(input.IsPlaceholderVisible);
        Assert.AreEqual("", input.Text);
        var placeholder = (TextBlock)input.Template.FindName("Watermark", input);
        Assert.IsFalse(placeholder.IsHitTestVisible);
        input.Text = "x";
        Assert.IsFalse(input.IsPlaceholderVisible);
        input.Text = "";
        Assert.IsTrue(input.IsPlaceholderVisible);
    });

    [TestMethod]
    public void ReadOnlyDisabledAndAutomationPreserveErrorsAndNativeValuePattern() => Sta.Run(() =>
    {
        var input = new ArcInput { Label = "邮箱", Description = "说明", ErrorMessage = "错误", Text = "a", IsReadOnly = true };
        var peer = UIElementAutomationPeer.CreatePeerForElement(input)!;
        Assert.AreEqual("邮箱", peer.GetName());
        Assert.AreEqual("说明 错误", peer.GetHelpText());
        var value = (IValueProvider)peer.GetPattern(PatternInterface.Value);
        Assert.IsTrue(value.IsReadOnly);
        AutomationProperties.SetName(input, "账户邮箱");
        Assert.AreEqual("账户邮箱", peer.GetName());
        input.IsEnabled = false;
        Assert.IsTrue(input.HasError);
        Assert.AreEqual("a", input.Text);
    });

    [TestMethod]
    public void TemplateReplacementKeepsBindingAndCustomValidationTemplate() => Sta.Run(() =>
    {
        var input = new ArcInput { Label = "Name" };
        var binding = Bind(input);
        Layout(input);
        var custom = new ControlTemplate();
        Validation.SetErrorTemplate(input, custom);
        input.Template = new ControlTemplate(typeof(ArcInput));
        input.ApplyTemplate();
        Assert.AreSame(binding, BindingOperations.GetBindingExpression(input, TextBox.TextProperty));
        Assert.AreSame(custom, Validation.GetErrorTemplate(input));
        input.ErrorMessage = "error";
        Assert.IsTrue(input.HasError);
    });

    private static BindingExpression Bind(ArcInput input) => (BindingExpression)input.SetBinding(TextBox.TextProperty,
        new Binding(nameof(Model.Value)) { Source = new Model(), NotifyOnValidationError = false });
    private static void Layout(FrameworkElement element)
    {
        if (!element.IsInitialized) { element.BeginInit(); element.EndInit(); }
        element.Measure(new Size(380, 500));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.ApplyTemplate();
        element.UpdateLayout();
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private string _value = "";
        public string Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Rule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo) => ValidationResult.ValidResult;
    }
    private sealed class NotifyModel : INotifyDataErrorInfo
    {
        public string Value { get; set; } = "";
        private string? _error;
        public bool HasErrors => _error is not null;
        public System.Collections.IEnumerable GetErrors(string? propertyName) => _error is null ? Array.Empty<string>() : new[] { _error };
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
        public void SetError(string? error) { _error = error; ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Value))); }
    }
}
