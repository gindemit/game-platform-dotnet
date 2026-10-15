using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Daily;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Closed named-map codec for the pinned daily status and daily.claim schema.</summary>
    public sealed class DailyMessagePackCodec
    {
        private const string Schema = "daily.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] EncodeStatusRequest() => codec.Encode(Schema + "statusRequest", Obj());
        public byte[] EncodeStatusResponse(DailyStatusResponse value) => codec.Encode(Schema + "statusResponse", Obj(
            ("utcDate", V.String(value.UtcDate)), ("nextUtcDayAt", V.Integer(value.NextUtcDayAt)), ("eligible", V.Boolean(value.Eligible)),
            ("cycleDay", V.Integer(value.CycleDay)), ("policyVersion", V.Integer(value.PolicyVersion)),
            ("reward", V.Array(value.Reward.Select(Planned))), ("history", V.Array(value.History.Select(Entry)))));
        public DailyStatusResponse DecodeStatusResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "statusResponse", bytes).Properties;
            return new DailyStatusResponse(p["utcDate"].StringValue, p["nextUtcDayAt"].IntegerValue, p["eligible"].BooleanValue,
                checked((int)p["cycleDay"].IntegerValue), checked((int)p["policyVersion"].IntegerValue),
                p["reward"].Items.Select(ReadPlanned).ToArray(), p["history"].Items.Select(ReadEntry).ToArray());
        }
        public byte[] EncodeClaimCommand(DailyClaimCommand value) => codec.Encode(Schema + "claimCommand", WriteClaimCommand(value));
        public DailyClaimCommand DecodeClaimCommand(byte[] bytes) => ReadClaimCommand(codec.Decode(Schema + "claimCommand", bytes));
        public byte[] EncodeClaimResult(DailyClaimedResult value) => codec.Encode(Schema + "claimResult", WriteClaimResult(value));
        public DailyClaimedResult DecodeClaimResult(byte[] bytes) => ReadClaimResult(codec.Decode(Schema + "claimResult", bytes));
        internal static V WriteClaimCommand(DailyClaimCommand value) => Obj(("utcDate", V.String(value.UtcDate)));
        internal static DailyClaimCommand ReadClaimCommand(V value) => new DailyClaimCommand(value.Properties["utcDate"].StringValue);
        internal static V WriteClaimResult(DailyClaimedResult value) => Obj(("kind", V.String("daily_claimed")),
            ("claimId", Id(value.ClaimId)), ("originatingOperationId", Id(value.OriginatingOperationId)), ("utcDate", V.String(value.UtcDate)),
            ("cycleDay", V.Integer(value.CycleDay)), ("policyVersion", V.Integer(value.PolicyVersion)), ("lines", Lines(value.Lines)));
        internal static DailyClaimedResult ReadClaimResult(V value)
        {
            var p = value.Properties;
            return new DailyClaimedResult(Id(p["claimId"]), Id(p["originatingOperationId"]), p["utcDate"].StringValue, checked((int)p["cycleDay"].IntegerValue),
                checked((int)p["policyVersion"].IntegerValue), p["lines"].Items.Select(G3RewardReceiptMessagePackCodec.ReadLine).ToArray());
        }
        private static V Planned(DailyPlannedLine value) => Obj(("kind", V.String(value.Kind)), ("resourceId", V.String(value.ResourceId)), ("quantity", Wide(value.Quantity)));
        private static DailyPlannedLine ReadPlanned(V value) => new DailyPlannedLine(value.Properties["kind"].StringValue, value.Properties["resourceId"].StringValue, Wide(value.Properties["quantity"]));
        private static V Entry(DailyHistoryEntry value) => Obj(("utcDate", V.String(value.UtcDate)), ("cycleDay", V.Integer(value.CycleDay)), ("claimId", Id(value.ClaimId)),
            ("policyVersion", V.Integer(value.PolicyVersion)), ("lines", Lines(value.Lines)));
        private static DailyHistoryEntry ReadEntry(V value)
        {
            var p = value.Properties;
            return new DailyHistoryEntry(p["utcDate"].StringValue, checked((int)p["cycleDay"].IntegerValue), Id(p["claimId"]), checked((int)p["policyVersion"].IntegerValue),
                p["lines"].Items.Select(G3RewardReceiptMessagePackCodec.ReadLine).ToArray());
        }
        private static V Lines(IReadOnlyList<ILiveSliceRewardLine> lines) => V.Array(lines.Select(G3RewardReceiptMessagePackCodec.WriteLine));
        private static V Obj(params (string, V)[] fields) => V.Object(fields.Select(x => new KeyValuePair<string, V>(x.Item1, x.Item2)).ToArray());
        private static V Id(Guid value) => V.String(value.ToString("D"));
        private static Guid Id(V value) => Guid.ParseExact(value.StringValue, "D");
        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));
        private static long Wide(V value) => long.Parse(value.StringValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
}
