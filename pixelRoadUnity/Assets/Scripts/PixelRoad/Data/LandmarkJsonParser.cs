using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// landmarks.json을 <see cref="SpotDefinition"/> 목록으로 변환한다.
    /// JsonUtility가 배열 루트를 못 읽으므로 객체로 감싸서 파싱하고,
    /// id 중복이나 좌표 범위 같은 데이터 오류는 여기서 걸러 예외로 알린다.
    /// </summary>
    public static class LandmarkJsonParser
    {
        /// <summary>
        /// JSON 문자열을 파싱해 검증까지 마친 랜드마크 목록을 돌려준다.
        /// 반경이 비어 있는 항목은 <paramref name="fallbackVisitRadiusMeters"/>로 채운다.
        /// </summary>
        public static List<SpotDefinition> Parse(string json, float fallbackVisitRadiusMeters)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<SpotDefinition>();
            }

            string trimmed = json.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal)
                || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                throw new FormatException("landmarks.json root must be a JSON array.");
            }

            LandmarkJsonCollection collection;
            try
            {
                collection = JsonUtility.FromJson<LandmarkJsonCollection>("{\"items\":" + trimmed + "}");
            }
            catch (Exception exception)
            {
                throw new FormatException("landmarks.json could not be parsed.", exception);
            }

            LandmarkJsonRecord[] records = collection != null && collection.items != null
                ? collection.items
                : Array.Empty<LandmarkJsonRecord>();
            List<SpotDefinition> landmarks = new List<SpotDefinition>(records.Length);
            HashSet<int> ids = new HashSet<int>();
            for (int index = 0; index < records.Length; index++)
            {
                LandmarkJsonRecord record = records[index];
                if (record == null)
                {
                    throw new FormatException("landmarks.json contains a null item at index " + index + ".");
                }

                if (record.id <= 0)
                {
                    throw new FormatException("landmark id must be greater than zero at index " + index + ".");
                }

                if (!ids.Add(record.id))
                {
                    throw new FormatException("landmark id is duplicated: " + record.id + ".");
                }

                if (record.latitude < -90d || record.latitude > 90d
                    || record.longitude < -180d || record.longitude > 180d)
                {
                    throw new FormatException("landmark coordinates are invalid for id " + record.id + ".");
                }

                float visitRadius = record.visitRadius > 0f
                    ? record.visitRadius
                    : fallbackVisitRadiusMeters;
                landmarks.Add(new SpotDefinition(
                    record.id,
                    string.IsNullOrWhiteSpace(record.name) ? record.id.ToString() : record.name.Trim(),
                    (record.category ?? string.Empty).Trim(),
                    (record.collectionTitle ?? string.Empty).Trim(),
                    (record.address ?? string.Empty).Trim(),
                    record.latitude,
                    record.longitude,
                    visitRadius,
                    (record.thumbnail ?? string.Empty).Trim(),
                    (record.shortDescription ?? string.Empty).Trim(),
                    (record.history ?? string.Empty).Trim(),
                    record.tags ?? Array.Empty<string>(),
                    string.IsNullOrWhiteSpace(record.view360Image) ? null : record.view360Image.Trim()));
            }

            return landmarks;
        }

        /// <summary>배열 루트를 JsonUtility로 읽기 위해 한 겹 씌우는 래퍼.</summary>
        [Serializable]
        private sealed class LandmarkJsonCollection
        {
            public LandmarkJsonRecord[] items;
        }

        /// <summary>JSON 필드명을 그대로 받는 원시 레코드. 검증 전 단계의 값이다.</summary>
        [Serializable]
        private sealed class LandmarkJsonRecord
        {
            public int id;
            public string name;
            public string category;
            public string collectionTitle;
            public string address;
            public double latitude;
            public double longitude;
            public float visitRadius;
            public string thumbnail;
            public string shortDescription;
            public string history;
            public string[] tags;
            public string view360Image;
        }
    }
}
