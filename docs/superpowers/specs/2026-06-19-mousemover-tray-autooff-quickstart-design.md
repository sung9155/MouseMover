# MouseMover 트레이 자동종료 빠른 시작 — 설계 문서

작성일: 2026-06-19

## 목적

트레이 아이콘 우클릭 메뉴에 "자동 종료" 하위메뉴를 추가한다. 시간 프리셋을
누르면 그 시간 뒤 자동 종료되도록 설정한 채로 덮개(절전방지 + 화면 가리기)가
즉시 시작된다. 설정 창을 거치지 않고 한 번에 시간 지정 시작.

## 요구사항

- 트레이 우클릭 메뉴에 `자동 종료 ▸` 하위메뉴 추가.
- 하위메뉴 항목: 30분 / 1시간 / 2시간 / 4시간.
- 항목 클릭 → 그 시간으로 자동 종료가 적용된 덮개가 즉시 시작.
- 클릭은 **원샷**: `settings.json`의 저장된 `AutoOffMinutes`는 바뀌지 않는다.
- 기존 `덮개 시작`(타이머 없이 저장 설정으로 시작)과 `설정...`, `종료`는 유지.

## 메뉴 구성

```
설정...
덮개 시작
자동 종료 ▸
   30분
   1시간
   2시간
   4시간
종료
```

## 동작

- "자동 종료 ▸ 1시간" 클릭:
  1. 저장된 `_settings`를 `Clone()`.
  2. 복제본의 `AutoOffMinutes`만 60으로 덮어쓴다(다른 필드 유지 — 스케줄, 라벨
     설정 등 그대로 적용).
  3. 복제본으로 덮개 + 절전방지 시작(기존 시작 경로 재사용).
  4. 저장하지 않음 → 다음 `덮개 시작`은 여전히 저장된 설정 사용.
- 자동 종료 자체는 기존 `StopPolicy`/`OverlayManager` 1초 틱이 처리(복제본의
  `AutoOffMinutes`를 보고 경과 시 자동 해제).

## 컴포넌트 (TrayAppContext.cs 만 수정)

- `StartCover()` → `StartCover(Settings effective)`로 리팩터. 기존 "덮개 시작"
  핸들러는 `StartCover(_settings)` 호출. 내부 로직(IsActive 가드,
  `_keepAwake.JiggleSeconds = effective.JiggleSeconds`, `_keepAwake.Start()`,
  `_overlay.Start(effective)`, 메뉴 비활성)은 동일하되 `_settings` 대신
  `effective` 사용.
- `StartCoverWith(int minutes)`: `var s = _settings.Clone(); s.AutoOffMinutes =
  minutes; StartCover(s);`
- `자동 종료` `ToolStripMenuItem`(`_autoOffMenu`)에 4개 프리셋을 `DropDownItems`로
  추가. 각 항목 click → `StartCoverWith(해당 분)`.
- 활성/비활성 토글: `StartCover`에서 `_startItem`과 `_autoOffMenu` 둘 다
  `Enabled = false`; `OnDismissed`에서 둘 다 `Enabled = true`.

프리셋 정의(트레이용, 분): `(30분,30) (1시간,60) (2시간,120) (4시간,240)`. "없음"은
하위메뉴에 넣지 않음(타이머 없는 시작은 `덮개 시작`이 담당).

## 엣지 케이스

- 덮개 활성 중 프리셋 클릭: `StartCover`의 `if (_overlay.IsActive) return;`로 무시
  (메뉴도 비활성 상태). 안전.
- 원샷 복제는 `Settings.Clone()`(MemberwiseClone) 사용 — `WorkDays` 배열은 얕은
  복사로 공유되지만 변경하지 않으므로 무방. `AutoOffMinutes`는 값 타입이라 복제본만
  바뀜.

## 테스트

- 자동 종료 판정 로직은 이미 `StopPolicy` 단위 테스트로 검증됨(28개 유지).
- 이 작업은 트레이 메뉴 배선 + 기존 시작 경로/`Clone` 재사용 → 새 단위 테스트
  없음. 빌드 + 스모크 + 수동 확인.

## 파일

- Modify: `src/MouseMover/TrayAppContext.cs`
- (선택) Modify: `README.md` — 트레이 자동종료 빠른 시작 한 줄.
