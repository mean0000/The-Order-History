using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheOther.Core
{
    /// <summary>
    /// 씬 전환, 현재 선택 연도, 선택 국가 코드 등
    /// 게임 전반의 글로벌 상태를 관리하는 싱글톤 매니저.
    ///
    /// 의존성: HistoryDataLoader (씬 전환 후에도 데이터 유지)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── 씬 이름 상수 ─────────────────────────────────────────────────
        // Build Settings에 등록된 씬 이름과 반드시 일치해야 한다.

        public const string SCENE_TITLE   = "TitleScene";
        public const string SCENE_MAIN    = "MainScene";
        public const string SCENE_DETAIL  = "DetailScene";

        // ── 타임라인 범위 상수 ───────────────────────────────────────────

        public const int YEAR_MIN = 1000;
        public const int YEAR_MAX = 2026;

        // ── 싱글톤 ────────────────────────────────────────────────────────

        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[GameManager]");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── 상태 프로퍼티 ─────────────────────────────────────────────────

        /// <summary>현재 타임라인에서 선택된 연도 (1000~2026)</summary>
        public int SelectedYear { get; private set; } = YEAR_MIN;

        /// <summary>현재 지구본에서 선택된 국가 코드 (ISO 3166-1 alpha-2). 미선택이면 빈 문자열.</summary>
        public string SelectedCountryCode { get; private set; } = string.Empty;

        /// <summary>씬 전환 중 여부 (중복 전환 방지용)</summary>
        public bool IsTransitioning { get; private set; } = false;

        // ── 이벤트 ────────────────────────────────────────────────────────

        /// <summary>연도 변경 시 발행. 이전 연도, 새 연도 순으로 전달.</summary>
        public event Action<int, int> OnYearChanged;

        /// <summary>국가 선택 변경 시 발행. 새 국가 코드 전달 (빈 문자열 = 선택 해제).</summary>
        public event Action<string> OnCountrySelected;

        /// <summary>씬 전환 시작 시 발행. 목적지 씬 이름 전달.</summary>
        public event Action<string> OnSceneTransitionStarted;

        /// <summary>씬 전환 완료 시 발행. 새 씬 이름 전달.</summary>
        public event Action<string> OnSceneTransitionCompleted;

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // 씬 전환 완료 콜백 등록
            SceneManager.sceneLoaded += HandleSceneLoaded;

            // HistoryDataLoader도 함께 초기화 보장
            EnsureHistoryDataLoader();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // ── 연도 관리 ─────────────────────────────────────────────────────

        /// <summary>
        /// 타임라인 연도를 변경한다.
        /// 범위(1000~2026) 외 값은 클램프 처리된다.
        /// </summary>
        public void SetYear(int year)
        {
            int clamped = Mathf.Clamp(year, YEAR_MIN, YEAR_MAX);

            if (clamped == SelectedYear) return;

            int previous = SelectedYear;
            SelectedYear = clamped;

            Debug.Log($"GameManager: 연도 변경 {previous} → {SelectedYear}");
            OnYearChanged?.Invoke(previous, SelectedYear);
        }

        // ── 국가 선택 관리 ────────────────────────────────────────────────

        /// <summary>
        /// 지구본에서 국가를 선택한다.
        /// countryCode가 null/빈 문자열이면 선택 해제로 처리.
        /// </summary>
        public void SelectCountry(string countryCode)
        {
            string normalized = string.IsNullOrEmpty(countryCode)
                ? string.Empty
                : countryCode.ToUpperInvariant();

            if (string.Equals(normalized, SelectedCountryCode, StringComparison.Ordinal))
                return;

            SelectedCountryCode = normalized;

            Debug.Log($"GameManager: 국가 선택 → '{SelectedCountryCode}'");
            OnCountrySelected?.Invoke(SelectedCountryCode);
        }

        /// <summary>현재 국가 선택을 해제한다.</summary>
        public void DeselectCountry() => SelectCountry(string.Empty);

        // ── 씬 전환 ──────────────────────────────────────────────────────

        /// <summary>타이틀 씬으로 전환</summary>
        public void GoToTitle() => LoadScene(SCENE_TITLE);

        /// <summary>메인 씬(지구본)으로 전환</summary>
        public void GoToMain() => LoadScene(SCENE_MAIN);

        /// <summary>
        /// 디테일 씬으로 전환.
        /// year, countryCode를 상태에 설정한 뒤 씬을 로드한다.
        /// </summary>
        public void GoToDetail(int year, string countryCode)
        {
            SetYear(year);
            SelectCountry(countryCode);
            LoadScene(SCENE_DETAIL);
        }

        /// <summary>씬 이름으로 직접 전환 (내부 공통 처리)</summary>
        private void LoadScene(string sceneName)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning($"GameManager: 씬 전환 중 중복 요청 무시 ({sceneName})");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("GameManager: 씬 이름이 비어 있습니다.");
                return;
            }

            IsTransitioning = true;
            Debug.Log($"GameManager: 씬 전환 시작 → {sceneName}");
            OnSceneTransitionStarted?.Invoke(sceneName);

            SceneManager.LoadScene(sceneName);
        }

        // ── 씬 전환 완료 콜백 ────────────────────────────────────────────

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IsTransitioning = false;
            Debug.Log($"GameManager: 씬 전환 완료 → {scene.name}");
            OnSceneTransitionCompleted?.Invoke(scene.name);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────

        /// <summary>
        /// HistoryDataLoader 인스턴스가 씬에 존재하는지 확인하고 없으면 생성.
        /// GameManager가 HistoryDataLoader보다 먼저 Awake될 경우를 대비.
        /// </summary>
        private static void EnsureHistoryDataLoader()
        {
            // 접근 시 자동 생성되는 싱글톤 구조 활용
            _ = HistoryDataLoader.Instance;
        }

        /// <summary>
        /// 현재 선택 상태를 초기값으로 리셋 (게임 재시작 등에서 호출).
        /// </summary>
        public void ResetState()
        {
            int previousYear = SelectedYear;
            SelectedYear = YEAR_MIN;
            SelectedCountryCode = string.Empty;

            if (previousYear != YEAR_MIN)
                OnYearChanged?.Invoke(previousYear, SelectedYear);

            OnCountrySelected?.Invoke(SelectedCountryCode);
            Debug.Log("GameManager: 상태 초기화 완료");
        }

#if UNITY_EDITOR
        // 에디터 전용: Inspector에서 현재 상태 확인용
        private void OnValidate()
        {
            // 에디터 재생 중 상태 확인을 위한 디버그 전용 — 런타임 로직 없음
        }
#endif
    }
}
