using System.Drawing;
using System.Windows.Forms;

namespace MouseMover;

public sealed class SettingsForm : Form
{
    private readonly NumericUpDown _interval = new() { Minimum = 5, Maximum = 600, Width = 80 };
    private readonly TextBox _statusText = new() { Width = 220 };
    private readonly CheckBox _showElapsed = new() { Text = "경과시간 표시", AutoSize = true };
    private readonly CheckBox _showDismissHint = new() { Text = "해제 안내 표시", AutoSize = true };
    private readonly NumericUpDown _fontSize = new() { Minimum = 8, Maximum = 48, Width = 80 };
    private readonly Button _colorButton = new() { Text = "색 선택...", Width = 90 };
    private readonly CheckBox _runAtStartup = new() { Text = "Windows 시작 시 자동 실행", AutoSize = true };
    private int _labelColorArgb;

    public Settings Result { get; private set; }

    public SettingsForm(Settings current)
    {
        Result = current.Clone();

        Text = "MouseMover 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 300);

        _interval.Value = Math.Clamp(current.JiggleSeconds, 5, 600);
        _statusText.Text = current.StatusText;
        _showElapsed.Checked = current.ShowElapsed;
        _showDismissHint.Checked = current.ShowDismissHint;
        _fontSize.Value = (decimal)Math.Clamp(current.LabelFontSize, 8f, 48f);
        _labelColorArgb = current.LabelColorArgb;
        _runAtStartup.Checked = current.RunAtStartup;

        _colorButton.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = Color.FromArgb(_labelColorArgb) };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _labelColorArgb = dlg.Color.ToArgb();
                _colorButton.BackColor = dlg.Color;
            }
        };
        _colorButton.BackColor = Color.FromArgb(_labelColorArgb);

        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Width = 80 };
        ok.Click += (_, _) => Commit();
        AcceptButton = ok;
        CancelButton = cancel;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.Controls.Add(new Label { Text = "지글 주기(초)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_interval, 1, 0);
        layout.Controls.Add(new Label { Text = "상태 텍스트", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_statusText, 1, 1);
        layout.Controls.Add(_showElapsed, 1, 2);
        layout.Controls.Add(_showDismissHint, 1, 3);
        layout.Controls.Add(new Label { Text = "글자 크기", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_fontSize, 1, 4);
        layout.Controls.Add(new Label { Text = "글자 색", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.Controls.Add(_colorButton, 1, 5);
        layout.Controls.Add(_runAtStartup, 1, 6);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
            Height = 48
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(layout);
        Controls.Add(buttons);
    }

    private void Commit()
    {
        Result = new Settings
        {
            JiggleSeconds = (int)_interval.Value,
            StatusText = _statusText.Text,
            ShowElapsed = _showElapsed.Checked,
            ShowDismissHint = _showDismissHint.Checked,
            LabelFontSize = (float)_fontSize.Value,
            LabelColorArgb = _labelColorArgb,
            RunAtStartup = _runAtStartup.Checked
        };
    }
}
