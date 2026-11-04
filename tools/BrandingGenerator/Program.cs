using System.Drawing;
using System.Drawing.Imaging;

namespace Camo.BrandingGenerator;

/// <summary>
/// Regenerates every Camo brand asset under <c>assets/branding</c>.
/// Run with: <c>dotnet run --project tools/BrandingGenerator -- assets/branding</c>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string outputDirectory = args.Length > 0 ? args[0] : FindDefaultOutput();
        Directory.CreateDirectory(outputDirectory);

        using (var logo = new Bitmap(512, 512, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(logo))
            {
                graphics.Clear(Color.Transparent);
                CamoBrand.RenderMark(graphics, new RectangleF(0, 0, 512, 512));
            }

            logo.Save(Path.Combine(outputDirectory, "camo-logo.png"), ImageFormat.Png);
        }

        CamoBrand.WriteIco(
            Path.Combine(outputDirectory, "camo-icon.ico"),
            [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]);

        const float markSize = 200f;
        SizeF logotypeSize;
        using (var measureSurface = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
        using (Graphics measureGraphics = Graphics.FromImage(measureSurface))
        {
            logotypeSize = CamoBrand.MeasureLogotype(measureGraphics, markSize);
        }

        using (var logotype = new Bitmap((int)Math.Ceiling(logotypeSize.Width), (int)markSize, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(logotype))
            {
                graphics.Clear(Color.Transparent);
                CamoBrand.RenderLogotype(graphics, new RectangleF(0, 0, logotype.Width, logotype.Height), markSize);
            }

            logotype.Save(Path.Combine(outputDirectory, "camo-logotype.png"), ImageFormat.Png);
        }

        using (var banner = new Bitmap(1280, 420, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(banner))
            {
                CamoBrand.RenderBanner(graphics, new Size(1280, 420));
            }

            banner.Save(Path.Combine(outputDirectory, "camo-banner.png"), ImageFormat.Png);
        }

        // Small preview strip used by the README table.
        using (var preview = new Bitmap(640, 160, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(preview))
            {
                graphics.Clear(CamoBrand.Ink);
                for (int i = 0; i < 4; i++)
                {
                    int size = i switch { 0 => 16, 1 => 32, 2 => 64, _ => 128 };
                    using Bitmap mark = CamoBrand.RenderMarkBitmap(size);
                    graphics.DrawImage(mark, 24 + (i * 150), 16);
                }
            }

            preview.Save(Path.Combine(outputDirectory, "camo-sizes.png"), ImageFormat.Png);
        }

        CamoBrand.WritePaletteJson(Path.Combine(outputDirectory, "palette.json"));
        Console.WriteLine($"Brand assets written to {Path.GetFullPath(outputDirectory)}");
        foreach (string file in Directory.EnumerateFiles(outputDirectory).Order())
        {
            Console.WriteLine($"  {Path.GetFileName(file),-24} {new FileInfo(file).Length,8} bytes");
        }

        return 0;
    }

    private static string FindDefaultOutput()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tools")))
            {
                return Path.Combine(directory.FullName, "assets", "branding");
            }

            directory = directory.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "assets", "branding");
    }
}
