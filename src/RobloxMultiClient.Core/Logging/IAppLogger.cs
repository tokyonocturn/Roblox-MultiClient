namespace RobloxMultiClient.Core.Logging;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>Minimal logger contract; the UI and the services share one sink.</summary>
public interface IAppLogger
{
    string LogDirectory { get; }

    string? CurrentLogFile { get; }

    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
