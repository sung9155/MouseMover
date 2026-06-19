# MouseMover Dismiss-ETA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 오버레이 우하단 해제 안내에 자동 해제 예정 시각 + 남은 시간을 표기(자동 종료 타이머/퇴근 스케줄 있을 때). 없으면 현행 문구 유지.

**Architecture:** 순수 함수 `StopPolicy.NextAutoStop(Settings, DateTime startLocal)`가 다음 자동 해제 예정 시각(또는 null)을 계산(단위 테스트). `OverlayForm`이 시작 로컬시각을 받아 매 틱 안내 줄을 구성. `OverlayManager`가 시작시각을 폼에 전달.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), WinForms, xUnit.

## Global Constraints

- Target framework: `net9.0-windows`. 기존 빌드/테스트(현재 28개) 깨지지 말 것.
- `StopPolicy.NextAutoStop(Settings s, DateTime startLocal)` → `DateTime?`:
  - 자동종료 후보: `s.AutoOffMinutes > 0` → `startLocal.AddMinutes(s.AutoOffMinutes)`.
  - 스케줄 후보: `s.ScheduleEnabled && s.WorkStartMinutes < s.WorkEndMinutes && IsWorkTime(s, startLocal)` → `startLocal.Date.AddMinutes(s.WorkEndMinutes)`.
  - 둘 중 이른 시각, 없으면 null. `ShouldAutoStop`는 변경 금지.
- 표기 포맷(ShowDismissHint 켜짐 + nextStop 존재 + 남은>0): 두 줄 — `"{HH:mm} 자동 해제 예정 ({N}분 남음)"` 그리고 `"아무 키나 클릭하면 해제"`. 그 외엔 현행 `"아무 키나 클릭하면 해제"` 한 줄. ShowDismissHint 꺼짐이면 안내 줄 없음.
- 남은 분 = `Math.Max(1, (int)Math.Ceiling((nextStop - now).TotalMinutes))`.
- 첫 줄(StatusText)·경과시간(ShowElapsed) 줄은 현행 유지. 해제 트리거(KeyDown/MouseDown만), 커서 숨김, `_dismissed` 가드, `PositionLabel` 빈크기 가드, `Dispose`(Cursor.Show+_font.Dispose)는 유지.
- 커밋 시 해당 태스크 파일만 add(절대 `git add -A` 금지).

---

## File Structure

| 파일 | 상태 | 책임 |
|---|---|---|
| `src/MouseMover/StopPolicy.cs` | 수정 | `NextAutoStop` 추가 |
| `tests/MouseMover.Tests/StopPolicyTests.cs` | 수정 | `NextAutoStop` 테스트 추가 |
| `src/MouseMover/OverlayForm.cs` | 수정 | ctor에 startLocal, UpdateElapsed에서 안내 줄 구성 |
| `src/MouseMover/OverlayManager.cs` | 수정 | 폼 생성 시 `_startLocal` 전달 |
| `README.md` | 수정 | 해제 안내 자동 해제 예정 표기 한 줄 |

---

## Task 1: StopPolicy.NextAutoStop (순수 함수)

**Files:**
- Modify: `src/MouseMover/StopPolicy.cs`
- Test: `tests/MouseMover.Tests/StopPolicyTests.cs`

**Interfaces:**
- Consumes: `Settings`, 기존 `StopPolicy.IsWorkTime`
- Produces: `static DateTime? MouseMover.StopPolicy.NextAutoStop(Settings s, DateTime startLocal)`

- [ ] **Step 1: 실패 테스트 추가**

`tests/MouseMover.Tests/StopPolicyTests.cs`의 기존 클래스에 메서드 추가(기존 `WorkdaySchedule()`/`Mon()`/`Sun()` 헬퍼 재사용):
```csharp
    // --- NextAutoStop ---
    [Fact]
    public void NextAutoStop_autooff_only_returns_start_plus_minutes()
    {
        var s = new Settings { AutoOffMinutes = 90 };
        var start = Mon(10, 0);
        Assert.Equal(start.AddMinutes(90), StopPolicy.NextAutoStop(s, start));
    }

    [Fact]
    public void NextAutoStop_schedule_only_returns_work_end_today()
    {
        var s = WorkdaySchedule(); // 09:00~18:00, 월~금
        var start = Mon(10, 0);
        Assert.Equal(Mon(18, 0), StopPolicy.NextAutoStop(s, start));
    }

    [Fact]
    public void NextAutoStop_both_returns_earlier()
    {
        var s = WorkdaySchedule();
        s.AutoOffMinutes = 30;          // 10:00 + 30분 = 10:30 (퇴근 18:00보다 이름)
        var start = Mon(10, 0);
        Assert.Equal(Mon(10, 30), StopPolicy.NextAutoStop(s, start));
    }

    [Fact]
    public void NextAutoStop_both_returns_schedule_when_earlier()
    {
        var s = WorkdaySchedule();
        s.AutoOffMinutes = 600;         // 10:00 + 600분 = 20:00 (퇴근 18:00이 이름)
        var start = Mon(10, 0);
        Assert.Equal(Mon(18, 0), StopPolicy.NextAutoStop(s, start));
    }

    [Fact]
    public void NextAutoStop_none_returns_null()
    {
        var s = new Settings(); // AutoOffMinutes=0, ScheduleEnabled=false
        Assert.Null(StopPolicy.NextAutoStop(s, Mon(10, 0)));
    }

    [Fact]
    public void NextAutoStop_schedule_started_outside_work_returns_null()
    {
        var s = WorkdaySchedule();      // 자동종료 없음, 스케줄만
        Assert.Null(StopPolicy.NextAutoStop(s, Mon(20, 0))); // 비근무 시작
    }

    [Fact]
    public void NextAutoStop_autooff_zero_with_schedule_returns_schedule()
    {
        var s = WorkdaySchedule();
        s.AutoOffMinutes = 0;
        Assert.Equal(Mon(18, 0), StopPolicy.NextAutoStop(s, Mon(9, 30)));
    }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter StopPolicyTests`
Expected: FAIL (`NextAutoStop` 없음 → 컴파일 에러).

- [ ] **Step 3: 구현**

`src/MouseMover/StopPolicy.cs`에 메서드 추가(`IsWorkTime` 위/아래 아무 곳, 클래스 내부):
```csharp
    public static DateTime? NextAutoStop(Settings s, DateTime startLocal)
    {
        DateTime? next = null;

        if (s.AutoOffMinutes > 0)
            next = startLocal.AddMinutes(s.AutoOffMinutes);

        if (s.ScheduleEnabled &&
            s.WorkStartMinutes < s.WorkEndMinutes &&
            IsWorkTime(s, startLocal))
        {
            var workEnd = startLocal.Date.AddMinutes(s.WorkEndMinutes);
            if (next is null || workEnd < next) next = workEnd;
        }

        return next;
    }
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter StopPolicyTests`
Expected: PASS (기존 10 + 신규 7 = 17 StopPolicyTests).

- [ ] **Step 5: 전체 테스트**

Run: `dotnet test`
Expected: 전체 PASS (기존 28 + 7 = 35).

- [ ] **Step 6: 커밋**

```bash
git add src/MouseMover/StopPolicy.cs tests/MouseMover.Tests/StopPolicyTests.cs
git commit -m "feat: add StopPolicy.NextAutoStop for dismiss ETA"
```

---

## Task 2: OverlayForm 안내 줄 + OverlayManager 전달 + README

**Files:**
- Modify: `src/MouseMover/OverlayForm.cs`
- Modify: `src/MouseMover/OverlayManager.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `StopPolicy.NextAutoStop`(Task 1), `Settings`
- Produces: `OverlayForm(Rectangle bounds, Settings settings, DateTime startLocal, Action onDismiss)` — 안내 줄에 자동 해제 예정 표기.

설계 메모: GUI — 단위 테스트 없음(NextAutoStop가 로직 커버). 빌드+스모크+수동. **구현 전 현재 `OverlayForm.cs`/`OverlayManager.cs`를 읽을 것.** 현재 OverlayForm ctor 시그니처는 `OverlayForm(Rectangle bounds, Settings settings, Action onDismiss)`이고 `_settings` 필드 보유, `UpdateElapsed(TimeSpan elapsed)`가 `List<string> lines`로 StatusText/경과시간/해제안내를 조립한다. OverlayManager는 `Start(Settings settings)`에서 `_startLocal = DateTime.Now`를 이미 기록하고 `new OverlayForm(screen.Bounds, settings, DismissAll)`로 폼 생성.

- [ ] **Step 1: OverlayForm에 startLocal 추가**

`OverlayForm` ctor 시그니처를 `OverlayForm(Rectangle bounds, Settings settings, DateTime startLocal, Action onDismiss)`로 변경하고 `private readonly DateTime _startLocal;` 필드에 저장. 기존 `_settings`, `_font`, dismiss 로직 등은 유지.

- [ ] **Step 2: UpdateElapsed에서 안내 줄 구성**

현재 `UpdateElapsed`의 해제 안내 줄 추가 부분을 NextAutoStop 기반으로 교체. 전체 메서드는 대략:
```csharp
    public void UpdateElapsed(TimeSpan elapsed)
    {
        var lines = new List<string> { _settings.StatusText };
        if (_settings.ShowElapsed) lines.Add(TimeFormat.Elapsed(elapsed));
        if (_settings.ShowDismissHint) lines.Add(DismissHint(elapsed));
        _label.Text = string.Join("\n", lines);
        PositionLabel();
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
```
주의: 현재 코드가 해제 안내를 단일 문자열 `"아무 키나 클릭하면 해제"`로 `lines.Add` 하고 있으므로, 그 한 줄을 위 `DismissHint(elapsed)` 호출로 교체한다. `DismissHint`가 두 줄(개행 포함) 문자열을 반환할 수 있으므로 `string.Join("\n", lines)`로 그대로 이어진다. `System.Collections.Generic`/`System`은 ImplicitUsings로 사용 가능.

- [ ] **Step 3: OverlayManager에서 startLocal 전달**

`OverlayManager.Start(Settings settings)`의 폼 생성부를 `new OverlayForm(screen.Bounds, settings, _startLocal, DismissAll)`로 변경(이미 존재하는 `_startLocal` 사용). 나머지 변경 없음.

- [ ] **Step 4: 빌드 + 전체 테스트**

Run: `dotnet build` 다음 `dotnet test`
Expected: 빌드 0 에러; 전체 35 PASS(이 태스크는 새 테스트 없음).

- [ ] **Step 5: 스모크 실행**

Run: 빌드된 exe 백그라운드 실행 → ~4초 후 시작 크래시 없는지 확인 → 종료.
(비대화형 환경은 즉시 code 0 종료 가능 — 예외만 없으면 OK. 실제 표기는 사용자 확인.)

- [ ] **Step 6: README 한 줄 추가**

`README.md`의 `해제 안내 표시` 항목 또는 기능 설명에 추가:
```markdown
- **해제 예정 표기:** 자동 종료 타이머나 요일(퇴근) 스케줄이 설정돼 있으면, 해제 안내에 자동 해제 예정 시각과 남은 시간(예: `18:00 자동 해제 예정 (59분 남음)`)이 함께 표시됩니다.
```

- [ ] **Step 7: 커밋**

```bash
git add src/MouseMover/OverlayForm.cs src/MouseMover/OverlayManager.cs README.md
git commit -m "feat: show auto-stop ETA in overlay dismiss hint"
```

---

## Self-Review 결과

**Spec coverage:**
- NextAutoStop(자동종료/스케줄/이른것/null/비근무) → Task 1 + 테스트 ✓
- 안내 줄 포맷(두 줄, 시각+남은분) → Task 2 ✓
- 자동해제 없으면 현행 문구 → Task 2 `DismissHint` else 분기 ✓
- ShowDismissHint 꺼짐 → 안내 줄 없음(기존 `if` 유지) ✓
- 1초 카운트다운 → 기존 1초 타이머가 UpdateElapsed 호출 ✓
- 문서화 → Task 2 Step 6 ✓

**Placeholder scan:** 코드/명령 실제 내용. TBD 없음.

**Type consistency:** `StopPolicy.NextAutoStop(Settings, DateTime) → DateTime?`, `OverlayForm(Rectangle, Settings, DateTime, Action)` + `UpdateElapsed(TimeSpan)` + `DismissHint(TimeSpan)`, `OverlayManager.Start(Settings)` 내부 폼 생성 4-인자 — 일치.
