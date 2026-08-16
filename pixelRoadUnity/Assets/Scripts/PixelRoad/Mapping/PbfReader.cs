using System;
using System.Text;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 원본 버퍼의 일부 구간을 복사 없이 가리킨다. 중첩 메시지를 다시 읽을 때 쓴다.
    /// </summary>
    internal readonly struct PbfSlice
    {
        public readonly byte[] Data;
        public readonly int Offset;
        public readonly int Length;

        public PbfSlice(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// 이 구간만 읽는 독립적인 리더를 만든다.
        /// </summary>
        public PbfReader CreateReader()
        {
            return new PbfReader(Data, Offset, Length);
        }
    }

    /// <summary>
    /// Protocol Buffers 와이어 포맷을 앞에서부터 훑어 읽는 최소 리더.
    /// 범위를 벗어나거나 형식이 깨진 입력은 모두 FormatException으로 막는다.
    /// </summary>
    internal struct PbfReader
    {
        private const int MaximumFieldNumber = 536870911;
        private const int MaximumGroupDepth = 16;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly byte[] data;
        private readonly int end;
        private int position;

        public PbfReader(byte[] data)
            : this(data, 0, data == null ? 0 : data.Length)
        {
        }

        /// <summary>
        /// 버퍼의 지정 구간만 읽도록 리더를 만든다. 구간이 버퍼를 벗어나면 예외를 던진다.
        /// </summary>
        public PbfReader(byte[] data, int offset, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || length < 0 || offset > data.Length - length)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The PBF slice is outside the source buffer.");
            }

            this.data = data;
            position = offset;
            end = offset + length;
        }

        public bool IsAtEnd
        {
            get { return position >= end; }
        }

        public int Remaining
        {
            get { return end - position; }
        }

        /// <summary>
        /// 다음 필드의 번호와 와이어 타입을 읽는다. 구간 끝에 닿으면 false를 돌려준다.
        /// </summary>
        public bool TryReadFieldHeader(out int fieldNumber, out int wireType)
        {
            if (IsAtEnd)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            ulong header = ReadVarint();
            ulong rawFieldNumber = header >> 3;
            wireType = (int)(header & 7UL);
            if (rawFieldNumber == 0UL || rawFieldNumber > MaximumFieldNumber)
            {
                throw new FormatException("The PBF field number is invalid.");
            }

            if (wireType < 0 || wireType > 5)
            {
                throw new FormatException("The PBF wire type is invalid.");
            }

            fieldNumber = (int)rawFieldNumber;
            return true;
        }

        /// <summary>
        /// 가변 길이 정수를 읽는다. 64비트를 넘기는 길이는 손상된 입력으로 보고 거부한다.
        /// </summary>
        public ulong ReadVarint()
        {
            ulong value = 0UL;
            for (int byteIndex = 0; byteIndex < 10; byteIndex++)
            {
                EnsureAvailable(1);
                byte next = data[position++];
                if (byteIndex == 9 && (next & 0xFE) != 0)
                {
                    throw new FormatException("The PBF varint exceeds 64 bits.");
                }

                value |= (ulong)(next & 0x7F) << (byteIndex * 7);
                if ((next & 0x80) == 0)
                {
                    return value;
                }
            }

            throw new FormatException("The PBF varint is too long.");
        }

        /// <summary>
        /// varint를 읽되 uint32 범위를 넘으면 오류로 처리한다.
        /// </summary>
        public uint ReadUInt32()
        {
            ulong value = ReadVarint();
            if (value > uint.MaxValue)
            {
                throw new FormatException("The PBF value exceeds uint32.");
            }

            return (uint)value;
        }

        /// <summary>
        /// 리틀 엔디언 고정 4바이트를 읽는다.
        /// </summary>
        public uint ReadFixed32()
        {
            EnsureAvailable(4);
            uint value = data[position]
                         | ((uint)data[position + 1] << 8)
                         | ((uint)data[position + 2] << 16)
                         | ((uint)data[position + 3] << 24);
            position += 4;
            return value;
        }

        /// <summary>
        /// 리틀 엔디언 고정 8바이트를 읽는다.
        /// </summary>
        public ulong ReadFixed64()
        {
            EnsureAvailable(8);
            ulong value = data[position]
                          | ((ulong)data[position + 1] << 8)
                          | ((ulong)data[position + 2] << 16)
                          | ((ulong)data[position + 3] << 24)
                          | ((ulong)data[position + 4] << 32)
                          | ((ulong)data[position + 5] << 40)
                          | ((ulong)data[position + 6] << 48)
                          | ((ulong)data[position + 7] << 56);
            position += 8;
            return value;
        }

        /// <summary>
        /// 고정 4바이트를 float 비트로 해석한다.
        /// </summary>
        public float ReadFloat()
        {
            return BitConverter.Int32BitsToSingle(unchecked((int)ReadFixed32()));
        }

        /// <summary>
        /// 고정 8바이트를 double 비트로 해석한다.
        /// </summary>
        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadFixed64()));
        }

        /// <summary>
        /// 길이 접두 필드를 건너뛰며 그 본문 구간을 돌려준다. 상한을 넘는 길이는 거부한다.
        /// </summary>
        public PbfSlice ReadLengthDelimitedSlice(int maximumLength)
        {
            int length = ReadLength();
            if (length > maximumLength)
            {
                throw new FormatException("The length-delimited PBF field exceeds its configured limit.");
            }

            PbfSlice slice = new PbfSlice(data, position, length);
            position += length;
            return slice;
        }

        /// <summary>
        /// 길이 접두 구간을 엄격한 UTF-8로 디코딩한다. 잘못된 바이트열은 오류로 본다.
        /// </summary>
        public string ReadString(int maximumByteLength)
        {
            PbfSlice slice = ReadLengthDelimitedSlice(maximumByteLength);
            try
            {
                return StrictUtf8.GetString(slice.Data, slice.Offset, slice.Length);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException("The PBF string is not valid UTF-8.", exception);
            }
        }

        /// <summary>
        /// 관심 없는 필드를 통째로 건너뛴다.
        /// </summary>
        public void SkipField(int fieldNumber, int wireType)
        {
            SkipField(fieldNumber, wireType, 0);
        }

        /// <summary>
        /// 와이어 타입별로 필드를 건너뛴다. 폐기된 group 형식도 처리하되 중첩 깊이를 제한한다.
        /// </summary>
        private void SkipField(int fieldNumber, int wireType, int groupDepth)
        {
            switch (wireType)
            {
                case 0:
                    ReadVarint();
                    return;
                case 1:
                    Advance(8);
                    return;
                case 2:
                    Advance(ReadLength());
                    return;
                case 3:
                    if (groupDepth >= MaximumGroupDepth)
                    {
                        throw new FormatException("The PBF group nesting is too deep.");
                    }

                    while (TryReadFieldHeader(out int nestedFieldNumber, out int nestedWireType))
                    {
                        if (nestedWireType == 4)
                        {
                            if (nestedFieldNumber != fieldNumber)
                            {
                                throw new FormatException("The PBF group has a mismatched end marker.");
                            }

                            return;
                        }

                        SkipField(nestedFieldNumber, nestedWireType, groupDepth + 1);
                    }

                    throw new FormatException("The PBF group is not terminated.");
                case 4:
                    throw new FormatException("An unexpected PBF end-group marker was found.");
                case 5:
                    Advance(4);
                    return;
                default:
                    throw new FormatException("The PBF wire type is invalid.");
            }
        }

        /// <summary>
        /// 길이 접두값을 읽고 그만큼의 바이트가 실제로 남아 있는지까지 확인한다.
        /// </summary>
        private int ReadLength()
        {
            ulong rawLength = ReadVarint();
            if (rawLength > int.MaxValue)
            {
                throw new FormatException("The PBF field length exceeds the supported range.");
            }

            int length = (int)rawLength;
            EnsureAvailable(length);
            return length;
        }

        /// <summary>
        /// 읽기 위치를 지정한 바이트만큼 앞으로 옮긴다.
        /// </summary>
        private void Advance(int byteCount)
        {
            EnsureAvailable(byteCount);
            position += byteCount;
        }

        /// <summary>
        /// 요청한 만큼 읽을 여유가 없으면 오류를 던져 버퍼 밖 접근을 막는다.
        /// </summary>
        private void EnsureAvailable(int byteCount)
        {
            if (byteCount < 0 || byteCount > end - position)
            {
                throw new FormatException("The PBF message ended unexpectedly.");
            }
        }
    }
}
