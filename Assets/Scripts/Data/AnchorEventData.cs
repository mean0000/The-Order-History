using System;
using System.Collections.Generic;

namespace TheOther.Data
{
    /// <summary>
    /// anchor_events.json의 개별 나라 이벤트 항목.
    /// JSON 필드명과 1:1 매핑 — snake_case 유지.
    /// </summary>
    [Serializable]
    public class CountryEventData
    {
        /// <summary>ISO 3166-1 alpha-2 국가 코드 (예: "KR", "GB")</summary>
        public string country_code;

        /// <summary>나라 이름 (한국어, 예: "고려", "잉글랜드")</summary>
        public string country_name;

        /// <summary>해당 나라의 시대 명칭 (예: "고려 중기")</summary>
        public string era;

        /// <summary>사건 제목</summary>
        public string event_title;

        /// <summary>사건 설명 본문</summary>
        public string description;

        /// <summary>감정 톤 태그 (예: "평화와 번영")</summary>
        public string emotional_tone;

        // ── 편의 프로퍼티 ────────────────────────────────────────────────

        /// <summary>country_code를 대문자로 정규화해 반환</summary>
        public string CountryCodeNormalized =>
            string.IsNullOrEmpty(country_code) ? string.Empty : country_code.ToUpperInvariant();

        public override string ToString() =>
            $"[{CountryCodeNormalized}] {event_title} ({era})";
    }

    /// <summary>
    /// anchor_events.json 배열의 단일 앵커 항목.
    /// 하나의 역사적 기준 연도와 해당 연도의 다국 이벤트 목록을 담는다.
    /// </summary>
    [Serializable]
    public class AnchorEventData
    {
        /// <summary>앵커 연도 (예: 1066)</summary>
        public int year;

        /// <summary>앵커 사건 이름 (예: "노르만 정복")</summary>
        public string anchor_title;

        /// <summary>해당 연도에 등록된 나라별 이벤트 목록</summary>
        public List<CountryEventData> events;

        // ── 편의 메서드 ────────────────────────────────────────────────

        /// <summary>
        /// 지정한 국가 코드의 이벤트를 반환한다.
        /// 대소문자를 구분하지 않으며, 없으면 null 반환.
        /// </summary>
        public CountryEventData GetEventByCountry(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || events == null)
                return null;

            string normalized = countryCode.ToUpperInvariant();
            return events.Find(e =>
                e != null &&
                string.Equals(e.CountryCodeNormalized, normalized, StringComparison.Ordinal));
        }

        public override string ToString() =>
            $"{year} — {anchor_title} ({events?.Count ?? 0}개 나라)";
    }

    /// <summary>
    /// JsonUtility는 최상위 배열을 직접 역직렬화할 수 없으므로
    /// 래퍼 클래스를 사용해 "[...]" 형식의 JSON을 파싱한다.
    /// </summary>
    [Serializable]
    public class AnchorEventDataWrapper
    {
        public List<AnchorEventData> items;

        /// <summary>
        /// "[...]" 형태의 JSON 문자열을 래퍼 형식으로 변환하는 헬퍼.
        /// JsonUtility.FromJson&lt;AnchorEventDataWrapper&gt;(Wrap(json)) 으로 사용.
        /// </summary>
        public static string Wrap(string jsonArray) =>
            $"{{\"items\":{jsonArray}}}";
    }
}
