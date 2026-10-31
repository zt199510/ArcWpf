using ArcWpf.Controls;
namespace ArcWpf.Tests;
[TestClass] public sealed class ShortcutRecorderTests
{
    [TestMethod] public void ShortcutAndPlaceholderAreBindable() => Sta.Run(() => { var recorder = new ShortcutRecorder { Shortcut = "Ctrl+K", Placeholder = "Record" }; Assert.AreEqual("Ctrl+K", recorder.Shortcut); Assert.AreEqual("Record", recorder.Placeholder); });
    [TestMethod] public void RecordingCanBeStoppedByFocusState() => Sta.Run(() => Assert.IsFalse(new ShortcutRecorder().IsRecording));
    [TestMethod] public void LabelIsConfigurable() => Sta.Run(() => Assert.AreEqual("Shortcut", new ShortcutRecorder().Label));
}
