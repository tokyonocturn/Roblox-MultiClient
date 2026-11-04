using RobloxMultiClient.App.Services;
using RobloxMultiClient.App.Status;
using RobloxMultiClient.App.Ui;
using RobloxMultiClient.Core;
using RobloxMultiClient.Core.Errors;

namespace RobloxMultiClient.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppServices? services = null;
        try
        {
            services = AppServices.Create(AppPaths.Default);

            Application.ThreadException += (_, args) => services.Errors.Report(
                new AppError(AppErrorKind.Unexpected, "Unexpected error", null, args.Exception));

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                services.Logger.Error("Unhandled exception", args.ExceptionObject as Exception);

            using (services)
            using (var presenter = new StatusPresenter(
                       services.Clients,
                       services.Guard,
                       services.Settings,
                       services.Errors,
                       services.Logger))
            using (var form = new MainForm(services, presenter))
            {
                Application.Run(form);
            }
        }
        catch (Exception ex)
        {
            services?.Logger.Error("Fatal startup error", ex);
            MessageBox.Show(
                "Roblox MultiClient could not start.\n\n" + ex.Message +
                "\n\nDetails: " + (services?.Logger.CurrentLogFile ?? AppPaths.Default.LogDirectory),
                "Roblox MultiClient",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
