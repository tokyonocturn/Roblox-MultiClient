namespace RobloxMultiClient.Core.Singleton;

public enum SingletonClaimState
{
    /// <summary>This process created the object and holds it for its lifetime.</summary>
    Claimed,

    /// <summary>The name already exists as a mutex held by another process; multi-instance is on.</summary>
    AlreadyHeld,

    /// <summary>
    /// The name is occupied by a running Roblox client's event. That client secured single-process
    /// for itself, so further clients are refused until it exits; the guard can then reclaim the name.
    /// </summary>
    HeldByEvent,

    /// <summary>The name could not be created or opened; multi-instance launching will not work.</summary>
    Failed,

    /// <summary>Multi-instance was turned off, so this application holds nothing.</summary>
    Released
}

public sealed record SingletonClaim(string Name, SingletonClaimState State, string? Detail = null);

/// <summary>
/// Owns the named kernel objects Roblox uses to enforce one client per session. Holding these
/// names is what allows a second client to start; releasing them restores normal behaviour.
/// </summary>
public interface ISingletonGuard : IDisposable
{
    IReadOnlyList<SingletonClaim> Claims { get; }

    /// <summary>Claims every name once. Repeated calls are ignored.</summary>
    void Claim();

    /// <summary>
    /// Gives the names back to the system, turning multi-instance off. Clients already running are
    /// unaffected: no process is touched, only this application's handles.
    /// </summary>
    void Release();

    /// <summary>
    /// Re-probes names that are not effectively held (a client has since exited). Safe to call at
    /// any time; names already claimed or held elsewhere are left alone.
    /// </summary>
    void Reclaim();

    /// <summary>True when every name is held by us or by another multi-instance holder.</summary>
    bool IsMultiInstanceReady { get; }
}
