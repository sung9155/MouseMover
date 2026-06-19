# MouseMover Settings Feature — Implementation Plan (Addendum)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax. This addendum extends `2026-06-19-mousemover.md` (Tasks 1–8 complete). Tasks numbered 10+ to avoid collision with the base plan's Task 9 (README), which runs last and now documents settings too.

**Goal:** 트레이 우클릭 메뉴에 "설정..." 추가. 설정 창에서 지글 주기, 우하단 상태 텍스트, 경과시간/해제안내 표시 여부, 라벨 글자 크기/색, Windows 시작 시 자동 실행을 변경. 설정은 `%APPDATA%\MouseMover\settings.json`에 저장.

**Architecture:** 새 `Settings`(POCO + JSON 직렬화), `StartupRegistration`(HKCU Run 레지스트리), `SettingsForm`(편집 다이얼로그) 추가. 기존 `OverlayForm`/`OverlayManager`/`KeepAwake`/`TrayAppContext`가 설정을 읽어 적용.

## Global Constraints (이 부록 한정, 기존 제약에 추가)

- 설정 파일: `%APPDATA%\MouseMover\settings.json`, `System.Text.Json`, `WriteIndented = true`.
- 라벨 색은 ARGB `int`로 저장(`Color.ToArgb()` / `Color.FromArgb(int)`).
- 시작 자동실행: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, 값 이름 `MouseMover`, 데이터 = `"<exe 경로>"`(따옴표 포함).
- 적용 시점: OK 누르면 즉시 저장 + 자동실행 레지스트리 반영. 주기/시각 변경은 다음 "덮개 시작"부터.
- 트레이 메뉴 순서: `설정...` / (구분선) / `덮개 시작` / (구분선) / `종료`.
- 설정 기본값: JiggleSeconds=45, StatusText="절전방지 중", ShowElapsed=true, ShowDismissHint=true, LabelFontSize=11, LabelColorArgb=unchecked((int)0xFFA0A0A0), RunAtStartup=false.
- 입력 범위: 지글 주기 5–600초, 글자 크기 8–48.

---

## File Structure (추가/수정)

| 파일 | 상태 | 책임 |
|---|---|---|
| `src/MouseMover/Settings.cs` | 신규 | 설정 POCO + `ToJson`/`FromJson`(순수) + `Load`/`Save`(파일) |
| `src/MouseMover/StartupRegistration.cs` | 신규 | HKCU Run 레지스트리 `Apply(bool,string)` / `IsEnabled()` |
| `src/MouseMover/SettingsForm.cs` | 신규 | 설정 편집 다이얼로그, OK 시 편집된 `Settings` 노출 |
| `src/MouseMover/KeepAwake.cs` | 수정 | `JiggleSeconds` 속성 추가(타이머 간격 변경) |
| `src/MouseMover/OverlayForm.cs` | 수정 | ctor가 `Settings` 받아 라벨 구성(텍스트/토글/폰트/색) |
| `src/MouseMover/OverlayManager.cs` | 수정 | `Start(Settings)` — 폼에 설정 전달 |
| `src/MouseMover/TrayAppContext.cs` | 수정 | `설정...` 메뉴, 설정 로드/적용/저장 |
| `tests/MouseMover.Tests/SettingsTests.cs` | 신규 | 기본값 + JSON 왕복 + 잘못된 JSON 폴백 |
| `tests/MouseMover.Tests/KeepAwakeTests.cs` | 수정 | `JiggleSeconds` 속성 테스트 추가 |

---

## Task 10: Settings 모델 + JSON 직렬화

**Files:**
- Create: `src/MouseMover/Settings.cs`
- Test: `tests/MouseMover.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `class MouseMover.Settings` (public 가변 속성): `int JiggleSeconds`, `string StatusText`, `bool ShowElapsed`, `bool ShowDismissHint`, `float LabelFontSize`, `int LabelColorArgb`, `bool RunAtStartup`.
  - `string ToJson()` (순수), `static Settings FromJson(string json)` (예외 시 기본값), `static Settings Load()`, `void Save()`, `Settings Clone()`.

- [ ] **Step 1: 실패 테스트 작성**

`tests/MouseMover.Tests/SettingsTests.cs`:
```csharp
using MouseMover;
using Xunit;

public class SettingsTests
{
    [Fact]
    public void Defaults_are_as_specified()
    {
        var s = new Settings();
        Assert.Equal(45, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText);
        Assert.True(s.ShowElapsed);
        Assert.True(s.ShowDismissHint);
        Assert.Equal(11f, s.LabelFontSize);
        Assert.Equal(unchecked((int)0xFFA0A0A0), s.LabelColorArgb);
        Assert.False(s.RunAtStartup);
    }

    [Fact]
    public void ToJson_then_FromJson_round_trips()
    {
        var s = new Settings
        {
            JiggleSeconds = 30,
            StatusText = "자리비움",
            ShowElapsed = false,
            ShowDismissHint = false,
            LabelFontSize = 20f,
            LabelColorArgb = unchecked((int)0xFF112233),
            RunAtStartup = true
        };
        var back = Settings.FromJson(s.ToJson());
        Assert.Equal(30, back.JiggleSeconds);
        Assert.Equal("자리비움", back.StatusText);
        Assert.False(back.ShowElapsed);
        Assert.False(back.ShowDismissHint);
        Assert.Equal(20f, back.LabelFontSize);
        Assert.Equal(unchecked((int)0xFF112233), back.LabelColorArgb);
        Assert.True(back.RunAtStartup);
    }

    [Fact]
    public void FromJson_invalid_returns_defaults()
    {
        var s = Settings.FromJson("this is not json");
        Assert.Equal(45, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText);
    }

    [Fact]
    public void FromJson_partial_keeps_defaults_for_missing()
    {
        var s = Settings.FromJson("{\"JiggleSeconds\":90}");
        Assert.Equal(90, s.JiggleSeconds);
        Assert.Equal("절전방지 중", s.StatusText); // 누락 필드는 기본값
        Assert.True(s.ShowElapsed);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter SettingsTests`
Expected: FAIL (Settings 타입 없음).

- [ ] **Step 3: 구현**

`src/MouseMover/Settings.cs`:
```csharp
using System.Text.Json;

namespace MouseMover;

public sealed class Settings
{
    public int JiggleSeconds { get; set; } = 45;
    public string StatusText { get; set; } = "절전방지 중";
    public bool ShowElapsed { get; set; } = true;
    public bool ShowDismissHint { get; set; } = true;
    public float LabelFontSize { get; set; } = 11f;
    public int LabelColorArgb { get; set; } = unchecked((int)0xFFA0A0A0);
    public bool RunAtStartup { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MouseMover");
            return Path.Combine(dir, "settings.json");
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static Settings FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        catch (JsonException)
        {
            return new Settings();
        }
    }

    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath) ? FromJson(File.ReadAllText(FilePath)) : new Settings();
        }
        catch (IOException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, ToJson());
    }

    public Settings Clone() => (Settings)MemberwiseClone();
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter SettingsTests`
Expected: PASS (4건).

- [ ] **Step 5: 커밋**

```bash
git add src/MouseMover/Settings.cs tests/MouseMover.Tests/SettingsTests.cs
git commit -m "feat: add Settings model with JSON persistence"
```

---

## Task 11: 시작 자동실행 레지스트리 (StartupRegistration)

**Files:**
- Create: `src/MouseMover/StartupRegistration.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `static class MouseMover.StartupRegistration` — `void Apply(bool enabled, string exePath)`, `bool IsEnabled()`.

설계 메모: 레지스트리 쓰기는 실제 HKCU에 부작용을 주므로 단위 테스트 없음(빌드 게이트 + 수동 확인). `Microsoft.Win32.Registry`는 `net9.0-windows` 데스크톱 SDK에 포함. 빌드 에러 시 `<PackageReference Include="Microsoft.Win32.Registry" Version="5.0.0" />`를 csproj에 추가.

- [ ] **Step 1: 구현**

`src/MouseMover/StartupRegistration.cs`:
```csharp
using Microsoft.Win32;

namespace MouseMover;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MouseMover";

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null) return;

        if (enabled)
            key.SetValue(ValueName, $"\"{exePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is not null;
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build`
Expected: 성공. `Microsoft.Win32` 미해결로 실패하면 위 PackageReference 추가 후 재빌드.

- [ ] **Step 3: 커밋**

```bash
git add src/MouseMover/StartupRegistration.cs src/MouseMover/MouseMover.csproj
git commit -m "feat: add Windows startup registration"
```
(csproj가 변경됐을 때만 함께 add.)

---

## Task 12: 설정 폼 (SettingsForm)

**Files:**
- Create: `src/MouseMover/SettingsForm.cs`

**Interfaces:**
- Consumes: `Settings` (Task 10)
- Produces: `class MouseMover.SettingsForm : Form` — ctor `SettingsForm(Settings current)`(전달된 설정의 Clone으로 컨트롤 초기화), `Settings Result { get; }`(OK 시 편집 결과). `DialogResult.OK`로 닫히면 호출자가 `Result` 사용.

설계 메모: GUI 폼 — 단위 테스트 없음(빌드 게이트 + 수동). 디자이너 없이 코드로 레이아웃.

- [ ] **Step 1: 구현**

`src/MouseMover/SettingsForm.cs`:
```csharp
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
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build`
Expected: 성공.

- [ ] **Step 3: 커밋**

```bash
git add src/MouseMover/SettingsForm.cs
git commit -m "feat: add settings dialog form"
```

---

## Task 13: 설정 연동 (KeepAwake, OverlayForm, OverlayManager, TrayAppContext)

**Files:**
- Modify: `src/MouseMover/KeepAwake.cs`
- Modify: `src/MouseMover/OverlayForm.cs`
- Modify: `src/MouseMover/OverlayManager.cs`
- Modify: `src/MouseMover/TrayAppContext.cs`
- Test: `tests/MouseMover.Tests/KeepAwakeTests.cs` (추가)

**Interfaces:**
- Consumes: `Settings` (Task 10), `SettingsForm` (Task 12), `StartupRegistration` (Task 11)
- Produces:
  - `KeepAwake.JiggleSeconds { get; set; }` — 타이머 간격(초). set은 `Math.Max(1, value)*1000` ms로 반영.
  - `OverlayForm(Rectangle bounds, Settings settings, Action onDismiss)` — 라벨을 설정대로 구성.
  - `OverlayManager.Start(Settings settings)` — 각 폼에 설정 전달.

구현자는 각 파일의 **현재 내용을 먼저 읽고** 아래 변경을 적용한다(파일들은 Task 5–8 이후 수정본 상태).

- [ ] **Step 1: KeepAwake — JiggleSeconds 속성 실패 테스트 추가**

`tests/MouseMover.Tests/KeepAwakeTests.cs`에 테스트 추가:
```csharp
    [Fact]
    public void JiggleSeconds_get_set_round_trips()
    {
        var ka = new KeepAwake(new FakeSender(), _ => { });
        ka.JiggleSeconds = 30;
        Assert.Equal(30, ka.JiggleSeconds);
    }

    [Fact]
    public void JiggleSeconds_default_is_45()
    {
        var ka = new KeepAwake(new FakeSender(), _ => { });
        Assert.Equal(45, ka.JiggleSeconds);
    }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter KeepAwakeTests`
Expected: FAIL (`JiggleSeconds` 없음).

- [ ] **Step 3: KeepAwake에 JiggleSeconds 속성 추가**

`KeepAwake.cs`의 `_timer` 필드를 활용해 속성 추가(생성자에서 `_timer.Interval = jiggleSeconds * 1000`은 그대로):
```csharp
    public int JiggleSeconds
    {
        get => _timer.Interval / 1000;
        set => _timer.Interval = Math.Max(1, value) * 1000;
    }
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter KeepAwakeTests`
Expected: PASS (기존 4 + 신규 2 = 6건).

- [ ] **Step 5: OverlayForm — Settings 기반 라벨**

`OverlayForm` ctor 시그니처를 `OverlayForm(Rectangle bounds, Settings settings, Action onDismiss)`로 변경하고 라벨 구성을 설정대로:
- 폰트: `new Font("Segoe UI", settings.LabelFontSize, FontStyle.Regular)` (기존 `_font` 필드 사용).
- 색: `_label.ForeColor = Color.FromArgb(settings.LabelColorArgb)`.
- 텍스트 구성 헬퍼: 첫 줄은 항상 `settings.StatusText`; `settings.ShowElapsed`면 경과시간 줄 추가; `settings.ShowDismissHint`면 `"아무 키나 클릭하면 해제"` 줄 추가.
- 설정을 필드에 저장(`private readonly Settings _settings;`)하고 `UpdateElapsed`가 이 헬퍼로 텍스트를 다시 만든다.

`UpdateElapsed` 및 텍스트 빌드 예시:
```csharp
    public void UpdateElapsed(TimeSpan elapsed)
    {
        var lines = new List<string> { _settings.StatusText };
        if (_settings.ShowElapsed) lines.Add(TimeFormat.Elapsed(elapsed));
        if (_settings.ShowDismissHint) lines.Add("아무 키나 클릭하면 해제");
        _label.Text = string.Join("\n", lines);
        PositionLabel();
    }
```
(기존 dismiss 로직, 커서 숨김, `_dismissed` 가드, `PositionLabel` 빈 크기 가드, `Dispose`의 `Cursor.Show()`+`_font.Dispose()`는 유지. `System.Collections.Generic`는 ImplicitUsings로 사용 가능.)

- [ ] **Step 6: OverlayManager — Start(Settings) 전달**

`OverlayManager`:
- `Start()`를 `Start(Settings settings)`로 변경하고, 폼 생성 시 `new OverlayForm(screen.Bounds, settings, DismissAll)`로 설정 전달.
- 나머지(타이머, RefreshElapsed, Stop, DismissAll, IsActive 가드)는 유지.

- [ ] **Step 7: TrayAppContext — 설정 메뉴/로드/적용/저장**

`TrayAppContext`:
- 필드 추가: `private Settings _settings = Settings.Load();`
- 생성자에서 `StartupRegistration.IsEnabled()`와 `_settings.RunAtStartup` 동기화는 하지 않아도 됨(파일이 진실원천). 단, 첫 로드 시 `_settings.RunAtStartup`을 레지스트리에 반영: `StartupRegistration.Apply(_settings.RunAtStartup, Application.ExecutablePath);`
- 메뉴 구성을 `설정...` / 구분선 / `덮개 시작` / 구분선 / `종료` 순서로:
```csharp
        var settingsItem = new ToolStripMenuItem("설정...", null, (_, _) => OpenSettings());
        // ... _startItem, exitItem 기존대로
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
```
- `OpenSettings()` 추가:
```csharp
    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.Result;
            _settings.Save();
            StartupRegistration.Apply(_settings.RunAtStartup, Application.ExecutablePath);
        }
    }
```
- `StartCover()`에서 주기와 설정 적용:
```csharp
        if (_overlay.IsActive) return;
        _keepAwake.JiggleSeconds = _settings.JiggleSeconds;
        _keepAwake.Start();
        _overlay.Start(_settings);
        _startItem.Enabled = false;
```
- `OnDismissed`, `ExitApp`, `Dispose(bool)`, 아이콘/뮤텍스 등 나머지는 유지.

- [ ] **Step 8: 빌드 + 전체 테스트**

Run: `dotnet build` 다음 `dotnet test`
Expected: 빌드 성공, 전건 PASS(기존 9 + Settings 4 + KeepAwake 신규 2 = 15건).

- [ ] **Step 9: 스모크 실행**

Run: 빌드된 exe를 백그라운드 실행 → ~4초 후 크래시 없는지 확인 → 종료.
(비대화형 환경에선 즉시 정상종료(code 0)될 수 있음 — 시작 예외만 없으면 OK. 실제 GUI 확인은 사용자 몫.)

- [ ] **Step 10: 커밋**

```bash
git add src/MouseMover/KeepAwake.cs src/MouseMover/OverlayForm.cs src/MouseMover/OverlayManager.cs src/MouseMover/TrayAppContext.cs tests/MouseMover.Tests/KeepAwakeTests.cs
git commit -m "feat: wire settings into keep-awake, overlay, and tray menu"
```

---

## Self-Review (부록)

**Spec coverage:** 설정 창(우클릭 메뉴) → Task 13 ✓ / 지글 주기 → Task 10,13 ✓ / 상태 텍스트 → Task 10,13 ✓ / 경과시간·해제안내 토글 → Task 10,13 ✓ / 글자 크기·색 → Task 10,12,13 ✓ / Windows 시작 자동실행 → Task 11,12,13 ✓ / 영속화 → Task 10 ✓.

**Type consistency:** `Settings`(7속성 + ToJson/FromJson/Load/Save/Clone), `StartupRegistration.Apply(bool,string)`/`IsEnabled()`, `SettingsForm(Settings)`+`Result`, `KeepAwake.JiggleSeconds`, `OverlayForm(Rectangle,Settings,Action)`, `OverlayManager.Start(Settings)` — 태스크 간 일치.
