using System.Diagnostics;
using RobloxMultiClient.Core.Clients;

namespace RobloxMultiClient.Windows;

/// <summary>
/// Reads the Roblox clients Windows reports. It keeps one <see cref="Process"/> per client so that
/// an exit (and its code, when Windows offers one) can be reported; it never issues a start, stop,
/// kill or signal of any kind.
/// </summary>
public sealed class WindowsClientWatcher : IClientWatcher
{
    private const string ProcessName = "RobloxPlayerBeta";

    private readonly Dictionary<int, Process> _tracked = [];
    private readonly object _gate = new();
    private bool _disposed;

    public event EventHandler<ClientExit>? ClientExited;

    public IReadOnlyList<RobloxClientProcess> Snapshot()
    {
        var found = new List<RobloxClientProcess>();
        var alive = new HashSet<int>();

        Process[] processes = Process.GetProcessesByName(ProcessName);
        try
        {
            foreach (Process process in processes)
            {
                RobloxClientProcess? client = Describe(process);
                if (client is null)
                {
                    continue;
                }

                alive.Add(client.ProcessId);
                found.Add(client);
                Track(process);
            }
        }
        finally
        {
            foreach (Process process in processes)
            {
                if (!alive.Contains(SafeId(process)))
                {
                    process.Dispose();
                }
            }
        }

        DropMissing(alive);
        found.Sort((left, right) => left.ProcessId.CompareTo(right.ProcessId));
        return found;
    }

    private void Track(Process process)
    {
        int id = SafeId(process);
        if (id == 0)
        {
            process.Dispose();
            return;
        }

        lock (_gate)
        {
            if (_disposed || _tracked.ContainsKey(id))
            {
                // Already tracked: the watcher owns its own handle for that process.
                process.Dispose();
                return;
            }

            _tracked[id] = process;
        }

        try
        {
            process.EnableRaisingEvents = true;
        }
        catch (Exception)
        {
            // Without the event we will not learn the exit code; the client is still listed.
            return;
        }

        process.Exited += (_, _) => OnExited(id, process);
    }

    private void OnExited(int processId, Process process)
    {
        string? path = TryPath(process);
        int? exitCode = null;
        try
        {
            exitCode = process.ExitCode;
        }
        catch (Exception)
        {
            // Windows will not always hand over the code of a process we did not start.
        }

        lock (_gate)
        {
            if (_tracked.Remove(processId, out Process? owned))
            {
                owned.Dispose();
            }
        }

        ClientExited?.Invoke(this, new ClientExit(processId, path, exitCode));
    }

    private void DropMissing(HashSet<int> alive)
    {
        List<int> gone;
        lock (_gate)
        {
            gone = _tracked.Keys.Where(id => !alive.Contains(id)).ToList();
        }

        // Anything still tracked but no longer enumerated has ended; dispose quietly. The exit
        // event normally fires first, so this is only a safety net for a missed notification.
        foreach (int id in gone)
        {
            lock (_gate)
            {
                if (_tracked.Remove(id, out Process? process))
                {
                    process.Dispose();
                }
            }
        }
    }

    private static RobloxClientProcess? Describe(Process process)
    {
        int id = SafeId(process);
        if (id == 0)
        {
            return null;
        }

        string? path = TryPath(process);
        DateTime? started = null;
        try
        {
            started = process.StartTime.ToUniversalTime();
        }
        catch (Exception)
        {
            // A guarded or exited process may refuse its start time; the client is still listed.
        }

        return new RobloxClientProcess(id, path, VersionFrom(path), started);
    }

    private static string? VersionFrom(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        string name = Path.GetFileName(directory);
        return name.StartsWith("version-", StringComparison.OrdinalIgnoreCase) ? name : null;
    }

    private static string? TryPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            // Elevated or packaged clients do not expose their module path to this user.
            return null;
        }
    }

    private static int SafeId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void Dispose()
    {
        List<Process> handles;
        lock (_gate)
        {
            _disposed = true;
            handles = _tracked.Values.ToList();
            _tracked.Clear();
        }

        foreach (Process process in handles)
        {
            process.Dispose();
        }
    }
}
