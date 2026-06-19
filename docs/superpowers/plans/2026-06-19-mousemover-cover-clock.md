# MouseMover 표시 개선 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 덮개 중앙에 큰 시계(HH:mm) + 커스텀 메시지 표시(개별 토글), 그리고 절전방지 전용 모드 트레이 툴팁에 자동 해제 예정 시각 표기.

**Architecture:** `Settings`에 `ShowClock`/`CenterMessage` 추가. `OverlayForm`이 중앙 시계/메시지 라벨을 그리고 1초마다 시계 갱신. `SettingsForm`에 컨트롤 2개. `TrayAppContext`의 KAO 1초 타이머가 `StopPolicy.NextAutoStop`로 툴팁 갱신.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), WinForms, xUnit.

## Global Constraints

- `net9.0-windows`. 기존 빌드/테스트(현재 35개) 깨지지 말 것.
- 새 Settings 필드: `bool ShowClock`(기본 false), `string CenterMessage`(기본 `""`).
- 덮개 중앙: `ShowClock`이면 시계(HH:mm, Segoe UI 64pt Bold), `CenterMessage` 비어있지 않으면 메시지(Segoe UI 24pt) 시계 아래. 색 = `Color.FromArgb(LabelColorArgb)`. 각 모니터 폼마다.
- 기본값(off+빈값) → 기존 순수 검은 덮개와 동일.
- 우하단 라벨/dismiss(KeyDown·MouseDown만)/커서 숨김/`_dismissed`/`_font.Dispose` 등 기존 동작 유지. 새 폰트도 생성된 경우 Dispose.
- 툴팁: KAO 활성 시 `NextAutoStop` 있으면 `MouseMover — 절전방지 중 (HH:mm까지)`, 없으면 `MouseMover — 절전방지 중`.
- 커밋 시 해당 태스크 파일만 add(`git add -A` 금지).

---

## File Structure

| 파일 | 상태 | 책임 |
|---|---|---|
| `src/MouseMover/Settings.cs` | 수정 | ShowClock/CenterMessage 추가 |
| `tests/MouseMover.Tests/SettingsTests.cs` | 수정 | 새 필드 기본값+왕복 |
| `src/MouseMover/OverlayForm.cs` | 수정 | 중앙 시계/메시지 라벨 |
| `src/MouseMover/SettingsForm.cs` | 수정 | 컨트롤 2개 |
| `src/MouseMover/TrayAppContext.cs` | 수정 | KAO 툴팁 ETA |
| `README.md` | 수정 | 기능 문서 |

---

## Task 1: Settings에 ShowClock/CenterMessage 추가

**Files:** Modify `src/MouseMover/Settings.cs`, Test `tests/MouseMover.Tests/SettingsTests.cs`

**Interfaces:** Produces `bool Settings.ShowClock`(기본 false), `string Settings.CenterMessage`(기본 "").

- [ ] **Step 1: 실패 테스트 추가** (기존 SettingsTests 클래스에 메서드 추가)
```csharp
    [Fact]
    public void Display_option_defaults()
    {
        var s = new Settings();
        Assert.False(s.ShowClock);
        Assert.Equal("", s.CenterMessage);
    }

    [Fact]
    public void Display_options_round_trip()
    {
        var s = new Settings { ShowClock = true, CenterMessage = "회의 중 · 16시 복귀" };
        var back = Settings.FromJson(s.ToJson());
        Assert.True(back.ShowClock);
        Assert.Equal("회의 중 · 16시 복귀", back.CenterMessage);
    }
```

- [ ] **Step 2: 실패 확인** — `dotnet test --filter SettingsTests` → FAIL(속성 없음).

- [ ] **Step 3: 구현** — `Settings.cs`의 기존 속성들(`WorkDays` 등) 다음에 추가:
```csharp
    public bool ShowClock { get; set; }
    public string CenterMessage { get; set; } = "";
```

- [ ] **Step 4: 통과 확인** — `dotnet test --filter SettingsTests` → PASS.

- [ ] **Step 5: 전체 테스트** — `dotnet test` → 35 + 2 = 37 PASS.

- [ ] **Step 6: 커밋**
```bash
git add src/MouseMover/Settings.cs tests/MouseMover.Tests/SettingsTests.cs
git commit -m "feat: add ShowClock and CenterMessage settings"
```

---

## Task 2: OverlayForm 중앙 시계/메시지

**Files:** Modify `src/MouseMover/OverlayForm.cs`

**Interfaces:** Consumes `Settings`(ShowClock/CenterMessage/LabelColorArgb). 공개 시그니처 변경 없음.

설계 메모: GUI — 단위 테스트 없음(빌드+수동). **구현 전 현재 `OverlayForm.cs`를 읽을 것.** 현재 ctor `(Rectangle, Settings, DateTime, Action)`, 우하단 `_label`+`_font`, `UpdateElapsed`가 `lines` 조립 후 `PositionLabel()`, `Dispose`에서 `Cursor.Show()`+`_font.Dispose()`.

- [ ] **Step 1: 필드 추가**
```csharp
    private readonly Label? _clockLabel;
    private readonly Label? _messageLabel;
    private readonly Font? _clockFont;
    private readonly Font? _messageFont;
```

- [ ] **Step 2: 생성자에서 중앙 라벨 생성** (기존 `Controls.Add(_label);` 다음, `UpdateElapsed(TimeSpan.Zero);` 호출 전에 삽입)
```csharp
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
```

- [ ] **Step 3: Shown 핸들러에 PositionCenter 추가** — 기존 `Shown += (_, _) => PositionLabel();`를 교체:
```csharp
        Shown += (_, _) => { PositionLabel(); PositionCenter(); };
```

- [ ] **Step 4: UpdateElapsed에서 시계 갱신** — 기존 메서드 끝 `PositionLabel();` 다음에 추가:
```csharp
        if (_clockLabel is not null)
        {
            _clockLabel.Text = DateTime.Now.ToString("HH:mm");
            PositionCenter();
        }
```

- [ ] **Step 5: PositionCenter 헬퍼 추가** (PositionLabel 옆)
```csharp
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
```

- [ ] **Step 6: Dispose에서 폰트 정리** — 기존 `if (disposing) { Cursor.Show(); _font.Dispose(); }`를:
```csharp
        if (disposing)
        {
            Cursor.Show();
            _font.Dispose();
            _clockFont?.Dispose();
            _messageFont?.Dispose();
        }
```

- [ ] **Step 7: 빌드 + 전체 테스트** — `dotnet build`(0 에러) → `dotnet test`(37 PASS).

- [ ] **Step 8: 커밋**
```bash
git add src/MouseMover/OverlayForm.cs
git commit -m "feat: show center clock and message on overlay"
```

---

## Task 3: SettingsForm 컨트롤 추가

**Files:** Modify `src/MouseMover/SettingsForm.cs`

**Interfaces:** Consumes `Settings`. `Commit()`이 ShowClock/CenterMessage 채움. 공개 API 유지.

설계 메모: GUI — 빌드+수동. **구현 전 현재 `SettingsForm.cs`를 읽을 것.** 현재 루트 `layout`은 2열 AutoSize TableLayoutPanel, 행 0~11 사용, 버튼 행은 row 12(2열 span). `Commit()`은 `Result = new Settings { ...12필드... }`.

- [ ] **Step 1: 컨트롤 필드 추가** (기존 필드 선언부에)
```csharp
    private readonly CheckBox _showClock = new() { Text = "덮개 중앙 시계 표시", AutoSize = true };
    private readonly TextBox _centerMessage = new() { Width = 220 };
```

- [ ] **Step 2: 생성자 초기화** (기존 `_runAtStartup.Checked = ...` 부근/요일 초기화 옆)
```csharp
        _showClock.Checked = current.ShowClock;
        _centerMessage.Text = current.CenterMessage;
```

- [ ] **Step 3: 레이아웃에 추가 + 버튼 행 이동** — 기존 `근무 요일` 행(row 11) 다음, 버튼 행 앞에 삽입하고 버튼 행 인덱스를 12 → 14로 변경:
```csharp
        layout.Controls.Add(_showClock, 1, 12);
        layout.Controls.Add(new Label { Text = "덮개 메시지", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 13);
        layout.Controls.Add(_centerMessage, 1, 13);
```
그리고 기존 버튼 추가부 `layout.Controls.Add(buttons, 0, 12);`를 `layout.Controls.Add(buttons, 0, 14);`로 변경(SetColumnSpan은 그대로).

- [ ] **Step 4: Commit 확장** — 기존 `Result = new Settings { ... }`에 두 줄 추가(기존 12필드 유지):
```csharp
            ShowClock = _showClock.Checked,
            CenterMessage = _centerMessage.Text,
```

- [ ] **Step 5: 빌드 + 전체 테스트** — `dotnet build`(0 에러/경고) → `dotnet test`(37 PASS).

- [ ] **Step 6: 커밋**
```bash
git add src/MouseMover/SettingsForm.cs
git commit -m "feat: add center clock/message controls to settings dialog"
```

---

## Task 4: TrayAppContext KAO 툴팁 ETA

**Files:** Modify `src/MouseMover/TrayAppContext.cs`

**Interfaces:** Consumes `StopPolicy.NextAutoStop`. 공개 시그니처 변경 없음.

설계 메모: 빌드+스모크+수동. **구현 전 현재 `TrayAppContext.cs`를 읽을 것.** 현재 `_keepAwakeOnlyTimer.Tick`이 `ShouldAutoStop` 검사만; `StartKeepAwakeOnly`가 `_tray.Text = "MouseMover — 절전방지 중";` 고정.

- [ ] **Step 1: 툴팁 헬퍼 추가**
```csharp
    private string KeepAwakeTooltip()
    {
        var next = StopPolicy.NextAutoStop(_kaoSettings, _kaoStartLocal);
        return next is { } t
            ? $"MouseMover — 절전방지 중 ({t:HH:mm}까지)"
            : "MouseMover — 절전방지 중";
    }
```

- [ ] **Step 2: 타이머 tick에서 툴팁 갱신** — 기존 tick 핸들러를:
```csharp
        _keepAwakeOnlyTimer.Tick += (_, _) =>
        {
            if (StopPolicy.ShouldAutoStop(_kaoSettings, _kaoStartLocal, DateTime.Now))
            {
                StopKeepAwakeOnly();
                return;
            }
            _tray.Text = KeepAwakeTooltip();
        };
```

- [ ] **Step 3: 시작 시 즉시 반영** — `StartKeepAwakeOnly`의 `_tray.Text = "MouseMover — 절전방지 중";`을 `_tray.Text = KeepAwakeTooltip();`로 변경. `StopKeepAwakeOnly`의 기본 툴팁 복원은 현행 유지.

- [ ] **Step 4: 빌드 + 테스트 + 스모크** — `dotnet build`(0 에러) → `dotnet test`(37 PASS) → exe 백그라운드 실행 ~4초 크래시 없음 확인 후 종료.

- [ ] **Step 5: 커밋**
```bash
git add src/MouseMover/TrayAppContext.cs
git commit -m "feat: show auto-stop ETA in keep-awake-only tray tooltip"
```

---

## Task 5: README

**Files:** Modify `README.md`

- [ ] **Step 1: 기능 추가** — 적절한 위치에:
```markdown
- **덮개 중앙 시계/메시지:** 설정에서 켜면 검은 덮개 중앙에 큰 시계(HH:mm)와 커스텀 메시지(예: `회의 중 · 16시 복귀`)를 표시.
```
그리고 절전방지 전용 모드 설명에 "트레이 툴팁에 자동 해제 예정 시각 표시" 한 마디 추가.

- [ ] **Step 2: 커밋**
```bash
git add README.md
git commit -m "docs: document center clock/message and tooltip ETA"
```

---

## Self-Review 결과

**Spec coverage:** ShowClock/CenterMessage 필드 → T1 ✓ / 덮개 중앙 시계·메시지 렌더 → T2 ✓ / 설정 컨트롤 → T3 ✓ / 툴팁 ETA → T4 ✓ / 기본 off 동작 불변 → T2(조건부 생성) ✓ / 문서 → T5 ✓.

**Placeholder scan:** 코드/명령 실제 내용. TBD 없음.

**Type consistency:** `Settings.ShowClock:bool`/`CenterMessage:string`, `OverlayForm.PositionCenter()`, `TrayAppContext.KeepAwakeTooltip()` + `StopPolicy.NextAutoStop(Settings,DateTime)` 재사용 — 일치.
