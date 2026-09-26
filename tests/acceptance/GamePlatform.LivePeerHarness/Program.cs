#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
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
using GamePlatform.Wire.Contracts;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine("GamePlatform.LivePeerHarness probe --live-file PATH --sqlite PATH --sdk-revision SHA --backend-revision SHA --unity-revision SHA [--expect-lost-response true]");
            Console.WriteLine("live-file: disposable nonproduction serve-live.json with local HTTPS endpoint and CA; never commit it.");
            return 0;
        }
        try
        {
            var options = Parse(args);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(options["--live-file"]));
            var config = document.RootElement;
            string Required(string key) => config.GetProperty(key).GetString() ?? throw new InvalidOperationException("missing_" + key);
            var supabase = new Uri(Required("supabaseUrl"), UriKind.Absolute);
            var functions = new Uri(Required("functionsBaseUrl"), UriKind.Absolute);
            if (supabase.Scheme != "https" || functions.Scheme != "https" || !supabase.IsLoopback ||
                supabase.Authority != functions.Authority || !functions.AbsolutePath.EndsWith("/functions/v1/game-platform/", StringComparison.Ordinal))
                throw new InvalidOperationException("nonproduction_loopback_https_required");
            var ca = X509Certificate2.CreateFromPem(Required("caPem"));
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
            {
                if (certificate == null || chain == null) return false;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(ca);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(new X509Certificate2(certificate));
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            var anonKey = Required("anonKey");
            using var signup = new HttpRequestMessage(HttpMethod.Post, new Uri(supabase, "/auth/v1/signup"));
            signup.Headers.Add("apikey", anonKey);
            signup.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var signupResponse = await client.SendAsync(signup);
            Require((int)signupResponse.StatusCode == 200, "auth_signup_unavailable");
            using var signupJson = JsonDocument.Parse(await signupResponse.Content.ReadAsStringAsync());
            var token = signupJson.RootElement.GetProperty("access_token").GetString();
            Require(!string.IsNullOrWhiteSpace(token), "auth_token_absent");
            using var ready = new HttpRequestMessage(HttpMethod.Get, new Uri(functions, "readyz"));
            ready.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var readyResponse = await client.SendAsync(ready);
            Require((int)readyResponse.StatusCode == 200, "backend_not_ready_" + (int)readyResponse.StatusCode);

            var app = new AppId(Guid.Parse(Required("appId")));
            var backendNamespace = new BackendNamespace(Required("backendNamespace"));
            var http = new BackendHttpConfiguration(functions, backendNamespace);
            var executor = new BoundedHttpClientExecutor(client, http);
            var auth = new StaticAuth(token!);
            using var refresh = new AuthRefreshCoordinator();
            var codec = new MessagePackWireCodec();
            var ids = new UuidV7Generator(new SystemUnixMillisecondClock(), new CryptographicUuidRandomSource());
            var installation = ids.NewId();
            var stream = new ClientStreamId(ids.NewId());
            var provision = new ProvisioningHttpProvider(http, executor, codec, auth, refresh);
            var provisioned = await provision.ProvisionAsync(app, installation, stream, CancellationToken.None);
            Require(provisioned.IsSuccess, "provision_failed_" + provisioned.Failure.Kind + "_" + provisioned.Failure.StatusCode);
            var account = provisioned.Value!.AccountId;
            var scope = new StorageScope(backendNamespace.Value, new PlatformId(app.Value.ToString("D")), new PlatformId(account.Value.ToString("D")));
            var db = await SqliteDatabase.OpenAsync(options["--sqlite"], scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            try
            {
                await db.ExecuteAsync(scope, transaction =>
                {
                    var sql = (SqliteTransactionSession)transaction;
                    Require(sql.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state") == 0, "sqlite_not_fresh");
                    sql.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,?,0,1,0,0)", scope.BackendNamespace,
                        scope.AppId.Value, scope.AccountId.Value, stream.Value.ToString("D"), installation.ToString("D"));
                    return true;
                }, CancellationToken.None);
                var store = new SqlitePrivateSyncStore(db, scope, NoopProjector.Instance, _ => { });
                var remote = new PrivateSyncHttpProvider(app, account, http, executor, codec, auth, refresh);
                var coordinator = new PrivateSyncCoordinator(store, remote, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                var result = await coordinator.BootstrapAsync(stream, CancellationToken.None);
                Require(result == PrivateSyncResult.BootstrapInstalled, "bootstrap_failed_" + result);
                var operation = new OperationId(ids.NewId());
                var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var session = ids.NewId().ToString("D");
                var commandBody = codec.Encode(new GameplayCompletionCommand(Required("g3ContentId"),
                    config.GetProperty("g3ContentVersion").GetInt32(), Required("g3Mode"), "normal", true, 1,
                    new GameplayValidation("untrusted-reference", "live-peer-local"), default, session, 1000, 1000,
                    new Dictionary<string, long> { ["moves"] = 1 }));
                var fingerprint = new byte[32];
                new CanonicalCommandFingerprint().Compute(new OwnerScope(backendNamespace, app, account), operation,
                    stream, installation, 1, "gameplay.session.completed", 1, 1, createdAt, commandBody, fingerprint);
                var command = new RemoteCommand(operation, stream, installation, 1, "gameplay.session.completed", 1, 1,
                    createdAt, commandBody, fingerprint);
                var push = new CommandPushHttpProvider(app, account, http, executor, codec, auth, refresh);
                using var secondSignup = new HttpRequestMessage(HttpMethod.Post, new Uri(supabase, "/auth/v1/signup"));
                secondSignup.Headers.Add("apikey", anonKey);
                secondSignup.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var secondSignupResponse = await client.SendAsync(secondSignup);
                Require((int)secondSignupResponse.StatusCode == 200, "second_auth_signup_unavailable");
                using var secondSignupJson = JsonDocument.Parse(await secondSignupResponse.Content.ReadAsStringAsync());
                var secondToken = secondSignupJson.RootElement.GetProperty("access_token").GetString();
                Require(!string.IsNullOrWhiteSpace(secondToken), "second_auth_token_absent");
                using var secondRefresh = new AuthRefreshCoordinator();
                var secondAuth = new StaticAuth(secondToken!);
                var secondAccount = await new ProvisioningHttpProvider(http, executor, codec, secondAuth, secondRefresh)
                    .ProvisionAsync(app, ids.NewId(), new ClientStreamId(ids.NewId()), CancellationToken.None);
                Require(secondAccount.IsSuccess && secondAccount.Value!.AccountId != account, "second_owner_not_provisioned");
                var wrongOwner = new CommandPushHttpProvider(app, secondAccount.Value!.AccountId, http, executor, codec, secondAuth, secondRefresh);
                var wrongFingerprint = new byte[32];
                new CanonicalCommandFingerprint().Compute(new OwnerScope(backendNamespace, app, account), operation,
                    stream, installation, 3, "gameplay.session.completed", 1, 1, createdAt, commandBody, wrongFingerprint);
                var mismatched = await wrongOwner.SendAsync(new RemoteCommand(operation, stream, installation, 3,
                    "gameplay.session.completed", 1, 1, createdAt, commandBody, wrongFingerprint), CancellationToken.None);
                Require(!mismatched.IsSuccess && (mismatched.Failure.StatusCode == 403 || mismatched.Failure.StatusCode == 404),
                    "owner_mismatch_not_denied_" + mismatched.IsSuccess + "_" + mismatched.Failure.Kind + "_" + mismatched.Failure.StatusCode);
                var accepted = await push.SendAsync(command, CancellationToken.None);
                var expectLost = options.ContainsKey("--expect-lost-response");
                if (expectLost)
                {
                    Require(!accepted.IsSuccess && accepted.Failure.Kind == RemoteFailureKind.OutcomeUncertain,
                        "expected_lost_response_not_observed");
                    var receipt = await new CommandReceiptHttpProvider(app, account, installation, http, executor, codec, auth, refresh)
                        .LookupAsync(command, CancellationToken.None);
                    Require(receipt.IsSuccess && receipt.Value!.Found && receipt.Value.Outcome!.Status == RemoteCommandStatus.Accepted,
                        "committed_receipt_not_found_after_lost_response");
                }
                else Require(accepted.IsSuccess && accepted.Value!.Status == RemoteCommandStatus.Accepted,
                    "completion_not_accepted_" + accepted.Failure.Kind + "_" + accepted.Failure.StatusCode);
                var replay = await push.SendAsync(command, CancellationToken.None);
                Require(replay.IsSuccess && replay.Value!.Status == RemoteCommandStatus.Accepted, "repeat_operation_not_idempotent");
                var duplicateOperation = new OperationId(ids.NewId());
                var duplicateFingerprint = new byte[32];
                new CanonicalCommandFingerprint().Compute(new OwnerScope(backendNamespace, app, account), duplicateOperation,
                    stream, installation, 2, "gameplay.session.completed", 1, 1, createdAt, commandBody, duplicateFingerprint);
                var duplicateSource = await push.SendAsync(new RemoteCommand(duplicateOperation, stream, installation, 2,
                    "gameplay.session.completed", 1, 1, createdAt, commandBody, duplicateFingerprint), CancellationToken.None);
                Require(duplicateSource.IsSuccess && duplicateSource.Value!.Status == RemoteCommandStatus.Accepted,
                    "duplicate_business_source_not_idempotent");
                var pulled = await coordinator.PullOnceAsync(CancellationToken.None);
                Require(pulled == PrivateSyncResult.BoundaryComplete || pulled == PrivateSyncResult.PageApplied,
                    "committed_pull_failed_" + pulled);
                var confirmed = await db.ExecuteAsync(scope, transaction =>
                    ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_confirmed_projection"), CancellationToken.None);
                var readyState = await db.ExecuteAsync(scope, transaction =>
                    ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT ready FROM gp_stream_state WHERE singleton=1"), CancellationToken.None);
                Require(readyState == 1 && confirmed > 0, "seed_or_ready_absent");
                var checkpoint = await store.GetPullCheckpointAsync(CancellationToken.None);
                Require(checkpoint.CopyCursor() is { Length: > 0 }, "pull_cursor_absent");
                Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", mode = "probe", sdkRevision = options["--sdk-revision"], backendRevision = options["--backend-revision"], unityRevision = options["--unity-revision"], provider = "CSharp production HTTP/MessagePack/SQLite", confirmed, ready = readyState, operation = operation.Value, ownerMismatchDenied = true, lostResponseRecovered = expectLost, repeatedOperationAccepted = true, duplicateSourceAccepted = true, stream = stream.Value, installation }));
            }
            finally { Require(await db.DisposeAsync(TimeSpan.FromSeconds(5)), "sqlite_close_failed"); }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("LIVE_PEER_FAILED: " + error.GetType().Name + ": " + error.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        if ((args.Length != 11 && args.Length != 13) || args[0] != "probe") throw new ArgumentException("nonempty_probe_selection_and_five_options_required");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!values.TryAdd(args[i], args[i + 1]) || string.IsNullOrWhiteSpace(args[i + 1])) throw new ArgumentException("invalid_cli_option");
        }
        foreach (var key in new[] { "--live-file", "--sqlite", "--sdk-revision", "--backend-revision", "--unity-revision" })
            if (!values.ContainsKey(key)) throw new ArgumentException("missing_" + key);
        if (values.Count != 5 && !(values.Count == 6 && values.TryGetValue("--expect-lost-response", out var lost) && lost == "true"))
            throw new ArgumentException("unknown_cli_option");
        foreach (var key in new[] { "--sdk-revision", "--backend-revision", "--unity-revision" })
            if (values[key].Length != 40 || !values[key].All(Uri.IsHexDigit)) throw new ArgumentException("revision_must_be_full_sha_" + key);
        if (File.Exists(values["--sqlite"])) throw new ArgumentException("fresh_sqlite_path_required");
        return values;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class StaticAuth : IAuthSession
    {
        private readonly AccessTokenSnapshot token;
        public StaticAuth(string value) { token = new AccessTokenSnapshot(value, 0); }
        public string SessionKey => "live-peer-disposable";
        public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(token);
        public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) => throw new InvalidOperationException("live_peer_auth_refresh_not_configured");
    }
    private sealed class NoopProjector : IPrivateSyncProjectionProjector
    {
        public static readonly NoopProjector Instance = new();
        public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group) { }
        public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot) { }
    }
}
