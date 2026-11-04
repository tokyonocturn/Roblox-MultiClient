namespace RobloxMultiClient.Core.Errors;

public enum AppErrorKind
{
    /// <summary>A singleton name could not be created or opened, so multi-instance may not work.</summary>
    SingletonUnavailable,

    /// <summary>Windows would not give us the list of running clients.</summary>
    ClientQueryFailed,

    /// <summary>An observed Roblox client ended with a non-zero exit code.</summary>
    ClientCrashed,

    SettingsUnreadable,
    SettingsWriteFailed,
    LogWriteFailed,
    Unexpected
}

/// <summary>A user-facing failure with enough detail to explain what to do next.</summary>
public sealed record AppError(AppErrorKind Kind, string Message, string? Detail = null, Exception? Exception = null)
{
    public DateTime OccurredUtc { get; } = DateTime.UtcNow;

    public string Describe() => Detail is null ? Message : $"{Message}: {Detail}";

    public override string ToString() => $"{Kind}: {Describe()}";
}

public interface IErrorReporter
{
    event EventHandler<AppError>? ErrorRaised;

    IReadOnlyList<AppError> Recent { get; }

    void Report(AppError error);
}

/// <summary>Single place errors are recorded, logged and broadcast from.</summary>
public sealed class ErrorReporter : IErrorReporter
{
    private const int MaxRecent = 50;
    private readonly List<AppError> _recent = [];
    private readonly Logging.IAppLogger _log;

    public ErrorReporter(Logging.IAppLogger log) => _log = log;

    public event EventHandler<AppError>? ErrorRaised;

    public IReadOnlyList<AppError> Recent
    {
        get
        {
            lock (_recent)
            {
                return _recent.ToArray();
            }
        }
    }

    public void Report(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        lock (_recent)
        {
            _recent.Add(error);
            if (_recent.Count > MaxRecent)
            {
                _recent.RemoveAt(0);
            }
        }

        _log.Error(error.ToString(), error.Exception);
        ErrorRaised?.Invoke(this, error);
    }
}
