using System;
namespace GamePlatform.Features.Daily
{
    public sealed class DailyPlannedLine
    {
        public DailyPlannedLine(string kind, string resourceId, long quantity)
        {
            if (kind != "currency" && kind != "item_stack")
                throw new ArgumentException("A planned line is a currency or item stack.", nameof(kind));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            Kind = kind;
            ResourceId = DailyValidation.Key(resourceId, nameof(resourceId));
            Quantity = quantity;
        }
        public string Kind { get; }
        public string ResourceId { get; }
        public long Quantity { get; }
    }
}
