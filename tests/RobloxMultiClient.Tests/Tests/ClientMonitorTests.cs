using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Errors;

namespace RobloxMultiClient.Tests.Tests;

internal static class ClientMonitorTests
{
    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("clients: the list mirrors what the watcher reports", () =>
        {
            using var context = new TestContext("clients-list");
            var watcher = new FakeClientWatcher();
            using var monitor = new ClientMonitor(watcher, context.Errors, context.Logger);

            watcher.Add(1201);
            watcher.Add(1202);
            monitor.Refresh();

            Assert.Equal(2, monitor.Clients.Count, "both clients are tracked");
            Assert.Equal(1201, monitor.Clients[0].ProcessId, "order is stable");
        }));

        tests.Add(("clients: a change raises one event, an unchanged list raises none", () =>
        {
            using var context = new TestContext("clients-changed");
            var watcher = new FakeClientWatcher();
            using var monitor = new ClientMonitor(watcher, context.Errors, context.Logger);
            int changes = 0;
            monitor.Changed += (_, _) => changes++;

            monitor.Refresh();
            Assert.Equal(0, changes, "an empty list is not a change");

            watcher.Add(2201);
            monitor.Refresh();
            Assert.Equal(1, changes, "a new client raises one change");

            monitor.Refresh();
            Assert.Equal(1, changes, "the same client is not a change again");
        }));

        tests.Add(("clients: an exit is reported with its code, and a crash becomes an error", () =>
        {
            using var context = new TestContext("clients-exit");
            var watcher = new FakeClientWatcher();
            using var monitor = new ClientMonitor(watcher, context.Errors, context.Logger);
            watcher.Add(3301);
            monitor.Refresh();

            watcher.Remove(3301);
            watcher.RaiseExit(3301, 0);
            Assert.Equal(0, context.Errors.Recent.Count, "a clean exit is not an error");
            Assert.Equal(0, monitor.Clients.Count, "the exited client left the list");

            watcher.Add(3302);
            monitor.Refresh();
            watcher.Remove(3302);
            watcher.RaiseExit(3302, 120);
            Assert.Single(context.Errors.Recent, AppErrorKind.ClientCrashed);
        }));

        tests.Add(("clients: a watcher that fails reports instead of throwing", () =>
        {
            using var context = new TestContext("clients-fail");
            var watcher = new FakeClientWatcher { FailWith = new InvalidOperationException("access denied") };
            using var monitor = new ClientMonitor(watcher, context.Errors, context.Logger);

            monitor.Refresh();

            Assert.Single(context.Errors.Recent, AppErrorKind.ClientQueryFailed);
            Assert.Equal(0, monitor.Clients.Count, "the last good list is kept");
        }));
    }
}
