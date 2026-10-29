namespace GamePlatform.Features.Store
{
    public sealed class StorefrontLine
    {
        public StorefrontLine(string kind, string resourceId, long quantity)
        {
            Kind = kind;
            ResourceId = resourceId;
            Quantity = quantity;
        }
        public string Kind { get; }
        public string ResourceId { get; }
        public long Quantity { get; }
    }
}
