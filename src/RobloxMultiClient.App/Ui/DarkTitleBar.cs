using System.Runtime.InteropServices;

namespace RobloxMultiClient.App.Ui;

/// <summary>
/// Asks the window manager for a dark title bar so the frame matches the app. Purely a local
/// Win32 call: older Windows versions ignore it and the app keeps the standard frame.
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Apply(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (!form.IsHandleCreated)
        {
            return;
        }

        int enabled = 1;
        try
        {
            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // No DWM (older Windows): keep the system frame.
        }
        catch (EntryPointNotFoundException)
        {
            // Same as above; the call simply is not available.
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
