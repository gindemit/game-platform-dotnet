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
using GamePlatform.Features.Catalog;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Inventory;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Inventory;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Features.Inventory
{
    /// <summary>SQLite cases exercise the pinned native store; the authority and codec are explicit test-only seams.</summary>
    public sealed class Cl104InventoryServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("inventory-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("inventory-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 4);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-0000-7000-8000-000000000004");
        private static readonly PlatformId Definition = new PlatformId("test-item");
        private static readonly PlatformId Instance = new PlatformId("instance-1");
        private static readonly IReadOnlyList<SqliteMigration> Migrations = SqlitePlatformMigrationRegistry.Migrations;

        [Fact]
        public async Task RealSqliteConfirmedStacksInstancesAndPendingIntentSurviveReopenWithoutChangingHoldings()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var service = Create(database, new PermitAuthority());
            var confirmed = Snapshot(1, 7, true);
            await service.ApplyConfirmedAsync(Context, confirmed, Catalog(true), CancellationToken.None);
            var pending = await service.SubmitIntentAsync(Request("0199f9a0-0000-7000-8000-000000000004"), Catalog(true), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, pending.State);
            Assert.Equal(7, pending.Value!.Confirmed!.Stacks.Single().Quantity);
            Assert.Single(pending.Value.PendingIntents);
            var rows = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Outbox: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), State: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='inventory'"));
            }, CancellationToken.None);
            Assert.Equal(1, rows.Outbox); Assert.Equal(2, rows.State);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, false);
            var restored = Create(reopened, new PermitAuthority());
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, cached.State);
            Assert.Equal(7, cached.Value!.Confirmed!.Stacks.Single().Quantity);
            Assert.Equal(Instance, cached.Value.Confirmed.Instances.Single().InstanceId);
            Assert.Equal(new byte[] { 5 }, cached.Value.Confirmed.Instances.Single().CopyAuditExtensions());
            Assert.Single(cached.Value.PendingIntents);
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SemanticRevisionZeroIsDistinctFromAbsenceReplaysExactlyRejectsConflictAndAdvancesToOne()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            Assert.Equal(FeatureSnapshotState.Missing, (await service.ReadCachedAsync(Context, CancellationToken.None)).State);
            var zero = Snapshot(0, 2, false);
            await service.ApplyConfirmedAsync(Context, zero, Catalog(true), CancellationToken.None);
            Assert.Equal(0L, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE feature_namespace='inventory' AND entity_key='confirmed'"), CancellationToken.None));
            var installed = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(0, installed.Revision); Assert.Equal(0, installed.Value!.Confirmed!.Revision);
            await service.ApplyConfirmedAsync(Context, zero, Catalog(true), CancellationToken.None);
            await Assert.ThrowsAsync<InventoryProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, Snapshot(0, 3, false), Catalog(true), CancellationToken.None));
            await service.ApplyConfirmedAsync(Context, Snapshot(1, 4, false), Catalog(true), CancellationToken.None);
            Assert.Equal(1, (await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.Confirmed!.Revision);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task OlderSameRevisionAndUnboundCatalogFailClosedWhileDistinctViewsRemainIndependent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            await service.ApplyConfirmedAsync(Context, Snapshot(2, 3, false), Catalog(true), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, Snapshot(1, 8, false), Catalog(true), CancellationToken.None);
            Assert.Equal(3, (await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.Confirmed!.Stacks.Single().Quantity);
            await Assert.ThrowsAsync<InventoryProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, Snapshot(2, 4, false), Catalog(true), CancellationToken.None));
            await Assert.ThrowsAsync<InventoryProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, Snapshot(3, 4, false), Catalog(false), CancellationToken.None));
            var other = new ScopedOwnerContext(Owner, new SemanticId("other-view"), 1);
            var otherScope = new StorageScope("inventory-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
            using var second = new InventoryService(other, otherScope, new SqliteDurableFeatureStateStore(database, otherScope), database,
                new SqliteAtomicCommandStore(database, otherScope, Owner, new Fingerprint(), new FixedClock()), new Codec(), new DenyAuthority(), () => 100);
            await second.ApplyConfirmedAsync(other, Snapshot(1, 9, false), Catalog(true, other), CancellationToken.None);
            Assert.Equal(3, (await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.Confirmed!.Stacks.Single().Quantity);
            Assert.Equal(9, (await second.ReadCachedAsync(other, CancellationToken.None)).Value!.Confirmed!.Stacks.Single().Quantity);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SameRevisionWithChangedInstancePayloadOrAuditBytesFailsClosed()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            await service.ApplyConfirmedAsync(Context, Snapshot(5, 3, true), Catalog(true), CancellationToken.None);
            var changedPayload = new InventoryConfirmedSnapshot(5, 100, new[] { new InventoryStack(Definition, 3, 5) },
                new[] { new InventoryInstance(Instance, Definition, 5, InventoryInstanceState.Active, new byte[] { 8 }, new byte[] { 5 }) });
            var changedAudit = new InventoryConfirmedSnapshot(5, 100, new[] { new InventoryStack(Definition, 3, 5) },
                new[] { new InventoryInstance(Instance, Definition, 5, InventoryInstanceState.Active, new byte[] { 9 }, new byte[] { 4 }) });
            await Assert.ThrowsAsync<InventoryProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, changedPayload, Catalog(true), CancellationToken.None));
            await Assert.ThrowsAsync<InventoryProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, changedAudit, Catalog(true), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AcceptedReceiptPreservesUntilItsAuthoritativeRevisionAndLeavesUnrelatedPendingIntent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new PermitAuthority());
            await service.ApplyConfirmedAsync(Context, Snapshot(1, 2, false), Catalog(true), CancellationToken.None);
            var accepted = Request("0199f9a0-0000-7000-8000-000000000007", "inventory-use-a");
            var unrelated = Request("0199f9a0-0000-7000-8000-000000000008", "inventory-use-b");
            await service.SubmitIntentAsync(accepted, Catalog(true), CancellationToken.None);
            await service.SubmitIntentAsync(unrelated, Catalog(true), CancellationToken.None);
            var awaiting = await service.MarkAcceptedAwaitingPullAsync(new InventoryIntentAcceptance(Context, accepted.OperationId, 3), CancellationToken.None);
            Assert.Equal(InventoryIntentStatus.AcceptedAwaitingPull, awaiting.Value!.PendingIntents.Single(value => value.OperationId == accepted.OperationId).Status);
            Assert.Equal(2, awaiting.Value.Confirmed!.Stacks.Single().Quantity);

            await service.ApplyConfirmedAsync(Context, Snapshot(2, 4, false), Catalog(true), CancellationToken.None);
            var older = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(2, older.Value!.PendingIntents.Count);
            Assert.Equal(InventoryIntentStatus.AcceptedAwaitingPull, older.Value.PendingIntents.Single(value => value.OperationId == accepted.OperationId).Status);

            await service.ApplyConfirmedAsync(Context, Snapshot(3, 5, false), Catalog(true), CancellationToken.None);
            var confirmed = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Single(confirmed.Value!.PendingIntents);
            Assert.Equal(unrelated.OperationId, confirmed.Value.PendingIntents.Single().OperationId);
            Assert.Equal(5, confirmed.Value.Confirmed!.Stacks.Single().Quantity);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SameRevisionReorderedStacksAndInstancesIsEquivalent()
        {
            var secondDefinition = new PlatformId("test-item-2"); var secondInstance = new PlatformId("instance-2");
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            var first = new InventoryConfirmedSnapshot(5, 100,
                new[] { new InventoryStack(Definition, 2, 5), new InventoryStack(secondDefinition, 7, 5) },
                new[] { new InventoryInstance(Instance, Definition, 5, InventoryInstanceState.Active, new byte[] { 1 }, new byte[] { 2 }), new InventoryInstance(secondInstance, secondDefinition, 5, InventoryInstanceState.Active, new byte[] { 3 }, new byte[] { 4 }) });
            var reordered = new InventoryConfirmedSnapshot(5, 100,
                new[] { new InventoryStack(secondDefinition, 7, 5), new InventoryStack(Definition, 2, 5) },
                new[] { new InventoryInstance(secondInstance, secondDefinition, 5, InventoryInstanceState.Active, new byte[] { 3 }, new byte[] { 4 }), new InventoryInstance(Instance, Definition, 5, InventoryInstanceState.Active, new byte[] { 1 }, new byte[] { 2 }) });
            await service.ApplyConfirmedAsync(Context, first, Catalog(true, Context, secondDefinition), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, reordered, Catalog(true, Context, secondDefinition), CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedSemanticRevisionZeroProjectionRollsBackCommitsAndReopensWithCursorSideEffect()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cursor_side_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, null, Snapshot(0, 2, false), Catalog(true));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cursor_side_sentinel VALUES (1)");
                throw new InvalidOperationException("simulate cursor failure");
            }, CancellationToken.None));
            var rolledBack = await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cursor_side_sentinel") + ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='inventory'"), CancellationToken.None);
            Assert.Equal(0, rolledBack);
            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, null, Snapshot(0, 2, false), Catalog(true));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cursor_side_sentinel VALUES (1)"); return true;
            }, CancellationToken.None);
            var committed = await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cursor_side_sentinel") + ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='inventory'"), CancellationToken.None);
            Assert.Equal(2, committed);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var reopened = await OpenReady(files.Path, false); var restored = Create(reopened, new DenyAuthority());
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(0, cached.Revision); Assert.Equal(0, cached.Value!.Confirmed!.Revision);
            Assert.Equal(1, await reopened.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cursor_side_sentinel"), CancellationToken.None));
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedProjectionCommitSuppressesMatchingAcceptedIntentAfterReopenButRetainsUnrelatedAwaitingIntent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new PermitAuthority());
            await service.ApplyConfirmedAsync(Context, Snapshot(0, 2, false), Catalog(true), CancellationToken.None);
            var accepted = Request("0199f9a0-0000-7000-8000-000000000009", "inventory-borrowed-a");
            var unrelated = Request("0199f9a0-0000-7000-8000-000000000010", "inventory-borrowed-b");
            await service.SubmitIntentAsync(accepted, Catalog(true), CancellationToken.None);
            await service.SubmitIntentAsync(unrelated, Catalog(true), CancellationToken.None);
            await service.MarkAcceptedAwaitingPullAsync(new InventoryIntentAcceptance(Context, accepted.OperationId, 1), CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cursor_side_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, 0, Snapshot(1, 5, false), Catalog(true));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cursor_side_sentinel VALUES (1)"); return true;
            }, CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, false); var restored = Create(reopened, new PermitAuthority());
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(5, cached.Value!.Confirmed!.Stacks.Single().Quantity);
            Assert.Single(cached.Value.PendingIntents);
            Assert.Equal(unrelated.OperationId, cached.Value.PendingIntents.Single().OperationId);
            Assert.Equal(InventoryIntentStatus.AwaitingReceipt, cached.Value.PendingIntents.Single().Status);
            Assert.Equal(1, await reopened.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cursor_side_sentinel WHERE value=1"), CancellationToken.None));
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task OfflineDeniedAndReplayCannotCreatePendingOrConsumeConfirmedQuantity()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            await service.ApplyConfirmedAsync(Context, Snapshot(1, 2, false), Catalog(true), CancellationToken.None);
            await Assert.ThrowsAsync<InventoryIntentRejectedException>(() => service.SubmitIntentAsync(Request("0199f9a0-0000-7000-8000-000000000005"), Catalog(true), CancellationToken.None));
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(2, cached.Value!.Confirmed!.Stacks.Single().Quantity); Assert.Empty(cached.Value.PendingIntents);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopen = await OpenReady(files.Path, false); var permitted = Create(reopen, new PermitAuthority());
            var request = Request("0199f9a0-0000-7000-8000-000000000006");
            await permitted.SubmitIntentAsync(request, Catalog(true), CancellationToken.None);
            await permitted.SubmitIntentAsync(request, Catalog(true), CancellationToken.None);
            var count = await reopen.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), CancellationToken.None);
            Assert.Equal(1, count);
            var rejected = await permitted.RejectIntentAsync(Context, request.OperationId, CancellationToken.None);
            Assert.Empty(rejected.Value!.PendingIntents); Assert.Equal(2, rejected.Value.Confirmed!.Stacks.Single().Quantity);
            permitted.Dispose(); Assert.True(await reopen.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void CheckedQuantitiesDuplicateIdentitiesAndTombstonesAreBoundedByContracts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryStack(Definition, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryStack(Definition, long.MinValue, 1));
            Assert.Throws<ArgumentException>(() => new InventoryConfirmedSnapshot(1, 1, new[] { new InventoryStack(Definition, 1, 1), new InventoryStack(Definition, 2, 2) }, Array.Empty<InventoryInstance>()));
            Assert.Throws<ArgumentException>(() => new InventoryInstance(Instance, Definition, 1, InventoryInstanceState.Tombstoned, new byte[] { 1 }, Array.Empty<byte>()));
            var tombstone = new InventoryInstance(Instance, Definition, 2, InventoryInstanceState.Tombstoned, Array.Empty<byte>(), Array.Empty<byte>());
            Assert.Equal(InventoryInstanceState.Tombstoned, tombstone.State);
        }

        [Fact]
        public async Task SemanticRevisionMaximumPersistsExactly()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path); var service = Create(database, new DenyAuthority());
            var exhausted = new InventoryConfirmedSnapshot(long.MaxValue, 100,
                new[] { new InventoryStack(Definition, 1, long.MaxValue) }, Array.Empty<InventoryInstance>());
            await service.ApplyConfirmedAsync(Context, exhausted, Catalog(true), CancellationToken.None);
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(long.MaxValue, cached.Revision); Assert.Equal(long.MaxValue, cached.Value!.Confirmed!.Revision);
            Assert.Equal(long.MaxValue, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE feature_namespace='inventory' AND entity_key='confirmed'"), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static InventoryService Create(SqliteDatabase database, IInventoryIntentAuthority authority) => new InventoryService(Context, Scope,
            new SqliteDurableFeatureStateStore(database, Scope), database, new SqliteAtomicCommandStore(database, Scope, Owner, new Fingerprint(), new FixedClock()), new Codec(), authority, () => 100);
        private static InventoryConfirmedSnapshot Snapshot(long revision, long quantity, bool instance) => new InventoryConfirmedSnapshot(revision, 100,
            new[] { new InventoryStack(Definition, quantity, revision) }, instance ? new[] { new InventoryInstance(Instance, Definition, revision, InventoryInstanceState.Active, new byte[] { 9 }, new byte[] { 5 }) } : Array.Empty<InventoryInstance>());
        private static CatalogSnapshot Catalog(bool visible, ScopedOwnerContext? context = null, PlatformId? secondDefinition = null) => new CatalogSnapshot(new CatalogSnapshotRequest(context ?? Context, 1, "en", "adult"), 1, 1, 100,
            visible ? (secondDefinition.HasValue ? new[] { Item(Definition, "test.item"), Item(secondDefinition.Value, "test.item.2") } : new[] { Item(Definition, "test.item") }) : Array.Empty<CatalogEntry>());
        private static CatalogEntry Item(PlatformId definition, string key) => new CatalogEntry(new CatalogDefinition(definition, CatalogResourceKind.Item, new SemanticId(key), 1, 1, Array.Empty<byte>()), new CatalogAppBinding(definition, 1, true, true, false, false, false, Array.Empty<byte>()));
        private static InventoryIntentRequest Request(string operation, string source = "inventory-use") => new InventoryIntentRequest(Context, new OperationId(Guid.Parse(operation)), Stream, source, InventoryIntentKind.Use, InventoryEntryKind.Stack, Definition);
        private static async Task<SqliteDatabase> OpenReady(string path, bool seed = true)
        {
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            if (seed) await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) SELECT 1, ?, ?, ?, ?, ?, 1, 1, 0, 0 WHERE NOT EXISTS (SELECT 1 FROM gp_stream_state)", Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString(), Installation.ToString("D")); return true;
            }, CancellationToken.None);
            return database;
        }

        private sealed class PermitAuthority : IInventoryIntentAuthority { public InventoryIntentAuthorization Authorize(InventoryIntentRequest _) => new InventoryIntentAuthorization(true, "inventory.use", "online"); }
        private sealed class DenyAuthority : IInventoryIntentAuthority { public InventoryIntentAuthorization Authorize(InventoryIntentRequest _) => new InventoryIntentAuthorization(false, null, "inventory_offline_denied"); }
        private sealed class Fingerprint : ICommandFingerprint { public int GetFingerprintLength(int version) => version == 1 ? 32 : 0; public void Compute(OwnerScope _, OperationId __, ClientStreamId ___, Guid ____, long _____, string ______, int _______, int ________, long _________, ReadOnlySpan<byte> body, Span<byte> destination) => SHA256.HashData(body).CopyTo(destination); }
        private sealed class FixedClock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1_789_555_200_000; }
        private sealed class Codec : IInventoryStateCodec
        {
            public byte[] EncodeConfirmed(InventoryConfirmedSnapshot value) => Text("C", string.Join(",", value.Stacks.Select(stack => stack.DefinitionId.Value + ":" + stack.Quantity + ":" + stack.Revision)), string.Join(",", value.Instances.Select(instance => instance.InstanceId.Value + ":" + instance.DefinitionId.Value + ":" + instance.Revision + ":" + (int)instance.State + ":" + Convert.ToBase64String(instance.CopyPayload()) + ":" + Convert.ToBase64String(instance.CopyAuditExtensions()))));
            public InventoryConfirmedSnapshot DecodeConfirmed(long revision, long at, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions)
            {
                var p = Parts(payload, 3); var stacks = string.IsNullOrEmpty(p[1]) ? Array.Empty<InventoryStack>() : p[1].Split(',').Select(value => { var a = value.Split(':'); return new InventoryStack(new PlatformId(a[0]), long.Parse(a[1]), long.Parse(a[2])); });
                var instances = string.IsNullOrEmpty(p[2]) ? Array.Empty<InventoryInstance>() : p[2].Split(',').Select(value => { var a = value.Split(':'); return new InventoryInstance(new PlatformId(a[0]), new PlatformId(a[1]), long.Parse(a[2]), (InventoryInstanceState)int.Parse(a[3]), Convert.FromBase64String(a[4]), Convert.FromBase64String(a[5])); });
                return new InventoryConfirmedSnapshot(revision, at, stacks, instances);
            }
            public byte[] EncodePending(IReadOnlyList<InventoryIntent> values) => Text("P", string.Join(",", values.Select(value => value.OperationId + ":" + value.StreamId + ":" + value.BusinessSource + ":" + (int)value.Kind + ":" + (int)value.TargetKind + ":" + value.TargetId.Value + ":" + value.ExpectedInventoryRevision + ":" + value.LocalRevision + ":" + (int)value.Status + ":" + (value.AcceptedInventoryRevision?.ToString() ?? string.Empty))));
            public IReadOnlyList<InventoryIntent> DecodePending(long _, ReadOnlySpan<byte> payload)
            {
                var p = Parts(payload, 2); if (string.IsNullOrEmpty(p[1])) return Array.Empty<InventoryIntent>(); return p[1].Split(',').Select(value => { var a = value.Split(':'); return new InventoryIntent(new OperationId(Guid.Parse(a[0])), new ClientStreamId(Guid.Parse(a[1])), a[2], (InventoryIntentKind)int.Parse(a[3]), (InventoryEntryKind)int.Parse(a[4]), new PlatformId(a[5]), long.Parse(a[6]), long.Parse(a[7]), (InventoryIntentStatus)int.Parse(a[8]), string.IsNullOrEmpty(a[9]) ? null : long.Parse(a[9])); }).ToArray();
            }
            public byte[] EncodeIntentCommand(InventoryIntent value) => Text("I", value.OperationId.ToString(), value.TargetId.Value, value.ExpectedInventoryRevision.ToString());
            public void ValidateInstanceExtensions(ReadOnlySpan<byte> extensions) { if (extensions.Length > 16_384) throw new InvalidOperationException("Unsupported inventory extension."); }
            private static byte[] Text(params string[] values) => Encoding.UTF8.GetBytes(string.Join("|", values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)))));
            private static string[] Parts(ReadOnlySpan<byte> value, int count) { var values = Encoding.UTF8.GetString(value).Split('|').Select(item => Encoding.UTF8.GetString(Convert.FromBase64String(item))).ToArray(); if (values.Length != count) throw new InvalidOperationException("Invalid test state."); return values; }
        }
        private sealed class TemporaryDatabase : IDisposable { private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl104", Guid.NewGuid().ToString("N")); public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); } public string Path { get; } public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); } }
    }
}
