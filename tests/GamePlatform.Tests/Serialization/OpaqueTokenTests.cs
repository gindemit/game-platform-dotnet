using System;
using System.Buffers;
using System.Linq;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;
using MessagePack;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Tests.Serialization;

public sealed class OpaqueTokenTests
{
    private const string Schema = "common.schema.json#/$defs/opaqueToken";
    private static string Token(int length) => Convert.ToBase64String(new byte[length]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Binary(int length)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.Write(new byte[length]); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    [Theory][InlineData(12)][InlineData(13)][InlineData(3072)]
    public void PrimitiveAndTypedTokensUseBinary(int length)
    {
        var codec = new QualificationMessagePackCodec();
        var token = Token(length);
        Assert.Equal(Binary(length), codec.Encode(Schema, V.String(token)));
        Assert.Equal(token, codec.Decode(Schema, Binary(length)).StringValue);
        var typed = new TypedQualificationCodec();
        var value = new BootstrapPageRequest(token, token, 65536);
        var bytes = typed.Encode(value);
        var reader = new MessagePackReader(bytes);
        Assert.Equal(4, reader.ReadMapHeader());
        int tokens = 0;
        for (int i = 0; i < 4; i++)
        {
            var key = reader.ReadString();
            if (key == "snapshotSession" || key == "pageToken")
            { Assert.Equal(MessagePackType.Binary, reader.NextMessagePackType); Assert.Equal(length, reader.ReadBytes()!.Value.Length); tokens++; }
            else reader.Skip();
        }
        Assert.Equal(2, tokens);
        Assert.Equal(token, typed.Decode<BootstrapPageRequest>(bytes).PageToken);
    }

    [Theory][InlineData(11)][InlineData(3073)]
    public void OutOfRangeTokensFailBothDirections(int length)
    {
        var codec = new QualificationMessagePackCodec();
        Assert.Throws<QualificationCodecException>(() => codec.Decode(Schema, Binary(length)));
        Assert.Throws<QualificationCodecException>(() => codec.Encode(Schema, V.String(Token(length))));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAA=")][InlineData("AAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAA")][InlineData("AAAAAAAAAAAAAAAAAB")]
    [InlineData("AAAAAAAAAAAAAAA/")][InlineData("AAAAAAAAAAAAAAAA\n")]
    public void NoncanonicalDiagnosticsFail(string token)
    { Assert.Throws<QualificationCodecException>(() => new QualificationMessagePackCodec().Encode(Schema, V.String(token))); }

    [Fact]
    public void WireStringIsNotAToken()
    {
        var bytes = new byte[] { 0xb0 }.Concat(Enumerable.Repeat((byte)'A', 16)).ToArray();
        Assert.Throws<QualificationCodecException>(() => new QualificationMessagePackCodec().Decode(Schema, bytes));
    }

    [Theory][InlineData(1, false)][InlineData(70, true)]
    public void BinaryExpansionIsReservedBeforeSchemaEvaluation(int count, bool exceedsBudget)
    {
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(count);
        for (int i = 0; i < count; i++) writer.WriteRaw(Binary(3072));
        writer.Flush();
        Assert.True(buffer.WrittenCount < 262144);
        var error = Assert.Throws<QualificationCodecException>(() => new QualificationMessagePackCodec().Decode(Schema, buffer.WrittenSpan.ToArray()));
        Assert.Contains(exceedsBudget ? "allocation budget" : "reviewed core schema", error.Message);
    }
}
