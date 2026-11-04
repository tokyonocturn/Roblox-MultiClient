using System.Runtime.InteropServices;
using RobloxMultiClient.Core.Singleton;
using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests.Tests;

/// <summary>
/// The guard's own contract. The three states it exists for are proven against a real client
/// process in <see cref="ThreeStateTests"/>; these tests pin the Win32 details underneath.
/// Each test claims private names, so nothing here touches a live Roblox session.
/// </summary>
internal static class SingletonTests
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr attributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private static IReadOnlyList<string> SyntheticNames(string suffix) =>
    [
        $"RMC_test_singleton_a_{suffix}",
        $"RMC_test_singleton_b_{suffix}"
    ];

    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("singleton: claiming creates both objects", () =>
        {
            using var context = new TestContext("singleton-claim");
            using var guard = new NamedSingletonGuard(context.Errors, context.Logger, SyntheticNames(Guid.NewGuid().ToString("N")));
            guard.Claim();

            Assert.Equal(2, guard.Claims.Count, "both objects claimed");
            Assert.True(guard.Claims.All(claim => claim.State == SingletonClaimState.Claimed), Assert.Describe(guard));
            Assert.True(guard.IsMultiInstanceReady, "multi-instance is ready");
            Assert.Equal(0, context.Errors.Recent.Count, "no errors reported");
        }));

        tests.Add(("singleton: claiming blocks the client's CreateEvent call", () =>
        {
            // The mechanism itself: the client creates a named event, so a name already taken by
            // our mutex makes CreateEventW fail, which disables its single-process guard.
            string name = "RMC_test_singleton_event_" + Guid.NewGuid().ToString("N");
            using var context = new TestContext("singleton-event");
            using var guard = new NamedSingletonGuard(context.Errors, context.Logger, [name]);
            guard.Claim();
            Assert.True(guard.IsMultiInstanceReady, Assert.Describe(guard));

            IntPtr handle = CreateEventW(IntPtr.Zero, manualReset: false, initialState: false, name);
            int error = Marshal.GetLastWin32Error();
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
            }

            Assert.Equal(IntPtr.Zero, handle, $"CreateEventW on a claimed name must fail (win32 error {error})");
            Assert.NotEqual(0, error, "Windows reports a specific error");
        }));

        tests.Add(("singleton: a second guard sees the names as already held", () =>
        {
            IReadOnlyList<string> names = SyntheticNames(Guid.NewGuid().ToString("N"));
            using var context = new TestContext("singleton-second");
            using (var first = new NamedSingletonGuard(context.Errors, context.Logger, names))
            {
                first.Claim();
                Assert.True(first.IsMultiInstanceReady, Assert.Describe(first));

                using var second = new NamedSingletonGuard(context.Errors, context.Logger, names);
                second.Claim();

                Assert.True(second.IsMultiInstanceReady, Assert.Describe(second));
                Assert.True(
                    second.Claims.All(claim => claim.State == SingletonClaimState.AlreadyHeld),
                    "second guard observes the existing holder: " + Assert.Describe(second));
            }
        }));

        tests.Add(("singleton: releasing gives both names back, claiming takes them again", () =>
        {
            IReadOnlyList<string> names = SyntheticNames(Guid.NewGuid().ToString("N"));
            using var context = new TestContext("singleton-release");
            using var guard = new NamedSingletonGuard(context.Errors, context.Logger, names);
            guard.Claim();
            Assert.True(guard.IsMultiInstanceReady, Assert.Describe(guard));

            guard.Release();

            Assert.False(guard.IsMultiInstanceReady, "nothing is held after release");
            Assert.True(
                guard.Claims.All(claim => claim.State == SingletonClaimState.Released),
                Assert.Describe(guard));
            foreach (string name in names)
            {
                using var free = new Mutex(initiallyOwned: false, name, out bool createdNew);
                Assert.True(createdNew, $"'{name}' is free after release");
            }

            guard.Claim();
            Assert.True(guard.IsMultiInstanceReady, "claiming after release works: " + Assert.Describe(guard));
        }));

        tests.Add(("singleton: a client-held name is reported, then reclaimed once the client is gone", () =>
        {
            // A running client's event occupies the name: that is a state to report, never a crash.
            // The guard must also not keep that event object alive, or every later client would be
            // refused after this one exits, so the replay below has to be able to create it again.
            string name = "RMC_test_singleton_event_" + Guid.NewGuid().ToString("N");
            using var context = new TestContext("singleton-reclaim");
            var clientEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            using var guard = new NamedSingletonGuard(context.Errors, context.Logger, [name]);

            guard.Claim();
            Assert.Equal(SingletonClaimState.HeldByEvent, guard.Claims[0].State, Assert.Describe(guard));
            Assert.False(guard.IsMultiInstanceReady, "a client-held event is not multi-instance ready");
            Assert.Equal(0, context.Errors.Recent.Count, "holding by a client is not an error");

            clientEvent.Dispose();
            using (var replay = new EventWaitHandle(false, EventResetMode.AutoReset, name, out bool createdNew))
            {
                Assert.True(createdNew, "the guard must not keep the client's event object alive");
            }

            guard.Reclaim();

            Assert.Equal(SingletonClaimState.Claimed, guard.Claims[0].State, Assert.Describe(guard));
            Assert.True(guard.IsMultiInstanceReady, "the name is reclaimed once the client is gone");
        }));
    }
}
