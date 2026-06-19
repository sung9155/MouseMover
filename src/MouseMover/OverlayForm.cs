using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayForm : Form
{
    private readonly Action _onDismiss;
    private readonly Label _label;
    private readonly Font _font;
    private readonly Settings _settings;
    private readonly DateTime _startLocal;
    private bool _dismissed;
    private readonly Label? _clockLabel;
    private readonly Label? _messageLabel;
    private readonly Font? _clockFont;
    private readonly Font? _messageFont;

    public OverlayForm(Rectangle bounds, Settings settings, DateTime startLocal, Action onDismiss)
    {
        _onDismiss = onDismiss;
        _settings = settings;
        _startLocal = startLocal;

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
        if (_settings.ShowClock)
        {
            _clockFont = new Font("Segoe UI", 64f, FontStyle.Bold);
            _clockLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(_settings.LabelColorArgb),
                BackColor = Color.Black,
                Font = _clockFont,
                Text = DateTime.Now.ToString("HH:mm")
            };
            Controls.Add(_clockLabel);
        }
        if (!string.IsNullOrEmpty(_settings.CenterMessage))
        {
            _messageFont = new Font("Segoe UI", 24f, FontStyle.Regular);
            _messageLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(_settings.LabelColorArgb),
                BackColor = Color.Black,
                Font = _messageFont,
                Text = _settings.CenterMessage
            };
            Controls.Add(_messageLabel);
        }
        UpdateElapsed(TimeSpan.Zero);
        Shown += (_, _) => { PositionLabel(); PositionCenter(); };

        KeyDown += (_, _) => Dismiss();
        MouseDown += (_, _) => Dismiss();
    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        var lines = new List<string> { _settings.StatusText };
        if (_settings.ShowElapsed) lines.Add(TimeFormat.Elapsed(elapsed));
        if (_settings.ShowDismissHint) lines.Add(DismissHint(elapsed));
        _label.Text = string.Join("\n", lines);
        PositionLabel();
        if (_clockLabel is not null)
        {
            _clockLabel.Text = DateTime.Now.ToString("HH:mm");
            PositionCenter();
        }
    }

    private string DismissHint(TimeSpan elapsed)
    {
        var now = _startLocal + elapsed;
        var nextStop = StopPolicy.NextAutoStop(_settings, _startLocal);
        if (nextStop is { } stop && stop - now > TimeSpan.Zero)
        {
            int remaining = Math.Max(1, (int)Math.Ceiling((stop - now).TotalMinutes));
            return $"{stop:HH:mm} 자동 해제 예정 ({remaining}분 남음)\n아무 키나 클릭하면 해제";
        }
        return "아무 키나 클릭하면 해제";
    }

    private void PositionLabel()
    {
        if (ClientSize.IsEmpty) return;
        // 우하단에서 24px 여백
        _label.Location = new Point(
            ClientSize.Width - _label.Width - 24,
            ClientSize.Height - _label.Height - 24);
    }

    private void PositionCenter()
    {
        if (ClientSize.IsEmpty) return;
        int cy = (int)(ClientSize.Height * 0.40);
        if (_clockLabel is not null)
            _clockLabel.Location = new Point((ClientSize.Width - _clockLabel.Width) / 2, cy);
        if (_messageLabel is not null)
        {
            int my = _clockLabel is not null ? _clockLabel.Bottom + 12 : cy;
            _messageLabel.Location = new Point((ClientSize.Width - _messageLabel.Width) / 2, my);
        }
    }

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        _onDismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cursor.Show();
            _font.Dispose();
            _clockFont?.Dispose();
            _messageFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
