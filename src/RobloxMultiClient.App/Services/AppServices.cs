using RobloxMultiClient.Core;
using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;
using RobloxMultiClient.Core.Settings;
using RobloxMultiClient.Core.Singleton;
using RobloxMultiClient.Windows;

namespace RobloxMultiClient.App.Services;

/// <summary>
/// Composition root. Builds every service once, wires them together and owns the disposables.
/// Nothing else in the app constructs services.
/// </summary>
public sealed class AppServices : IDisposable
{
    private AppServices(
        AppPaths paths,
        IAppLogger logger,
        IErrorReporter errors,
        SettingsService settings,
        ISingletonGuard guard,
        ClientMonitor clients)
    {
        Paths = paths;
        Logger = logger;
        Errors = errors;
        Settings = settings;
        Guard = guard;
        Clients = clients;
    }

    public AppPaths Paths { get; }
    public IAppLogger Logger { get; }
    public IErrorReporter Errors { get; }
    public SettingsService Settings { get; }
    public ISingletonGuard Guard { get; }
    public ClientMonitor Clients { get; }

    /// <param name="singletonNames">
    /// Overrides the kernel object names the guard claims. Production uses the client's own names.
    /// Verification tooling confines itself to private names through <c>RMC_SINGLETON_NAMES</c>,
    /// which must hold exactly two semicolon-separated object names; an invalid value fails
    /// startup rather than silently falling back to the live client's names.
    /// </param>
    /// <param name="watcher">
    /// Replaces the Windows watcher in tests, so client lists can be driven without real processes.
    /// </param>
    public static AppServices Create(
        AppPaths paths,
        IReadOnlyList<string>? singletonNames = null,
        IClientWatcher? watcher = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        paths.EnsureCreated();

        var logger = new FileAppLogger(paths.LogDirectory);
        var errors = new ErrorReporter(logger);
        var store = new JsonSettingsStore(paths.SettingsPath, errors, logger);
        var settings = new SettingsService(store, errors, logger);
        singletonNames ??= VerificationNames();
        var guard = new NamedSingletonGuard(errors, logger, singletonNames);
        var clients = new ClientMonitor(watcher ?? new WindowsClientWatcher(), errors, logger);

        if (singletonNames is not null)
        {
            logger.Warning(
                "Claiming verification singleton names instead of the client's: " +
                string.Join(", ", singletonNames));
        }

        logger.Info("Roblox MultiClient starting");
        return new AppServices(paths, logger, errors, settings, guard, clients);
    }

    /// <summary>
    /// The verification hook: two private object names so a test run never claims the names of a
    /// live Roblox client. Absent means production names. Malformed input is an error, not a
    /// fallback, because a silent fallback could claim names the caller meant to avoid.
    /// </summary>
    private static IReadOnlyList<string>? VerificationNames()
    {
        string? raw = Environment.GetEnvironmentVariable("RMC_SINGLETON_NAMES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] names = raw.Split(';', StringSplitOptions.TrimEntries);
        bool valid = names.Length == 2 && names.All(name =>
            name.Length is > 0 and <= 128 && name.All(ch => ch > ' ' && ch != '\\'));
        if (!valid)
        {
            throw new ArgumentException(
                "RMC_SINGLETON_NAMES must contain exactly two object names without spaces or backslashes.");
        }

        return names;
    }

    public void Dispose()
    {
        Clients.Dispose();
        Guard.Dispose();
    }
}
