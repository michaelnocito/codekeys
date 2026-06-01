using System.Drawing.Drawing2D;

namespace CodeKeys.App.UI.Controls;

/// <summary>
/// A thin, flat horizontal slider (rounded track, accent-filled progress, round knob) — replaces the
/// dated WinForms TrackBar so the Mix section matches a modern phone slider. Min/Max/Value + a
/// <see cref="ValueChanged"/> event mirror the bits of TrackBar the window actually used.
/// </summary>
public sealed class FlatSlider : Control
{
    private int _min, _max = 100, _value;
    private bool _drag;

    public event EventHandler? ValueChanged;

    public Color AccentColor { get; set; } = Color.FromArgb(10, 90, 200);
    public Color TrackColor { get; set; } = Color.FromArgb(228, 228, 233);

    public FlatSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Height = 28;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    public int Minimum { get => _min; set { _min = value; Invalidate(); } }
    public int Maximum { get => _max; set { _max = value; Invalidate(); } }

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Min(_max, Math.Max(_min, value));
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private const int Knob = 16;
    private int TrackLeft => Knob / 2;
    private int TrackRight => Width - Knob / 2;
    private int TrackSpan => Math.Max(1, TrackRight - TrackLeft);

    private void SetFromX(int x)
    {
        double t = (x - TrackLeft) / (double)TrackSpan;
        Value = _min + (int)Math.Round(t * (_max - _min));
    }

    protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int cy = Height / 2;
        const int th = 4;
        var track = new Rectangle(TrackLeft, cy - th / 2, TrackSpan, th);

        double frac = (_value - _min) / (double)Math.Max(1, _max - _min);
        int knobX = TrackLeft + (int)Math.Round(frac * TrackSpan);

        using (var tb = new SolidBrush(TrackColor))
        using (var path = Rounded(track, th / 2))
            g.FillPath(tb, path);

        var fill = new Rectangle(TrackLeft, cy - th / 2, Math.Max(1, knobX - TrackLeft), th);
        using (var ab = new SolidBrush(AccentColor))
        using (var path = Rounded(fill, th / 2))
            g.FillPath(ab, path);

        using var knobBrush = new SolidBrush(Color.White);
        using var knobPen = new Pen(AccentColor, 2f);
        var kr = new Rectangle(knobX - Knob / 2, cy - Knob / 2, Knob, Knob);
        g.FillEllipse(knobBrush, kr);
        g.DrawEllipse(knobPen, kr);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Max(2, radius * 2);
        var p = new GraphicsPath();
        if (r.Width <= d) { p.AddEllipse(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }
}
