#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Sync.Pull;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;

internal static class Program
{
    private static async Task<int> Main()
    {
        string Required(string name) => Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Missing {name}");
        var url = new Uri(Required("R09_BOOTSTRAP_URL"), UriKind.Absolute);
        if (url.Scheme != Uri.UriSchemeHttps || url.Host != "localhost" || url.AbsolutePath != "/platform")
            throw new InvalidOperationException("The fixture URL must be exact-host local HTTPS /platform");
        var pin = Required("R09_BOOTSTRAP_CERT_SHA256");
        if (pin.Length != 64 || !pin.All(Uri.IsHexDigit)) throw new InvalidOperationException("Invalid certificate pin");
        var app = Guid.Parse(Required("R09_BOOTSTRAP_APP"));
        var account = Guid.Parse(Required("R09_BOOTSTRAP_ACCOUNT"));
        var stream = Guid.Parse(Required("R09_BOOTSTRAP_STREAM"));
        var installation = Guid.Parse(Required("R09_BOOTSTRAP_INSTALLATION"));
        var databasePath = Required("R09_BOOTSTRAP_SQLITE_PATH");
        var scope = new StorageScope("be025-postgres", new PlatformId(app.ToString("D")), new PlatformId(account.ToString("D")));
        var configuration = new BackendHttpConfiguration(url, new BackendNamespace("be025-postgres"));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
        {
            if (request.RequestUri?.Host != "localhost" || certificate == null ||
                (errors & ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)) != SslPolicyErrors.None ||
                DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
                return false;
            var actual = Convert.ToHexString(SHA256.HashData(certificate.RawData));
            return string.Equals(actual, pin, StringComparison.OrdinalIgnoreCase);
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var executor = new BoundedHttpClientExecutor(client, configuration);
        using var refresh = new AuthRefreshCoordinator();
        var remote = new PrivateSyncHttpProvider(new AppId(app), new PlatformUserId(account), configuration,
            executor, new MessagePackWireCodec(), new StaticAuth(), refresh);
        var cancellation = CancellationToken.None;
        var clientStream = new ClientStreamId(stream);
        var database = await Open(databasePath, scope, stream, installation);
        try
        {
            var store = Store(database, scope);
            var coordinator = new PrivateSyncCoordinator(store, remote, () => 1_726_704_000_000L);
            var interrupted = await coordinator.BootstrapAsync(clientStream, cancellation);
            Require(interrupted == PrivateSyncResult.RemoteFailure, "first bootstrap did not fail after page one");
            Require(await Scalar(database, scope, "SELECT COUNT(*) FROM gp_confirmed_projection") == 0, "partial projection became visible");
            Require(await Scalar(database, scope, "SELECT ready FROM gp_stream_state WHERE singleton=1") == 0, "stream became Ready before final page");
            Require(await store.GetBootstrapProgressAsync(cancellation) != null, "first page was not staged");
            Console.WriteLine("R09 SDK RED: page-two HTTPS fault; staged progress retained; confirmed=0; ready=0");
        }
        finally { Require(await database.DisposeAsync(TimeSpan.FromSeconds(5)), "first SQLite close failed"); }
        database = await Open(databasePath, scope, stream, installation);
        try
        {
            var store = Store(database, scope);
            var coordinator = new PrivateSyncCoordinator(store, remote, () => 1_726_704_000_000L);
            var recovered = await coordinator.BootstrapAsync(clientStream, cancellation);
            Require(recovered == PrivateSyncResult.BootstrapInstalled, "reopened SDK did not install complete bootstrap: " + recovered);
            Require(await Scalar(database, scope, "SELECT ready FROM gp_stream_state WHERE singleton=1") == 1, "stream not Ready after final page");
            Require(await Scalar(database, scope, "SELECT COUNT(*) FROM gp_confirmed_projection WHERE collection='inventory'") == 1025, "inventory count != 1025");
            Require(await Scalar(database, scope, "SELECT COUNT(DISTINCT entity_key) FROM gp_confirmed_projection WHERE collection='inventory'") == 1025, "duplicate inventory keys");
            Require(await store.GetBootstrapProgressAsync(cancellation) == null, "staging not cleared after install");
            var checkpoint = await store.GetPullCheckpointAsync(cancellation);
            Require(checkpoint.CopyCursor() is { Length: > 0 }, "final pull cursor absent");
            Console.WriteLine("R09 SDK GREEN: reopened same SQLite; 1025 unique inventory; ready=1; staging=0; cursor present");
        }
        finally { Require(await database.DisposeAsync(TimeSpan.FromSeconds(5)), "second SQLite close failed"); }
        return 0;
    }

    private static async Task<SqliteDatabase> Open(string path, StorageScope scope, Guid stream, Guid installation)
    {
        var database = await SqliteDatabase.OpenAsync(path, scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
        await database.ExecuteAsync(scope, transaction =>
        {
            var session = (SqliteTransactionSession)transaction;
            if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state") == 0)
                session.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,?,0,1,0,0)",
                    scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value,
                    stream.ToString("D"), installation.ToString("D"));
            return true;
        }, CancellationToken.None);
        return database;
    }
    private static SqlitePrivateSyncStore Store(SqliteDatabase database, StorageScope scope) =>
        new SqlitePrivateSyncStore(database, scope, NoopProjector.Instance, _ => { });
    private static Task<int> Scalar(SqliteDatabase database, StorageScope scope, string sql) =>
        database.ExecuteAsync(scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>(sql), CancellationToken.None);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class StaticAuth : IAuthSession
    {
        public string SessionKey => "r09-local-bootstrap";
        public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new AccessTokenSnapshot("synthetic", 0));
        public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) => GetAsync(cancellationToken);
    }
    private sealed class NoopProjector : IPrivateSyncProjectionProjector
    {
        public static readonly NoopProjector Instance = new NoopProjector();
        public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group) { }
        public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot) { }
    }
}
