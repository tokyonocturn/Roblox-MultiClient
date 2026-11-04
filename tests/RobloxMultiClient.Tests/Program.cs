using System.Diagnostics;
using RobloxMultiClient.Tests.Tests;

namespace RobloxMultiClient.Tests;

/// <summary>
/// Zero-dependency test runner. Exists instead of a test framework so the project restores and
/// builds with no package feed, keeping the build reproducible and the dependency audit trivial.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var tests = new List<(string Name, Action Body)>();
        SettingsTests.Register(tests);
        ClientMonitorTests.Register(tests);
        SingletonTests.Register(tests);
        ThreeStateTests.Register(tests);
        LoggingTests.Register(tests);
        PrivacyTests.Register(tests);
        UiTests.Register(tests);

        bool verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);
        int failed = 0;
        var total = Stopwatch.StartNew();

        foreach ((string name, Action body) in tests)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                body();
                Console.WriteLine($"PASS  {name}  ({watch.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL  {name}  ({watch.ElapsedMilliseconds} ms)");
                Console.WriteLine("      " + (verbose ? ex.ToString() : ex.Message).Replace("\n", "\n      "));
            }
        }

        total.Stop();
        Console.WriteLine();
        Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed in {total.Elapsed.TotalSeconds:0.0}s");
        return failed == 0 ? 0 : 1;
    }
}
