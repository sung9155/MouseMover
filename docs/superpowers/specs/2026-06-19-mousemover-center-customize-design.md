# MouseMover 덮개 중앙 표시 커스터마이징 확장 — 설계 문서

작성일: 2026-06-19

## 목적

덮개 중앙 시계/메시지의 커스터마이징 폭을 대폭 확장한다. 시계는 디지털/아날로그
선택, 글자 크기·색·굵게, 초 표시, 12시간제, 날짜 표시까지 지원.

## 요구사항

- 시계 스타일: **디지털**(현행) 또는 **아날로그**(시계판+바늘) 선택.
- 시계 글자 크기(디지털) / 시계 크기(아날로그), 메시지 글자 크기 각각 조절.
- 중앙 전용 글자 색(우하단 코너 라벨 색과 별도).
- 시계·메시지 굵게 토글.
- 시계 초 표시(디지털 HH:mm:ss / 아날로그 초침).
- 12시간제(오전/오후) 토글.
- 시계 아래 날짜 표시 토글.
- 설정창에서 모두 변경, `settings.json` 저장.
- 기본값은 현재 모양과 동일 → 켜도 기존과 같고, 옵션 조절 시 변화.

## 데이터 (Settings 추가, 8필드)

| 필드 | 타입 | 기본값 | 의미 |
|---|---|---|---|
| `AnalogClock` | bool | false | false=디지털, true=아날로그 |
| `ClockFontSize` | int | 64 | 디지털 글자 pt / 아날로그 지름 기준 |
| `MessageFontSize` | int | 24 | 메시지 글자 pt |
| `CenterColorArgb` | int | `unchecked((int)0xFFA0A0A0)` | 중앙 전용 색 |
| `CenterBold` | bool | false | 시계·메시지 굵게 |
| `ClockSeconds` | bool | false | 초 표시 |
| `Clock12Hour` | bool | false | 오전/오후 12시간제 |
| `ShowDate` | bool | false | 시계 아래 날짜 |

JSON 직렬화 기존 그대로. 누락 필드는 기본값(구버전 settings.json 호환).

## 순수 로직 (단위 테스트)

### ClockFormat (`src/MouseMover/ClockFormat.cs`)
```csharp
public static string Text(DateTime t, bool seconds, bool twelveHour)
```
- 24h: `seconds ? "HH:mm:ss" : "HH:mm"`.
- 12h: `seconds ? "tt h:mm:ss" : "tt h:mm"` (ko-KR → 오전/오후).
- `t.ToString(fmt, new CultureInfo("ko-KR"))`.

### ClockGeometry (`src/MouseMover/ClockGeometry.cs`)
12시 방향 0도, 시계방향 증가(도 단위):
```csharp
public static double HourAngle(DateTime t)   => (t.Hour % 12 + t.Minute / 60.0) * 30.0;
public static double MinuteAngle(DateTime t) => (t.Minute + t.Second / 60.0) * 6.0;
public static double SecondAngle(DateTime t) => t.Second * 6.0;
```

## 컴포넌트

### AnalogClock (`src/MouseMover/AnalogClock.cs`) — 신규 컨트롤
- `Control` 상속, `DoubleBuffered = true`.
- 속성: `DateTime Time`(set→Invalidate), `Color HandColor`, `bool ShowSeconds`.
- `OnPaint`: 안티앨리어스, 시계판 원 + 12 눈금 + 시/분침(초 옵션이면 초침). 각도는
  `ClockGeometry` 사용. 바늘 끝점 = 중심 + 길이·(sin θ, −cos θ), θ는 라디안.
- 색 = `HandColor`. 크기 = 컨트롤 Size(지름).

### OverlayForm (통합)
- `ShowClock`일 때:
  - `AnalogClock`이면 `AnalogClock` 컨트롤 생성(지름 ≈ `ClockFontSize * 4`,
    `HandColor = CenterColorArgb`, `ShowSeconds = ClockSeconds`), 매 틱 `Time = DateTime.Now`.
  - 아니면 디지털 라벨(폰트 `ClockFontSize` + 굵게 옵션, 색 `CenterColorArgb`),
    매 틱 `ClockFormat.Text(now, ClockSeconds, Clock12Hour)`.
- `CenterMessage` 비어있지 않으면 메시지 라벨(폰트 `MessageFontSize` + 굵게, 색 `CenterColorArgb`).
- `ShowDate`면 날짜 라벨(시계 아래, ko-KR `yyyy-MM-dd ddd`, 색 `CenterColorArgb`).
- 중앙 배치: 시계(또는 아날로그) → 날짜 → 메시지 세로로 쌓아 중앙 정렬(`PositionCenter` 확장).
- 색 참조를 중앙 요소는 `CenterColorArgb`로 변경(우하단 코너 라벨은 `LabelColorArgb` 유지).
- 기존 동작(코너 라벨, dismiss, 커서, 가드, 폰트 Dispose)·null 안전 유지. 새 폰트/컨트롤도 생성된 경우만 Dispose.

### SettingsForm
- 중앙 표시 컨트롤이 많아지므로 **`덮개 중앙 표시` GroupBox**로 묶는다(내부 중첩
  TableLayoutPanel). 기존 `시계 표시`(ShowClock)·`덮개 메시지`(CenterMessage)를 이 그룹으로
  이동하고 신규 컨트롤 추가:
  - 시계 표시 체크(ShowClock)
  - 시계 스타일: 디지털/아날로그 (라디오 2개 또는 콤보)
  - 시계 크기 NumericUpDown(24~200), 메시지 크기 NumericUpDown(12~120)
  - 중앙 색 버튼(ColorDialog)
  - 굵게 / 초 표시 / 12시간제 / 날짜 표시 체크
  - 덮개 메시지 텍스트
- `Commit()`에 8필드 추가(기존 필드 유지).
- 폼 AutoSize 유지(GroupBox 포함해 잘리지 않게).

## 엣지 케이스
- 기본값 = 현재 모양(디지털 64pt 회색). 구버전 settings.json은 누락 필드 기본값.
- 아날로그에서 굵게/12시간제는 의미 없음(무시) — 초 표시만 초침에 반영.
- 색: 기존엔 중앙도 `LabelColorArgb` 따랐으나 이제 `CenterColorArgb`(기본 동일 회색).
  코너 색을 커스텀했던 사용자는 중앙이 기본 회색이 됨(독립 색). 문서화.
- 긴 날짜/메시지: 단일 줄 가정(v1).

## 테스트
- `SettingsTests`: 8 새 필드 기본값 + JSON 왕복.
- `ClockFormatTests`: 24h/12h × 초 유무 조합.
- `ClockGeometryTests`: 대표 시각의 시/분/초침 각도.
- AnalogClock 그리기 / OverlayForm 통합 / SettingsForm은 빌드+수동.

## 파일
- Modify: `Settings.cs`, `SettingsTests.cs`, `OverlayForm.cs`, `SettingsForm.cs`, `README.md`
- Create: `ClockFormat.cs`, `ClockGeometry.cs`, `AnalogClock.cs`, `ClockFormatTests.cs`, `ClockGeometryTests.cs`
