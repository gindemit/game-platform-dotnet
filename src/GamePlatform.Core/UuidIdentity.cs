using System;

namespace GamePlatform.Core
{
    /// <summary>RFC 9562 byte ordering; Guid's mixed-endian array format never crosses this API.</summary>
    public static class UuidIdentity
    {
        public static void WriteRfcBytes(Guid value, Span<byte> destination)
        {
            if (destination.Length < 16)
                throw new ArgumentException("Sixteen bytes required.", nameof(destination));
            var bytes = value.ToByteArray();
            destination[0] = bytes[3];
            destination[1] = bytes[2];
            destination[2] = bytes[1];
            destination[3] = bytes[0];
            destination[4] = bytes[5];
            destination[5] = bytes[4];
            destination[6] = bytes[7];
            destination[7] = bytes[6];
            bytes.AsSpan(8, 8).CopyTo(destination.Slice(8));
        }

        public static Guid FromRfcBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != 16)
                throw new ArgumentException("Exactly sixteen bytes required.", nameof(bytes));
            var mixed = bytes.ToArray();
            mixed[0] = bytes[3];
            mixed[1] = bytes[2];
            mixed[2] = bytes[1];
            mixed[3] = bytes[0];
            mixed[4] = bytes[5];
            mixed[5] = bytes[4];
            mixed[6] = bytes[7];
            mixed[7] = bytes[6];
            return new Guid(mixed);
        }

        public static bool IsValid(Guid value, bool allowIssuedVersions = true)
        {
            Span<byte> bytes = stackalloc byte[16];
            WriteRfcBytes(value, bytes);
            int version = bytes[6] >> 4;
            return value != Guid.Empty && (bytes[8] & 0xc0) == 0x80 &&
                (version == 7 || (allowIssuedVersions && version >= 1 && version <= 8));
        }

        public static int Compare(Guid left, Guid right)
        {
            Span<byte> a = stackalloc byte[16];
            Span<byte> b = stackalloc byte[16];
            WriteRfcBytes(left, a);
            WriteRfcBytes(right, b);
            for (int i = 0; i < 16; i++)
            {
                int order = a[i].CompareTo(b[i]);
                if (order != 0)
                    return order;
            }
            return 0;
        }
    }
}
