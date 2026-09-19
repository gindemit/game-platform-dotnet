using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Serialization
{
    public sealed class TypedCodecTests
    {
        [Fact]
        public void TypedMappingReservesRetainedCopiesBeforeMaterialization()
        {
            var codec = new TypedQualificationCodec();
            var id = Guid.Parse("019952d1-0000-7000-8000-000000000003");
            var metrics = Enumerable.Range(0, 40).ToDictionary(i => "metric" + i, i => (long)i);
            var gameplay = new GameplayCompletionCommand("level", 1, "mode", "normal", true, 0,
                new GameplayValidation("none", ""), default, "session", 0, 1, metrics);
            var operation = new PushOperation(id, id, 1, "gameplay.session.completed", 0, gameplay);
            var operationTree = codec.ToDiagnostic(operation);
            var tree = QualificationValue.Object(new Dictionary<string, QualificationValue>
            {
                ["protocolVersion"] = QualificationValue.Integer(1),
                ["clientStreamId"] = QualificationValue.String(id.ToString("D")),
                ["operations"] = QualificationValue.Array(Enumerable.Repeat(operationTree, 64))
            });
            new QualificationMessagePackCodec().ValidateDiagnostic("push.schema.json#/$defs/request", tree);
            Assert.Throws<QualificationCodecException>(() => codec.FromDiagnostic<PushRequest>(tree));
            Assert.Throws<QualificationCodecException>(() => codec.ToDiagnostic(new PushRequest(id, Enumerable.Repeat(operation, 64).ToArray())));
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(253402300799999L)]
        public void NullableEntitlementTimestampUsesIntegerRepresentation(long expiresAt)
        {
            var codec = new TypedQualificationCodec();
            var value = new ProjectionEntitlementData(true, expiresAt);
            var diagnostic = codec.ToDiagnostic(value);
            Assert.Equal(QualificationValueKind.Integer, diagnostic.Properties["expiresAt"].Kind);
            Assert.Equal(expiresAt, codec.Decode<ProjectionEntitlementData>(codec.Encode(value)).ExpiresAt);
        }

        [Fact]
        public void TypedPatchPreservesMaximumRevisionAndExplicitNull()
        {
            var codec = new TypedQualificationCodec();
            var original = new ProfilePatchCommand(long.MaxValue, default, WireOptional<string?>.Present(null), default);
            var decoded = codec.Decode<ProfilePatchCommand>(codec.Encode(original));
            Assert.Equal(long.MaxValue, decoded.ExpectedRevision);
            Assert.True(decoded.AvatarKey.IsPresent);
            Assert.Null(decoded.AvatarKey.Value);
            Assert.False(decoded.DisplayName.IsPresent);
        }

        [Fact]
        public void TypedCarrierStillRequiresWireAdmissionValidation()
        {
            var codec = new TypedQualificationCodec();
            var invalid = new ProfilePatchCommand(-1, WireOptional<string>.Present("name"), default, default);
            Assert.Throws<QualificationCodecException>(() => codec.Encode(invalid));
        }

        private sealed class UnsupportedPayload : IPushPayload { }

        [Fact]
        public void ArbitraryUnionImplementationsFailBeforeEncoding()
        {
            var codec = new TypedQualificationCodec();
            var operation = new PushOperation(Guid.Parse("019952d1-0000-7000-8000-000000000003"),
                Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), 1, "profile.patch", 0, new UnsupportedPayload());
            Assert.Throws<QualificationCodecException>(() => codec.Encode(operation));
        }

        [Fact]
        public void RecursiveExtensionsFailClosedAtMappingBoundary()
        {
            ExtensionValue value = ExtensionValue.Null;
            for (int i = 0; i < 40; i++) value = ExtensionValue.FromArray(new[] { value });
            var extension = new CommonExtension("test", 1, new Dictionary<string, ExtensionValue> { ["deep"] = value });
            Assert.Throws<QualificationCodecException>(() => new TypedQualificationCodec().ToDiagnostic(extension));
        }
    }
}
