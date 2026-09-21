#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Closed production mapper for only the approved G3 reward-receipt v2 request/response.</summary>
    public sealed class G3RewardReceiptMessagePackCodec
    {
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] EncodeRequest(G3RewardReceiptRequest value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return codec.Encode("g3/reward-receipt.schema.json#/$defs/request", Obj(("operationId", V.String(value.OperationId.ToString("D")))));
        }
        public byte[] EncodeResponse(IG3RewardReceiptResponse value) => codec.Encode("g3/reward-receipt.schema.json#/$defs/response", WriteResponse(value));
        public IG3RewardReceiptResponse DecodeResponse(byte[] bytes)
        {
            var value = codec.Decode("g3/reward-receipt.schema.json#/$defs/response", bytes);
            var operation = Guid.ParseExact(value.Properties["operationId"].StringValue, "D");
            var status = value.Properties["status"].StringValue;
            if (status == "not_found") return new G3RewardReceiptNotFoundResponse(operation);
            if (status != "completion_recorded" || value.Properties["outcomeAuthority"].StringValue != "client_trusted_unvalidated") throw new QualificationCodecException("Unsupported G3 receipt response.");
            return new G3RewardReceiptCompletionResponse(operation, value.Properties["reward"].Kind == QualificationValueKind.Null ? null : ReadReceipt(value.Properties["reward"]));
        }
        private static V WriteResponse(IG3RewardReceiptResponse value)
        {
            switch (value)
            {
                case G3RewardReceiptNotFoundResponse item: return Obj(("protocolVersion", V.Integer(1)), ("schemaVersion", V.Integer(2)), ("operationId", V.String(item.OperationId.ToString("D"))), ("status", V.String("not_found")));
                case G3RewardReceiptCompletionResponse item: return Obj(("protocolVersion", V.Integer(1)), ("schemaVersion", V.Integer(2)), ("operationId", V.String(item.OperationId.ToString("D"))), ("status", V.String("completion_recorded")), ("outcomeAuthority", V.String("client_trusted_unvalidated")), ("reward", item.Reward == null ? V.Null : WriteReceipt(item.Reward)));
                default: throw new QualificationCodecException("Unsupported G3 receipt response implementation.");
            }
        }
        private static V WriteReceipt(LiveSliceRewardReceipt value) => Obj(("grantId", V.String(value.GrantId.ToString("D"))), ("originatingOperationId", V.String(value.OriginatingOperationId.ToString("D"))), ("feedRevision", Wide(value.FeedRevision)), ("recordedAt", V.Integer(value.RecordedAt)), ("source", Obj(("kind", V.String("gameplay.completion")), ("key", V.String(value.Source.Key)), ("rewardSlot", V.String(value.Source.RewardSlot)), ("policyId", V.String(value.Source.PolicyId)), ("policyVersion", V.Integer(value.Source.PolicyVersion)))), ("planId", V.String(value.PlanId)), ("planVersion", V.Integer(value.PlanVersion)), ("lines", V.Array(value.Lines.Select(WriteLine))));
        private static V WriteLine(ILiveSliceRewardLine value)
        {
            switch (value)
            {
                case LiveSliceCurrencyRewardLine item: return Obj(("lineIndex", V.Integer(item.LineIndex)), ("definitionVersion", V.Integer(item.DefinitionVersion)), ("kind", V.String("currency")), ("currencyId", V.String(item.CurrencyId)), ("quantity", Wide(item.Quantity)));
                case LiveSliceStackRewardLine item: return Obj(("lineIndex", V.Integer(item.LineIndex)), ("definitionVersion", V.Integer(item.DefinitionVersion)), ("kind", V.String("item_stack")), ("itemId", V.String(item.ItemId)), ("quantity", Wide(item.Quantity)));
                case LiveSliceEntitlementRewardLine item: return Obj(("lineIndex", V.Integer(item.LineIndex)), ("definitionVersion", V.Integer(item.DefinitionVersion)), ("kind", V.String("entitlement")), ("entitlementId", V.String(item.EntitlementId)), ("expiresAt", item.ExpiresAt.HasValue ? V.Integer(item.ExpiresAt.Value) : V.Null));
                default: throw new QualificationCodecException("Unsupported reward line implementation.");
            }
        }
        private static LiveSliceRewardReceipt ReadReceipt(V value)
        {
            var p = value.Properties; var source = p["source"].Properties;
            var lines = p["lines"].Items.Select(ReadLine).ToArray();
            return new LiveSliceRewardReceipt(Guid.ParseExact(p["grantId"].StringValue, "D"), Guid.ParseExact(p["originatingOperationId"].StringValue, "D"), ReadWide(p["feedRevision"]), p["recordedAt"].IntegerValue, new LiveSliceRewardSource(source["key"].StringValue, source["rewardSlot"].StringValue, source["policyId"].StringValue, checked((int)source["policyVersion"].IntegerValue)), p["planId"].StringValue, checked((int)p["planVersion"].IntegerValue), lines);
        }
        private static ILiveSliceRewardLine ReadLine(V value)
        {
            var p = value.Properties; var index = checked((int)p["lineIndex"].IntegerValue); var version = checked((int)p["definitionVersion"].IntegerValue);
            switch (p["kind"].StringValue)
            {
                case "currency": return new LiveSliceCurrencyRewardLine(index, version, p["currencyId"].StringValue, ReadWide(p["quantity"]));
                case "item_stack": return new LiveSliceStackRewardLine(index, version, p["itemId"].StringValue, ReadWide(p["quantity"]));
                case "entitlement": return new LiveSliceEntitlementRewardLine(index, version, p["entitlementId"].StringValue, p["expiresAt"].Kind == QualificationValueKind.Null ? (long?)null : p["expiresAt"].IntegerValue);
                default: throw new QualificationCodecException("Unsupported reward line kind.");
            }
        }
        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));
        private static long ReadWide(V value) => long.Parse(value.StringValue, CultureInfo.InvariantCulture);
        private static V Obj(params (string Key, V Value)[] values) => V.Object(values.Select(value => new KeyValuePair<string, V>(value.Key, value.Value)));
    }
}
