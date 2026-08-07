using System;
using System.Text;

namespace PixelRoad.Mapping
{
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

        public PbfReader CreateReader()
        {
            return new PbfReader(Data, Offset, Length);
        }
    }

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

        public uint ReadUInt32()
        {
            ulong value = ReadVarint();
            if (value > uint.MaxValue)
            {
                throw new FormatException("The PBF value exceeds uint32.");
            }

            return (uint)value;
        }

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

        public float ReadFloat()
        {
            return BitConverter.Int32BitsToSingle(unchecked((int)ReadFixed32()));
        }

        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadFixed64()));
        }

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

        public void SkipField(int fieldNumber, int wireType)
        {
            SkipField(fieldNumber, wireType, 0);
        }

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

        private void Advance(int byteCount)
        {
            EnsureAvailable(byteCount);
            position += byteCount;
        }

        private void EnsureAvailable(int byteCount)
        {
            if (byteCount < 0 || byteCount > end - position)
            {
                throw new FormatException("The PBF message ended unexpectedly.");
            }
        }
    }
}
