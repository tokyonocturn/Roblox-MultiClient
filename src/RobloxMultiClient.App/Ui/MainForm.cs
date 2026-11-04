using RobloxMultiClient.App.Services;
using RobloxMultiClient.App.Status;
using RobloxMultiClient.Core.Errors;

namespace RobloxMultiClient.App.Ui;

/// <summary>
/// The status window. It renders snapshots produced by <see cref="StatusPresenter"/> and forwards
/// user gestures back to it; it owns no application state.
/// </summary>
public sealed partial class MainForm : Form, IStatusView
{
    private readonly AppServices _services;
    private readonly StatusPresenter _presenter;
    private Size _lastNormalSize;
    private bool _exiting;
    private bool _balloonShown;

    public MainForm(AppServices services, StatusPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(presenter);

        _services = services;
        _presenter = presenter;

        InitializeUi(services.Settings.Current);
        BuildTrayIcon();

        _presenter.Attach(this);
        _presenter.Enable();
    }

    public void Render(StatusSnapshot snapshot)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Render(snapshot)));
            return;
        }

        RenderChips(snapshot);
        RenderControls(snapshot);
        RenderClients(snapshot);
        RenderFooter(snapshot);
    }

    public void ShowError(AppError error)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowError(error)));
            return;
        }

        _errorLabel.Text = error.Describe();
        _errorLabel.Visible = true;
        _detailsButton.Visible = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Apply(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _trayIcon.Visible = true;
        _services.Logger.Info($"Status window shown at {Width}x{Height}, {DeviceDpi} dpi");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Normal && Width >= MinimumSize.Width && Height >= MinimumSize.Height)
        {
            _lastNormalSize = Size;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window keeps multi-instance on: that is the point of the app. Quit ends it, and
        // a Windows shutdown must be allowed through.
        // Windows shutdown must be allowed through; everything else hides the window. A WM_CLOSE
        // from outside arrives as TaskManagerClosing, which still means "the user closed it" here.
        bool shutdown = e.CloseReason is CloseReason.WindowsShutDown or CloseReason.ApplicationExitCall;
        if (!_exiting && !shutdown)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        Size size = _lastNormalSize.IsEmpty ? Size : _lastNormalSize;
        if (size.Width >= MinimumSize.Width && size.Height >= MinimumSize.Height)
        {
            _presenter.SaveWindowSize(size.Width, size.Height);
        }

        _presenter.Dispose();
        base.OnFormClosing(e);
    }

    private void HideToTray()
    {
        Hide();
        if (_balloonShown)
        {
            return;
        }

        _balloonShown = true;
        _trayIcon.ShowBalloonTip(
            5000,
            "Roblox MultiClient is still running",
            "Multi-instance stays on. Right-click the tray icon to turn it off or exit.",
            ToolTipIcon.Info);
    }

    private void Quit()
    {
        _exiting = true;
        Close();
    }
}
