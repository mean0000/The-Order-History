using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheOther.Core;

namespace TheOther.Timeline
{
    /// <summary>
    /// 타임라인 컨트롤러.
    ///
    /// 담당 기능:
    ///   - 마우스 스크롤(Ctrl 없음) → 앵커 연도 간 이동
    ///   - 앵커 스냅: SmoothDamp 기반 0.3초 부드러운 전환
    ///   - 전환 중 스크롤 입력 무시 (큐잉 없음)
    ///   - GameManager.SetYear() 호출 (앵커 도달 시)
    ///   - DirectionalLight 방향 → 연도에 따라 서서히 변화
    ///
    /// 씬 구성 전제:
    ///   - GameManager, HistoryDataLoader 싱글톤이 씬에 존재
    ///   - DirectionalLight를 Inspector에서 연결
    /// </summary>
    public class TimelineController : MonoBehaviour
    {
        // ── Inspector 노출 필드 ───────────────────────────────────────────

        [Header("태양(DirectionalLight) 설정")]
        [SerializeField] private Light sunLight;                    // 씬의 Directional Light
        [SerializeField] private float sunRotationSpeed = 2f;      // 태양 방향 전환 속도 (스무딩)

        [Header("스크롤 입력 설정")]
        [SerializeField] private float scrollThreshold = 1.0f;     // 앵커 이동 발동 임계값 (누적 스크롤)
        [SerializeField] private float scrollSensitivity = 3f;     // 스크롤 누적 배율

        [Header("앵커 스냅 설정")]
        [SerializeField] private float snapDuration = 0.3f;        // 연도 전환 소요 시간 (초)

        // ── 앵커 데이터 ───────────────────────────────────────────────────

        private IReadOnlyList<int> _anchorYears;    // 정렬된 앵커 연도 목록
        private int _currentAnchorIndex = 0;        // 현재 앵커 인덱스

        // ── 스크롤 상태 ───────────────────────────────────────────────────

        private float _scrollAccumulator = 0f;      // 스크롤 누적값
        private bool _isTransitioning = false;       // 연도 전환 중 여부

        // ── 태양 방향 스무딩 ─────────────────────────────────────────────

        // 현재 태양 Euler 각도 (X축 기준 수직 방향 제어)
        private float _currentSunAngle = 0f;
        private float _targetSunAngle = 0f;
        private float _sunAngleVelocity = 0f;       // SmoothDamp 내부 속도

        // 연도 범위를 0~360도로 매핑하는 상수
        private const float SUN_ANGLE_MIN = 0f;
        private const float SUN_ANGLE_MAX = 360f;

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            // HistoryDataLoader 로드 완료 이벤트 구독
            // IsLoaded가 이미 true인 경우(씬 재진입 등)를 위해 즉시 초기화도 시도한다
            if (HistoryDataLoader.Instance == null) return;

            HistoryDataLoader loader = HistoryDataLoader.Instance;
            loader.OnLoaded += HandleDataLoaded;

            if (loader.IsLoaded)
            {
                InitializeAnchorData();
            }
        }

        private void OnDisable()
        {
            HistoryDataLoader.Instance.OnLoaded -= HandleDataLoaded;
        }

        private void Update()
        {
            HandleScrollInput();
            UpdateSunDirection();
        }

        // ── 앵커 데이터 초기화 ────────────────────────────────────────────

        private void HandleDataLoaded()
        {
            InitializeAnchorData();
        }

        /// <summary>
        /// HistoryDataLoader에서 앵커 목록을 받아 초기 상태를 설정한다.
        /// 현재 GameManager.SelectedYear와 가장 가까운 앵커 인덱스로 시작.
        /// </summary>
        private void InitializeAnchorData()
        {
            _anchorYears = HistoryDataLoader.Instance.GetAnchorYears();

            if (_anchorYears == null || _anchorYears.Count == 0)
            {
                Debug.LogWarning("TimelineController: 앵커 연도 데이터가 비어 있습니다.");
                return;
            }

            // 현재 선택 연도에 가장 가까운 앵커 인덱스 탐색
            int currentYear = GameManager.Instance.SelectedYear;
            _currentAnchorIndex = FindNearestAnchorIndex(currentYear);

            // 태양 초기 방향 즉시 동기화
            _targetSunAngle = YearToSunAngle(_anchorYears[_currentAnchorIndex]);
            _currentSunAngle = _targetSunAngle;
            ApplySunAngle(_currentSunAngle);

            Debug.Log($"TimelineController: 초기화 완료. 앵커 {_anchorYears.Count}개, 시작 연도: {_anchorYears[_currentAnchorIndex]}");
        }

        // ── 스크롤 입력 처리 ─────────────────────────────────────────────

        /// <summary>
        /// Ctrl 없는 순수 ScrollWheel 입력만 처리한다.
        /// GlobeController는 Ctrl + ScrollWheel로 줌을 담당한다.
        /// 전환 중에는 입력을 무시한다 (큐잉 없음).
        /// </summary>
        private void HandleScrollInput()
        {
            // 데이터 미로드 또는 전환 중이면 무시
            if (_anchorYears == null || _anchorYears.Count == 0) return;
            if (_isTransitioning) return;

            // Ctrl 키가 눌린 상태면 GlobeController의 줌 입력이므로 무시
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            // 스크롤 누적 (위: 양수 → 연도 증가(미래), 아래: 음수 → 연도 감소(과거))
            _scrollAccumulator += scroll * scrollSensitivity;

            // 임계값 초과 시 앵커 이동
            if (_scrollAccumulator >= scrollThreshold)
            {
                _scrollAccumulator = 0f;
                TryMoveToNextAnchor(direction: 1);
            }
            else if (_scrollAccumulator <= -scrollThreshold)
            {
                _scrollAccumulator = 0f;
                TryMoveToNextAnchor(direction: -1);
            }
        }

        /// <summary>
        /// direction: +1 = 미래(다음 앵커), -1 = 과거(이전 앵커)
        /// </summary>
        private void TryMoveToNextAnchor(int direction)
        {
            int targetIndex = _currentAnchorIndex + direction;

            // 범위 클램프
            targetIndex = Mathf.Clamp(targetIndex, 0, _anchorYears.Count - 1);

            if (targetIndex == _currentAnchorIndex)
            {
                // 이미 처음 또는 끝 앵커 — 이동 없음
                return;
            }

            _currentAnchorIndex = targetIndex;
            int targetYear = _anchorYears[_currentAnchorIndex];

            // 태양 방향 목표 업데이트
            _targetSunAngle = YearToSunAngle(targetYear);

            // 코루틴으로 부드럽게 연도 전환
            StartCoroutine(TransitionToAnchor(targetYear));
        }

        // ── 앵커 전환 코루틴 ──────────────────────────────────────────────

        /// <summary>
        /// 0.3초 동안 연도를 부드럽게 전환하고 GameManager.SetYear()를 호출한다.
        /// 전환 완료 전에는 추가 스크롤 입력이 무시된다.
        /// </summary>
        private IEnumerator TransitionToAnchor(int targetYear)
        {
            _isTransitioning = true;

            float elapsed = 0f;
            int startYear = GameManager.Instance.SelectedYear;

            while (elapsed < snapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / snapDuration);

                // 중간 연도 표시용: 선형 보간 (UI에서 숫자가 스크롤되는 느낌)
                int displayYear = Mathf.RoundToInt(Mathf.Lerp(startYear, targetYear, t));

                // GameManager에는 최종 앵커 연도에 도달했을 때만 이벤트가 가도록
                // 중간값은 YearDisplayUI가 별도로 처리하거나 여기서 직접 처리 가능.
                // 현재 구현: 전환 중간에도 GameManager를 업데이트하면 불필요한 이벤트가
                // 여러 시스템에 전파되므로, 최종 도달 시에만 SetYear() 호출한다.
                // → YearDisplayUI는 _transitionDisplayYear 프로퍼티를 폴링하거나,
                //   TransitionYearChanged 이벤트를 사용해도 된다.
                //   단순함을 위해 여기서는 직접 UI 없이 최종 호출만 한다.
                _ = displayYear; // 향후 중간 연도 UI 연동 시 사용

                yield return null;
            }

            // 전환 완료: GameManager에 최종 앵커 연도 확정
            GameManager.Instance.SetYear(targetYear);

            _isTransitioning = false;
            Debug.Log($"TimelineController: 앵커 전환 완료 → {targetYear}년");
        }

        // ── 태양 방향 제어 ────────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 현재 태양 각도를 목표 각도로 SmoothDamp 전환한다.
        /// TASK-003 셰이더와 분리: 씬의 DirectionalLight 방향만 제어한다.
        /// </summary>
        private void UpdateSunDirection()
        {
            if (sunLight == null) return;

            // 각도가 이미 수렴하면 연산 스킵
            if (Mathf.Abs(_currentSunAngle - _targetSunAngle) < 0.01f)
            {
                _currentSunAngle = _targetSunAngle;
                return;
            }

            _currentSunAngle = Mathf.SmoothDamp(
                _currentSunAngle,
                _targetSunAngle,
                ref _sunAngleVelocity,
                1f / sunRotationSpeed  // smoothTime: 속도가 클수록 빠르게 수렴
            );

            ApplySunAngle(_currentSunAngle);
        }

        /// <summary>
        /// 계산된 각도를 DirectionalLight의 Euler rotation에 적용한다.
        /// X축: 수직 고도(태양 높낮이), Y축: 수평 방위(시대 흐름에 따른 공전)
        /// </summary>
        private void ApplySunAngle(float angle)
        {
            if (sunLight == null) return;

            // Y축(수평)으로 공전 + 고정 X 기울기로 지구 기울기 표현 (약 30도)
            sunLight.transform.rotation = Quaternion.Euler(30f, angle, 0f);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────

        /// <summary>
        /// 연도(1000~2026)를 0~360도 태양 각도로 선형 매핑한다.
        /// </summary>
        private float YearToSunAngle(int year)
        {
            float t = Mathf.InverseLerp(GameManager.YEAR_MIN, GameManager.YEAR_MAX, year);
            return Mathf.Lerp(SUN_ANGLE_MIN, SUN_ANGLE_MAX, t);
        }

        /// <summary>
        /// 정렬된 _anchorYears에서 지정 연도와 가장 가까운 인덱스를 반환한다.
        /// </summary>
        private int FindNearestAnchorIndex(int year)
        {
            if (_anchorYears == null || _anchorYears.Count == 0) return 0;

            int nearestIndex = 0;
            int minDiff = int.MaxValue;

            for (int i = 0; i < _anchorYears.Count; i++)
            {
                int diff = Mathf.Abs(year - _anchorYears[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>필수 참조 누락 시 경고 로그 출력.</summary>
        private void ValidateReferences()
        {
            if (sunLight == null)
                Debug.LogWarning("TimelineController: sunLight(DirectionalLight)가 Inspector에 연결되지 않았습니다. 태양 방향 제어가 비활성화됩니다.");
        }

#if UNITY_EDITOR
        // 에디터 전용: Inspector에서 앵커 인덱스 실시간 확인
        private void OnValidate()
        {
            if (scrollThreshold <= 0f) scrollThreshold = 0.1f;
            if (snapDuration <= 0f)    snapDuration    = 0.1f;
        }
#endif
    }
}
