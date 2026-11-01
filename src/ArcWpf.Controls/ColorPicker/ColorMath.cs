using System.Globalization;
using System.Text.RegularExpressions;

namespace ArcWpf.Controls;

public enum ColorFormat { Hex, Rgb, Hsl, Oklch }

/// <summary>A saved swatch.</summary>
public sealed record ColorSwatch(string Id, string Color);

/// <summary>Hue in degrees, saturation, value, and alpha in 0–1.</summary>
public readonly record struct Hsva(double H, double S, double V, double A);

/// <summary>sRGB channels in 0–1.</summary>
public readonly record struct Rgba(double R, double G, double B, double A);

/// <summary>The registry's color math (color-picker.tsx, frozen 2026-10-31): hex, rgb(), hsl(), and oklch() in modern or comma syntax.</summary>
public static class ColorMath
{
    private static double Clamp(double value, double min = 0, double max = 1) => Math.Min(max, Math.Max(min, value));
    private static double Round(double value, int digits = 0) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static Rgba HsvToRgb(Hsva c)
    {
        double F(double n) { double k = (n + c.H / 60) % 6; return c.V - c.V * c.S * Math.Max(0, Math.Min(Math.Min(k, 4 - k), 1)); }
        return new(F(5), F(3), F(1), c.A);
    }

    public static Hsva RgbToHsv(Rgba c, double hue = 0)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B)), d = max - min;
        double h = hue;
        if (d > 1e-6)
        {
            if (max == c.R) h = 60 * ((c.G - c.B) / d % 6);
            else if (max == c.G) h = 60 * ((c.B - c.R) / d + 2);
            else h = 60 * ((c.R - c.G) / d + 4);
            if (h < 0) h += 360;
        }
        return new(h, max == 0 ? 0 : d / max, max, c.A);
    }

    public static Rgba HslToRgb(double h, double s, double l, double a)
    {
        double K(double n) => (n + h / 30) % 12;
        double chroma = s * Math.Min(l, 1 - l);
        double F(double n) => l - chroma * Math.Max(-1, Math.Min(Math.Min(K(n) - 3, 9 - K(n)), 1));
        return new(F(0), F(8), F(4), a);
    }

    private static (double H, double S, double L) RgbToHsl(Rgba c, double hue)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B)), l = (max + min) / 2, d = max - min;
        double s = d < 1e-6 ? 0 : d / (1 - Math.Abs(2 * l - 1));
        return (d < 1e-6 ? hue : RgbToHsv(c with { A = 1 }).H, s, l);
    }

    public static double ToLinear(double c) => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
    private static double FromLinear(double c) => c <= .0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - .055;

    private static (double L, double C, double H) RgbToOklch(Rgba c, double hue)
    {
        double lr = ToLinear(c.R), lg = ToLinear(c.G), lb = ToLinear(c.B);
        double l = Math.Cbrt(.4122214708 * lr + .5363325363 * lg + .0514459929 * lb);
        double m = Math.Cbrt(.2119034982 * lr + .6806995451 * lg + .1073969566 * lb);
        double s = Math.Cbrt(.0883024619 * lr + .2817188376 * lg + .6299787005 * lb);
        double L = .2104542553 * l + .793617785 * m - .0040720468 * s;
        double A = 1.9779984951 * l - 2.428592205 * m + .4505937099 * s;
        double B = .0259040371 * l + .7827717662 * m - .808675766 * s;
        double C = Math.Sqrt(A * A + B * B);
        double H = Math.Atan2(B, A) * 180 / Math.PI;
        if (H < 0) H += 360;
        return (L, C, C < .0005 ? hue : H);
    }

    private static Rgba OklchToRgb(double L, double C, double H, double a)
    {
        double A = C * Math.Cos(H * Math.PI / 180), B = C * Math.Sin(H * Math.PI / 180);
        double l = Math.Pow(L + .3963377774 * A + .2158037573 * B, 3);
        double m = Math.Pow(L - .1055613458 * A - .0638541728 * B, 3);
        double s = Math.Pow(L - .0894841775 * A - 1.291485548 * B, 3);
        return new(Clamp(FromLinear(4.0767416621 * l - 3.3077115913 * m + .2309699292 * s)), Clamp(FromLinear(-1.2684380046 * l + 2.6097574011 * m - .3413193965 * s)), Clamp(FromLinear(-.0041960863 * l - .7034186147 * m + 1.707614701 * s)), a);
    }

    public static byte Byte(double c) => (byte)Math.Round(Clamp(c) * 255, MidpointRounding.AwayFromZero);
    private static string Hex2(double c) => Byte(c).ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>"#2F6BFF", with two alpha digits when the color is not opaque.</summary>
    public static string ToHex(Hsva c)
    {
        var rgb = HsvToRgb(c);
        return $"#{Hex2(rgb.R)}{Hex2(rgb.G)}{Hex2(rgb.B)}{(c.A < .999 ? Hex2(c.A) : "")}";
    }

    public static string Format(Hsva c, ColorFormat kind)
    {
        var rgb = HsvToRgb(c);
        string alpha = c.A < .999 ? $" / {Math.Round(c.A * 100, MidpointRounding.AwayFromZero)}%" : "";
        switch (kind)
        {
            case ColorFormat.Hex: return ToHex(c);
            case ColorFormat.Rgb: return $"rgb({Byte(rgb.R)} {Byte(rgb.G)} {Byte(rgb.B)}{alpha})";
            case ColorFormat.Hsl:
                var (h, s, l) = RgbToHsl(rgb, c.H);
                return $"hsl({Round(h) % 360} {Round(s * 100)}% {Round(l * 100)}%{alpha})";
            default:
                var (L, C, H) = RgbToOklch(rgb, c.H);
                return $"oklch({N(Round(L * 100, 1))}% {N(Round(C, 3))} {N(Round(H, 1) % 360)}{alpha})";
        }
    }

    private static double ReadAlpha(Group group) => !group.Success ? 1 : group.Value.EndsWith('%') ? Clamp(double.Parse(group.Value.TrimEnd('%'), CultureInfo.InvariantCulture) / 100) : Clamp(double.Parse(group.Value, CultureInfo.InvariantCulture));
    private static double D(Group group) => double.Parse(group.Value, CultureInfo.InvariantCulture);

    /// <summary>Reads hex, rgb(), hsl(), and oklch() in modern or comma syntax. Returns null for anything else.</summary>
    public static Hsva? Parse(string? input, double hue = 0)
    {
        string text = (input ?? "").Trim().ToLowerInvariant();
        try
        {
            var match = Regex.Match(text, "^#?([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$");
            if (match.Success)
            {
                string digits = match.Groups[1].Value;
                if (digits.Length <= 4) digits = string.Concat(digits.Select(d => $"{d}{d}"));
                double Nib(int i) => int.Parse(digits.Substring(i, 2), NumberStyles.HexNumber) / 255d;
                return RgbToHsv(new(Nib(0), Nib(2), Nib(4), digits.Length == 8 ? Nib(6) : 1), hue);
            }
            match = Regex.Match(text, @"^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:\s*[/,]\s*([\d.]+%?))?\s*\)$");
            if (match.Success) return RgbToHsv(new(Clamp(D(match.Groups[1]) / 255), Clamp(D(match.Groups[2]) / 255), Clamp(D(match.Groups[3]) / 255), ReadAlpha(match.Groups[4])), hue);
            match = Regex.Match(text, @"^hsla?\(\s*([\d.]+)(?:deg)?[\s,]+([\d.]+)%?[\s,]+([\d.]+)%?(?:\s*[/,]\s*([\d.]+%?))?\s*\)$");
            if (match.Success)
            {
                double h = D(match.Groups[1]) % 360;
                var next = RgbToHsv(HslToRgb(h, Clamp(D(match.Groups[2]) / 100), Clamp(D(match.Groups[3]) / 100), ReadAlpha(match.Groups[4])), h);
                return next with { H = h };
            }
            match = Regex.Match(text, @"^oklch\(\s*([\d.]+)(%?)\s+([\d.]+)\s+([\d.]+)(?:deg)?(?:\s*\/\s*([\d.]+%?))?\s*\)$");
            if (match.Success)
            {
                double L = match.Groups[2].Value.Length > 0 ? D(match.Groups[1]) / 100 : D(match.Groups[1]);
                return RgbToHsv(OklchToRgb(Clamp(L), D(match.Groups[3]), D(match.Groups[4]), ReadAlpha(match.Groups[5])), hue);
            }
        }
        catch (FormatException) { }
        catch (OverflowException) { }
        return null;
    }

    /// <summary>WCAG contrast of a color, composited over the background, against that background.</summary>
    public static double Contrast(Hsva color, Rgba background)
    {
        var fg = HsvToRgb(color);
        double Mix(double c, double bg) => c * fg.A + bg * (1 - fg.A);
        static double Lum(double r, double g, double b) => .2126 * ToLinear(r) + .7152 * ToLinear(g) + .0722 * ToLinear(b);
        double a = Lum(Mix(fg.R, background.R), Mix(fg.G, background.G), Mix(fg.B, background.B)), b = Lum(background.R, background.G, background.B);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    public static string Grade(double ratio) => ratio >= 7 ? "AAA" : ratio >= 4.5 ? "AA" : ratio >= 3 ? "AA large" : "Fails";

    public static string FormatName(ColorFormat kind) => kind switch { ColorFormat.Hex => "Hex", ColorFormat.Rgb => "RGB", ColorFormat.Hsl => "HSL", _ => "OKLCH" };

    public static System.Windows.Media.Color ToColor(Hsva c, bool opaque = false)
    {
        var rgb = HsvToRgb(c);
        return System.Windows.Media.Color.FromArgb(opaque ? (byte)255 : Byte(c.A), Byte(rgb.R), Byte(rgb.G), Byte(rgb.B));
    }
}
