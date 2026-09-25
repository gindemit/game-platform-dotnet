#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// Closed mapper for the A06 store purchase and quest claim push/receipt superset.
    /// </summary>
    /// <remarks>
    /// Results other than the two A06 accepted kinds are mapped by the frozen v1 DTO codec.
    /// </remarks>
    public sealed class StoreQuestMessagePackCodec
    {
        private const string Schema = "a06/store-quest.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        private readonly TypedQualificationCodec typed = new TypedQualificationCodec();

        public byte[] EncodePurchaseCommand(StoreOfferPurchaseCommand value) => codec.Encode(Schema + "purchaseCommand", WritePayload(value));

        public StoreOfferPurchaseCommand DecodePurchaseCommand(byte[] bytes) => ReadPurchase(codec.Decode(Schema + "purchaseCommand", bytes));

        public byte[] EncodeClaimCommand(QuestClaimCommand value) => codec.Encode(Schema + "claimCommand", WritePayload(value));

        public QuestClaimCommand DecodeClaimCommand(byte[] bytes) => ReadClaim(codec.Decode(Schema + "claimCommand", bytes));

        public byte[] EncodePushRequest(PushRequest value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return codec.Encode(Schema + "request", Obj(("protocolVersion", V.Integer(value.ProtocolVersion)), ("clientStreamId", Uuid(value.ClientStreamId)),
                ("operations", V.Array(value.Operations.Select(WriteOperation)))));
        }

        public PushRequest DecodePushRequest(byte[] bytes)
        {
            var p = codec.Decode(Schema + "request", bytes).Properties;
            return new PushRequest(ReadUuid(p["clientStreamId"]), p["operations"].Items.Select(ReadOperation).ToArray());
        }

        public byte[] EncodePushResponse(PushResponse value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return codec.Encode(Schema + "response", Obj(("protocolVersion", V.Integer(value.ProtocolVersion)), ("clientStreamId", Uuid(value.ClientStreamId)),
                ("operationResults", V.Array(value.OperationResults.Select(WriteResult))), ("finalizedThrough", Wide(value.FinalizedThrough)), ("serverTime", V.Integer(value.ServerTime))));
        }

        public PushResponse DecodePushResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "response", bytes).Properties;
            return new PushResponse(ReadUuid(p["clientStreamId"]), p["operationResults"].Items.Select(ReadResult).ToArray(), ReadWide(p["finalizedThrough"]), p["serverTime"].IntegerValue);
        }

        public byte[] EncodeResult(IPushResult value) => codec.Encode(Schema + "result", WriteResult(value));

        public IPushResult DecodeResult(byte[] bytes) => ReadResult(codec.Decode(Schema + "result", bytes));

        public byte[] EncodeReceiptResponse(RecoveryReceiptResponse value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var result = value.Result switch
            {
                null => V.Null,
                PushAccepted accepted when IsStoreQuest(accepted.Result) => WriteAccepted(accepted),
                PushAccepted accepted => typed.ToDiagnostic(accepted),
                PushTerminalRejected rejected => typed.ToDiagnostic(rejected),
                _ => throw new QualificationCodecException("Unsupported receipt result implementation.")
            };
            return codec.Encode(Schema + "receiptResponse", Obj(("protocolVersion", V.Integer(value.ProtocolVersion)), ("clientStreamId", Uuid(value.ClientStreamId)),
                ("found", V.Boolean(value.Found)), ("result", result), ("serverTime", V.Integer(value.ServerTime)), ("operationId", Uuid(value.OperationId)),
                ("observedFinalizedThrough", Wide(value.ObservedFinalizedThrough))));
        }

        public RecoveryReceiptResponse DecodeReceiptResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "receiptResponse", bytes).Properties;
            var result = p["result"];
            IPushFinalResult? final = result.Kind == K.Null ? null : IsStoreQuestAccepted(result) ? ReadAccepted(result)
                : result.Properties["status"].StringValue == "accepted" ? typed.FromDiagnostic<PushAccepted>(result) : typed.FromDiagnostic<PushTerminalRejected>(result);
            return new RecoveryReceiptResponse(ReadUuid(p["clientStreamId"]), p["found"].BooleanValue, final, p["serverTime"].IntegerValue, ReadUuid(p["operationId"]),
                ReadWide(p["observedFinalizedThrough"]));
        }

        private V WriteResult(IPushResult value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value is PushAccepted accepted && IsStoreQuest(accepted.Result) ? WriteAccepted(accepted) : typed.ToDiagnostic(value);
        }

        private IPushResult ReadResult(V value) => IsStoreQuestAccepted(value) ? ReadAccepted(value) : typed.FromDiagnostic<IPushResult>(value);

        private static bool IsStoreQuest(IPushAcceptedResult result) => result is StoreOfferPurchasedResult || result is QuestClaimedResult;

        private static bool IsStoreQuestAccepted(V value)
        {
            if (!value.Properties.TryGetValue("status", out var status) || status.StringValue != "accepted") return false;
            var kind = value.Properties["result"].Properties["kind"].StringValue;
            return kind == "store_offer_purchased" || kind == "quest_claimed";
        }

        private static V WriteAccepted(PushAccepted value) => Obj(("operationId", Uuid(value.OperationId)), ("sequence", Wide(value.Sequence)), ("status", V.String("accepted")),
            ("feedRevision", Wide(value.FeedRevision)), ("result", WriteAcceptedResult(value.Result)));

        private static PushAccepted ReadAccepted(V value)
        {
            var p = value.Properties;
            return new PushAccepted(ReadUuid(p["operationId"]), ReadWide(p["sequence"]), ReadWide(p["feedRevision"]), ReadAcceptedResult(p["result"]));
        }

        private static V WriteAcceptedResult(IPushAcceptedResult value)
        {
            switch (value)
            {
                case StoreOfferPurchasedResult item:
                    return Obj(("kind", V.String("store_offer_purchased")), ("purchaseId", Uuid(item.PurchaseId)), ("originatingOperationId", Uuid(item.OriginatingOperationId)),
                        ("offerId", V.String(item.OfferId)), ("offerVersion", V.Integer(item.OfferVersion)), ("purchaseKey", Uuid(item.PurchaseKey)),
                        ("debit", Obj(("currencyId", V.String(item.Debit.CurrencyId)), ("quantity", Wide(item.Debit.Quantity)))), ("lines", Lines(item.Lines)));
                case QuestClaimedResult item:
                    return Obj(("kind", V.String("quest_claimed")), ("claimId", Uuid(item.ClaimId)), ("originatingOperationId", Uuid(item.OriginatingOperationId)),
                        ("questId", V.String(item.QuestId)), ("occurrenceKey", V.String(item.OccurrenceKey)), ("lines", Lines(item.Lines)));
                default:
                    throw new QualificationCodecException("Unsupported A06 accepted result implementation.");
            }
        }

        private static IPushAcceptedResult ReadAcceptedResult(V value)
        {
            var p = value.Properties;
            var lines = p["lines"].Items.Select(G3RewardReceiptMessagePackCodec.ReadLine).ToArray();
            if (p["kind"].StringValue == "quest_claimed")
                return new QuestClaimedResult(ReadUuid(p["claimId"]), ReadUuid(p["originatingOperationId"]), p["questId"].StringValue, p["occurrenceKey"].StringValue, lines);
            var debit = p["debit"].Properties;
            return new StoreOfferPurchasedResult(ReadUuid(p["purchaseId"]), ReadUuid(p["originatingOperationId"]), p["offerId"].StringValue, checked((int)p["offerVersion"].IntegerValue),
                ReadUuid(p["purchaseKey"]), new StoreOfferDebit(debit["currencyId"].StringValue, ReadWide(debit["quantity"])), lines);
        }

        private static V WriteOperation(PushOperation value) => Obj(("operationId", Uuid(value.OperationId)), ("installationId", Uuid(value.InstallationId)),
            ("sequence", Wide(value.Sequence)), ("type", V.String(value.Type)), ("schemaVersion", V.Integer(1)), ("clientCreatedAt", V.Integer(value.ClientCreatedAt)),
            ("payload", WritePayload(value.Payload)));

        private static PushOperation ReadOperation(V value)
        {
            var p = value.Properties;
            var type = p["type"].StringValue;
            IPushPayload payload = type == "store.offer.purchase" ? ReadPurchase(p["payload"]) : type == "quest.claim" ? (IPushPayload)ReadClaim(p["payload"])
                : throw new QualificationCodecException("Only A06 operations are mapped by this codec.");
            return new PushOperation(ReadUuid(p["operationId"]), ReadUuid(p["installationId"]), ReadWide(p["sequence"]), type, p["clientCreatedAt"].IntegerValue, payload);
        }

        private static V WritePayload(IPushPayload value)
        {
            switch (value)
            {
                case StoreOfferPurchaseCommand item:
                    return Obj(("offerId", V.String(item.OfferId)), ("offerVersion", V.Integer(item.OfferVersion)), ("purchaseKey", Uuid(item.PurchaseKey)));
                case QuestClaimCommand item:
                    return Obj(("questId", V.String(item.QuestId)), ("occurrenceKey", V.String(item.OccurrenceKey)));
                default:
                    throw new QualificationCodecException("Only A06 payloads are mapped by this codec.");
            }
        }

        private static StoreOfferPurchaseCommand ReadPurchase(V value) =>
            new StoreOfferPurchaseCommand(value.Properties["offerId"].StringValue, checked((int)value.Properties["offerVersion"].IntegerValue), ReadUuid(value.Properties["purchaseKey"]));

        private static QuestClaimCommand ReadClaim(V value) => new QuestClaimCommand(value.Properties["questId"].StringValue, value.Properties["occurrenceKey"].StringValue);

        private static V Lines(IReadOnlyList<ILiveSliceRewardLine> lines) => V.Array(lines.Select(G3RewardReceiptMessagePackCodec.WriteLine));

        private static V Uuid(Guid value) => V.String(value.ToString("D"));

        private static Guid ReadUuid(V value) => Guid.ParseExact(value.StringValue, "D");

        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));

        private static long ReadWide(V value) => long.Parse(value.StringValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        private static V Obj(params (string Key, V Value)[] values) => V.Object(values.Select(value => new KeyValuePair<string, V>(value.Key, value.Value)));
    }
}
