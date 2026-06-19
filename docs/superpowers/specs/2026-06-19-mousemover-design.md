# MouseMover — 설계 문서

작성일: 2026-06-19

## 목적

PC가 주기적으로 절전 모드로 들어가는 것을 방지하고, 동시에 화면 내용을
가려 자리를 비울 때 화면이 보이지 않게 하는 Windows 트레이 유틸리티.

## 요구사항

- 주기적으로 마우스를 움직여 절전(화면/시스템) 모드를 방지한다.
- "덮개" 시작 시 연결된 **모든 모니터**를 검은 전체화면으로 가린다.
- 우하단 모서리에 작은 상태 표기: 상태 텍스트 + 경과시간 + 해제 안내.
- **아무 키 입력 또는 마우스 클릭**으로 즉시 해제한다.
- 트레이 아이콘으로 실행/제어한다.
- 절전방지는 마우스 지글 + Win32 API를 함께 사용한다(가장 확실).

## 스택

- C# WinForms, .NET 8 (`net8.0-windows`).
- 단일 실행 파일 빌드 (`PublishSingleFile=true`, self-contained 선택).
- 커스텀 트레이/앱 아이콘 (`app.ico`).

## 컴포넌트

각 파일은 단일 책임을 가진다.

| 파일 | 역할 | 의존 |
|---|---|---|
| `Program.cs` | 진입점. 단일 인스턴스 mutex. `Application.Run(TrayAppContext)` | TrayAppContext |
| `TrayAppContext.cs` | 트레이 아이콘 + 컨텍스트 메뉴(덮개 시작 / 종료). 시작·중지 조율 | OverlayManager, KeepAwake |
| `KeepAwake.cs` | 타이머로 마우스 지글(SendInput) + `SetThreadExecutionState` 설정/복원 | NativeMethods |
| `OverlayManager.cs` | `Screen.AllScreens` 순회 → 모니터마다 `OverlayForm` 생성/해제. 경과시간 타이머 보유 | OverlayForm |
| `OverlayForm.cs` | 검은 전체화면 폼 + 우하단 라벨. 키/클릭 입력 → 해제 콜백 호출 | — |
| `NativeMethods.cs` | P/Invoke 정의 (SendInput, INPUT/MOUSEINPUT, SetThreadExecutionState, ES_* 플래그) | — |

## 데이터 흐름

1. 앱 시작 → 트레이 아이콘 상주(덮개는 시작 안 함).
2. 트레이 메뉴 "덮개 시작" 클릭:
   - `OverlayManager.Start()` → 모든 모니터에 검은 폼 표시, 경과시간 타이머(1초) 시작.
   - `KeepAwake.Start()` → 지글 타이머(45초) 시작 + 실행상태 플래그 설정.
3. 임의 폼에서 KeyDown 또는 MouseDown 발생:
   - `OverlayManager`가 해제 콜백 호출 → 모든 폼 dispose + 경과시간 타이머 중지.
   - `KeepAwake.Stop()` → 지글 타이머 중지 + 실행상태 복원.
4. 트레이 메뉴 "종료" → KeepAwake 복원 보장 후 앱 종료.

## 동작 상세

### Keep-awake

- **마우스 지글:** 45초마다 `SendInput`으로 상대이동 +1px → 즉시 -1px(제자리 복귀).
  커서 위치는 실질적으로 변하지 않지만 OS 유휴 타이머는 리셋된다.
- **실행상태 API:** 시작 시
  `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)`,
  중지/종료 시 `SetThreadExecutionState(ES_CONTINUOUS)`로 복원.
- 복원은 `finally` / `Dispose`에서 반드시 보장(앱이 비정상 종료해도 OS가 스레드 종료 시 자동 해제).

### 오버레이

- `FormBorderStyle = None`, `TopMost = true`, `BackColor = Black`, `ShowInTaskbar = false`.
- 각 폼을 해당 `Screen.Bounds`에 맞춰 위치/크기 지정(`StartPosition = Manual`).
- 커서 숨김(`Cursor.Hide()` 또는 투명 커서).
- `KeyPreview = true`. **해제 트리거는 KeyDown + MouseDown(클릭)만.**
  마우스 *이동*은 무시 → 지글이 자기 자신을 해제하지 못하게 함.

### 우하단 라벨

- 내용(3줄 또는 한 줄):
  - `절전방지 중`
  - 경과시간 `HH:MM:SS`
  - `아무 키나 클릭하면 해제`
- 폼 우하단에 앵커(`Anchor = Bottom | Right`), 모든 모니터에 표시.
- 1초마다 경과시간 갱신.

### 아이콘

- `app.ico` — 32x32(+16x16) 간단한 마우스 + 달/zzz 모티프.
- PowerShell + `System.Drawing` 생성 스크립트(`tools/make-icon.ps1`)로 만들어 repo에 커밋.
- `.csproj`의 `ApplicationIcon`과 `NotifyIcon.Icon`에 사용.

## 에러 처리

- `SendInput` 실패(반환 0)는 무시(로그만, v1은 로그 생략 가능).
- 실행상태 플래그는 어떤 경로로 중지하든 복원.
- 단일 인스턴스: named `Mutex`로 중복 실행 방지.

## 설정 (v1)

- 지글 간격: 상수 45초(코드 상수). 트레이 설정 UI는 YAGNI — 추후.

## 테스트

- 단위 테스트 가능 로직을 분리:
  - 경과시간 → `HH:MM:SS` 포맷 함수.
  - `KeepAwake` 틱 동작(SendInput 호출을 인터페이스로 추상화해 검증).
- 오버레이 표시/다중 모니터/해제는 수동 확인.

## 빌드/배포

- `dotnet build` / `dotnet publish -c Release -r win-x64 --self-contained`
  `-p:PublishSingleFile=true` → 단일 `MouseMover.exe`.

## 프로젝트 구조

```
MouseMover/
  MouseMover.sln
  src/MouseMover/
    MouseMover.csproj
    Program.cs
    TrayAppContext.cs
    KeepAwake.cs
    OverlayManager.cs
    OverlayForm.cs
    NativeMethods.cs
    app.ico
  tools/make-icon.ps1
  tests/MouseMover.Tests/        (단위 테스트)
  docs/superpowers/specs/
```
