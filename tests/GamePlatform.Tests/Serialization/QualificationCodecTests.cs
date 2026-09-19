using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamePlatform.Serialization.MessagePack;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Tests.Serialization;

public sealed class QualificationCodecTests
{
    private static string Root
    {
        get { var d=new DirectoryInfo(AppContext.BaseDirectory); while(d!=null&&!File.Exists(Path.Combine(d.FullName,"GamePlatform.sln"))) d=d.Parent; return d?.FullName??throw new InvalidOperationException("Repository root missing."); }
    }
    public static IEnumerable<object[]> Fingerprints()
    {
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"contracts/v1/fixtures/semantic/fingerprint-vectors.json")));
        foreach(var v in document.RootElement.GetProperty("vectors").EnumerateArray()) yield return new object[]{v.GetProperty("name").GetString()!,v.GetRawText()};
    }
    public static IEnumerable<object[]> Corpus()
    {
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"tests/acceptance/core-corpus.json")));
        foreach(var v in document.RootElement.GetProperty("validations").EnumerateArray()) yield return new object[]{v.GetProperty("id").GetString()!,v.GetRawText()};
    }
    [Theory][MemberData(nameof(Corpus))]
    public void ReviewedCorpus(string name,string json)
    {
        Assert.False(string.IsNullOrWhiteSpace(name)); using var doc=JsonDocument.Parse(json);var v=doc.RootElement;
        using var source=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,v.GetProperty("instancePath").GetString()!)));
        var schema=Path.GetFileName(v.GetProperty("schemaPath").GetString()!)+v.GetProperty("schemaFragment").GetString();var codec=new QualificationMessagePackCodec();
        if(!v.GetProperty("expectedValid").GetBoolean()) { Assert.ThrowsAny<Exception>(()=>codec.Encode(schema,Parse(source.RootElement)));return; }
        var value=Parse(source.RootElement);var bytes=codec.Encode(schema,value);var back=codec.Decode(schema,bytes);
        Assert.Equal(Convert.ToHexString(bytes),Convert.ToHexString(codec.Encode(schema,back)));
    }
    [Theory][MemberData(nameof(Fingerprints))]
    public void CanonicalKnownAnswers(string name,string json)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        using var doc=JsonDocument.Parse(json); var vector=doc.RootElement; var codec=new QualificationMessagePackCodec();
        if(!vector.GetProperty("valid").GetBoolean()) { Assert.ThrowsAny<Exception>(()=>codec.Fingerprint(Parse(vector.GetProperty("input"))));return; }
        var value=Parse(vector.GetProperty("input"));
        Assert.Equal(vector.GetProperty("canonicalHex").GetString(),Convert.ToHexString(codec.CanonicalBytes(value)).ToLowerInvariant());
        Assert.Equal(vector.GetProperty("sha256").GetString(),codec.Fingerprint(value));
    }
    [Theory]
    [InlineData("0")][InlineData("-9223372036854775808")][InlineData("9223372036854775807")][InlineData("9007199254740993")]
    public void ExactInt64RoundTrip(string value)
    {
        var codec=new QualificationMessagePackCodec(); const string schema="common.schema.json#/$defs/signedInt64";
        Assert.Equal(value,codec.Decode(schema,codec.Encode(schema,V.String(value))).StringValue);
    }
    [Theory]
    [InlineData("00112233-4455-4677-8899-aabbccddeeff","C41000112233445546778899AABBCCDDEEFF")]
    [InlineData("01890f3e-7a6b-7c8d-9e0f-102030405060","C41001890F3E7A6B7C8D9E0F102030405060")]
    public void UuidRfcBytes(string uuid,string hex)
    {
        var codec=new QualificationMessagePackCodec(); var bytes=codec.Encode("common.schema.json#/$defs/uuid",V.String(uuid));
        Assert.Equal(hex,Convert.ToHexString(bytes)); Assert.Equal(uuid,codec.Decode("common.schema.json#/$defs/uuid",bytes).StringValue);
    }
    [Theory]
    [InlineData("CF8000000000000000")][InlineData("CB3FF0000000000000")][InlineData("CA3F800000")][InlineData("D40000")][InlineData("0101")][InlineData("A131")][InlineData("D3")]
    public void RejectAmbiguousOrMalformedNumbers(string hex)
    { Assert.Throws<QualificationCodecException>(()=>new QualificationMessagePackCodec().Decode("common.schema.json#/$defs/signedInt64",Convert.FromHexString(hex))); }
    [Theory]
    [InlineData("82A16101A16102")][InlineData("81A2C08001")][InlineData("DE0101")][InlineData("DDFFFFFFFF")][InlineData("DF00010000")]
    public void RejectDuplicateUtf8AndDeclaredOversize(string hex)
    { Assert.Throws<QualificationCodecException>(()=>new QualificationMessagePackCodec().Decode("profile.schema.json#/$defs/patchCommand",Convert.FromHexString(hex))); }
    [Theory]
    [InlineData("01")][InlineData("CC01")][InlineData("CD0001")][InlineData("CE00000001")][InlineData("CF0000000000000001")][InlineData("D001")][InlineData("D10001")][InlineData("D200000001")][InlineData("D30000000000000001")]
    public void CompactAndFullWidthIntegerTokensNormalize(string hex)
    { Assert.Equal("1",new QualificationMessagePackCodec().Decode("common.schema.json#/$defs/signedInt64",Convert.FromHexString(hex)).StringValue); }
    [Fact]
    public void CanonicalIdentifiersRejectTrailingNewlines()
    { Assert.Throws<QualificationCodecException>(()=>new QualificationMessagePackCodec().ValidateDiagnostic("common.schema.json#/$defs/uuid",V.String("00112233-4455-4677-8899-aabbccddeeff\n"))); }
    [Fact]
    public void ExtensionDepthIncludesWrapperAndOnlyContainers()
    {
        var codec=new QualificationMessagePackCodec(); V chain=V.String("leaf");
        for(int i=0;i<14;i++) chain=V.Array(new[]{chain});
        V Wrapper(V child)=>V.Object(new Dictionary<string,V>{{"schema",V.String("test")},{"version",V.Integer(1)},{"value",V.Object(new Dictionary<string,V>{{"a",child}})}});
        codec.ValidateDiagnostic("common.schema.json#/$defs/extension",Wrapper(chain));
        Assert.Throws<QualificationCodecException>(()=>codec.ValidateDiagnostic("common.schema.json#/$defs/extension",Wrapper(V.Array(new[]{chain}))));
    }
    [Fact]
    public void ExtensionAggregateCompactJsonBytesAreBounded()
    {
        var extension=V.Object(new Dictionary<string,V>{{"schema",V.String("test")},{"version",V.Integer(1)},{"value",V.Object(new Dictionary<string,V>{{"a",V.String(new string('x',8100))},{"b",V.String(new string('x',8100))},{"c",V.String(new string('x',8100))}})}});
        Assert.Throws<QualificationCodecException>(()=>new QualificationMessagePackCodec().ValidateDiagnostic("common.schema.json#/$defs/extension",extension));
    }
    [Fact]
    public void ExtractedInlineSchemaPointerSupportsArraySegments()
    {
        var value=V.Object(new Dictionary<string,V>{{"kind",V.String("profile_updated")},{"profileRevision",V.String("1")}});
        new QualificationMessagePackCodec().ValidateDiagnostic("push.schema.json#/$defs/accepted/properties/result/oneOf/0",value);
    }
    [Fact]
    public void ReferencedPrimitiveAlsoEnforcesSiblingConst()
    {
        const string schema="recovery.schema.json#/$defs/streamResponse/properties/nextSequence";var codec=new QualificationMessagePackCodec();
        codec.ValidateDiagnostic(schema,V.String("1"));
        Assert.Throws<QualificationCodecException>(()=>codec.Encode(schema,V.String("2")));
        Assert.Throws<QualificationCodecException>(()=>codec.Decode(schema,new byte[]{2}));
    }
    internal static V Parse(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null=>V.Null,JsonValueKind.True=>V.Boolean(true),JsonValueKind.False=>V.Boolean(false),
        JsonValueKind.String=>V.String(value.GetString()!),JsonValueKind.Number=>V.Integer(value.GetInt64()),
        JsonValueKind.Array=>V.Array(value.EnumerateArray().Select(Parse)),
        JsonValueKind.Object=>V.Object(value.EnumerateObject().Select(p=>new KeyValuePair<string,V>(p.Name,Parse(p.Value)))),
        _=>throw new InvalidOperationException()
    };
}
