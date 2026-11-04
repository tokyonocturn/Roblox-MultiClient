using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests.Tests;

internal static class LoggingTests
{
    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("logging: entries land in the app-data log file", () =>
        {
            using var context = new TestContext("logging");
            context.Logger.Info("hello from the test");
            context.Logger.Error("something failed", new InvalidOperationException("boom"));

            string? file = context.Logger.CurrentLogFile;
            Assert.True(file is not null, "a log file was created");
            Assert.True(File.Exists(file!), "log file exists on disk");
            Assert.True(file!.StartsWith(context.Paths.LogDirectory, StringComparison.OrdinalIgnoreCase),
                "log lives under the app data folder");

            string content = File.ReadAllText(file);
            Assert.Contains(content, "hello from the test", "info line written");
            Assert.Contains(content, "something failed", "error line written");
            Assert.Contains(content, "InvalidOperationException", "exception type recorded");
        }));

        tests.Add(("logging: logger never throws when the directory is unusable", () =>
        {
            var logger = new FileAppLogger(Path.Combine(Path.GetTempPath(), "multiclient\0bad"));

            // Must not throw: logging failures are swallowed by design.
            logger.Info("still safe");
        }));
    }
}
