using System.Windows.Input;
using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ShortcutRecorderTests
{
    [TestMethod] public void NormalizeUsesModAndFixedOrder()
    {
        Assert.AreEqual("mod+shift+k", Shortcuts.Normalize("shift+ctrl+K", ShortcutPlatform.Other));
        Assert.AreEqual("mod+k", Shortcuts.Normalize("meta+k", ShortcutPlatform.Mac));
    }

    [TestMethod] public void FromKeyReadsPhysicalKeys()
    {
        Assert.AreEqual("mod+k", Shortcuts.FromKey(Key.K, ModifierKeys.Control, ShortcutPlatform.Other));
        Assert.AreEqual("alt+n", Shortcuts.FromKey(Key.N, ModifierKeys.Alt, ShortcutPlatform.Other));
        Assert.AreEqual("mod+.", Shortcuts.FromKey(Key.OemPeriod, ModifierKeys.Control, ShortcutPlatform.Other));
        Assert.IsNull(Shortcuts.FromKey(Key.LeftCtrl, ModifierKeys.Control, ShortcutPlatform.Other));
    }

    [TestMethod] public void FormatFollowsPlatform()
    {
        Assert.AreEqual("Ctrl+Shift+F", Shortcuts.Format("mod+shift+f", ShortcutPlatform.Other).Text);
        Assert.AreEqual("⇧⌘F", Shortcuts.Format("mod+shift+f", ShortcutPlatform.Mac).Text);
        Assert.AreEqual("Control K", Shortcuts.Format("mod+k", ShortcutPlatform.Other).Spoken);
    }

    [TestMethod] public void StrongModifierRequiredExceptFunctionKeys()
    {
        Assert.IsFalse(Shortcuts.HasStrongModifier("shift+k"));
        Assert.IsTrue(Shortcuts.HasStrongModifier("f5"));
        Assert.IsTrue(Shortcuts.HasStrongModifier("alt+n"));
    }

    [TestMethod] public void RecorderStartsIdle() => Sta.Run(() =>
    {
        var recorder = new ShortcutRecorder { Shortcut = "mod+k" };
        Assert.IsFalse(recorder.IsRecording);
        Assert.AreEqual("Record shortcut", recorder.Placeholder);
    });
}
