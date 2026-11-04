using System.ComponentModel;
using System.Drawing;

namespace RobloxMultiClient.App.Ui;

/// <summary>
/// A details ListView painted entirely by the application, so the list matches the Camo palette
/// instead of the system window colours. Column order is fixed:
/// Client | PID | Version | Started | Executable.
/// </summary>
internal sealed class ClientListView : ListView
{
    private const int TextPadding = 10;
    private const int MinimumLastColumnWidth = 64;

    private static readonly Font CellFont = Theme.Body(9f);
    private static readonly Font EmphasisedFont = Theme.Body(9f, FontStyle.Bold);
    private static readonly Font HeaderFont = Theme.Body(8.25f, FontStyle.Bold);

    public ClientListView()
    {
        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        MultiSelect = false;
        HideSelection = true;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = CellFont;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>Shown in place of rows while no clients are running.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = string.Empty;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        StretchLastColumn();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        StretchLastColumn();
    }

    /// <summary>
    /// Lets the last column consume the leftover width. The header is a child window painted by the
    /// system, so the only way to avoid its light filler strip at the right edge is to leave it no
    /// leftover width at all.
    /// </summary>
    private void StretchLastColumn()
    {
        if (Columns.Count == 0 || ClientSize.Width <= 0)
        {
            return;
        }

        int others = Columns.Cast<ColumnHeader>().Take(Columns.Count - 1).Sum(column => column.Width);
        int last = Math.Max(MinimumLastColumnWidth, ClientSize.Width - others);
        if (Columns[Columns.Count - 1].Width != last)
        {
            Columns[Columns.Count - 1].Width = last;
        }
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var fill = new SolidBrush(Theme.SurfaceAlt))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        using (var border = new Pen(Theme.Border))
        {
            e.Graphics.DrawLine(
                border,
                e.Bounds.Left,
                e.Bounds.Bottom - 1,
                e.Bounds.Right,
                e.Bounds.Bottom - 1);
        }

        var text = new Rectangle(
            e.Bounds.Left + TextPadding,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - TextPadding * 2),
            e.Bounds.Height);

        TextRenderer.DrawText(
            e.Graphics,
            e.Header?.Text ?? string.Empty,
            HeaderFont,
            text,
            Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        if (e.Item is null)
        {
            return;
        }

        var row = new Rectangle(0, e.Bounds.Top, ClientSize.Width, e.Bounds.Height);
        using (var fill = new SolidBrush(e.Item.Index % 2 == 1 ? Theme.SurfaceAlt : Theme.Surface))
        {
            e.Graphics.FillRectangle(fill, row);
        }

        // Cells are placed from the column widths rather than from sub-item bounds: the row paints
        // as one unit, so no column can be skipped or clipped by the control's own layout pass.
        int x = 0;
        for (int column = 0; column < Columns.Count && column < e.Item.SubItems.Count; column++)
        {
            int width = Columns[column].Width;
            DrawCellText(e.Graphics, e.Item, column, new Rectangle(x, e.Bounds.Top, width, e.Bounds.Height));
            x += width;
        }
    }

    private static void DrawCellText(Graphics graphics, ListViewItem item, int columnIndex, Rectangle bounds)
    {
        if (columnIndex >= item.SubItems.Count)
        {
            return;
        }

        var text = new Rectangle(
            bounds.Left + TextPadding,
            bounds.Top,
            Math.Max(0, bounds.Width - TextPadding * 2),
            bounds.Height);

        TextRenderer.DrawText(
            graphics,
            item.SubItems[columnIndex].Text,
            columnIndex == 0 ? EmphasisedFont : CellFont,
            text,
            Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (Items.Count > 0 || string.IsNullOrEmpty(EmptyText))
        {
            return;
        }

        var area = new Rectangle(
            TextPadding,
            0,
            Math.Max(0, ClientSize.Width - TextPadding * 2),
            ClientSize.Height);

        TextRenderer.DrawText(
            e.Graphics,
            EmptyText,
            CellFont,
            area,
            Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}
