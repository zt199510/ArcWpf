using System.Collections.ObjectModel;
using ArcWpf.Controls;
using System.Windows;

namespace ArcWpf.Tests;

[TestClass]
public sealed class NextArcControlsTests
{
    [TestMethod] public void CountdownExposesRemainingAndCompletion() => Sta.Run(() => { var c = new Countdown { Target = DateTime.UtcNow.AddSeconds(3) }; Assert.IsFalse(c.IsComplete); c.Target = DateTime.UtcNow.AddSeconds(-1); Assert.IsTrue(c.IsComplete); Assert.AreEqual(TimeSpan.Zero, c.Remaining); });
    [TestMethod] public void CountdownSupportsArcUnitSelection() => Sta.Run(() => { var c = new Countdown { Target = DateTime.UtcNow.AddHours(27), Units = new ObservableCollection<CountdownUnit> { CountdownUnit.Hours, CountdownUnit.Minutes } }; Assert.HasCount(2, c.Units); Assert.Contains(CountdownUnit.Hours, c.Units); });
    [TestMethod] public void AnimatedCounterKeepsFormattingContract() => Sta.Run(() => { var c = new AnimatedCounter { Value = 1234.5, Decimals = 1, Prefix = "$", Suffix = " USD" }; Assert.AreEqual("$", c.Prefix); Assert.AreEqual(1, c.Decimals); });
    [TestMethod] public void AnnouncementBarCyclesAndCloses() => Sta.Run(() => { var b = new AnnouncementBar { Messages = new ObservableCollection<string> { "a", "b" }, AutoPlay = false }; b.Index = 1; Assert.AreEqual(1, b.Index); b.Close(); Assert.IsFalse(b.IsOpen); });
    [TestMethod] public void TimelineStoresEvents() => Sta.Run(() => { var t = new Timeline(); t.Events.Add(new TimelineEvent { Title = "Build" }); Assert.HasCount(1, t.Events); });
    [TestMethod] public void TimelineFormatsRelativeTime() => Sta.Run(() => { var t = new Timeline(); var item = new TimelineEvent { At = DateTime.UtcNow.AddMinutes(-5), Title = "Build" }; t.Events.Add(item); t.Now = DateTime.UtcNow; Assert.AreEqual("5m", item.DisplayTime); Assert.IsFalse(string.IsNullOrWhiteSpace(item.FullTime)); });
    [TestMethod] public void FileDropzoneHonorsLimits() => Sta.Run(() => { var d = new FileDropzone { MaxFiles = 2, Multiple = true }; Assert.AreEqual(2, d.MaxFiles); Assert.IsTrue(d.AllowDrop); });
    [TestMethod] public void FileDropzoneItemPublishesStatusChanges() => Sta.Run(() => { var item = new FileDropzoneItem(); var changed = false; item.PropertyChanged += (_, e) => changed |= e.PropertyName == nameof(FileDropzoneItem.Status); item.Status = "failed"; Assert.IsTrue(changed); });
    [TestMethod] public void FileDropzoneExposesArcLayoutOptions() => Sta.Run(() => { var d = new FileDropzone { ListPlacement = "inside", CompactAt = 1, Note = "PNG or PDF", DropLabel = "Drop to upload", MaxSize = 1024 }; d.Items.Add(new FileDropzoneItem { Name = "a.pdf", Size = 12 }); Assert.IsTrue(d.IsListInside); Assert.IsTrue(d.IsCompact); Assert.AreEqual("12 B", d.Items[0].FormattedSize); Assert.AreEqual("1 KB", FileDropzone.FormatFileSize(1024)); });
    [TestMethod] public void AvatarUploadHasFallbackInitials() => Sta.Run(() => { var a = new AvatarUpload { Initials = "AW" }; Assert.AreEqual("AW", a.Initials); });
    [TestMethod] public void AvatarUploadClampsProgress() => Sta.Run(() => { var a = new AvatarUpload { Progress = 140 }; Assert.AreEqual(100d, a.Progress); a.Progress = -2; Assert.AreEqual(0d, a.Progress); Assert.IsTrue(a.AllowDrop); });
    [TestMethod] public void MentionInputRetainsNativeTextEditing() => Sta.Run(() => { var input = new MentionInput { Text = "@Ada" }; Assert.AreEqual("@Ada", input.Text); Assert.IsTrue(input.AcceptsReturn); });
    [TestMethod] public void MentionInputExposesStructuredTokens() => Sta.Run(() => { var input = new MentionInput(); var token = new MentionToken { Kind = MentionKind.Person, Id = "u1", Label = "Ada", Start = 0, End = 4 }; input.Mentions.Add(token); Assert.AreEqual("@Ada", token.Text); Assert.AreEqual("u1", input.Mentions[0].Id); });
    [TestMethod] public void EmojiPickerHasUnicodeEmoji15Palette() => Sta.Run(() => { var picker = new EmojiPicker(); Assert.IsGreaterThan(1800, picker.Emojis.Count); Assert.Contains("😀", picker.Emojis); });
    [TestMethod] public void EmojiPickerTracksRecentLimit() => Sta.Run(() => { var picker = new EmojiPicker { MaxRecent = 1 }; picker.SelectEmoji("😀"); picker.SelectEmoji("🎉"); Assert.HasCount(1, picker.RecentEmojis); Assert.AreEqual("🎉", picker.RecentEmojis[0]); });
    [TestMethod] public void EmojiPickerExposesCategorySelection() => Sta.Run(() => { var picker = new EmojiPicker { SelectedCategory = "Symbols" }; Assert.AreEqual("Symbols", picker.SelectedCategory); });
    [TestMethod] public void EmojiPickerClampsSkinToneAndAppliesIt() => Sta.Run(() => { var picker = new EmojiPicker { SkinTone = 9 }; Assert.AreEqual(5, picker.SkinTone); picker.SelectEmoji("👍"); Assert.AreEqual("👍🏿", picker.SelectedEmoji); picker.SkinTone = -1; Assert.AreEqual(0, picker.SkinTone); });
}
