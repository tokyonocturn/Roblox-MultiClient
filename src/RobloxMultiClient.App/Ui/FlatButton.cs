using System.Drawing;

namespace RobloxMultiClient.App.Ui;

/// <summary>A borderless (primary) or outlined (secondary) button in the Camo palette.</summary>
internal sealed class FlatButton : Button
{
    private readonly Color _idle;
    private readonly Color _hover;

    public FlatButton(string text, bool primary)
    {
        Text = text;
        Primary = primary;
        AutoSize = false;
        Height = 32;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        TabStop = true;

        if (primary)
        {
            _idle = Theme.Olive;
            _hover = Color.FromArgb(0xA5, 0xB8, 0x74);
            ForeColor = Theme.Ink;
            Font = Theme.Body(9f, FontStyle.Bold);
            FlatAppearance.BorderSize = 0;
        }
        else
        {
            _idle = Theme.SurfaceAlt;
            _hover = Theme.SurfaceHover;
            ForeColor = Theme.Text;
            Font = Theme.Body(9f);
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = Theme.Border;
        }

        BackColor = _idle;
        FlatAppearance.MouseOverBackColor = _hover;
        FlatAppearance.MouseDownBackColor = _hover;
    }

    public bool Primary { get; }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? _idle : Theme.SurfaceAlt;
        ForeColor = Enabled ? (Primary ? Theme.Ink : Theme.Text) : Theme.Muted;
        Invalidate();
    }
}
