using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PixelRoad.Mapping
{
    [Serializable]
    public sealed class TileCacheEntry
    {
        public byte[] Data;
        public long ExpiresUnix;
        public string ETag;
        public string LastModified;
    }

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

        public void ClearExpiredOrTrim()
        {
            ClearExpiredOrTrim(GetCurrentUnixSeconds());
        }

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

        private static CacheMetadata ReadMetadata(string metadataPath)
        {
            string json = File.ReadAllText(metadataPath, Utf8WithoutBom);
            return JsonUtility.FromJson<CacheMetadata>(json);
        }

        private static bool IsValidMetadata(CacheMetadata metadata)
        {
            return metadata != null &&
                   metadata.Version == MetadataVersion &&
                   metadata.DataLength >= 0L &&
                   !string.IsNullOrEmpty(metadata.DataHash);
        }

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

        private static void ReplaceWithTemporaryFile(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(temporaryPath, destinationPath);
        }

        private static void DeletePair(string dataPath, string metadataPath)
        {
            SafeDelete(dataPath);
            SafeDelete(metadataPath);
        }

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

        private static long GetCurrentUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

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

        private sealed class CacheRecord
        {
            public string DataPath;
            public string MetadataPath;
            public long LastAccessUnix;
            public long SizeBytes;
        }
    }
}
