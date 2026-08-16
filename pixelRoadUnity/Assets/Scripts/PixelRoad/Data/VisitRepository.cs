using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// 방문 기록을 JSON 파일로 읽고 쓴다. 생성 시 한 번 읽어 메모리에 들고 있다가
    /// 새 방문이 생길 때만 다시 저장한다.
    /// </summary>
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

        /// <summary>기기의 영구 저장 경로에 기록을 두는 기본 생성자.</summary>
        public VisitRepository()
            : this(Path.Combine(Application.persistentDataPath, FileName))
        {
        }

        /// <summary>저장 경로를 직접 지정한다. 테스트에서 임시 폴더를 쓰기 위한 통로다.</summary>
        public VisitRepository(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                throw new ArgumentException("Visit storage path is required.", nameof(storagePath));
            }

            filePath = storagePath;
            Load();
        }

        /// <summary>해당 랜드마크를 한 번이라도 방문했는지 확인한다.</summary>
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

        /// <summary>
        /// 저장 파일을 읽어 메모리 목록을 채운다. 파일이 깨졌으면 경고만 남기고
        /// 빈 상태로 시작해 앱이 멈추지 않게 한다.
        /// </summary>
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

        /// <summary>
        /// 임시 파일에 먼저 쓰고 교체한다. 저장 도중 종료돼도 원본이 반쯤 망가지지 않게 하려는 것이다.
        /// </summary>
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

        /// <summary>기록 목록을 사람이 읽을 수 있게 들여쓴 JSON 배열 문자열로 만든다.</summary>
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

        /// <summary>저장된 시각 문자열을 로컬 시간으로 되돌린다. 형식이 다르면 실패로 처리한다.</summary>
        private static bool TryParseTimestamp(string value, out DateTime result)
        {
            return DateTime.TryParseExact(
                value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out result);
        }

        /// <summary>배열 루트를 JsonUtility로 읽기 위해 한 겹 씌우는 래퍼.</summary>
        [Serializable]
        private sealed class VisitedLandmarkCollection
        {
            public VisitedLandmarkRecord[] items;
        }
    }

    /// <summary>
    /// 랜드마크 하나의 방문 이력. 파일에 그대로 직렬화되므로 필드명이 곧 JSON 키다.
    /// </summary>
    [Serializable]
    public sealed class VisitedLandmarkRecord
    {
        public int landmarkId;
        public int visitCount;
        public string firstVisitedAt;
        public string lastVisitedAt;
    }
}
