# MouseMover 덮개 중앙 커스터마이징 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 덮개 중앙 시계/메시지에 디지털·아날로그 선택, 글자 크기·색·굵게, 초/12시간/날짜 옵션 추가.

**Architecture:** `Settings`에 8필드. 순수 함수 `ClockFormat`(디지털 문자열) / `ClockGeometry`(아날로그 바늘 각도)를 단위 테스트. 신규 `AnalogClock` 컨트롤이 `ClockGeometry`로 시계판을 그림. `OverlayForm`이 설정대로 디지털/아날로그/메시지/날짜를 중앙에 배치. `SettingsForm`에 `덮개 중앙 표시` GroupBox.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), WinForms, xUnit.

## Global Constraints

- `net9.0-windows`. 기존 빌드 0 에러/0 경고 유지. 현재 37 테스트.
- 새 Settings 필드/기본값: `AnalogClock`(false), `ClockFontSize`(64), `MessageFontSize`(24), `CenterColorArgb`(`unchecked((int)0xFFA0A0A0)`), `CenterBold`(false), `ClockSeconds`(false), `Clock12Hour`(false), `ShowDate`(false).
- `ClockFormat.Text(DateTime,bool seconds,bool twelveHour)`: 24h `HH:mm`/`HH:mm:ss`, 12h `tt h:mm`/`tt h:mm:ss` (ko-KR).
- `ClockGeometry`: 12시=0도 시계방향. Hour `(h%12+m/60)*30`, Minute `(m+s/60)*6`, Second `s*6`.
- 중앙 요소 색 = `CenterColorArgb`; 우하단 코너 라벨은 `LabelColorArgb` 유지.
- 아날로그 지름 = `ClockFontSize * 4` px.
- 기존 OverlayForm 불변식 유지: 코너 라벨, dismiss(KeyDown/MouseDown만, MouseMove 금지), 커서 숨김/복원, `_dismissed` 가드, `PositionLabel` 빈크기 가드, 폰트 Dispose. 새 폰트/컨트롤은 생성된 경우만 Dispose. null 안전.
- 커밋 시 해당 태스크 파일만 add(`git add -A` 금지).

---

## File Structure

| 파일 | 상태 | 책임 |
|---|---|---|
| `src/MouseMover/Settings.cs` | 수정 | 8필드 |
| `src/MouseMover/ClockFormat.cs` | 신규 | 디지털 시계 문자열(순수) |
| `src/MouseMover/ClockGeometry.cs` | 신규 | 바늘 각도(순수) |
| `src/MouseMover/AnalogClock.cs` | 신규 | 아날로그 시계 컨트롤 |
| `src/MouseMover/OverlayForm.cs` | 수정 | 중앙 통합 |
| `src/MouseMover/SettingsForm.cs` | 수정 | 중앙 표시 GroupBox |
| `tests/MouseMover.Tests/SettingsTests.cs` | 수정 | 8필드 테스트 |
| `tests/MouseMover.Tests/ClockFormatTests.cs` | 신규 | 형식 테스트 |
| `tests/MouseMover.Tests/ClockGeometryTests.cs` | 신규 | 각도 테스트 |
| `README.md` | 수정 | 문서 |

---

## Task 1: Settings 8필드

**Files:** Modify `src/MouseMover/Settings.cs`, Test `tests/MouseMover.Tests/SettingsTests.cs`

**Interfaces:** Produces 8 public 가변 속성(기본값 위 Global Constraints).

- [ ] **Step 1: 실패 테스트 추가** (기존 SettingsTests 클래스에)
```csharp
    [Fact]
    public void Center_customize_defaults()
    {
        var s = new Settings();
        Assert.False(s.AnalogClock);
        Assert.Equal(64, s.ClockFontSize);
        Assert.Equal(24, s.MessageFontSize);
        Assert.Equal(unchecked((int)0xFFA0A0A0), s.CenterColorArgb);
        Assert.False(s.CenterBold);
        Assert.False(s.ClockSeconds);
        Assert.False(s.Clock12Hour);
        Assert.False(s.ShowDate);
    }

    [Fact]
    public void Center_customize_round_trip()
    {
        var s = new Settings
        {
            AnalogClock = true, ClockFontSize = 120, MessageFontSize = 40,
            CenterColorArgb = unchecked((int)0xFF00FF00), CenterBold = true,
            ClockSeconds = true, Clock12Hour = true, ShowDate = true
        };
        var b = Settings.FromJson(s.ToJson());
        Assert.True(b.AnalogClock);
        Assert.Equal(120, b.ClockFontSize);
        Assert.Equal(40, b.MessageFontSize);
        Assert.Equal(unchecked((int)0xFF00FF00), b.CenterColorArgb);
        Assert.True(b.CenterBold);
        Assert.True(b.ClockSeconds);
        Assert.True(b.Clock12Hour);
        Assert.True(b.ShowDate);
    }
```

- [ ] **Step 2: 실패 확인** — `dotnet test --filter SettingsTests` → FAIL.

- [ ] **Step 3: 구현** — `Settings.cs` 기존 속성(ShowClock/CenterMessage) 다음에 추가:
```csharp
    public bool AnalogClock { get; set; }
    public int ClockFontSize { get; set; } = 64;
    public int MessageFontSize { get; set; } = 24;
    public int CenterColorArgb { get; set; } = unchecked((int)0xFFA0A0A0);
    public bool CenterBold { get; set; }
    public bool ClockSeconds { get; set; }
    public bool Clock12Hour { get; set; }
    public bool ShowDate { get; set; }
```

- [ ] **Step 4: 통과 확인** — `dotnet test --filter SettingsTests` → PASS.

- [ ] **Step 5: 전체** — `dotnet test` → 37 + 2 = 39.

- [ ] **Step 6: 커밋**
```bash
git add src/MouseMover/Settings.cs tests/MouseMover.Tests/SettingsTests.cs
git commit -m "feat: add center-display customization settings fields"
```

---

## Task 2: ClockFormat 순수 함수

**Files:** Create `src/MouseMover/ClockFormat.cs`, Test `tests/MouseMover.Tests/ClockFormatTests.cs`

**Interfaces:** `static string MouseMover.ClockFormat.Text(DateTime t, bool seconds, bool twelveHour)`

- [ ] **Step 1: 실패 테스트**
`tests/MouseMover.Tests/ClockFormatTests.cs`:
```csharp
using System;
using MouseMover;
using Xunit;

public class ClockFormatTests
{
    private static DateTime At(int h, int m, int s) => new(2026, 6, 19, h, m, s);

    [Fact] public void H24_no_seconds() => Assert.Equal("09:05", ClockFormat.Text(At(9, 5, 7), false, false));
    [Fact] public void H24_seconds() => Assert.Equal("09:05:07", ClockFormat.Text(At(9, 5, 7), true, false));
    [Fact] public void H24_afternoon() => Assert.Equal("13:05", ClockFormat.Text(At(13, 5, 0), false, false));

    [Fact] public void H12_morning() => Assert.Equal("오전 9:05", ClockFormat.Text(At(9, 5, 0), false, true));
    [Fact] public void H12_afternoon() => Assert.Equal("오후 1:05", ClockFormat.Text(At(13, 5, 0), false, true));
    [Fact] public void H12_midnight() => Assert.Equal("오전 12:30", ClockFormat.Text(At(0, 30, 0), false, true));
    [Fact] public void H12_seconds() => Assert.Equal("오후 1:05:09", ClockFormat.Text(At(13, 5, 9), true, true));
}
```

- [ ] **Step 2: 실패 확인** — `dotnet test --filter ClockFormatTests` → FAIL(타입 없음).

- [ ] **Step 3: 구현**
`src/MouseMover/ClockFormat.cs`:
```csharp
using System.Globalization;

namespace MouseMover;

public static class ClockFormat
{
    private static readonly CultureInfo Ko = new("ko-KR");

    public static string Text(DateTime t, bool seconds, bool twelveHour)
    {
        string fmt = twelveHour
            ? (seconds ? "tt h:mm:ss" : "tt h:mm")
            : (seconds ? "HH:mm:ss" : "HH:mm");
        return t.ToString(fmt, Ko);
    }
}
```

- [ ] **Step 4: 통과 확인** — `dotnet test --filter ClockFormatTests` → PASS(7).

- [ ] **Step 5: 커밋**
```bash
git add src/MouseMover/ClockFormat.cs tests/MouseMover.Tests/ClockFormatTests.cs
git commit -m "feat: add ClockFormat digital time string helper"
```

---

## Task 3: ClockGeometry 순수 함수

**Files:** Create `src/MouseMover/ClockGeometry.cs`, Test `tests/MouseMover.Tests/ClockGeometryTests.cs`

**Interfaces:** `static double HourAngle(DateTime)`, `MinuteAngle(DateTime)`, `SecondAngle(DateTime)` (도, 12시=0, 시계방향).

- [ ] **Step 1: 실패 테스트**
`tests/MouseMover.Tests/ClockGeometryTests.cs`:
```csharp
using System;
using MouseMover;
using Xunit;

public class ClockGeometryTests
{
    private static DateTime At(int h, int m, int s) => new(2026, 6, 19, h, m, s);

    [Fact] public void Hour_3() => Assert.Equal(90.0, ClockGeometry.HourAngle(At(3, 0, 0)), 3);
    [Fact] public void Hour_9() => Assert.Equal(270.0, ClockGeometry.HourAngle(At(9, 0, 0)), 3);
    [Fact] public void Hour_12() => Assert.Equal(0.0, ClockGeometry.HourAngle(At(12, 0, 0)), 3);
    [Fact] public void Hour_630() => Assert.Equal(195.0, ClockGeometry.HourAngle(At(6, 30, 0)), 3);
    [Fact] public void Minute_30() => Assert.Equal(180.0, ClockGeometry.MinuteAngle(At(1, 30, 0)), 3);
    [Fact] public void Minute_15() => Assert.Equal(90.0, ClockGeometry.MinuteAngle(At(1, 15, 0)), 3);
    [Fact] public void Second_15() => Assert.Equal(90.0, ClockGeometry.SecondAngle(At(1, 0, 15)), 3);
}
```

- [ ] **Step 2: 실패 확인** — `dotnet test --filter ClockGeometryTests` → FAIL.

- [ ] **Step 3: 구현**
`src/MouseMover/ClockGeometry.cs`:
```csharp
namespace MouseMover;

public static class ClockGeometry
{
    public static double HourAngle(DateTime t) => (t.Hour % 12 + t.Minute / 60.0) * 30.0;
    public static double MinuteAngle(DateTime t) => (t.Minute + t.Second / 60.0) * 6.0;
    public static double SecondAngle(DateTime t) => t.Second * 6.0;
}
```

- [ ] **Step 4: 통과 확인** — `dotnet test --filter ClockGeometryTests` → PASS(7).

- [ ] **Step 5: 커밋**
```bash
git add src/MouseMover/ClockGeometry.cs tests/MouseMover.Tests/ClockGeometryTests.cs
git commit -m "feat: add ClockGeometry hand-angle helper"
```

---

## Task 4: AnalogClock 컨트롤

**Files:** Create `src/MouseMover/AnalogClock.cs`

**Interfaces:** Consumes `ClockGeometry`. Produces `class AnalogClock : Control` — `DateTime Time`(set→Invalidate), `Color HandColor`, `bool ShowSeconds`.

설계 메모: GUI 그리기 — 단위 테스트 없음(각도는 ClockGeometry로 검증). 빌드 게이트.

- [ ] **Step 1: 구현**
`src/MouseMover/AnalogClock.cs`:
```csharp
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

    public DateTime Time
    {
        get => _time;
        set { _time = value; Invalidate(); }
    }

    public Color HandColor { get; set; } = Color.Gainsboro;
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
```

- [ ] **Step 2: 빌드 확인** — `dotnet build` → 0 에러/0 경고.

- [ ] **Step 3: 커밋**
```bash
git add src/MouseMover/AnalogClock.cs
git commit -m "feat: add analog clock control"
```

---

## Task 5: OverlayForm 통합

**Files:** Modify `src/MouseMover/OverlayForm.cs`

**Interfaces:** Consumes `Settings`(새 8필드), `ClockFormat`, `AnalogClock`. 공개 시그니처 불변.

설계 메모: GUI — 빌드+스모크+수동. **구현 전 현재 `OverlayForm.cs` 정독.** 현재 중앙은 `_clockLabel`(디지털, 64pt)+`_messageLabel`(24pt), 색은 `LabelColorArgb`, `PositionCenter`로 배치.

- [ ] **Step 1: 필드 정리/추가**
중앙 관련 필드를 다음과 같이(기존 `_clockLabel`/`_messageLabel`/`_clockFont`/`_messageFont` 유지 + 추가):
```csharp
    private readonly AnalogClock? _analog;
    private readonly Label? _dateLabel;
    private readonly Font? _dateFont;
```

- [ ] **Step 2: 생성자 — 중앙 요소 조건부 생성**
기존 중앙 생성 블록을 아래로 교체(색은 `CenterColorArgb`, 폰트 크기/굵게 설정 반영):
```csharp
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
```

- [ ] **Step 3: UpdateElapsed — 시계 갱신**
디지털/아날로그 모두 매 틱 갱신하도록(기존 `_clockLabel` 갱신 블록 교체):
```csharp
        if (_clockLabel is not null)
            _clockLabel.Text = ClockFormat.Text(DateTime.Now, _settings.ClockSeconds, _settings.Clock12Hour);
        if (_analog is not null)
            _analog.Time = DateTime.Now;
        if (_dateLabel is not null)
            _dateLabel.Text = DateTime.Now.ToString("yyyy-MM-dd ddd", new System.Globalization.CultureInfo("ko-KR"));
        if (_clockLabel is not null || _analog is not null || _dateLabel is not null || _messageLabel is not null)
            PositionCenter();
```

- [ ] **Step 4: PositionCenter — 시계/날짜/메시지 세로 스택 중앙 정렬**
기존 `PositionCenter` 교체:
```csharp
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
```

- [ ] **Step 5: Dispose — 새 폰트/컨트롤 정리**
`Dispose(disposing)`의 `if (disposing)` 블록에 추가:
```csharp
            _clockFont?.Dispose();
            _messageFont?.Dispose();
            _dateFont?.Dispose();
            _analog?.Dispose();
```
(기존 `Cursor.Show()` + `_font.Dispose()` 유지. `_clockFont`/`_messageFont`가 이미 있으면 중복 추가 말 것.)

- [ ] **Step 6: 빌드 + 전체 테스트** — `dotnet build`(0 에러/경고) → `dotnet test`(39 PASS).

- [ ] **Step 7: 스모크** — exe 백그라운드 ~4초 크래시 없음 후 종료.

- [ ] **Step 8: 커밋**
```bash
git add src/MouseMover/OverlayForm.cs
git commit -m "feat: integrate digital/analog clock, date, styling into overlay center"
```

---

## Task 6: SettingsForm GroupBox

**Files:** Modify `src/MouseMover/SettingsForm.cs`

**Interfaces:** Consumes `Settings`. `Commit()`이 8필드 추가. 공개 API 유지.

설계 메모: GUI — 빌드+스모크+수동. **구현 전 현재 `SettingsForm.cs` 정독.** 현재 루트 `layout` 2열 AutoSize. `_showClock`(row 12, col1), `덮개 메시지` 라벨+`_centerMessage`(row 13), 버튼 row 14. `Commit()`은 14필드.

목표: `_showClock`·`_centerMessage`를 새 **GroupBox `덮개 중앙 표시`**(내부 중첩 TableLayoutPanel)로 옮기고 신규 컨트롤을 그 안에 추가. GroupBox를 루트 layout의 한 행(2열 span)으로 배치하고 버튼 행은 그 뒤로 유지.

- [ ] **Step 1: 컨트롤 필드 추가** (기존 `_showClock`/`_centerMessage`는 유지)
```csharp
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
```

- [ ] **Step 2: 초기화** (생성자, `current`로)
```csharp
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
```
(`_showClock.Checked`/`_centerMessage.Text` 초기화는 기존대로 유지.)

- [ ] **Step 3: GroupBox 구성 + 루트 배치**
기존 `layout`에서 `_showClock`(row 12)·`덮개 메시지` 라벨·`_centerMessage`(row 13) 추가 줄을 제거하고, 대신 GroupBox를 만들어 그 행에 배치. GroupBox 내부는 2열 중첩 TableLayoutPanel:
```csharp
        var centerGrid = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, GrowStyle = TableLayoutPanelGrowStyle.AddRows, Dock = DockStyle.Fill
        };
        centerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        centerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        centerGrid.Controls.Add(_showClock, 0, 0); centerGrid.SetColumnSpan(_showClock, 2);
        var styleFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        styleFlow.Controls.Add(_clockDigital); styleFlow.Controls.Add(_clockAnalog);
        centerGrid.Controls.Add(new Label { Text = "시계 스타일", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        centerGrid.Controls.Add(styleFlow, 1, 1);
        centerGrid.Controls.Add(new Label { Text = "시계 크기", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        centerGrid.Controls.Add(_clockSize, 1, 2);
        centerGrid.Controls.Add(new Label { Text = "메시지 크기", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        centerGrid.Controls.Add(_messageSize, 1, 3);
        centerGrid.Controls.Add(new Label { Text = "중앙 색", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        centerGrid.Controls.Add(_centerColorButton, 1, 4);
        var optFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        optFlow.Controls.Add(_centerBold); optFlow.Controls.Add(_clockSeconds);
        optFlow.Controls.Add(_clock12Hour); optFlow.Controls.Add(_showDate);
        centerGrid.Controls.Add(optFlow, 0, 5); centerGrid.SetColumnSpan(optFlow, 2);
        centerGrid.Controls.Add(new Label { Text = "덮개 메시지", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        centerGrid.Controls.Add(_centerMessage, 1, 6);

        var centerGroup = new GroupBox { Text = "덮개 중앙 표시", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
        centerGroup.Controls.Add(centerGrid);
        layout.Controls.Add(centerGroup, 0, 12);
        layout.SetColumnSpan(centerGroup, 2);
```
그리고 버튼 행 인덱스를 `layout.Controls.Add(buttons, 0, 13);`로(또는 기존 14에서 13으로) 조정해 GroupBox 다음에 오게 한다. **현재 파일의 실제 행 번호를 확인해 GroupBox가 기존 12·13 두 행을 대체하고 버튼이 그 다음 행이 되도록** 맞출 것.

- [ ] **Step 4: Commit 확장** — 기존 `Result = new Settings { ... }`에 추가(기존 필드 유지):
```csharp
            AnalogClock = _clockAnalog.Checked,
            ClockFontSize = (int)_clockSize.Value,
            MessageFontSize = (int)_messageSize.Value,
            CenterColorArgb = _centerColorArgb,
            CenterBold = _centerBold.Checked,
            ClockSeconds = _clockSeconds.Checked,
            Clock12Hour = _clock12Hour.Checked,
            ShowDate = _showDate.Checked,
```

- [ ] **Step 5: 빌드 + 전체 테스트** — `dotnet build`(0 에러/경고) → `dotnet test`(39 PASS).

- [ ] **Step 6: 스모크** — exe ~4초 크래시 없음.

- [ ] **Step 7: 커밋**
```bash
git add src/MouseMover/SettingsForm.cs
git commit -m "feat: add center-display group with clock style and styling controls"
```

---

## Task 7: README

**Files:** Modify `README.md`

- [ ] **Step 1: 갱신** — 덮개 중앙 시계/메시지 항목을 확장:
```markdown
- **덮개 중앙 시계/메시지:** 검은 덮개 중앙에 시계(디지털/아날로그 선택)와 메시지 표시. 설정에서 시계 크기·메시지 크기, 중앙 글자 색(코너와 별도), 굵게, 초 표시, 12시간제(오전/오후), 날짜 표시까지 조절.
```

- [ ] **Step 2: 커밋**
```bash
git add README.md
git commit -m "docs: document expanded center-display customization"
```

---

## Self-Review 결과

**Spec coverage:** 8필드 → T1 ✓ / 디지털 형식 → T2 ✓ / 아날로그 각도 → T3 ✓ / 아날로그 컨트롤 → T4 ✓ / OverlayForm 통합(디지털·아날로그·날짜·색·크기·굵게) → T5 ✓ / 설정 GroupBox → T6 ✓ / 문서 → T7 ✓.

**Placeholder scan:** 코드/명령 실제 내용. T5/T6은 현재 파일 정독 후 적용 명시. TBD 없음.

**Type consistency:** `Settings` 8필드, `ClockFormat.Text(DateTime,bool,bool)`, `ClockGeometry.{Hour,Minute,Second}Angle(DateTime)`, `AnalogClock`{`Time`,`HandColor`,`ShowSeconds`}, `OverlayForm.PositionCenter()` — 일치.
