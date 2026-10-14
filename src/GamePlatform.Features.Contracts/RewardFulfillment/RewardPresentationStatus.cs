using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{
    /// <summary>Durable UI state for a server-issued receipt. It never describes a locally granted balance or right.</summary>
    public enum RewardPresentationStatus
    {
        Pending = 0, AcceptedAwaitingPull = 1, Confirmed = 2, Rejected = 3
    }
}
