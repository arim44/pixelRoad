using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Data
{
    public static class LandmarkJsonParser
    {
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

        [Serializable]
        private sealed class LandmarkJsonCollection
        {
            public LandmarkJsonRecord[] items;
        }

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
