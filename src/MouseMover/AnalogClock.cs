using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MouseMover;

public sealed class AnalogClock : Control
{
    private DateTime _time = DateTime.Now;

    public AnalogClock()
    {
        DoubleBuffered = true;
        BackColor = Color.Black;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Time
    {
        get => _time;
        set { _time = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HandColor { get; set; } = Color.Gainsboro;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowSeconds { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float d = Math.Min(Width, Height);
        var center = new PointF(Width / 2f, Height / 2f);
        float r = d / 2f - d * 0.06f;

        using var pen = new Pen(HandColor, Math.Max(2f, d * 0.012f));
        g.DrawEllipse(pen, center.X - r, center.Y - r, r * 2, r * 2);

        // 12 눈금
        using var tick = new Pen(HandColor, Math.Max(1f, d * 0.008f));
        for (int i = 0; i < 12; i++)
        {
            double a = i * 30.0 * Math.PI / 180.0;
            var p1 = Polar(center, r * 0.88f, a);
            var p2 = Polar(center, r, a);
            g.DrawLine(tick, p1, p2);
        }

        DrawHand(g, center, r * 0.55f, ClockGeometry.HourAngle(_time), Math.Max(3f, d * 0.020f));
        DrawHand(g, center, r * 0.80f, ClockGeometry.MinuteAngle(_time), Math.Max(2f, d * 0.013f));
        if (ShowSeconds)
            DrawHand(g, center, r * 0.88f, ClockGeometry.SecondAngle(_time), Math.Max(1f, d * 0.006f));
    }

    private void DrawHand(Graphics g, PointF c, float len, double angleDeg, float width)
    {
        using var pen = new Pen(HandColor, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var end = Polar(c, len, angleDeg * Math.PI / 180.0);
        g.DrawLine(pen, c, end);
    }

    // 12시=0도, 시계방향: x = cx + len*sin θ, y = cy − len*cos θ
    private static PointF Polar(PointF c, float len, double angleRad)
        => new(c.X + (float)(len * Math.Sin(angleRad)), c.Y - (float)(len * Math.Cos(angleRad)));
}
