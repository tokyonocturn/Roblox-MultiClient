// A harmless stand-in for RobloxPlayerBeta.exe used by the automated tests.
//
// With --singleton-event <name> it reproduces the real client's single-instance guard: it creates
// the named kernel event and refuses to start when another client already holds it. A failed
// creation (for example because a multi-instance helper holds the name with a different object
// type) disables the guard, which is the client's own CreateEventFailure path.
using System.Runtime.InteropServices;

namespace FakeRobloxClient;

internal static class Program
{
    private const int ErrorAlreadyExists = 183;
    private const int RefusedExitCode = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr attributes, bool manualReset, bool initialState, string name);

    private static int Main(string[] args)
    {
        int exitAfterMs = 0;
        int exitCode = 0;
        string? singletonEvent = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--exit-after":
                    _ = int.TryParse(args[i + 1], out exitAfterMs);
                    break;
                case "--exit-code":
                    _ = int.TryParse(args[i + 1], out exitCode);
                    break;
                case "--singleton-event":
                    singletonEvent = args[i + 1];
                    break;
            }
        }

        if (singletonEvent is not null)
        {
            IntPtr handle = CreateEventW(IntPtr.Zero, manualReset: false, initialState: false, singletonEvent);
            int error = Marshal.GetLastWin32Error();

            if (handle != IntPtr.Zero && error == ErrorAlreadyExists)
            {
                Console.Error.WriteLine($"refused: another client already holds '{singletonEvent}'");
                return RefusedExitCode;
            }

            // Any handle is deliberately kept for the lifetime of the process: it is the claim. A
            // zero handle means the name belongs to another object type, so the guard is skipped.
        }

        if (exitAfterMs > 0)
        {
            Thread.Sleep(exitAfterMs);
            return exitCode;
        }

        while (true)
        {
            Thread.Sleep(200);
        }
    }
}
