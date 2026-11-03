using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf;

/// <summary>Arc's initial semantic palette. Put application overrides in the containing dictionary.</summary>
public sealed class ArcThemeResources : ResourceDictionary
{
    private ArcThemeMode _mode;

    public ArcThemeResources()
    {
        // ScrollBar's default style key lives in PresentationFramework, so Generic.xaml cannot restyle it; an implicit style here reaches every control.
        MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/ArcWpf.Core;component/Themes/ScrollBar.xaml", UriKind.Relative)));
        Rebuild();
    }

    public ArcThemeMode Mode
    {
        get => _mode;
        set
        {
            if (!Enum.IsDefined(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            if (_mode == value) { return; }
            _mode = value;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        bool dark = Mode == ArcThemeMode.Dark;
        // Values are from the frozen Arc foundation.css (see THIRD-PARTY-NOTICES.md).
        var neutrals = new Dictionary<string, double>
        {
            ["Background"] = dark ? .19 : 1, ["Foreground"] = dark ? .96 : .15,
            ["Surface"] = dark ? .215 : 1, ["SurfaceRaised"] = dark ? .24 : 1,
            ["SurfaceMuted"] = dark ? .26 : .978, ["TextSecondary"] = dark ? .77 : .46,
            ["TextMuted"] = dark ? .64 : .59, ["Border"] = dark ? .29 : .935,
            ["BorderStrong"] = dark ? .40 : .82, ["Focus"] = dark ? .96 : .15,
            ["ControlOn"] = dark ? .93 : .23, ["ControlTrack"] = dark ? .33 : .895,
            ["ControlTrackHover"] = dark ? .37 : .86, ["ControlThumb"] = dark ? .97 : 1,
            ["ControlThumbOn"] = dark ? .19 : 1,
            ["ControlGlyph"] = dark ? .19 : 1, ["AccentStrong"] = dark ? .91 : .24
        };
        foreach (var pair in neutrals) { PutBrush(pair.Key, Oklch(pair.Value, 0, 0)); }
        // Slider-specific tokens from the frozen 2026-10-26 foundation; existing controls keep their palette.
        PutBrush("ControlFill", Oklch(dark ? .62 : .23, 0, 0));
        var sliderBorder = Colors.Black; sliderBorder.A = (byte)Math.Round(255 * (dark ? .3 : .07)); PutBrush("SliderThumbBorder", sliderBorder);
        var sliderShadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Direction = 270, Opacity = dark ? .35 : .14 };
        sliderShadow.Freeze(); this["ArcWpf.Shadow.SliderThumb"] = sliderShadow;
        var overlayColor = Oklch(.1, 0, 0); overlayColor.A = (byte)Math.Round(255 * .46); PutBrush("DialogOverlay", overlayColor);
        var closeColor = Oklch(neutrals["SurfaceMuted"], 0, 0); closeColor.A = 128; PutBrush("DialogClose", closeColor);
        // ButtonGroup color-mix(in oklab) of achromatic neutrals reduces to mixing L (frozen 2026-10-28 button-group CSS).
        double MixL(string a, string b, double share) => neutrals[a] * (1 - share) + neutrals[b] * share;
        PutBrush("ButtonGroupHighlightPressed", Oklch(MixL("SurfaceMuted", "BorderStrong", .3), 0, 0));
        PutBrush("ButtonGroupSolidHighlight", Oklch(MixL("Foreground", "Background", .13), 0, 0));
        PutBrush("ButtonGroupSolidHighlightPressed", Oklch(MixL("Foreground", "Background", .22), 0, 0));
        PutBrush("ButtonGroupSolidDivider", Oklch(MixL("Foreground", "Background", .2), 0, 0));
        PutBrush("ButtonGroupSolidMutedInk", Oklch(MixL("Foreground", "Background", .72), 0, 0));
        // split-button.module.css primary variant: hover foreground 88%, pressed/open 80%, trigger border-left background 18%.
        PutBrush("SplitButtonHover", Oklch(MixL("Foreground", "Background", .12), 0, 0));
        PutBrush("SplitButtonPressed", Oklch(MixL("Foreground", "Background", .2), 0, 0));
        PutBrush("SplitButtonDivider", Oklch(MixL("Foreground", "Background", .18), 0, 0));
        // skeleton.module.css: color-mix(in oklab, var(--border-strong) 38%, var(--surface)).
        PutBrush("SkeletonFill", Oklch(MixL("Surface", "BorderStrong", .38), 0, 0));
        PutBrush("TooltipBorder",Oklch(neutrals["Background"] * .14 + neutrals["Foreground"] * .86, 0, 0));
        var raised = new DropShadowEffect { Color=Colors.Black, BlurRadius=dark?22:18, ShadowDepth=dark?8:6, Direction=270, Opacity=dark?.22:.065 }; raised.Freeze(); this["ArcWpf.Shadow.Raised"]=raised;
        PutBrush("ToastSuccess", Oklch(dark ? .35 : .92, 0, 0)); PutBrush("ToastSuccessBorder", Oklch(dark ? .55 : .72, 0, 0)); PutBrush("ToastSuccessGlyph", Oklch(dark ? .82 : .32, 0, 0));
        double dangerL = dark ? .76 : .54;
        double dangerC = dark ? .2 : .21;
        PutBrush("Danger", Oklch(dangerL, dangerC, 25));
        // OTP input: frozen 2026-10-27 foundation accent and srgb danger/surface mix.
        var otpAccent = Oklch(dark ? .8 : .33, 0, 0); PutBrush("Accent", otpAccent);
        otpAccent.A = (byte)Math.Round(255 * (dark ? .13 : .1)); PutBrush("AccentSubtle", otpAccent);
        otpAccent.A = (byte)Math.Round(255 * .11); PutBrush("UndoFlash", otpAccent);
        var otpDanger = Oklch(dangerL, dangerC, 25); var otpSurface = Oklch(neutrals["Surface"], 0, 0);
        PutBrush("OTPErrorSurface", Color.FromRgb((byte)Math.Round(otpDanger.R*.07+otpSurface.R*.93), (byte)Math.Round(otpDanger.G*.07+otpSurface.G*.93), (byte)Math.Round(otpDanger.B*.07+otpSurface.B*.93)));
        // dropdown-menu.module.css .highlight[data-tone="danger"]: danger 8% over surface (sRGB approximation of the oklab mix).
        PutBrush("MenuDangerHighlight", Color.FromRgb((byte)Math.Round(otpDanger.R*.08+otpSurface.R*.92), (byte)Math.Round(otpDanger.G*.08+otpSurface.G*.92), (byte)Math.Round(otpDanger.B*.08+otpSurface.B*.92)));
        PutBrush("AlertSuccess", Oklch(dark ? .72 : .42, dark ? .14 : .16, 145));
        PutBrush("AlertWarning", Oklch(dark ? .82 : .54, dark ? .16 : .18, 82));
        // color-mix(in oklab, danger 55%, surface-muted), with a neutral second color.
        PutBrush("DangerDisabled", Oklch(dangerL * .55 + neutrals["SurfaceMuted"] * .45, dangerC * .55, 25));
        // Badge CSS from the frozen 2026-10-27 registry. Mix with neutral surfaces in OKLab.
        foreach (var tone in new[] { "Neutral", "Success", "Info", "Warning", "Danger" })
        {
            var (l, c, h, backgroundMix, borderMix) = tone switch
            {
                "Success" => (dark ? .78 : .49, dark ? .18 : .19, 150d, .10, .25),
                "Info" => (dark ? .8 : .33, 0d, 0d, .10, .24),
                "Warning" => (dark ? .83 : .58, dark ? .17 : .18, dark ? 80d : 75d, .11, .27),
                "Danger" => (dangerL, dangerC, 25d, .10, .26),
                _ => (neutrals["TextSecondary"], 0d, 0d, 0d, 0d)
            };
            Color Mix(double amount, string neutral) => Oklch(l * amount + neutrals[neutral] * (1 - amount), c * amount, h);
            PutBrush($"Badge{tone}Foreground", tone == "Info" ? Oklch(neutrals["AccentStrong"], 0, 0) : Oklch(l, c, h));
            var badgeBackground = tone == "Neutral" ? Oklch(neutrals["SurfaceMuted"], 0, 0) : Mix(backgroundMix, "Surface");
            if (tone == "Info") { badgeBackground = Oklch(l, c, h); badgeBackground.A = (byte)Math.Round(255 * (dark ? .13 : .1)); }
            PutBrush($"Badge{tone}Background", badgeBackground);
            PutBrush($"Badge{tone}Border", Mix(borderMix, "Border"));
            PutBrush($"Badge{tone}HoverBackground", Mix(.1, "Surface"));
            PutBrush($"Badge{tone}HoverBorder", Mix(.32, "Border"));
        }
        // Avatar presence palette, frozen 2026-10-27 foundation/CSS.
        PutBrush("AvatarOnline", Oklch(dark ? .78 : .49, dark ? .18 : .19, 150));
        var avatarOutline = Oklch(neutrals["Border"], 0, 0); avatarOutline.A = (byte)Math.Round(255 * .7);
        PutBrush("AvatarStatusOutline", avatarOutline);
        // --success / --warning from the frozen foundation; UserMenu presence dots and Progress completion use them.
        PutBrush("Success", Oklch(dark ? .78 : .49, dark ? .18 : .19, 150));
        PutBrush("Warning", Oklch(dark ? .83 : .58, dark ? .17 : .18, dark ? 80 : 75));
        // user-menu.module.css: color-mix(in oklab, foreground N%, transparent) is the foreground at N% alpha.
        Color Ink(double share) { var ink = Oklch(neutrals["Foreground"], 0, 0); ink.A = (byte)Math.Round(255 * share); return ink; }
        PutBrush("UserMenuLine", Ink(.09));
        PutBrush("UserMenuHighlight", Ink(.06));
        PutBrush("UserMenuTrack", Ink(.05));
        PutBrush("UserMenuPlan", Ink(.04));
        // filter-toolbar.module.css: menu highlight foreground 6%; closed trigger press mixes surface-muted with border 55%.
        PutBrush("FilterMenuHighlight", Ink(.06));
        PutBrush("FilterMenuPressed", Oklch(MixL("SurfaceMuted", "Border", .55), 0, 0));
        PutBrush("UserMenuTriggerPressed", Oklch(MixL("SurfaceMuted", "Foreground", .08), 0, 0));
        // .thumb is surface-raised in light; [data-theme="dark"] mixes foreground 20% into it and drops the shadow.
        PutBrush("UserMenuThumb", Oklch(dark ? MixL("SurfaceRaised", "Foreground", .2) : neutrals["SurfaceRaised"], 0, 0));
        this["ArcWpf.Radius.Control"] = new CornerRadius(18);
        this["ArcWpf.Radius.Panel"] = new CornerRadius(26);
        this["ArcWpf.Radius.Pill"] = new CornerRadius(9999);
        // Font binaries are not bundled yet; this explicit Windows fallback is documented.
        this["ArcWpf.Font.Body"] = new FontFamily("Segoe UI, Microsoft YaHei UI");
        this["ArcWpf.FontSize.Small"] = 14d;
        this["ArcWpf.FontSize.ExtraSmall"] = 12d;
        this["ArcWpf.ControlHeight.Small"] = 36d;
        this["ArcWpf.ControlHeight.Medium"] = 44d;
        this["ArcWpf.ControlHeight.Large"] = 50d;
        // 2026-10-30 frozen foundation: --border-subtle and the neutral --accent-foreground (= --background).
        PutBrush("BorderSubtle", Oklch(dark ? .25 : .965, 0, 0));
        PutBrush("AccentForeground", Oklch(neutrals["Background"], 0, 0));
        // scroll-area / date-range-picker / expanding-search color-mix(in oklab, foreground N%, transparent).
        PutBrush("ScrollAreaThumb", Ink(.30));
        PutBrush("ScrollAreaThumbHover", Ink(.46));
        PutBrush("RangeRailHighlight", Ink(.065));
        PutBrush("RangePresetHover", Ink(.04));
        PutBrush("RangeDayHover", Ink(.07));
        PutBrush("ExpandingSearchHighlight", Ink(.06));
        // date-range-picker trigger hover: color-mix(in oklab, foreground 3%, surface-raised).
        PutBrush("RangeTriggerHover", Oklch(MixL("SurfaceRaised", "Foreground", .03), 0, 0));
        // 2026-10-31 frozen input batch (rating, chip-group, tag-input, password-strength, shortcut-recorder, money-input,
        // phone-input, color-picker). color-mix(in oklab) of a chromatic token with a neutral keeps the hue and scales chroma.
        Color Alpha(Color color, double share) { color.A = (byte)Math.Round(255 * share); return color; }
        double accentL = dark ? .8 : .33;
        (double l, double c, double h) warning = (dark ? .83 : .58, dark ? .17 : .18, dark ? 80 : 75), success = (dark ? .78 : .49, dark ? .18 : .19, 150);
        Color Tint((double l, double c, double h) tone, double share, string neutral) => Oklch(tone.l * share + neutrals[neutral] * (1 - share), tone.c * share, tone.h);
        PutBrush("RatingKept", Alpha(Oklch(warning.l, warning.c, warning.h), .3));
        PutBrush("ChipSelectedBorder", Oklch(accentL * .42 + neutrals["Border"] * .58, 0, 0));
        PutBrush("ChipSelectedSurface", Oklch(accentL * .11 + neutrals["Surface"] * .89, 0, 0));
        PutBrush("ChipSelectedHoverBorder", Oklch(accentL * .6 + neutrals["Border"] * .4, 0, 0));
        PutBrush("ChipSelectedHoverSurface", Oklch(accentL * .16 + neutrals["Surface"] * .84, 0, 0));
        PutBrush("PasswordRuleFill", Alpha(Oklch(success.l, success.c, success.h), .16));
        PutBrush("KbdPressedBorder", Oklch(accentL * .55 + neutrals["BorderStrong"] * .45, 0, 0));
        PutBrush("KbdWarningBorder", Tint(warning, .55, "BorderStrong"));
        PutBrush("KbdWarningSurface", Tint(warning, .12, "SurfaceRaised"));
        PutBrush("RecorderSurface", Oklch(accentL * .04 + neutrals["Surface"] * .96, 0, 0));
        PutBrush("ShortcutSheet", Oklch(MixL("Surface", "SurfaceMuted", .45), 0, 0));
        PutBrush("MoneyGhost", Alpha(Oklch(neutrals["TextMuted"], 0, 0), .55));
        PutBrush("PhoneGuide", Alpha(Oklch(neutrals["TextMuted"], 0, 0), .72));
        PutBrush("FieldHighlight", Ink(.065));
        PutBrush("FieldClear", Ink(.08));
        PutBrush("FieldSelection", Ink(.16));
        PutBrush("ColorFormatHover", Ink(.05));
        PutBrush("ContrastPass", Alpha(Oklch(success.l, success.c, success.h), .14));
        PutBrush("ContrastLarge", Alpha(Oklch(warning.l, warning.c, warning.h), .16));
        PutBrush("ContrastFail", Alpha(Oklch(dangerL, dangerC, 25), .12));
        // usage-meter.module.css: ordered accent/surface blends, a neutral track, and status badges.
        double[] usageShares = [1, .62, .36, .22];
        for (int index = 0; index < usageShares.Length; index++)
            PutBrush($"UsageSegment{index}", Oklch(accentL * usageShares[index] + neutrals["Surface"] * (1 - usageShares[index]), 0, 0));
        PutBrush("UsageTrack", Oklch(MixL("Surface", "Foreground", .07), 0, 0));
        PutBrush("UsageNearBadge", Tint(warning, .14, "Surface"));
        PutBrush("UsageOverBadge", Tint((dangerL, dangerC, 25), .12, "Surface"));
        PutBrush("ChipListSurface", Alpha(Oklch(neutrals["Surface"], 0, 0), .55));
        // code-block.module.css token roles; Windows browsers resolve its mono stack to Consolas.
        this["ArcWpf.Font.Mono"] = new FontFamily("Consolas, Courier New, Microsoft YaHei UI");
        PutBrush("CodeString", dark ? Oklch(.77, .15, 150) : Oklch(.46, .13, 150));
        PutBrush("CodeNumber", dark ? Oklch(.8, .14, 75) : Oklch(.5, .14, 55));
        PutBrush("CodeKeyword", dark ? Oklch(.78, .16, 285) : Oklch(.48, .15, 285));
        PutBrush("CodeType", dark ? Oklch(.79, .13, 230) : Oklch(.46, .13, 230));
        PutBrush("CodeFunction", dark ? Oklch(.8, .13, 25) : Oklch(.43, .12, 25));
        PutBrush("CodeProperty", dark ? Oklch(.79, .12, 85) : Oklch(.44, .12, 85));
        // json-viewer.module.css uses the 2026-11-02 foundation roles, which moved on from the older Success/Warning above.
        (double l, double c, double h) dataSuccess = dark ? (.79, .14, 162) : (.5, .12, 162), dataWarning = dark ? (.83, .12, 78) : (.55, .12, 68);
        PutBrush("JsonString", Oklch(dataSuccess.l, dataSuccess.c, dataSuccess.h));
        PutBrush("JsonBoolean", Oklch(dataWarning.l, dataWarning.c, dataWarning.h));
        PutBrush("JsonMark", Alpha(Oklch(dataWarning.l, dataWarning.c, dataWarning.h), .22));
        PutBrush("JsonMarkCurrent", Alpha(Oklch(dataWarning.l, dataWarning.c, dataWarning.h), .48));
        PutBrush("JsonRowHover", Oklch(MixL("Surface", "Foreground", .035), 0, 0));
        // sortable-data-table.module.css: every fill is a mix over the cell base, which is surface or the sorted column tint.
        PutBrush("TableHover", Oklch(MixL("Surface", "Foreground", .03), 0, 0));
        PutBrush("TableHoverSorted", Oklch(MixL("SurfaceMuted", "Foreground", .03), 0, 0));
        PutBrush("TableSelected", Oklch(accentL * .08 + neutrals["Surface"] * .92, 0, 0));
        PutBrush("TableSelectedSorted", Oklch(accentL * .08 + neutrals["SurfaceMuted"] * .92, 0, 0));
        PutBrush("TableSelectedHover", Oklch(accentL * .11 + neutrals["Surface"] * .89, 0, 0));
        PutBrush("TableSelectedHoverSorted", Oklch(accentL * .11 + neutrals["SurfaceMuted"] * .89, 0, 0));
        // tree-table.module.css: accent-subtle with foreground 4%, and the chevron hover over surface-muted.
        PutBrush("TreeSelectedHover", Alpha(Oklch(accentL * .96 + neutrals["Foreground"] * .04, 0, 0), (dark ? .13 : .1) * .96 + .04));
        PutBrush("TreeChevronHover", Oklch(MixL("SurfaceMuted", "Foreground", .06), 0, 0));
        // The small status badge the 2026-11-02 data demos show: 20 high, radius 5, computed from uiarc.dev in both themes.
        PutBrush("StatusNeutralFill", Oklch(dark ? .2816 : .946, 0, 0));
        PutBrush("StatusNeutralInk", Oklch(dark ? .7814 : .4104, 0, 0));
        PutBrush("StatusInfoFill", dark ? Oklch(.284, .0114, 236) : Oklch(.951, .011, 240));
        PutBrush("StatusInfoInk", dark ? Oklch(.8002, .0893, 236) : Oklch(.4524, .0924, 240));
        PutBrush("StatusSuccessFill", dark ? Oklch(.284, .0168, 162) : Oklch(.95, .012, 162));
        PutBrush("StatusSuccessInk", dark ? Oklch(.8002, .1316, 162) : Oklch(.444, .1008, 162));
        PutBrush("StatusWarningFill", dark ? Oklch(.2888, .0144, 78) : Oklch(.955, .012, 68));
        PutBrush("StatusWarningInk", dark ? Oklch(.8378, .1128, 78) : Oklch(.454, .0912, 68));
        // toggle-group.module.css and action-swap.module.css, color-mix(in oklab) over the 2026-11-02 foundation roles.
        PutBrush("ToggleFill", Oklch(neutrals["ControlOn"] * (dark ? .14 : .11) + neutrals["Surface"] * (dark ? .86 : .89), 0, 0));
        (double l, double c, double h) dataDanger = dark ? (.71, .155, 8) : (.53, .18, 10);
        foreach (var (name, tone, fill, border, hover) in new[] { ("Success", dataSuccess, .1, .25, .42), ("Warning", dataWarning, .12, .3, .48), ("Danger", dataDanger, .1, .25, .42) })
        {
            PutBrush($"Swap{name}", Oklch(tone.l, tone.c, tone.h));
            PutBrush($"Swap{name}Fill", Tint(tone, fill, "Surface"));
            PutBrush($"Swap{name}Border", Tint(tone, border, "Border"));
            PutBrush($"Swap{name}HoverBorder", Tint(tone, hover, "Border"));
        }
        PutBrush("TimelineRing", Alpha(Oklch(neutrals["Foreground"], 0, 0), .1));
        var live = (l: dark ? .76 : .5, c: .12, h: 162d);
        PutBrush("LiveSuccess", Oklch(live.l, live.c, live.h));
        PutBrush("TimelineSuccess", Oklch(neutrals["Surface"] * .88 + live.l * .12, live.c * .12, live.h));
        PutBrush("TimelineDanger", Oklch(neutrals["Surface"] * .88 + dangerL * .12, dangerC * .12, 25));
        PutBrush("AnnouncementStageBorder", Oklch(dark ? neutrals["Border"] : .965, 0, 0));
        PutBrush("AnnouncementPageLine", Oklch(dark ? neutrals["SurfaceMuted"] : .978, 0, 0));
        PutBrush("FileDropzoneFailedBorder", Oklch(neutrals["Border"] * .74 + dangerL * .26, dangerC * .26, 25));
        PutBrush("MentionHighlight", Alpha(Oklch(neutrals["Foreground"], 0, 0), .065));
        PutBrush("MentionChannelToken", Alpha(Oklch(neutrals["Foreground"], 0, 0), .07));
        PutBrush("AvatarScrim", Alpha(Oklch(neutrals["Surface"], 0, 0), .62));
        PutBrush("ComposerSend", Oklch(dark ? .92 : .23, 0, 0));
        PutBrush("AnnouncementIconHover", Alpha(Oklch(neutrals["Foreground"], 0, 0), .1));
        PutBrush("AnnouncementInvertedHover", Alpha(Oklch(neutrals["Background"], 0, 0), .066));
        PutBrush("AnnouncementInvertedMuted", Alpha(Oklch(neutrals["Background"], 0, 0), .66));
        PutBrush("CountdownCompleteFill", Oklch(neutrals["Surface"] * .91 + live.l * .09, live.c * .09, live.h));
        PutBrush("CountdownCompleteBorder", Oklch(neutrals["Border"] * .65 + live.l * .35, live.c * .35, live.h));
        PutBrush("UndoRowActionHover", Alpha(Oklch(neutrals["Foreground"], 0, 0), .07));
        this["ArcWpf.Button.Padding.Small"] = new Thickness(12, 0, 12, 0);
        this["ArcWpf.Button.Padding.Medium"] = new Thickness(16, 0, 16, 0);
        this["ArcWpf.Button.Padding.Large"] = new Thickness(20, 0, 20, 0);
        var shadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Direction = 270, Opacity = dark ? .18 : .035 };
        shadow.Freeze();
        this["ArcWpf.Shadow.Resting"] = shadow;
        // Dominant floating-shadow layer from frozen foundation.css; WPF has a single DropShadowEffect.
        var floating = new DropShadowEffect { Color = Colors.Black, BlurRadius = dark ? 55 : 48,
            ShadowDepth = dark ? 22 : 20, Direction = 270, Opacity = dark ? .34 : .105 };
        floating.Freeze();
        this["ArcWpf.Shadow.Floating"] = floating;
    }

    private void PutBrush(string name, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        this[$"ArcWpf.Brush.{name}"] = brush;
    }

    // OKLCH -> linear sRGB -> encoded sRGB. Out-of-gamut channels are clipped for WPF's sRGB palette.
    private static Color Oklch(double lightness, double chroma, double hue)
    {
        double angle = hue * Math.PI / 180;
        double a = chroma * Math.Cos(angle), b = chroma * Math.Sin(angle);
        double l = Math.Pow(lightness + .3963377774 * a + .2158037573 * b, 3);
        double m = Math.Pow(lightness - .1055613458 * a - .0638541728 * b, 3);
        double s = Math.Pow(lightness - .0894841775 * a - 1.2914855480 * b, 3);
        static byte Encode(double c)
        {
            double encoded = c <= .0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - .055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }
        return Color.FromRgb(Encode(4.0767416621 * l - 3.3077115913 * m + .2309699292 * s),
            Encode(-1.2684380046 * l + 2.6097574011 * m - .3413193965 * s),
            Encode(-.0041960863 * l - .7034186147 * m + 1.7076147010 * s));
    }
}
