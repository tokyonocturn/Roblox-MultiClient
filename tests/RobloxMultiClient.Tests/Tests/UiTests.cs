using System.Threading;
using RobloxMultiClient.App.Services;
using RobloxMultiClient.App.Status;
using RobloxMultiClient.App.Ui;
using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Singleton;

namespace RobloxMultiClient.Tests.Tests;

internal static class UiTests
{
    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("ui: the window is an enabler, not a launcher", () => RunSta(() =>
        {
            using var context = new TestContext("ui-form");
            var watcher = new FakeClientWatcher();
            watcher.Add(7777);
            using AppServices services = AppServices.Create(context.Paths, context.SingletonNames, watcher);
            using var presenter = new StatusPresenter(
                services.Clients, services.Guard, services.Settings, services.Errors, services.Logger);

            using var form = new MainForm(services, presenter);
            form.CreateControl();

            Assert.Contains(form.Text, "Roblox MultiClient", "window title is branded");
            Assert.True(form.Icon is not null, "the Camo icon is embedded and loaded");
            Assert.True(form.Visible == false, "the smoke test never shows a window");

            string[] names = form.Controls
                .Cast<Control>()
                .SelectMany(CollectNames)
                .ToArray();
            foreach (string required in new[] { "ToggleButton", "RefreshButton", "QuitButton", "ClientList" })
            {
                Assert.True(names.Contains(required), $"the window has {required}");
            }

            foreach (string launcher in new[] { "LaunchButton", "RestartButton", "CloseAllButton", "InstallCombo", "ArgumentsBox" })
            {
                Assert.False(names.Contains(launcher), $"the window must not offer {launcher}");
            }

            presenter.Refresh();
            var list = (ListView)Find(form, "ClientList")!;
            Assert.Equal(1, list.Items.Count, "the watched client has a row");
            Assert.Equal("7777", list.Items[0].Text, "the row carries the pid");
            Assert.Equal("version-test", list.Items[0].SubItems[1].Text, "the row carries the version");
            Assert.Contains(list.Items[0].SubItems[3].Text, "RobloxPlayerBeta.exe", "the row carries the path");
        })));

        tests.Add(("ui: enabling claims the names, disabling gives them back", () => RunSta(() =>
        {
            using var context = new TestContext("ui-toggle");
            var watcher = new FakeClientWatcher();
            using AppServices services = AppServices.Create(context.Paths, [context.SingletonEventName], watcher);
            var view = new RecordingView();
            using var presenter = new StatusPresenter(
                services.Clients, services.Guard, services.Settings, services.Errors, services.Logger);

            presenter.Attach(view);
            presenter.Enable();

            Assert.True(view.Last!.Enabled, "the snapshot reports multi-instance on");
            Assert.True(view.Last!.Ready, "both names are held");
            Assert.True(
                view.Last!.Claims.All(claim => claim.State == SingletonClaimState.Claimed),
                Assert.Describe(services.Guard));

            presenter.Disable();

            Assert.False(view.Last!.Enabled, "the snapshot reports multi-instance off");
            Assert.False(view.Last!.Ready, "nothing is held any more");
            var probe = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                context.SingletonEventName,
                out bool createdNew);
            probe.Dispose();
            Assert.True(createdNew, "a client can take the name once multi-instance is off");

            presenter.Enable();
            Assert.True(view.Last!.Ready, "turning it on again reclaims the name");
        })));

        tests.Add(("ui: waiting for a client-held name, then taking it over", () => RunSta(() =>
        {
            using var context = new TestContext("ui-waiting");
            var watcher = new FakeClientWatcher();
            using AppServices services = AppServices.Create(context.Paths, [context.SingletonEventName], watcher);
            using var clientEvent = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                context.SingletonEventName);

            var view = new RecordingView();
            using var presenter = new StatusPresenter(
                services.Clients, services.Guard, services.Settings, services.Errors, services.Logger);
            presenter.Attach(view);
            presenter.Enable();

            Assert.False(view.Last!.Ready, "a running client blocks the name");
            Assert.Contains(view.Last!.StatusMessage, "Waiting", "the window explains the wait");

            clientEvent.Dispose();
            presenter.Refresh();

            Assert.True(view.Last!.Ready, "the name is taken over once that client is gone");
            Assert.Contains(view.Last!.StatusMessage, "start Roblox", "the window reports that it is on");
        })));

        tests.Add(("ui: clients the app did not start are listed", () => RunSta(() =>
        {
            using var context = new TestContext("ui-clients");
            var watcher = new FakeClientWatcher();
            using AppServices services = AppServices.Create(context.Paths, context.SingletonNames, watcher);
            var view = new RecordingView();
            using var presenter = new StatusPresenter(
                services.Clients, services.Guard, services.Settings, services.Errors, services.Logger);

            presenter.Attach(view);
            presenter.Enable();
            watcher.Add(4242);
            presenter.Refresh();

            Assert.Equal(1, view.Last!.Clients.Count, "the client is visible");
            Assert.Equal(4242, view.Last!.Clients[0].ProcessId, "with the pid Windows reported");
        })));

        tests.Add(("ui: window size is saved through the presenter", () => RunSta(() =>
        {
            using var context = new TestContext("ui-size");
            var watcher = new FakeClientWatcher();
            using AppServices services = AppServices.Create(context.Paths, context.SingletonNames, watcher);
            using var presenter = new StatusPresenter(
                services.Clients, services.Guard, services.Settings, services.Errors, services.Logger);

            presenter.SaveWindowSize(1024, 640);

            var reloaded = new Core.Settings.SettingsService(
                new Windows.JsonSettingsStore(context.Paths.SettingsPath, context.Errors, context.Logger),
                context.Errors,
                context.Logger);
            Assert.Equal(1024, reloaded.Current.WindowWidth, "width persisted");
            Assert.Equal(640, reloaded.Current.WindowHeight, "height persisted");
        })));
    }

    private static Control? Find(Control root, string name)
    {
        if (root.Name == name)
        {
            return root;
        }

        foreach (Control child in root.Controls)
        {
            Control? match = Find(child, name);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static IEnumerable<string> CollectNames(Control control)
    {
        if (!string.IsNullOrEmpty(control.Name))
        {
            yield return control.Name;
        }

        foreach (Control child in control.Controls)
        {
            foreach (string name in CollectNames(child))
            {
                yield return name;
            }
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new AssertionException("STA thread failed: " + failure);
        }
    }
}
