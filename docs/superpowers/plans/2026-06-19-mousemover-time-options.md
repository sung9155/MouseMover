# MouseMover Time Options Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MouseMover에 시간 기반 자동 종료 추가 — 덮개 시작 후 N분 뒤 자동 종료(프리셋), 그리고 요일별 근무시간이 끝나면 자동 종료(전환에만 발동).

**Architecture:** `Settings`에 5개 필드 추가. 순수 함수 `StopPolicy`가 (설정, 시작시각, 현재시각)으로 자동종료 여부를 판정(단위 테스트의 핵심). `OverlayManager`의 기존 1초 타이머가 매 틱 `StopPolicy`를 호출해 참이면 기존 `DismissAll()` 경로로 해제. `SettingsForm`에 컨트롤 추가.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), WinForms, xUnit, System.Text.Json.

## Global Constraints

- Target framework: `net9.0-windows`. 기존 빌드/테스트 깨지 말 것(현재 15 테스트 통과).
- 새 Settings 필드: `int AutoOffMinutes`(기본 0), `bool ScheduleEnabled`(기본 false), `int WorkStartMinutes`(기본 540), `int WorkEndMinutes`(기본 1080), `bool[] WorkDays`(기본 `[false,true,true,true,true,true,false]` = Sun..Sat, 월~금 true).
- `WorkDays` 인덱스 = `(int)DayOfWeek` (Sunday=0 … Saturday=6).
- 자동종료 프리셋: 없음(0) / 30분(30) / 1시간(60) / 2시간(120) / 4시간(240).
- 스케줄 자동종료는 **"근무 중 시작 → 근무 종료" 전환에만 발동**: `ScheduleEnabled && WorkStartMinutes<WorkEndMinutes && IsWorkTime(start) && !IsWorkTime(now)`.
- 유효창(`WorkStartMinutes < WorkEndMinutes`)이 아니면 스케줄 자동종료 미발동. 자정 넘는 근무는 미지원.
- 근무시간 경계: `WorkStartMinutes <= 분 < WorkEndMinutes` (종료 분은 비근무).
- 소스 `src/MouseMover/`, 테스트 `tests/MouseMover.Tests/`.
- 커밋 시 해당 태스크 파일만 `git add`(절대 `git add -A` 금지). bin/obj/publish는 gitignore됨.

---

## File Structure

| 파일 | 상태 | 책임 |
|---|---|---|
| `src/MouseMover/Settings.cs` | 수정 | 5개 시간 옵션 필드 추가 |
| `src/MouseMover/StopPolicy.cs` | 신규 | 순수 자동종료 판정 (`ShouldAutoStop`, `IsWorkTime`) |
| `src/MouseMover/OverlayManager.cs` | 수정 | 시작 로컬시각 기록, 1초 틱에서 StopPolicy 검사 → DismissAll |
| `src/MouseMover/SettingsForm.cs` | 수정 | 자동종료 ComboBox, 스케줄 체크박스, 근무시각 2개, 요일 7개 |
| `tests/MouseMover.Tests/SettingsTests.cs` | 수정 | 새 필드 기본값 + 왕복 테스트 추가 |
| `tests/MouseMover.Tests/StopPolicyTests.cs` | 신규 | StopPolicy 집중 테스트 |

---

## Task 1: Settings에 시간 옵션 필드 추가

**Files:**
- Modify: `src/MouseMover/Settings.cs`
- Test: `tests/MouseMover.Tests/SettingsTests.cs`

**Interfaces:**
- Consumes: 기존 `Settings`
- Produces: `Settings`에 public 가변 속성 추가 — `int AutoOffMinutes`(기본 0), `bool ScheduleEnabled`(기본 false), `int WorkStartMinutes`(기본 540), `int WorkEndMinutes`(기본 1080), `bool[] WorkDays`(기본 `[false,true,true,true,true,true,false]`). 기존 `ToJson`/`FromJson`/`Load`/`Save`/`Clone` 동작 유지.

- [ ] **Step 1: 실패 테스트 추가**

`tests/MouseMover.Tests/SettingsTests.cs`에 테스트 메서드 추가(기존 클래스 안):
```csharp
    [Fact]
    public void Time_option_defaults_are_as_specified()
    {
        var s = new Settings();
        Assert.Equal(0, s.AutoOffMinutes);
        Assert.False(s.ScheduleEnabled);
        Assert.Equal(540, s.WorkStartMinutes);
        Assert.Equal(1080, s.WorkEndMinutes);
        Assert.Equal(new[] { false, true, true, true, true, true, false }, s.WorkDays);
    }

    [Fact]
    public void Time_options_round_trip_through_json()
    {
        var s = new Settings
        {
            AutoOffMinutes = 120,
            ScheduleEnabled = true,
            WorkStartMinutes = 600,
            WorkEndMinutes = 1020,
            WorkDays = new[] { true, false, false, false, false, false, true }
        };
        var back = Settings.FromJson(s.ToJson());
        Assert.Equal(120, back.AutoOffMinutes);
        Assert.True(back.ScheduleEnabled);
        Assert.Equal(600, back.WorkStartMinutes);
        Assert.Equal(1020, back.WorkEndMinutes);
        Assert.Equal(new[] { true, false, false, false, false, false, true }, back.WorkDays);
    }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter SettingsTests`
Expected: FAIL (속성 없음 → 컴파일 에러).

- [ ] **Step 3: 구현**

`src/MouseMover/Settings.cs`의 기존 속성들(예: `RunAtStartup` 아래) 다음에 추가:
```csharp
    public int AutoOffMinutes { get; set; }
    public bool ScheduleEnabled { get; set; }
    public int WorkStartMinutes { get; set; } = 540;
    public int WorkEndMinutes { get; set; } = 1080;
    public bool[] WorkDays { get; set; } = { false, true, true, true, true, true, false };
```
(`AutoOffMinutes`/`ScheduleEnabled`는 각각 0/false 기본이므로 초기화 생략 가능.)

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter SettingsTests`
Expected: PASS (기존 4 + 신규 2 = 6 SettingsTests, 전체 17).

- [ ] **Step 5: 커밋**

```bash
git add src/MouseMover/Settings.cs tests/MouseMover.Tests/SettingsTests.cs
git commit -m "feat: add time-option fields to Settings"
```

---

## Task 2: StopPolicy 순수 판정 로직

**Files:**
- Create: `src/MouseMover/StopPolicy.cs`
- Test: `tests/MouseMover.Tests/StopPolicyTests.cs`

**Interfaces:**
- Consumes: `Settings` (Task 1)
- Produces:
  - `static bool MouseMover.StopPolicy.ShouldAutoStop(Settings s, DateTime startLocal, DateTime nowLocal)`
  - `static bool MouseMover.StopPolicy.IsWorkTime(Settings s, DateTime t)`

판정 규칙(스펙):
- `ShouldAutoStop` = 자동종료(`AutoOffMinutes>0 && (now-start).TotalMinutes >= AutoOffMinutes`) **또는** 스케줄(`ScheduleEnabled && WorkStartMinutes<WorkEndMinutes && IsWorkTime(start) && !IsWorkTime(now)`).
- `IsWorkTime(s,t)` = `WorkStartMinutes<WorkEndMinutes && WorkDays[(int)t.DayOfWeek] && WorkStartMinutes <= (t.Hour*60+t.Minute) < WorkEndMinutes`.

- [ ] **Step 1: 실패 테스트 작성**

`tests/MouseMover.Tests/StopPolicyTests.cs`:
```csharp
using System;
using MouseMover;
using Xunit;

public class StopPolicyTests
{
    // 2026-06-15는 월요일. 근무 09:00~18:00, 월~금 기본.
    private static Settings WorkdaySchedule() => new()
    {
        ScheduleEnabled = true,
        WorkStartMinutes = 540,   // 09:00
        WorkEndMinutes = 1080,    // 18:00
        WorkDays = new[] { false, true, true, true, true, true, false }
    };

    private static DateTime Mon(int hour, int min) => new(2026, 6, 15, hour, min, 0); // 월요일
    private static DateTime Sun(int hour, int min) => new(2026, 6, 14, hour, min, 0); // 일요일

    // --- 자동종료 타이머 ---
    [Fact]
    public void AutoOff_not_reached_returns_false()
    {
        var s = new Settings { AutoOffMinutes = 60 };
        var start = Mon(10, 0);
        Assert.False(StopPolicy.ShouldAutoStop(s, start, start.AddMinutes(59)));
    }

    [Fact]
    public void AutoOff_reached_returns_true()
    {
        var s = new Settings { AutoOffMinutes = 60 };
        var start = Mon(10, 0);
        Assert.True(StopPolicy.ShouldAutoStop(s, start, start.AddMinutes(60)));
    }

    [Fact]
    public void AutoOff_zero_never_triggers()
    {
        var s = new Settings { AutoOffMinutes = 0 };
        var start = Mon(10, 0);
        Assert.False(StopPolicy.ShouldAutoStop(s, start, start.AddHours(10)));
    }

    // --- 스케줄: 전환에만 발동 ---
    [Fact]
    public void Schedule_started_in_work_now_after_work_stops()
    {
        var s = WorkdaySchedule();
        Assert.True(StopPolicy.ShouldAutoStop(s, Mon(17, 0), Mon(18, 0)));
    }

    [Fact]
    public void Schedule_started_in_work_still_in_work_does_not_stop()
    {
        var s = WorkdaySchedule();
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(10, 0), Mon(11, 0)));
    }

    [Fact]
    public void Schedule_started_outside_work_does_not_stop()
    {
        var s = WorkdaySchedule();
        // 비근무(20:00) 시작 → 스케줄은 끄지 않음
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(20, 0), Mon(23, 0)));
    }

    [Fact]
    public void Schedule_disabled_does_not_stop()
    {
        var s = WorkdaySchedule();
        s.ScheduleEnabled = false;
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(17, 0), Mon(18, 0)));
    }

    [Fact]
    public void Schedule_invalid_window_does_not_stop()
    {
        var s = WorkdaySchedule();
        s.WorkStartMinutes = 1080; // 종료 <= 시작 → 무효
        s.WorkEndMinutes = 540;
        Assert.False(StopPolicy.ShouldAutoStop(s, Mon(10, 0), Mon(19, 0)));
    }

    // --- IsWorkTime ---
    [Fact]
    public void IsWorkTime_boundary_end_is_not_work()
    {
        var s = WorkdaySchedule();
        Assert.True(StopPolicy.IsWorkTime(s, Mon(9, 0)));    // 시작 포함
        Assert.False(StopPolicy.IsWorkTime(s, Mon(18, 0)));  // 종료 제외
        Assert.False(StopPolicy.IsWorkTime(s, Mon(8, 59)));
    }

    [Fact]
    public void IsWorkTime_disabled_weekday_is_not_work()
    {
        var s = WorkdaySchedule();
        Assert.False(StopPolicy.IsWorkTime(s, Sun(10, 0))); // 일요일 off
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter StopPolicyTests`
Expected: FAIL (StopPolicy 타입 없음).

- [ ] **Step 3: 구현**

`src/MouseMover/StopPolicy.cs`:
```csharp
namespace MouseMover;

public static class StopPolicy
{
    public static bool ShouldAutoStop(Settings s, DateTime startLocal, DateTime nowLocal)
    {
        if (s.AutoOffMinutes > 0 &&
            (nowLocal - startLocal).TotalMinutes >= s.AutoOffMinutes)
        {
            return true;
        }

        if (s.ScheduleEnabled &&
            s.WorkStartMinutes < s.WorkEndMinutes &&
            IsWorkTime(s, startLocal) &&
            !IsWorkTime(s, nowLocal))
        {
            return true;
        }

        return false;
    }

    public static bool IsWorkTime(Settings s, DateTime t)
    {
        if (s.WorkStartMinutes >= s.WorkEndMinutes) return false;
        if (!s.WorkDays[(int)t.DayOfWeek]) return false;
        int minutes = t.Hour * 60 + t.Minute;
        return s.WorkStartMinutes <= minutes && minutes < s.WorkEndMinutes;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter StopPolicyTests`
Expected: PASS (11건).

- [ ] **Step 5: 커밋**

```bash
git add src/MouseMover/StopPolicy.cs tests/MouseMover.Tests/StopPolicyTests.cs
git commit -m "feat: add StopPolicy auto-stop decision logic"
```

---

## Task 3: OverlayManager에서 자동종료 검사

**Files:**
- Modify: `src/MouseMover/OverlayManager.cs`

**Interfaces:**
- Consumes: `Settings`(Task 1), `StopPolicy`(Task 2), 기존 `OverlayManager`
- Produces: 동작 변경만(공개 시그니처 유지). `Start(Settings)`가 설정과 시작 로컬시각을 저장하고, 1초 타이머가 매 틱 자동종료 조건을 검사.

설계 메모: GUI/타이머 영역 — 단위 테스트 없음(StopPolicy가 로직을 커버). 빌드 + 수동 확인. **구현 전 현재 `OverlayManager.cs`를 읽을 것.** 현재 상태 요약: `Start(Settings settings)`가 `_startUtc = DateTime.UtcNow` 기록 후 화면별 폼 생성, `_elapsedTimer`(1초)가 `RefreshElapsed()` 호출, `RefreshElapsed`는 `DateTime.UtcNow - _startUtc`로 경과시간 계산해 각 폼 `UpdateElapsed`. `DismissAll()`은 `Stop()` 후 `_onDismiss()` 호출. `Stop()`은 `_elapsedTimer.Stop()` + 폼 `Close()` + 리스트 클리어 + `IsActive=false`.

- [ ] **Step 1: 설정/시작시각 저장 필드 추가**

`OverlayManager`에 필드 추가:
```csharp
    private Settings _settings = new();
    private DateTime _startLocal;
```

- [ ] **Step 2: Start에서 설정/로컬시각 기록**

`Start(Settings settings)` 안, 기존 `_startUtc = DateTime.UtcNow;` 옆에 추가:
```csharp
        _settings = settings;
        _startLocal = DateTime.Now;
```
(폼 생성에 이미 `settings`를 넘기고 있으면 그대로 두고, 위 두 줄만 추가.)

- [ ] **Step 3: RefreshElapsed에서 자동종료 검사**

`RefreshElapsed()` 메서드 끝(각 폼 `UpdateElapsed` 호출 후)에 추가:
```csharp
        if (StopPolicy.ShouldAutoStop(_settings, _startLocal, DateTime.Now))
        {
            DismissAll();
        }
```
주의: `DismissAll()`이 `Stop()`을 호출해 `_elapsedTimer`를 멈추므로 타이머 콜백 안에서 호출해도 안전(WinForms Timer는 UI 스레드 단일 실행). 그 후 폼/리스트가 정리되고 이 콜백은 즉시 반환.

- [ ] **Step 4: 빌드 + 전체 테스트**

Run: `dotnet build` 다음 `dotnet test`
Expected: 빌드 0 에러; 전체 테스트 PASS(17 + StopPolicy 11 = 28).

- [ ] **Step 5: 커밋**

```bash
git add src/MouseMover/OverlayManager.cs
git commit -m "feat: auto-stop overlay via StopPolicy on timer tick"
```

---

## Task 4: SettingsForm에 시간 옵션 컨트롤 추가

**Files:**
- Modify: `src/MouseMover/SettingsForm.cs`

**Interfaces:**
- Consumes: `Settings`(Task 1)
- Produces: 폼에 컨트롤 추가, `Commit()`이 새 필드를 `Result`에 채움. 공개 API(`SettingsForm(Settings)`, `Result`) 유지.

설계 메모: GUI — 단위 테스트 없음(빌드 + 수동). **구현 전 현재 `SettingsForm.cs`를 읽을 것.** 현재: 생성자에서 컨트롤 초기화 → `TableLayoutPanel`(2열) 배치 → OK/취소 `FlowLayoutPanel` → `Commit()`이 `Result = new Settings { ... }`로 모든 필드 채움. 새 컨트롤도 같은 패턴으로 추가하고, `ClientSize` 높이를 늘려 다 보이게 한다.

- [ ] **Step 1: 컨트롤 필드 추가**

기존 컨트롤 필드 선언부에 추가:
```csharp
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
```

- [ ] **Step 2: 생성자에서 초기화 (Result 클론 직후, 기존 초기화 옆)**

```csharp
        foreach (var p in AutoOffPresets) _autoOff.Items.Add(p.Label);
        int presetIndex = Array.FindIndex(AutoOffPresets, p => p.Minutes == current.AutoOffMinutes);
        _autoOff.SelectedIndex = presetIndex >= 0 ? presetIndex : 0;

        _scheduleEnabled.Checked = current.ScheduleEnabled;
        // DateTimePicker는 날짜는 무시하고 시:분만 사용
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
```

- [ ] **Step 3: 레이아웃에 추가 (기존 TableLayoutPanel 행 다음 행들)**

기존 `layout.Controls.Add(...)` 행들 다음에(행 번호는 기존 마지막 다음부터, 아래 예시의 7,8,9,10은 기존 행 수에 맞게 조정):
```csharp
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
```
또한 폼이 다 보이도록 `ClientSize`를 키운다(예: 기존 `new Size(360, 300)` → `new Size(380, 440)`).

- [ ] **Step 4: OK 검증 + Commit 확장**

OK 버튼의 Click 핸들러(또는 `Commit()` 진입부)에서 종료>시작 강제. `Commit()`에서 새 필드를 채운다. 종료≤시작이면 `_workEnd`를 `_workStart + 1분`으로 보정(저장은 항상 유효창):
```csharp
    private void Commit()
    {
        int startMin = _workStart.Value.Hour * 60 + _workStart.Value.Minute;
        int endMin = _workEnd.Value.Hour * 60 + _workEnd.Value.Minute;
        if (endMin <= startMin) endMin = Math.Min(startMin + 1, 1439);

        Result = new Settings
        {
            // ... 기존 7개 필드 그대로 유지 ...
            AutoOffMinutes = AutoOffPresets[_autoOff.SelectedIndex].Minutes,
            ScheduleEnabled = _scheduleEnabled.Checked,
            WorkStartMinutes = startMin,
            WorkEndMinutes = endMin,
            WorkDays = Array.ConvertAll(_workDays, cb => cb.Checked)
        };
    }
```
**중요:** 기존 `Commit()`의 7개 필드(JiggleSeconds, StatusText, ShowElapsed, ShowDismissHint, LabelFontSize, LabelColorArgb, RunAtStartup) 할당을 그대로 두고 위 5개만 추가한다.

- [ ] **Step 5: 빌드 + 전체 테스트**

Run: `dotnet build` 다음 `dotnet test`
Expected: 빌드 0 에러/0 경고; 전체 28 PASS.

- [ ] **Step 6: 스모크 실행**

Run: 빌드된 exe를 백그라운드 실행 → ~4초 후 시작 크래시 없는지 확인 → 종료.
(비대화형 환경은 즉시 code 0 종료 가능 — 예외만 없으면 OK. 실제 GUI/설정창은 사용자 확인.)

- [ ] **Step 7: 커밋**

```bash
git add src/MouseMover/SettingsForm.cs
git commit -m "feat: add time-option controls to settings dialog"
```

---

## Task 5: README 갱신

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: 전체
- Produces: 시간 옵션 문서화.

- [ ] **Step 1: README 기능/설정 절에 추가**

`README.md`의 설정 창 항목 목록에 추가(기존 문장 흐름에 맞춰):
```markdown
- **자동 종료:** 덮개 시작 후 일정 시간(없음/30분/1시간/2시간/4시간) 뒤 자동 해제.
- **요일 스케줄:** 근무 시간대(시작~종료)와 요일을 지정하면, 근무 시간에 켠 덮개가 근무 종료 시 자동 해제. 비근무 시간의 수동 시작은 막지 않음. (자정 넘는 근무는 미지원)
```

- [ ] **Step 2: 커밋**

```bash
git add README.md
git commit -m "docs: document time-option settings"
```

---

## Self-Review 결과

**Spec coverage:**
- 자동종료 프리셋 → Task 1(필드), Task 2(판정), Task 3(발동), Task 4(UI) ✓
- 요일 스케줄(공통시간+요일) → Task 1, 2, 3, 4 ✓
- "전환에만 발동" → Task 2 `ShouldAutoStop` 스케줄 분기 + 테스트 ✓
- 비근무 수동 시작 안 막음 → Task 2 `started_outside_work_does_not_stop` 테스트 ✓
- 유효창/경계/요일 엣지 → Task 2 테스트 ✓
- 설정 창 통합 → Task 4 ✓
- 영속화(JSON) → Task 1 왕복 테스트 ✓
- 문서화 → Task 5 ✓

**Placeholder scan:** 코드/명령 실제 내용. Task 4의 행 번호(7~11)는 "기존 행 수에 맞게 조정"으로 명시(현재 파일 읽고 적용) — 구현자가 현재 SettingsForm 행 수를 보고 채움. TBD 없음.

**Type consistency:** `Settings`(+AutoOffMinutes/ScheduleEnabled/WorkStartMinutes/WorkEndMinutes/WorkDays), `StopPolicy.ShouldAutoStop(Settings,DateTime,DateTime)`/`IsWorkTime(Settings,DateTime)`, `OverlayManager.Start(Settings)` 유지, `SettingsForm(Settings)`+`Result`+`Commit()` — 일치.
