namespace GamePlatform.Features.Contracts.Snapshots
{

    public enum FeatureSnapshotState
    {
        Missing = 0,
        Available = 1,
        Stale = 2,
        Pending = 3,
        Unavailable = 4,
        Error = 5
    }
}
