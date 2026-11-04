using System.Reflection;
using RobloxMultiClient.App.Services;
using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Windows;

namespace RobloxMultiClient.Tests.Tests;

internal static class PrivacyTests
{
    private static readonly string[] ForbiddenSourceTokens =
    [
        "HttpClient",
        "WebClient",
        "WebRequest",
        "System.Net",
        "Dns.GetHost",
        "Socket(",
        "https://",
        "http://"
    ];

    public static void Register(List<(string Name, Action Body)> tests)
    {
        tests.Add(("privacy: shipped assemblies reference no networking stack", () =>
        {
            Assembly[] assemblies =
            [
                typeof(ClientMonitor).Assembly,
                typeof(WindowsClientWatcher).Assembly,
                typeof(AppServices).Assembly
            ];

            foreach (Assembly assembly in assemblies)
            {
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    string name = reference.Name ?? string.Empty;
                    Assert.False(
                        name.StartsWith("System.Net", StringComparison.OrdinalIgnoreCase),
                        $"{assembly.GetName().Name} references {name}");
                }
            }
        }));

        tests.Add(("privacy: only framework assemblies are referenced", () =>
        {
            Assembly[] assemblies =
            [
                typeof(ClientMonitor).Assembly,
                typeof(WindowsClientWatcher).Assembly,
                typeof(AppServices).Assembly
            ];

            foreach (Assembly assembly in assemblies)
            {
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    string name = reference.Name ?? string.Empty;
                    bool framework = name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("RobloxMultiClient", StringComparison.OrdinalIgnoreCase)
                        || name is "mscorlib" or "netstandard" or "WindowsBase";
                    Assert.True(framework, $"{assembly.GetName().Name} depends on non-framework '{name}'");
                }
            }
        }));

        tests.Add(("privacy: application source contains no network or URL calls", () =>
        {
            string root = RepositoryRoot();
            string sourceRoot = Path.Combine(root, "src");
            var offenders = new List<string>();

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                foreach (string token in ForbiddenSourceTokens)
                {
                    if (text.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add($"{Path.GetFileName(file)}::{token}");
                    }
                }
            }

            Assert.Equal(0, offenders.Count, "network-related tokens found: " + string.Join(", ", offenders));
        }));
    }

    internal static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RobloxMultiClient.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new AssertionException("could not locate the repository root (RobloxMultiClient.sln)");
    }
}
