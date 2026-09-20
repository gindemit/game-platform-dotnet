#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Contracts.Wallet;
using GamePlatform.Features.Wallet;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Tests.Features.Wallet
{
    /// <summary>Real SQLite coverage for the semantic-only frozen G3 wallet projection path.</summary>
    public sealed class Cl105G3WalletProjectionTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405061"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddee01"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("g3-wallet-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("g3-wallet-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 7);
        private static readonly SemanticId Coin = new SemanticId("test.coin");
        private static readonly IReadOnlyList<SqliteMigration> Migrations = SqlitePlatformMigrationRegistry.Migrations.Concat(new[]
        {
            new SqliteMigration(8, "g3-wallet-cursor-sentinel", new[] { "CREATE TABLE g3_wallet_cursor_sentinel (singleton INTEGER PRIMARY KEY, value INTEGER NOT NULL)" })
        }).ToArray();

        [Fact]
        public async Task MissingNeverSynthesizesZeroAndRevisionZeroPreservesSigned64Balance()
        {
            using var files = new TemporaryDatabase();
            var database = await Open(files.Path);
            var service = Create(database, Context);
            var request = new G3WalletProjectionRequest(Context, Coin);
            var absent = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Missing, absent.State); Assert.Equal(SnapshotFreshness.Missing, absent.Freshness); Assert.Equal(0, absent.Revision); Assert.Null(absent.Value);
            await database.ExecuteAsync(Scope, transaction => { service.ApplyConfirmed(transaction, -1, request, new G3ConfirmedWalletProjection(Coin, long.MinValue, 0)); return true; }, CancellationToken.None);
            var zero = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, zero.State); Assert.Equal(0, zero.Revision); Assert.Equal(long.MinValue, zero.Value!.Balance);
            await database.ExecuteAsync(Scope, transaction => { service.ApplyConfirmed(transaction, 0, request, new G3ConfirmedWalletProjection(Coin, long.MaxValue, 1)); return true; }, CancellationToken.None);
            var extrema = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(1, extrema.Revision); Assert.Equal(long.MaxValue, extrema.Value!.Balance);
            Assert.Throws<ArgumentOutOfRangeException>(() => new G3ConfirmedWalletProjection(Coin, 0, -1));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedTransactionRollsBackCommitsAndReopensWithCursorSentinel()
        {
            using var files = new TemporaryDatabase(); var database = await Open(files.Path); var service = Create(database, Context); var request = new G3WalletProjectionRequest(Context, Coin);
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                service.ApplyConfirmed(transaction, -1, request, new G3ConfirmedWalletProjection(Coin, 9, 0));
                session.Execute("INSERT INTO g3_wallet_cursor_sentinel VALUES (1, 10)");
                throw new InvalidOperationException("simulate cursor group rollback");
            }, CancellationToken.None));
            Assert.Equal(FeatureSnapshotState.Missing, (await service.ReadCachedAsync(request, CancellationToken.None)).State);
            Assert.Equal(0, await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM g3_wallet_cursor_sentinel"), CancellationToken.None));
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                service.ApplyConfirmed(transaction, -1, request, new G3ConfirmedWalletProjection(Coin, 9, 0));
                session.Execute("INSERT INTO g3_wallet_cursor_sentinel VALUES (1, 11)"); return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var reopened = await Open(files.Path); var restored = Create(reopened, Context); var snapshot = await restored.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, snapshot.State); Assert.Equal(0, snapshot.Revision); Assert.Equal(9, snapshot.Value!.Balance);
            Assert.Equal(11, await reopened.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT value FROM g3_wallet_cursor_sentinel WHERE singleton=1"), CancellationToken.None));
            Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SameOrLowerChangedProjectionCannotOverwriteAndRemovalReadsAsMissing()
        {
            using var files = new TemporaryDatabase(); var database = await Open(files.Path); var service = Create(database, Context); var request = new G3WalletProjectionRequest(Context, Coin);
            await database.ExecuteAsync(Scope, transaction => { service.ApplyConfirmed(transaction, -1, request, new G3ConfirmedWalletProjection(Coin, 50, 2)); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<G3WalletProjectionConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction => { service.ApplyConfirmed(transaction, 2, request, new G3ConfirmedWalletProjection(Coin, 999, 2)); return true; }, CancellationToken.None));
            await Assert.ThrowsAsync<G3WalletProjectionConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction => { service.ApplyConfirmed(transaction, 2, request, new G3ConfirmedWalletProjection(Coin, -999, 1)); return true; }, CancellationToken.None));
            var unchanged = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(50, unchanged.Value!.Balance); Assert.Equal(2, unchanged.Revision);
            await database.ExecuteAsync(Scope, transaction => { service.ApplyRemoval(transaction, 2, request, 3, G3WalletProjectionRemoval.Reset); return true; }, CancellationToken.None);
            var reset = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Missing, reset.State); Assert.Null(reset.Value); Assert.Equal(0, reset.Revision);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task OwnerAndViewNamespacesAreExactAndIsolated()
        {
            using var files = new TemporaryDatabase(); var database = await Open(files.Path);
            var secondView = new ScopedOwnerContext(Owner, new SemanticId("private-second"), 7);
            var first = Create(database, Context); var second = Create(database, secondView);
            var firstRequest = new G3WalletProjectionRequest(Context, Coin); var secondRequest = new G3WalletProjectionRequest(secondView, Coin);
            await database.ExecuteAsync(Scope, transaction =>
            {
                first.ApplyConfirmed(transaction, -1, firstRequest, new G3ConfirmedWalletProjection(Coin, 7, 0));
                second.ApplyConfirmed(transaction, -1, secondRequest, new G3ConfirmedWalletProjection(Coin, 11, 0)); return true;
            }, CancellationToken.None);
            Assert.Equal(7, (await first.ReadCachedAsync(firstRequest, CancellationToken.None)).Value!.Balance);
            Assert.Equal(11, (await second.ReadCachedAsync(secondRequest, CancellationToken.None)).Value!.Balance);
            await Assert.ThrowsAsync<G3WalletProjectionOwnerMismatchException>(() => first.ReadCachedAsync(secondRequest, CancellationToken.None));
            await Assert.ThrowsAsync<G3WalletProjectionOwnerMismatchException>(() => database.ExecuteAsync<bool>(Scope, transaction => { first.ApplyConfirmed(transaction, 0, secondRequest, new G3ConfirmedWalletProjection(Coin, 99, 1)); return true; }, CancellationToken.None));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static G3WalletProjectionService Create(SqliteDatabase database, ScopedOwnerContext owner) => new G3WalletProjectionService(owner, Scope, new SqliteDurableFeatureStateStore(database, Scope), new TestCodec());
        private static Task<SqliteDatabase> Open(string path) => SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);

        private sealed class TestCodec : IG3WalletProjectionCodec
        {
            public int SchemaVersion => 1;
            public byte[] Encode(G3ConfirmedWalletProjection projection) => Encoding.UTF8.GetBytes(projection.CurrencyId.Value + "|" + projection.Balance.ToString(CultureInfo.InvariantCulture) + "|" + projection.Revision.ToString(CultureInfo.InvariantCulture));
            public G3ConfirmedWalletProjection Decode(int schemaVersion, ReadOnlySpan<byte> payload)
            {
                if (schemaVersion != SchemaVersion) throw new InvalidOperationException("Unsupported test schema.");
                var fields = Encoding.UTF8.GetString(payload.ToArray()).Split('|');
                if (fields.Length != 3) throw new InvalidOperationException("Invalid test projection.");
                return new G3ConfirmedWalletProjection(new SemanticId(fields[0]), long.Parse(fields[1], CultureInfo.InvariantCulture), long.Parse(fields[2], CultureInfo.InvariantCulture));
            }
        }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl105-g3", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
