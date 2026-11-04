using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.Json;

namespace Camo.BrandingGenerator;

/// <summary>
/// Draws the Camo identity. Everything is generated from geometry and one seeded pattern, so the
/// assets are reproducible and carry no external licence.
/// </summary>
internal static class CamoBrand
{
    // Camo palette. Keep in sync with src/RobloxMultiClient.App/Ui/Theme.cs.
    public static readonly Color Ink = Color.FromArgb(0xFF, 0x0E, 0x12, 0x11);
    public static readonly Color Moss = Color.FromArgb(0xFF, 0x3E, 0x5B, 0x3A);
    public static readonly Color Olive = Color.FromArgb(0xFF, 0x8F, 0xA2, 0x5F);
    public static readonly Color Sand = Color.FromArgb(0xFF, 0xC9, 0xBF, 0xA3);
    public static readonly Color Clay = Color.FromArgb(0xFF, 0x7A, 0x4B, 0x32);
    public static readonly Color Bone = Color.FromArgb(0xFF, 0xEC, 0xEC, 0xE2);
    public static readonly Color Text = Color.FromArgb(0xFF, 0xE9, 0xEF, 0xE9);
    public static readonly Color Muted = Color.FromArgb(0xFF, 0x93, 0xA1, 0x99);

    private const int PatternSeed = 20261103;

    public static void RenderMark(Graphics graphics, RectangleF box)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float radius = box.Width * 0.235f;
        using GraphicsPath body = Squircle(box, radius);

        using (var baseBrush = new SolidBrush(Moss))
        {
            graphics.FillPath(baseBrush, body);
        }

        GraphicsState state = graphics.Save();
        graphics.SetClip(body, CombineMode.Intersect);

        var random = new Random(PatternSeed);
        (Color Colour, float Scale, int Count)[] layers =
        [
            (Clay, 0.30f, 3),
            (Olive, 0.34f, 4),
            (Ink, 0.26f, 3),
            (Sand, 0.20f, 3)
        ];

        foreach ((Color colour, float scale, int count) in layers)
        {
            for (int i = 0; i < count; i++)
            {
                float cx = box.X + (float)random.NextDouble() * box.Width;
                float cy = box.Y + (float)random.NextDouble() * box.Height;
                float r = box.Width * scale * (0.7f + (float)random.NextDouble() * 0.6f);
                using GraphicsPath blob = Blob(random, cx, cy, r);
                using var brush = new SolidBrush(Color.FromArgb(
                    colour == Ink ? 150 : 235, colour.R, colour.G, colour.B));
                graphics.FillPath(brush, blob);
            }
        }

            graphics.Restore(state);

        // Multi-instance glyph: four panes, one tinted, with the pattern showing through the gaps.
        float inset = box.Width * 0.215f;
        var area = RectangleF.Inflate(box, -inset, -inset);
        float gap = box.Width * 0.045f;
        float paneWidth = (area.Width - gap) / 2f;
        float paneHeight = (area.Height - gap) / 2f;
        float paneRadius = box.Width * 0.028f;

        DrawPane(graphics, new RectangleF(area.X, area.Y, paneWidth, paneHeight), paneRadius, Bone);
        DrawPane(graphics, new RectangleF(area.X + paneWidth + gap, area.Y, paneWidth, paneHeight), paneRadius, Sand);
        DrawPane(graphics, new RectangleF(area.X, area.Y + paneHeight + gap, paneWidth, paneHeight), paneRadius, Bone);
        DrawPane(graphics, new RectangleF(area.X + paneWidth + gap, area.Y + paneHeight + gap, paneWidth, paneHeight), paneRadius, Bone);
    }

    private static void DrawPane(Graphics graphics, RectangleF rect, float radius, Color colour)
    {
        using GraphicsPath path = Squircle(rect, Math.Max(0.5f, radius));
        using var brush = new SolidBrush(colour);
        graphics.FillPath(brush, path);
    }

    /// <summary>
    /// Draws tracked display text from glyph outlines. Outlines give exact bounds, so measured
    /// layout and drawn layout can never disagree and nothing gets clipped.
    /// </summary>
    public static void RenderWordmark(
        Graphics graphics,
        string text,
        RectangleF box,
        float emSize,
        Color colour,
        float tracking)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using FontFamily family = new(FontFamilyName());
        using var brush = new SolidBrush(colour);
        using var format = new StringFormat(StringFormat.GenericTypographic);

        float x = box.X;
        float baselineCentre = box.Y + (box.Height / 2f);
        foreach (char character in text)
        {
            using GraphicsPath path = GlyphPath(character, family, emSize, format);
            RectangleF bounds = path.GetBounds();
            using var transform = new Matrix();
            transform.Translate(x - bounds.X, baselineCentre - (bounds.Height / 2f) - bounds.Y);
            path.Transform(transform);
            graphics.FillPath(brush, path);
            x += bounds.Width + tracking;
        }
    }

    public static float MeasureWordmark(Graphics graphics, string text, float height, float tracking)
    {
        using FontFamily family = new(FontFamilyName());
        using var format = new StringFormat(StringFormat.GenericTypographic);
        float width = 0f;
        foreach (char character in text)
        {
            using GraphicsPath path = GlyphPath(character, family, height, format);
            width += path.GetBounds().Width + tracking;
        }

        return width - tracking;
    }

    private static GraphicsPath GlyphPath(char character, FontFamily family, float emSize, StringFormat format)
    {
        var path = new GraphicsPath();
        path.AddString(
            character.ToString(),
            family,
            (int)FontStyle.Bold,
            emSize,
            PointF.Empty,
            format);
        return path;
    }

    /// <summary>Mark plus tracked wordmark, laid out left to right and measured, never clipped.</summary>
    public static SizeF MeasureLogotype(Graphics graphics, float markSize)
    {
        float emSize = markSize * 0.72f;
        float tracking = markSize * 0.035f;
        float textWidth = MeasureWordmark(graphics, "CAMO", emSize, tracking);
        float gap = markSize * 0.20f;
        return new SizeF(markSize + gap + textWidth + (markSize * 0.02f), markSize);
    }

    public static void RenderLogotype(Graphics graphics, RectangleF area, float markSize)
    {
        float emSize = markSize * 0.72f;
        float tracking = markSize * 0.035f;
        float gap = markSize * 0.20f;
        float textWidth = MeasureWordmark(graphics, "CAMO", emSize, tracking);

        RenderMark(graphics, new RectangleF(area.X, area.Y, markSize, markSize));

        RenderWordmark(
            graphics,
            "CAMO",
            new RectangleF(area.X + markSize + gap, area.Y, textWidth, markSize),
            emSize,
            Text,
            tracking);
    }

    public static void RenderBanner(Graphics graphics, Size size)
    {
        graphics.Clear(Ink);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Faint camo texture across the whole banner, then a left-to-right dark scrim so the
        // identity block stays crisp. No hard clip edge anywhere.
        var random = new Random(PatternSeed + 7);
        for (int i = 0; i < 16; i++)
        {
            float cx = (float)random.NextDouble() * size.Width;
            float cy = (float)random.NextDouble() * size.Height;
            float r = size.Height * (0.22f + (float)random.NextDouble() * 0.55f);
            using GraphicsPath blob = Blob(random, cx, cy, r);
            Color colour = i % 3 == 0 ? Moss : i % 3 == 1 ? Clay : Olive;
            using var brush = new SolidBrush(Color.FromArgb(30, colour.R, colour.G, colour.B));
            graphics.FillPath(brush, blob);
        }

        var scrimBounds = new RectangleF(0, 0, size.Width * 0.72f, size.Height);
        using (var scrim = new LinearGradientBrush(scrimBounds, Color.FromArgb(238, Ink), Color.FromArgb(0, Ink), 0f))
        {
            scrim.SetBlendTriangularShape(0.55f, 0.9f);
            graphics.FillRectangle(scrim, scrimBounds);
        }

        float markSize = size.Height * 0.42f;
        float left = size.Width * 0.065f;
        float top = size.Height * 0.29f;
        RenderMark(graphics, new RectangleF(left, top, markSize, markSize));

        float textHeight = size.Height * 0.235f;
        float tracking = textHeight * 0.055f;
        float textX = left + markSize + size.Height * 0.085f;
        RenderWordmark(
            graphics,
            "CAMO",
            new RectangleF(textX, top - size.Height * 0.02f, size.Width - textX, textHeight * 1.35f),
            textHeight,
            Text,
            tracking);

        using (Font captionFont = DisplayFont(size.Height * 0.052f, bold: true))
        using (var captionBrush = new SolidBrush(Sand))
        using (var captionFormat = new StringFormat(StringFormat.GenericTypographic))
        {
            float captionY = top + size.Height * 0.30f;
            float captionTracking = size.Height * 0.010f;
            float x = textX;
            foreach (char character in "ROBLOX MULTICLIENT")
            {
                string glyph = character.ToString();
                graphics.DrawString(glyph, captionFont, captionBrush, x, captionY, captionFormat);
                x += graphics.MeasureString(glyph, captionFont, PointF.Empty, captionFormat).Width + captionTracking;
            }
        }

        using (Font taglineFont = new Font(FontFamilyName(), size.Height * 0.042f))
        using (var taglineBrush = new SolidBrush(Muted))
        {
            graphics.DrawString(
                "Local, private multi-instance launcher for Windows",
                taglineFont,
                taglineBrush,
                textX,
                top + size.Height * 0.375f);
        }

        using (var rulePen = new Pen(Color.FromArgb(70, Moss), 2f))
        {
            graphics.DrawLine(rulePen, left, size.Height * 0.87f, size.Width - left, size.Height * 0.87f);
        }
    }

    public static Bitmap RenderMarkBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        RenderMark(graphics, new RectangleF(0, 0, size, size));
        return bitmap;
    }

    public static void WriteIco(string path, int[] sizes)
    {
        var frames = new List<byte[]>();
        foreach (int size in sizes)
        {
            using Bitmap bitmap = RenderMarkBitmap(size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            frames.Add(stream.ToArray());
        }

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);            // reserved
        writer.Write((ushort)1);            // type: icon
        writer.Write((ushort)sizes.Length);

        int offset = 6 + (16 * sizes.Length);
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dimension = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            writer.Write(dimension);        // width
            writer.Write(dimension);        // height
            writer.Write((byte)0);          // palette
            writer.Write((byte)0);          // reserved
            writer.Write((ushort)1);        // planes
            writer.Write((ushort)32);       // bpp
            writer.Write(frames[i].Length);
            writer.Write(offset);
            offset += frames[i].Length;
        }

        foreach (byte[] frame in frames)
        {
            writer.Write(frame);
        }
    }

    public static void WritePaletteJson(string path) => File.WriteAllText(path, JsonSerializer.Serialize(new
    {
        name = "Camo",
        application = "Roblox MultiClient",
        colors = new Dictionary<string, string>
        {
            ["ink"] = "#0E1211",
            ["surface"] = "#151C19",
            ["surfaceAlt"] = "#1B2420",
            ["border"] = "#2A352F",
            ["text"] = "#E9EFE9",
            ["muted"] = "#93A199",
            ["moss"] = "#3E5B3A",
            ["olive"] = "#8FA25F",
            ["sand"] = "#C9BFA3",
            ["clay"] = "#7A4B32",
            ["bone"] = "#ECECE2",
            ["ok"] = "#7FD08A",
            ["warn"] = "#E4C265",
            ["danger"] = "#E2706E"
        },
        typography = new { family = "Segoe UI", display = "CAMO wordmark, tracked uppercase", mono = "Consolas" },
        mark = "Camo-patterned squircle with a four-pane window glyph; one pane in sand marks the extra instance.",
        seededPattern = PatternSeed
    }, new JsonSerializerOptions { WriteIndented = true }));

    private static string FontFamilyName()
    {
        using var installed = new InstalledFontCollection();
        string[] preferred = ["Segoe UI Variable Display", "Segoe UI", "Tahoma"];
        foreach (string name in preferred)
        {
            if (installed.Families.Any(family => family.Name == name))
            {
                return name;
            }
        }

        return "Arial";
    }

    private static Font DisplayFont(float pixelHeight, bool bold = true)
    {
        // Font sizes are points; convert the wanted pixel height at 96 dpi.
        float points = pixelHeight * 72f / 96f;
        return new Font(FontFamilyName(), points, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
    }

    private static GraphicsPath Squircle(RectangleF bounds, float radius)
    {
        float d = Math.Max(0.5f, radius * 2f);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Blob(Random random, float cx, float cy, float radius, int points = 9)
    {
        var vertices = new PointF[points];
        for (int i = 0; i < points; i++)
        {
            double angle = 2 * Math.PI * i / points;
            float jitter = radius * (0.62f + (float)random.NextDouble() * 0.55f);
            vertices[i] = new PointF(
                cx + (float)Math.Cos(angle) * jitter,
                cy + (float)Math.Sin(angle) * jitter);
        }

        var path = new GraphicsPath();
        path.AddClosedCurve(vertices, 0.42f);
        return path;
    }
}
