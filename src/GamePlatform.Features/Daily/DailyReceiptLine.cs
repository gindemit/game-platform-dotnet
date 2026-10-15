using System;
namespace GamePlatform.Features.Daily
{
    public sealed class DailyReceiptLine
    {
        public DailyReceiptLine(int lineIndex, int definitionVersion, string kind, string resourceId, long quantity)
        {
            if (lineIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (definitionVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (kind != "currency" && kind != "item_stack")
                throw new ArgumentException("A receipt line is a currency or item stack.", nameof(kind));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            LineIndex = lineIndex;
            DefinitionVersion = definitionVersion;
            Kind = kind;
            ResourceId = DailyValidation.Key(resourceId, nameof(resourceId));
            Quantity = quantity;
        }
        public int LineIndex { get; }
        public int DefinitionVersion { get; }
        public string Kind { get; }
        public string ResourceId { get; }
        public long Quantity { get; }
    }
}
