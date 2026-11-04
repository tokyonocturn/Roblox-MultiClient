namespace RobloxMultiClient.Core.Clients;

/// <summary>
/// A Roblox client process observed on this machine. This application never owns, starts or stops
/// one: it exists to watch them, and the type deliberately offers no way to terminate a process.
/// </summary>
public sealed record RobloxClientProcess(
    int ProcessId,
    string? ExecutablePath,
    string? VersionName,
    DateTime? StartedUtc)
{
    /// <summary>Folder name of the version, or a neutral label when the path is unreadable.</summary>
    public string DisplayVersion => VersionName ?? "unknown version";

    public string DisplayPath => string.IsNullOrWhiteSpace(ExecutablePath) ? "(not readable)" : ExecutablePath!;
}

/// <summary>Reported when a client this application was watching has exited.</summary>
public sealed record ClientExit(int ProcessId, string? ExecutablePath, int? ExitCode);

/// <summary>
/// What the platform can tell us about Roblox clients. Implementations only read: the interface has
/// no start, kill or signal operation by design.
/// </summary>
public interface IClientWatcher : IDisposable
{
    /// <summary>Clients running at this moment, in a stable order.</summary>
    IReadOnlyList<RobloxClientProcess> Snapshot();

    /// <summary>
    /// Raised when a client present in an earlier snapshot exits. Exit code is null when Windows
    /// will not give it to us, which is normal for a process that already ended.
    /// </summary>
    event EventHandler<ClientExit>? ClientExited;
}
