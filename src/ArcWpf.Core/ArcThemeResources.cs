using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ArcWpf;

/// <summary>Arc's initial semantic palette. Put application overrides in the containing dictionary.</summary>
public sealed class ArcThemeResources : ResourceDictionary
{
    private ArcThemeMode _mode;

    public ArcThemeResources() => Rebuild();

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
        var overlayColor = Oklch(.1, 0, 0); overlayColor.A = (byte)Math.Round(255 * .46); PutBrush("DialogOverlay", overlayColor);
        var closeColor = Oklch(neutrals["SurfaceMuted"], 0, 0); closeColor.A = 128; PutBrush("DialogClose", closeColor);
        PutBrush("TooltipBorder", Oklch(neutrals["Background"] * .14 + neutrals["Foreground"] * .86, 0, 0));
        var raised = new DropShadowEffect { Color=Colors.Black, BlurRadius=dark?22:18, ShadowDepth=dark?8:6, Direction=270, Opacity=dark?.22:.065 }; raised.Freeze(); this["ArcWpf.Shadow.Raised"]=raised;
        PutBrush("ToastSuccess", Oklch(dark ? .35 : .92, 0, 0)); PutBrush("ToastSuccessBorder", Oklch(dark ? .55 : .72, 0, 0)); PutBrush("ToastSuccessGlyph", Oklch(dark ? .82 : .32, 0, 0));
        double dangerL = dark ? .76 : .54;
        double dangerC = dark ? .2 : .21;
        PutBrush("Danger", Oklch(dangerL, dangerC, 25));
        PutBrush("AlertSuccess", Oklch(dark ? .72 : .42, dark ? .14 : .16, 145));
        PutBrush("AlertWarning", Oklch(dark ? .82 : .54, dark ? .16 : .18, 82));
        // color-mix(in oklab, danger 55%, surface-muted), with a neutral second color.
        PutBrush("DangerDisabled", Oklch(dangerL * .55 + neutrals["SurfaceMuted"] * .45, dangerC * .55, 25));
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





