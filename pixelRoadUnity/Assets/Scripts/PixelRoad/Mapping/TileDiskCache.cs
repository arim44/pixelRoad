using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 디스크에 보관하는 타일 하나의 본문과 재검증용 HTTP 정보를 담는다.
    /// </summary>
    [Serializable]
    public sealed class TileCacheEntry
    {
        public byte[] Data;
        public long ExpiresUnix;
        public string ETag;
        public string LastModified;
    }

    /// <summary>
    /// 내려받은 타일을 영구 저장 경로에 캐시해 재실행과 오프라인 상황에서도 지도를 그릴 수 있게 한다.
    /// 손상된 파일은 읽는 즉시 버리고, 용량이 넘치면 오래 안 쓴 항목부터 정리한다.
    /// </summary>
    public sealed class TileDiskCache
    {
        private const int MetadataVersion = 1;
        private const string DataExtension = ".tile";
        private const string MetadataExtension = ".json";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly object syncRoot = new object();
        private readonly string cacheDirectory;
        private readonly long maxBytes;
        private readonly bool enabled;

        /// <summary>
        /// 제공자별로 분리된 캐시 폴더와 최대 용량을 정한다. 용량이 0이면 캐시는 꺼진 상태가 된다.
        /// </summary>
        public TileDiskCache(string providerId, int maxMegabytes, bool enabled)
        {
            string safeProviderId = SanitizeProviderId(providerId);
            cacheDirectory = Path.Combine(
                Application.persistentDataPath,
                "PixelRoad",
                "VectorTileCache",
                safeProviderId);

            maxBytes = Math.Max(0L, maxMegabytes) * 1024L * 1024L;
            this.enabled = enabled && maxBytes > 0L;
        }

        /// <summary>
        /// 저장된 타일을 읽는다. 메타데이터나 해시가 어긋나면 파손으로 보고 지운 뒤 실패를 돌려준다.
        /// </summary>
        public bool TryRead(TileKey key, out TileCacheEntry entry)
        {
            entry = null;
            if (!enabled)
            {
                return false;
            }

            lock (syncRoot)
            {
                GetEntryPaths(key, out string dataPath, out string metadataPath);

                try
                {
                    if (!File.Exists(dataPath) || !File.Exists(metadataPath))
                    {
                        DeletePair(dataPath, metadataPath);
                        return false;
                    }

                    CacheMetadata metadata = ReadMetadata(metadataPath);
                    if (!IsValidMetadata(metadata))
                    {
                        DeletePair(dataPath, metadataPath);
                        return false;
                    }

                    byte[] data = File.ReadAllBytes(dataPath);
                    if (data.LongLength != metadata.DataLength ||
                        !string.Equals(ComputeHash(data), metadata.DataHash, StringComparison.Ordinal))
                    {
                        DeletePair(dataPath, metadataPath);
                        return false;
                    }

                    entry = new TileCacheEntry
                    {
                        Data = data,
                        ExpiresUnix = metadata.ExpiresUnix,
                        ETag = metadata.ETag,
                        LastModified = metadata.LastModified
                    };

                    long nowUnix = GetCurrentUnixSeconds();
                    if (metadata.LastAccessUnix != nowUnix)
                    {
                        metadata.LastAccessUnix = nowUnix;
                        TryWriteMetadata(metadataPath, metadata);
                    }

                    return true;
                }
                catch (Exception)
                {
                    DeletePair(dataPath, metadataPath);
                    entry = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// 타일을 임시 파일에 먼저 쓰고 교체하는 방식으로 저장해, 중간에 끊겨도 반쪽 캐시가 남지 않게 한다.
        /// </summary>
        public void Write(TileKey key, TileCacheEntry entry)
        {
            if (!enabled || entry == null || entry.Data == null)
            {
                return;
            }

            lock (syncRoot)
            {
                GetEntryPaths(key, out string dataPath, out string metadataPath);
                Directory.CreateDirectory(cacheDirectory);

                string uniqueSuffix = ".tmp-" + Guid.NewGuid().ToString("N");
                string temporaryDataPath = dataPath + uniqueSuffix;
                string temporaryMetadataPath = metadataPath + uniqueSuffix;

                try
                {
                    CacheMetadata metadata = new CacheMetadata
                    {
                        Version = MetadataVersion,
                        ExpiresUnix = entry.ExpiresUnix,
                        ETag = entry.ETag,
                        LastModified = entry.LastModified,
                        LastAccessUnix = GetCurrentUnixSeconds(),
                        DataLength = entry.Data.LongLength,
                        DataHash = ComputeHash(entry.Data)
                    };

                    File.WriteAllBytes(temporaryDataPath, entry.Data);
                    File.WriteAllText(
                        temporaryMetadataPath,
                        JsonUtility.ToJson(metadata),
                        Utf8WithoutBom);

                    ReplaceWithTemporaryFile(temporaryDataPath, dataPath);
                    ReplaceWithTemporaryFile(temporaryMetadataPath, metadataPath);
                }
                catch (Exception)
                {
                    SafeDelete(temporaryDataPath);
                    SafeDelete(temporaryMetadataPath);
                    return;
                }

                ClearExpiredOrTrimLocked(GetCurrentUnixSeconds());
            }
        }

        /// <summary>현재 시각을 기준으로 만료 정리와 용량 정리를 수행한다.</summary>
        public void ClearExpiredOrTrim()
        {
            ClearExpiredOrTrim(GetCurrentUnixSeconds());
        }

        /// <summary>디코딩에 실패한 타일처럼 더 믿을 수 없는 항목을 캐시에서 제거한다.</summary>
        public void Remove(TileKey key)
        {
            if (!enabled)
            {
                return;
            }

            lock (syncRoot)
            {
                GetEntryPaths(key, out string dataPath, out string metadataPath);
                DeletePair(dataPath, metadataPath);
            }
        }

        /// <summary>기준 시각을 직접 넘겨 정리한다. 테스트에서 만료 시점을 조작하기 위해 열어 둔다.</summary>
        public void ClearExpiredOrTrim(long nowUnixSeconds)
        {
            if (!enabled)
            {
                return;
            }

            lock (syncRoot)
            {
                ClearExpiredOrTrimLocked(nowUnixSeconds);
            }
        }

        /// <summary>
        /// 잠금을 이미 잡은 상태에서 실제 정리를 수행한다. 만료·손상 항목을 지우고,
        /// 그래도 용량을 넘으면 오래 안 쓴 순으로 지운다.
        /// </summary>
        private void ClearExpiredOrTrimLocked(long nowUnixSeconds)
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return;
            }

            DeleteInterruptedWriteFiles();
            DeleteOrphanedDataFiles();

            List<CacheRecord> records = new List<CacheRecord>();
            long totalBytes = 0L;
            string[] metadataPaths;

            try
            {
                metadataPaths = Directory.GetFiles(
                    cacheDirectory,
                    "*" + MetadataExtension,
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < metadataPaths.Length; i++)
            {
                string metadataPath = metadataPaths[i];
                string dataPath = Path.ChangeExtension(metadataPath, DataExtension);

                try
                {
                    CacheMetadata metadata = ReadMetadata(metadataPath);
                    if (!File.Exists(dataPath) || !IsValidMetadata(metadata))
                    {
                        DeletePair(dataPath, metadataPath);
                        continue;
                    }

                    FileInfo dataFile = new FileInfo(dataPath);
                    FileInfo metadataFile = new FileInfo(metadataPath);
                    if (dataFile.Length != metadata.DataLength)
                    {
                        DeletePair(dataPath, metadataPath);
                        continue;
                    }

                    if (metadata.ExpiresUnix > 0L && metadata.ExpiresUnix <= nowUnixSeconds)
                    {
                        DeletePair(dataPath, metadataPath);
                        continue;
                    }

                    long recordBytes = dataFile.Length + metadataFile.Length;
                    totalBytes += recordBytes;
                    records.Add(new CacheRecord
                    {
                        DataPath = dataPath,
                        MetadataPath = metadataPath,
                        LastAccessUnix = metadata.LastAccessUnix,
                        SizeBytes = recordBytes
                    });
                }
                catch (Exception)
                {
                    DeletePair(dataPath, metadataPath);
                }
            }

            if (totalBytes <= maxBytes)
            {
                return;
            }

            records.Sort((left, right) => left.LastAccessUnix.CompareTo(right.LastAccessUnix));
            for (int i = 0; i < records.Count && totalBytes > maxBytes; i++)
            {
                CacheRecord record = records[i];
                DeletePair(record.DataPath, record.MetadataPath);
                totalBytes -= record.SizeBytes;
            }
        }

        /// <summary>이전 실행이 저장 도중 종료되며 남긴 임시 파일을 치운다.</summary>
        private void DeleteInterruptedWriteFiles()
        {
            string[] temporaryPaths;
            try
            {
                temporaryPaths = Directory.GetFiles(
                    cacheDirectory,
                    "*.tmp-*",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < temporaryPaths.Length; i++)
            {
                SafeDelete(temporaryPaths[i]);
            }
        }

        /// <summary>짝이 되는 메타데이터가 없는 본문 파일은 쓸 수 없으므로 지운다.</summary>
        private void DeleteOrphanedDataFiles()
        {
            string[] dataPaths;
            try
            {
                dataPaths = Directory.GetFiles(
                    cacheDirectory,
                    "*" + DataExtension,
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return;
            }

            for (int i = 0; i < dataPaths.Length; i++)
            {
                string metadataPath = Path.ChangeExtension(dataPaths[i], MetadataExtension);
                if (!File.Exists(metadataPath))
                {
                    SafeDelete(dataPaths[i]);
                }
            }
        }

        /// <summary>타일 키를 파일 이름으로 바꿔 본문과 메타데이터 경로를 만든다.</summary>
        private void GetEntryPaths(TileKey key, out string dataPath, out string metadataPath)
        {
            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "z{0}_x{1}_y{2}",
                key.Zoom,
                key.X,
                key.Y);

            dataPath = Path.Combine(cacheDirectory, fileName + DataExtension);
            metadataPath = Path.Combine(cacheDirectory, fileName + MetadataExtension);
        }

        /// <summary>메타데이터 JSON을 읽어 객체로 되돌린다.</summary>
        private static CacheMetadata ReadMetadata(string metadataPath)
        {
            string json = File.ReadAllText(metadataPath, Utf8WithoutBom);
            return JsonUtility.FromJson<CacheMetadata>(json);
        }

        /// <summary>버전이 맞고 필수 값이 채워졌는지 확인해 옛 형식이나 깨진 파일을 걸러낸다.</summary>
        private static bool IsValidMetadata(CacheMetadata metadata)
        {
            return metadata != null &&
                   metadata.Version == MetadataVersion &&
                   metadata.DataLength >= 0L &&
                   !string.IsNullOrEmpty(metadata.DataHash);
        }

        /// <summary>메타데이터만 갱신한다. 실패해도 캐시 본문은 그대로 두고 조용히 넘어간다.</summary>
        private static void TryWriteMetadata(string metadataPath, CacheMetadata metadata)
        {
            string temporaryPath = metadataPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(metadata), Utf8WithoutBom);
                ReplaceWithTemporaryFile(temporaryPath, metadataPath);
            }
            catch (Exception)
            {
                SafeDelete(temporaryPath);
            }
        }

        /// <summary>임시 파일을 최종 경로로 옮겨 저장을 확정한다.</summary>
        private static void ReplaceWithTemporaryFile(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(temporaryPath, destinationPath);
        }

        /// <summary>본문과 메타데이터를 함께 지워 한쪽만 남는 상태를 막는다.</summary>
        private static void DeletePair(string dataPath, string metadataPath)
        {
            SafeDelete(dataPath);
            SafeDelete(metadataPath);
        }

        /// <summary>파일 삭제 실패를 삼킨다. 캐시 정리는 지도 렌더링을 방해하면 안 된다.</summary>
        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A cache cleanup failure must never interrupt map rendering.
            }
        }

        /// <summary>
        /// 제공자 아이디를 폴더 이름으로 쓸 수 있게 정리하고 해시를 붙인다.
        /// 읽기 쉬우면서도 다른 제공자와 충돌하지 않는 이름을 얻기 위함이다.
        /// </summary>
        private static string SanitizeProviderId(string providerId)
        {
            string original = string.IsNullOrWhiteSpace(providerId) ? "default" : providerId.Trim();
            StringBuilder builder = new StringBuilder(Math.Min(original.Length, 40));
            bool previousWasSeparator = false;

            for (int i = 0; i < original.Length && builder.Length < 40; i++)
            {
                char character = original[i];
                bool allowed = (character >= 'a' && character <= 'z') ||
                               (character >= 'A' && character <= 'Z') ||
                               (character >= '0' && character <= '9') ||
                               character == '-' ||
                               character == '_';

                if (allowed)
                {
                    builder.Append(character);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            string readablePart = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(readablePart))
            {
                readablePart = "provider";
            }

            return readablePart + "_" + ComputeHash(Utf8WithoutBom.GetBytes(original));
        }

        /// <summary>파일 손상 검사용 FNV-1a 해시를 계산한다. 보안용이 아니라 무결성 확인용이다.</summary>
        private static string ComputeHash(byte[] data)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;

            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>만료 판정 기준이 되는 현재 UTC 시각을 초 단위로 얻는다.</summary>
        private static long GetCurrentUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 타일 본문과 함께 저장되는 부가 정보. 만료 판정, 재검증, 무결성 검사에 쓰인다.
        /// </summary>
        [Serializable]
        private sealed class CacheMetadata
        {
            public int Version;
            public long ExpiresUnix;
            public string ETag;
            public string LastModified;
            public long LastAccessUnix;
            public long DataLength;
            public string DataHash;
        }

        /// <summary>용량 정리 단계에서 삭제 후보를 오래된 순으로 줄 세우기 위한 임시 항목이다.</summary>
        private sealed class CacheRecord
        {
            public string DataPath;
            public string MetadataPath;
            public long LastAccessUnix;
            public long SizeBytes;
        }
    }
}
