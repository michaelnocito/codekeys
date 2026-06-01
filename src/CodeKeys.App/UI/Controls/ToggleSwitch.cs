using System.Drawing.Drawing2D;

namespace CodeKeys.App.UI.Controls;

/// <summary>
/// A minimal iOS-style on/off switch (rounded track + sliding knob). Replaces the chunky WinForms
/// CheckBox so the panel reads like a phone settings screen. Exposes <see cref="Checked"/> +
/// <see cref="CheckedChanged"/> so it is a near drop-in for a CheckBox.
/// </summary>
public sealed class ToggleSwitch : Control
{
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public Color OnColor { get; set; } = Color.FromArgb(10, 90, 200);
    public Color OffColor { get; set; } = Color.FromArgb(206, 206, 214);

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(48, 28);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int h = Height - 2;
        int w = Width - 2;
        var track = new Rectangle(1, 1, w, h);
        using (var path = Rounded(track, h / 2))
        using (var brush = new SolidBrush(_checked ? OnColor : OffColor))
            g.FillPath(brush, path);

        int knob = h - 6;
        int kx = _checked ? Width - knob - 4 : 4;
        using var knobBrush = new SolidBrush(Color.White);
        g.FillEllipse(knobBrush, kx, 4, knob, knob);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }
}
