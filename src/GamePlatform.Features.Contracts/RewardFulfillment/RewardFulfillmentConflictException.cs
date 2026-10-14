using System;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{
    public sealed class RewardFulfillmentConflictException : InvalidOperationException
    {
        public RewardFulfillmentConflictException(string reason) : base(reason) { }
    }
}
