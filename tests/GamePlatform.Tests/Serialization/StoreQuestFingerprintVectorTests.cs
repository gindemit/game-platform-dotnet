using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Tests.Serialization;

public sealed class StoreQuestFingerprintVectorTests
{
    private static readonly JsonDocument Vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "contracts/a06/fingerprint-vectors.json")));
    private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();

    [Theory]
    [InlineData("purchase_text")]
    [InlineData("purchase_messagepack_bin16")]
    [InlineData("purchase_key_changed")]
    [InlineData("claim_text")]
    public void CanonicalBytesMatchTheApprovedKnownAnswer(string name)
    {
        var vector = Vector(name);

        var canonical = codec.CanonicalBytes(Scoped(vector, Operation(vector)), QualificationMessagePackCodec.StoreQuestOperationSchema);

        Assert.Equal(vector.GetProperty("canonicalHex").GetString(), Convert.ToHexString(canonical).ToLowerInvariant());
        Assert.Equal(vector.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant());
    }

    [Theory]
    [InlineData("purchase_text")]
    [InlineData("purchase_messagepack_bin16")]
    [InlineData("purchase_key_changed")]
    [InlineData("claim_text")]
    public void ProductionFingerprintMatchesTheApprovedDigest(string name)
    {
        var vector = Vector(name);
        var operation = Operation(vector).Properties;
        var scope = vector.GetProperty("scope");
        var type = operation["type"].StringValue;
        var body = codec.Encode(type == "quest.claim" ? "a06/store-quest.schema.json#/$defs/claimCommand" : "a06/store-quest.schema.json#/$defs/purchaseCommand", operation["payload"]);
        var destination = new byte[32];

        new CanonicalCommandFingerprint(true).Compute(
            new OwnerScope(new BackendNamespace(scope.GetProperty("backendNamespace").GetString()!), new AppId(Guid.Parse(scope.GetProperty("authenticatedAppId").GetString()!)),
                new PlatformUserId(Guid.Parse(scope.GetProperty("authenticatedPlatformUserId").GetString()!))),
            new OperationId(Guid.Parse(operation["operationId"].StringValue)), new ClientStreamId(Guid.Parse(scope.GetProperty("clientStreamId").GetString()!)),
            Guid.Parse(operation["installationId"].StringValue), long.Parse(operation["sequence"].StringValue, CultureInfo.InvariantCulture), type, 1, 1,
            operation["clientCreatedAt"].IntegerValue, body, destination);

        Assert.Equal(vector.GetProperty("sha256").GetString(), Convert.ToHexString(destination).ToLowerInvariant());
    }

    [Fact]
    public void DeclaredPairsCompareAsApproved()
    {
        foreach (var pair in Vectors.RootElement.GetProperty("pairs").EnumerateArray())
        {
            var a = Vector(pair.GetProperty("a").GetString()!).GetProperty("sha256").GetString();
            var b = Vector(pair.GetProperty("b").GetString()!).GetProperty("sha256").GetString();
            Assert.Equal(pair.GetProperty("equal").GetBoolean(), a == b);
        }
    }

    [Fact]
    public void UppercasePurchaseKeyTextIsRejectedBeforeHashing()
    {
        var vector = Vector("purchase_uppercase_key_rejected");

        Assert.Throws<QualificationCodecException>(() => codec.CanonicalBytes(Scoped(vector, Operation(vector)), QualificationMessagePackCodec.StoreQuestOperationSchema));
    }

    [Fact]
    public void FifteenBytePurchaseKeyIsRejectedBeforeHashing()
    {
        var vector = Vector("purchase_bin15_key_rejected");

        Assert.Throws<QualificationCodecException>(() => Operation(vector));
    }

    [Fact]
    public void StoreQuestKindsFailClosedWithoutTheCapability()
    {
        var owner = new OwnerScope(new BackendNamespace("test.synthetic"), new AppId(Guid.Parse("019952d1-0000-7000-8000-000000000001")), new PlatformUserId(Guid.Parse("019952d1-0000-7000-8000-000000000002")));
        var body = new StoreQuestMessagePackCodec().EncodeClaimCommand(new GamePlatform.Wire.Contracts.QuestClaimCommand("test.quest", "test.quest:occ-1"));

        Assert.Throws<NotSupportedException>(() => new CanonicalCommandFingerprint().Compute(owner, new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003")),
            new ClientStreamId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060")), Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), 1, "quest.claim", 1, 1, 0, body, new byte[32]));
        Assert.Throws<QualificationCodecException>(() => codec.CanonicalBytes(Scoped(Vector("claim_text"), Operation(Vector("claim_text")))));
    }

    [Fact]
    public void LegacyKnownAnswersAreUnchangedWithTheCapability()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        var checkedCount = 0;
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray().Where(value => value.GetProperty("valid").GetBoolean()))
        {
            var input = vector.GetProperty("input");
            var kind = input.GetProperty("type").GetString()!;
            if (kind != "profile.patch" && kind != "gameplay.session.completed") continue;
            var body = codec.Encode(kind == "profile.patch" ? "profile.schema.json#/$defs/patchCommand" : "gameplay.schema.json#/$defs/completionCommand", QualificationCodecTests.Parse(input.GetProperty("payload")));
            var destination = new byte[32];
            new CanonicalCommandFingerprint(true).Compute(
                new OwnerScope(new BackendNamespace(input.GetProperty("backendNamespace").GetString()!), new AppId(Guid.Parse(input.GetProperty("authenticatedAppId").GetString()!)),
                    new PlatformUserId(Guid.Parse(input.GetProperty("authenticatedPlatformUserId").GetString()!))),
                new OperationId(Guid.Parse(input.GetProperty("operationId").GetString()!)), new ClientStreamId(Guid.Parse(input.GetProperty("clientStreamId").GetString()!)),
                Guid.Parse(input.GetProperty("installationId").GetString()!), long.Parse(input.GetProperty("sequence").GetString()!, CultureInfo.InvariantCulture),
                kind, input.GetProperty("schemaVersion").GetInt32(), 1, input.GetProperty("clientCreatedAt").GetInt64(), body, destination);
            Assert.Equal(vector.GetProperty("sha256").GetString(), Convert.ToHexString(destination).ToLowerInvariant());
            checkedCount++;
        }
        Assert.NotEqual(0, checkedCount);
    }

    private V Operation(JsonElement vector) => vector.TryGetProperty("operationMessagePackHex", out var hex)
        ? codec.Decode(QualificationMessagePackCodec.StoreQuestOperationSchema, Convert.FromHexString(hex.GetString()!))
        : QualificationCodecTests.Parse(vector.GetProperty("operation"));

    private static V Scoped(JsonElement vector, V operation)
    {
        var scope = vector.GetProperty("scope");
        var values = operation.Properties.ToList();
        foreach (var name in new[] { "backendNamespace", "authenticatedAppId", "authenticatedPlatformUserId", "clientStreamId" })
            values.Add(new KeyValuePair<string, V>(name, V.String(scope.GetProperty(name).GetString()!)));
        return V.Object(values);
    }

    private static JsonElement Vector(string name) => Vectors.RootElement.GetProperty("vectors").EnumerateArray().Single(value => value.GetProperty("name").GetString() == name);

    private static string Root
    {
        get { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory != null && !File.Exists(Path.Combine(directory.FullName, "GamePlatform.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
    }
}
