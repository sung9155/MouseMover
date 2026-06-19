# MouseMover

PC 절전 모드 방지 + 화면 가리기 Windows 트레이 유틸리티.

## 기능

- **절전 방지:** 주기적 마우스 지글 + Win32 `SetThreadExecutionState` API로 시스템/화면 절전 모드 방지.
- **화면 가리기:** 모든 모니터를 검은 전체화면 오버레이로 가림.
- **상태 표시:** 오버레이 우하단에 상태 텍스트, 경과시간, 해제 안내 표시 (설정에서 각각 토글 가능).
- **즉시 해제:** 아무 키 또는 마우스 클릭으로 즉시 해제.
- **트레이 제어:** 시스템 트레이 아이콘으로 제어 (우클릭 메뉴에서 `설정...` / `덮개 시작` / `종료`). 단일 인스턴스 보장.

## 설정

우클릭 메뉴의 `설정...`에서 다음을 변경할 수 있습니다:

- **지글 주기:** 5–600초 (기본값: 45초)
- **상태 텍스트:** 오버레이에 표시할 텍스트 (기본값: "절전방지 중")
- **경과시간 표시:** 토글 (기본값: 켜짐)
- **해제 안내 표시:** 토글 (기본값: 켜짐)
- **글자 크기:** 8–48pt (기본값: 11pt)
- **글자 색:** 색 선택 대화상자 (기본값: 회색)
- **Windows 시작 시 자동 실행:** 토글 (기본값: 꺼짐)
- **자동 종료:** 덮개 시작 후 일정 시간(없음/30분/1시간/2시간/4시간) 뒤 자동 해제.
- **요일 스케줄:** 근무 시간대(시작~종료)와 요일을 지정하면, 근무 시간에 켠 덮개가 근무 종료 시 자동 해제. 비근무 시간의 수동 시작은 막지 않음. (자정 넘는 근무는 미지원)

설정은 `%APPDATA%\MouseMover\settings.json`에 자동 저장됩니다.

시각/주기 변경은 다음 "덮개 시작"부터 적용됩니다.

## 빌드

### 필수 요구사항
- .NET 9 SDK

### 명령어

```
dotnet publish src/MouseMover -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

산출물: `src/MouseMover/bin/Release/net9.0-windows/win-x64/publish/MouseMover.exe`

## 개발

- **테스트:** `dotnet test` (15개 테스트)
- **실행:** `dotnet run --project src/MouseMover`
- **아이콘 재생성:** `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1`
