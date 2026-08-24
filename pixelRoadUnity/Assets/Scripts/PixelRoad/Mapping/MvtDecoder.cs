using System;
using System.Collections.Generic;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// MVT 스펙이 정의하는 지오메트리 종류. 숫자 값이 곧 프로토콜 값이라 그대로 캐스팅한다.
    /// </summary>
    public enum MvtGeometryType
    {
        Unknown = 0,
        Point = 1,
        LineString = 2,
        Polygon = 3
    }

    /// <summary>
    /// 타일 로컬 좌표계의 정수 좌표 한 점. Y축은 아래로 증가한다.
    /// </summary>
    public readonly struct MvtPoint
    {
        public int X { get; }
        public int Y { get; }

        public MvtPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// 연속된 점 하나의 묶음. 닫힌 경로는 폴리곤 링, 열린 경로는 선을 뜻한다.
    /// </summary>
    public sealed class MvtPath
    {
        public IReadOnlyList<MvtPoint> Points { get; }
        public bool IsClosed { get; }

        internal MvtPath(List<MvtPoint> points, bool isClosed)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }

    /// <summary>
    /// 디코딩된 지형지물 하나. 태그 인덱스는 이미 키/값으로 풀어 Properties에 담아 둔다.
    /// </summary>
    public sealed class MvtFeature
    {
        public bool HasId { get; }
        public ulong Id { get; }
        public MvtGeometryType GeometryType { get; }
        public IReadOnlyList<uint> Tags { get; }
        public IReadOnlyDictionary<string, object> Properties { get; }
        public IReadOnlyList<MvtPath> Paths { get; }

        internal MvtFeature(
            bool hasId,
            ulong id,
            MvtGeometryType geometryType,
            List<uint> tags,
            Dictionary<string, object> properties,
            List<MvtPath> paths)
        {
            HasId = hasId;
            Id = id;
            GeometryType = geometryType;
            Tags = tags;
            Properties = properties;
            Paths = paths;
        }
    }

    /// <summary>
    /// 이름과 좌표 해상도(Extent)를 공유하는 지형지물 묶음.
    /// </summary>
    public sealed class MvtLayer
    {
        public string Name { get; }
        public uint Version { get; }
        public uint Extent { get; }
        public IReadOnlyList<string> Keys { get; }
        public IReadOnlyList<object> Values { get; }
        public IReadOnlyList<MvtFeature> Features { get; }

        internal MvtLayer(
            string name,
            uint version,
            uint extent,
            List<string> keys,
            List<object> values,
            List<MvtFeature> features)
        {
            Name = name;
            Version = version;
            Extent = extent;
            Keys = keys;
            Values = values;
            Features = features;
        }
    }

    /// <summary>
    /// 타일 한 장에서 디코딩한 레이어 전체를 담는다.
    /// </summary>
    public sealed class MvtTile
    {
        public IReadOnlyList<MvtLayer> Layers { get; }

        internal MvtTile(List<MvtLayer> layers)
        {
            Layers = layers;
        }

        /// <summary>
        /// 이름이 정확히 일치하는 레이어를 찾는다. 없으면 null을 돌려준다.
        /// </summary>
        public MvtLayer FindLayer(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            for (int index = 0; index < Layers.Count; index++)
            {
                MvtLayer layer = Layers[index];
                if (string.Equals(layer.Name, name, StringComparison.Ordinal))
                {
                    return layer;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Mapbox Vector Tile(PBF) 바이트를 해석한다. 손상되거나 악의적인 타일에 대비해
    /// 크기와 개수 상한을 촘촘히 두고 위반하면 FormatException을 던진다.
    /// </summary>
    public static class MvtDecoder
    {
        private const int MaximumTileBytes = 16 * 1024 * 1024;
        private const int MaximumLayerBytes = 8 * 1024 * 1024;
        private const int MaximumFeatureBytes = 4 * 1024 * 1024;
        private const int MaximumValueBytes = 1024 * 1024;
        private const int MaximumLayerNameBytes = 1024;
        private const int MaximumKeyBytes = 16 * 1024;
        private const int MaximumValueStringBytes = 256 * 1024;
        private const int MaximumLayers = 256;
        private const int MaximumFeaturesPerLayer = 200000;
        private const int MaximumFeaturesPerTile = 250000;
        private const int MaximumKeysPerLayer = 65536;
        private const int MaximumValuesPerLayer = 65536;
        private const int MaximumTagIntegersPerFeature = 8192;
        private const int MaximumGeometryIntegersPerFeature = 1000000;
        private const int MaximumGeometryPointsPerFeature = 500000;
        private const int MaximumGeometryPointsPerTile = 2000000;
        private const int MaximumPathsPerFeature = 65536;
        private const int MaximumGeometryOperations = 1000000;
        private const uint MaximumExtent = 1U << 24;
        private const uint DefaultExtent = 4096U;

        /// <summary>
        /// 타일 바이트를 디코딩한다. allowedLayers를 넘기면 해당 레이어만 파싱해 비용을 줄인다.
        /// </summary>
        public static MvtTile Decode(byte[] data, ISet<string> allowedLayers = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length > MaximumTileBytes)
            {
                throw new FormatException("The MVT tile exceeds the 16 MiB safety limit.");
            }

            PbfReader reader = new PbfReader(data);
            List<MvtLayer> layers = new List<MvtLayer>();
            int layerCount = 0;
            int totalFeatureCount = 0;
            int totalPointCount = 0;

            while (reader.TryReadFieldHeader(out int fieldNumber, out int wireType))
            {
                if (fieldNumber != 3)
                {
                    reader.SkipField(fieldNumber, wireType);
                    continue;
                }

                RequireWireType("tile.layers", wireType, 2);
                layerCount++;
                if (layerCount > MaximumLayers)
                {
                    throw new FormatException("The MVT tile contains too many layers.");
                }

                PbfSlice layerSlice = reader.ReadLengthDelimitedSlice(MaximumLayerBytes);
                LayerEnvelope envelope;
                try
                {
                    envelope = ReadLayerEnvelope(layerSlice);
                }
                catch (FormatException exception)
                {
                    throw new FormatException("An MVT layer header is invalid.", exception);
                }

                if (allowedLayers != null && !allowedLayers.Contains(envelope.Name))
                {
                    continue;
                }

                try
                {
                    layers.Add(ParseLayer(layerSlice, envelope, ref totalFeatureCount, ref totalPointCount));
                }
                catch (FormatException exception)
                {
                    throw new FormatException("MVT layer '" + envelope.Name + "' is invalid.", exception);
                }
            }

            return new MvtTile(layers);
        }

        /// <summary>
        /// 레이어를 한 번 훑어 이름과 항목 개수만 먼저 파악한다. 레이어 필터링과 리스트 용량 예약에 쓴다.
        /// </summary>
        private static LayerEnvelope ReadLayerEnvelope(PbfSlice slice)
        {
            PbfReader reader = slice.CreateReader();
            string name = null;
            uint version = 0U;
            uint extent = DefaultExtent;
            bool hasName = false;
            bool hasVersion = false;
            int featureCount = 0;
            int keyCount = 0;
            int valueCount = 0;

            while (reader.TryReadFieldHeader(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1:
                        RequireWireType("layer.name", wireType, 2);
                        name = reader.ReadString(MaximumLayerNameBytes);
                        hasName = true;
                        break;
                    case 2:
                        RequireWireType("layer.features", wireType, 2);
                        reader.ReadLengthDelimitedSlice(MaximumFeatureBytes);
                        featureCount++;
                        if (featureCount > MaximumFeaturesPerLayer)
                        {
                            throw new FormatException("The MVT layer contains too many features.");
                        }

                        break;
                    case 3:
                        RequireWireType("layer.keys", wireType, 2);
                        reader.ReadLengthDelimitedSlice(MaximumKeyBytes);
                        keyCount++;
                        if (keyCount > MaximumKeysPerLayer)
                        {
                            throw new FormatException("The MVT layer contains too many keys.");
                        }

                        break;
                    case 4:
                        RequireWireType("layer.values", wireType, 2);
                        reader.ReadLengthDelimitedSlice(MaximumValueBytes);
                        valueCount++;
                        if (valueCount > MaximumValuesPerLayer)
                        {
                            throw new FormatException("The MVT layer contains too many values.");
                        }

                        break;
                    case 5:
                        RequireWireType("layer.extent", wireType, 0);
                        extent = reader.ReadUInt32();
                        break;
                    case 15:
                        RequireWireType("layer.version", wireType, 0);
                        version = reader.ReadUInt32();
                        hasVersion = true;
                        break;
                    default:
                        reader.SkipField(fieldNumber, wireType);
                        break;
                }
            }

            ValidateLayerHeader(hasName, name, hasVersion, version, extent);
            return new LayerEnvelope(name, version, extent, featureCount, keyCount, valueCount);
        }

        /// <summary>
        /// 레이어를 실제로 파싱해 키/값 테이블과 지형지물을 만든다. 타일 전체 누적량도 함께 검사한다.
        /// </summary>
        private static MvtLayer ParseLayer(
            PbfSlice slice,
            LayerEnvelope envelope,
            ref int totalFeatureCount,
            ref int totalPointCount)
        {
            PbfReader reader = slice.CreateReader();
            string name = null;
            uint version = 0U;
            uint extent = DefaultExtent;
            bool hasName = false;
            bool hasVersion = false;
            List<PbfSlice> featureSlices = new List<PbfSlice>(envelope.FeatureCount);
            List<string> keys = new List<string>(envelope.KeyCount);
            List<object> values = new List<object>(envelope.ValueCount);

            while (reader.TryReadFieldHeader(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1:
                        RequireWireType("layer.name", wireType, 2);
                        name = reader.ReadString(MaximumLayerNameBytes);
                        hasName = true;
                        break;
                    case 2:
                        RequireWireType("layer.features", wireType, 2);
                        featureSlices.Add(reader.ReadLengthDelimitedSlice(MaximumFeatureBytes));
                        break;
                    case 3:
                        RequireWireType("layer.keys", wireType, 2);
                        keys.Add(reader.ReadString(MaximumKeyBytes));
                        break;
                    case 4:
                        RequireWireType("layer.values", wireType, 2);
                        values.Add(ParseValue(reader.ReadLengthDelimitedSlice(MaximumValueBytes)));
                        break;
                    case 5:
                        RequireWireType("layer.extent", wireType, 0);
                        extent = reader.ReadUInt32();
                        break;
                    case 15:
                        RequireWireType("layer.version", wireType, 0);
                        version = reader.ReadUInt32();
                        hasVersion = true;
                        break;
                    default:
                        reader.SkipField(fieldNumber, wireType);
                        break;
                }
            }

            ValidateLayerHeader(hasName, name, hasVersion, version, extent);
            List<MvtFeature> features = new List<MvtFeature>(featureSlices.Count);
            for (int index = 0; index < featureSlices.Count; index++)
            {
                totalFeatureCount++;
                if (totalFeatureCount > MaximumFeaturesPerTile)
                {
                    throw new FormatException("The MVT tile contains too many features.");
                }

                features.Add(ParseFeature(featureSlices[index], keys, values, ref totalPointCount));
            }

            return new MvtLayer(name, version, extent, keys, values, features);
        }

        /// <summary>
        /// 값 메시지를 해석해 C# 값으로 돌려준다. 값 필드를 두 개 이상 채운 메시지는 오류로 본다.
        /// </summary>
        private static object ParseValue(PbfSlice slice)
        {
            PbfReader reader = slice.CreateReader();
            object value = null;
            bool hasValue = false;

            while (reader.TryReadFieldHeader(out int fieldNumber, out int wireType))
            {
                object nextValue;
                switch (fieldNumber)
                {
                    case 1:
                        RequireWireType("value.string_value", wireType, 2);
                        nextValue = reader.ReadString(MaximumValueStringBytes);
                        break;
                    case 2:
                        RequireWireType("value.float_value", wireType, 5);
                        nextValue = reader.ReadFloat();
                        break;
                    case 3:
                        RequireWireType("value.double_value", wireType, 1);
                        nextValue = reader.ReadDouble();
                        break;
                    case 4:
                        RequireWireType("value.int_value", wireType, 0);
                        nextValue = unchecked((long)reader.ReadVarint());
                        break;
                    case 5:
                        RequireWireType("value.uint_value", wireType, 0);
                        nextValue = reader.ReadVarint();
                        break;
                    case 6:
                        RequireWireType("value.sint_value", wireType, 0);
                        nextValue = DecodeZigZag64(reader.ReadVarint());
                        break;
                    case 7:
                        RequireWireType("value.bool_value", wireType, 0);
                        nextValue = reader.ReadVarint() != 0UL;
                        break;
                    default:
                        reader.SkipField(fieldNumber, wireType);
                        continue;
                }

                if (hasValue)
                {
                    throw new FormatException("An MVT value message sets more than one value field.");
                }

                value = nextValue;
                hasValue = true;
            }

            return value;
        }

        /// <summary>
        /// 지형지물 하나를 파싱한다. 태그 인덱스를 레이어 테이블로 풀어 속성 사전을 만들고 지오메트리를 경로로 바꾼다.
        /// </summary>
        private static MvtFeature ParseFeature(
            PbfSlice slice,
            List<string> keys,
            List<object> values,
            ref int totalPointCount)
        {
            PbfReader reader = slice.CreateReader();
            bool hasId = false;
            ulong id = 0UL;
            MvtGeometryType geometryType = MvtGeometryType.Unknown;
            List<uint> tags = null;
            List<uint> geometry = null;

            while (reader.TryReadFieldHeader(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1:
                        RequireWireType("feature.id", wireType, 0);
                        id = reader.ReadVarint();
                        hasId = true;
                        break;
                    case 2:
                        ReadRepeatedUInt32(
                            ref reader,
                            wireType,
                            ref tags,
                            MaximumTagIntegersPerFeature,
                            "feature.tags");
                        break;
                    case 3:
                        RequireWireType("feature.type", wireType, 0);
                        uint rawType = reader.ReadUInt32();
                        if (rawType > (uint)MvtGeometryType.Polygon)
                        {
                            throw new FormatException("The MVT feature geometry type is invalid.");
                        }

                        geometryType = (MvtGeometryType)rawType;
                        break;
                    case 4:
                        ReadRepeatedUInt32(
                            ref reader,
                            wireType,
                            ref geometry,
                            MaximumGeometryIntegersPerFeature,
                            "feature.geometry");
                        break;
                    default:
                        reader.SkipField(fieldNumber, wireType);
                        break;
                }
            }

            if (tags == null)
            {
                tags = new List<uint>(0);
            }

            if ((tags.Count & 1) != 0)
            {
                throw new FormatException("The MVT feature tag array has an odd number of indices.");
            }

            Dictionary<string, object> properties = new Dictionary<string, object>(tags.Count / 2, StringComparer.Ordinal);
            for (int index = 0; index < tags.Count; index += 2)
            {
                uint keyIndex = tags[index];
                uint valueIndex = tags[index + 1];
                if (keyIndex >= keys.Count || valueIndex >= values.Count)
                {
                    throw new FormatException("The MVT feature references a tag table index that does not exist.");
                }

                properties[keys[(int)keyIndex]] = values[(int)valueIndex];
            }

            List<MvtPath> paths = DecodeGeometry(geometry, geometryType, ref totalPointCount);
            return new MvtFeature(hasId, id, geometryType, tags, properties, paths);
        }

        /// <summary>
        /// 반복 필드를 읽는다. 패킹된 형태와 낱개 varint 형태를 모두 받아들인다.
        /// </summary>
        private static void ReadRepeatedUInt32(
            ref PbfReader reader,
            int wireType,
            ref List<uint> values,
            int maximumCount,
            string fieldName)
        {
            if (values == null)
            {
                values = new List<uint>();
            }

            if (wireType == 0)
            {
                AddRepeatedValue(values, reader.ReadUInt32(), maximumCount, fieldName);
                return;
            }

            if (wireType != 2)
            {
                throw new FormatException(fieldName + " has the wrong PBF wire type.");
            }

            PbfReader packedReader = reader.ReadLengthDelimitedSlice(MaximumFeatureBytes).CreateReader();
            while (!packedReader.IsAtEnd)
            {
                AddRepeatedValue(values, packedReader.ReadUInt32(), maximumCount, fieldName);
            }
        }

        /// <summary>
        /// 개수 상한을 확인한 뒤 값을 추가한다.
        /// </summary>
        private static void AddRepeatedValue(List<uint> values, uint value, int maximumCount, string fieldName)
        {
            if (values.Count >= maximumCount)
            {
                throw new FormatException(fieldName + " exceeds its configured item limit.");
            }

            values.Add(value);
        }

        /// <summary>
        /// MoveTo/LineTo/ClosePath 명령 스트림을 실제 좌표 경로 목록으로 풀어낸다.
        /// </summary>
        private static List<MvtPath> DecodeGeometry(
            List<uint> geometry,
            MvtGeometryType geometryType,
            ref int totalPointCount)
        {
            List<MvtPath> paths = new List<MvtPath>();
            if (geometry == null || geometry.Count == 0)
            {
                return paths;
            }

            if (geometryType == MvtGeometryType.Unknown)
            {
                throw new FormatException("An MVT feature with unknown geometry type contains geometry commands.");
            }

            int index = 0;
            int featurePointCount = 0;
            int operationCount = 0;
            long cursorX = 0L;
            long cursorY = 0L;
            List<MvtPoint> currentPath = null;

            while (index < geometry.Count)
            {
                uint commandInteger = geometry[index++];
                int commandId = (int)(commandInteger & 7U);
                int repeatCount = (int)(commandInteger >> 3);
                if (repeatCount <= 0)
                {
                    throw new FormatException("An MVT geometry command has a zero repeat count.");
                }

                if (repeatCount > MaximumGeometryOperations - operationCount)
                {
                    throw new FormatException("The MVT geometry exceeds its operation limit.");
                }

                operationCount += repeatCount;
                switch (commandId)
                {
                    case 1:
                        EnsureCoordinateParameters(geometry, index, repeatCount);
                        if (geometryType == MvtGeometryType.Point)
                        {
                            List<MvtPoint> pointPath = new List<MvtPoint>(repeatCount);
                            for (int pointIndex = 0; pointIndex < repeatCount; pointIndex++)
                            {
                                pointPath.Add(ReadGeometryPoint(
                                    geometry,
                                    ref index,
                                    ref cursorX,
                                    ref cursorY,
                                    ref featurePointCount,
                                    ref totalPointCount));
                            }

                            AddPath(paths, new MvtPath(pointPath, false));
                            break;
                        }

                        if (repeatCount != 1)
                        {
                            throw new FormatException("Line and polygon MoveTo commands must have a repeat count of one.");
                        }

                        if (geometryType == MvtGeometryType.LineString && currentPath != null)
                        {
                            FinishLinePath(paths, ref currentPath);
                        }
                        else if (geometryType == MvtGeometryType.Polygon && currentPath != null)
                        {
                            throw new FormatException("An MVT polygon ring is missing ClosePath.");
                        }

                        currentPath = new List<MvtPoint>(8)
                        {
                            ReadGeometryPoint(
                                geometry,
                                ref index,
                                ref cursorX,
                                ref cursorY,
                                ref featurePointCount,
                                ref totalPointCount)
                        };
                        break;
                    case 2:
                        if (geometryType == MvtGeometryType.Point || currentPath == null)
                        {
                            throw new FormatException("An MVT LineTo command does not follow a valid MoveTo command.");
                        }

                        EnsureCoordinateParameters(geometry, index, repeatCount);
                        for (int pointIndex = 0; pointIndex < repeatCount; pointIndex++)
                        {
                            currentPath.Add(ReadGeometryPoint(
                                geometry,
                                ref index,
                                ref cursorX,
                                ref cursorY,
                                ref featurePointCount,
                                ref totalPointCount));
                        }

                        break;
                    case 7:
                        if (geometryType != MvtGeometryType.Polygon || currentPath == null || repeatCount != 1)
                        {
                            throw new FormatException("An MVT ClosePath command is invalid for this geometry.");
                        }

                        if (currentPath.Count < 3)
                        {
                            throw new FormatException("An MVT polygon ring contains fewer than three points.");
                        }

                        AddPath(paths, new MvtPath(currentPath, true));
                        currentPath = null;
                        break;
                    default:
                        throw new FormatException("The MVT geometry contains an unknown command identifier.");
                }
            }

            if (geometryType == MvtGeometryType.LineString && currentPath != null)
            {
                FinishLinePath(paths, ref currentPath);
            }
            else if (geometryType == MvtGeometryType.Polygon && currentPath != null)
            {
                throw new FormatException("An MVT polygon ring is missing its final ClosePath command.");
            }

            return paths;
        }

        /// <summary>
        /// 지그재그 델타 한 쌍을 읽어 커서를 옮기고 절대 좌표 한 점을 만든다.
        /// </summary>
        private static MvtPoint ReadGeometryPoint(
            List<uint> geometry,
            ref int index,
            ref long cursorX,
            ref long cursorY,
            ref int featurePointCount,
            ref int totalPointCount)
        {
            int deltaX = DecodeZigZag32(geometry[index++]);
            int deltaY = DecodeZigZag32(geometry[index++]);
            cursorX += deltaX;
            cursorY += deltaY;
            if (cursorX < int.MinValue || cursorX > int.MaxValue || cursorY < int.MinValue || cursorY > int.MaxValue)
            {
                throw new FormatException("An MVT geometry coordinate exceeds the int32 range.");
            }

            featurePointCount++;
            totalPointCount++;
            if (featurePointCount > MaximumGeometryPointsPerFeature || totalPointCount > MaximumGeometryPointsPerTile)
            {
                throw new FormatException("The MVT geometry exceeds its point limit.");
            }

            return new MvtPoint((int)cursorX, (int)cursorY);
        }

        /// <summary>
        /// 명령이 요구하는 좌표 쌍이 남은 데이터에 실제로 있는지 미리 확인한다.
        /// </summary>
        private static void EnsureCoordinateParameters(List<uint> geometry, int index, int repeatCount)
        {
            int remaining = geometry.Count - index;
            if (repeatCount > remaining / 2)
            {
                throw new FormatException("An MVT geometry command is missing coordinate parameters.");
            }
        }

        /// <summary>
        /// 쌓아 두던 선 경로를 마감해 결과 목록에 넣고 커서를 비운다.
        /// </summary>
        private static void FinishLinePath(List<MvtPath> paths, ref List<MvtPoint> currentPath)
        {
            if (currentPath.Count < 2)
            {
                throw new FormatException("An MVT line contains fewer than two points.");
            }

            AddPath(paths, new MvtPath(currentPath, false));
            currentPath = null;
        }

        /// <summary>
        /// 경로 개수 상한을 확인한 뒤 경로를 추가한다.
        /// </summary>
        private static void AddPath(List<MvtPath> paths, MvtPath path)
        {
            if (paths.Count >= MaximumPathsPerFeature)
            {
                throw new FormatException("The MVT feature contains too many paths.");
            }

            paths.Add(path);
        }

        /// <summary>
        /// 레이어 헤더가 지원 범위인지 본다. 이름이 있어야 하고 버전은 2, Extent는 유효한 값이어야 한다.
        /// </summary>
        private static void ValidateLayerHeader(
            bool hasName,
            string name,
            bool hasVersion,
            uint version,
            uint extent)
        {
            if (!hasName || name == null)
            {
                throw new FormatException("The MVT layer is missing its required name.");
            }

            if (!hasVersion || version != 2U)
            {
                throw new FormatException("Only MVT 2.x layers are supported.");
            }

            if (extent == 0U || extent > MaximumExtent)
            {
                throw new FormatException("The MVT layer extent is outside the supported range.");
            }
        }

        /// <summary>
        /// 필드의 와이어 타입이 스펙과 다르면 오류를 던져 잘못된 해석을 막는다.
        /// </summary>
        private static void RequireWireType(string fieldName, int actualWireType, int expectedWireType)
        {
            if (actualWireType != expectedWireType)
            {
                throw new FormatException(fieldName + " has the wrong PBF wire type.");
            }
        }

        /// <summary>
        /// 지그재그로 인코딩된 32비트 값을 부호 있는 정수로 되돌린다.
        /// </summary>
        private static int DecodeZigZag32(uint value)
        {
            return (int)(value >> 1) ^ -((int)value & 1);
        }

        /// <summary>
        /// 지그재그로 인코딩된 64비트 값을 부호 있는 정수로 되돌린다.
        /// </summary>
        private static long DecodeZigZag64(ulong value)
        {
            return (long)(value >> 1) ^ -((long)value & 1L);
        }

        /// <summary>
        /// 사전 스캔으로 얻은 레이어 헤더 값과 항목 개수를 모아 둔다.
        /// </summary>
        private readonly struct LayerEnvelope
        {
            public readonly string Name;
            public readonly uint Version;
            public readonly uint Extent;
            public readonly int FeatureCount;
            public readonly int KeyCount;
            public readonly int ValueCount;

            public LayerEnvelope(
                string name,
                uint version,
                uint extent,
                int featureCount,
                int keyCount,
                int valueCount)
            {
                Name = name;
                Version = version;
                Extent = extent;
                FeatureCount = featureCount;
                KeyCount = keyCount;
                ValueCount = valueCount;
            }
        }
    }
}
