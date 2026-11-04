using System.Drawing;
using System.Drawing.Drawing2D;

namespace RobloxMultiClient.App.Ui;

internal enum ChipState
{
    Ok,
    Warning,
    Error,
    Neutral
}

/// <summary>A dark pill with a coloured dot, used for singleton/installation/client indicators.</summary>
internal sealed class StatusChip : Control
{
    private ChipState _state = ChipState.Neutral;

    public StatusChip(string caption)
    {
        Caption = caption;
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        Height = 30;
        Font = Theme.Body(8.75f);
        ForeColor = Theme.Text;
        BackColor = Color.Transparent;
    }

    public string Caption { get; }

    public string Value { get; private set; } = "-";

    public void Set(string value, ChipState state)
    {
        Value = value;
        _state = state;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = RoundedRect(bounds, 8))
        using (var fill = new SolidBrush(Theme.SurfaceAlt))
        using (var pen = new Pen(Theme.Border))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        Color dot = _state switch
        {
            ChipState.Ok => Theme.Ok,
            ChipState.Warning => Theme.Warn,
            ChipState.Error => Theme.Danger,
            _ => Theme.Muted
        };

        using (var dotBrush = new SolidBrush(dot))
        {
            g.FillEllipse(dotBrush, 11, Height / 2 - 3, 7, 7);
        }

        using var textBrush = new SolidBrush(Theme.Text);
        using var captionBrush = new SolidBrush(Theme.Muted);
        var captionRect = new Rectangle(26, 0, Width - 34, Height);
        var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(Caption, Font, captionBrush, captionRect, format);
        float captionWidth = g.MeasureString(Caption + " ", Font).Width;
        var valueRect = new Rectangle(26 + (int)captionWidth, 0, Math.Max(10, Width - 34 - (int)captionWidth), Height);
        g.DrawString(Value, Font, textBrush, valueRect, format);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
