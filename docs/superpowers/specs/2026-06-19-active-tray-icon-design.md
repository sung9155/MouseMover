# 활성 시 움직이는 트레이 아이콘 (Animated Active Tray Icon)

날짜: 2026-06-19

## 목적

절전방지가 동작 중일 때 트레이 아이콘을 움직이게 해 사용자가 한눈에 활성 여부를
알 수 있게 한다. 현재 아이콘은 정적이라 켜짐/꺼짐 구분이 안 됨.

## 범위

- **활성 표현:** 움직이는 아이콘(애니메이션). 우하단에 초록 "활성" 점 배지, 반경+투명도
  맥동, 4프레임, ~400ms 간격(1.6초 루프).
- **활성 상태:** 덮개 모드 + 절전방지 전용 모드 둘 다. (둘 다 PC를 깨워 둠)
- **비활성:** base 정적 아이콘(현행 `app.ico`) 그대로.

## 현재 구조

- `tools/make-icon.ps1`이 빌드 외 시점에 `app.ico` 생성(네이비 원 + 흰 마우스 + 노란 달).
- `MouseMover.csproj`가 `app.ico`를 출력에 복사(`CopyToOutputDirectory=PreserveNewest`).
- `TrayAppContext` ctor가 `BaseDirectory/app.ico`를 `_icon`으로 로드(`_ownsIcon`로 dispose
  여부 추적), 없으면 `SystemIcons.Application`로 폴백. `_tray.Icon = _icon` 정적 설정.
- 상태 전환(StartCover/OnDismissed/StartKeepAwakeOnly/StopKeepAwakeOnly)은 모두
  `UpdateMenuState()`를 호출 → 메뉴 활성/텍스트 일원화.

## 설계 (런타임 합성)

base 아이콘을 그대로 로드하고, 그 위에 맥동 점을 C#으로 덧그려 활성 프레임을 만든다.
새 파일/빌드스크립트/csproj 변경 없음. 프레임이 항상 base 아트와 일치.

### 1. NativeMethods (`src/MouseMover/NativeMethods.cs`)
- P/Invoke 추가:
  ```csharp
  [DllImport("user32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool DestroyIcon(IntPtr hIcon);
  ```
  `Bitmap.GetHicon()`로 만든 HICON 핸들 누수 방지용.

### 2. 신규 클래스 `ActiveIconFrames` (`src/MouseMover/ActiveIconFrames.cs`)
- 단일 책임: base `Icon`으로 활성 애니메이션 프레임 묶음 생성·보관. `IDisposable`.
- 생성자 `ActiveIconFrames(Icon baseIcon, int size = 32)`:
  - 4개 프레임 생성. 각 프레임:
    - `size`×`size` 비트맵에 `baseIcon`을 그림(`Graphics.DrawIcon`).
    - 우하단에 초록 점 오버레이. 프레임 i에 따라 반경·알파를 맥동
      (예: 단계 `[0.5, 0.8, 1.0, 0.8]` 비율로 반경/알파 스케일).
    - `bmp.GetHicon()` → `Icon.FromHandle(h)` → `.Clone()`으로 관리 복제본 보관 →
      원본 `h`는 `NativeMethods.DestroyIcon(h)`로 즉시 해제.
- `IReadOnlyList<Icon> Frames { get; }` 노출.
- `Dispose()`: 모든 프레임 `Icon.Dispose()`.

### 3. TrayAppContext (`src/MouseMover/TrayAppContext.cs`)
- 필드 추가: `ActiveIconFrames _activeFrames`, `System.Windows.Forms.Timer _animTimer`,
  `int _animIndex`.
- ctor: `_icon` 로드 직후 `_activeFrames = new ActiveIconFrames(_icon);`
  `_animTimer = new() { Interval = 400 }; _animTimer.Tick += OnAnimTick;`
- `OnAnimTick`: `_animIndex = (_animIndex + 1) % _activeFrames.Frames.Count;`
  `_tray.Icon = _activeFrames.Frames[_animIndex];`
- `UpdateMenuState()` 끝에 `UpdateTrayIcon()` 호출(또는 인라인):
  - `bool active = _overlay.IsActive || _keepAwakeOnlyActive;`
  - active이고 타이머 미동작 → `_animIndex = 0; _animTimer.Start();`
  - 비활성 → `_animTimer.Stop(); _tray.Icon = _icon;` (base 복귀)
- Dispose: `_animTimer.Stop(); _animTimer.Dispose(); _activeFrames.Dispose();`
  (기존 `_icon` dispose는 `_ownsIcon` 가드 유지)

## 데이터 흐름

```
StartCover / StartKeepAwakeOnly
  └─ UpdateMenuState() → UpdateTrayIcon()
       └─ active == true → _animTimer.Start()
            └─ OnAnimTick (400ms) → _tray.Icon = Frames[++i % n]   # 맥동

OnDismissed / StopKeepAwakeOnly
  └─ UpdateMenuState() → UpdateTrayIcon()
       └─ active == false → _animTimer.Stop(); _tray.Icon = _icon  # 정적 복귀
```

## 오류 처리

- base가 `SystemIcons.Application`(공유)로 폴백돼도 합성은 동작(비트맵에 그릴 뿐).
  공유 아이콘 핸들은 파괴하지 않음 — 우리가 만든 GetHicon 핸들만 `DestroyIcon`.
- `GetHicon` 실패 등 예외 시 해당 프레임 생성 실패 가능 → 프레임 0개면 애니메이션
  비활성, `_tray.Icon`은 base 유지(안전). (방어적으로 Frames 비었으면 타이머 시작 안 함)

## 테스트 전략

- GDI+ / WinForms / 파일 로드 결합이라 단위 테스트 불가 → 수동 검증:
  앱 실행 → 덮개 또는 절전방지 시작 시 트레이 아이콘 점 맥동, 중지 시 정적 base 복귀.
- 프레임 인덱스 전진(`(i+1)%n`)은 사소해 별도 테스트 없음.

## YAGNI / 비포함

- 사전생성 .ico 프레임(make-icon.ps1/csproj 확장) — 제외(런타임 합성 채택).
- 점 색/속도/프레임 수 사용자 설정 — 제외(고정값). 필요 시 후속.
- 회전 등 다른 애니메이션 — 제외(맥동 점 채택).
