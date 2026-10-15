using System;
using System.Linq;
using GamePlatform.Features.Daily;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Daily;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Daily;
namespace GamePlatform.Tests.Serialization
{
    public sealed class DailyCodecTests
    {
        private static readonly Guid Operation = Guid.Parse("0199f9a0-2000-7000-8000-000000000008");
        [Fact]
        public void StatusRoundTripsThroughTheDurableCodecWithoutPrecisionLoss()
        {
            var codec = new DailyFeatureCodec();
            var value = DailyServiceTests.Status(history: new[] { DailyServiceTests.Entry() });
            var bytes = codec.EncodeStatus(value);
            var decoded = codec.DecodeStatus(bytes);
            Assert.Equal(9_007_199_254_740_993, decoded.Reward[0].Quantity);
            Assert.Equal(("2026-10-15", true, 3, 2, 1_792_147_200_000), (decoded.UtcDate, decoded.Eligible, decoded.CycleDay, decoded.PolicyVersion, decoded.NextUtcDayAtMilliseconds));
            Assert.Equal(("currency", "coins", 9_007_199_254_740_993), (decoded.History[0].Lines[0].Kind, decoded.History[0].Lines[0].ResourceId, decoded.History[0].Lines[0].Quantity));
            Assert.Equal(bytes, codec.EncodeStatus(decoded));
            Assert.Throws<QualificationCodecException>(() => codec.DecodeStatus(bytes.Concat(new byte[] { 0 }).ToArray()));
        }
        [Fact]
        public void ClaimedResultDecodesBesideTheQuestClaimAndRejectsForeignLineKinds()
        {
            var codec = new DailyMessagePackCodec();
            var lines = new ILiveSliceRewardLine[] { new LiveSliceCurrencyRewardLine(0, 1, "coins", 25), new LiveSliceStackRewardLine(1, 1, "seed.daisy", 2) };
            var bytes = codec.EncodeClaimResult(new DailyClaimedResult(DailyServiceTests.ClaimId, Operation, "2026-10-15", 3, 2, lines));
            var entry = new DailyFeatureCodec().DecodeClaimResult(bytes);
            Assert.Equal((DailyServiceTests.ClaimId, "2026-10-15", 3, 2, 2), (entry.ClaimId, entry.UtcDate, entry.CycleDay, entry.PolicyVersion, entry.Lines.Count));
            Assert.Equal(("item_stack", "seed.daisy", 2L), (entry.Lines[1].Kind, entry.Lines[1].ResourceId, entry.Lines[1].Quantity));
            Assert.Throws<QualificationCodecException>(() => codec.EncodeClaimResult(new DailyClaimedResult(DailyServiceTests.ClaimId, Operation, "2026-10-15", 3, 2,
                new ILiveSliceRewardLine[] { new LiveSliceEntitlementRewardLine(0, 1, "vip", null) })));
            Assert.Throws<QualificationCodecException>(() => codec.EncodeClaimResult(new DailyClaimedResult(DailyServiceTests.ClaimId, Operation, "2026-10-15", 8, 2, lines)));
            Assert.Throws<QualificationCodecException>(() => codec.EncodeClaimCommand(new DailyClaimCommand("2026-13-01")));
        }
        [Fact]
        public void ClaimCommandAndEmptyStatusRequestAreClosedShapes()
        {
            var codec = new DailyMessagePackCodec();
            Assert.Equal("2026-10-15", codec.DecodeClaimCommand(codec.EncodeClaimCommand(new DailyClaimCommand("2026-10-15"))).UtcDate);
            Assert.Equal(new byte[] { 0x80 }, codec.EncodeStatusRequest());
        }
    }
}
