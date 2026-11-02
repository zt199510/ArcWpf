using System.Collections.ObjectModel;
using ArcWpf.Controls;
using System.Windows;

namespace ArcWpf.Tests;

[TestClass]
public sealed class NextArcControlsTests
{
    [TestMethod] public void CountdownExposesRemainingAndCompletion() => Sta.Run(() => { var c = new Countdown { Target = DateTime.UtcNow.AddSeconds(3) }; Assert.IsFalse(c.IsComplete); c.Target = DateTime.UtcNow.AddSeconds(-1); Assert.IsTrue(c.IsComplete); Assert.AreEqual(TimeSpan.Zero, c.Remaining); });
    [TestMethod] public void AnimatedCounterKeepsFormattingContract() => Sta.Run(() => { var c = new AnimatedCounter { Value = 1234.5, Decimals = 1, Prefix = "$", Suffix = " USD" }; Assert.AreEqual("$", c.Prefix); Assert.AreEqual(1, c.Decimals); });
    [TestMethod] public void AnnouncementBarCyclesAndCloses() => Sta.Run(() => { var b = new AnnouncementBar { Messages = new ObservableCollection<string> { "a", "b" }, AutoPlay = false }; b.Index = 1; Assert.AreEqual(1, b.Index); b.Close(); Assert.IsFalse(b.IsOpen); });
    [TestMethod] public void TimelineStoresEvents() => Sta.Run(() => { var t = new Timeline(); t.Events.Add(new TimelineEvent { Title = "Build" }); Assert.HasCount(1, t.Events); });
    [TestMethod] public void FileDropzoneHonorsLimits() => Sta.Run(() => { var d = new FileDropzone { MaxFiles = 2, Multiple = true }; Assert.AreEqual(2, d.MaxFiles); Assert.IsTrue(d.AllowDrop); });
    [TestMethod] public void AvatarUploadHasFallbackInitials() => Sta.Run(() => { var a = new AvatarUpload { Initials = "AW" }; Assert.AreEqual("AW", a.Initials); });
    [TestMethod] public void MentionInputRetainsNativeTextEditing() => Sta.Run(() => { var input = new MentionInput { Text = "@Ada" }; Assert.AreEqual("@Ada", input.Text); Assert.IsTrue(input.AcceptsReturn); });
    [TestMethod] public void EmojiPickerHasDefaultPalette() => Sta.Run(() => { var picker = new EmojiPicker(); Assert.IsGreaterThan(10, picker.Emojis.Count); });
}
