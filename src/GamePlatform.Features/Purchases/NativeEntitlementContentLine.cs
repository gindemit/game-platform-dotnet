namespace GamePlatform.Features.Purchases
{
    /// <summary>A permanent right. It has no quantity and is never consumed.</summary>
    public sealed class NativeEntitlementContentLine : NativeContentLine
    {
        public NativeEntitlementContentLine(string resourceId, int? lineIndex = null, int? definitionVersion = null)
            : base(resourceId, lineIndex, definitionVersion) { }
    }
}
