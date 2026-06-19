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
    private readonly AnalogClock? _analog;
    private readonly Label? _dateLabel;
    private readonly Font? _dateFont;

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

        var centerColor = Color.FromArgb(_settings.CenterColorArgb);
        var centerStyle = _settings.CenterBold ? FontStyle.Bold : FontStyle.Regular;

        if (_settings.ShowClock)
        {
            if (_settings.AnalogClock)
            {
                int dia = Math.Max(48, _settings.ClockFontSize * 4);
                _analog = new AnalogClock
                {
                    Size = new Size(dia, dia),
                    HandColor = centerColor,
                    ShowSeconds = _settings.ClockSeconds,
                    Time = DateTime.Now
                };
                Controls.Add(_analog);
            }
            else
            {
                _clockFont = new Font("Segoe UI", _settings.ClockFontSize, centerStyle);
                _clockLabel = new Label
                {
                    AutoSize = true, ForeColor = centerColor, BackColor = Color.Black,
                    Font = _clockFont,
                    Text = ClockFormat.Text(DateTime.Now, _settings.ClockSeconds, _settings.Clock12Hour)
                };
                Controls.Add(_clockLabel);
            }
        }

        if (_settings.ShowDate)
        {
            _dateFont = new Font("Segoe UI", Math.Max(10, _settings.MessageFontSize), centerStyle);
            _dateLabel = new Label
            {
                AutoSize = true, ForeColor = centerColor, BackColor = Color.Black,
                Font = _dateFont,
                Text = DateTime.Now.ToString("yyyy-MM-dd ddd", new System.Globalization.CultureInfo("ko-KR"))
            };
            Controls.Add(_dateLabel);
        }

        if (!string.IsNullOrEmpty(_settings.CenterMessage))
        {
            _messageFont = new Font("Segoe UI", _settings.MessageFontSize, centerStyle);
            _messageLabel = new Label
            {
                AutoSize = true, ForeColor = centerColor, BackColor = Color.Black,
                Font = _messageFont, Text = _settings.CenterMessage
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
            _clockLabel.Text = ClockFormat.Text(DateTime.Now, _settings.ClockSeconds, _settings.Clock12Hour);
        if (_analog is not null)
            _analog.Time = DateTime.Now;
        if (_dateLabel is not null)
            _dateLabel.Text = DateTime.Now.ToString("yyyy-MM-dd ddd", new System.Globalization.CultureInfo("ko-KR"));
        if (_clockLabel is not null || _analog is not null || _dateLabel is not null || _messageLabel is not null)
            PositionCenter();
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

        var items = new List<Control>();
        if (_analog is not null) items.Add(_analog);
        if (_clockLabel is not null) items.Add(_clockLabel);
        if (_dateLabel is not null) items.Add(_dateLabel);
        if (_messageLabel is not null) items.Add(_messageLabel);
        if (items.Count == 0) return;

        const int gap = 16;
        int total = 0;
        foreach (var c in items) total += c.Height;
        total += gap * (items.Count - 1);

        int y = (ClientSize.Height - total) / 2;
        foreach (var c in items)
        {
            c.Location = new Point((ClientSize.Width - c.Width) / 2, y);
            y += c.Height + gap;
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
            _dateFont?.Dispose();
            _analog?.Dispose();
        }
        base.Dispose(disposing);
    }
}
