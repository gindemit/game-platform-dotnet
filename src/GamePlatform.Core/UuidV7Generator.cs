using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{

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
                for (int i = 5; i >= 0; i--)
                {
                    bytes[i] = (byte)timestamp;
                    timestamp >>= 8;
                }
                bytes[6] = (byte)((bytes[6] & 15) | 0x70);
                bytes[8] = (byte)((bytes[8] & 63) | 0x80);
                Guid result = UuidIdentity.FromRfcBytes(bytes);
                long observed = 0;
                for (int i = 0; i < 6; i++)
                    observed = (observed << 8) | bytes[i];
                lastTimestamp = observed;
                return result;
            }
        }
    }
}
