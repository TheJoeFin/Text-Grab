using System.Drawing;

namespace Text_Grab.Core.Editing;

/// <summary>
/// Adapts syntax-highlighting colors made for a light background to a dark one.
/// </summary>
/// <remarks>
/// The code editor's built-in highlighting definitions choose dark, saturated colors for white
/// backgrounds (navy keywords, dark green comments, dark red strings), which are unreadable on Text
/// Grab's dark theme. Rather than keep a second palette per language, each color keeps its hue and
/// saturation and has its lightness flipped, then is lightened further until it reaches a readable
/// contrast against the dark background.
/// </remarks>
public static class CodeHighlightColors
{
    /// <summary>The dark editor background contrast is measured against (WPF-UI's dark base, #202020).</summary>
    public static readonly Color DarkBackground = Color.FromArgb(0x20, 0x20, 0x20);

    /// <summary>The contrast ratio a text color is lightened to reach (WCAG AA for body text).</summary>
    public const double MinimumContrast = 4.5;

    /// <summary>The lightest a background highlight is allowed to be on the dark theme.</summary>
    public const double MaximumBackgroundLightness = 0.25;

    /// <summary>
    /// A foreground color with the same hue as <paramref name="color"/> that reads well on
    /// <see cref="DarkBackground"/>.
    /// </summary>
    public static Color ForegroundForDarkTheme(Color color)
    {
        (double hue, double saturation, double lightness) = ToHsl(color);
        lightness = 1 - lightness;

        Color adjusted = FromHsl(color.A, hue, saturation, lightness);
        while (ContrastRatio(adjusted, DarkBackground) < MinimumContrast && lightness < 1)
        {
            lightness = Math.Min(1, lightness + 0.02);
            adjusted = FromHsl(color.A, hue, saturation, lightness);
        }

        return adjusted;
    }

    /// <summary>A background highlight with the same hue as <paramref name="color"/>, dark enough for the dark theme.</summary>
    public static Color BackgroundForDarkTheme(Color color)
    {
        (double hue, double saturation, double lightness) = ToHsl(color);
        return FromHsl(color.A, hue, saturation, Math.Min(1 - lightness, MaximumBackgroundLightness));
    }

    /// <summary>True when <paramref name="textColor"/> is light, so the theme it belongs to is dark.</summary>
    public static bool IsDarkThemeText(Color textColor) => RelativeLuminance(textColor) > 0.5;

    /// <summary>The WCAG contrast ratio between two colors, from 1 to 21. Alpha is ignored.</summary>
    public static double ContrastRatio(Color first, Color second)
    {
        double a = RelativeLuminance(first);
        double b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>The WCAG relative luminance of a color, from 0 (black) to 1 (white).</summary>
    public static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2;

        if (max == min)
            return (0, 0, lightness);

        double delta = max - min;
        double saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        double hue = max == r ? ((g - b) / delta) + (g < b ? 6 : 0)
            : max == g ? ((b - r) / delta) + 2
            : ((r - g) / delta) + 4;

        return (hue / 6, saturation, lightness);
    }

    private static Color FromHsl(int alpha, double hue, double saturation, double lightness)
    {
        if (saturation == 0)
        {
            int gray = ToByte(lightness);
            return Color.FromArgb(alpha, gray, gray, gray);
        }

        double q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - (lightness * saturation);
        double p = (2 * lightness) - q;

        return Color.FromArgb(
            alpha,
            ToByte(HueToChannel(p, q, hue + (1.0 / 3))),
            ToByte(HueToChannel(p, q, hue)),
            ToByte(HueToChannel(p, q, hue - (1.0 / 3))));
    }

    private static double HueToChannel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + ((q - p) * 6 * t);
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + ((q - p) * ((2.0 / 3) - t) * 6);
        return p;
    }

    private static int ToByte(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
