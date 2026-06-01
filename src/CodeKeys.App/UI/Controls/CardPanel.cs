using System.Drawing.Drawing2D;

namespace CodeKeys.App.UI.Controls;

/// <summary>
/// A white rounded-rectangle "card" with a hairline border — the grouping container used to give the
/// window an iOS-settings feel (sections of controls inside soft cards) while keeping Mike's white,
/// minimal palette. Child controls are added normally; the card just paints the rounded backdrop.
/// </summary>
public sealed class CardPanel : Panel
{
    public int Radius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.FromArgb(233, 233, 238);
    public Color FillColor { get; set; } = Color.White;

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Rounded(r, Radius);
        using (var fill = new SolidBrush(FillColor)) g.FillPath(fill, path);
        using var pen = new Pen(BorderColor, 1f);
        g.DrawPath(pen, path);

        base.OnPaint(e);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
