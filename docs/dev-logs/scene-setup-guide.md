# MainScene 씬 조립 가이드

> **작성일:** 2026-05-13  
> **대상:** Unity Editor에서 직접 수행하는 작업  
> **전제:** TASK-001~004 스크립트 및 셰이더 구현 완료 상태

---

## 전체 순서 요약

```
1. 머티리얼 생성
2. Globe 오브젝트 (지구 본체)
3. Atmosphere / Highlight 구체
4. 카메라 설정
5. Directional Light (태양)
6. Timeline 오브젝트
7. UI Canvas (연도 표시)
8. StreamingAssets 확인
9. Play 전 최종 체크
```

---

## 1단계 — 머티리얼 생성

`Assets/Materials/` 폴더를 만들고 머티리얼 3개를 생성한다.

| 머티리얼 이름 | 셰이더 |
|-------------|--------|
| `M_EarthSurface` | `TheOther/EarthSurface` |
| `M_Atmosphere` | `TheOther/Atmosphere` |
| `M_CountryHighlight` | `TheOther/CountryHighlight` |

**생성 방법:** Project 창 우클릭 → Create → Material → 이름 입력 → Inspector에서 셰이더 변경

---

## 2단계 — Globe 오브젝트 (지구 본체)

### 2-1. 오브젝트 생성
- Hierarchy → 우클릭 → 3D Object → **Sphere**
- 이름: `"Globe"`
- Transform:
  - Position `(0, 0, 0)`
  - Rotation `(0, 0, 0)`
  - Scale `(1, 1, 1)`

### 2-2. Collider 교체 (중요!)
> SphereCollider는 클릭 UV가 항상 (0,0)을 반환해 나라 클릭이 동작하지 않는다.

1. Inspector에서 **Sphere Collider 제거** (Remove Component)
2. **Mesh Collider 추가** (Add Component → Mesh Collider)
3. Mesh Collider의 Mesh 필드에 Unity 기본 **`Sphere`** 메시 드래그
4. Assets 창에서 Sphere 메시 선택 → Inspector → **Read/Write Enabled** 체크 ✅

### 2-3. 머티리얼 적용
- MeshRenderer → Materials → Element 0 → **M_EarthSurface**

### 2-4. 컴포넌트 추가
**GlobeController** 추가 (Add Component → `TheOther.Globe.GlobeController`):
- Globe Transform → `Globe` (자기 자신)
- Main Camera → `Main Camera`
- 수치 기본값 유지 (Rotation Speed 0.3, Inertia Damping 3, Zoom Speed 2, Zoom Min 3, Zoom Max 12)

**GlobeHighlight** 추가 (Add Component → `TheOther.Globe.GlobeHighlight`):
- Highlight Renderer → **3단계에서 만들 `Globe_Highlight` 오브젝트** 연결 (3단계 완료 후)
- Country Masks → MVP에서는 비워 둬도 됨 (마스크 텍스처 없으면 하이라이트 비표시)

---

## 3단계 — Atmosphere & Highlight 구체

### 3-1. Atmosphere 구체 (대기권)
- Hierarchy → 3D Object → Sphere → 이름: `"Globe_Atmosphere"`
- Transform:
  - Position `(0, 0, 0)`
  - Scale `(1.025, 1.025, 1.025)` ← 지구보다 2.5% 크게
- **Collider 제거**
- MeshRenderer:
  - Material → **M_Atmosphere**
  - Cast Shadows → **Off**
  - Receive Shadows → **Off**

### 3-2. Highlight 구체 (국가 하이라이트)
- Hierarchy → 3D Object → Sphere → 이름: `"Globe_Highlight"`
- Transform:
  - Position `(0, 0, 0)`
  - Scale `(1.003, 1.003, 1.003)` ← 지구보다 아주 살짝 크게
- **Collider 제거**
- MeshRenderer:
  - Material → **M_CountryHighlight**
  - Cast Shadows → **Off**
  - Receive Shadows → **Off**
- 이 오브젝트의 MeshRenderer를 **Globe 오브젝트의 GlobeHighlight 컴포넌트** → Highlight Renderer 필드에 연결

---

## 4단계 — 카메라

- Hierarchy에서 **Main Camera** 선택
- Transform:
  - Position `(0, 0, -8)`
  - Rotation `(0, 0, 0)`

---

## 5단계 — Directional Light (태양)

- Hierarchy → Light → **Directional Light** (이미 있으면 그대로 사용)
- 이름: `"Sun"` 으로 변경 권장
- 초기 회전값은 무관 (Play 시 TimelineController가 자동 제어)

---

## 6단계 — Timeline 오브젝트

- Hierarchy → 우클릭 → Create Empty → 이름: `"[Timeline]"`
- **TimelineController** 컴포넌트 추가 (`TheOther.Timeline.TimelineController`):

| 필드 | 연결 대상 |
|------|---------|
| Sun Light | `Sun` (Directional Light) |
| Earth Surface Material | `M_EarthSurface` |
| Atmosphere Material | `M_Atmosphere` |
| Sun Rotation Speed | `2` (기본값) |
| Scroll Threshold | `1.0` (기본값) |
| Scroll Sensitivity | `3` (기본값) |
| Snap Duration | `0.3` (기본값) |

---

## 7단계 — UI Canvas (연도 표시)

### 7-1. Canvas 생성
- Hierarchy → UI → **Canvas**
- Canvas 설정:
  - Render Mode → **Screen Space - Overlay**
  - Canvas Scaler → Scale With Screen Size, Reference Resolution `1920 × 1080`

### 7-2. 오브젝트 구조
Canvas 하위에 아래 구조 생성:

```
Canvas
└── YearDisplay (Empty GameObject) ← YearDisplayUI 컴포넌트 추가
    ├── YearText (TextMeshProUGUI)
    └── AnchorTitleText (TextMeshProUGUI)
```

**YearText 설정:**
- Anchor: Bottom-Center
- Position: `(0, 60, 0)`
- Font Size: `48`
- Alignment: Center
- 초기 텍스트: `"1000 CE"`

**AnchorTitleText 설정:**
- Anchor: Bottom-Center
- Position: `(0, 120, 0)`
- Font Size: `36`
- Alignment: Center
- Color Alpha: `0` (투명 — 코드에서 제어)
- 초기 텍스트: 빈 칸

### 7-3. YearDisplayUI 컴포넌트 연결
`YearDisplay` 오브젝트에 **YearDisplayUI** 컴포넌트 추가:

| 필드 | 연결 대상 |
|------|---------|
| Year Text | `YearText` |
| Anchor Title Text | `AnchorTitleText` |
| Title Display Duration | `3` |
| Title Fade Duration | `0.5` |

---

## 8단계 — StreamingAssets 확인

아래 파일이 존재하는지 확인:

```
Assets/StreamingAssets/anchor_events.json  ✅ (이미 있음)
```

없으면 `docs/history/anchor_events.json`을 복사해 넣는다.

---

## 9단계 — Play 전 최종 체크

| 항목 | 확인 |
|------|------|
| Globe에 MeshCollider (Read/Write Enabled) 적용 | ☐ |
| M_EarthSurface가 Globe MeshRenderer에 할당 | ☐ |
| Globe_Atmosphere — M_Atmosphere, 그림자 Off | ☐ |
| Globe_Highlight — M_CountryHighlight, 그림자 Off | ☐ |
| GlobeHighlight → Highlight Renderer 연결 | ☐ |
| GlobeController → Globe Transform, Main Camera 연결 | ☐ |
| TimelineController → Sun, 두 머티리얼 연결 | ☐ |
| YearDisplayUI → YearText, AnchorTitleText 연결 | ☐ |
| Main Camera Position `(0, 0, -8)` | ☐ |
| Console 에러 없이 Play 가능 | ☐ |

---

## 입력 구조 참고

| 조작 | 기능 |
|------|------|
| 마우스 드래그 | 지구 회전 |
| 스크롤 휠 | 타임라인 이동 (앵커 연도 이동) |
| Ctrl + 스크롤 휠 | 카메라 줌인/아웃 |
| 나라 클릭 | 국가 선택 (Console에 코드 출력) |

---

## 다음 작업

씬 조립 완료 후 **TASK-005 (국가 정보 패널 UI)** 진행 예정.
