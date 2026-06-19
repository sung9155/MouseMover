# MouseMover 시간 옵션 — 설계 문서

작성일: 2026-06-19

## 목적

기존 MouseMover(절전방지 + 화면 가리기 트레이 앱)에 시간 기반 자동 종료를
추가한다. (1) 덮개 시작 후 일정 시간 뒤 자동 종료, (2) 요일별 근무시간이
끝나면 자동 종료.

## 요구사항

- **자동 종료 타이머:** 덮개 시작 후 설정된 시간이 지나면 자동으로 해제.
  프리셋(없음 / 30분 / 1시간 / 2시간 / 4시간) 중 선택.
- **요일 스케줄:** 공통 근무 시간대(시작~종료 시각) + 적용 요일 선택.
  근무 시간에 시작한 덮개는 근무 시간이 끝나면 자동 종료.
- **스케줄 동작 = 자동 종료만.** 비근무 시간대의 *수동* 시작은 막지 않는다.
- 설정은 기존 설정 창(트레이 우클릭 → 설정...)에 통합.

## 핵심 동작: "전환에만 발동"

스케줄 자동종료는 매 순간 비근무 여부로 끄는 게 아니라, **"근무 중 시작 →
근무 시간 종료" 전환**에만 발동한다.

- 근무 시간에 켠 덮개 → 근무 시간이 끝나면 자동 종료.
- 비근무 시간에 *수동으로* 켠 덮개 → 스케줄은 끄지 않는다(자동종료 타이머만
  적용).

이로써 "비근무 시간에도 수동 시작은 언제든 가능"과 "근무 끝나면 자동 종료"를
동시에 만족한다. (매 초 비근무면 끄는 방식은 수동 시작을 1초 만에 차단하므로
배제.)

## 데이터 (Settings 추가 필드)

| 필드 | 타입 | 기본값 | 의미 |
|---|---|---|---|
| `AutoOffMinutes` | int | 0 | 0=없음. 프리셋 30/60/120/240. 덮개 시작 후 경과 분 도달 시 종료 |
| `ScheduleEnabled` | bool | false | 요일 스케줄 사용 여부 |
| `WorkStartMinutes` | int | 540 | 근무 시작(자정 기준 분, 09:00) |
| `WorkEndMinutes` | int | 1080 | 근무 종료(자정 기준 분, 18:00) |
| `WorkDays` | bool[7] | 월~금 true | 인덱스 = `(int)DayOfWeek` (Sun=0 … Sat=6) |

`WorkDays` 기본값: `[false, true, true, true, true, true, false]` (일 false, 월~금 true, 토 false).

JSON 직렬화는 기존 `System.Text.Json` 그대로. 누락 필드는 기본값(기존 동작).

## 새 유닛: StopPolicy (순수 함수)

`src/MouseMover/StopPolicy.cs` — 부작용 없는 정적 클래스. 단위 테스트의 핵심.

```csharp
public static bool ShouldAutoStop(Settings s, DateTime startLocal, DateTime nowLocal)
```
- 자동종료: `s.AutoOffMinutes > 0 && (nowLocal - startLocal).TotalMinutes >= s.AutoOffMinutes` → true.
- 스케줄: `s.ScheduleEnabled && IsValidWindow(s) && IsWorkTime(s, startLocal) && !IsWorkTime(s, nowLocal)` → true.
- 그 외 false.

```csharp
public static bool IsWorkTime(Settings s, DateTime t)
```
- `s.WorkDays[(int)t.DayOfWeek]` 가 true AND `s.WorkStartMinutes <= (t.Hour*60+t.Minute) < s.WorkEndMinutes`.
- 단 `IsValidWindow(s)`가 false면 항상 false.

```csharp
private static bool IsValidWindow(Settings s) => s.WorkStartMinutes < s.WorkEndMinutes;
```

## 연동

- **`OverlayManager`**
  - `Start(Settings settings)`에서 설정을 필드에 저장하고 시작 시각의 **로컬**
    시간(`_startLocal`)을 기록.
  - 기존 1초 경과 타이머 콜백(`RefreshElapsed`)에서, 라벨 갱신 후
    `StopPolicy.ShouldAutoStop(_settings, _startLocal, <현재 로컬시각>)`을 검사.
    참이면 `DismissAll()` 호출(수동 해제와 동일 경로 → KeepAwake 정지 + 폼 닫기
    + 트레이 메뉴 복원).
  - 시작 시각의 로컬 시간은 테스트 불가 영역이므로 `StopPolicy` 자체를
    테스트하고, OverlayManager는 빌드+수동 확인.

- **`SettingsForm`** (컨트롤 추가)
  - 자동종료: `ComboBox`(항목 없음/30분/1시간/2시간/4시간 ↔ 0/30/60/120/240분).
  - 스케줄 사용: `CheckBox`.
  - 근무 시작/종료: `DateTimePicker`(Format=Time, ShowUpDown) 2개. 분 단위.
  - 요일: 체크박스 7개(일·월·화·수·목·금·토), `WorkDays` 인덱스에 매핑.
  - OK 시 검증: 종료 ≤ 시작이면 저장 막고 안내(또는 스케줄 자동 비활성). 폼에서
    종료>시작 보장.
  - `Commit()`이 새 필드를 `Result`에 채움.

- **`TrayAppContext`**: 변경 없음(설정은 이미 `_overlay.Start(_settings)`로 전달).

## 엣지 케이스

- 유효창 아님(종료 ≤ 시작): `StopPolicy`가 스케줄 자동종료를 발동하지 않음(안전).
  폼은 종료>시작을 강제.
- 자정 넘는 야간 근무(예 22:00~06:00): v1 미지원(시작<종료만 지원). 문서화.
- 자동종료와 스케줄 동시 설정: 둘 중 먼저 충족되는 조건이 종료를 발동.

## 테스트

`tests/MouseMover.Tests/StopPolicyTests.cs` — `StopPolicy` 집중:
- 자동종료: 경과 < 분 → false; 경과 ≥ 분 → true; AutoOffMinutes=0 → 무관.
- 스케줄: 근무 중 시작 + 현재 비근무 → true; 비근무 시작 → false(안 꺼짐);
  현재도 근무 중 → false; 해당 요일 off → (근무 아님) 전환 판정; 유효창 아님 →
  false(안전); 경계(분 == WorkEndMinutes는 비근무).
- `IsWorkTime` 직접: 요일/시간 조합.

SettingsForm·OverlayManager 연동은 빌드 + 수동 확인.

## 파일

- Create: `src/MouseMover/StopPolicy.cs`, `tests/MouseMover.Tests/StopPolicyTests.cs`
- Modify: `src/MouseMover/Settings.cs`, `src/MouseMover/SettingsForm.cs`,
  `src/MouseMover/OverlayManager.cs`
