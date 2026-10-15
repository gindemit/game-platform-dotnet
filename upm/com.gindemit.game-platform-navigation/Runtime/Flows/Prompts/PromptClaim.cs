namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>An exclusive dispatch claim. Resolve or release it explicitly after the owning flow acts.</summary>
    public sealed class PromptClaim
    {
        internal PromptClaim(PromptRequest request, long queueId, long claimId)
        { Request = request; QueueId = queueId; ClaimId = claimId; }
        public PromptRequest Request { get; }
        internal long QueueId { get; }
        internal long ClaimId { get; }
    }
}
