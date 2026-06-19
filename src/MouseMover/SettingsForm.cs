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

    private readonly CheckBox _showClock = new() { Text = "덮개 중앙 시계 표시", AutoSize = true };
    private readonly TextBox _centerMessage = new() { Width = 220 };

    // Step 1: new center-display control fields
    private readonly RadioButton _clockDigital = new() { Text = "디지털", AutoSize = true, Checked = true };
    private readonly RadioButton _clockAnalog = new() { Text = "아날로그", AutoSize = true };
    private readonly NumericUpDown _clockSize = new() { Minimum = 24, Maximum = 200, Width = 80 };
    private readonly NumericUpDown _messageSize = new() { Minimum = 12, Maximum = 120, Width = 80 };
    private readonly Button _centerColorButton = new() { Text = "중앙 색...", Width = 90 };
    private readonly CheckBox _centerBold = new() { Text = "굵게", AutoSize = true };
    private readonly CheckBox _clockSeconds = new() { Text = "초 표시", AutoSize = true };
    private readonly CheckBox _clock12Hour = new() { Text = "오전/오후", AutoSize = true };
    private readonly CheckBox _showDate = new() { Text = "날짜 표시", AutoSize = true };
    private int _centerColorArgb;

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
        // 내용 기반 자동 크기 — 고정폭에서 요일 체크박스가 잘리던 문제 방지
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

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

        _showClock.Checked = current.ShowClock;
        _centerMessage.Text = current.CenterMessage;

        // Step 2: initialize new center-display controls from current
        _clockAnalog.Checked = current.AnalogClock;
        _clockDigital.Checked = !current.AnalogClock;
        _clockSize.Value = Math.Clamp(current.ClockFontSize, 24, 200);
        _messageSize.Value = Math.Clamp(current.MessageFontSize, 12, 120);
        _centerColorArgb = current.CenterColorArgb;
        _centerColorButton.BackColor = Color.FromArgb(_centerColorArgb);
        _centerColorButton.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = Color.FromArgb(_centerColorArgb) };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _centerColorArgb = dlg.Color.ToArgb();
                _centerColorButton.BackColor = dlg.Color;
            }
        };
        _centerBold.Checked = current.CenterBold;
        _clockSeconds.Checked = current.ClockSeconds;
        _clock12Hour.Checked = current.Clock12Hour;
        _showDate.Checked = current.ShowDate;

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

        // 루트 2열 그리드 — 내용에 맞춰 자동 크기(클립 방지)
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

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

        // 요일 — 7열 그리드(셀마다 체크박스). 폭이 내용에 맞춰 늘어나 잘리지 않음
        var daysGrid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 7,
            RowCount = 1,
            Margin = new Padding(0)
        };
        for (int i = 0; i < 7; i++)
        {
            daysGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _workDays[i].Margin = new Padding(2, 3, 2, 3);
            daysGrid.Controls.Add(_workDays[i], i, 0);
        }
        layout.Controls.Add(new Label { Text = "근무 요일", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 11);
        layout.Controls.Add(daysGrid, 1, 11);

        // 버튼 행 — 루트 마지막 행, 두 열 span, 오른쪽 정렬
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 10, 0, 0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        // Step 3: build the 덮개 중앙 표시 GroupBox with a nested 2-col TableLayoutPanel
        var centerGrid = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, GrowStyle = TableLayoutPanelGrowStyle.AddRows, Dock = DockStyle.Fill
        };
        centerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        centerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // row 0: _showClock spanning both columns
        centerGrid.Controls.Add(_showClock, 0, 0); centerGrid.SetColumnSpan(_showClock, 2);

        // row 1: 시계 스타일 label + radio buttons flow
        var styleFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        styleFlow.Controls.Add(_clockDigital); styleFlow.Controls.Add(_clockAnalog);
        centerGrid.Controls.Add(new Label { Text = "시계 스타일", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        centerGrid.Controls.Add(styleFlow, 1, 1);

        // row 2: 시계 크기
        centerGrid.Controls.Add(new Label { Text = "시계 크기", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        centerGrid.Controls.Add(_clockSize, 1, 2);

        // row 3: 메시지 크기
        centerGrid.Controls.Add(new Label { Text = "메시지 크기", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        centerGrid.Controls.Add(_messageSize, 1, 3);

        // row 4: 중앙 색
        centerGrid.Controls.Add(new Label { Text = "중앙 색", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        centerGrid.Controls.Add(_centerColorButton, 1, 4);

        // row 5: options flow (bold/seconds/12h/date), spanning both columns
        var optFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        optFlow.Controls.Add(_centerBold); optFlow.Controls.Add(_clockSeconds);
        optFlow.Controls.Add(_clock12Hour); optFlow.Controls.Add(_showDate);
        centerGrid.Controls.Add(optFlow, 0, 5); centerGrid.SetColumnSpan(optFlow, 2);

        // row 6: 덮개 메시지 label + text box
        centerGrid.Controls.Add(new Label { Text = "덮개 메시지", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        centerGrid.Controls.Add(_centerMessage, 1, 6);

        var centerGroup = new GroupBox { Text = "덮개 중앙 표시", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
        centerGroup.Controls.Add(centerGrid);

        // GroupBox at root row 12 (replaces old row-12 _showClock and row-13 덮개 메시지)
        layout.Controls.Add(centerGroup, 0, 12);
        layout.SetColumnSpan(centerGroup, 2);

        // buttons at root row 13 (was 14)
        layout.Controls.Add(buttons, 0, 13);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
    }

    private void Commit()
    {
        int startMin = _workStart.Value.Hour * 60 + _workStart.Value.Minute;
        startMin = Math.Min(startMin, 1438);
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
            WorkDays = Array.ConvertAll(_workDays, cb => cb.Checked),
            ShowClock = _showClock.Checked,
            CenterMessage = _centerMessage.Text,
            // Step 4: new center-display fields
            AnalogClock = _clockAnalog.Checked,
            ClockFontSize = (int)_clockSize.Value,
            MessageFontSize = (int)_messageSize.Value,
            CenterColorArgb = _centerColorArgb,
            CenterBold = _centerBold.Checked,
            ClockSeconds = _clockSeconds.Checked,
            Clock12Hour = _clock12Hour.Checked,
            ShowDate = _showDate.Checked,
        };
    }
}
