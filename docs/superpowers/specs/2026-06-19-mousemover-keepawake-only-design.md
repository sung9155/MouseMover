# MouseMover 절전방지 전용 모드 — 설계 문서

작성일: 2026-06-19

## 목적

화면 덮개(검은 오버레이) 없이 절전 모드만 방지하는 모드를 추가한다. 화면은
그대로 보이되 마우스 지글 + `SetThreadExecutionState`로 시스템/화면 절전만 막는다.

## 요구사항

- 트레이 메뉴에서 덮개 없이 절전방지만 시작/중지.
- 오버레이가 없어 키/클릭 해제가 불가 → **트레이 메뉴 토글**로 중지(`절전방지만
  시작` ↔ `절전방지 중지`).
- 자동 종료 타이머와 요일(퇴근) 스케줄은 이 모드에서도 **적용**된다.
- 덮개 모드와 **상호 배타**: 동시에 둘 다 실행 불가.
- 시각 피드백이 없으므로 활성 시 트레이 툴팁을 `절전방지 중`으로 표시.

## 트레이 메뉴

```
설정...
덮개 시작
자동 종료 ▸ (30분/1시간/2시간/4시간)
절전방지만 시작      ← 활성 시 "절전방지 중지"로 토글
종료
```

## 동작

- `절전방지만 시작` 클릭:
  1. `_overlay.IsActive` 또는 이미 절전방지전용 활성이면 무시(상호 배타).
  2. 저장된 `_settings` 사용. `_keepAwake.JiggleSeconds = _settings.JiggleSeconds`,
     `_keepAwake.Start()`.
  3. 시작 로컬시각 기록, 1초 자동종료 타이머 시작.
  4. 메뉴 상태 갱신, 툴팁 `절전방지 중`.
- 1초 타이머 tick: `StopPolicy.ShouldAutoStop(_kaoSettings, _kaoStartLocal,
  DateTime.Now)`가 true면 자동 중지(자동종료/퇴근 스케줄).
- `절전방지 중지` 클릭 또는 자동 중지: 타이머 정지, `_keepAwake.Stop()`(실행상태
  복원), 메뉴/툴팁 원복.

## 상호 배타 & 메뉴 상태 (UpdateMenuState)

| 상태 | 덮개 시작 | 자동 종료 ▸ | 절전방지만 토글 |
|---|---|---|---|
| idle | 활성 | 활성 | 활성, "절전방지만 시작" |
| 덮개 활성 | 비활성 | 비활성 | 비활성 |
| 절전방지전용 활성 | 비활성 | 비활성 | 활성, "절전방지 중지" |

`UpdateMenuState()`:
- `idle = !_overlay.IsActive && !_keepAwakeOnlyActive`
- `_startItem.Enabled = idle; _autoOffMenu.Enabled = idle;`
- `_keepAwakeOnlyItem.Enabled = !_overlay.IsActive;`
- `_keepAwakeOnlyItem.Text = _keepAwakeOnlyActive ? "절전방지 중지" : "절전방지만 시작";`

기존 `StartCover`/`OnDismissed`도 수동 토글 대신 `UpdateMenuState()`를 호출하도록
정리(덮개 활성 시 절전방지전용 항목도 비활성됨).

## 컴포넌트 (TrayAppContext.cs 중심)

- 필드: `private readonly ToolStripMenuItem _keepAwakeOnlyItem;`,
  `private readonly System.Windows.Forms.Timer _keepAwakeOnlyTimer;`,
  `private bool _keepAwakeOnlyActive;`, `private Settings _kaoSettings = new();`,
  `private DateTime _kaoStartLocal;`.
- 생성자: 토글 항목 생성(click → `ToggleKeepAwakeOnly()`), 메뉴에 `자동 종료 ▸`
  다음·`종료` 앞에 삽입. 타이머(1초) 생성 + tick 핸들러.
- `ToggleKeepAwakeOnly()`: 활성이면 `StopKeepAwakeOnly()`, 아니면 `StartKeepAwakeOnly()`.
- `StartKeepAwakeOnly()` / `StopKeepAwakeOnly()`: 위 동작.
- `UpdateMenuState()`: 위 표.
- `Dispose(bool)`: `_keepAwakeOnlyTimer.Stop()` + `Dispose()` 추가. `_keepAwake.Dispose()`가
  실행상태를 복원하므로 별도 처리 불필요.

`StopPolicy`/`OverlayManager`/`Settings`/`OverlayForm`은 변경 없음.

## 엣지 케이스

- 덮개 활성 중 `절전방지만` 시도: 항목 비활성 + `StartKeepAwakeOnly` 가드로 무시.
- 절전방지전용 활성 중 `덮개 시작`/자동종료: 항목 비활성 + `StartCover` 가드.
- 자동 중지 후 메뉴/툴팁 원복.
- 종료(Dispose): KeepAwake 실행상태 복원 보장.

## 의도적 트레이드오프

자동종료 폴링이 덮개 모드(`OverlayManager`의 1초 타이머)와 절전방지전용
(`TrayAppContext`의 1초 타이머) 두 곳에서 `StopPolicy.ShouldAutoStop`를 호출한다.
검증된 오버레이/덮개 경로를 건드리지 않기 위해 작은 중복을 수용한다(둘 다 동일
순수 함수 사용 → 동작 일관).

## 테스트

- 자동종료 판정은 이미 `StopPolicy` 단위 테스트(35개)로 검증됨.
- 이 작업은 트레이 메뉴 배선 + 기존 `KeepAwake` 재사용 → 새 단위 테스트 없음.
  빌드 + 스모크 + 수동 확인.

## 파일

- Modify: `src/MouseMover/TrayAppContext.cs`
- Modify: `README.md`
