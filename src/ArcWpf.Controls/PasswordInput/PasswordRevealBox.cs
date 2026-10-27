using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace ArcWpf.Controls;

// Native text editing while explicitly revealed. A non-TextBox peer avoids native plaintext Value-change events.
public sealed class PasswordRevealBox : TextBox
{
    static PasswordRevealBox()
    {
        IsUndoEnabledProperty.OverrideMetadata(typeof(PasswordRevealBox), new FrameworkPropertyMetadata(false, null, (_, _) => false));
        AcceptsReturnProperty.OverrideMetadata(typeof(PasswordRevealBox), new FrameworkPropertyMetadata(false, null, (_, _) => false));
        AcceptsTabProperty.OverrideMetadata(typeof(PasswordRevealBox), new FrameworkPropertyMetadata(false, null, (_, _) => false));
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new RevealPeer(this);
    private sealed class RevealPeer(PasswordRevealBox owner) : FrameworkElementAutomationPeer(owner), IValueProvider
    {
        protected override string GetClassNameCore() => nameof(PasswordRevealBox);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
        protected override bool IsPasswordCore() => true;
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.Value ? this : null;
        protected override List<AutomationPeer>? GetChildrenCore() => [];
        public bool IsReadOnly => owner.IsReadOnly;
        public string Value => "";
        public void SetValue(string value)
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            if (owner.IsReadOnly) throw new InvalidOperationException();
            ArgumentNullException.ThrowIfNull(value); owner.SetCurrentValue(TextProperty, value);
        }
    }
}
