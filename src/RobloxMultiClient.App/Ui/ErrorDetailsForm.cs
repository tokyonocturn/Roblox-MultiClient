using System.Diagnostics;
using System.Drawing;
using System.Text;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Logging;

namespace RobloxMultiClient.App.Ui;

/// <summary>Shows the recent failures verbatim, with the log location for follow-up.</summary>
internal sealed class ErrorDetailsForm : Form
{
    private readonly string _dataDirectory;

    public ErrorDetailsForm(IReadOnlyList<AppError> errors, string dataDirectory, IAppLogger logger)
    {
        _dataDirectory = dataDirectory;

        Text = "Recent problems, Roblox MultiClient";
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(620, 380);

        var list = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(18, 18),
            Size = new Size(584, 290),
            BackColor = Theme.SurfaceAlt,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Mono(8.25f),
            Text = Format(errors, logger)
        };

        var copy = new FlatButton("Copy", primary: false) { Location = new Point(18, 322), Width = 90 };
        copy.Click += (_, _) =>
        {
            if (list.TextLength > 0)
            {
                Clipboard.SetText(list.Text);
            }
        };

        var openLogs = new FlatButton("Open logs", primary: false) { Location = new Point(116, 322), Width = 110 };
        openLogs.Click += (_, _) => OpenLogs(logger);

        var close = new FlatButton("Close", primary: true) { Location = new Point(502, 322), Width = 100 };
        close.Click += (_, _) => Close();

        Controls.AddRange([list, copy, openLogs, close]);
    }

    private static string Format(IReadOnlyList<AppError> errors, IAppLogger logger)
    {
        var text = new StringBuilder();
        if (errors.Count == 0)
        {
            text.AppendLine("No problems have been recorded in this session.");
        }
        else
        {
            foreach (AppError error in errors.Reverse())
            {
                text.Append(error.OccurredUtc.ToLocalTime().ToString("HH:mm:ss"))
                    .Append("  ")
                    .AppendLine(error.Kind.ToString());
                text.AppendLine("  " + error.Describe());
                if (error.Exception is not null)
                {
                    text.AppendLine("  " + error.Exception.GetType().Name + ": " + error.Exception.Message);
                }

                text.AppendLine();
            }
        }

        text.AppendLine("Log file: " + (logger.CurrentLogFile ?? Path.Combine(logger.LogDirectory, "(not written yet)")));
        return text.ToString();
    }

    private void OpenLogs(IAppLogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = logger.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            _ = _dataDirectory;
        }
    }
}
