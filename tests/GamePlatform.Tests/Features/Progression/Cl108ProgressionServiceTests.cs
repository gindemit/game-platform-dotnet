#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Progression;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Features.Progression
{
    /// <summary>Uses the pinned native SQLite implementation; the codec is a deliberately narrow test-only frozen-command seam.</summary>
    public sealed class Cl108ProgressionServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000108"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000109"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("progression-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("progression-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 7);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-0000-7000-8000-000000000110"));
        private static readonly IReadOnlyList<SqliteMigration> Migrations = new[] { SqliteOutboxMigration.Create(1), SqliteFeatureStateMigration.Create(2) };

        [Fact]
        public async Task RealSqliteLocalCompletionAndOutboxCommitTogetherAndSurviveSoftRestart()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var request = Request("0199f9a0-0000-7000-8000-000000000111", "run-a", 3);
            var pending = await service.CompleteAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, pending.State); Assert.Single(pending.Value!.PendingCompletions);
            var rows = await database.ExecuteAsync(Scope, transaction => { var sql = (SqliteTransactionSession)transaction; return (sql.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), sql.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='progression'")); }, CancellationToken.None);
            Assert.Equal(1, rows.Item1); Assert.Equal(1, rows.Item2);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var reopened = await OpenReady(files.Path, false); var restored = Create(reopened, Context);
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, cached.State); Assert.Equal(3, cached.Value!.PendingCompletions.Single().ContentVersion);
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task DuplicateCallbackIsIdempotentButSameRunOrOperationWithDifferentPayloadFailsClosed()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var one = Request("0199f9a0-0000-7000-8000-000000000112", "run-b", 1);
            await service.CompleteAsync(one, CancellationToken.None); await service.CompleteAsync(one, CancellationToken.None);
            await Assert.ThrowsAsync<ProgressionConflictException>(() => service.CompleteAsync(Request("0199f9a0-0000-7000-8000-000000000113", "run-b", 2), CancellationToken.None));
            await Assert.ThrowsAsync<ProgressionConflictException>(() => service.CompleteAsync(Request("0199f9a0-0000-7000-8000-000000000112", "run-c", 2), CancellationToken.None));
            var count = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), CancellationToken.None);
            Assert.Equal(1, count); service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task LostAckRetainsPendingUntilExplicitAcceptedThenPullConfirmationWithoutAllocatingValue()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var request = Request("0199f9a0-0000-7000-8000-000000000114", "run-c", 1);
            await service.CompleteAsync(request, CancellationToken.None);
            await service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, Projection(1, 10), Array.Empty<OperationId>()), CancellationToken.None);
            Assert.Equal(ProgressionPendingStatus.AwaitingReceipt, (await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.PendingCompletions.Single().Status);
            await service.MarkAcceptedAwaitingPullAsync(new ProgressionAcceptance(Context, request.OperationId), CancellationToken.None);
            var unrelated = Request("0199f9a0-0000-7000-8000-000000000120", "run-c-unrelated", 1);
            await service.CompleteAsync(unrelated, CancellationToken.None);
            var accepted = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(2, accepted.Value!.PendingCompletions.Count);
            Assert.Equal(ProgressionPendingStatus.AcceptedAwaitingPull, accepted.Value.PendingCompletions.Single(value => value.OperationId == request.OperationId).Status);
            Assert.Equal(10, accepted.Value.Confirmed!.States.Single().Value);
            await service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, Projection(2, 11), new[] { request.OperationId }), CancellationToken.None);
            var confirmed = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Single(confirmed.Value!.PendingCompletions); Assert.Equal(unrelated.OperationId, confirmed.Value.PendingCompletions.Single().OperationId);
            Assert.Equal(11, confirmed.Value.Confirmed!.States.Single().Value);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task UnknownOrPrematureConfirmationIsRejectedBeforeProjectionOrPendingMutation()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var awaiting = Request("0199f9a0-0000-7000-8000-000000000121", "run-confirmation", 1);
            await service.CompleteAsync(awaiting, CancellationToken.None);
            var unknown = new OperationId(Guid.Parse("0199f9a0-0000-7000-8000-000000000122"));
            await Assert.ThrowsAsync<ProgressionConflictException>(() => service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, Projection(1, 10), new[] { unknown }), CancellationToken.None));
            await Assert.ThrowsAsync<ProgressionConflictException>(() => service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, Projection(1, 10), new[] { awaiting.OperationId }), CancellationToken.None));
            var unchanged = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Null(unchanged.Value!.Confirmed); Assert.Single(unchanged.Value.PendingCompletions);
            Assert.Equal(ProgressionPendingStatus.AwaitingReceipt, unchanged.Value.PendingCompletions.Single().Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task TerminalRejectionPreservesOtherPendingWorkAndPermitsNextSequence()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var rejected = Request("0199f9a0-0000-7000-8000-000000000115", "run-d", 1);
            var retained = Request("0199f9a0-0000-7000-8000-000000000116", "run-e", 1);
            await service.CompleteAsync(rejected, CancellationToken.None); await service.CompleteAsync(retained, CancellationToken.None);
            await service.RejectAsync(Context, rejected.OperationId, CancellationToken.None);
            var next = await service.CompleteAsync(Request("0199f9a0-0000-7000-8000-000000000117", "run-f", 1), CancellationToken.None);
            Assert.Equal(2, next.Value!.PendingCompletions.Count); Assert.DoesNotContain(next.Value.PendingCompletions, x => x.OperationId == rejected.OperationId);
            var sequence = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton=1"), CancellationToken.None);
            Assert.Equal(4, sequence); service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AccountOrGenerationSwitchCannotReadOrMutatePendingOutcomeAndResetProjectionRetainsIt()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, Context);
            var request = Request("0199f9a0-0000-7000-8000-000000000118", "run-g", 1);
            await service.CompleteAsync(request, CancellationToken.None);
            var switched = new ScopedOwnerContext(Owner, new SemanticId("private"), 8);
            await Assert.ThrowsAsync<ProgressionOwnerMismatchException>(() => service.ReadCachedAsync(switched, CancellationToken.None));
            await Assert.ThrowsAsync<ProgressionOwnerMismatchException>(() => service.ApplyConfirmedAsync(new ProgressionConfirmation(switched, Projection(1, 10), Array.Empty<OperationId>()), CancellationToken.None));
            await service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, Projection(1, 10), Array.Empty<OperationId>()), CancellationToken.None);
            var reset = new ProgressionConfirmedProjection(2, 11, Array.Empty<ProgressionConfirmedState>());
            await service.ApplyConfirmedAsync(new ProgressionConfirmation(Context, reset, Array.Empty<OperationId>()), CancellationToken.None);
            var snapshot = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Single(snapshot.Value!.PendingCompletions); Assert.Empty(snapshot.Value.Confirmed!.States);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void ContractPreservesSigned64OutcomeValuesAndRejectsLocalRewardShapes()
        {
            var request = Request("0199f9a0-0000-7000-8000-000000000119", "run-h", int.MaxValue, long.MinValue);
            Assert.Equal(long.MinValue, request.Outcome.Score); Assert.Equal(ProgressionOutcomeAuthority.ClientTrustedUnvalidated, request.OutcomeAuthority);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionConfirmedState(new SemanticId("campaign.level"), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionCompletionRequest(Context, request.OperationId, Stream, "run", request.Outcome, 0, ProgressionOutcomeAuthority.ClientTrustedUnvalidated));
        }

        private static ProgressionService Create(SqliteDatabase database, ScopedOwnerContext context) => new ProgressionService(context, Scope, new SqliteDurableFeatureStateStore(database, Scope), database, new SqliteAtomicCommandStore(database, Scope, Owner, new Fingerprint()), new Codec(), () => 100);
        private static ProgressionCompletionRequest Request(string operation, string source, int contentVersion, long score = 7) => new ProgressionCompletionRequest(Context, new OperationId(Guid.Parse(operation)), Stream, source,
            new GameplayOutcome(new PlatformId("session-1"), new PlatformId("mode-1"), new PlatformId("content-1"), new PlatformId("difficulty-1"), true, score, long.MaxValue, 1_000_000, new Dictionary<string, long>(), ""), contentVersion, ProgressionOutcomeAuthority.ClientTrustedUnvalidated);
        private static ProgressionConfirmedProjection Projection(long revision, long value) => new ProgressionConfirmedProjection(revision, 100, new[] { new ProgressionConfirmedState(new SemanticId("campaign.level"), value) });
        private static async Task<SqliteDatabase> OpenReady(string path, bool seed = true)
        {
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            if (seed) await database.ExecuteAsync(Scope, tx => { ((SqliteTransactionSession)tx).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, ready, next_sequence, local_revision, finalized_through) SELECT 1, ?, ?, ?, ?, 1, 1, 0, 0 WHERE NOT EXISTS (SELECT 1 FROM gp_stream_state)", Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString()); return true; }, CancellationToken.None);
            return database;
        }

        private sealed class Fingerprint : ICommandFingerprint
        {
            public int GetFingerprintLength(int version) => version == 1 ? 32 : 0;
            public void Compute(OwnerScope _, OperationId __, ClientStreamId ___, long ____, string _____, int ______, int _______, ReadOnlySpan<byte> body, Span<byte> destination) => SHA256.HashData(body).CopyTo(destination);
        }

        private sealed class Codec : IProgressionStateCodec
        {
            public byte[] EncodeCompletionCommand(PendingProgressionCompletion value) => Text("C", value.Outcome.Content.Value, value.ContentVersion.ToString(), value.Outcome.Score.ToString(), value.OutcomeAuthority.ToString());
            public byte[] EncodePending(IReadOnlyList<PendingProgressionCompletion> values) => Text("P", string.Join(";", values.Select(value => string.Join(",", value.OperationId.ToString(), value.StreamId.ToString(), B(value.BusinessSource), value.ContentVersion, (int)value.OutcomeAuthority, value.LocalRevision, (int)value.Status, B(value.Outcome.Session.Value), B(value.Outcome.Mode.Value), B(value.Outcome.Content.Value), B(value.Outcome.Difficulty.Value), value.Outcome.Success ? "1" : "0", value.Outcome.Score, value.Outcome.DurationTicks, value.Outcome.TicksPerSecond, B(value.Outcome.ValidationReference)))));
            public IReadOnlyList<PendingProgressionCompletion> DecodePending(long _, ReadOnlySpan<byte> payload)
            {
                var items = Parts(payload, 2)[1]; if (string.IsNullOrEmpty(items)) return Array.Empty<PendingProgressionCompletion>();
                return items.Split(';').Select(item => { var p = item.Split(','); var outcome = new GameplayOutcome(new PlatformId(U(p[7])), new PlatformId(U(p[8])), new PlatformId(U(p[9])), new PlatformId(U(p[10])), p[11] == "1", long.Parse(p[12]), long.Parse(p[13]), int.Parse(p[14]), new Dictionary<string, long>(), U(p[15])); return new PendingProgressionCompletion(new OperationId(Guid.Parse(p[0])), new ClientStreamId(Guid.Parse(p[1])), U(p[2]), outcome, int.Parse(p[3]), (ProgressionOutcomeAuthority)int.Parse(p[4]), long.Parse(p[5]), (ProgressionPendingStatus)int.Parse(p[6])); }).ToArray();
            }
            public byte[] EncodeConfirmed(ProgressionConfirmedProjection value) => Text("S", string.Join(";", value.States.Select(state => B(state.StateKey.Value) + "," + state.Value)));
            public ProgressionConfirmedProjection DecodeConfirmed(long revision, long at, ReadOnlySpan<byte> payload) { var part = Parts(payload, 2)[1]; var states = string.IsNullOrEmpty(part) ? Array.Empty<ProgressionConfirmedState>() : part.Split(';').Select(value => { var p = value.Split(','); return new ProgressionConfirmedState(new SemanticId(U(p[0])), long.Parse(p[1])); }).ToArray(); return new ProgressionConfirmedProjection(revision, at, states); }
            private static byte[] Text(params string[] values) => Encoding.UTF8.GetBytes(string.Join("|", values.Select(B)));
            private static string[] Parts(ReadOnlySpan<byte> value, int count) { var all = Encoding.UTF8.GetString(value).Split('|').Select(U).ToArray(); if (all.Length != count) throw new InvalidOperationException("Invalid test codec payload."); return all; }
            private static string B(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); private static string U(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl108", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose()
            {
                for (var attempt = 0; attempt != 5 && Directory.Exists(directory); attempt++)
                {
                    try { Directory.Delete(directory, true); }
                    catch (IOException) when (attempt != 4) { Thread.Sleep(10); }
                    catch (IOException) { return; }
                }
            }
        }
    }
}
