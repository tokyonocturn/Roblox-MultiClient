using System.Threading;
using RobloxMultiClient.Core;
using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;
using RobloxMultiClient.Core.Settings;
using RobloxMultiClient.Core.Singleton;
using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests;

internal sealed class AssertionException(string message) : Exception(message);

internal static class Assert
{
    public static void True(bool condition, string because)
    {
        if (!condition)
        {
            throw new AssertionException(because);
        }
    }

    public static void False(bool condition, string because) => True(!condition, because);

    public static void Equal<T>(T expected, T actual, string because)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{because} (expected '{expected}', got '{actual}')");
        }
    }

    public static void NotEqual<T>(T unexpected, T actual, string because)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
        {
            throw new AssertionException($"{because} (both were '{actual}')");
        }
    }

    public static void Contains(string haystack, string needle, string because)
    {
        if (!haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException($"{because} (missing '{needle}')");
        }
    }

    public static AppError Single(IReadOnlyList<AppError> errors, AppErrorKind kind)
    {
        AppError? match = errors.FirstOrDefault(error => error.Kind == kind);
        return match ?? throw new AssertionException($"expected an error of kind {kind}, saw: {string.Join(", ", errors.Select(e => e.Kind))}");
    }

    /// <summary>Every claim with its state and detail, for assertion messages.</summary>
    public static string Describe(ISingletonGuard guard) =>
        string.Join(", ", guard.Claims.Select(claim => $"{claim.Name}={claim.State} ({claim.Detail})"));
}

/// <summary>Per-test sandbox: temp app data, real Windows services, and a harmless fictive client.</summary>
internal sealed class TestContext : IDisposable
{
    private readonly List<int> _spawned = [];

    public TestContext(string name)
    {
        Name = name;
        Root = Path.Combine(Path.GetTempPath(), "multiclient-tests", $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);

        Paths = new AppPaths(Path.Combine(Root, "appdata"));
        Paths.EnsureCreated();

        Logger = new FileAppLogger(Paths.LogDirectory);
        Errors = new ErrorReporter(Logger);
        Settings = new SettingsService(
            new JsonSettingsStore(Paths.SettingsPath, Errors, Logger),
            Errors,
            Logger);
    }

    public string Name { get; }
    public string Root { get; }

    /// <summary>
    /// Private kernel object names for a test: the app is pointed at these so tests never depend
    /// on, or interfere with, the live Roblox client's own objects.
    /// </summary>
    public IReadOnlyList<string> SingletonNames { get; } =
    [
        $"RMC_test_{Guid.NewGuid():N}_mutex",
        $"RMC_test_{Guid.NewGuid():N}_event"
    ];

    /// <summary>The name a fictive client uses for its single-instance event.</summary>
    public string SingletonEventName => SingletonNames[^1];

    public AppPaths Paths { get; }
    public IAppLogger Logger { get; }
    public ErrorReporter Errors { get; }
    public SettingsService Settings { get; }

    private static string FakeClientDirectory => Path.Combine(AppContext.BaseDirectory, "fake");

    /// <summary>Stages the fictive client under this sandbox and returns its path.</summary>
    public string ClientPath()
    {
        string directory = Path.Combine(Root, "Roblox", "Versions", "version-test-0001");
        string source = Path.Combine(FakeClientDirectory, "RobloxPlayerBeta.exe");
        if (!File.Exists(source))
        {
            throw new AssertionException(
                $"fake client missing at {source}; build tests/FakeRobloxClient first");
        }

        Directory.CreateDirectory(directory);
        foreach (string file in Directory.EnumerateFiles(FakeClientDirectory))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
        }

        return Path.Combine(directory, "RobloxPlayerBeta.exe");
    }

    /// <summary>Starts the fictive client the way a user starts Roblox: directly, outside the app.</summary>
    public System.Diagnostics.Process StartClient(string playerPath, string arguments)
    {
        System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = playerPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new AssertionException("could not start the fictive client");

        _spawned.Add(process.Id);
        return process;
    }

    /// <summary>Waits until a named kernel event exists, i.e. a client has taken the guard.</summary>
    public static bool WaitForEvent(string name, int timeoutMs = 4000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using var handle = EventWaitHandle.OpenExisting(name);
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100);
            }
        }

        return false;
    }

    public static bool IsAlive(int processId)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void KillQuietly(int processId)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(5000);
            }
        }
        catch (Exception)
        {
            // Cleanup only.
        }
    }

    public void Dispose()
    {
        foreach (int id in _spawned)
        {
            KillQuietly(id);
        }

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle keeps the temp folder; harmless.
        }
    }
}

/// <summary>A watcher the tests drive by hand, standing in for whatever Windows reports.</summary>
internal sealed class FakeClientWatcher : IClientWatcher
{
    private readonly List<RobloxClientProcess> _clients = [];

    public event EventHandler<ClientExit>? ClientExited;

    public Exception? FailWith { get; set; }

    public int SnapshotCount { get; private set; }

    public IReadOnlyList<RobloxClientProcess> Snapshot()
    {
        SnapshotCount++;
        if (FailWith is not null)
        {
            throw FailWith;
        }

        return _clients.ToArray();
    }

    public void Add(int processId, string? path = null)
    {
        _clients.Add(new RobloxClientProcess(
            processId,
            path ?? $@"C:\Sandbox\Versions\version-test\RobloxPlayerBeta.exe",
            "version-test",
            DateTime.UtcNow));
    }

    public void Remove(int processId) =>
        _clients.RemoveAll(client => client.ProcessId == processId);

    public void RaiseExit(int processId, int? exitCode) =>
        ClientExited?.Invoke(this, new ClientExit(processId, null, exitCode));

    public void Dispose()
    {
    }
}

/// <summary>Records the last snapshot so UI tests can assert on what a user would see.</summary>
internal sealed class RecordingView : App.Status.IStatusView
{
    public App.Status.StatusSnapshot? Last { get; private set; }

    public int RenderCount { get; private set; }

    public Core.Errors.AppError? LastError { get; private set; }

    public void Render(App.Status.StatusSnapshot snapshot)
    {
        Last = snapshot;
        RenderCount++;
    }

    public void ShowError(Core.Errors.AppError error) => LastError = error;
}
