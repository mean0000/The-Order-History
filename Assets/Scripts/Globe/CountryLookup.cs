using System.Collections.Generic;
using UnityEngine;

namespace TheOther.Globe
{
    /// <summary>
    /// UV 좌표 → 국가 코드 변환 유틸리티.
    ///
    /// MVP 단계: 주요 국가의 UV 범위를 수동으로 정의.
    /// 이후 국가 경계 텍스처(픽셀 샘플링) 방식으로 교체 가능하도록
    /// GetCountryCode 인터페이스를 단일 진입점으로 유지한다.
    ///
    /// UV 규약:
    ///   U=0 → 경도 -180°,  U=1 → 경도 +180°
    ///   V=0 → 위도 +90°(북극), V=1 → 위도 -90°(남극)
    /// </summary>
    public static class CountryLookup
    {
        // ── 국가 UV 범위 정의 ─────────────────────────────────────────────

        /// <summary>
        /// 국가별 UV 사각형 범위 목록.
        /// 겹치는 경우 리스트 앞쪽 항목이 우선된다(더 작은/정확한 나라를 앞에 배치).
        /// </summary>
        private static readonly List<CountryUvEntry> _entries = new List<CountryUvEntry>
        {
            // ── 동아시아 ────────────────────────────────────────────────
            new CountryUvEntry("KR", 0.755f, 0.769f, 0.27f, 0.37f),   // 한국
            new CountryUvEntry("JP", 0.770f, 0.800f, 0.25f, 0.38f),   // 일본
            new CountryUvEntry("VN", 0.740f, 0.760f, 0.35f, 0.45f),   // 베트남
            new CountryUvEntry("MN", 0.700f, 0.760f, 0.18f, 0.28f),   // 몽골
            new CountryUvEntry("CN", 0.700f, 0.780f, 0.22f, 0.42f),   // 중국 (동아시아 중 가장 넓음 → 뒤에 배치)

            // ── 남아시아 / 중동 ────────────────────────────────────────
            new CountryUvEntry("AF", 0.645f, 0.680f, 0.28f, 0.38f),   // 아프가니스탄
            new CountryUvEntry("IN", 0.680f, 0.730f, 0.30f, 0.48f),   // 인도
            new CountryUvEntry("TR", 0.580f, 0.630f, 0.28f, 0.36f),   // 터키

            // ── 유럽 ────────────────────────────────────────────────────
            new CountryUvEntry("GB", 0.490f, 0.515f, 0.22f, 0.30f),   // 영국
            new CountryUvEntry("DE", 0.530f, 0.555f, 0.24f, 0.32f),   // 독일
            new CountryUvEntry("FR", 0.510f, 0.540f, 0.26f, 0.34f),   // 프랑스
            new CountryUvEntry("ES", 0.490f, 0.530f, 0.30f, 0.38f),   // 스페인
            new CountryUvEntry("EU", 0.490f, 0.580f, 0.20f, 0.36f),   // 유럽 일반 (개별 국가 뒤에 배치)

            // ── 러시아 (넓은 범위 → 개별 국가 뒤에 배치) ───────────────
            new CountryUvEntry("RU", 0.550f, 0.850f, 0.05f, 0.30f),   // 러시아

            // ── 아메리카 ────────────────────────────────────────────────
            new CountryUvEntry("MX", 0.155f, 0.215f, 0.38f, 0.48f),   // 멕시코 (US보다 앞에 배치)
            new CountryUvEntry("US", 0.100f, 0.280f, 0.22f, 0.42f),   // 미국
        };

        // ── 공개 API ──────────────────────────────────────────────────────

        /// <summary>
        /// UV 좌표에 해당하는 국가 코드를 반환한다.
        /// 매칭되는 나라가 없으면 빈 문자열을 반환한다.
        /// </summary>
        /// <param name="uv">지구 표면 UV 좌표 (0~1 범위)</param>
        /// <returns>ISO 3166-1 alpha-2 국가 코드, 또는 빈 문자열</returns>
        public static string GetCountryCode(Vector2 uv)
        {
            // UV 범위를 0~1로 클램프 (부동소수점 오차 방어)
            float u = Mathf.Clamp01(uv.x);
            float v = Mathf.Clamp01(uv.y);

            foreach (CountryUvEntry entry in _entries)
            {
                if (entry.Contains(u, v))
                    return entry.CountryCode;
            }

            return string.Empty;
        }

        /// <summary>
        /// 등록된 모든 국가 코드 목록을 반환한다.
        /// 디버그 및 에디터 유틸 용도.
        /// </summary>
        public static IEnumerable<string> GetAllCountryCodes()
        {
            // 중복 제거 없이 순서 그대로 반환 (MVP에서는 중복 없음)
            foreach (CountryUvEntry entry in _entries)
                yield return entry.CountryCode;
        }

        // ── 내부 데이터 구조 ─────────────────────────────────────────────

        /// <summary>
        /// 국가 하나의 UV 사각형 범위를 나타내는 불변 구조체.
        /// </summary>
        private readonly struct CountryUvEntry
        {
            public readonly string CountryCode;

            // U (경도) 범위
            private readonly float _uMin;
            private readonly float _uMax;

            // V (위도) 범위
            private readonly float _vMin;
            private readonly float _vMax;

            public CountryUvEntry(string code, float uMin, float uMax, float vMin, float vMax)
            {
                CountryCode = code;
                _uMin = uMin;
                _uMax = uMax;
                _vMin = vMin;
                _vMax = vMax;
            }

            /// <summary>주어진 (u, v) 좌표가 이 나라의 범위 안에 포함되는지 반환한다.</summary>
            public bool Contains(float u, float v)
            {
                return u >= _uMin && u <= _uMax
                    && v >= _vMin && v <= _vMax;
            }
        }
    }
}
