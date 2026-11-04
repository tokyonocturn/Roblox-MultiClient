using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Settings;
using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests.Tests;

internal static class SettingsTests
{
    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("settings: window size persists across a restart", () =>
        {
            using var context = new TestContext("settings-persist");
            context.Settings.Update(current => current with { WindowWidth = 1200, WindowHeight = 720 });

            // A brand-new service over the same file models the next application start.
            var reloaded = new SettingsService(
                new JsonSettingsStore(context.Paths.SettingsPath, context.Errors, context.Logger),
                context.Errors,
                context.Logger);

            Assert.Equal(1200, reloaded.Current.WindowWidth, "window width survived");
            Assert.Equal(720, reloaded.Current.WindowHeight, "window height survived");
            Assert.True(File.Exists(context.Paths.SettingsPath), "settings file exists on disk");
        }));

        tests.Add(("settings: corrupt file falls back to defaults and reports", () =>
        {
            using var context = new TestContext("settings-corrupt");
            File.WriteAllText(context.Paths.SettingsPath, "{ this is not json");

            var store = new JsonSettingsStore(context.Paths.SettingsPath, context.Errors, context.Logger);
            AppSettings loaded = store.Load();

            Assert.Equal(new AppSettings().WindowWidth, loaded.WindowWidth, "defaults are used");
            Assert.Single(context.Errors.Recent, AppErrorKind.SettingsUnreadable);
        }));

        tests.Add(("settings: window size is clamped to what the window can be", () =>
        {
            var normalized = new AppSettings { WindowWidth = 10, WindowHeight = 99999 }.Normalized();

            Assert.Equal(AppSettings.MinimumWindowWidth, normalized.WindowWidth, "minimum width enforced");
            Assert.Equal(3000, normalized.WindowHeight, "maximum height enforced");
        }));
    }
}
