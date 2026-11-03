using System.Windows;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class NextArcControlsPixelTests
{
    [TestMethod] public void CounterRowIsOneEmTallWithDigitsCentred() => Sta.Run(() =>
    {
        var counter = new AnimatedCounter { Label = "Total", Value = 12840, FontFamily = new FontFamily("Arial"), FontSize = 36, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = counter, Width = 400, Height = 200, Left = -4000, Top = -4000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        window.Show();
        counter.UpdateLayout();
        var slot = (FrameworkElement)counter.Template.FindName("PART_Digits", counter);
        Assert.AreEqual(36d, slot.ActualHeight);
        Assert.AreEqual(16.8 + 8 + 36, counter.ActualHeight, .01);
        var number = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetChild(slot, 0);
        Assert.AreEqual(slot.ActualHeight / 2, number.TranslatePoint(new Point(0, number.ActualHeight / 2), slot).Y, .01);
        window.Close();
    });

    [TestMethod] public void BarReportsOpenChanges() => Sta.Run(() =>
    {
        var bar = new AnnouncementBar();
        int changes = 0;
        bar.IsOpenChanged += (_, _) => changes++;
        bar.Close();
        bar.IsOpen = true;
        Assert.AreEqual(2, changes);
    });

    [TestMethod] public void CountdownCompletesWithAnInjectedClock() => Sta.Run(() =>
    {
        var now = new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);
        var countdown = new Countdown { Clock = () => now, Target = now.AddSeconds(90).UtcDateTime };
        Assert.IsFalse(countdown.IsComplete);
        now = now.AddSeconds(91);
        countdown.Target = countdown.Target;
        countdown.TargetText = "2026-11-10T18:00:00Z";
        Assert.IsTrue(countdown.IsComplete);
        Assert.AreEqual(TimeSpan.Zero, countdown.Remaining);
    });

    [TestMethod] public void TimelineKeepsExplicitExpansionWithoutDefaults() => Sta.Run(() =>
    {
        var timeline = new Timeline();
        var item = new TimelineEvent { Id = "a", Title = "Build", IsExpanded = true };
        timeline.Events.Add(item);
        timeline.DefaultExpanded = [];
        Assert.IsTrue(item.IsExpanded);
        timeline.DefaultExpanded = ["b"];
        var next = new TimelineEvent { Id = "b", Title = "Deploy" };
        timeline.Events.Add(next);
        Assert.IsTrue(next.IsExpanded);
    });

    [TestMethod] public void TimelineUsesClockTimeOutsideTheRelativeWindow() => Sta.Run(() =>
    {
        var now = new DateTime(2026, 9, 21, 14, 32, 0, DateTimeKind.Utc);
        var timeline = new Timeline { TimeZoneId = "UTC", Now = now };
        var recent = new TimelineEvent { At = now.AddMinutes(-11), Title = "a" };
        var hours = new TimelineEvent { At = now.AddHours(-5), Title = "b" };
        var older = new TimelineEvent { At = new DateTime(2026, 9, 18, 17, 40, 0, DateTimeKind.Utc), Title = "c", Detail = new System.Windows.Controls.TextBlock() };
        timeline.Events.Add(recent);
        timeline.Events.Add(hours);
        timeline.Events.Add(older);
        Assert.AreEqual("11m", recent.DisplayTime);
        Assert.AreEqual("5h", hours.DisplayTime);
        Assert.AreEqual("5:40 PM", older.DisplayTime);
    });

    [TestMethod] public void TimelineEventPublishesRichContent() => Sta.Run(() =>
    {
        var item = new TimelineEvent();
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        item.Avatar = new DrawingImage();
        item.Icon = "M20,6 L9,17 L4,12";
        item.Detail = "Reviewed";
        CollectionAssert.IsSubsetOf(new[] { nameof(TimelineEvent.Avatar), nameof(TimelineEvent.Icon), nameof(TimelineEvent.Detail) }, changed);
    });

    [TestMethod] public void AnnouncementAdvancesAndWrapsBothWays() => Sta.Run(() =>
    {
        var bar = new AnnouncementBar();
        bar.Announcements.Add(new AnnouncementItem { Id = "a", Message = "One" });
        bar.Announcements.Add(new AnnouncementItem { Id = "b", Message = "Two", CountdownTarget = DateTimeOffset.UtcNow.AddHours(1), CountdownLabel = "Ends in" });
        bar.Next();
        Assert.AreEqual(1, bar.Index);
        bar.Next();
        Assert.AreEqual(0, bar.Index);
        bar.Previous();
        Assert.AreEqual(1, bar.Index);
        Assert.IsFalse(bar.Controls);
    });

    [TestMethod] public void AnimatedCounterFormatsBeforeItsTemplateExists() => Sta.Run(() =>
    {
        var counter = new AnimatedCounter { Value = 12840, Label = "Total responses" };
        Assert.AreEqual("Total responses: 12,840", System.Windows.Automation.AutomationProperties.GetName(counter));
    });

    [TestMethod] public void MentionTriggerOpensWithoutInsertingASuggestion() => Sta.Run(() =>
    {
        var input = new MentionInput { Text = "Ping" };
        input.People.Add(new MentionPerson { Id = "emma", Name = "Emma Collins", Role = "Product designer" });
        input.CaretIndex = 4;
        input.InsertTrigger('@');
        Assert.AreEqual("Ping @", input.Text);
        Assert.IsEmpty(input.Mentions);
    });

    [TestMethod] public void FileDropzoneItemDescribesItsState() => Sta.Run(() =>
    {
        var failed = new FileDropzoneItem { Name = "launch-video.mov", Status = "failed", Error = "Connection lost" };
        Assert.AreEqual("Connection lost", failed.StatusText);
        Assert.AreNotEqual(new FileDropzoneItem { Name = "brief.pdf" }.IconData, failed.IconData);
        Assert.IsNull(failed.Thumbnail);
        var photo = new DrawingImage();
        Assert.AreSame(photo, new FileDropzoneItem { PreviewSource = photo, Preview = "x.png" }.Thumbnail);
        Assert.AreEqual("12 MB", FileDropzone.FormatFileSize(12 * 1048576 + 400));
        Assert.AreEqual("1.8 MB", FileDropzone.FormatFileSize((long)(1.8 * 1048576)));
    });

    [TestMethod] public void AvatarUploadRemovesItsPhotoAndFlagsErrors() => Sta.Run(() =>
    {
        var avatar = new AvatarUpload { ImageSource = new DrawingImage() };
        bool removed = false;
        avatar.Removed += (_, _) => removed = true;
        avatar.Error = "Too large";
        Assert.IsTrue(avatar.HasError);
        avatar.RemoveImage();
        Assert.IsTrue(removed);
        Assert.IsNull(avatar.ImageSource);
        Assert.IsFalse(avatar.HasError);
    });

    [TestMethod] public void EmojiPickerUsesArcGridAndPopularSet() => Sta.Run(() =>
    {
        var picker = new EmojiPicker();
        Assert.AreEqual(9, picker.Columns);
        Assert.AreEqual(27, picker.MaxRecent);
        Assert.HasCount(18, picker.DefaultRecent);
        Assert.AreEqual("👍", picker.DefaultRecent[0]);
        picker.IsOpen = true;
        picker.SelectEmoji("🎉");
        Assert.IsFalse(picker.IsOpen);
        Assert.AreEqual("🎉", picker.RecentEmojis[0]);
    });
}

[TestClass]
public sealed class ColorEmojiTablesTests
{
    private static byte[] Be(params int[] words) => words.SelectMany(word => new[] { (byte)(word >> 8), (byte)word }).ToArray();

    // Base glyph 5 has two layers: glyph 6 in palette 0, glyph 7 in the text colour.
    private static byte[] Colr(int version = 0) => Be(version, 1, 0, 14, 0, 20, 2, 5, 0, 2, 6, 0, 7, 0xFFFF);

    // One palette with one colour, stored blue, green, red, alpha.
    private static byte[] Cpal() => [.. Be(0, 1, 1, 1, 0, 14, 0), 0x10, 0x20, 0x30, 0x40];

    // One lookup, coverage format 1 on glyph 5: 5 + 8 + 9 → 20 and 5 + 8 → 21, listed shortest first.
    private static byte[] Gsub() => Be(1, 0, 0, 0, 10, 1, 4, 4, 0, 1, 8, 1, 8, 1, 14, 1, 1, 5, 2, 6, 12, 21, 2, 8, 20, 3, 8, 9);

    // 200 units per em, short offsets. Glyph 1 is a 100 unit square, 2 places it at (50, 20), 3 is four control points with no on-curve point, 4 is a lone anchor point.
    private static readonly (string, byte[])[] Outlines =
    [
        ("head", Be([.. new int[9], 200, .. new int[15], 0])),
        ("maxp", Be(0, 0x5000, 5)),
        ("loca", Be(0, 0, 17, 26, 43, 53)),
        ("glyf", Be(
            1, 0, 0, 100, 100, 3, 0, 0x0101, 0x0101, 0, 100, 0, -100, 0, 0, 100, 0,
            -1, 0, 0, 0, 0, 3, 1, 50, 20,
            1, 0, 0, 0, 0, 3, 0, 0, 0, 0, 100, 0, -100, 0, 0, 100, 0,
            1, 0, 0, 0, 0, 0, 0, 0x0100, 0, 0)),
    ];

    private static byte[] Font(params (string Tag, byte[] Data)[] colorTables)
    {
        (string Tag, byte[] Data)[] tables = [.. colorTables, .. Outlines];
        var output = new List<byte>(Be(0, 1, tables.Length, 0, 0, 0));
        int offset = 12 + tables.Length * 16;
        foreach (var (tag, data) in tables)
        {
            output.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
            output.AddRange(Be(0, 0, offset >> 16, offset, data.Length >> 16, data.Length));
            offset += data.Length;
        }
        foreach (var (_, data) in tables) output.AddRange(data);
        return [.. output];
    }

    [TestMethod] public void ReadsLayersAndPalette()
    {
        var tables = ColorEmojiTables.Parse(Font(("COLR", Colr()), ("CPAL", Cpal())))!;
        Assert.IsTrue(tables.TryGetLayers(5, out var layers));
        Assert.HasCount(2, layers);
        Assert.AreEqual((ushort)6, layers[0].Glyph);
        Assert.AreEqual(Color.FromArgb(0x40, 0x30, 0x20, 0x10), tables.PaletteColor(layers[0].Palette));
        Assert.IsNull(tables.PaletteColor(layers[1].Palette));
        Assert.IsFalse(tables.TryGetLayers(6, out _));
    }

    [TestMethod] public void ReadsTheFallbackListOfAVersionOneTable() => Assert.IsTrue(ColorEmojiTables.Parse(Font(("COLR", Colr(1)), ("CPAL", Cpal())))!.TryGetLayers(5, out _));

    [TestMethod] public void PrefersTheLongestLigature()
    {
        var tables = ColorEmojiTables.Parse(Font(("COLR", Colr()), ("CPAL", Cpal()), ("GSUB", Gsub())))!;
        CollectionAssert.AreEqual(new ushort[] { 20 }, tables.Shape([5, 8, 9]));
        CollectionAssert.AreEqual(new ushort[] { 21 }, tables.Shape([5, 8]));
        CollectionAssert.AreEqual(new ushort[] { 1, 21, 2 }, tables.Shape([1, 5, 8, 2]));
        CollectionAssert.AreEqual(new ushort[] { 5 }, tables.Shape([5]));
    }

    [TestMethod] public void RejectsFontsWithoutUsableColorTables()
    {
        Assert.IsNull(ColorEmojiTables.Parse(Font(("CPAL", Cpal()))));
        Assert.IsNull(ColorEmojiTables.Parse([.. Font(("COLR", Colr()), ("CPAL", Cpal())).Take(40)]));
        Assert.IsNull(ColorEmojiTables.Parse(Font(("COLR", Colr()[..12]), ("CPAL", Cpal()))));
        Assert.IsNull(ColorEmojiTables.Parse([1, 2, 3]));
    }

    [TestMethod] public void BuildsOutlinesFromTheGlyphTable() => Sta.Run(() =>
    {
        var tables = ColorEmojiTables.Parse(Font(("COLR", Colr()), ("CPAL", Cpal())))!;
        Assert.IsNull(tables.Outline(0));
        Assert.IsNull(tables.Outline(4));
        Assert.IsNull(tables.Outline(9));
        Assert.AreEqual(new Rect(0, -128, 128, 128), tables.Outline(1)!.Bounds);
        Assert.AreEqual(new Rect(64, -153.6, 128, 128), Rounded(tables.Outline(2)!.Bounds));
        var curved = tables.Outline(3)!.Bounds;
        Assert.AreEqual(128, curved.Width, .5);
        Assert.AreEqual(128, curved.Height, .5);
    });

    private static Rect Rounded(Rect rect) => new(Math.Round(rect.X, 3), Math.Round(rect.Y, 3), Math.Round(rect.Width, 3), Math.Round(rect.Height, 3));

    [TestMethod] public void CatalogFollowsArcOrder()
    {
        var items = EmojiCatalog.Items;
        Assert.HasCount(1870, items);
        Assert.AreEqual("😀", items[0].Emoji);
        Assert.AreEqual("Animals", items.Single(item => item.Emoji == "🦀").Group);
        var flags = items.Where(item => item.Group == "Flags").Select(item => item.Emoji).ToArray();
        Assert.AreEqual("🏁", flags[0]);
        Assert.HasCount(269, flags);
        CollectionAssert.AreEqual(new[] { "👥", "🫂", "👪", "👣" }, items.SkipWhile(item => item.Emoji != "👥").Take(4).Select(item => item.Emoji).ToArray());
    }

    [TestMethod] public void GlyphFallsBackWithoutAColorFont() => Sta.Run(() =>
    {
        Assert.IsNull(ColorEmojiFont.For(new FontFamily("Arial")));
        var glyph = new ColorEmojiGlyph { Text = "👍", EmojiSize = 20 };
        glyph.Measure(new Size(100, 100));
        Assert.AreEqual(25d, glyph.DesiredSize.Width);
    });
}
