using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests.Tests;

/// <summary>
/// The app's whole purpose in three states, exercised against a fictive client that enforces the
/// same named single-instance object the real client creates: without the claim a second client
/// refuses to start; with the claim two clients run at once; after the claim is released the
/// refusal comes back.
/// </summary>
internal static class ThreeStateTests
{
    private const int RefusedExitCode = 3;

    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("three-state: a second client is refused while the first holds the object", () =>
        {
            using var context = new TestContext("three-refuse");
            string name = context.SingletonEventName;
            string playerPath = context.ClientPath();

            using var first = context.StartClient(playerPath, $"--singleton-event {name}");
            Assert.True(TestContext.WaitForEvent(name), "the first client created the single-instance object");

            using var second = context.StartClient(playerPath, $"--singleton-event {name}");
            Assert.True(second.WaitForExit(8000), "the second client exited instead of running");
            Assert.Equal(RefusedExitCode, second.ExitCode, "the second client refused to start");
            Assert.True(TestContext.IsAlive(first.Id), "the first client kept running");
        }));

        tests.Add(("three-state: with the claim held, two clients run at once", () =>
        {
            using var context = new TestContext("three-claim");
            string name = context.SingletonEventName;
            string playerPath = context.ClientPath();

            using var guard = new NamedSingletonGuard(context.Errors, context.Logger, [name]);
            guard.Claim();
            Assert.True(guard.IsMultiInstanceReady, Assert.Describe(guard));

            using var first = context.StartClient(playerPath, $"--singleton-event {name}");
            using var second = context.StartClient(playerPath, $"--singleton-event {name}");

            Thread.Sleep(1200);
            Assert.True(TestContext.IsAlive(first.Id), "the first client is running under the claim");
            Assert.True(TestContext.IsAlive(second.Id), "the second client is running under the claim");
            Assert.False(TestContext.WaitForEvent(name, 700), "no client could create the object while it is claimed");
        }));

        tests.Add(("three-state: the refusal returns after the claim is released", () =>
        {
            using var context = new TestContext("three-release");
            string name = context.SingletonEventName;
            string playerPath = context.ClientPath();

            var guard = new NamedSingletonGuard(context.Errors, context.Logger, [name]);
            guard.Claim();
            Assert.True(guard.IsMultiInstanceReady, Assert.Describe(guard));
            guard.Dispose();

            using var first = context.StartClient(playerPath, $"--singleton-event {name}");
            Assert.True(TestContext.WaitForEvent(name), "the first client claimed the object after release");

            using var second = context.StartClient(playerPath, $"--singleton-event {name}");
            Assert.True(second.WaitForExit(8000), "the second client exited");
            Assert.Equal(RefusedExitCode, second.ExitCode, "the refusal is back after the claim was released");
        }));
    }
}
