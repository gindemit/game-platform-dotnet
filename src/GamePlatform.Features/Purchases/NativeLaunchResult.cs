namespace GamePlatform.Features.Purchases
{
    public sealed class NativeLaunchResult
    {
        public NativeLaunchResult(NativeLaunchStatus status, NativeProof? proof = null)
        {
            if ((status == NativeLaunchStatus.Proof && proof == null) ||
                (proof != null && status != NativeLaunchStatus.Proof && status != NativeLaunchStatus.Pending))
                throw new System.ArgumentException("Only purchased or pending results carry proof.");
            Status = status;
            Proof = proof;
        }
        public NativeLaunchStatus Status { get; }
        public NativeProof? Proof { get; }
    }
}
