using System.Collections;
using TMPro;
using UnityEngine;
using TheOther.Core;
using TheOther.Data;

namespace TheOther.Timeline
{
    /// <summary>
    /// 연도 표시 UI 컴포넌트.
    ///
    /// 담당 기능:
    ///   - GameManager.OnYearChanged 구독 → TextMeshPro 텍스트 갱신
    ///   - 연도 포맷: "1066 CE" (향후 BCE 확장 가능)
    ///   - 앵커 연도 도달 시 anchor_title 표시: "1066\n노르만 정복"
    ///   - anchor_title 3초 후 페이드아웃, 연도 텍스트만 남김
    ///
    /// 씬 구성 전제:
    ///   - Canvas(Screen Space - Overlay) 하위에 배치
    ///   - yearText: 항상 표시되는 연도 TextMeshProUGUI
    ///   - anchorTitleText: 앵커 도달 시만 표시, 알파 페이드 처리
    /// </summary>
    public class YearDisplayUI : MonoBehaviour
    {
        // ── Inspector 노출 필드 ───────────────────────────────────────────

        [Header("UI 텍스트 참조")]
        [SerializeField] private TextMeshProUGUI yearText;          // 항상 표시: "1066 CE"
        [SerializeField] private TextMeshProUGUI anchorTitleText;   // 앵커 이벤트 제목 (페이드인/아웃)

        [Header("앵커 타이틀 설정")]
        [SerializeField] private float titleDisplayDuration = 3f;   // 타이틀 표시 유지 시간 (초)
        [SerializeField] private float titleFadeDuration    = 0.5f; // 페이드인/아웃 소요 시간 (초)

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private Coroutine _titleFadeCoroutine = null;   // 실행 중인 페이드 코루틴 (중복 방지)

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            ValidateReferences();

            // anchorTitleText 초기 알파 0 (투명)
            if (anchorTitleText != null)
            {
                SetTextAlpha(anchorTitleText, 0f);
                anchorTitleText.text = string.Empty;
            }
        }

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnYearChanged += HandleYearChanged;

            // 현재 연도로 초기 UI 갱신
            RefreshYearText(GameManager.Instance.SelectedYear);
        }

        private void OnDisable()
        {
            // GameManager가 씬 언로드 중 파괴될 수 있으므로 null 체크
            if (GameManager.Instance != null)
                GameManager.Instance.OnYearChanged -= HandleYearChanged;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────

        private void HandleYearChanged(int previousYear, int newYear)
        {
            RefreshYearText(newYear);
            TryShowAnchorTitle(newYear);
        }

        // ── 연도 텍스트 갱신 ──────────────────────────────────────────────

        /// <summary>
        /// 연도 숫자를 "YYYY CE" 형식으로 표시한다.
        /// 연도가 0 이하이면 "YYYY BCE" 형식으로 표시 (확장성 확보).
        /// </summary>
        private void RefreshYearText(int year)
        {
            if (yearText == null) return;

            yearText.text = FormatYear(year);
        }

        /// <summary>연도 정수를 표시 문자열로 변환.</summary>
        private static string FormatYear(int year)
        {
            if (year > 0)
                return $"{year} CE";
            else if (year < 0)
                return $"{Mathf.Abs(year)} BCE";
            else
                return "1 CE"; // year = 0은 역사적으로 존재하지 않으므로 폴백
        }

        // ── 앵커 타이틀 표시 ──────────────────────────────────────────────

        /// <summary>
        /// 앵커 연도에 해당하는 이벤트 제목을 페이드인으로 표시한 뒤
        /// titleDisplayDuration초 후 페이드아웃한다.
        /// HistoryDataLoader가 로드 완료되지 않았으면 타이틀을 표시하지 않는다.
        /// </summary>
        private void TryShowAnchorTitle(int year)
        {
            if (anchorTitleText == null) return;

            HistoryDataLoader loader = HistoryDataLoader.Instance;
            if (!loader.IsLoaded) return;

            AnchorEventData anchor = loader.GetAnchorByYear(year);
            if (anchor == null) return;  // 앵커 연도가 아닌 경우

            // anchor_title이 없으면 표시 스킵
            string title = anchor.anchor_title;
            if (string.IsNullOrEmpty(title)) return;

            // "1066\n노르만 정복" 형식으로 구성
            string displayText = $"{year}\n{title}";

            // 진행 중인 페이드 코루틴이 있으면 중단 후 새로 시작
            if (_titleFadeCoroutine != null)
            {
                StopCoroutine(_titleFadeCoroutine);
                _titleFadeCoroutine = null;
            }

            anchorTitleText.text = displayText;
            _titleFadeCoroutine = StartCoroutine(ShowAndFadeTitle());
        }

        // ── 페이드 코루틴 ─────────────────────────────────────────────────

        /// <summary>
        /// 타이틀 텍스트를 페이드인 → 유지 → 페이드아웃하는 시퀀스.
        /// </summary>
        private IEnumerator ShowAndFadeTitle()
        {
            // 1. 페이드인
            yield return StartCoroutine(FadeText(anchorTitleText, 0f, 1f, titleFadeDuration));

            // 2. 유지
            yield return new WaitForSeconds(titleDisplayDuration);

            // 3. 페이드아웃
            yield return StartCoroutine(FadeText(anchorTitleText, 1f, 0f, titleFadeDuration));

            // 페이드 완료 후 텍스트 비우기 (렌더링 비용 절감)
            anchorTitleText.text = string.Empty;
            _titleFadeCoroutine = null;
        }

        /// <summary>
        /// TextMeshProUGUI의 알파를 fromAlpha에서 toAlpha로 duration초 동안 선형 전환.
        /// </summary>
        private static IEnumerator FadeText(TextMeshProUGUI target, float fromAlpha, float toAlpha, float duration)
        {
            if (target == null) yield break;
            if (duration <= 0f)
            {
                SetTextAlpha(target, toAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
                SetTextAlpha(target, alpha);
                yield return null;
            }

            SetTextAlpha(target, toAlpha);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        /// <summary>TextMeshProUGUI의 Color.alpha만 변경한다.</summary>
        private static void SetTextAlpha(TextMeshProUGUI target, float alpha)
        {
            Color c = target.color;
            c.a = Mathf.Clamp01(alpha);
            target.color = c;
        }

        /// <summary>필수 참조 누락 시 경고 로그 출력.</summary>
        private void ValidateReferences()
        {
            if (yearText == null)
                Debug.LogError("YearDisplayUI: yearText(TextMeshProUGUI)가 Inspector에 연결되지 않았습니다.");

            if (anchorTitleText == null)
                Debug.LogWarning("YearDisplayUI: anchorTitleText가 없으면 앵커 타이틀 표시가 비활성화됩니다.");
        }
    }
}
