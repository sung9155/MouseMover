# Idle-aware Jiggle — Design Spec

**Date:** 2026-06-19
**Status:** Approved (design)
**Topic:** Skip the keep-awake mouse jiggle while the user is actively using the PC.

## Problem

`KeepAwake` runs a `System.Windows.Forms.Timer` that fires every `JiggleSeconds`
and unconditionally calls `IInputSender.Jiggle()` (a +1px/-1px relative mouse
move). This injects synthetic input even while the user is actively typing or
moving the mouse, which can fight the user's own cursor and is unnecessary —
when the user is active there is nothing to keep alive.

Sleep prevention itself is handled separately by
`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)`
and does **not** depend on the jiggle. The jiggle's real purpose is keeping
presence/idle detectors (e.g. Teams/Slack "active", corporate idle counters)
from flipping to away. That purpose is only relevant when the user is idle.

## Goal

On each timer tick, jiggle only if the user has been idle for at least
`JiggleSeconds`. If the user was active more recently than that, skip the tick.
Sleep prevention via `SetThreadExecutionState` is unchanged and always active
while `KeepAwake` is running.

This is **always-on behavior** — no new setting, no UI, no `settings.json`
field.

## Non-goals

- No separate/configurable idle threshold (the gate is exactly `JiggleSeconds`).
- No on/off toggle for the feature.
- No change to sleep-prevention semantics.
- No change to the overlay/cover, scheduling, or tray.

## Architecture

Three production touches plus tests and a README line.

### 1. `NativeMethods.cs` — idle-time source

Add P/Invoke and a small real provider:

- `LASTINPUTINFO` struct (`cbSize`, `dwTime`).
- `[DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO)`.
- `static class Win32IdleTime { static TimeSpan Get() }` computing
  `Environment.TickCount64 - lastInput.dwTime` as milliseconds → `TimeSpan`.
  `Environment.TickCount64` is used (not `TickCount`/`GetTickCount`) to avoid the
  49.7-day 32-bit wrap. `dwTime` is a 32-bit tick value; the subtraction is done
  against the low 32 bits of `TickCount64` so the delta is correct across the
  uint boundary.

### 2. `KeepAwake.cs` — gate the tick

- Constructor gains an optional trailing parameter:
  `KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45, Func<TimeSpan>? idleTime = null)`.
  Store `_idleTime = idleTime ?? Win32IdleTime.Get`. The optional parameter keeps
  existing call sites (`new KeepAwake(sender, flags => ...)`) compiling unchanged.
- Extract the tick body into a private method:

  ```csharp
  private void Tick()
  {
      if (_idleTime() >= TimeSpan.FromSeconds(JiggleSeconds))
          _sender.Jiggle();
  }
  ```

- Wire `_timer.Tick += (_, _) => Tick();` and route `TickForTest()` through the
  same `Tick()` so the gate is exercised in tests (it no longer calls `Jiggle()`
  directly).

### 3. `TrayAppContext.cs`

No change required — it constructs
`new KeepAwake(new Win32InputSender(), ...)` and the new parameter defaults to
the real provider. Keep-awake-only mode reuses the same `KeepAwake` instance and
therefore inherits the behavior for free.

## Data flow

```
timer tick ─▶ Tick() ─▶ idleTime() ─▶ idle >= JiggleSeconds ? Jiggle() : skip
SetThreadExecutionState(...) set on Start(), cleared on Stop() — independent.
```

## Edge cases

- **Just clicked Start while active:** first tick is skipped (user present). Fine.
- **Tick-count wrap:** avoided via `Environment.TickCount64`.
- **Keep-awake-only mode:** same instance → same gating.
- **Jiggle resets idle:** the synthetic move counts as input and resets
  `GetLastInputInfo`, so after a jiggle the next tick sees ~`JiggleSeconds` of
  idle again and re-jiggles — steady-state keep-alive while genuinely idle.

## Testing

xUnit, existing `FakeSender`, idle injected as `Func<TimeSpan>` — no native call:

- **Update** `Tick_jiggles_once` → inject `() => TimeSpan.FromHours(1)`, assert
  one jiggle.
- **Add** `Tick_skips_when_user_active` → inject `() => TimeSpan.Zero`, tick,
  assert `JiggleCount == 0`.
- **Add** `Tick_jiggles_when_idle_past_period` → idle just over `JiggleSeconds`,
  assert one jiggle.

Existing `Start_*` / `Stop_*` / `JiggleSeconds_*` tests are unaffected.

## Effort

Small: one logic edit (`KeepAwake`), one native helper (`NativeMethods`), three
test changes, one README line. No new setting, no UI, no schema change.
