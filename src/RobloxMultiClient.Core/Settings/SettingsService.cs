using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;

namespace RobloxMultiClient.Core.Settings;

/// <summary>Owns the current settings value and is the only writer to the store.</summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;
    private readonly IErrorReporter _errors;
    private readonly IAppLogger _log;
    private readonly object _gate = new();
    private AppSettings _current;

    public SettingsService(ISettingsStore store, IErrorReporter errors, IAppLogger log)
    {
        _store = store;
        _errors = errors;
        _log = log;
        _current = store.Load().Normalized();
    }

    public string SettingsPath => _store.SettingsPath;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Applies a change and persists it. A failed write is surfaced, not swallowed.</summary>
    public void Update(Func<AppSettings, AppSettings> mutate)
    {
        AppSettings next;
        lock (_gate)
        {
            next = mutate(_current).Normalized();
            if (next == _current)
            {
                return;
            }

            _current = next;
        }

        try
        {
            _store.Save(next);
            _log.Info($"Settings saved to {_store.SettingsPath}");
        }
        catch (Exception ex)
        {
            _errors.Report(new AppError(
                AppErrorKind.SettingsWriteFailed,
                "Settings could not be saved",
                _store.SettingsPath,
                ex));
        }
    }
}
