using System.Drawing;
using System.Reflection;

namespace RobloxMultiClient.App.Ui;

/// <summary>Loads the Camo branding assets embedded in this assembly.</summary>
internal static class Assets
{
    private static readonly Assembly Assembly = typeof(Assets).Assembly;

    public static Image LoadImage(string fileName)
    {
        using Stream stream = OpenStream(fileName);
        // Copy into memory so the image does not depend on the disposed stream.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return Image.FromStream(buffer);
    }

    public static Icon LoadIcon(string fileName)
    {
        using Stream stream = OpenStream(fileName);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return new Icon(buffer);
    }

    private static Stream OpenStream(string fileName)
    {
        string? name = Assembly.GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            throw new InvalidOperationException($"Embedded asset '{fileName}' is missing from the build.");
        }

        return Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded asset '{fileName}' could not be opened.");
    }
}
