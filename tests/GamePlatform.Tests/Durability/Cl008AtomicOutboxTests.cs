using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Durability
{
    public sealed class Cl008AtomicOutboxTests
    {
        private static readonly AppId AppId = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId UserId = new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("test-backend"), AppId, UserId);
        private static readonly StorageScope Scope = new StorageScope("test-backend", new PlatformId(AppId.ToString()), new PlatformId(UserId.ToString()));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1212-7777-8888-999999999999");
        private static readonly IReadOnlyList<SqliteMigration> Migrations = SqlitePlatformMigrationRegistry.Migrations.Concat(new[]
        {
            new SqliteMigration(8, "test-projection", new[] { "CREATE TABLE test_projection (name TEXT PRIMARY KEY, value INTEGER NOT NULL)" })
        }).ToArray();

        [Fact]
        public async Task ProjectionSequenceAndImmutableOutboxCommitAndReopenTogether()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var store = NewStore(database);
            var command = Draft("0199f9a0-2222-7777-8888-999999999999", new byte[] { 1, 2, 3 });
            var admission = await store.CommitAsync("run-1", command, (transaction, revision) =>
            {
                Assert.Equal(1, revision);
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO test_projection(name, value) VALUES ('score', ?)", revision);
            }, CancellationToken.None);
            Assert.Equal(1, admission.Sequence);
            Assert.Equal(1, admission.LocalRevision);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            var persisted = await reopened.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (
                    Projection: session.ExecuteScalar<long>("SELECT value FROM test_projection WHERE name = 'score'"),
                    Sequence: session.ExecuteScalar<long>("SELECT sequence FROM gp_outbox WHERE operation_id = ?", command.OperationId.ToString()),
                    Body: session.ExecuteScalar<byte[]>("SELECT semantic_body FROM gp_outbox WHERE operation_id = ?", command.OperationId.ToString()),
                    FingerprintLength: session.ExecuteScalar<int>("SELECT length(fingerprint) FROM gp_outbox WHERE operation_id = ?", command.OperationId.ToString()),
                    Next: session.ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton = 1"));
            }, CancellationToken.None);
            Assert.Equal(1, persisted.Projection);
            Assert.Equal(1, persisted.Sequence);
            Assert.Equal(new byte[] { 1, 2, 3 }, persisted.Body);
            Assert.Equal(32, persisted.FingerprintLength);
            Assert.Equal(2, persisted.Next);
            Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task FailureAtEveryWriteBoundaryRollsBackAllThreeEffects()
        {
            foreach (var failurePoint in new[] { AdmissionCheckpoint.ProjectionApplied, AdmissionCheckpoint.SequenceAllocated, AdmissionCheckpoint.OutboxInserted })
            {
                using var files = new TemporaryDatabase();
                var database = await OpenReady(files.Path);
                var store = NewStore(database, observed => { if (observed == failurePoint) throw new InjectedFailureException(); });
                await Assert.ThrowsAsync<InjectedFailureException>(() => store.CommitAsync(
                    "run-fault", Draft("0199f9a0-3333-7777-8888-999999999999", new byte[] { 4 }),
                    (transaction, _) => ((SqliteTransactionSession)transaction).Execute("INSERT INTO test_projection(name, value) VALUES ('score', 1)"),
                    CancellationToken.None));
                var counts = await Inspect(database);
                Assert.Equal((0, 0, 1L, 0L), counts);
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
        }

        [Fact]
        public async Task AdmissionCapturesOneDurableEnvelopeAndDuplicateBusinessRunDoesNotRecaptureIt()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var clock = new CountingClock(1_789_555_200_000);
            var store = NewStore(database, null, clock);
            var draft = Draft("0199f9a0-3344-7777-8888-999999999999", new byte[] { 4, 8 });
            var first = await store.CommitAsync("run-envelope", draft, InsertProjection("envelope"), CancellationToken.None);
            var replay = await store.CommitAsync("run-envelope", draft, InsertProjection("not-applied"), CancellationToken.None);
            var persisted = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Installation: session.ExecuteScalar<string>("SELECT installation_id FROM gp_outbox WHERE operation_id=?", draft.OperationId.ToString()), CreatedAt: session.ExecuteScalar<long>("SELECT client_created_at FROM gp_outbox WHERE operation_id=?", draft.OperationId.ToString()));
            }, CancellationToken.None);
            Assert.Equal(1, clock.Calls);
            Assert.Equal(Installation, first.InstallationId);
            Assert.Equal(first.InstallationId, replay.InstallationId);
            Assert.Equal(1_789_555_200_000, first.ClientCreatedAt);
            Assert.Equal(first.ClientCreatedAt, replay.ClientCreatedAt);
            Assert.Equal(Installation.ToString("D"), persisted.Installation);
            Assert.Equal(first.ClientCreatedAt, persisted.CreatedAt);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AbruptProcessTerminationRollsBackPreCommitAndPreservesPostCommit()
        {
            foreach (var phase in new[] { "precommit", "postcommit" })
            {
                using var files = new TemporaryDatabase();
                var initialized = await OpenReady(files.Path);
                Assert.True(await initialized.DisposeAsync(TimeSpan.FromSeconds(5)));
                var project = FindTestProject();
                var signal = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(files.Path)!, "crash-ready");
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo("dotnet", $"test \"{project}\" -c Release --no-build --no-restore --filter FullyQualifiedName~Cl008AtomicOutboxTests.CrashChild")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.Environment["GP_CL008_CRASH_PATH"] = files.Path;
                process.StartInfo.Environment["GP_CL008_CRASH_PHASE"] = phase;
                process.StartInfo.Environment["GP_CL008_CRASH_SIGNAL"] = signal;
                Assert.True(process.Start());
                var deadline = Stopwatch.StartNew();
                while (!File.Exists(signal) && !process.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(25);
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(10_000), "Crash probe did not terminate in time.");
                Assert.True(File.Exists(signal), "Crash probe did not reach the requested write boundary.");
                Assert.NotEqual(0, process.ExitCode);

                var recovered = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
                var counts = await Inspect(recovered);
                Assert.Equal(phase == "precommit" ? (0, 0, 1L, 0L) : (1, 1, 2L, 1L), counts);
                Assert.True(await recovered.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
        }

        [Fact]
        public async Task CrashChild()
        {
            var path = Environment.GetEnvironmentVariable("GP_CL008_CRASH_PATH");
            var phase = Environment.GetEnvironmentVariable("GP_CL008_CRASH_PHASE");
            var signal = Environment.GetEnvironmentVariable("GP_CL008_CRASH_SIGNAL");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(phase) || string.IsNullOrEmpty(signal)) return;
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            var checkpoint = phase == "precommit"
                ? new Action<AdmissionCheckpoint>(observed =>
                {
                    if (observed != AdmissionCheckpoint.OutboxInserted) return;
                    File.WriteAllText(signal, "precommit");
                    Thread.Sleep(Timeout.Infinite);
                })
                : null;
            var store = NewStore(database, checkpoint);
            await store.CommitAsync("run-crash", Draft("0199f9a0-aaaa-7777-8888-999999999999", new byte[] { 11 }), InsertProjection("crash"), CancellationToken.None);
            File.WriteAllText(signal, "postcommit");
            Thread.Sleep(Timeout.Infinite);
        }

        [Fact]
        public async Task ConcurrentDuplicateReturnsOriginalIdentityWithoutRepeatingProjection()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var store = NewStore(database);
            var command = Draft("0199f9a0-4444-7777-8888-999999999999", new byte[] { 5, 6 });
            Action<ILocalStorageTransaction, long> projection = (transaction, _) =>
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO test_projection(name, value) VALUES ('once', 1)");
            var first = store.CommitAsync("run-duplicate", command, projection, CancellationToken.None);
            var second = store.CommitAsync("run-duplicate", command, projection, CancellationToken.None);
            var results = await Task.WhenAll(first, second);
            Assert.Equal(results[0].Sequence, results[1].Sequence);
            Assert.Equal(results[0].LocalRevision, results[1].LocalRevision);
            var counts = await Inspect(database);
            Assert.Equal((1, 1, 2L, 1L), counts);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ChangedBodyOperationOrBusinessRunConflictsWithoutMutation()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var store = NewStore(database);
            var original = Draft("0199f9a0-5555-7777-8888-999999999999", new byte[] { 7 });
            await store.CommitAsync("run-conflict", original, InsertProjection("original"), CancellationToken.None);
            var changedBody = Draft(original.OperationId.ToString(), new byte[] { 8 });
            var bodyConflict = await Assert.ThrowsAsync<StorageException>(() => store.CommitAsync("run-conflict", changedBody, InsertProjection("changed"), CancellationToken.None));
            Assert.Equal(StorageFailure.IdentityConflict, bodyConflict.Failure);
            var differentOperation = Draft("0199f9a0-6666-7777-8888-999999999999", new byte[] { 7 });
            var runConflict = await Assert.ThrowsAsync<StorageException>(() => store.CommitAsync("run-conflict", differentOperation, InsertProjection("other"), CancellationToken.None));
            Assert.Equal(StorageFailure.IdentityConflict, runConflict.Failure);
            Assert.Equal((1, 1, 2L, 1L), await Inspect(database));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task NotReadyForeignStreamAndSequenceOverflowFailClosed()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            var store = NewStore(database);
            var command = Draft("0199f9a0-7777-7777-8888-999999999999", new byte[] { 9 });
            var notReady = await Assert.ThrowsAsync<StorageException>(() => store.CommitAsync("run-not-ready", command, InsertProjection("no"), CancellationToken.None));
            Assert.Equal(StorageFailure.NotReady, notReady.Failure);
            await SeedReady(database, long.MaxValue);
            var exhausted = await Assert.ThrowsAsync<StorageException>(() => store.CommitAsync("run-overflow", command, InsertProjection("no"), CancellationToken.None));
            Assert.Equal(StorageFailure.SequenceExhausted, exhausted.Failure);
            var foreign = new CommandDraft(Owner, command.OperationId,
                new ClientStreamId(Guid.Parse("0199f9a0-8888-7777-8888-999999999999")), "profile.patch", 1, 1, new byte[] { 9 });
            var stream = await Assert.ThrowsAsync<StorageException>(() => store.CommitAsync("run-stream", foreign, InsertProjection("no"), CancellationToken.None));
            Assert.Equal(StorageFailure.StreamRecoveryRequired, stream.Failure);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RecoveryDetectsGapsAndStaleCloneAndExpiredLeaseKeepsIdentity()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var store = NewStore(database);
            var command = Draft("0199f9a0-9999-7777-8888-999999999999", new byte[] { 10 });
            await store.CommitAsync("run-recovery", command, InsertProjection("recovery"), CancellationToken.None);
            Assert.Equal(StreamRecoveryState.Current, (await store.AssessRecoveryAsync(0, CancellationToken.None)).State);
            Assert.Equal(StreamRecoveryState.ServerAheadOfLocalState, (await store.AssessRecoveryAsync(2, CancellationToken.None)).State);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("UPDATE gp_outbox SET delivery_state = 'in_flight', leased_until = 100 WHERE operation_id = ?", command.OperationId.ToString());
                session.Execute("UPDATE gp_stream_state SET next_sequence = 3 WHERE singleton = 1");
                return true;
            }, CancellationToken.None);
            Assert.Equal(StreamRecoveryState.LocalGap, (await store.AssessRecoveryAsync(0, CancellationToken.None)).State);
            Assert.Equal(1, await store.RecoverExpiredLeasesAsync(100, CancellationToken.None));
            var state = await database.ExecuteAsync(Scope, transaction =>
                ((SqliteTransactionSession)transaction).ExecuteScalar<string>("SELECT delivery_state FROM gp_outbox WHERE operation_id = ?", command.OperationId.ToString()), CancellationToken.None);
            Assert.Equal("pending", state);
            var immutable = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_outbox SET semantic_body = X'00' WHERE operation_id = ?", command.OperationId.ToString());
                return true;
            }, CancellationToken.None));
            Assert.Equal(StorageFailure.Constraint, immutable.Failure);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static SqliteAtomicCommandStore NewStore(SqliteDatabase database, Action<AdmissionCheckpoint>? checkpoint = null, IUnixMillisecondClock? clock = null) =>
            new SqliteAtomicCommandStore(database, Scope, Owner, new TestFingerprint(), clock ?? new FixedClock(), checkpoint);

        private static CommandDraft Draft(string operationId, byte[] body) => new CommandDraft(
            Owner, new OperationId(Guid.Parse(operationId)), Stream, "profile.patch", 1, 1, body);

        private static Action<ILocalStorageTransaction, long> InsertProjection(string name) => (transaction, revision) =>
            ((SqliteTransactionSession)transaction).Execute("INSERT INTO test_projection(name, value) VALUES (?, ?)", name, revision);

        private static async Task<SqliteDatabase> OpenReady(string path)
        {
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            await SeedReady(database, 1);
            return database;
        }

        private static Task<bool> SeedReady(SqliteDatabase database, long nextSequence) => database.ExecuteAsync(Scope, transaction =>
        {
            ((SqliteTransactionSession)transaction).Execute(
                "INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) VALUES (1, ?, ?, ?, ?, ?, 1, ?, 0, 0)",
                Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString(), Installation.ToString("D"), nextSequence);
            return true;
        }, CancellationToken.None);

        private static Task<(int Projection, int Outbox, long Next, long Revision)> Inspect(SqliteDatabase database) => database.ExecuteAsync(Scope, transaction =>
        {
            var session = (SqliteTransactionSession)transaction;
            return (
                session.ExecuteScalar<int>("SELECT COUNT(*) FROM test_projection"),
                session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"),
                session.ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton = 1"),
                session.ExecuteScalar<long>("SELECT local_revision FROM gp_stream_state WHERE singleton = 1"));
        }, CancellationToken.None);

        private static string FindTestProject()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "tests", "GamePlatform.Tests", "GamePlatform.Tests.csproj");
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("The test project could not be located for the crash probe.");
        }

        private sealed class TestFingerprint : ICommandFingerprint
        {
            public int GetFingerprintLength(int fingerprintVersion) => fingerprintVersion == 1 ? 32 : 0;
            public void Compute(OwnerScope owner, OperationId operationId, ClientStreamId streamId, Guid installationId, long sequence,
                string operationKind, int schemaVersion, int fingerprintVersion, long clientCreatedAt, ReadOnlySpan<byte> semanticBody, Span<byte> destination)
            {
                var prefix = Encoding.UTF8.GetBytes(owner.Backend.Value + "|" + owner.AppId + "|" + owner.UserId + "|" + operationId + "|" + streamId + "|" + installationId + "|" + sequence + "|" + operationKind + "|" + schemaVersion + "|" + fingerprintVersion + "|" + clientCreatedAt + "|");
                var bytes = new byte[prefix.Length + semanticBody.Length];
                prefix.CopyTo(bytes, 0);
                semanticBody.CopyTo(bytes.AsSpan(prefix.Length));
                SHA256.HashData(bytes).CopyTo(destination);
            }
        }

        private sealed class FixedClock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1_789_555_200_000; }
        private sealed class CountingClock : IUnixMillisecondClock { private readonly long value; public CountingClock(long value) { this.value = value; } public int Calls { get; private set; } public long GetUnixMilliseconds() { Calls++; return value; } }

        private sealed class InjectedFailureException : Exception { }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl008", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        }
    }
}
