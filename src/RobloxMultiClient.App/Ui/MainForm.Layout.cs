using System.Drawing;
using RobloxMultiClient.Core.Settings;

namespace RobloxMultiClient.App.Ui;

public sealed partial class MainForm
{
    private PictureBox _logo = null!;
    private Label _brandName = null!;
    private Label _brandProduct = null!;
    private Label _statusCaption = null!;
    private StatusChip _chipMultiInstance = null!;
    private StatusChip _chipClients = null!;
    private Label _versionLabel = null!;
    private FlatButton _quitButton = null!;
    private Label _headerLabel = null!;
    private Label _subheaderLabel = null!;
    private FlatButton _toggleButton = null!;
    private FlatButton _refreshButton = null!;
    private ClientListView _clientList = null!;
    private Label _statusLabel = null!;
    private Label _summaryLabel = null!;
    private Label _logLabel = null!;
    private Label _errorLabel = null!;
    private FlatButton _detailsButton = null!;
    private NotifyIcon _trayIcon = null!;
    private ToolStripMenuItem _trayToggle = null!;

    private void InitializeUi(AppSettings settings)
    {
        SuspendLayout();

        Text = "Roblox MultiClient";
        BackColor = Theme.Ink;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        MinimumSize = new Size(AppSettings.MinimumWindowWidth, AppSettings.MinimumWindowHeight);
        Size = new Size(settings.WindowWidth, settings.WindowHeight);
        StartPosition = FormStartPosition.CenterScreen;
        // Pixel-based layout: scale by DPI, never by the form's font baseline.
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        try
        {
            Icon = Assets.LoadIcon("camo-icon.ico");
        }
        catch (InvalidOperationException)
        {
            // A missing icon must not stop the app from starting; the form keeps the default.
        }

        var sidebar = BuildSidebar();
        var content = BuildContent();

        Controls.Add(content);
        Controls.Add(sidebar);

        WireEvents();
        ResumeLayout(true);

        // Sit the version line just above Quit instead of floating in the empty sidebar.
        _versionLabel.Top = _quitButton.Top - _versionLabel.Height - 10;
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 228,
            BackColor = Theme.Surface,
            Padding = new Padding(20)
        };

        _logo = new PictureBox
        {
            Location = new Point(20, 22),
            Size = new Size(58, 58),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            _logo.Image = Assets.LoadImage("camo-logo.png");
        }
        catch (InvalidOperationException)
        {
            _logo.BackColor = Theme.Moss;
        }

        _brandName = new Label
        {
            Text = "CAMO",
            Location = new Point(20, 88),
            AutoSize = true,
            Font = Theme.Body(15f, FontStyle.Bold),
            ForeColor = Theme.Text
        };

        _brandProduct = new Label
        {
            Text = "ROBLOX MULTICLIENT",
            Location = new Point(21, 118),
            AutoSize = true,
            Font = Theme.Body(7.5f, FontStyle.Bold),
            ForeColor = Theme.Sand
        };

        var separator = new Panel
        {
            Location = new Point(20, 150),
            Size = new Size(188, 1),
            BackColor = Theme.Border
        };

        _statusCaption = new Label
        {
            Text = "STATUS",
            Location = new Point(20, 164),
            AutoSize = true,
            Font = Theme.Body(7.5f, FontStyle.Bold),
            ForeColor = Theme.Muted
        };

        _chipMultiInstance = new StatusChip("Multi-instance") { Location = new Point(20, 184), Width = 188 };
        _chipClients = new StatusChip("Clients") { Location = new Point(20, 220), Width = 188 };

        _versionLabel = new Label
        {
            Text = $"v{typeof(MainForm).Assembly.GetName().Version?.ToString(3)}",
            Location = new Point(20, sidebar.Height - 122),
            AutoSize = true,
            Font = Theme.Mono(8f),
            ForeColor = Theme.Muted,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        _quitButton = new FlatButton("Quit", primary: false)
        {
            Name = "QuitButton",
            Location = new Point(20, sidebar.Height - 54),
            Size = new Size(188, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _quitButton.Click += (_, _) => Quit();

        sidebar.Controls.AddRange([_logo, _brandName, _brandProduct, separator, _statusCaption,
            _chipMultiInstance, _chipClients, _versionLabel, _quitButton]);
        return sidebar;
    }

    private Control BuildContent()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Ink,
            Padding = new Padding(28, 20, 28, 12),
            ColumnCount = 1,
            RowCount = 4
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));

        table.Controls.Add(BuildHeader(), 0, 0);
        table.Controls.Add(BuildToolbar(), 0, 1);
        table.Controls.Add(BuildClientList(), 0, 2);
        table.Controls.Add(BuildFooter(), 0, 3);
        return table;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ink };

        _headerLabel = new Label
        {
            Text = "Multi-instance",
            Location = new Point(0, 0),
            AutoSize = true,
            Font = Theme.Body(18f, FontStyle.Bold),
            ForeColor = Theme.Text
        };

        _subheaderLabel = new Label
        {
            Text = "Leave this open and start Roblox as usual: the website, your shortcut, or the app.",
            Location = new Point(2, 34),
            AutoSize = true,
            Font = Theme.Body(8.75f),
            ForeColor = Theme.Muted
        };

        panel.Controls.AddRange([_headerLabel, _subheaderLabel]);
        return panel;
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Ink,
            Margin = new Padding(0, 6, 0, 12)
        };

        _toggleButton = new FlatButton("Turn off", primary: true)
        {
            Name = "ToggleButton",
            Width = 140,
            Margin = new Padding(0, 0, 8, 0)
        };

        _refreshButton = new FlatButton("Refresh", primary: false)
        {
            Name = "RefreshButton",
            Width = 96,
            Margin = new Padding(0)
        };

        toolbar.Controls.AddRange([_toggleButton, _refreshButton]);
        return toolbar;
    }

    private Control BuildClientList()
    {
        _clientList = new ClientListView
        {
            Name = "ClientList",
            Dock = DockStyle.Fill,
            EmptyText = "No Roblox clients listed.\r\nStart Roblox normally, then press Refresh; every session stays independent.",
            Margin = new Padding(0, 4, 0, 4)
        };

        _clientList.Columns.Add("PID", 76);
        _clientList.Columns.Add("Version", 196);
        _clientList.Columns.Add("Started", 88);
        _clientList.Columns.Add("Executable", 220);
        return _clientList;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ink };

        _statusLabel = new Label
        {
            Location = new Point(0, 2),
            AutoSize = false,
            Size = new Size(600, 18),
            Font = Theme.Body(9f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoEllipsis = true,
            Text = "Starting…"
        };

        _summaryLabel = new Label
        {
            Location = new Point(0, 24),
            AutoSize = false,
            Size = new Size(600, 18),
            Font = Theme.Body(8.5f),
            ForeColor = Theme.Muted,
            AutoEllipsis = true
        };

        _logLabel = new Label
        {
            Location = new Point(0, 44),
            AutoSize = false,
            Size = new Size(600, 16),
            Font = Theme.Mono(8f),
            ForeColor = Theme.Muted,
            AutoEllipsis = true
        };

        _errorLabel = new Label
        {
            Location = new Point(0, 66),
            AutoSize = false,
            Size = new Size(600, 18),
            Font = Theme.Body(8.75f, FontStyle.Bold),
            ForeColor = Theme.Danger,
            AutoEllipsis = true,
            Visible = false
        };

        _detailsButton = new FlatButton("Details", primary: false)
        {
            Name = "ErrorDetailsButton",
            Width = 78,
            Height = 22,
            Location = new Point(footer.Width - 78, 64),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };
        _detailsButton.Click += (_, _) => ShowErrorDetails();

        footer.Controls.AddRange([_statusLabel, _summaryLabel, _logLabel, _errorLabel, _detailsButton]);
        return footer;
    }

    private void BuildTrayIcon()
    {
        _trayToggle = new ToolStripMenuItem("Turn multi-instance off");
        _trayToggle.Click += (_, _) => _presenter.Toggle();

        var show = new ToolStripMenuItem("Show status window");
        show.Click += (_, _) => ShowFromTray();

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => Quit();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([show, new ToolStripSeparator(), _trayToggle, new ToolStripSeparator(), exit]);

        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "Roblox MultiClient",
            ContextMenuStrip = menu,
            Visible = false
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ShowErrorDetails()
    {
        using var dialog = new ErrorDetailsForm(_services.Errors.Recent, _services.Paths.DataDirectory, _services.Logger);
        dialog.ShowDialog(this);
    }
}
