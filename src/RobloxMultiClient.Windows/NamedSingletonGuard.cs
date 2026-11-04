using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;
using RobloxMultiClient.Core.Singleton;

namespace RobloxMultiClient.Windows;

/// <summary>
/// Claims the two named kernel objects the Roblox client uses for its single-instance check.
/// The names only need to <em>exist</em>: the client's own <c>CreateEventW</c> then fails with a
/// type mismatch and it starts as an additional instance instead of redirecting to the first one.
/// Both names are session-scoped (no <c>Global\</c> prefix), matching the per-user client.
/// </summary>
public sealed class NamedSingletonGuard : ISingletonGuard
{
    /// <summary>Names observed in the shipped client (see research/FINDINGS.md §3).</summary>
    public static readonly IReadOnlyList<string> DefaultObjectNames =
    [
        "ROBLOX_singletonMutex",
        "ROBLOX_singletonEvent"
    ];

    private readonly IErrorReporter _errors;
    private readonly IAppLogger _log;
    private readonly IReadOnlyList<string> _names;
    private readonly Dictionary<string, IDisposable> _handles = new(StringComparer.Ordinal);
    private readonly List<SingletonClaim> _claims = [];
    private readonly object _gate = new();
    private bool _claimed;

    public NamedSingletonGuard(IErrorReporter errors, IAppLogger log, IReadOnlyList<string>? names = null)
    {
        _errors = errors;
        _log = log;
        _names = names ?? DefaultObjectNames;
    }

    public IReadOnlyList<SingletonClaim> Claims
    {
        get
        {
            lock (_gate)
            {
                return _claims.ToArray();
            }
        }
    }

    public bool IsMultiInstanceReady
    {
        get
        {
            lock (_gate)
            {
                return _claims.Count == _names.Count &&
                    _claims.All(claim => claim.State is SingletonClaimState.Claimed or SingletonClaimState.AlreadyHeld);
            }
        }
    }

    public void Claim()
    {
        lock (_gate)
        {
            if (_claimed)
            {
                return;
            }

            _claimed = true;
            _claims.Clear();
            foreach (string name in _names)
            {
                _claims.Add(ClaimOne(name));
            }
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            for (int i = 0; i < _claims.Count; i++)
            {
                ReleaseHandle(_claims[i].Name);
                _claims[i] = new SingletonClaim(_claims[i].Name, SingletonClaimState.Released, "multi-instance turned off");
            }

            _claimed = false;
        }

        _log.Info("Singleton objects released: multi-instance is off");
    }

    /// <summary>
    /// Probes names that are not effectively held. A client exit reaches this from the watcher's
    /// thread, so every mutation of the claim list is serialised.
    /// </summary>
    public void Reclaim()
    {
        lock (_gate)
        {
            for (int i = 0; i < _claims.Count; i++)
            {
                SingletonClaim claim = _claims[i];
                if (claim.State is SingletonClaimState.Claimed or SingletonClaimState.AlreadyHeld)
                {
                    continue;
                }

                ReleaseHandle(claim.Name);
                SingletonClaim next = Probe(claim.Name);
                if (next.State != claim.State)
                {
                    Announce(next);
                }

                _claims[i] = next;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (string name in _handles.Keys.ToArray())
            {
                ReleaseHandle(name);
            }
        }
    }

    private SingletonClaim ClaimOne(string name)
    {
        SingletonClaim claim = Probe(name);
        Announce(claim);
        return claim;
    }

    /// <summary>Logs a claim and reports the one state that leaves the user without multi-instance.</summary>
    private void Announce(SingletonClaim claim)
    {
        _log.Info($"Singleton object '{claim.Name}': {claim.State} ({claim.Detail})");
        if (claim.State == SingletonClaimState.Failed)
        {
            _errors.Report(new AppError(
                AppErrorKind.SingletonUnavailable,
                $"Could not claim '{claim.Name}'; a second Roblox client may be refused",
                claim.Detail,
                null));
        }
    }

    private SingletonClaim Probe(string name)
    {
        // 1. A mutex already carries the name: another multi-instance helper holds it. Effective.
        if (TryOpenMutex(name, out Mutex? existingMutex) == ProbeResult.Opened)
        {
            _handles[name] = existingMutex!;
            return new SingletonClaim(name, SingletonClaimState.AlreadyHeld, "mutex held by another process");
        }

        // 2. An event carries the name: a running Roblox client secured single-process with it.
        if (TryOpenEvent(name, out EventWaitHandle? existingEvent) == ProbeResult.Opened)
        {
            // Deliberately not kept: the handle belongs to another process's object, and holding
            // it open would keep that object alive after the client exits, blocking our reclaim.
            existingEvent!.Dispose();
            return new SingletonClaim(name, SingletonClaimState.HeldByEvent, "a running Roblox client holds this name");
        }

        // 3. Nothing carries the name: create the mutex whose name makes the client's CreateEvent fail.
        try
        {
            // Ownership is irrelevant, only the name matters; never release it while we live.
            var mutex = new Mutex(initiallyOwned: false, name);
            _handles[name] = mutex;
            return new SingletonClaim(name, SingletonClaimState.Claimed, "held by Roblox MultiClient");
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Something appeared between the probes (a client starting at the same moment).
            return ReentrantProbe(name);
        }
        catch (UnauthorizedAccessException ex)
        {
            // The name exists but its security descriptor keeps us out; the name is still taken.
            return new SingletonClaim(name, SingletonClaimState.AlreadyHeld, $"exists but cannot be opened: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new SingletonClaim(name, SingletonClaimState.Failed, ex.Message);
        }
    }

    private SingletonClaim ReentrantProbe(string name)
    {
        if (TryOpenMutex(name, out Mutex? mutex) == ProbeResult.Opened)
        {
            _handles[name] = mutex!;
            return new SingletonClaim(name, SingletonClaimState.AlreadyHeld, "mutex held by another process");
        }

        if (TryOpenEvent(name, out EventWaitHandle? waitEvent) == ProbeResult.Opened)
        {
            waitEvent!.Dispose();
            return new SingletonClaim(name, SingletonClaimState.HeldByEvent, "a running Roblox client holds this name");
        }

        return new SingletonClaim(name, SingletonClaimState.Failed, "the name is taken by an object this app cannot open");
    }

    private void ReleaseHandle(string name)
    {
        if (_handles.Remove(name, out IDisposable? handle))
        {
            handle.Dispose();
        }
    }

    private enum ProbeResult
    {
        Absent,
        Opened
    }

    private static ProbeResult TryOpenMutex(string name, out Mutex? mutex)
    {
        try
        {
            mutex = Mutex.OpenExisting(name);
            return ProbeResult.Opened;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Absent, or the name belongs to an object of another type (the client's event).
            mutex = null;
            return ProbeResult.Absent;
        }
        catch (UnauthorizedAccessException)
        {
            mutex = null;
            return ProbeResult.Absent;
        }
    }

    private static ProbeResult TryOpenEvent(string name, out EventWaitHandle? waitEvent)
    {
        try
        {
            waitEvent = EventWaitHandle.OpenExisting(name);
            return ProbeResult.Opened;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            waitEvent = null;
            return ProbeResult.Absent;
        }
        catch (UnauthorizedAccessException)
        {
            waitEvent = null;
            return ProbeResult.Absent;
        }
    }
}
