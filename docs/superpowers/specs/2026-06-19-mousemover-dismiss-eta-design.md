# MouseMover 해제 예정 시각 표기 — 설계 문서

작성일: 2026-06-19

## 목적

오버레이 우하단 "해제 안내"에 자동 해제 예정 시각과 남은 시간을 표기한다.
자동 종료 타이머나 요일(퇴근) 스케줄로 인해 자동 해제가 예정된 경우, 언제
꺼지는지 사용자가 알 수 있게 한다.

## 요구사항

- 자동 해제 예정이 **없으면**: 현행 `아무 키나 클릭하면 해제` 유지.
- 자동 해제 예정이 **있으면**(자동 종료 타이머 또는 퇴근 스케줄): 예정 시각과
  남은 시간을 함께 표기.
- 표기 포맷(두 줄):
  ```
  18:00 자동 해제 예정 (59분 남음)
  아무 키나 클릭하면 해제
  ```
- 남은 시간은 1초마다 갱신(카운트다운).
- `해제 안내 표시`(ShowDismissHint)가 꺼져 있으면 안내 줄 자체를 표시하지 않음(현행).

## 핵심 로직: StopPolicy.NextAutoStop (순수 함수)

`src/MouseMover/StopPolicy.cs`에 추가:

```csharp
public static DateTime? NextAutoStop(Settings s, DateTime startLocal)
```

- 후보 1 — 자동 종료 타이머: `s.AutoOffMinutes > 0` → `startLocal.AddMinutes(s.AutoOffMinutes)`.
- 후보 2 — 퇴근 스케줄: `s.ScheduleEnabled && s.WorkStartMinutes < s.WorkEndMinutes &&
  IsWorkTime(s, startLocal)` → `startLocal.Date.AddMinutes(s.WorkEndMinutes)` (시작한 날의
  퇴근 시각).
- 두 후보 중 **이른 시각**(min) 반환. 후보가 하나도 없으면 `null`.
- 예정 시각은 시작 시점으로 결정되므로 현재 시각은 인자로 받지 않는다(남은 시간은
  호출자가 `nextStop - now`로 계산).
- 기존 `ShouldAutoStop`와 동일 경계 규칙(자동종료 `>=`, 스케줄 퇴근 분 도달 시 해제)을
  공유한다. `ShouldAutoStop`는 변경하지 않는다.

## 표기 (OverlayForm)

- `OverlayForm` 생성자에 `DateTime startLocal`을 추가로 받아 필드 저장.
- `UpdateElapsed(TimeSpan elapsed)`에서:
  - `nowLocal = _startLocal + elapsed`
  - `nextStop = StopPolicy.NextAutoStop(_settings, _startLocal)`
  - 안내 줄 구성(`ShowDismissHint`가 켜진 경우만):
    - `nextStop != null && (nextStop - nowLocal) > TimeSpan.Zero`:
      - 줄1: `$"{nextStop:HH:mm} 자동 해제 예정 ({remaining}분 남음)"`,
        `remaining = Math.Max(1, (int)Math.Ceiling((nextStop.Value - nowLocal).TotalMinutes))`
      - 줄2: `아무 키나 클릭하면 해제`
    - 그 외: `아무 키나 클릭하면 해제` (현행 한 줄)
  - 첫 줄(StatusText)과 경과시간(ShowElapsed) 줄은 현행 유지.

## 연동 (OverlayManager)

- `OverlayManager.Start(Settings)`가 이미 `_startLocal`을 기록하므로, 폼 생성 시
  `new OverlayForm(screen.Bounds, settings, _startLocal, DismissAll)`로 전달.
- 나머지(타이머, RefreshElapsed의 ShouldAutoStop 검사, Dismiss 경로)는 변경 없음.

## 엣지 케이스

- `nextStop`이 과거이거나 남은 시간 ≤ 0: 곧 해제되므로 특별 줄 없이 현행 안내만
  표시(다음 틱에 `ShouldAutoStop`가 해제).
- 자동종료와 스케줄 동시: 이른 시각이 표기됨(실제 해제도 이른 쪽).
- 비근무 시간 수동 시작: 스케줄 후보 없음(IsWorkTime(start) false) → 자동종료
  후보만, 없으면 현행 안내.

## 테스트

`tests/MouseMover.Tests/StopPolicyTests.cs`에 `NextAutoStop` 테스트 추가:
- 자동종료만: `start + AutoOffMinutes` 반환.
- 스케줄만(근무 중 시작): 그날 `WorkEndMinutes` 시각 반환.
- 둘 다: 이른 시각 반환.
- 아무것도 없음: `null`.
- 비근무 시작 + 스케줄만: `null`(스케줄 후보 제외).
- 자동종료=0 + 스케줄: 스케줄 시각만.

OverlayForm/OverlayManager 표기는 빌드 + 수동 확인.

## 파일

- Modify: `src/MouseMover/StopPolicy.cs`, `tests/MouseMover.Tests/StopPolicyTests.cs`
- Modify: `src/MouseMover/OverlayForm.cs`, `src/MouseMover/OverlayManager.cs`
- (선택) Modify: `README.md` — 해제 안내에 자동 해제 예정 시각 표기 한 줄.
