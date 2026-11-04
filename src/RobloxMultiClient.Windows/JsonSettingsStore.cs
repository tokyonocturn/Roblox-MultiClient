using System.Text.Json;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;
using RobloxMultiClient.Core.Settings;

namespace RobloxMultiClient.Windows;

/// <summary>Stores settings as indented JSON next to the logs, written atomically.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IErrorReporter _errors;
    private readonly IAppLogger _log;

    public JsonSettingsStore(string settingsPath, IErrorReporter errors, IAppLogger log)
    {
        SettingsPath = settingsPath;
        _errors = errors;
        _log = log;
    }

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(SettingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _errors.Report(new AppError(
                AppErrorKind.SettingsUnreadable,
                "Settings could not be read; defaults are in use",
                SettingsPath,
                ex));
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        string temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, SettingsPath, overwrite: true);
    }
}
