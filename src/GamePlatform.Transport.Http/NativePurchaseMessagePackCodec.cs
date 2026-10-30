using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Features.Purchases;
using GamePlatform.Serialization.MessagePack;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
namespace GamePlatform.Transport.Http
{
    internal sealed class NativePurchaseMessagePackCodec
    {
        private const string Schema = "native-purchases/schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] ProductsRequest() => codec.Encode(Schema + "productsRequest", Obj());
        public byte[] IntentRequest(Guid key, string productId, int version) => codec.Encode(Schema + "intentRequest",
            Obj(("intentKey", Id(key)), ("productId", V.String(productId)), ("productVersion", V.Integer(version)), ("source", V.String("purchase"))));
        public byte[] ProofRequest(Guid intentId, NativeProof proof) => codec.Encode(Schema + "proofRequest",
            Obj(("intentId", Id(intentId)), ("purchaseToken", V.String(proof.Token))));
        public byte[] RecoverProofRequest(Guid key, NativeProof proof) => codec.Encode(Schema + "recoverProofRequest",
            Obj(("intentKey", Id(key)), ("provider", V.String("google_play")), ("packageName", V.String(proof.PackageName)),
                ("storeProductId", V.String(proof.StoreProductId)), ("purchaseToken", V.String(proof.Token))));
        public byte[] StatusRequest(Guid intentId) => codec.Encode(Schema + "statusRequest", Obj(("intentId", Id(intentId))));
        public byte[] RecoveryRequest(Guid? after, int limit) => codec.Encode(Schema + "recoveryRequest",
            Obj(("afterIntentId", after.HasValue ? Id(after.Value) : V.Null), ("limit", V.Integer(limit))));
        public IReadOnlyList<NativeProduct> Products(byte[] bytes)
        {
            var values = codec.Decode(Schema + "productsResponse", bytes).Properties["products"].Items;
            var result = values.Select(value =>
            {
                var p = value.Properties;
                return new NativeProduct(p["productId"].StringValue, checked((int)p["productVersion"].IntegerValue),
                    p["packageName"].StringValue, p["storeProductId"].StringValue, p["environment"].StringValue,
                    p["contents"].Items.Select(Line).ToArray());
            }).ToArray();
            for (int i = 1; i < result.Length; i++)
                if (string.CompareOrdinal(result[i - 1].ProductId, result[i].ProductId) >= 0)
                    throw new FormatException("Native products are not ordered.");
            return Array.AsReadOnly(result);
        }
        public NativeIntentState Intent(byte[] bytes) => ReadIntent(codec.Decode(Schema + "intentState", bytes));
        public NativeRecoveryPage Recovery(byte[] bytes)
        {
            var p = codec.Decode(Schema + "recoveryResponse", bytes).Properties;
            var items = p["intents"].Items.Select(ReadIntent).ToArray();
            return new NativeRecoveryPage(Array.AsReadOnly(items), p["nextIntentId"].Kind == K.Null ? (Guid?)null : Guid.ParseExact(p["nextIntentId"].StringValue, "D"));
        }
        private static NativeIntentState ReadIntent(V value)
        {
            var p = value.Properties;
            var receipt = p["receipt"];
            Guid? grant = null, charge = null, epoch = null, originatingIntent = null;
            long? revision = null, visibility = null, originalRevision = null;
            NativeContentLine[] lines = Array.Empty<NativeContentLine>();
            if (receipt.Kind != K.Null)
            {
                var r = receipt.Properties;
                var checkpoint = r["deliveryCheckpoint"].Properties;
                grant = Guid.ParseExact(r["grantId"].StringValue, "D");
                charge = Guid.ParseExact(r["chargeId"].StringValue, "D");
                originatingIntent = Guid.ParseExact(r["originatingIntentId"].StringValue, "D");
                originalRevision = Wide(r["originalFeedRevision"]);
                epoch = Guid.ParseExact(checkpoint["logEpoch"].StringValue, "D");
                revision = Wide(checkpoint["feedRevision"]);
                visibility = Wide(checkpoint["visibilityGeneration"]);
                lines = r["lines"].Items.Select(Line).ToArray();
            }
            if ((p["state"].StringValue == "granted") != grant.HasValue)
                throw new FormatException("Native receipt state mismatch.");
            if (p["launchAllowed"].BooleanValue &&
                (p["source"].StringValue != "purchase" || p["state"].StringValue != "awaiting_proof"))
                throw new FormatException("Invalid native launch permission.");
            return new NativeIntentState(Guid.ParseExact(p["intentId"].StringValue, "D"), Guid.ParseExact(p["intentKey"].StringValue, "D"),
                p["productId"].StringValue, checked((int)p["productVersion"].IntegerValue), p["packageName"].StringValue,
                p["storeProductId"].StringValue, p["environment"].StringValue, Guid.ParseExact(p["customerBinding"].StringValue, "D"),
                p["source"].StringValue, p["launchAllowed"].BooleanValue, p["state"].StringValue,
                p["reason"].Kind == K.Null ? null : p["reason"].StringValue, p["finishState"].StringValue,
                revision, visibility, epoch, grant, charge, Array.AsReadOnly(lines), originalRevision, originatingIntent,
                p["retryAfterMilliseconds"].Kind == K.Null ? (int?)null : checked((int)p["retryAfterMilliseconds"].IntegerValue));
        }
        private static NativeContentLine Line(V value)
        {
            var p = value.Properties;
            var kind = p["kind"].StringValue;
            return new NativeContentLine(kind, p[kind == "currency" ? "currencyId" : "itemId"].StringValue, Wide(p["quantity"]),
                p.TryGetValue("lineIndex", out var index) ? checked((int)index.IntegerValue) : (int?)null,
                p.TryGetValue("definitionVersion", out var definition) ? checked((int)definition.IntegerValue) : (int?)null);
        }
        private static long Wide(V value) => long.Parse(value.StringValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        private static V Id(Guid value) => V.String(value.ToString("D"));
        private static V Obj(params (string, V)[] fields) => V.Object(fields.Select(field => new KeyValuePair<string, V>(field.Item1, field.Item2)));
    }
}
