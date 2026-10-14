using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    public interface IRewardFulfillmentStateCodec
    {
        byte[] EncodeRecord(RewardPresentationRecord record);
        RewardPresentationRecord DecodeRecord(long revision, ReadOnlySpan<byte> payload);
        byte[] EncodeGroup(RewardProjectionGroup group);
        RewardProjectionGroup DecodeGroup(ScopedOwnerContext owner, long revision, ReadOnlySpan<byte> payload);
    }
}
