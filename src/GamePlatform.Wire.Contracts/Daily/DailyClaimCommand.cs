using System;
namespace GamePlatform.Wire.Contracts.Daily
{
    public sealed class DailyClaimCommand : IPushPayload
    {
        public DailyClaimCommand(string utcDate)
        {
            UtcDate = utcDate ?? throw new ArgumentNullException(nameof(utcDate));
        }
        public string UtcDate { get; }
    }
}
