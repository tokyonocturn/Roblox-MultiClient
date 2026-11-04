using System.Drawing;
using System.Drawing.Text;

namespace RobloxMultiClient.App.Ui;

/// <summary>Camo palette and fonts. One place defines every colour the app draws.</summary>
internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(0x0E, 0x12, 0x11);
    public static readonly Color Surface = Color.FromArgb(0x15, 0x1C, 0x19);
    public static readonly Color SurfaceAlt = Color.FromArgb(0x1B, 0x24, 0x20);
    public static readonly Color SurfaceHover = Color.FromArgb(0x22, 0x2D, 0x27);
    public static readonly Color Border = Color.FromArgb(0x2A, 0x35, 0x2F);
    public static readonly Color Text = Color.FromArgb(0xE9, 0xEF, 0xE9);
    public static readonly Color Muted = Color.FromArgb(0x93, 0xA1, 0x99);
    public static readonly Color Moss = Color.FromArgb(0x3E, 0x5B, 0x3A);
    public static readonly Color Olive = Color.FromArgb(0x8F, 0xA2, 0x5F);
    public static readonly Color Sand = Color.FromArgb(0xC9, 0xBF, 0xA3);
    public static readonly Color Ok = Color.FromArgb(0x7F, 0xD0, 0x8A);
    public static readonly Color Warn = Color.FromArgb(0xE4, 0xC2, 0x65);
    public static readonly Color Danger = Color.FromArgb(0xE2, 0x70, 0x6E);

    public static string FontFamily { get; } = ResolveFontFamily();

    public static Font Body(float size = 9f, FontStyle style = FontStyle.Regular) => new(FontFamily, size, style);

    public static Font Mono(float size = 8.25f) => new("Consolas", size, FontStyle.Regular);

    private static string ResolveFontFamily()
    {
        using var installed = new InstalledFontCollection();
        string[] preferred = ["Segoe UI Variable Text", "Segoe UI", "Tahoma"];
        foreach (string name in preferred)
        {
            if (installed.Families.Any(family => family.Name == name))
            {
                return name;
            }
        }

        return SystemFonts.MessageBoxFont?.FontFamily.Name ?? "Arial";
    }
}
