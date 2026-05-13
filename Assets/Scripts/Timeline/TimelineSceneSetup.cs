// ─────────────────────────────────────────────────────────────────────────────
//  TimelineSceneSetup.cs
//  네임스페이스: TheOther.Timeline
//
//  이 파일은 런타임 코드가 없습니다.
//  MainScene에서 TASK-004 타임라인 시스템을 구성하는 방법을 안내합니다.
// ─────────────────────────────────────────────────────────────────────────────

/*
 * ════════════════════════════════════════════════════════════════════
 *  1. TimelineController 컴포넌트 추가
 * ════════════════════════════════════════════════════════════════════
 *
 *  위치: Hierarchy → 빈 GameObject 생성 → 이름 "[Timeline]" 권장
 *
 *  추가할 컴포넌트:
 *    TimelineController (TheOther.Timeline)
 *
 *  Inspector 설정:
 *    Sun Light         → 씬의 Directional Light 드래그 연결
 *    Sun Rotation Speed → 2 (기본값, 태양이 느리게 회전)
 *    Scroll Threshold  → 1.0 (스크롤 1 누적 시 앵커 이동)
 *    Scroll Sensitivity→ 3 (스크롤 휠 감도 배율)
 *    Snap Duration     → 0.3 (초 단위, 앵커 전환 속도)
 *
 *
 * ════════════════════════════════════════════════════════════════════
 *  2. 연도 표시 UI Canvas 구성
 * ════════════════════════════════════════════════════════════════════
 *
 *  Hierarchy 구조 예시:
 *
 *  [UI_Timeline] (Canvas)
 *  ├── Render Mode : Screen Space - Overlay
 *  ├── Canvas Scaler : Scale With Screen Size, 1920×1080 권장
 *  │
 *  └── YearDisplay (GameObject) ← YearDisplayUI 컴포넌트 추가
 *      ├── YearText (TextMeshProUGUI)
 *      │     Anchor: Bottom-Center
 *      │     Pos: (0, 60, 0)
 *      │     Font Size: 48
 *      │     Alignment: Center
 *      │     Text (초기값): "1000 CE"
 *      │
 *      └── AnchorTitleText (TextMeshProUGUI)
 *            Anchor: Bottom-Center
 *            Pos: (0, 120, 0)
 *            Font Size: 36
 *            Alignment: Center
 *            Color Alpha: 0 (투명, YearDisplayUI가 코드로 제어)
 *            Text (초기값): "" (비워 둘 것)
 *
 *  YearDisplayUI Inspector 설정:
 *    Year Text          → YearText 오브젝트 연결
 *    Anchor Title Text  → AnchorTitleText 오브젝트 연결
 *    Title Display Duration → 3 (초)
 *    Title Fade Duration    → 0.5 (초)
 *
 *
 * ════════════════════════════════════════════════════════════════════
 *  3. DirectionalLight 연결
 * ════════════════════════════════════════════════════════════════════
 *
 *  - Hierarchy에서 씬의 Directional Light 오브젝트를 선택한다.
 *  - [Timeline] 오브젝트의 TimelineController 컴포넌트 →
 *    "Sun Light" 필드에 Directional Light를 드래그 연결한다.
 *  - TimelineController는 transform.rotation을 코드로 제어하므로
 *    Directional Light의 초기 회전값은 런타임에 덮어쓰인다.
 *    (에디터에서 초기 회전을 맞춰 놓아도 Play 후 자동 동기화됨)
 *
 *
 * ════════════════════════════════════════════════════════════════════
 *  4. 입력 충돌 해결 요약
 * ════════════════════════════════════════════════════════════════════
 *
 *  스크롤 휠 담당 분리:
 *    TimelineController  →  순수 ScrollWheel (Ctrl 없음) → 앵커 이동
 *    GlobeController     →  Ctrl + ScrollWheel           → 카메라 줌
 *
 *  GlobeController.HandleScrollInput() 수정 사항 (GlobeController.cs 참조):
 *    - LeftControl 또는 RightControl이 눌리지 않으면 즉시 return
 *    - 나머지 줌 로직은 동일
 *
 *
 * ════════════════════════════════════════════════════════════════════
 *  5. 앵커 데이터 JSON (StreamingAssets/anchor_events.json)
 * ════════════════════════════════════════════════════════════════════
 *
 *  각 항목에 anchor_title 필드가 있어야 앵커 도달 시 타이틀이 표시된다.
 *  예시:
 *    {
 *      "year": 1066,
 *      "anchor_title": "노르만 정복",
 *      "country_events": [...]
 *    }
 *
 *  anchor_title이 없거나 빈 문자열이면 타이틀 UI는 표시되지 않는다.
 *
 *
 * ════════════════════════════════════════════════════════════════════
 *  6. 씬 실행 순서 체크리스트
 * ════════════════════════════════════════════════════════════════════
 *
 *  [ ] GameManager 싱글톤이 씬에 존재하는가? (또는 DontDestroyOnLoad로 유지 중)
 *  [ ] HistoryDataLoader 싱글톤이 존재하고 anchor_events.json이 StreamingAssets에 있는가?
 *  [ ] TimelineController의 Sun Light 필드가 연결되었는가?
 *  [ ] YearDisplayUI의 yearText 필드가 연결되었는가?
 *  [ ] GlobeController.cs HandleScrollInput()에 Ctrl 조건이 추가되었는가?
 *
 */

// 런타임 코드 없음 — 씬 설정 가이드 전용 파일
