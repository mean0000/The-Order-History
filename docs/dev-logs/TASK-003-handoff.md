# TASK-003 핸드오프 가이드 — 지구본 셰이더 & 비주얼

**작성일:** 2026-05-13
**담당 에이전트:** [art] shader-vfx-artist
**대상:** [play] gameplay 에이전트 — C# 연동 가이드

---

## 생성된 셰이더 파일

| 파일 | 경로 | 용도 |
|------|------|------|
| `EarthSurface.shader` | `Assets/Shaders/EarthSurface.shader` | 지구 표면 (낮/밤 혼합, 대기권 림) |
| `CountryHighlight.shader` | `Assets/Shaders/CountryHighlight.shader` | 나라 하이라이트 오버레이 |
| `Atmosphere.shader` | `Assets/Shaders/Atmosphere.shader` | 대기권 외곽 레이어 |

---

## 1. EarthSurface.shader — 노출 파라미터

| 파라미터 이름 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `_DayTex` | Texture2D | white | 낮 면 알베도 텍스처 |
| `_NightTex` | Texture2D | black | 밤 면 도시 불빛 텍스처 |
| `_SurfaceMaskTex` | Texture2D | black | 팩 마스크 (R=해양) |
| `_SunDirection` | Vector3 | (1, 0.5, 0) | 월드 공간 태양 방향 (정규화) |
| `_DayNightSoftness` | Float | 0.12 | 터미네이터 경계 부드러움 (0.02~0.5) |
| `_OceanColor` | Color | (0.08, 0.25, 0.55) | 해양 색상 틴트 |
| `_LandColor` | Color | (0.55, 0.45, 0.28) | 육지 색상 틴트 |
| `_OceanSpecColor` | Color | (0.8, 0.9, 1.0) | 해양 반사광 색상 |
| `_OceanSpecPower` | Float | 128 | 해양 반사 선명도 (16~512) |
| `_NightEmissionIntensity` | Float | 1.0 | 야간 도시 불빛 강도 |
| `_AtmosphereColor` | Color | (0.35, 0.75, 1.0) | 지표 림 대기권 색상 |
| `_AtmospherePower` | Float | 3.5 | 프레넬 지수 (좁을수록 얇아짐) |
| `_AtmosphereIntensity` | Float | 0.9 | 지표 림 강도 |
| `_CelSteps` | Float | 2 | 육지 셀 셰이딩 밴드 수 |
| `_CelSoftness` | Float | 0.05 | 셀 경계 부드러움 |

### C# 연동 예시 — 태양 방향 업데이트 (TASK-004 타임라인 연동)

```csharp
// EarthSurfaceController.cs (예시)
using UnityEngine;

public class EarthSurfaceController : MonoBehaviour
{
    [SerializeField] private Material earthMaterial;  // EarthSurface 머티리얼 참조

    // 타임라인 시스템(TASK-004)에서 호출: 연도 변경 시 태양 방향 전달
    public void SetSunDirection(Vector3 worldSpaceSunDir)
    {
        earthMaterial.SetVector("_SunDirection", worldSpaceSunDir.normalized);
    }
}
```

**머티리얼 생성 순서:**
1. Project 창 → `Assets/Shaders/` → `EarthSurface.shader` 우클릭 → Create > Material
2. 생성된 머티리얼을 Earth Sphere의 MeshRenderer에 할당
3. 텍스처가 없는 경우: 파라미터 기본값으로 색상 확인 가능 (흰색/검정 텍스처 기본값)

---

## 2. CountryHighlight.shader — 노출 파라미터

| 파라미터 이름 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `_HighlightMask` | Texture2D | black | R=나라 채움 마스크, G=경계 근접도 |
| `_HighlightColor` | Color (HDR) | (0.2, 0.7, 1.0, 0.35) | 채움 색상 (A=불투명도) |
| `_BorderColor` | Color (HDR) | (0.4, 0.9, 1.0, 1.0) | 경계 글로우 색상 |
| `_FillOpacity` | Float | 0.3 | 채움 투명도 마스터 |
| `_BorderGlowWidth` | Float | 0.4 | 글로우 페더 폭 |
| `_BorderGlowIntensity` | Float | 2.0 | 경계 글로우 밝기 |
| `_PulseIntensity` | Float | 1.0 | **펄스 강도 [0~1] — DOTween으로 제어** |
| `_PulseBase` | Float | 0.3 | PulseIntensity=0일 때 최소 밝기 |
| `_HoverGlow` | Float | 0.0 | 호버 상태 글로우 (0=오프, 1=호버) |
| `_HoverGlowColor` | Color (HDR) | (0.6, 0.85, 1.0) | 호버 글로우 색상 |

### C# 연동 예시 — DOTween 펄스 + 나라 선택

```csharp
// CountryHighlightController.cs (예시)
using UnityEngine;
using DG.Tweening;

public class CountryHighlightController : MonoBehaviour
{
    [SerializeField] private Renderer highlightRenderer;
    private Material _mat;
    private Tween _pulseTween;

    private static readonly int PulseIntensityID = Shader.PropertyToID("_PulseIntensity");
    private static readonly int HighlightColorID  = Shader.PropertyToID("_HighlightColor");
    private static readonly int HoverGlowID       = Shader.PropertyToID("_HoverGlow");

    void Awake()
    {
        // MaterialPropertyBlock 대신 인스턴스 머티리얼 사용 (나라별 색상 필요)
        _mat = highlightRenderer.material;
    }

    // 나라 선택 시 [play] 에이전트의 OnCountrySelected 이벤트에서 호출
    public void OnCountrySelected(Color countryColor, Texture2D maskTexture)
    {
        _mat.SetTexture("_HighlightMask", maskTexture);
        _mat.SetColor(HighlightColorID, countryColor);

        // 기존 트윈 Kill 후 새 펄스 시작
        _pulseTween?.Kill();
        _mat.SetFloat(PulseIntensityID, 0f);
        _pulseTween = DOTween.To(
            () => _mat.GetFloat(PulseIntensityID),
            v  => _mat.SetFloat(PulseIntensityID, v),
            1f, 0.3f
        ).SetEase(Ease.OutCubic)
         .SetLoops(-1, LoopType.Yoyo);
    }

    // 호버 상태 On/Off
    public void SetHover(bool isHover)
    {
        _mat.SetFloat(HoverGlowID, isHover ? 1f : 0f);
    }

    // 선택 해제 시 호출
    public void Deselect()
    {
        _pulseTween?.Kill();
        _mat.SetFloat(PulseIntensityID, 0f);
        _mat.SetFloat(HoverGlowID, 0f);
    }

    void OnDestroy()
    {
        _pulseTween?.Kill();
        if (_mat) Destroy(_mat);  // 인스턴스 머티리얼 메모리 해제
    }
}
```

**머티리얼 오브젝트 설정:**
- 하이라이트용 Sphere를 Earth Sphere의 자식으로 배치 (동일한 위치/스케일)
- 해당 Sphere의 MeshCollider는 비활성화 (클릭 이벤트는 Earth Sphere가 담당)
- Renderer → Cast Shadows = Off, Receive Shadows = Off

---

## 3. Atmosphere.shader — 노출 파라미터

| 파라미터 이름 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `_AtmosphereColor` | Color (HDR) | (0.35, 0.72, 1.0) | 림 내측 색상 |
| `_AtmosphereColorOuter` | Color (HDR) | (0.55, 0.88, 1.0) | 림 최외곽 색상 |
| `_FresnelPower` | Float | 4.0 | 프레넬 지수 (높을수록 더 얇은 림) |
| `_Intensity` | Float | 0.85 | 전체 불투명도/밝기 |
| `_SunDirection` | Vector3 | (1, 0.5, 0) | 월드 공간 태양 방향 (EarthSurface와 동기화) |
| `_DaySideBoost` | Float | 0.35 | 낮쪽 림 밝기 추가량 |
| `_NightSideAttenuation` | Float | 0.6 | 밤쪽 대기권 감쇠 (1=완전히 어둡게) |

### C# 연동 예시 — EarthSurface와 태양 방향 동기화

```csharp
// EarthSurfaceController.cs — SetSunDirection 확장
public void SetSunDirection(Vector3 worldSpaceSunDir)
{
    Vector3 dir = worldSpaceSunDir.normalized;
    earthMaterial.SetVector("_SunDirection", dir);
    atmosphereMaterial.SetVector("_SunDirection", dir);  // 동기화 필수
}
```

**Atmosphere Sphere 설정:**
- Earth Sphere 스케일: 1.0 (예: Vector3.one)
- Atmosphere Sphere 스케일: 1.025 (약 2.5% 크게)
- Renderer → Cast Shadows = Off, Receive Shadows = Off
- Layer: Earth와 동일 레이어로 설정 (클릭 레이캐스트가 통과해야 함)

---

## 4. 전체 씬 계층 구조 제안

```
EarthRoot (empty GameObject)
  ├── EarthSphere          — Sphere Mesh + EarthSurface Material + Collider
  ├── AtmosphereSphere     — Sphere Mesh (scale 1.025×) + Atmosphere Material
  └── HighlightSphere      — Sphere Mesh (scale 1.002×) + CountryHighlight Material
```

---

## 5. 태양 방향 계산 (TASK-004 타임라인 연동 참고)

TASK-004에서 연도가 바뀔 때 태양 방향을 업데이트해야 합니다.

```csharp
// 예시: 연도를 0~1로 정규화하여 태양이 지구를 한 바퀴 도는 느낌
Vector3 ComputeSunDirection(float normalizedYear)
{
    // 연도가 흐를수록 태양이 황도 경사각(23.5도)으로 공전
    float angle = normalizedYear * Mathf.PI * 2f;
    float tilt  = 23.5f * Mathf.Deg2Rad;
    return new Vector3(
        Mathf.Cos(angle),
        Mathf.Sin(tilt),
        Mathf.Sin(angle)
    ).normalized;
}
```

---

## 6. 텍스처가 없을 때 기본 시각

현재 세 셰이더 모두 텍스처 없이도 동작합니다:
- `EarthSurface`: 흰색 텍스처 기반으로 `_OceanColor` / `_LandColor` 틴트 적용됨
- `CountryHighlight`: 마스크 없으면 아무것도 표시 안 됨 (정상 동작)
- `Atmosphere`: 텍스처 불필요, 프레넬 계산만으로 동작

---

## 재수정 이력
- 2026-05-13: [stab] 반려 1/3 → Atmosphere 출력 공식, EarthSurface CustomEditor, PulseIntensity 기본값, CBUFFER 정리 수정 완료

---

## 완료 기준 체크리스트 (TASK-003)

- [x] 낮/밤 경계가 `_SunDirection`에 따라 부드럽게 표현됨
- [x] 대기권 림 라이트가 우주에서 보이는 지구 느낌
- [x] 나라 하이라이트가 `_HighlightMask` 기반으로 작동
- [x] `_PulseIntensity` 파라미터가 외부(C#/DOTween)에서 제어 가능하도록 노출
- [x] URP에서 정상 작동 (Built-in ShaderLab 코드 없음)
- [x] 60fps 유지 가능한 수준 — 셰이더당 텍스처 샘플 최대 3개, ALU 우선 설계
