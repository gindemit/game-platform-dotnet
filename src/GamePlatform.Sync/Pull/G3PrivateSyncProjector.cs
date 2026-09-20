#nullable enable
using System;
using System.Collections.Generic;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Sync.Pull
{
    /// <summary>Consumer-owned typed installation boundary for the deliberately narrow G3 projection set.</summary>
    public interface IG3PrivateProjectionSink
    {
        void ReplaceSnapshot(ILocalStorageTransaction transaction, PrivateSyncProjectionObservation observation,
            ProfileProfile? profile, IReadOnlyList<ProjectionProgressionSnapshot> progression,
            IReadOnlyList<ProjectionWalletSnapshot> wallet);
        void ApplyGroup(ILocalStorageTransaction transaction, long feedRevision, PrivateSyncProjectionObservation observation,
            IReadOnlyList<IProjectionChange> changes);
    }

    /// <summary>
    /// Decodes raw, already-authorized projection bytes into frozen typed DTOs inside the cursor transaction.
    /// Inventory and entitlement collections must remain empty for G3 because their frozen rows lack the richer
    /// catalog/origin fields required by CL-104/106; non-empty rows fail closed instead of being fabricated.
    /// </summary>
    public sealed class G3PrivateSyncProjector : IObservedPrivateSyncProjectionProjector
    {
        private readonly IWireCodec codec; private readonly IG3PrivateProjectionSink sink;
        public G3PrivateSyncProjector(IWireCodec codec, IG3PrivateProjectionSink sink)
        { this.codec=codec??throw new ArgumentNullException(nameof(codec));this.sink=sink??throw new ArgumentNullException(nameof(sink)); }

        public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group) =>
            throw new InvalidOperationException("G3 projection requires fixed-boundary and server-time observation.");
        public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot) =>
            throw new InvalidOperationException("G3 projection requires fixed-boundary and server-time observation.");

        public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group, PrivateSyncProjectionObservation observation)
        {
            if(transaction==null)throw new ArgumentNullException(nameof(transaction));if(group==null)throw new ArgumentNullException(nameof(group));
            var typed=new IProjectionChange[group.Changes.Count];
            for(var i=0;i<typed.Length;i++)typed[i]=DecodeChange(group.Changes[i]);
            sink.ApplyGroup(transaction,group.Revision,observation,Array.AsReadOnly(typed));
        }

        public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot, PrivateSyncProjectionObservation observation)
        {
            if(transaction==null)throw new ArgumentNullException(nameof(transaction));if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            ProfileProfile? profile=null;var progression=new List<ProjectionProgressionSnapshot>();var wallet=new List<ProjectionWalletSnapshot>();
            foreach(var mutation in snapshot)
            {
                if(mutation==null||mutation.Kind!=StoredProjectionKind.Upsert)throw new InvalidOperationException("A G3 snapshot contains an invalid mutation.");
                var value=codec.Decode<IProjectionSnapshotEntity>(mutation.CopyPayload());
                switch(value)
                {
                    case ProjectionProfileSnapshot item:
                        Match(mutation,item.Collection,item.EntityKey.Profile,item.Revision);if(profile!=null)throw new InvalidOperationException("The G3 snapshot contains more than one profile.");profile=item.Data;break;
                    case ProjectionProgressionSnapshot item:
                        Match(mutation,item.Collection,item.EntityKey.StateKey,item.Revision);progression.Add(item);break;
                    case ProjectionWalletSnapshot item:
                        Match(mutation,item.Collection,item.EntityKey.CurrencyId,item.Revision);wallet.Add(item);break;
                    case ProjectionInventorySnapshot:
                    case ProjectionEntitlementSnapshot:
                        throw new InvalidOperationException("G3 inventory and entitlement collections must remain authoritatively empty.");
                    default:throw new InvalidOperationException("The G3 snapshot entity is unsupported.");
                }
            }
            sink.ReplaceSnapshot(transaction,observation,profile,progression.AsReadOnly(),wallet.AsReadOnly());
        }

        private IProjectionChange DecodeChange(StoredProjectionMutation mutation)
        {
            if(mutation==null)throw new ArgumentNullException(nameof(mutation));
            if(mutation.Collection=="inventory"||mutation.Collection=="entitlements")throw new InvalidOperationException("G3 inventory and entitlement collections must remain authoritatively empty.");
            IProjectionChange value;
            if(mutation.Kind==StoredProjectionKind.Upsert)value=codec.Decode<IProjectionChange>(mutation.CopyPayload());
            else value=Removal(mutation);
            MatchChange(mutation,value);return value;
        }

        private static IProjectionChange Removal(StoredProjectionMutation value)
        {
            var kind=value.Kind==StoredProjectionKind.RemoveFromView?"view_remove":value.Kind==StoredProjectionKind.Tombstone?"tombstone":value.Kind==StoredProjectionKind.Invalidation?"invalidation":throw new InvalidOperationException("The G3 removal kind is unsupported.");
            switch(value.Collection)
            {
                case "profile":return new ProjectionProfileRemovalValue(new ProjectionProfileKey(),value.Revision,kind);
                case "progression":return new ProjectionProgressionRemoval(new ProjectionProgressionKey(value.EntityKey),value.Revision,kind);
                case "wallet":return new ProjectionWalletRemoval(new ProjectionWalletKey(value.EntityKey),value.Revision,kind);
                default:throw new InvalidOperationException("The G3 collection is unsupported.");
            }
        }

        private static void MatchChange(StoredProjectionMutation raw,IProjectionChange value)
        {
            switch(value)
            {
                case ProjectionProfileUpsert item:Match(raw,item.EntityType,item.EntityKey.Profile,item.Revision);break;
                case ProjectionProgressionUpsert item:Match(raw,item.EntityType,item.EntityKey.StateKey,item.Revision);break;
                case ProjectionWalletUpsert item:Match(raw,item.EntityType,item.EntityKey.CurrencyId,item.Revision);break;
                case ProjectionProfileRemovalValue item:Match(raw,item.EntityType,item.EntityKey.Profile,item.Revision);break;
                case ProjectionProgressionRemoval item:Match(raw,item.EntityType,item.EntityKey.StateKey,item.Revision);break;
                case ProjectionWalletRemoval item:Match(raw,item.EntityType,item.EntityKey.CurrencyId,item.Revision);break;
                default:throw new InvalidOperationException("The G3 projection change is unsupported.");
            }
        }
        private static void Match(StoredProjectionMutation raw,string collection,string key,long revision)
        {if(!string.Equals(raw.Collection,collection,StringComparison.Ordinal)||!string.Equals(raw.EntityKey,key,StringComparison.Ordinal)||raw.Revision!=revision)throw new InvalidOperationException("The typed G3 projection identity does not match its raw envelope.");}
    }
}
