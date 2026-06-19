using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayForm : Form
{
    private readonly Action _onDismiss;
    private readonly Label _label;
    private readonly Font _font;
    private readonly Settings _settings;
    private bool _dismissed;

    public OverlayForm(Rectangle bounds, Settings settings, Action onDismiss)
    {
        _onDismiss = onDismiss;
        _settings = settings;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor.Hide();
        KeyPreview = true;
        DoubleBuffered = true;

        _font = new Font("Segoe UI", settings.LabelFontSize, FontStyle.Regular);
        _label = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(settings.LabelColorArgb),
            BackColor = Color.Black,
            Font = _font,
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
        var lines = new List<string> { _settings.StatusText };
        if (_settings.ShowElapsed) lines.Add(TimeFormat.Elapsed(elapsed));
        if (_settings.ShowDismissHint) lines.Add("아무 키나 클릭하면 해제");
        _label.Text = string.Join("\n", lines);
        PositionLabel();
    }

    private void PositionLabel()
    {
        if (ClientSize.IsEmpty) return;
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
        if (disposing) {
            Cursor.Show();
            _font.Dispose();
        }
        base.Dispose(disposing);
    }
}
