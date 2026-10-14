using System;
using System.Text;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
namespace GamePlatform.Features.Friends
{
    internal static class FriendsCache
    {
        internal const string Epoch = "epoch";
        internal static readonly string[] Boards = { "players-monthly", "players-daily", "players-weekly" };
        internal static string Namespace(Guid playerId) => "friends:" + playerId.ToString("D");
        internal static long Floor(DurableFeatureState? value) => ReadMetadata(value, 0);
        internal static long GraphFloor(DurableFeatureState? value) => ReadMetadata(value, 8);
        private static long ReadMetadata(DurableFeatureState? value, int offset)
        {
            if (value == null)
                return 0;
            var bytes = value.CopyPayload();
            if (bytes.Length != 16)
                throw new InvalidOperationException("Corrupt friends cache fence.");
            long number = BitConverter.ToInt64(bytes, offset);
            if (number < 0)
                throw new InvalidOperationException("Corrupt friends cache fence.");
            return number;
        }
        internal static void Invalidate(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner, Guid playerId, long requiredRevision = 0, long graphRevision = 0)
        {
            if (playerId == Guid.Empty || requiredRevision < 0 || graphRevision < 0)
                throw new ArgumentException("Invalid friends fence.");
            string ns = Namespace(playerId);
            var prior = state.Read(tx, owner, FriendsPlayerBinding.Namespace, Epoch);
            Guid bound = FriendsPlayerBinding.Load(tx, state, owner, Guid.Empty);
            if (bound != Guid.Empty && bound != playerId)
                throw new InvalidOperationException("Friends invalidation player mismatch.");
            if (bound != Guid.Empty)
            {
                foreach (string key in FriendsValidation.Views)
                    state.Delete(tx, owner, ns, key);
                foreach (string board in Boards)
                    state.Delete(tx, owner, ns, "ranks:" + board);
            }
            var metadata = new byte[16];
            BitConverter.GetBytes(Math.Max(Floor(prior), requiredRevision)).CopyTo(metadata, 0);
            BitConverter.GetBytes(Math.Max(GraphFloor(prior), graphRevision)).CopyTo(metadata, 8);
            state.Upsert(tx, new DurableFeatureMutation(owner, FriendsPlayerBinding.Namespace, Epoch, checked((prior?.Revision ?? 0) + 1), 0, metadata, Array.Empty<byte>()));
        }
        internal static void CheckEpoch(DurableFeatureState? current, long expected)
        {
            if ((current?.Revision ?? 0) != expected)
                throw new FriendsOperationException(new Backend.Contracts.Remote.RemoteFailure(Backend.Contracts.Remote.RemoteFailureKind.Conflict), "friends.refresh_required");
        }
        internal static void InvalidateOwner(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner)
        {
            Guid bound = FriendsPlayerBinding.Load(tx, state, owner, Guid.Empty);
            if (bound != Guid.Empty)
            {
                Invalidate(tx, state, owner, bound);
                return;
            }
            var prior = state.Read(tx, owner, FriendsPlayerBinding.Namespace, Epoch);
            var metadata = new byte[16];
            BitConverter.GetBytes(Floor(prior)).CopyTo(metadata, 0);
            BitConverter.GetBytes(GraphFloor(prior)).CopyTo(metadata, 8);
            state.Upsert(tx, new DurableFeatureMutation(owner, FriendsPlayerBinding.Namespace, Epoch, checked((prior?.Revision ?? 0) + 1), 0, metadata, Array.Empty<byte>()));
        }
        internal static bool Matches(DurableFeatureState row, string query) => Encoding.UTF8.GetString(row.CopyExtensions()) == query;
        internal static bool Fresh(long requestStarted, long now, long serverTime, long expiry) => now >= requestStarted && expiry > serverTime && now - requestStarted < expiry - serverTime;
        internal static byte[] Context(string query) => Encoding.UTF8.GetBytes(query);
    }
}
