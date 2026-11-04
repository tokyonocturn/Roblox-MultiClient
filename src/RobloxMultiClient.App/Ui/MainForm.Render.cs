using RobloxMultiClient.App.Status;
using RobloxMultiClient.Core.Clients;

namespace RobloxMultiClient.App.Ui;

public sealed partial class MainForm
{
    private string _renderedClientSignature = string.Empty;

    private void WireEvents()
    {
        _toggleButton.Click += (_, _) => _presenter.Toggle();
        _refreshButton.Click += (_, _) => _presenter.Refresh();
    }

    private void RenderChips(StatusSnapshot snapshot)
    {
        if (!snapshot.Enabled)
        {
            _chipMultiInstance.Set("off", ChipState.Neutral);
        }
        else if (snapshot.Ready)
        {
            _chipMultiInstance.Set("on", ChipState.Ok);
        }
        else
        {
            _chipMultiInstance.Set("waiting", ChipState.Warning);
        }

        int clients = snapshot.Clients.Count;
        _chipClients.Set(clients == 1 ? "1 running" : $"{clients} running",
            clients == 0 ? ChipState.Neutral : ChipState.Ok);
    }

    private void RenderControls(StatusSnapshot snapshot)
    {
        _toggleButton.Text = snapshot.Enabled ? "Turn off" : "Turn on";
        _trayToggle.Text = snapshot.Enabled ? "Turn multi-instance off" : "Turn multi-instance on";
        _trayIcon.Text = snapshot.Ready
            ? "Roblox MultiClient, multi-instance on"
            : "Roblox MultiClient, multi-instance off";
    }

    private void RenderClients(StatusSnapshot snapshot)
    {
        string signature = string.Join(
            "|",
            snapshot.Clients.Select(client => $"{client.ProcessId}:{client.ExecutablePath}"));

        if (signature == _renderedClientSignature)
        {
            return;
        }

        _renderedClientSignature = signature;

        _clientList.BeginUpdate();
        try
        {
            _clientList.Items.Clear();
            foreach (RobloxClientProcess client in snapshot.Clients)
            {
                var item = new ListViewItem(client.ProcessId.ToString());
                item.SubItems.Add(client.DisplayVersion);
                item.SubItems.Add(client.StartedUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "-");
                item.SubItems.Add(client.DisplayPath);
                _clientList.Items.Add(item);
            }
        }
        finally
        {
            _clientList.EndUpdate();
        }
    }

    private void RenderFooter(StatusSnapshot snapshot)
    {
        _statusLabel.Text = snapshot.StatusMessage;

        int clients = snapshot.Clients.Count;
        _summaryLabel.Text = clients switch
        {
            0 => "Nothing listed yet. Open Roblox normally, then press Refresh to list it.",
            1 => "1 client running. Open another whenever you like.",
            _ => $"{clients} clients running independently. This app started none of them and will not close them."
        };

        _logLabel.Text = "Logs: " + UnderLocalAppData(snapshot.LogDirectory);
    }

    /// <summary>Display the data folder relative to %LOCALAPPDATA% instead of a personal path.</summary>
    private static string UnderLocalAppData(string path)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return path.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "%LOCALAPPDATA%" + path[local.Length..]
            : path;
    }
}
