using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;

namespace RobloxMultiClient.Core.Clients;

/// <summary>
/// Owns the list of running Roblox clients: the only place a client snapshot lives. It asks the
/// watcher what exists, notices arrivals and departures, and reports unexpected terminations. It
/// never affects a client's lifetime.
/// </summary>
public sealed class ClientMonitor : IDisposable
{
    private readonly IClientWatcher _watcher;
    private readonly IErrorReporter _errors;
    private readonly IAppLogger _log;
    private readonly object _gate = new();
    private IReadOnlyList<RobloxClientProcess> _clients = [];

    public ClientMonitor(IClientWatcher watcher, IErrorReporter errors, IAppLogger log)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _watcher.ClientExited += OnClientExited;
    }

    /// <summary>Raised after the list may have changed.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<RobloxClientProcess> Clients
    {
        get
        {
            lock (_gate)
            {
                return _clients;
            }
        }
    }

    public void Refresh()
    {
        IReadOnlyList<RobloxClientProcess> next;
        try
        {
            next = _watcher.Snapshot();
        }
        catch (Exception ex)
        {
            _errors.Report(new AppError(
                AppErrorKind.ClientQueryFailed,
                "Could not read the list of running Roblox clients",
                ex.Message,
                ex));
            return;
        }

        bool changed;
        lock (_gate)
        {
            changed = !SameClients(_clients, next);
            _clients = next;
        }

        if (changed)
        {
            _log.Info($"Roblox clients now running: {next.Count} ({string.Join(", ", next.Select(client => client.ProcessId))})");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClientExited(object? sender, ClientExit exit)
    {
        if (exit.ExitCode is int code)
        {
            _log.Info($"Roblox client {exit.ProcessId} exited with code {code}");
            if (code != 0)
            {
                _errors.Report(new AppError(
                    AppErrorKind.ClientCrashed,
                    $"A Roblox client ended unexpectedly (exit code {code})",
                    exit.ExecutablePath));
            }
        }
        else
        {
            _log.Info($"Roblox client {exit.ProcessId} exited");
        }

        // Drop it from the list before the UI next paints.
        Refresh();
    }

    private static bool SameClients(
        IReadOnlyList<RobloxClientProcess> left,
        IReadOnlyList<RobloxClientProcess> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].ProcessId != right[i].ProcessId ||
                !string.Equals(left[i].ExecutablePath, right[i].ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose() => _watcher.ClientExited -= OnClientExited;
}
