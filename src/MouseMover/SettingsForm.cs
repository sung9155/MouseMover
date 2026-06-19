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

    private readonly ComboBox _autoOff = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _scheduleEnabled = new() { Text = "요일 스케줄 사용", AutoSize = true };
    private readonly DateTimePicker _workStart = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
    private readonly DateTimePicker _workEnd = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
    private readonly CheckBox[] _workDays = new CheckBox[7];

    private static readonly (string Label, int Minutes)[] AutoOffPresets =
    {
        ("없음", 0), ("30분", 30), ("1시간", 60), ("2시간", 120), ("4시간", 240)
    };
    private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

    public Settings Result { get; private set; }

    public SettingsForm(Settings current)
    {
        Result = current.Clone();

        Text = "MouseMover 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 440);

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

        foreach (var p in AutoOffPresets) _autoOff.Items.Add(p.Label);
        int presetIndex = Array.FindIndex(AutoOffPresets, p => p.Minutes == current.AutoOffMinutes);
        _autoOff.SelectedIndex = presetIndex >= 0 ? presetIndex : 0;

        _scheduleEnabled.Checked = current.ScheduleEnabled;
        var today = DateTime.Today;
        _workStart.Value = today.AddMinutes(Math.Clamp(current.WorkStartMinutes, 0, 1439));
        _workEnd.Value = today.AddMinutes(Math.Clamp(current.WorkEndMinutes, 0, 1439));

        for (int i = 0; i < 7; i++)
        {
            _workDays[i] = new CheckBox
            {
                Text = DayNames[i],
                AutoSize = true,
                Checked = i < current.WorkDays.Length && current.WorkDays[i]
            };
        }

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

        layout.Controls.Add(new Label { Text = "자동 종료", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        layout.Controls.Add(_autoOff, 1, 7);
        layout.Controls.Add(_scheduleEnabled, 1, 8);
        layout.Controls.Add(new Label { Text = "근무 시작", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        layout.Controls.Add(_workStart, 1, 9);
        layout.Controls.Add(new Label { Text = "근무 종료", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 10);
        layout.Controls.Add(_workEnd, 1, 10);

        var daysPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        foreach (var cb in _workDays) daysPanel.Controls.Add(cb);
        layout.Controls.Add(new Label { Text = "근무 요일", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 11);
        layout.Controls.Add(daysPanel, 1, 11);

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
        int startMin = _workStart.Value.Hour * 60 + _workStart.Value.Minute;
        int endMin = _workEnd.Value.Hour * 60 + _workEnd.Value.Minute;
        if (endMin <= startMin) endMin = Math.Min(startMin + 1, 1439);

        Result = new Settings
        {
            JiggleSeconds = (int)_interval.Value,
            StatusText = _statusText.Text,
            ShowElapsed = _showElapsed.Checked,
            ShowDismissHint = _showDismissHint.Checked,
            LabelFontSize = (float)_fontSize.Value,
            LabelColorArgb = _labelColorArgb,
            RunAtStartup = _runAtStartup.Checked,
            AutoOffMinutes = AutoOffPresets[_autoOff.SelectedIndex].Minutes,
            ScheduleEnabled = _scheduleEnabled.Checked,
            WorkStartMinutes = startMin,
            WorkEndMinutes = endMin,
            WorkDays = Array.ConvertAll(_workDays, cb => cb.Checked)
        };
    }
}
