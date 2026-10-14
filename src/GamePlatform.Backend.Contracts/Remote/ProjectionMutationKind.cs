namespace GamePlatform.Backend.Contracts.Remote
{

    /// <summary>Internal semantic mirror of the frozen private-feed change literal.</summary>
    public enum ProjectionMutationKind
    {
        Upsert, RemoveFromView, Tombstone, Invalidation
    }
}
