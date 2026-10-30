namespace GamePlatform.Features.Purchases
{
    public sealed class NativeContentLine
    {
        public NativeContentLine(string kind, string resourceId, long quantity, int? lineIndex = null, int? definitionVersion = null)
        { Kind = kind; ResourceId = resourceId; Quantity = quantity; LineIndex = lineIndex; DefinitionVersion = definitionVersion; }
        public string Kind { get; }
        public string ResourceId { get; }
        public long Quantity { get; }
        public int? LineIndex { get; }
        public int? DefinitionVersion { get; }
    }
}
