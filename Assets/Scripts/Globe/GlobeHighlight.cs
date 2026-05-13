using UnityEngine;
using TheOther.Core;

namespace TheOther.Globe
{
    /// <summary>
    /// 국가 선택 시 시각 피드백을 담당하는 컴포넌트.
    ///
    /// 현재 단계(TASK-002):
    ///   - GameManager.OnCountrySelected 이벤트 구독
    ///   - 선택/해제 시 Debug.Log 출력
    ///
    /// 다음 단계(TASK-003):
    ///   - 셰이더 프로퍼티 연동으로 교체 예정
    ///   - HighlightCountry / ClearHighlight 메서드 인터페이스는 유지
    /// </summary>
    public class GlobeHighlight : MonoBehaviour
    {
        // ── 상태 ──────────────────────────────────────────────────────────

        /// <summary>현재 하이라이트된 국가 코드. 없으면 빈 문자열.</summary>
        private string _currentCountryCode = string.Empty;

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void OnEnable()
        {
            // GameManager 인스턴스가 없으면 이벤트 구독을 건너뜀
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("GlobeHighlight: GameManager 인스턴스를 찾을 수 없습니다. 이벤트 구독을 건너뜁니다.");
                return;
            }

            GameManager.Instance.OnCountrySelected += HandleCountrySelected;
        }

        private void OnDisable()
        {
            // GameManager가 이미 파괴된 경우(씬 언로드 등)를 방어
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnCountrySelected -= HandleCountrySelected;
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────────

        /// <summary>
        /// GameManager.OnCountrySelected 이벤트 수신 핸들러.
        /// countryCode가 빈 문자열이면 선택 해제.
        /// </summary>
        private void HandleCountrySelected(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
            {
                ClearHighlight();
            }
            else
            {
                HighlightCountry(countryCode);
            }
        }

        // ── 하이라이트 제어 (TASK-003 교체 대상) ─────────────────────────

        /// <summary>
        /// 지정한 국가를 하이라이트한다.
        /// TASK-003에서 셰이더 프로퍼티 설정으로 교체된다.
        /// </summary>
        /// <param name="countryCode">하이라이트할 국가 코드</param>
        public void HighlightCountry(string countryCode)
        {
            _currentCountryCode = countryCode;
            Debug.Log($"GlobeHighlight: 국가 선택됨 → '{_currentCountryCode}' (TASK-003에서 셰이더 연동 예정)");

            // TODO(TASK-003): 아래 코드로 교체
            // _globeRenderer.material.SetFloat("_SelectedCountryId", GetCountryId(countryCode));
        }

        /// <summary>
        /// 현재 하이라이트를 해제한다.
        /// TASK-003에서 셰이더 초기화 로직으로 교체된다.
        /// </summary>
        public void ClearHighlight()
        {
            if (string.IsNullOrEmpty(_currentCountryCode)) return;

            Debug.Log($"GlobeHighlight: 국가 선택 해제 (이전: '{_currentCountryCode}')");
            _currentCountryCode = string.Empty;

            // TODO(TASK-003): 아래 코드로 교체
            // _globeRenderer.material.SetFloat("_SelectedCountryId", -1f);
        }

        // ── 공개 상태 조회 ────────────────────────────────────────────────

        /// <summary>현재 하이라이트된 국가 코드를 반환한다. 없으면 빈 문자열.</summary>
        public string CurrentCountryCode => _currentCountryCode;

        /// <summary>현재 하이라이트가 활성 상태인지 반환한다.</summary>
        public bool IsHighlightActive => !string.IsNullOrEmpty(_currentCountryCode);
    }
}
