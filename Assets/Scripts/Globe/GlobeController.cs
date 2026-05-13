using UnityEngine;
using TheOther.Core;

namespace TheOther.Globe
{
    /// <summary>
    /// 지구본 인터랙션 컨트롤러.
    ///
    /// 담당 기능:
    ///   - 마우스 드래그 → 지구 회전 (관성 포함)
    ///   - 마우스 스크롤 → 카메라 줌인/아웃
    ///   - 마우스 클릭 → 국가 선택 (RayCast + UV → CountryLookup)
    ///
    /// 씬 구성 전제:
    ///   - Globe 오브젝트: 원점(0,0,0) 기준 Sphere + SphereCollider
    ///   - Camera: Globe를 바라보며 Z축 방향으로 배치 (예: position (0,0,-8))
    /// </summary>
    public class GlobeController : MonoBehaviour
    {
        // ── Inspector 노출 필드 ───────────────────────────────────────────

        [Header("회전 설정")]
        [SerializeField] private float rotationSpeed = 0.3f;    // 드래그 감도
        [SerializeField] private float inertiaDamping = 3f;     // 관성 감속 계수 (클수록 빠르게 멈춤)

        [Header("줌 설정")]
        [SerializeField] private float zoomSpeed = 2f;          // 줌 속도
        [SerializeField] private float zoomMin = 3f;            // 최소 카메라 거리 (지구 근접)
        [SerializeField] private float zoomMax = 12f;           // 최대 카메라 거리 (전체 보임)

        [Header("오브젝트 참조")]
        [SerializeField] private Transform globeTransform;      // 회전할 지구 Transform
        [SerializeField] private Camera mainCamera;             // 줌/레이캐스트에 사용하는 카메라

        // ── 드래그 상태 ───────────────────────────────────────────────────

        private bool _isDragging = false;
        private Vector3 _lastMousePosition;     // 직전 프레임 마우스 위치
        private Vector2 _velocity;              // 현재 회전 속도 (x: yaw, y: pitch) — 관성에 사용

        // ── 클릭 판별 ─────────────────────────────────────────────────────

        private Vector3 _mouseDownPosition;     // 마우스 버튼 눌린 순간 위치
        private const float ClickThresholdPx = 5f;  // 이 픽셀 이하 이동 → 클릭으로 처리

        // ── 줌 상태 ───────────────────────────────────────────────────────

        private float _currentZoomDistance;     // 현재 카메라 ↔ 지구 중심 거리

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            ValidateReferences();
        }

        private void Start()
        {
            if (mainCamera != null)
            {
                // 씬 시작 시 카메라의 초기 거리를 기준값으로 설정
                _currentZoomDistance = Vector3.Distance(
                    mainCamera.transform.position,
                    GetGlobeCenter()
                );
                // min/max 범위 안으로 클램프
                _currentZoomDistance = Mathf.Clamp(_currentZoomDistance, zoomMin, zoomMax);
            }
        }

        private void Update()
        {
            HandleDragInput();
            HandleScrollInput();
            HandleClickInput();
            ApplyInertia();
        }

        // ── 드래그 입력 처리 ─────────────────────────────────────────────

        private void HandleDragInput()
        {
            // 마우스 버튼 누름 시작
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastMousePosition = Input.mousePosition;
                _mouseDownPosition = Input.mousePosition;

                // 드래그 시작 시 관성 초기화 (이전 회전 흐름 차단)
                _velocity = Vector2.zero;
            }

            // 드래그 중: 델타 계산 → 회전 적용
            if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector3 delta = Input.mousePosition - _lastMousePosition;

                if (delta.sqrMagnitude > 0f)
                {
                    // x 이동 → Y축(yaw) 회전, y 이동 → X축(pitch) 회전
                    float yaw   =  delta.x * rotationSpeed;
                    float pitch = -delta.y * rotationSpeed;

                    RotateGlobe(yaw, pitch);

                    // 관성용 속도 갱신: 스무딩 없이 직전 델타를 그대로 저장
                    // (Time.deltaTime 나누기: 프레임레이트 독립 관성 계산을 위해)
                    if (Time.deltaTime > 0f)
                    {
                        _velocity.x = yaw   / Time.deltaTime;
                        _velocity.y = pitch  / Time.deltaTime;
                    }
                }

                _lastMousePosition = Input.mousePosition;
            }

            // 마우스 버튼 해제 → 드래그 종료, 관성 시작
            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                // _velocity는 마지막 프레임 값을 유지 → ApplyInertia()가 이어받아 감속
            }
        }

        // ── 관성 감속 ─────────────────────────────────────────────────────

        private void ApplyInertia()
        {
            // 드래그 중에는 관성 적용 안 함 (드래그 핸들러가 직접 회전)
            if (_isDragging) return;

            // 속도가 무시해도 될 수준이면 조기 종료 (연산 낭비 방지)
            if (_velocity.sqrMagnitude < 0.001f)
            {
                _velocity = Vector2.zero;
                return;
            }

            // 지수 감속: 매 프레임 (1 - damping * dt) 배율로 줄어든다
            // → 고정 계수 대비 프레임레이트 독립적인 곡선 감속
            float decayFactor = 1f - Mathf.Clamp01(inertiaDamping * Time.deltaTime);
            _velocity *= decayFactor;

            RotateGlobe(_velocity.x * Time.deltaTime, _velocity.y * Time.deltaTime);
        }

        // ── 스크롤 줌 처리 ───────────────────────────────────────────────

        private void HandleScrollInput()
        {
            // Ctrl + ScrollWheel 만 줌으로 처리한다.
            // 순수 ScrollWheel(Ctrl 없음)은 TimelineController가 앵커 이동에 사용한다.
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            if (mainCamera == null) return;

            // 스크롤 위 → 줌인(거리 감소), 아래 → 줌아웃(거리 증가)
            _currentZoomDistance -= scroll * zoomSpeed;
            _currentZoomDistance = Mathf.Clamp(_currentZoomDistance, zoomMin, zoomMax);

            ApplyCameraZoom();
        }

        /// <summary>
        /// 카메라를 지구 중심 기준으로 현재 방향을 유지한 채 거리만 조정한다.
        /// </summary>
        private void ApplyCameraZoom()
        {
            if (mainCamera == null) return;

            Vector3 center = GetGlobeCenter();
            Vector3 directionFromCenter = (mainCamera.transform.position - center).normalized;

            mainCamera.transform.position = center + directionFromCenter * _currentZoomDistance;
        }

        // ── 클릭 → 국가 선택 처리 ───────────────────────────────────────

        private void HandleClickInput()
        {
            if (!Input.GetMouseButtonUp(0)) return;

            // 드래그 거리가 임계값을 초과하면 클릭이 아닌 드래그로 간주
            float dragDistance = Vector3.Distance(Input.mousePosition, _mouseDownPosition);
            if (dragDistance >= ClickThresholdPx) return;

            TrySelectCountryAtCursor();
        }

        /// <summary>
        /// 현재 마우스 커서 위치에서 레이를 발사해 지구 표면의 국가를 판별한다.
        /// </summary>
        private void TrySelectCountryAtCursor()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            // hit.textureCoord는 MeshCollider(Read/Write Enabled)에서만 유효한 UV를 반환한다.
            // SphereCollider/BoxCollider 등 프리미티브 콜라이더는 항상 (0,0)을 반환하므로
            // Globe 오브젝트에는 반드시 MeshCollider를 사용해야 한다.
            // (GlobeSceneSetup.cs 참조)
            Vector2 uv = hit.textureCoord;

            string countryCode = CountryLookup.GetCountryCode(uv);

            Debug.Log($"GlobeController: 클릭 UV({uv.x:F3}, {uv.y:F3}) → 국가 코드: '{countryCode}'");

            if (string.IsNullOrEmpty(countryCode))
            {
                // 빈 영역 클릭 → 선택 해제
                GameManager.Instance.DeselectCountry();
            }
            else
            {
                GameManager.Instance.SelectCountry(countryCode);
            }
        }

        // ── 회전 적용 헬퍼 ───────────────────────────────────────────────

        /// <summary>
        /// 지구를 월드 축 기준으로 yaw(Y축), pitch(X축) 회전시킨다.
        /// 월드 축 사용: 드래그 방향이 화면과 직관적으로 일치한다.
        /// </summary>
        private void RotateGlobe(float yaw, float pitch)
        {
            if (globeTransform == null) return;

            // 월드 Y축 기준 좌우 회전
            globeTransform.Rotate(Vector3.up, yaw, Space.World);

            // 카메라 기준 수평 축으로 상하 회전
            // Vector3.right(월드 X)가 아닌 카메라 right 축을 사용해야
            // 지구가 기울어진 상태에서도 드래그 방향이 화면과 일치한다.
            Vector3 pitchAxis = mainCamera != null
                ? mainCamera.transform.right
                : Vector3.right;
            globeTransform.Rotate(pitchAxis, pitch, Space.World);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────

        /// <summary>지구 중심 월드 좌표를 반환한다. globeTransform이 없으면 원점.</summary>
        private Vector3 GetGlobeCenter()
        {
            return globeTransform != null ? globeTransform.position : Vector3.zero;
        }

        /// <summary>필수 참조가 누락된 경우 경고 로그를 출력한다.</summary>
        private void ValidateReferences()
        {
            if (globeTransform == null)
                Debug.LogWarning("GlobeController: globeTransform이 할당되지 않았습니다. Inspector에서 Globe 오브젝트를 연결하세요.");

            if (mainCamera == null)
            {
                // 자동 폴백: Camera.main 사용
                mainCamera = Camera.main;
                if (mainCamera == null)
                    Debug.LogError("GlobeController: mainCamera를 찾을 수 없습니다. Inspector에서 직접 할당하세요.");
                else
                    Debug.Log("GlobeController: mainCamera를 Camera.main으로 자동 설정했습니다.");
            }
        }

#if UNITY_EDITOR
        // 에디터 전용: Scene 뷰에서 줌 범위 시각화
        private void OnDrawGizmosSelected()
        {
            if (mainCamera == null) return;
            Vector3 center = GetGlobeCenter();

            // 최소/최대 줌 거리를 구 형태로 표시
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(center, zoomMin);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            Gizmos.DrawWireSphere(center, zoomMax);
        }
#endif
    }
}
