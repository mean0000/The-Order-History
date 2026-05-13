using System.Collections.Generic;
using UnityEngine;
using TheOther.Core;

namespace TheOther.Globe
{
    /// <summary>
    /// 국가 선택 시 CountryHighlight 셰이더와 연동해 시각 피드백을 제공한다.
    ///
    /// 담당 기능:
    ///   - GameManager.OnCountrySelected 이벤트 구독
    ///   - 선택 시: _HighlightMask 설정 + _FillOpacity 활성화
    ///   - 해제 시: _FillOpacity = 0 (투명화)
    ///   - 호버 시: _HoverGlow 설정 (선택 사항)
    ///
    /// 씬 구성 전제:
    ///   - highlightRenderer: CountryHighlight 셰이더 머티리얼이 적용된 MeshRenderer
    ///     (지구 Sphere보다 아주 약간 큰 구체에 적용, Collider 없음)
    ///   - countryMasks: 국가 코드 → HighlightMask 텍스처 매핑 (Inspector에서 할당)
    ///
    /// HighlightMask 텍스처 규약:
    ///   R = 국가 내부 채우기 영역 (0=외부, 1=내부)
    ///   G = 경계 근접도 (0=중심, 1=경계선)
    ///   Equirectangular 투영 사용 (지구 텍스처와 동일한 UV 좌표계)
    ///
    /// MVP 참고:
    ///   개별 국가 마스크 텍스처가 없는 경우 기본값(black)으로 폴백되며,
    ///   시각 효과는 표시되지 않지만 코드 흐름은 정상 동작한다.
    /// </summary>
    public class GlobeHighlight : MonoBehaviour
    {
        // ── Inspector 노출 필드 ───────────────────────────────────────────

        [Header("셰이더 렌더러")]
        [SerializeField] private Renderer highlightRenderer;   // CountryHighlight 머티리얼이 적용된 구체

        [Header("국가 마스크 텍스처 목록")]
        [SerializeField] private List<CountryMaskEntry> countryMasks = new List<CountryMaskEntry>();

        [Header("하이라이트 수치")]
        [SerializeField, Range(0f, 1f)] private float fillOpacity       = 0.3f;   // 채우기 불투명도
        [SerializeField, Range(0f, 4f)] private float borderGlowIntensity = 2.0f; // 경계 빛 강도

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private Material _highlightMat;                        // 인스턴스 머티리얼 (런타임 수정용)
        private string   _currentCountryCode = string.Empty;  // 현재 하이라이트된 국가 코드

        // 마스크 딕셔너리 (Awake에서 List → Dictionary 변환, 런타임 O(1) 조회)
        private Dictionary<string, Texture2D> _maskDict;

        // 셰이더 프로퍼티 ID 캐시 (string 해시 계산을 1회만 수행)
        private static readonly int ID_HighlightMask       = Shader.PropertyToID("_HighlightMask");
        private static readonly int ID_FillOpacity         = Shader.PropertyToID("_FillOpacity");
        private static readonly int ID_BorderGlowIntensity = Shader.PropertyToID("_BorderGlowIntensity");
        private static readonly int ID_PulseIntensity      = Shader.PropertyToID("_PulseIntensity");
        private static readonly int ID_HoverGlow           = Shader.PropertyToID("_HoverGlow");

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            BuildMaskDictionary();
            CacheHighlightMaterial();
        }

        private void OnEnable()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("GlobeHighlight: GameManager 인스턴스를 찾을 수 없습니다. 이벤트 구독을 건너뜁니다.");
                return;
            }

            GameManager.Instance.OnCountrySelected += HandleCountrySelected;
        }

        private void OnDisable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnCountrySelected -= HandleCountrySelected;
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────────

        private void HandleCountrySelected(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                ClearHighlight();
            else
                HighlightCountry(countryCode);
        }

        // ── 하이라이트 제어 ───────────────────────────────────────────────

        /// <summary>
        /// 지정한 국가를 하이라이트한다.
        /// 해당 국가의 HighlightMask 텍스처를 셰이더에 전달하고 FillOpacity를 활성화한다.
        /// 마스크 텍스처가 없으면 기본값(black)을 유지하며 빛 효과는 보이지 않는다.
        /// </summary>
        public void HighlightCountry(string countryCode)
        {
            if (_highlightMat == null) return;

            _currentCountryCode = countryCode;

            // 국가 마스크 텍스처 설정 (없으면 null → 셰이더 기본값 유지)
            if (_maskDict != null && _maskDict.TryGetValue(countryCode.ToUpperInvariant(), out Texture2D mask))
                _highlightMat.SetTexture(ID_HighlightMask, mask);

            // 하이라이트 활성화
            _highlightMat.SetFloat(ID_FillOpacity,         fillOpacity);
            _highlightMat.SetFloat(ID_BorderGlowIntensity, borderGlowIntensity);
            _highlightMat.SetFloat(ID_PulseIntensity,      0f);   // DOTween 연동 전 기본값
            _highlightMat.SetFloat(ID_HoverGlow,           0f);

            Debug.Log($"GlobeHighlight: 하이라이트 활성 → '{_currentCountryCode}'");
        }

        /// <summary>현재 하이라이트를 해제한다.</summary>
        public void ClearHighlight()
        {
            if (_highlightMat == null || string.IsNullOrEmpty(_currentCountryCode)) return;

            Debug.Log($"GlobeHighlight: 하이라이트 해제 (이전: '{_currentCountryCode}')");
            _currentCountryCode = string.Empty;

            _highlightMat.SetFloat(ID_FillOpacity,    0f);
            _highlightMat.SetFloat(ID_PulseIntensity, 0f);
            _highlightMat.SetFloat(ID_HoverGlow,      0f);
        }

        // ── 공개 상태 조회 ────────────────────────────────────────────────

        /// <summary>현재 하이라이트된 국가 코드. 없으면 빈 문자열.</summary>
        public string CurrentCountryCode => _currentCountryCode;

        /// <summary>하이라이트가 활성 상태인지 반환.</summary>
        public bool IsHighlightActive => !string.IsNullOrEmpty(_currentCountryCode);

        // ── 초기화 헬퍼 ──────────────────────────────────────────────────

        private void BuildMaskDictionary()
        {
            _maskDict = new Dictionary<string, Texture2D>(countryMasks.Count);

            foreach (CountryMaskEntry entry in countryMasks)
            {
                if (entry == null || string.IsNullOrEmpty(entry.countryCode) || entry.maskTexture == null)
                    continue;

                string key = entry.countryCode.ToUpperInvariant();
                if (!_maskDict.ContainsKey(key))
                    _maskDict[key] = entry.maskTexture;
                else
                    Debug.LogWarning($"GlobeHighlight: 중복 국가 마스크 항목 무시 — '{key}'");
            }
        }

        private void CacheHighlightMaterial()
        {
            if (highlightRenderer == null)
            {
                Debug.LogWarning("GlobeHighlight: highlightRenderer가 Inspector에 연결되지 않았습니다. 하이라이트 비활성화됩니다.");
                return;
            }

            // material(인스턴스 복사본)을 사용해 다른 오브젝트에 영향 없이 수정
            _highlightMat = highlightRenderer.material;
        }

        // ── 내부 데이터 구조 ─────────────────────────────────────────────

        /// <summary>Inspector에서 국가 코드 ↔ 마스크 텍스처를 매핑하는 직렬화 항목.</summary>
        [System.Serializable]
        public class CountryMaskEntry
        {
            public string    countryCode;   // ISO 3166-1 alpha-2 (예: "KR", "US")
            public Texture2D maskTexture;   // R=fill, G=border 마스크 텍스처
        }
    }
}
