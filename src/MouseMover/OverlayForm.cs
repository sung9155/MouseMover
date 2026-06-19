using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayForm : Form
{
    private readonly Action _onDismiss;
    private readonly Label _label;
    private bool _dismissed;

    public OverlayForm(Rectangle bounds, Action onDismiss)
    {
        _onDismiss = onDismiss;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor.Hide();
        KeyPreview = true;
        DoubleBuffered = true;

        _label = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Black,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomRight
        };
        Controls.Add(_label);
        UpdateElapsed(TimeSpan.Zero);
        Shown += (_, _) => PositionLabel();

        KeyDown += (_, _) => Dismiss();
        MouseDown += (_, _) => Dismiss();
    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        _label.Text =
            "절전방지 중\n" +
            TimeFormat.Elapsed(elapsed) + "\n" +
            "아무 키나 클릭하면 해제";
        PositionLabel();
    }

    private void PositionLabel()
    {
        // 우하단에서 24px 여백
        _label.Location = new Point(
            ClientSize.Width - _label.Width - 24,
            ClientSize.Height - _label.Height - 24);
    }

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        _onDismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Cursor.Show();
        base.Dispose(disposing);
    }
}
