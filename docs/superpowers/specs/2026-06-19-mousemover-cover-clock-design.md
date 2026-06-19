# MouseMover 표시 개선 (덮개 시계/메시지 + 툴팁 ETA) — 설계 문서

작성일: 2026-06-19

## 목적

두 가지 표시 개선:
1. **덮개 중앙 시계/메시지**: 검은 덮개 중앙에 큰 시계(HH:mm)와 커스텀 메시지를
   표시(각각 개별 토글). 자리비움 안내 효과.
2. **절전방지 전용 모드 툴팁 ETA**: 오버레이가 없는 절전방지 전용 모드에서 트레이
   툴팁에 자동 해제 예정 시각을 표시.

## 요구사항

### 덮개 중앙 시계/메시지
- `ShowClock`이 켜지면 덮개 중앙에 큰 시계(HH:mm), 1초마다 갱신.
- `CenterMessage`가 비어있지 않으면 시계 아래 메시지 표시.
- 각 모니터 덮개마다 중앙 표시. 색은 기존 `LabelColorArgb` 재사용.
- 우하단 라벨(상태/경과시간/해제안내)은 현행 유지.
- 설정창에서 시계 토글 + 메시지 텍스트 지정, `settings.json` 저장.
- 기본값: `ShowClock=false`, `CenterMessage=""` → 켜기 전엔 기존처럼 순수 검은 덮개.

### 툴팁 ETA
- 절전방지 전용 모드 활성 중: 자동 해제 예정이 있으면 트레이 툴팁
  `MouseMover — 절전방지 중 (HH:mm까지)`, 없으면 `MouseMover — 절전방지 중`.
- 기존 KAO 1초 타이머에서 매 틱 갱신. `StopPolicy.NextAutoStop` 재사용.

## 데이터 (Settings 추가)

| 필드 | 타입 | 기본값 | 의미 |
|---|---|---|---|
| `ShowClock` | bool | false | 덮개 중앙 시계 표시 |
| `CenterMessage` | string | "" | 덮개 중앙 메시지(빈값=숨김) |

JSON 직렬화 기존 그대로(누락 필드는 기본값).

## 컴포넌트

### OverlayForm (덮개 중앙 표시)
- 필드: `_clockLabel`(big), `_messageLabel`(medium), 각 폰트 `_clockFont`/`_messageFont`.
- 생성자: `_settings.ShowClock`이면 시계 라벨 생성·추가; `_settings.CenterMessage`가
  비어있지 않으면 메시지 라벨 생성·텍스트 설정·추가. 색 = `Color.FromArgb(LabelColorArgb)`.
- 중앙 배치 헬퍼 `PositionCenter()`: 시계는 화면 세로 ~40% 지점 가로 중앙, 메시지는
  시계 바로 아래. `Shown` 및 텍스트 갱신 후 호출. `ClientSize.IsEmpty`면 무시(기존 가드와 동일).
- `UpdateElapsed`: 시계 라벨이 있으면 `DateTime.Now.ToString("HH:mm")`로 갱신 후
  `PositionCenter()`. 메시지는 정적.
- 기존 동작(우하단 라벨, dismiss KeyDown/MouseDown만, 커서 숨김, `_dismissed` 가드,
  `Dispose`의 Cursor.Show + `_font.Dispose`)은 유지. 새 폰트도 `Dispose`에서 정리(생성된
  경우만).

### OverlayManager
- 변경 없음(폼이 이미 `settings`를 받음).

### SettingsForm
- `덮개 중앙 시계 표시` 체크박스(`_showClock`) + `덮개 중앙 메시지` 텍스트
  필드(`_centerMessage`). 생성자에서 `current`로 초기화, `Commit()`에서 두 필드 채움.
  기존 12필드 할당 유지.

### TrayAppContext (툴팁 ETA)
- `_keepAwakeOnlyTimer` tick 핸들러에서 자동중지 검사 후, 활성이면 툴팁 갱신:
  `var next = StopPolicy.NextAutoStop(_kaoSettings, _kaoStartLocal);`
  `_tray.Text = next is { } t ? $"MouseMover — 절전방지 중 ({t:HH:mm}까지)" : "MouseMover — 절전방지 중";`
- `StartKeepAwakeOnly`의 초기 툴팁도 동일 규칙으로 설정(즉시 반영). `StopKeepAwakeOnly`는
  현행대로 기본 툴팁 복원.

## 엣지 케이스
- 시계/메시지 둘 다 off(기본): 중앙 비어 순수 검은 덮개.
- 메시지만, 시계 off: 메시지만 중앙.
- 긴 메시지: AutoSize 라벨 + 가로 중앙 → 폭 넘으면 양끝 잘릴 수 있음(단일 줄). v1은
  단일 줄, 과도하게 길지 않다고 가정.
- 툴팁 길이: NotifyIcon.Text 한도 내(문구 짧음).

## 테스트
- `SettingsTests`: 새 필드 기본값 + JSON 왕복 추가(단위).
- OverlayForm 중앙 렌더 / SettingsForm 컨트롤 / 툴팁은 빌드 + 수동.
- `StopPolicy.NextAutoStop`는 이미 단위 테스트됨.

## 파일
- Modify: `src/MouseMover/Settings.cs`, `tests/MouseMover.Tests/SettingsTests.cs`
- Modify: `src/MouseMover/OverlayForm.cs`, `src/MouseMover/SettingsForm.cs`,
  `src/MouseMover/TrayAppContext.cs`
- Modify: `README.md`
