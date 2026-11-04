namespace RobloxMultiClient.Core.Settings;

/// <summary>Everything the application persists. Plain data, no behaviour.</summary>
public sealed record AppSettings
{
    /// <summary>Kept in step with the window's MinimumSize, so a saved size is always applicable.</summary>
    public const int MinimumWindowWidth = 780;

    public const int MinimumWindowHeight = 520;

    public int WindowWidth { get; init; } = 880;

    public int WindowHeight { get; init; } = 560;

    public AppSettings Normalized() => this with
    {
        WindowWidth = Math.Clamp(WindowWidth, MinimumWindowWidth, 4000),
        WindowHeight = Math.Clamp(WindowHeight, MinimumWindowHeight, 3000)
    };
}

public interface ISettingsStore
{
    string SettingsPath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}
