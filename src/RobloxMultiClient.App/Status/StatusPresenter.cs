using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;
using RobloxMultiClient.Core.Settings;
using RobloxMultiClient.Core.Singleton;

namespace RobloxMultiClient.App.Status;

/// <summary>
/// Owns whether multi-instance is on and turns that into what the window shows. It never starts or
/// stops a Roblox client; it only holds the kernel objects and watches the clients Windows reports.
/// </summary>
public sealed class StatusPresenter : IDisposable
{
    private readonly ClientMonitor _monitor;
    private readonly ISingletonGuard _guard;
    private readonly SettingsService _settings;
    private readonly IErrorReporter _errors;
    private readonly IAppLogger _log;
    private IStatusView? _view;
    private SynchronizationContext? _ui;
    private bool _enabled;

    public StatusPresenter(
        ClientMonitor monitor,
        ISingletonGuard guard,
        SettingsService settings,
        IErrorReporter errors,
        IAppLogger log)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _monitor.Changed += OnMonitorChanged;
        _errors.ErrorRaised += OnErrorRaised;
    }

    public bool Enabled => _enabled;

    public void Attach(IStatusView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _ui = SynchronizationContext.Current;
        _monitor.Refresh();
        Publish();
    }

    /// <summary>Turns multi-instance on. Called once at startup, then only by the user.</summary>
    public void Enable()
    {
        if (_enabled)
        {
            return;
        }

        _enabled = true;
        _guard.Claim();
        _log.Info("Multi-instance enabled");
        Publish();
    }

    public void Disable()
    {
        if (!_enabled)
        {
            return;
        }

        _enabled = false;
        _guard.Release();
        _log.Info("Multi-instance disabled");
        Publish();
    }

    public void Toggle()
    {
        if (_enabled)
        {
            Disable();
        }
        else
        {
            Enable();
        }
    }

    /// <summary>Re-reads clients, and takes back a name a client was holding once that client is gone.</summary>
    public void Refresh()
    {
        Reclaim();
        _monitor.Refresh();
        Publish();
    }

    public void SaveWindowSize(int width, int height) =>
        _settings.Update(current => current with { WindowWidth = width, WindowHeight = height });

    public StatusSnapshot Snapshot() => new(
        _enabled,
        _guard.IsMultiInstanceReady,
        _guard.Claims,
        _monitor.Clients,
        _log.LogDirectory,
        StatusText());

    private string StatusText()
    {
        if (!_enabled)
        {
            return "Off. Roblox will allow one client at a time.";
        }

        if (_guard.IsMultiInstanceReady)
        {
            return "On. Start Roblox however you normally would, as often as you like.";
        }

        if (_guard.Claims.Any(claim => claim.State == SingletonClaimState.HeldByEvent))
        {
            return "Waiting. A running Roblox client still holds the single-instance name. Close it and this turns on by itself.";
        }

        if (_guard.Claims.Any(claim => claim.State == SingletonClaimState.Failed))
        {
            return "Blocked. This app could not take the single-instance name. See Details.";
        }

        return "Waiting for the single-instance name.";
    }

    /// <summary>
    /// The list changes when a client starts or exits. There is no polling timer: an exit is what
    /// frees a name a client was holding, so it takes the name back and repaints here.
    /// </summary>
    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        if (_ui is null)
        {
            Reclaim();
            Publish();
            return;
        }

        _ui.Post(_ =>
        {
            Reclaim();
            Publish();
        }, null);
    }

    private void Reclaim()
    {
        if (_enabled)
        {
            _guard.Reclaim();
        }
    }

    private void OnErrorRaised(object? sender, AppError error) => _view?.ShowError(error);

    private void Publish() => _view?.Render(Snapshot());

    public void Dispose()
    {
        _monitor.Changed -= OnMonitorChanged;
        _errors.ErrorRaised -= OnErrorRaised;
    }
}
