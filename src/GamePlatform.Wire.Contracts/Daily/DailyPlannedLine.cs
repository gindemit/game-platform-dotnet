using System;
namespace GamePlatform.Wire.Contracts.Daily
{
    public sealed class DailyPlannedLine
    {
        public DailyPlannedLine(string kind, string resourceId, long quantity)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
            Quantity = quantity;
        }
        public string Kind { get; }
        public string ResourceId { get; }
        public long Quantity { get; }
    }
}
