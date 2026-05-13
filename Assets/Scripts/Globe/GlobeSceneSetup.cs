// =============================================================================
//  GlobeSceneSetup.cs
//  씬 수동 구성 가이드 — 런타임 코드 없음, 주석 전용 파일
// =============================================================================
//
//  Unity Editor에서 MainScene을 직접 구성하는 방법:
//
//  ── 1. 지구 오브젝트 생성 ─────────────────────────────────────────────────
//    (a) Hierarchy 창 우클릭 → 3D Object → Sphere
//    (b) 오브젝트 이름: "Globe"
//    (c) Transform:
//          Position  (0, 0, 0)
//          Rotation  (0, 0, 0)
//          Scale     (1, 1, 1)   ← 필요 시 조정
//    (d) Sphere Collider 자동 추가됨 → 반드시 MeshCollider로 교체할 것 (필수)
//        ※ SphereCollider는 hit.textureCoord가 항상 (0,0)을 반환해 국가 클릭 불가
//           교체 방법: 아래 8번 항목 참조
//
//  ── 2. 카메라 설정 ────────────────────────────────────────────────────────
//    (a) Main Camera Transform:
//          Position  (0, 0, -8)
//          Rotation  (0, 0, 0)
//    (b) 카메라가 원점(0,0,0)을 바라보도록 설정됨
//    (c) zoomMin(3) ~ zoomMax(12) 범위를 고려해 초기 거리 -8 권장
//
//  ── 3. GlobeController 컴포넌트 추가 ─────────────────────────────────────
//    (a) Hierarchy에서 Globe 오브젝트 선택
//        또는 빈 오브젝트(GlobeManager 등) 생성 후 추가 (권장)
//    (b) Add Component → TheOther.Globe.GlobeController
//    (c) Inspector 연결:
//          Globe Transform → Globe 오브젝트
//          Main Camera     → Main Camera
//    (d) 수치 조정 (기본값 사용 가능):
//          Rotation Speed  : 0.3   (드래그 감도)
//          Inertia Damping : 3     (관성 감속 — 클수록 빠르게 멈춤)
//          Zoom Speed      : 2
//          Zoom Min        : 3     (최대 근접 거리)
//          Zoom Max        : 12    (최대 원거리)
//
//  ── 4. GlobeHighlight 컴포넌트 추가 ──────────────────────────────────────
//    (a) Globe 오브젝트 또는 GlobeController와 같은 오브젝트에 추가
//    (b) Add Component → TheOther.Globe.GlobeHighlight
//    (c) 추가 연결 불필요 — GameManager.OnCountrySelected 이벤트에 자동 구독
//
//  ── 5. GameManager 확인 ───────────────────────────────────────────────────
//    (a) GameManager는 DontDestroyOnLoad 싱글톤으로 자동 생성
//    (b) 씬에 별도 배치 불필요
//    (c) 필요 시 빈 오브젝트에 GameManager 컴포넌트를 수동 배치해도 무방
//
//  ── 6. 지구 텍스처 적용 ──────────────────────────────────────────────────
//    (a) Globe 오브젝트 → MeshRenderer → Material 지정
//    (b) 표준 Equirectangular(직사각형) 세계지도 텍스처 사용
//        텍스처 규약: U=0 서경 180°, U=1 동경 180°, V=0 북극, V=1 남극
//    (c) TASK-003에서 커스텀 Globe 셰이더로 교체 예정
//
//  ── 7. Physics Layer 설정 (선택 사항) ────────────────────────────────────
//    (a) Globe 오브젝트에 "Globe" 레이어 지정
//    (b) Project Settings → Physics → Layer Collision Matrix에서
//        Globe 레이어만 레이캐스트에 포함되도록 설정하면 성능 개선
//    (c) GlobeController의 TrySelectCountryAtCursor()에서
//        Physics.Raycast 호출 시 layerMask 파라미터를 추가해 사용
//
//  ── 8. 클릭 정확도 향상 옵션 ─────────────────────────────────────────────
//    (a) SphereCollider 대신 MeshCollider 사용 시:
//        - Globe 오브젝트의 Sphere Collider 제거
//        - MeshCollider 추가 → Mesh = Sphere 메시 지정
//        - 메시의 Read/Write Enabled 옵션 활성화 필수
//          (Assets → Sphere 메시 선택 → Inspector → Read/Write Enabled 체크)
//    (b) MeshCollider만 hit.textureCoord로 올바른 UV를 반환함
//        SphereCollider는 항상 (0,0)을 반환하므로 반드시 교체해야 함
//
// =============================================================================

// 이 파일은 런타임 코드를 포함하지 않습니다.
// Unity 컴파일 경고를 방지하기 위해 최소한의 네임스페이스 선언만 유지합니다.
namespace TheOther.Globe { }
