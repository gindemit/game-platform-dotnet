using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{
    /// <summary>RFC 9562 byte ordering; Guid's mixed-endian array format never crosses this API.</summary>
    public static class UuidIdentity
    {
        public static void WriteRfcBytes(Guid value, Span<byte> destination)
        {
            if (destination.Length < 16) throw new ArgumentException("Sixteen bytes required.", nameof(destination));
            var bytes = value.ToByteArray();
            destination[0] = bytes[3]; destination[1] = bytes[2]; destination[2] = bytes[1]; destination[3] = bytes[0];
            destination[4] = bytes[5]; destination[5] = bytes[4]; destination[6] = bytes[7]; destination[7] = bytes[6];
            bytes.AsSpan(8, 8).CopyTo(destination.Slice(8));
        }

        public static Guid FromRfcBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != 16) throw new ArgumentException("Exactly sixteen bytes required.", nameof(bytes));
            var mixed = bytes.ToArray();
            mixed[0] = bytes[3]; mixed[1] = bytes[2]; mixed[2] = bytes[1]; mixed[3] = bytes[0];
            mixed[4] = bytes[5]; mixed[5] = bytes[4]; mixed[6] = bytes[7]; mixed[7] = bytes[6];
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
            Span<byte> a = stackalloc byte[16]; Span<byte> b = stackalloc byte[16];
            WriteRfcBytes(left, a); WriteRfcBytes(right, b);
            for (int i = 0; i < 16; i++) { int order = a[i].CompareTo(b[i]); if (order != 0) return order; }
            return 0;
        }
    }

    public interface IUnixMillisecondClock { long GetUnixMilliseconds(); }
    public interface IUuidRandomSource { void Fill(Span<byte> destination); }

    public sealed class SystemUnixMillisecondClock : IUnixMillisecondClock
    {
        public long GetUnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public sealed class CryptographicUuidRandomSource : IUuidRandomSource
    {
        public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
    }

    /// <summary>Injected clock and secure entropy. Uses fresh randomness for every ID; no causal ordering guarantee.</summary>
    public sealed class UuidV7Generator
    {
        private readonly IUnixMillisecondClock clock;
        private readonly IUuidRandomSource random;
        private readonly object gate = new object();
        private long lastTimestamp;

        public UuidV7Generator(IUnixMillisecondClock clock, IUuidRandomSource random)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public Guid NewId()
        {
            lock (gate)
            {
                long timestamp = PlatformNumbers.UnixMilliseconds(clock.GetUnixMilliseconds());
                // Clock rollback retains the latest observed time, without claiming cross-process order.
                timestamp = Math.Max(timestamp, lastTimestamp);
                Span<byte> bytes = stackalloc byte[16];
                random.Fill(bytes);
                for (int i = 5; i >= 0; i--) { bytes[i] = (byte)timestamp; timestamp >>= 8; }
                bytes[6] = (byte)((bytes[6] & 15) | 0x70);
                bytes[8] = (byte)((bytes[8] & 63) | 0x80);
                Guid result = UuidIdentity.FromRfcBytes(bytes);
                long observed = 0;
                for (int i = 0; i < 6; i++) observed = (observed << 8) | bytes[i];
                lastTimestamp = observed;
                return result;
            }
        }
    }

    public static class PlatformNumbers
    {
        public const long MaximumUnixMilliseconds = 253402300799999;
        public static long UnixMilliseconds(long value)
        {
            if (value < 0 || value > MaximumUnixMilliseconds) throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
        public static long Add(long value, long delta) => checked(value + delta);
        public static long NextPositiveSequence(long current)
        {
            if (current < 0) throw new ArgumentOutOfRangeException(nameof(current));
            return checked(current + 1);
        }
    }
}
