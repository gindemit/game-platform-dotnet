using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.Tests.Serialization;

public sealed class CanonicalCommandFingerprintTests
{
    [Fact]
    public void AdapterMatchesEveryApprovedCommandKnownAnswer()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        var codec = new QualificationMessagePackCodec();
        var adapter = new CanonicalCommandFingerprint();
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray().Where(value => value.GetProperty("valid").GetBoolean()))
        {
            var input = vector.GetProperty("input");
            var kind = input.GetProperty("type").GetString()!;
            if (kind != "profile.patch" && kind != "gameplay.session.completed") continue;
            var body = codec.Encode(kind == "profile.patch" ? "profile.schema.json#/$defs/patchCommand" : "gameplay.schema.json#/$defs/completionCommand",
                QualificationCodecTests.Parse(input.GetProperty("payload")));
            var destination = new byte[32];
            adapter.Compute(
                new OwnerScope(new BackendNamespace(input.GetProperty("backendNamespace").GetString()!),
                    new AppId(Guid.Parse(input.GetProperty("authenticatedAppId").GetString()!)),
                    new PlatformUserId(Guid.Parse(input.GetProperty("authenticatedPlatformUserId").GetString()!))),
                new OperationId(Guid.Parse(input.GetProperty("operationId").GetString()!)),
                new ClientStreamId(Guid.Parse(input.GetProperty("clientStreamId").GetString()!)),
                Guid.Parse(input.GetProperty("installationId").GetString()!),
                long.Parse(input.GetProperty("sequence").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                kind, input.GetProperty("schemaVersion").GetInt32(), 1, input.GetProperty("clientCreatedAt").GetInt64(), body, destination);
            Assert.Equal(vector.GetProperty("sha256").GetString(), Convert.ToHexString(destination).ToLowerInvariant());
        }
    }

    [Fact]
    public void AdapterFailsClosedForUnknownVersionsKindsAndPayloads()
    {
        var adapter = new CanonicalCommandFingerprint();
        var owner = new OwnerScope(new BackendNamespace("test.synthetic"), new AppId(Guid.Parse("019952d1-0000-7000-8000-000000000001")), new PlatformUserId(Guid.Parse("019952d1-0000-7000-8000-000000000002")));
        var operation = new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        var stream = new ClientStreamId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        Assert.Equal(0, adapter.GetFingerprintLength(2));
        Assert.Throws<NotSupportedException>(() => adapter.Compute(owner, operation, stream, Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), 1, "unknown", 1, 1, 0, new byte[] { 0xc0 }, new byte[32]));
        Assert.Throws<QualificationCodecException>(() => adapter.Compute(owner, operation, stream, Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), 1, "profile.patch", 1, 1, 0, new byte[] { 0xc0 }, new byte[32]));
    }

    private static string Root
    {
        get { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory != null && !File.Exists(Path.Combine(directory.FullName, "GamePlatform.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
    }
}
