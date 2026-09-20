using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Production adapter for the frozen gsc1 semantic command fingerprint.</summary>
    public sealed class CanonicalCommandFingerprint : ICommandFingerprint
    {
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();

        public int GetFingerprintLength(int fingerprintVersion) => fingerprintVersion == 1 ? 32 : 0;

        public void Compute(OwnerScope owner, OperationId operationId, ClientStreamId streamId, Guid installationId,
            long sequence, string operationKind, int schemaVersion, int fingerprintVersion, long clientCreatedAt,
            ReadOnlySpan<byte> semanticBody, Span<byte> destination)
        {
            if (!owner.IsValid || !operationId.IsValid || !streamId.IsValid || !UuidIdentity.IsValid(installationId))
                throw new ArgumentException("A complete command owner and identity are required.");
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (schemaVersion != 1 || fingerprintVersion != 1) throw new NotSupportedException("The command fingerprint version is unsupported.");
            PlatformNumbers.UnixMilliseconds(clientCreatedAt);
            if (destination.Length != 32) throw new ArgumentException("A SHA-256 destination is required.", nameof(destination));

            var payload = codec.Decode(PayloadSchema(operationKind), semanticBody.ToArray());
            var command = QualificationValue.Object(new[]
            {
                Pair("backendNamespace", QualificationValue.String(owner.Backend.Value)),
                Pair("authenticatedAppId", QualificationValue.String(owner.AppId.ToString())),
                Pair("authenticatedPlatformUserId", QualificationValue.String(owner.UserId.ToString())),
                Pair("clientStreamId", QualificationValue.String(streamId.ToString())),
                Pair("operationId", QualificationValue.String(operationId.ToString())),
                Pair("installationId", QualificationValue.String(installationId.ToString("D"))),
                Pair("sequence", QualificationValue.String(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                Pair("type", QualificationValue.String(operationKind)),
                Pair("schemaVersion", QualificationValue.Integer(schemaVersion)),
                Pair("clientCreatedAt", QualificationValue.Integer(clientCreatedAt)),
                Pair("payload", payload)
            });
            var canonical = codec.CanonicalBytes(command);
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(canonical);
                digest.AsSpan().CopyTo(destination);
            }
        }

        private static string PayloadSchema(string operationKind)
        {
            if (string.Equals(operationKind, "profile.patch", StringComparison.Ordinal)) return "profile.schema.json#/$defs/patchCommand";
            if (string.Equals(operationKind, "gameplay.session.completed", StringComparison.Ordinal)) return "gameplay.schema.json#/$defs/completionCommand";
            throw new NotSupportedException("The command kind has no frozen fingerprint mapping.");
        }

        private static KeyValuePair<string, QualificationValue> Pair(string key, QualificationValue value) =>
            new KeyValuePair<string, QualificationValue>(key, value);
    }
}
