using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf.Controls;
public partial class PasswordInput
{
    private readonly OwnedMotion _motion = new();
    private Border? _shell;
    private FrameworkElement? _editorVisual;
    private BlurEffect? _resolveBlur;
    private SolidColorBrush? _border;
    private PasswordMessage? _description, _errorMessage;
    internal bool IsMotionRunning => _motion.IsRunning || (_toggle?.IsMotionRunning ?? false) || (_description?.IsMotionRunning ?? false) || (_errorMessage?.IsMotionRunning ?? false);
    private void InitializeVisuals()
    {
        _shell = GetTemplateChild("PART_Shell") as Border; _border = null;
        _editorVisual = GetTemplateChild("PART_EditorVisual") as FrameworkElement;
        if (_editorVisual is not null) { _resolveBlur = new() { Radius = 0 }; _editorVisual.Effect = _resolveBlur; }
        if (GetTemplateChild("PART_DescriptionHost") is Panel hint) { _description = new(); hint.Children.Add(_description); }
        if (GetTemplateChild("PART_ErrorHost") is Panel error) { _errorMessage = new(); error.Children.Add(_errorMessage); }
    }
    private void ReleaseVisuals()
    { StopMotion(); _description?.Detach(); _errorMessage?.Detach(); _description = _errorMessage = null; _shell = null; _editorVisual = null; _resolveBlur = null; _border = null; }
    private void StopMotion() { _motion.Stop(); _toggle?.Stop(); _description?.Stop(); _errorMessage?.Stop(); }
    private void UpdateColors(bool animate)
    {
        if (_shell is null || BorderBrush is not SolidColorBrush brush) return;
        _border ??= new(brush.Color); _shell.BorderBrush = _border;
        _motion.Color(_border, brush.Color, .16, animate && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full);
    }
    private void AnimateResolve()
    {
        if (_editorVisual is null || _resolveBlur is null) return;
        bool animate = !_releasing && IsLoaded && IsVisible && ArcMotion.GetLevel(this) == ArcMotionLevel.Full;
        // Never retain a departing plaintext layer. Only the newly active native editor is rendered.
        _motion.Double(_editorVisual, OpacityProperty, .35, 0, false);
        _motion.Double(_resolveBlur, BlurEffect.RadiusProperty, animate ? 2 : 0, 0, false);
        _motion.Double(_editorVisual, OpacityProperty, 1, .24, animate, enter: true);
        _motion.Double(_resolveBlur, BlurEffect.RadiusProperty, 0, .24, animate, enter: true);
    }
    private void UpdateMessages(bool animate)
    {
        var errors = Validation.GetErrors(this);
        string message = !string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorMessage : errors.Select(e => e.ErrorContent as string).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? (errors.Count > 0 ? "输入值无效" : "");
        bool changed = EffectiveErrorMessage != message;
        SetValue(HasErrorPropertyKey, !string.IsNullOrWhiteSpace(message)); SetValue(EffectiveErrorMessagePropertyKey, message);
        _description?.SetText(Description ?? "", animate); _errorMessage?.SetText(message, animate);
        foreach (var editor in new FrameworkElement?[] { _password, _reveal })
            if (editor is not null) AutomationProperties.SetHelpText(editor, string.Join(" ", new[] { Description, message }.Where(s => !string.IsNullOrWhiteSpace(s))));
        if (changed && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged) && UIElementAutomationPeer.FromElement(this) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
