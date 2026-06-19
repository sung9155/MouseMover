# Idle-aware Jiggle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Skip the keep-awake mouse jiggle while the user is actively using the PC; jiggle only after the user has been idle for at least `JiggleSeconds`.

**Architecture:** Add a Win32 `GetLastInputInfo`-based idle-time source in `NativeMethods.cs`, then gate `KeepAwake`'s timer tick on that idle time. Idle time is injected into `KeepAwake` as a `Func<TimeSpan>` so the gate is unit-testable with no native call. Sleep prevention (`SetThreadExecutionState`) is unchanged and always on while running.

**Tech Stack:** C# / .NET 9 WinForms, P/Invoke (user32), xUnit.

## Global Constraints

- Target framework: `.NET 9` (`net9.0-windows`).
- Always-on behavior: no new `settings.json` field, no UI control, no toggle.
- The idle gate threshold is exactly `JiggleSeconds` (no separate threshold).
- `SetThreadExecutionState` sleep-prevention semantics must not change.
- Use `Environment.TickCount64` (not `TickCount`/`GetTickCount`) to avoid the 32-bit 49.7-day wrap.
- Existing `KeepAwake` call sites must keep compiling — the new constructor parameter is optional and trailing.

---

### Task 1: Win32 idle-time source

**Files:**
- Modify: `src/MouseMover/NativeMethods.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `NativeMethods.LASTINPUTINFO` struct (`uint cbSize`, `uint dwTime`).
  - `[DllImport("user32.dll")] static extern bool NativeMethods.GetLastInputInfo(ref LASTINPUTINFO plii)`.
  - `static class Win32IdleTime { static TimeSpan Get(); }` — real idle-time provider used as `KeepAwake`'s default (Task 2).

This task adds P/Invoke and a thin real provider. Like the existing `SetThreadExecutionState`/`SendInput` declarations, it is not unit-tested directly; its consumer (`KeepAwake`) is tested via an injected `Func<TimeSpan>` seam in Task 2. Verification here is a successful build.

- [ ] **Step 1: Add the P/Invoke, struct, and provider**

In `src/MouseMover/NativeMethods.cs`, inside `public static class NativeMethods`, add the declaration next to the other `[DllImport]` members:

```csharp
[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
```

Add the struct alongside the other `[StructLayout]` structs in the same class:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}
```

Then add a new top-level provider class in the same file (after the `NativeMethods` class, still in `namespace MouseMover;`):

```csharp
public static class Win32IdleTime
{
    // Milliseconds since the last user input (keyboard/mouse), as a TimeSpan.
    public static TimeSpan Get()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // dwTime is a 32-bit tick value. Subtract against the low 32 bits of
        // TickCount64 with unsigned wrap so the delta is correct across the
        // uint boundary; TickCount64 avoids the 49.7-day 32-bit overall wrap.
        uint now = unchecked((uint)Environment.TickCount64);
        uint idleMs = unchecked(now - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
```

`using System.Runtime.InteropServices;` is already present at the top of the file (used by the existing P/Invokes), so no new using is required.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/MouseMover -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/MouseMover/NativeMethods.cs
git commit -m "feat: add Win32 idle-time source (GetLastInputInfo)"
```

---

### Task 2: Gate the jiggle on idle time

**Files:**
- Modify: `src/MouseMover/KeepAwake.cs`
- Test: `tests/MouseMover.Tests/KeepAwakeTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `Win32IdleTime.Get` (Task 1), `IInputSender` (existing).
- Produces:
  - New `KeepAwake` constructor signature:
    `KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45, Func<TimeSpan>? idleTime = null)`.
  - Private `void Tick()` — jiggles iff `idleTime() >= TimeSpan.FromSeconds(JiggleSeconds)`. Used by the timer and by `TickForTest()`.

- [ ] **Step 1: Update and add the failing tests**

In `tests/MouseMover.Tests/KeepAwakeTests.cs`, **replace** the existing `Tick_jiggles_once` test with an idle-injecting version, and **add** two new tests. The `FakeSender` helper already exists at the top of the file — reuse it.

Replace this existing test:

```csharp
    [Fact]
    public void Tick_jiggles_once()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { });
        ka.Start();

        ka.TickForTest();

        Assert.Equal(1, sender.JiggleCount);
    }
```

with:

```csharp
    [Fact]
    public void Tick_jiggles_when_idle_at_period()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { }, 45, () => TimeSpan.FromHours(1));
        ka.Start();

        ka.TickForTest();

        Assert.Equal(1, sender.JiggleCount);
    }

    [Fact]
    public void Tick_skips_when_user_active()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { }, 45, () => TimeSpan.Zero);
        ka.Start();

        ka.TickForTest();

        Assert.Equal(0, sender.JiggleCount);
    }

    [Fact]
    public void Tick_jiggles_when_idle_just_past_period()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { }, 30, () => TimeSpan.FromSeconds(31));
        ka.Start();

        ka.TickForTest();

        Assert.Equal(1, sender.JiggleCount);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~KeepAwakeTests"`
Expected: FAIL — the 4-argument `KeepAwake` constructor does not exist yet (compile error), and/or `Tick_skips_when_user_active` would fail because the current tick always jiggles.

- [ ] **Step 3: Implement the gate in `KeepAwake`**

In `src/MouseMover/KeepAwake.cs`:

Add a field next to the existing readonly fields:

```csharp
    private readonly Func<TimeSpan> _idleTime;
```

Change the constructor from:

```csharp
    public KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45)
    {
        _sender = sender;
        _setExecutionState = setExecutionState;
        _timer = new System.Windows.Forms.Timer { Interval = jiggleSeconds * 1000 };
        _timer.Tick += (_, _) => _sender.Jiggle();
    }
```

to:

```csharp
    public KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45, Func<TimeSpan>? idleTime = null)
    {
        _sender = sender;
        _setExecutionState = setExecutionState;
        _idleTime = idleTime ?? Win32IdleTime.Get;
        _timer = new System.Windows.Forms.Timer { Interval = jiggleSeconds * 1000 };
        _timer.Tick += (_, _) => Tick();
    }

    // 유휴 시간이 지글 주기 이상일 때만 지글 (사용자가 활동 중이면 건너뜀)
    private void Tick()
    {
        if (_idleTime() >= TimeSpan.FromSeconds(JiggleSeconds))
            _sender.Jiggle();
    }
```

Change `TickForTest` from:

```csharp
    // 테스트용: 타이머 콜백과 동일하게 한 번 지글
    public void TickForTest() => _sender.Jiggle();
```

to:

```csharp
    // 테스트용: 타이머 콜백과 동일한 유휴 게이트를 거쳐 한 번 처리
    public void TickForTest() => Tick();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~KeepAwakeTests"`
Expected: PASS — all `KeepAwakeTests` green (the 3 updated/new tick tests plus the unchanged `Start_*`/`Stop_*`/`JiggleSeconds_*` tests).

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all tests green (the prior 28 with `Tick_jiggles_once` replaced by 3 tick tests → 30 total).

- [ ] **Step 6: Update the README**

In `README.md`, under the `## 기능` list, update the **절전 방지** bullet (currently line ~7) to note the idle gate. Replace:

```markdown
- **절전 방지:** 주기적 마우스 지글 + Win32 `SetThreadExecutionState` API로 시스템/화면 절전 모드 방지.
```

with:

```markdown
- **절전 방지:** 주기적 마우스 지글 + Win32 `SetThreadExecutionState` API로 시스템/화면 절전 모드 방지. 지글은 사용자가 유휴 상태일 때(마지막 입력이 지글 주기 이상 경과)만 실행되어 작업 중 커서를 건드리지 않음. 절전 차단 자체는 항상 동작.
```

- [ ] **Step 7: Commit**

```bash
git add src/MouseMover/KeepAwake.cs tests/MouseMover.Tests/KeepAwakeTests.cs README.md
git commit -m "feat: jiggle only while user idle (idle-aware keep-awake)"
```

---

## Self-Review

**Spec coverage:**
- Idle source (`GetLastInputInfo`, `Win32IdleTime.Get`, `TickCount64` wrap handling) → Task 1.
- Gate tick on `idleTime() >= JiggleSeconds`, injected `Func<TimeSpan>`, optional trailing ctor param for backward compat, `TickForTest` routed through gate → Task 2.
- Sleep prevention unchanged → `Start()`/`Stop()` untouched; verified by the unchanged `Start_*`/`Stop_*` tests still passing (Task 2 Step 5).
- No new setting / no UI → confirmed; `Settings.cs`, `SettingsForm.cs`, `TrayAppContext.cs` untouched.
- Tests: update `Tick_jiggles_once`, add skip-when-active and jiggle-when-idle → Task 2 Step 1.
- README line → Task 2 Step 6.

**Placeholder scan:** No TBD/TODO; all code shown in full.

**Type consistency:** `KeepAwake(IInputSender, Action<uint>, int=45, Func<TimeSpan>?=null)`, `Win32IdleTime.Get()` returning `TimeSpan`, `NativeMethods.LASTINPUTINFO`/`GetLastInputInfo(ref ...)`, private `Tick()` referenced by both the timer and `TickForTest()` — names and signatures match across Task 1 and Task 2.
