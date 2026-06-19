# 자동종료 시 화면 잠금 (Lock on Auto-Stop)

날짜: 2026-06-19

## 목적

덮개(화면 가리기) 모드가 **자동종료**(자동 종료 타이머 또는 요일/퇴근 스케줄 종료)될 때
Windows 화면을 자동으로 잠근다. 퇴근 시 PC가 잠긴 상태로 남도록 하기 위함.

사용자 요청은 "Ctrl+L"이었으나, 의도 확인 결과 진짜 목적은 **화면 잠금**이므로
합성 키 입력 대신 `LockWorkStation()` API를 사용한다. (합성 Win+L은 OS가 차단)

## 범위

- **동작:** Windows 화면 잠금 (`user32.dll` `LockWorkStation()`)
- **적용 모드:** 덮개 모드만. 절전방지 전용 모드는 제외.
- **트리거:** 자동종료일 때만. 키 입력/마우스 클릭에 의한 **수동 해제는 잠그지 않음**.
- **제어:** 신규 설정 토글 `LockOnAutoStop`, 기본값 꺼짐(`false`).

## 현재 구조 (관련 부분)

자동종료 판정은 순수 함수 `StopPolicy.ShouldAutoStop`가 담당. 두 경로에서 호출:

1. 덮개: `OverlayManager.RefreshElapsed` → `ShouldAutoStop` → `DismissAll`
2. 절전방지 전용: `TrayAppContext.OnKeepAwakeOnlyTick` → `ShouldAutoStop` → `StopKeepAwakeOnly`

문제: 덮개의 **수동 해제**(폼 키/클릭 콜백)도 `DismissAll`을 거친다. 즉 자동/수동이
같은 경로로 합쳐진다. 잠금은 자동 경로에서만 실행해야 하므로 두 경로를 분리해야 한다.

부수효과 주입 패턴은 이미 존재: `KeepAwake`는 `IInputSender`와 실행상태 람다를
생성자로 주입받는다. 잠금도 같은 방식으로 `OverlayManager`에 주입한다.

## 설계 (A안: 잠금 동작 주입 + 해제 경로 분리)

### 1. Settings (`src/MouseMover/Settings.cs`)
- 신규 필드 `public bool LockOnAutoStop { get; set; }`, 기본 `false`.
- `settings.json` 기존 JSON 직렬화로 자동 저장/로드.

### 2. Native (`src/MouseMover/NativeMethods.cs`)
- P/Invoke 추가:
  ```csharp
  [DllImport("user32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool LockWorkStation();
  ```

### 3. OverlayManager (`src/MouseMover/OverlayManager.cs`)
- 생성자에 `Action lockWorkstation` 인자 추가, 필드로 보관(`_lock`).
- `DismissAll()` → `DismissAll(bool lockAfter)`로 변경.
  - 폼 콜백(수동 해제)은 `() => DismissAll(false)` 전달.
  - `RefreshElapsed`의 자동종료 분기는 `DismissAll(_settings.LockOnAutoStop)`.
- 실행 순서: `Stop()`(폼 닫기) → `_onDismiss()` → `lockAfter`이면 `_lock()`.
  - 잠금을 마지막에 호출해 폼이 모두 닫힌 뒤 잠금 화면이 뜨도록 함.

### 4. TrayAppContext (`src/MouseMover/TrayAppContext.cs`)
- 조립 지점 변경:
  `_overlay = new OverlayManager(OnDismissed, () => NativeMethods.LockWorkStation());`
- 빠른 자동종료 프리셋(`StartCoverWith`)은 저장 설정을 복제하므로 `LockOnAutoStop`이
  그대로 따라간다. 별도 처리 불필요. 일관 동작.

### 5. SettingsForm (`src/MouseMover/SettingsForm.cs`)
- 체크박스 "자동 종료 시 화면 잠금" 추가, `LockOnAutoStop`에 바인딩.
- 기존 체크박스 레이아웃 패턴을 따른다.

### 6. README (`README.md`)
- "기능" 및 "설정" 절에 신규 토글 문서화.

## 데이터 흐름

```
RefreshElapsed (1초 타이머)
  └─ ShouldAutoStop == true (자동종료)
       └─ DismissAll(lockAfter = settings.LockOnAutoStop)
            ├─ Stop()        # 모든 오버레이 폼 닫기
            ├─ _onDismiss()  # KeepAwake 중지, 메뉴 갱신
            └─ if lockAfter: _lock()   # LockWorkStation()

OverlayForm 키/클릭 (수동 해제)
  └─ DismissAll(lockAfter = false)   # 잠금 안 함
```

## 오류 처리

- `LockWorkStation()`은 false 반환 가능(드묾). 반환값 무시 — 잠금 실패해도 앱은
  정상 종료 흐름 유지. 재시도/예외 처리 없음(잠금은 보조 기능, 실패가 치명적이지 않음).

## 테스트 전략

- `SettingsTests`: `LockOnAutoStop` 기본값 `false` 확인 + JSON 왕복 보존 (기존 패턴).
- 자동/수동 분기 및 잠금 게이트는 Win32 + WinForms UI 결합이라 단위 테스트 불가 →
  수동 검증(빌드 후 실제 실행). 자동종료 판정 자체는 `StopPolicyTests`가 이미 커버.

## YAGNI / 비포함

- 절전방지 전용 모드 잠금 — 제외(범위 결정).
- 수동 해제 시 잠금 — 제외.
- 합성 키 입력(실제 Ctrl+L 전송) — 제외(목적이 화면 잠금).
- 잠금 실패 재시도/알림 — 제외.
