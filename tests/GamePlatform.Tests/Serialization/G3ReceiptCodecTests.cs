using System;
using System.Collections.Generic;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.Tests.Serialization
{
    public sealed class G3ReceiptCodecTests
    {
        private const string Response = "g3/reward-receipt.schema.json#/$defs/response";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();

        [Fact]
        public void CompletionNullRewardRoundTripsWithExplicitAuthority()
        {
            var value = Object(
                ("protocolVersion", QualificationValue.Integer(1)),
                ("schemaVersion", QualificationValue.Integer(2)),
                ("operationId", QualificationValue.String("01890f3e-7a6b-7c8d-9e0f-102030405060")),
                ("status", QualificationValue.String("completion_recorded")),
                ("outcomeAuthority", QualificationValue.String("client_trusted_unvalidated")),
                ("reward", QualificationValue.Null));

            var decoded = codec.Decode(Response, codec.Encode(Response, value));
            Assert.Equal("client_trusted_unvalidated", decoded.Properties["outcomeAuthority"].StringValue);
            Assert.Equal(QualificationValueKind.Null, decoded.Properties["reward"].Kind);
        }

        [Fact]
        public void CompletionRejectsAbsentRewardUnknownAuthorityAndSigned64Overflow()
        {
            var absent = Object(
                ("protocolVersion", QualificationValue.Integer(1)),
                ("schemaVersion", QualificationValue.Integer(2)),
                ("operationId", QualificationValue.String("01890f3e-7a6b-7c8d-9e0f-102030405060")),
                ("status", QualificationValue.String("completion_recorded")),
                ("outcomeAuthority", QualificationValue.String("client_trusted_unvalidated")));
            Assert.Throws<QualificationCodecException>(() => codec.Encode(Response, absent));

            var unknownAuthority = Object(absent.Properties,
                ("outcomeAuthority", QualificationValue.String("server_validated")),
                ("reward", QualificationValue.Null));
            Assert.Throws<QualificationCodecException>(() => codec.Encode(Response, unknownAuthority));

            var overflowLine = Object(
                ("lineIndex", QualificationValue.Integer(0)),
                ("definitionVersion", QualificationValue.Integer(1)),
                ("kind", QualificationValue.String("currency")),
                ("currencyId", QualificationValue.String("test.coin")),
                ("quantity", QualificationValue.String("9223372036854775808")));
            var reward = Object(
                ("grantId", QualificationValue.String("018f0f3e-7a6b-7c8d-9e0f-102030405061")),
                ("originatingOperationId", QualificationValue.String("01890f3e-7a6b-7c8d-9e0f-102030405060")),
                ("feedRevision", QualificationValue.String("1")),
                ("recordedAt", QualificationValue.Integer(1)),
                ("source", Object(("kind", QualificationValue.String("gameplay.completion")), ("key", QualificationValue.String(new string('a', 64))),
                    ("rewardSlot", QualificationValue.String("fixture.slot")), ("policyId", QualificationValue.String("mrsquare.casual.fixture")), ("policyVersion", QualificationValue.Integer(1)))),
                ("planId", QualificationValue.String("test.coin.one")),
                ("planVersion", QualificationValue.Integer(1)),
                ("lines", QualificationValue.Array(new[] { overflowLine })));
            Assert.Throws<QualificationCodecException>(() => codec.Encode(Response,
                Object(absent.Properties, ("reward", reward))));
        }

        private static QualificationValue Object(params (string Key, QualificationValue Value)[] fields) =>
            QualificationValue.Object(ToPairs(fields));

        private static QualificationValue Object(IReadOnlyDictionary<string, QualificationValue> source,
            params (string Key, QualificationValue Value)[] replacements)
        {
            var values = new Dictionary<string, QualificationValue>(source, StringComparer.Ordinal);
            foreach (var replacement in replacements) values[replacement.Key] = replacement.Value;
            return QualificationValue.Object(values);
        }

        private static IEnumerable<KeyValuePair<string, QualificationValue>> ToPairs((string Key, QualificationValue Value)[] fields)
        {
            foreach (var field in fields) yield return new KeyValuePair<string, QualificationValue>(field.Key, field.Value);
        }
    }
}
