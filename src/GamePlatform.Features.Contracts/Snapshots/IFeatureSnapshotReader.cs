using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Snapshots
{

    public interface IFeatureSnapshotReader<in TQuery, TSnapshot> where TSnapshot : class
    {
        FeatureSnapshot<TSnapshot> Read(ScopedOwnerContext owner, TQuery query);
    }
}
