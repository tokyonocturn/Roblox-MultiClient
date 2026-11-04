using System.Globalization;
using RobloxMultiClient.Core.Logging;

namespace RobloxMultiClient.Windows;

/// <summary>Appends human-readable lines to a per-day log file under the app data folder.</summary>
public sealed class FileAppLogger : IAppLogger
{
    private readonly object _gate = new();
    private string? _currentDate;
    private string? _currentFile;

    public FileAppLogger(string logDirectory)
    {
        LogDirectory = logDirectory;
        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception)
        {
            // The first write attempt will report the real failure through CurrentLogFile staying null.
        }
    }

    public string LogDirectory { get; }

    public string? CurrentLogFile
    {
        get
        {
            lock (_gate)
            {
                return _currentFile;
            }
        }
    }

    public void Info(string message) => Write(LogLevel.Info, message, null);

    public void Warning(string message) => Write(LogLevel.Warning, message, null);

    public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    private void Write(LogLevel level, string message, Exception? exception)
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var line = new System.Text.StringBuilder()
            .Append(stamp).Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
            .Append(message);

        if (exception is not null)
        {
            line.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            if (exception.StackTrace is { Length: > 0 } stack)
            {
                line.AppendLine().Append(stack);
            }
        }

        lock (_gate)
        {
            try
            {
                string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                if (_currentDate != today || _currentFile is null)
                {
                    _currentDate = today;
                    _currentFile = Path.Combine(LogDirectory, $"multiclient-{today}.log");
                }

                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(_currentFile, line.ToString() + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging must never take the application down; the UI surfaces the missing path.
            }
        }
    }
}
