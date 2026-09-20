#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{
    /// <summary>Server-confirmed right state. Active is not itself offline proof when an expiry exists.</summary>
    public enum EntitlementRightState { Active = 0, Revoked = 1, Expired = 2, Unknown = 3 }

    /// <summary>What a client may honestly present from a cached projection. It never grants a right.</summary>
    public enum EntitlementOfflineEligibility { Eligible = 0, Ineligible = 1, ExpiryUncertain = 2, Unknown = 3 }

    /// <summary>Non-secret receipt identity retained only as the server-confirmed origin of a right.</summary>
    public sealed class EntitlementOriginReceipt
    {
        private static readonly Regex Source = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);

        public EntitlementOriginReceipt(PlatformId receiptId, string businessSource)
        {
            if (!receiptId.IsValid) throw new ArgumentException("A receipt identity is required.", nameof(receiptId));
            if (string.IsNullOrWhiteSpace(businessSource) || !Source.IsMatch(businessSource)) throw new ArgumentOutOfRangeException(nameof(businessSource));
            ReceiptId = receiptId;
            BusinessSource = businessSource;
        }

        public PlatformId ReceiptId { get; }
        public string BusinessSource { get; }
    }

    /// <summary>One opaque server-confirmed entitlement. Payload is not parsed or made authoritative by this feature.</summary>
    public sealed class EntitlementRight
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private readonly byte[] payload;

        public EntitlementRight(PlatformId entitlementId, long revision, EntitlementRightState state,
            EntitlementOriginReceipt originReceipt, long? expiresAtServerMilliseconds, ReadOnlySpan<byte> payload)
        {
            if (!entitlementId.IsValid) throw new ArgumentException("An entitlement identity is required.", nameof(entitlementId));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(EntitlementRightState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            OriginReceipt = originReceipt ?? throw new ArgumentNullException(nameof(originReceipt));
            if (expiresAtServerMilliseconds.HasValue && (expiresAtServerMilliseconds.Value < 0 || expiresAtServerMilliseconds.Value > 253_402_300_799_999L))
                throw new ArgumentOutOfRangeException(nameof(expiresAtServerMilliseconds));
            if ((state == EntitlementRightState.Expired || state == EntitlementRightState.Revoked) && payload.Length != 0)
                throw new ArgumentException("Revoked and expired rights carry no opaque payload.", nameof(payload));
            if (payload.Length > MaximumPayloadBytes) throw new ArgumentOutOfRangeException(nameof(payload));
            EntitlementId = entitlementId;
            Revision = revision;
            State = state;
            ExpiresAtServerMilliseconds = expiresAtServerMilliseconds;
            this.payload = payload.ToArray();
        }

        public PlatformId EntitlementId { get; }
        public long Revision { get; }
        public EntitlementRightState State { get; }
        public EntitlementOriginReceipt OriginReceipt { get; }
        public long? ExpiresAtServerMilliseconds { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }

    /// <summary>Bounded immutable server projection. Only the server may call a right expired or revoked.</summary>
    public sealed class EntitlementConfirmedSnapshot
    {
        private readonly EntitlementRight[] rights;

        public EntitlementConfirmedSnapshot(long revision, long confirmedAtMilliseconds, IEnumerable<EntitlementRight> rights)
        {
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (rights == null) throw new ArgumentNullException(nameof(rights));
            this.rights = rights.ToArray();
            if (this.rights.Length > 512 || this.rights.Any(value => value == null) || this.rights.Select(value => value.EntitlementId).Distinct().Count() != this.rights.Length)
                throw new ArgumentException("Entitlement projection contains invalid or duplicate rights.", nameof(rights));
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
        }

        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<EntitlementRight> Rights => Array.AsReadOnly(rights);
    }

    public sealed class EntitlementRightView
    {
        public EntitlementRightView(EntitlementRight right, EntitlementOfflineEligibility offlineEligibility)
        {
            Right = right ?? throw new ArgumentNullException(nameof(right));
            if (!Enum.IsDefined(typeof(EntitlementOfflineEligibility), offlineEligibility)) throw new ArgumentOutOfRangeException(nameof(offlineEligibility));
            OfflineEligibility = offlineEligibility;
        }

        public EntitlementRight Right { get; }
        public EntitlementOfflineEligibility OfflineEligibility { get; }
    }

    /// <summary>Presentation projection. No local clock is accepted because device time cannot create authority.</summary>
    public sealed class EntitlementSnapshot
    {
        private readonly EntitlementRightView[] rights;

        public EntitlementSnapshot(EntitlementConfirmedSnapshot confirmed, IEnumerable<EntitlementRightView> rights)
        {
            Confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
            if (rights == null) throw new ArgumentNullException(nameof(rights));
            this.rights = rights.ToArray();
            if (this.rights.Length != confirmed.Rights.Count || this.rights.Any(value => value == null) ||
                this.rights.Select(value => value.Right.EntitlementId).Distinct().Count() != this.rights.Length)
                throw new ArgumentException("Entitlement views do not faithfully describe the confirmed projection.", nameof(rights));
        }

        public EntitlementConfirmedSnapshot Confirmed { get; }
        public IReadOnlyList<EntitlementRightView> Rights => Array.AsReadOnly(rights);
    }

    /// <summary>Feature-owned codec seam; production wire/storage adapters remain separate owners.</summary>
    public interface IEntitlementStateCodec
    {
        byte[] EncodeConfirmed(EntitlementConfirmedSnapshot snapshot);
        EntitlementConfirmedSnapshot DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        void ValidateSupportedExtensions(ReadOnlySpan<byte> extensions);
    }

    public sealed class EntitlementOwnerMismatchException : InvalidOperationException
    {
        public EntitlementOwnerMismatchException() : base("Entitlement data does not belong to the captured account/view generation.") { }
    }

    public sealed class EntitlementProjectionConflictException : InvalidOperationException
    {
        public EntitlementProjectionConflictException(string reason) : base(reason) { }
    }
}
