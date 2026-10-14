using System;
using System.Text;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
namespace GamePlatform.Features.Friends
{
    internal static class FriendsPlayerBinding
    {
        internal const string Namespace = "friends-owner";
        internal static Guid Load(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner, Guid supplied)
        {
            var row = state.Read(tx, owner, Namespace, "player-binding");
            if (row == null)
                return supplied;
            if (!Guid.TryParseExact(Encoding.UTF8.GetString(row.CopyPayload()), "D", out var stored) || stored == Guid.Empty)
                throw new InvalidOperationException("Corrupt friends player binding.");
            if (supplied != Guid.Empty && supplied != stored)
                throw Conflict();
            return stored;
        }
        internal static Guid Bind(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner, Guid supplied, Guid received)
        {
            if (received == Guid.Empty)
                throw Conflict();
            Guid current = Load(tx, state, owner, supplied);
            if (current != Guid.Empty && current != received)
                throw Conflict();
            if (state.Read(tx, owner, Namespace, "player-binding") == null)
                state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, "player-binding", 0, 0, Encoding.UTF8.GetBytes(received.ToString("D")), Array.Empty<byte>()));
            return received;
        }
        private static FriendsOperationException Conflict() => new FriendsOperationException(new Backend.Contracts.Remote.RemoteFailure(Backend.Contracts.Remote.RemoteFailureKind.Protocol), "friends.player_mismatch");
    }
}
