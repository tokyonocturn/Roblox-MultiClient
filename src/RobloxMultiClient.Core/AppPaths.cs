namespace RobloxMultiClient.Core;

/// <summary>All locations this application reads or writes. Everything stays under one local folder.</summary>
public sealed record AppPaths(string DataDirectory)
{
    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary><c>%LOCALAPPDATA%\Camo\RobloxMultiClient</c></summary>
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Camo",
        "RobloxMultiClient"));

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
