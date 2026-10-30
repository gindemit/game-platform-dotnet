namespace GamePlatform.Features.Purchases
{
    /// <summary>Legacy adapters must enforce this fixed bound before allocation/admission. Growing stores use IPagedProtectedProofStore.</summary>
    public interface IBoundedProtectedProofStore : IProtectedProofStore
    {
        int MaximumRecords { get; }
    }
}
