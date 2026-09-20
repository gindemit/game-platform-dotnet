#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Contracts.Wallet;
using GamePlatform.Features.Wallet;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Features.Wallet
{
    /// <summary>These cases use the real pinned SQLite store. The injected remote is deliberately a test-only feature port.</summary>
    public sealed class Cl105WalletServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405061"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddee01"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("wallet-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("wallet-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-1111-7777-8888-999999999991"));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 7);
        private static readonly WalletCurrency Coin = new WalletCurrency(new PlatformId("test-coin"), new SemanticId("test.coin"));
        private static readonly IReadOnlyList<SqliteMigration> Migrations = new[] { SqliteOutboxMigration.Create(1), SqliteFeatureStateMigration.Create(2) };

        [Fact]
        public async Task RealSqliteConfirmedIntentAndOutboxCommitTogetherAndSurviveReopenWithSigned64Extrema()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var remote = new TestRemote();
            var service = Create(database, Context, remote);
            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, long.MaxValue, 1), Catalog(), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, long.MinValue, 2), Catalog(), CancellationToken.None);
            var request = Spend("0199f9a0-2222-7777-8888-999999999991", Coin, 2, 1, "wallet-long-1");
            var pending = await service.SubmitSpendIntentAsync(request, Catalog(), CancellationToken.None);

            Assert.Equal(FeatureSnapshotState.Pending, pending.State);
            Assert.Equal(long.MinValue, pending.Value!.Confirmed!.Balance);
            Assert.Equal(1, remote.AuthorizationCalls);
            var persisted = await database.ExecuteAsync(Scope, tx =>
            {
                var session = (SqliteTransactionSession)tx;
                return (Outbox: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"),
                    Pending: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='wallet' AND entity_key='pending/test-coin' AND length(payload)>0"));
            }, CancellationToken.None);
            Assert.Equal(1, persisted.Outbox); Assert.Equal(1, persisted.Pending);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, false);
            var restored = Create(reopened, Context, new TestRemote());
            var cached = await restored.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, cached.State);
            Assert.Equal(long.MinValue, cached.Value!.Confirmed!.Balance);
            Assert.Equal(request.OperationId, cached.Value.Pending!.OperationId);
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AcceptedReceiptThenOlderPullPreservesPendingUntilConfirmingGroupAndDuplicateReceiptIsIdempotent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, Context, new TestRemote());
            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, 100, 10), Catalog(), CancellationToken.None);
            var request = Spend("0199f9a0-3333-7777-8888-999999999991", Coin, 10, 5, "wallet-receipt-1");
            await service.SubmitSpendIntentAsync(request, Catalog(), CancellationToken.None);
            var acceptance = new WalletSpendAcceptance(Context, request.OperationId, 12);
            var accepted = await service.MarkAcceptedAwaitingPullAsync(acceptance, Coin, Catalog(), CancellationToken.None);
            var duplicate = await service.MarkAcceptedAwaitingPullAsync(acceptance, Coin, Catalog(), CancellationToken.None);
            Assert.Equal(WalletPendingStatus.AcceptedAwaitingPull, accepted.Value!.Pending!.Status);
            Assert.Equal(100, duplicate.Value!.Confirmed!.Balance); // receipt has not become a local debit.

            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, 95, 11), Catalog(), CancellationToken.None);
            var olderPull = await service.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, olderPull.State);
            Assert.Equal(WalletPendingStatus.AcceptedAwaitingPull, olderPull.Value!.Pending!.Status);
            Assert.Equal(95, olderPull.Value.Confirmed!.Balance);

            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, 95, 12), Catalog(), CancellationToken.None);
            var confirmed = await service.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, confirmed.State);
            Assert.Null(confirmed.Value!.Pending); Assert.Equal(95, confirmed.Value.Confirmed!.Balance);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task StaleOrOfflineBalanceCannotAuthorizeSpendAndLeavesNoCommand()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var remote = new TestRemote { Offline = true };
            var service = Create(database, Context, remote);
            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 3), Catalog(), CancellationToken.None);
            var cached = await service.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, cached.State);
            await Assert.ThrowsAsync<WalletOfflineSpendException>(() => service.SubmitSpendIntentAsync(Spend("0199f9a0-4444-7777-8888-999999999991", Coin, 2, 1, "wallet-stale-1"), Catalog(), CancellationToken.None));
            Assert.Equal(0, remote.AuthorizationCalls);
            await Assert.ThrowsAsync<WalletOfflineSpendException>(() => service.SubmitSpendIntentAsync(Spend("0199f9a0-4444-7777-8888-999999999992", Coin, 3, 1, "wallet-offline-1"), Catalog(), CancellationToken.None));
            Assert.Equal(1, remote.AuthorizationCalls);
            var count = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), CancellationToken.None);
            Assert.Equal(0, count);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ConcurrentDevicesHaveIndependentServerOutcomesAndRejectedIntentStaysLabelled()
        {
            using var oneFiles = new TemporaryDatabase(); using var twoFiles = new TemporaryDatabase();
            var oneDatabase = await OpenReady(oneFiles.Path); var twoDatabase = await OpenReady(twoFiles.Path);
            var one = Create(oneDatabase, Context, new TestRemote()); var two = Create(twoDatabase, Context, new TestRemote());
            await one.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 5), Catalog(), CancellationToken.None);
            await two.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 5), Catalog(), CancellationToken.None);
            var oneRequest = Spend("0199f9a0-5555-7777-8888-999999999991", Coin, 5, 5, "wallet-device-one");
            var twoRequest = Spend("0199f9a0-5555-7777-8888-999999999992", Coin, 5, 8, "wallet-device-two");
            await one.SubmitSpendIntentAsync(oneRequest, Catalog(), CancellationToken.None);
            await two.SubmitSpendIntentAsync(twoRequest, Catalog(), CancellationToken.None);

            await one.MarkAcceptedAwaitingPullAsync(new WalletSpendAcceptance(Context, oneRequest.OperationId, 6), Coin, Catalog(), CancellationToken.None);
            await one.ApplyConfirmedAsync(Context, Confirmed(Coin, 5, 6), Catalog(), CancellationToken.None);
            var rejected = await two.MarkRejectedAsync(new WalletSpendRejection(Context, twoRequest.OperationId, "insufficient_funds"), Coin, Catalog(), CancellationToken.None);
            var oneState = await one.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(), CancellationToken.None);
            Assert.Null(oneState.Value!.Pending); Assert.Equal(5, oneState.Value.Confirmed!.Balance);
            Assert.Equal(FeatureSnapshotState.Error, rejected.State);
            Assert.Equal("insufficient_funds", rejected.DiagnosticCode);
            Assert.Equal(WalletPendingStatus.Rejected, rejected.Value!.Pending!.Status);
            one.Dispose(); two.Dispose();
            Assert.True(await oneDatabase.DisposeAsync(TimeSpan.FromSeconds(5))); Assert.True(await twoDatabase.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task NamespaceOwnerAndOverflowBoundariesFailClosedWithoutCrossViewState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletSpendIntent(Coin, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WalletSpendIntent(Coin, 1, long.MinValue));
            Assert.Equal(long.MaxValue, new WalletConfirmedBalance(Coin, long.MaxValue, 1, 1, Array.Empty<byte>()).Balance);
            Assert.Equal(long.MinValue, new WalletConfirmedBalance(Coin, long.MinValue, 1, 1, Array.Empty<byte>()).Balance);

            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var otherView = new ScopedOwnerContext(Owner, new SemanticId("private-other"), 7);
            var one = Create(database, Context, new TestRemote()); var two = Create(database, otherView, new TestRemote());
            await one.ApplyConfirmedAsync(Context, Confirmed(Coin, 7, 1), Catalog(), CancellationToken.None);
            await two.ApplyConfirmedAsync(otherView, Confirmed(Coin, 9, 1), Catalog(otherView), CancellationToken.None);
            await Assert.ThrowsAsync<WalletOwnerMismatchException>(() => one.ReadCachedAsync(new WalletBalanceRequest(otherView, Coin), Catalog(otherView), CancellationToken.None));
            await Assert.ThrowsAsync<WalletConflictException>(() => one.ApplyConfirmedAsync(Context, Confirmed(Coin, 8, 1), Catalog(), CancellationToken.None));
            var rows = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='wallet'"), CancellationToken.None);
            Assert.Equal(2, rows);
            one.Dispose(); two.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ExactCatalogBindingIsRequiredForRefreshReadInstallAndSpendAdmission()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var remote = new TestRemote { ReadValue = new WalletRemoteRead(Context, Confirmed(Coin, 10, 1)) };
            var service = Create(database, Context, remote);
            var request = Spend("0199f9a0-6666-7777-8888-999999999991", Coin, 1, 1, "wallet-catalog-1");

            await Assert.ThrowsAsync<WalletCatalogException>(() => service.RefreshAsync(new WalletBalanceRequest(Context, Coin), Catalog(included: false), CancellationToken.None));
            await Assert.ThrowsAsync<WalletCatalogException>(() => service.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 1), Catalog(kind: CatalogResourceKind.Item), CancellationToken.None));
            var foreign = new ScopedOwnerContext(Owner, new SemanticId("private-foreign"), 7);
            await Assert.ThrowsAsync<WalletCatalogException>(() => service.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 1), Catalog(foreign), CancellationToken.None));
            await service.ApplyConfirmedAsync(Context, Confirmed(Coin, 10, 1), Catalog(), CancellationToken.None);
            await Assert.ThrowsAsync<WalletCatalogException>(() => service.ReadCachedAsync(new WalletBalanceRequest(Context, Coin), Catalog(included: false), CancellationToken.None));
            await Assert.ThrowsAsync<WalletCatalogException>(() => service.SubmitSpendIntentAsync(request, Catalog(spend: false), CancellationToken.None));
            Assert.Equal(0, remote.AuthorizationCalls);
            var outbox = await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"), CancellationToken.None);
            Assert.Equal(0, outbox);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static WalletService Create(SqliteDatabase database, ScopedOwnerContext owner, IWalletRemote remote) => new WalletService(owner, Scope,
            new SqliteDurableFeatureStateStore(database, Scope), database, new SqliteAtomicCommandStore(database, Scope, Owner, new TestFingerprint()),
            new TestCodec(), remote, 8, TimeSpan.FromMinutes(1), () => 1_000);
        private static WalletConfirmedBalance Confirmed(WalletCurrency currency, long balance, long revision) => new WalletConfirmedBalance(currency, balance, revision, 100, new byte[] { 7 });
        private static CatalogSnapshot Catalog(ScopedOwnerContext? owner = null, bool included = true, bool spend = true, CatalogResourceKind kind = CatalogResourceKind.Currency) =>
            new CatalogSnapshot(new CatalogSnapshotRequest(owner ?? Context, 1, "en", "adult"), 1, 1, 100,
                included ? new[] { new CatalogEntry(new CatalogDefinition(Coin.DefinitionId, kind, Coin.SemanticKey, 1, 1, Array.Empty<byte>()),
                    new CatalogAppBinding(Coin.DefinitionId, 1, true, false, false, spend, false, Array.Empty<byte>())) } : Array.Empty<CatalogEntry>());
        private static WalletSpendRequest Spend(string operation, WalletCurrency currency, long expected, long amount, string source) => new WalletSpendRequest(Context,
            new OperationId(Guid.Parse(operation)), Stream, source, new WalletSpendIntent(currency, expected, amount));
        private static async Task<SqliteDatabase> OpenReady(string path, bool seed = true)
        {
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            if (seed) await database.ExecuteAsync(Scope, tx =>
            {
                ((SqliteTransactionSession)tx).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, ready, next_sequence, local_revision, finalized_through) SELECT 1, ?, ?, ?, ?, 1, 1, 0, 0 WHERE NOT EXISTS (SELECT 1 FROM gp_stream_state)", Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString());
                return true;
            }, CancellationToken.None);
            return database;
        }

        private sealed class TestRemote : IWalletRemote
        {
            public bool Offline { get; set; }
            public WalletRemoteRead? ReadValue { get; set; }
            public int AuthorizationCalls { get; private set; }
            public Task<WalletRemoteRead> ReadAsync(WalletBalanceRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadValue == null) throw new NotSupportedException();
                return Task.FromResult(ReadValue);
            }
            public Task<WalletSpendAuthorization> AuthorizeSpendAsync(WalletSpendRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested(); AuthorizationCalls++;
                if (Offline) throw new WalletOfflineSpendException();
                return Task.FromResult(new WalletSpendAuthorization(request.Owner, request.OperationId, request.BusinessSource, request.Intent));
            }
        }

        private sealed class TestCodec : IWalletStateCodec
        {
            public byte[] EncodeConfirmed(WalletConfirmedBalance value) => Text("C", value.Currency.DefinitionId.Value, value.Currency.SemanticKey.Value, value.Balance.ToString(CultureInfo.InvariantCulture));
            public WalletConfirmedBalance DecodeConfirmed(long revision, long at, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions)
            { var p = Parts(payload, 3); return new WalletConfirmedBalance(Currency(p[1], p[2]), long.Parse(p[3], CultureInfo.InvariantCulture), revision, at, extensions); }
            public byte[] EncodePending(PendingWalletSpend value) => Text("P", value.OperationId.ToString(), value.StreamId.ToString(), value.BusinessSource,
                value.Intent.Currency.DefinitionId.Value, value.Intent.Currency.SemanticKey.Value, value.Intent.ExpectedRevision.ToString(CultureInfo.InvariantCulture), value.Intent.Amount.ToString(CultureInfo.InvariantCulture),
                ((int)value.Status).ToString(CultureInfo.InvariantCulture), value.AcceptedRevision?.ToString(CultureInfo.InvariantCulture), value.RejectionCode);
            public PendingWalletSpend DecodePending(long localRevision, ReadOnlySpan<byte> payload)
            {
                var p = Parts(payload, 10); return new PendingWalletSpend(new OperationId(Guid.Parse(p[1])), new ClientStreamId(Guid.Parse(p[2])), p[3],
                    new WalletSpendIntent(Currency(p[4], p[5]), long.Parse(p[6], CultureInfo.InvariantCulture), long.Parse(p[7], CultureInfo.InvariantCulture)), localRevision,
                    (WalletPendingStatus)int.Parse(p[8], CultureInfo.InvariantCulture), string.IsNullOrEmpty(p[9]) ? null : long.Parse(p[9], CultureInfo.InvariantCulture), Null(p[10]));
            }
            public byte[] EncodeSpendCommand(WalletSpendIntent value) => Text("S", value.Currency.DefinitionId.Value, value.Currency.SemanticKey.Value, value.ExpectedRevision.ToString(CultureInfo.InvariantCulture), value.Amount.ToString(CultureInfo.InvariantCulture));
            public void ValidateExtensions(ReadOnlySpan<byte> extensions) { if (extensions.Length > 16_384 || (extensions.Length > 0 && extensions[0] == 0xff)) throw new InvalidOperationException("Unsupported extension shape."); }
            private static WalletCurrency Currency(string id, string key) => new WalletCurrency(new PlatformId(id), new SemanticId(key));
            private static byte[] Text(params string?[] values) => Encoding.UTF8.GetBytes(string.Join("|", Array.ConvertAll(values, value => value == null ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value)))));
            private static string[] Parts(ReadOnlySpan<byte> data, int fieldsAfterKind)
            {
                var values = Encoding.UTF8.GetString(data).Split('|'); if (values.Length != fieldsAfterKind + 1) throw new InvalidOperationException("Invalid test state.");
                for (var i = 0; i < values.Length; i++) values[i] = Encoding.UTF8.GetString(Convert.FromBase64String(values[i])); return values;
            }
            private static string? Null(string value) => value.Length == 0 ? null : value;
        }

        private sealed class TestFingerprint : ICommandFingerprint
        {
            public int GetFingerprintLength(int version) => version == 1 ? 32 : 0;
            public void Compute(OwnerScope owner, OperationId operation, ClientStreamId stream, long sequence, string kind, int schema, int fingerprint, ReadOnlySpan<byte> body, Span<byte> destination) => SHA256.HashData(body).CopyTo(destination);
        }
        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl105", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
