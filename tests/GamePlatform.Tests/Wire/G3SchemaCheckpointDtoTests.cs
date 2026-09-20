using System;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Wire
{
    public sealed class G3SchemaCheckpointDtoTests
    {
        [Fact]
        public void CompletionRequiresExplicitUnvalidatedAuthorityAndPreservesNullReward()
        {
            var response = new G3RewardReceiptCompletionResponse(Guid.NewGuid(), null);
            Assert.Equal(2, response.SchemaVersion);
            Assert.Equal("client_trusted_unvalidated", response.OutcomeAuthority);
            Assert.Null(response.Reward);
        }

        [Fact]
        public void ReceiptPreservesSigned64ValuesAndOwnsLineList()
        {
            var source = new LiveSliceRewardSource(new string('a', 64), "fixture.slot", "mrsquare.casual.fixture", 1);
            ILiveSliceRewardLine[] lines = { new LiveSliceCurrencyRewardLine(0, 1, "test.coin", long.MaxValue) };
            var receipt = new LiveSliceRewardReceipt(Guid.NewGuid(), Guid.NewGuid(), long.MaxValue,
                253402300799999L, source, "test.coin.one", 1, lines);
            lines[0] = new LiveSliceStackRewardLine(0, 1, "other", 1);

            Assert.Equal(long.MaxValue, receipt.FeedRevision);
            var line = Assert.IsType<LiveSliceCurrencyRewardLine>(Assert.Single(receipt.Lines));
            Assert.Equal(long.MaxValue, line.Quantity);
        }
    }
}
