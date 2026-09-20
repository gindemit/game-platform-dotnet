#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Entitlements;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Entitlements;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Tests.Features.Entitlements
{
    /// <summary>Focused tests use the real pinned SQLite executor; the codec is an explicit non-production test seam.</summary>
    public sealed class Cl106EntitlementsServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0600-7000-8000-000000000001"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0600-7000-8000-000000000002"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("entitlements-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("entitlements-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 4);
        private static readonly PlatformId RightId = new PlatformId("premium-pass");
        private static readonly PlatformId ReceiptId = new PlatformId("receipt-1");
        private static readonly PlatformId SecondRightId = new PlatformId("season-pass");
        private static readonly PlatformId SecondReceiptId = new PlatformId("receipt-2");

        [Fact]
        public async Task RealSqliteActivationDuplicateAndReopenPreserveServerConfirmedOrigin()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenAsync(files.Path);
            var service = Create(database);
            var active = Snapshot(1, EntitlementRightState.Active, null, new byte[] { 7 });
            await service.ApplyConfirmedAsync(Context, active, Catalog(Context, true), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, active, Catalog(Context, true), CancellationToken.None);
            var count = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='entitlements'"), CancellationToken.None);
            Assert.Equal(1, count);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await OpenAsync(files.Path);
            service = Create(database);
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, cached.State);
            var right = Assert.Single(cached.Value!.Rights);
            Assert.Equal(EntitlementOfflineEligibility.Eligible, right.OfflineEligibility);
            Assert.Equal(ReceiptId, right.Right.OriginReceipt.ReceiptId);
            Assert.Equal("purchase.receipt-1", right.Right.OriginReceipt.BusinessSource);
            Assert.Equal(new byte[] { 7 }, right.Right.CopyPayload());
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RevocationIsMonotonicAndStaleActivationCannotRestoreRight()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            await service.ApplyConfirmedAsync(Context, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, Snapshot(3, EntitlementRightState.Revoked, null, Array.Empty<byte>()), Catalog(Context, true), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true), CancellationToken.None);
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            var right = Assert.Single(cached.Value!.Rights);
            Assert.Equal(3, cached.Revision);
            Assert.Equal(EntitlementRightState.Revoked, right.Right.State);
            Assert.Equal(EntitlementOfflineEligibility.Ineligible, right.OfflineEligibility);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SameRevisionComparisonIsOrderIndependentButRejectsChangedRightContent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            var first = new EntitlementConfirmedSnapshot(5, 105, new[]
            {
                Right(RightId, ReceiptId, 5, EntitlementRightState.Active, null, new byte[] { 1 }),
                Right(SecondRightId, SecondReceiptId, 5, EntitlementRightState.Unknown, null, new byte[] { 2 })
            });
            var reorderedEquivalent = new EntitlementConfirmedSnapshot(5, 105, new[]
            {
                Right(SecondRightId, SecondReceiptId, 5, EntitlementRightState.Unknown, null, new byte[] { 2 }),
                Right(RightId, ReceiptId, 5, EntitlementRightState.Active, null, new byte[] { 1 })
            });
            await service.ApplyConfirmedAsync(Context, first, Catalog(Context, true, RightId, SecondRightId), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, reorderedEquivalent, Catalog(Context, true, RightId, SecondRightId), CancellationToken.None);
            var changed = new EntitlementConfirmedSnapshot(5, 105, new[]
            {
                Right(RightId, ReceiptId, 5, EntitlementRightState.Active, null, new byte[] { 9 }),
                Right(SecondRightId, SecondReceiptId, 5, EntitlementRightState.Unknown, null, new byte[] { 2 })
            });
            await Assert.ThrowsAsync<EntitlementProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, changed, Catalog(Context, true, RightId, SecondRightId), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedTransactionRejectsNonAdvancingReplacementAndRollsBackCursorSideEffect()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cl106_cursor_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<InjectedFailure>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                service.ApplyConfirmedInTransaction(transaction, Context, 0, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl106_cursor_sentinel VALUES (?)", 1);
                throw new InjectedFailure();
            }, CancellationToken.None));
            Assert.Null(await new SqliteDurableFeatureStateStore(database, Scope).ReadAsync(Context, "entitlements", "confirmed", CancellationToken.None));
            var afterRollback = await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl106_cursor_sentinel"), CancellationToken.None);
            Assert.Equal(0, afterRollback);

            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedInTransaction(transaction, Context, 0, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl106_cursor_sentinel VALUES (?)", 1);
                return true;
            }, CancellationToken.None);
            Assert.NotNull(await new SqliteDurableFeatureStateStore(database, Scope).ReadAsync(Context, "entitlements", "confirmed", CancellationToken.None));
            var afterCommit = await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl106_cursor_sentinel"), CancellationToken.None);
            Assert.Equal(1, afterCommit);

            await Assert.ThrowsAsync<EntitlementProjectionConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl106_cursor_sentinel VALUES (?)", 2);
                service.ApplyConfirmedInTransaction(transaction, Context, 2, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 9 }), Catalog(Context, true));
                return true;
            }, CancellationToken.None));
            await Assert.ThrowsAsync<EntitlementProjectionConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl106_cursor_sentinel VALUES (?)", 3);
                service.ApplyConfirmedInTransaction(transaction, Context, 2, Snapshot(1, EntitlementRightState.Active, null, new byte[] { 9 }), Catalog(Context, true));
                return true;
            }, CancellationToken.None));
            var afterRejected = await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl106_cursor_sentinel"), CancellationToken.None);
            Assert.Equal(1, afterRejected);
            var preserved = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(2, preserved.Revision);
            Assert.Equal(new byte[] { 1 }, Assert.Single(preserved.Value!.Rights).Right.CopyPayload());
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ActiveRightWithServerExpiryIsExplicitlyUncertainOfflineWithoutUsingDeviceTime()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            await service.ApplyConfirmedAsync(Context, Snapshot(1, EntitlementRightState.Active, 1, new byte[] { 1 }), Catalog(Context, true), CancellationToken.None);
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(EntitlementOfflineEligibility.ExpiryUncertain, Assert.Single(cached.Value!.Rights).OfflineEligibility);
            await service.ApplyConfirmedAsync(Context, Snapshot(2, EntitlementRightState.Expired, 1, Array.Empty<byte>()), Catalog(Context, true), CancellationToken.None);
            Assert.Equal(EntitlementOfflineEligibility.Ineligible, Assert.Single((await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.Rights).OfflineEligibility);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AppVisibilityRemovalDropsTheRightOnlyWhenServerProjectionRemovesIt()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            await service.ApplyConfirmedAsync(Context, Snapshot(1, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, new EntitlementConfirmedSnapshot(2, 101, Array.Empty<EntitlementRight>()), Catalog(Context, false), CancellationToken.None);
            var cached = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(2, cached.Revision); Assert.Empty(cached.Value!.Rights);
            await Assert.ThrowsAsync<EntitlementProjectionConflictException>(() => service.ApplyConfirmedAsync(Context, Snapshot(3, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, false), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task InvalidReceiptAndAccountSwitchFailClosed()
        {
            Assert.Throws<ArgumentException>(() => new EntitlementOriginReceipt(default, "purchase.receipt-1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntitlementOriginReceipt(ReceiptId, "invalid receipt source"));
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database);
            await service.ApplyConfirmedAsync(Context, Snapshot(1, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(Context, true), CancellationToken.None);
            var switched = new ScopedOwnerContext(new OwnerScope(Owner.Backend, App, new PlatformUserId(Guid.Parse("0199f9a0-0600-7000-8000-000000000099"))), new SemanticId("private"), 1);
            await Assert.ThrowsAsync<EntitlementOwnerMismatchException>(() => service.ReadCachedAsync(switched, CancellationToken.None));
            await Assert.ThrowsAsync<EntitlementOwnerMismatchException>(() => service.ApplyConfirmedAsync(switched, Snapshot(2, EntitlementRightState.Active, null, new byte[] { 1 }), Catalog(switched, true), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static EntitlementsService Create(SqliteDatabase database) => new EntitlementsService(Context, Scope,
            new SqliteDurableFeatureStateStore(database, Scope), database, new Codec());

        private static EntitlementConfirmedSnapshot Snapshot(long revision, EntitlementRightState state, long? expiry, byte[] payload) =>
            new EntitlementConfirmedSnapshot(revision, 100 + revision, new[] { Right(RightId, ReceiptId, revision, state, expiry, payload) });

        private static EntitlementRight Right(PlatformId rightId, PlatformId receiptId, long revision, EntitlementRightState state, long? expiry, byte[] payload) =>
            new EntitlementRight(rightId, revision, state, new EntitlementOriginReceipt(receiptId, "purchase." + receiptId.Value), expiry, payload);

        private static CatalogSnapshot Catalog(ScopedOwnerContext context, bool visible, params PlatformId[] definitions) => new CatalogSnapshot(
            new CatalogSnapshotRequest(context, 1, "en", "adult"), 1, 1, 100,
            visible ? (definitions.Length == 0 ? new[] { RightId } : definitions).Select(definition => new CatalogEntry(
                new CatalogDefinition(definition, CatalogResourceKind.Entitlement, new SemanticId("test." + definition.Value), 1, 1, Array.Empty<byte>()),
                new CatalogAppBinding(definition, 1, true, true, false, false, false, Array.Empty<byte>()))).ToArray() : Array.Empty<CatalogEntry>());

        private static Task<SqliteDatabase> OpenAsync(string path) => SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);

        private sealed class Codec : IEntitlementStateCodec
        {
            public byte[] EncodeConfirmed(EntitlementConfirmedSnapshot snapshot) => Encoding.UTF8.GetBytes(string.Join("|", snapshot.Rights.Select(right =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(right.EntitlementId.Value + ";" + right.Revision + ";" + (int)right.State + ";" +
                right.OriginReceipt.ReceiptId.Value + ";" + right.OriginReceipt.BusinessSource + ";" +
                (right.ExpiresAtServerMilliseconds.HasValue ? right.ExpiresAtServerMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "") + ";" + Convert.ToBase64String(right.CopyPayload()))))));

            public EntitlementConfirmedSnapshot DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions)
            {
                ValidateSupportedExtensions(extensions);
                var text = Encoding.UTF8.GetString(payload);
                var rights = string.IsNullOrEmpty(text) ? Array.Empty<EntitlementRight>() : text.Split('|').Select(encoded =>
                {
                    var fields = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split(';');
                    if (fields.Length != 7) throw new InvalidOperationException("Invalid entitlement test state.");
                    long? expiry = string.IsNullOrEmpty(fields[5]) ? null : long.Parse(fields[5], System.Globalization.CultureInfo.InvariantCulture);
                    return new EntitlementRight(new PlatformId(fields[0]), long.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture),
                        (EntitlementRightState)int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                        new EntitlementOriginReceipt(new PlatformId(fields[3]), fields[4]), expiry, Convert.FromBase64String(fields[6]));
                });
                return new EntitlementConfirmedSnapshot(revision, confirmedAtMilliseconds, rights);
            }

            public void ValidateSupportedExtensions(ReadOnlySpan<byte> extensions)
            {
                if (extensions.Length != 0) throw new InvalidOperationException("Unsupported entitlement extension.");
            }
        }

        private sealed class InjectedFailure : Exception { }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl106", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
