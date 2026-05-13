using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TheOther.Data;

namespace TheOther.Core
{
    /// <summary>
    /// StreamingAssets/anchor_events.json을 런타임에 로드·파싱하고
    /// 연도/국가 기반 조회 API를 제공하는 싱글톤 매니저.
    ///
    /// 사용법:
    ///   HistoryDataLoader.Instance.GetAnchorYears()
    ///   HistoryDataLoader.Instance.GetAnchorByYear(1066)
    ///   HistoryDataLoader.Instance.GetEventByYearAndCountry(1066, "KR")
    /// </summary>
    public class HistoryDataLoader : MonoBehaviour
    {
        // ── 싱글톤 ────────────────────────────────────────────────────────

        private static HistoryDataLoader _instance;

        public static HistoryDataLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 씬에 없으면 자동 생성 (씬 로드 순서 무관하게 안전하게 접근 가능)
                    GameObject go = new GameObject("[HistoryDataLoader]");
                    _instance = go.AddComponent<HistoryDataLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Inspector 노출 ───────────────────────────────────────────────

        [SerializeField, Tooltip("StreamingAssets 내 JSON 파일 이름")]
        private string _jsonFileName = "anchor_events.json";

        // ── 상태 ─────────────────────────────────────────────────────────

        /// <summary>데이터 로드 완료 여부</summary>
        public bool IsLoaded { get; private set; } = false;

        /// <summary>로드 중 에러 메시지. 없으면 null.</summary>
        public string LoadError { get; private set; } = null;

        /// <summary>로드 완료 시 발행되는 이벤트</summary>
        public event Action OnLoaded;

        /// <summary>로드 실패 시 발행되는 이벤트 (에러 메시지 포함)</summary>
        public event Action<string> OnLoadFailed;

        // ── 내부 데이터 ──────────────────────────────────────────────────

        // 빠른 조회를 위해 year → AnchorEventData 딕셔너리로 인덱싱
        private Dictionary<int, AnchorEventData> _anchorByYear =
            new Dictionary<int, AnchorEventData>();

        // 연도 정렬 목록 (타임라인 슬라이더 등에서 활용)
        private List<int> _sortedYears = new List<int>();

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            // 중복 인스턴스 제거
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            StartCoroutine(LoadJsonCoroutine());
        }

        // ── 로드 코루틴 ──────────────────────────────────────────────────

        private IEnumerator LoadJsonCoroutine()
        {
            string filePath = GetStreamingAssetsPath(_jsonFileName);

            // UnityWebRequest를 사용하면 Android/iOS/PC 모두 동일한 코드로 동작
            // 코루틴 내 using 선언은 Dispose 타이밍이 불확실하므로 블록 형태를 사용한다
            using (UnityWebRequest request = UnityWebRequest.Get(filePath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = $"HistoryDataLoader: JSON 로드 실패 — {request.error} ({filePath})";
                    LoadError = error;
                    Debug.LogError(error);
                    OnLoadFailed?.Invoke(error);
                    yield break;
                }

                string json = request.downloadHandler.text;
                ParseAndIndex(json);
            }
        }

        /// <summary>
        /// 플랫폼별 StreamingAssets 경로를 URL 형식으로 반환.
        /// UnityWebRequest가 요구하는 file:// 또는 jar:// 스킴 처리.
        /// </summary>
        private static string GetStreamingAssetsPath(string fileName)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: Application.streamingAssetsPath가 이미 jar:// 경로를 포함하므로 그대로 사용
            return Path.Combine(Application.streamingAssetsPath, fileName);
#else
            // PC, iOS, Editor 등은 file:// 스킴 또는 직접 경로
            return "file://" + Path.Combine(Application.streamingAssetsPath, fileName);
#endif
        }

        // ── 파싱 & 인덱싱 ────────────────────────────────────────────────

        private void ParseAndIndex(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                string error = "HistoryDataLoader: JSON 문자열이 비어 있습니다.";
                LoadError = error;
                Debug.LogError(error);
                OnLoadFailed?.Invoke(error);
                return;
            }

            AnchorEventDataWrapper wrapper;
            try
            {
                string wrappedJson = AnchorEventDataWrapper.Wrap(json);
                wrapper = JsonUtility.FromJson<AnchorEventDataWrapper>(wrappedJson);
            }
            catch (Exception ex)
            {
                string error = $"HistoryDataLoader: JSON 파싱 오류 — {ex.Message}";
                LoadError = error;
                Debug.LogError(error);
                OnLoadFailed?.Invoke(error);
                return;
            }

            if (wrapper?.items == null || wrapper.items.Count == 0)
            {
                string error = "HistoryDataLoader: 파싱 결과가 비어 있습니다. JSON 구조를 확인하세요.";
                LoadError = error;
                Debug.LogError(error);
                OnLoadFailed?.Invoke(error);
                return;
            }

            // 딕셔너리 및 연도 목록 구성
            _anchorByYear.Clear();
            _sortedYears.Clear();

            foreach (AnchorEventData anchor in wrapper.items)
            {
                if (anchor == null) continue;

                if (_anchorByYear.ContainsKey(anchor.year))
                {
                    Debug.LogWarning($"HistoryDataLoader: 중복 연도 발견 ({anchor.year}), 첫 번째 항목 유지.");
                    continue;
                }

                _anchorByYear[anchor.year] = anchor;
                _sortedYears.Add(anchor.year);
            }

            _sortedYears.Sort(); // 오름차순 정렬

            IsLoaded = true;
            Debug.Log($"HistoryDataLoader: 로드 완료 — {_sortedYears.Count}개 앵커 연도");
            OnLoaded?.Invoke();
        }

        // ── 공개 조회 API ─────────────────────────────────────────────────

        /// <summary>
        /// 로드된 모든 앵커 연도 목록을 오름차순으로 반환.
        /// 로드 전이면 빈 리스트 반환.
        /// </summary>
        public IReadOnlyList<int> GetAnchorYears()
        {
            return _sortedYears.AsReadOnly();
        }

        /// <summary>
        /// 지정 연도의 AnchorEventData를 반환.
        /// 해당 연도가 없거나 로드 전이면 null 반환.
        /// </summary>
        public AnchorEventData GetAnchorByYear(int year)
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("HistoryDataLoader: 아직 로드가 완료되지 않았습니다.");
                return null;
            }

            _anchorByYear.TryGetValue(year, out AnchorEventData result);
            return result;
        }

        /// <summary>
        /// 지정 연도 + 국가 코드로 단일 CountryEventData를 반환.
        /// 없으면 null 반환.
        /// </summary>
        /// <param name="year">앵커 연도</param>
        /// <param name="countryCode">ISO 3166-1 alpha-2 국가 코드 (대소문자 무관)</param>
        public CountryEventData GetEventByYearAndCountry(int year, string countryCode)
        {
            AnchorEventData anchor = GetAnchorByYear(year);
            if (anchor == null) return null;

            return anchor.GetEventByCountry(countryCode);
        }

        /// <summary>
        /// 지정 연도에 가장 가까운 앵커 연도를 반환.
        /// 타임라인 슬라이더에서 스냅 위치 계산 시 활용.
        /// </summary>
        public int GetNearestAnchorYear(int year)
        {
            if (_sortedYears.Count == 0) return year;

            int nearest = _sortedYears[0];
            int minDiff = Mathf.Abs(year - nearest);

            foreach (int anchorYear in _sortedYears)
            {
                int diff = Mathf.Abs(year - anchorYear);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    nearest = anchorYear;
                }
            }

            return nearest;
        }
    }
}
