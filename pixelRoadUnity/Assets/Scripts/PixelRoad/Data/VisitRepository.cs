using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelRoad.Data
{
    public sealed class VisitRepository
    {
        public const string FileName = "visited_landmarks.json";
        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss";

        private readonly string filePath;
        private readonly List<VisitedLandmarkRecord> records = new List<VisitedLandmarkRecord>();
        private readonly Dictionary<int, VisitedLandmarkRecord> recordsById =
            new Dictionary<int, VisitedLandmarkRecord>();

        public string FilePath
        {
            get { return filePath; }
        }

        public IReadOnlyList<VisitedLandmarkRecord> Records
        {
            get { return records; }
        }

        public VisitRepository()
            : this(Path.Combine(Application.persistentDataPath, FileName))
        {
        }

        public VisitRepository(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                throw new ArgumentException("Visit storage path is required.", nameof(storagePath));
            }

            filePath = storagePath;
            Load();
        }

        public bool HasVisited(int landmarkId)
        {
            return recordsById.ContainsKey(landmarkId);
        }

        /// <summary>
        /// 같은 로컬 날짜에는 한 번만 기록하고, 마지막 방문일의 다음 날짜부터 횟수를 늘린다.
        /// 새 방문이 저장됐을 때만 true를 반환한다.
        /// </summary>
        public bool RecordVisit(int landmarkId, DateTime visitedAt)
        {
            if (landmarkId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(landmarkId));
            }

            DateTime localVisitTime = visitedAt.Kind == DateTimeKind.Utc
                ? visitedAt.ToLocalTime()
                : visitedAt;
            string timestamp = localVisitTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

            if (recordsById.TryGetValue(landmarkId, out VisitedLandmarkRecord existing))
            {
                if (TryParseTimestamp(existing.lastVisitedAt, out DateTime lastVisited)
                    && localVisitTime.Date <= lastVisited.Date)
                {
                    return false;
                }

                existing.visitCount = Math.Max(1, existing.visitCount) + 1;
                if (string.IsNullOrWhiteSpace(existing.firstVisitedAt))
                {
                    existing.firstVisitedAt = timestamp;
                }

                existing.lastVisitedAt = timestamp;
            }
            else
            {
                VisitedLandmarkRecord created = new VisitedLandmarkRecord
                {
                    landmarkId = landmarkId,
                    visitCount = 1,
                    firstVisitedAt = timestamp,
                    lastVisitedAt = timestamp
                };
                records.Add(created);
                recordsById.Add(landmarkId, created);
            }

            records.Sort((left, right) => left.landmarkId.CompareTo(right.landmarkId));
            Save();
            return true;
        }

        private void Load()
        {
            records.Clear();
            recordsById.Clear();
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                if (!json.StartsWith("[", StringComparison.Ordinal)
                    || !json.EndsWith("]", StringComparison.Ordinal))
                {
                    throw new FormatException("visited_landmarks.json root must be a JSON array.");
                }

                VisitedLandmarkCollection collection = JsonUtility.FromJson<VisitedLandmarkCollection>(
                    "{\"items\":" + json + "}");
                VisitedLandmarkRecord[] loaded = collection != null && collection.items != null
                    ? collection.items
                    : Array.Empty<VisitedLandmarkRecord>();
                for (int index = 0; index < loaded.Length; index++)
                {
                    VisitedLandmarkRecord record = loaded[index];
                    if (record == null || record.landmarkId <= 0 || recordsById.ContainsKey(record.landmarkId))
                    {
                        continue;
                    }

                    record.visitCount = Math.Max(1, record.visitCount);
                    records.Add(record);
                    recordsById.Add(record.landmarkId, record);
                }

                records.Sort((left, right) => left.landmarkId.CompareTo(right.landmarkId));
            }
            catch (Exception exception)
            {
                records.Clear();
                recordsById.Clear();
                Debug.LogWarning("[PixelRoad] 방문 기록을 읽지 못했습니다: " + exception.Message);
            }
        }

        private void Save()
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = filePath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, SerializeRecords(), new UTF8Encoding(false));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                File.Move(temporaryPath, filePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private string SerializeRecords()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[\n");
            for (int index = 0; index < records.Count; index++)
            {
                string item = JsonUtility.ToJson(records[index], true).Replace("\n", "\n  ");
                builder.Append("  ").Append(item);
                if (index < records.Count - 1)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append("]\n");
            return builder.ToString();
        }

        private static bool TryParseTimestamp(string value, out DateTime result)
        {
            return DateTime.TryParseExact(
                value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out result);
        }

        [Serializable]
        private sealed class VisitedLandmarkCollection
        {
            public VisitedLandmarkRecord[] items;
        }
    }

    [Serializable]
    public sealed class VisitedLandmarkRecord
    {
        public int landmarkId;
        public int visitCount;
        public string firstVisitedAt;
        public string lastVisitedAt;
    }
}
