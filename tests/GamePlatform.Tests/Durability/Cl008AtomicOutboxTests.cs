using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Xunit.Abstractions;

namespace GamePlatform.Tests.Durability
{
    public sealed class Cl008AtomicOutboxTests
    {
        private readonly ITestOutputHelper output;

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

        public Cl008AtomicOutboxTests(ITestOutputHelper output)
        {
            this.output = output;
        }

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
                var launcherStart = process.StartTime;
                var deadline = Stopwatch.StartNew();
                while (!File.Exists(signal) && !process.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(25);
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(10_000), "Crash probe did not terminate in time.");
                Assert.True(File.Exists(signal), "Crash probe did not reach the requested write boundary.");
                Assert.NotEqual(0, process.ExitCode);

                var childPid = ReadChildPid(signal);
                var childPidReused = false;
                var childWait = Stopwatch.StartNew();
                while (childPid != null && IsChildAlive(childPid.Value, out childPidReused) && childWait.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(25);
                var childAliveAtReopen = childPid != null && IsChildAlive(childPid.Value, out childPidReused);
                var shmWait = Stopwatch.StartNew();
                var shmReleased = ProbeShmTruncation(files.Path, out var shmFirst);
                var shmProbe = shmFirst;
                while (!shmReleased && shmWait.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(25);
                    shmReleased = ProbeShmTruncation(files.Path, out shmProbe);
                }
                var diagnostics = $"phase={phase} launcherPid={process.Id} launcherExit={process.ExitCode} childPid={(childPid?.ToString() ?? "unknown")} childExitWaitMs={childWait.ElapsedMilliseconds} childAliveAtReopen={childAliveAtReopen} childPidReused={childPidReused} shmReleaseWaitMs={shmWait.ElapsedMilliseconds} shmReleased={shmReleased} shmFirstProbe=[{shmFirst}] shmProbe=[{shmProbe}]";
                output.WriteLine(diagnostics);

                var sqliteLog = SqliteErrorLog.Start(System.IO.Path.GetDirectoryName(files.Path)!);
                try
                {
                    SqliteDatabase recovered;
                    try
                    {
                        recovered = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        var evidence = CopyEvidence(files.Path, phase);
                        var filesAtFailure = DescribeFiles(files.Path);
                        ProbeShmTruncation(files.Path, out var shmAtFailure);
                        var rawImmediate = ProbeRawConnection(files.Path);
                        var retry = await OpenAndInspect(files.Path);
                        var locks = await ProbeLocksOverTime(files.Path);
                        var census = DescribeProcesses(launcherStart);
                        var attributes = DescribeAttributes(files.Path);
                        var copies = await CopyOpenDiagnostics(files.Path);
                        var raw = ProbeRawConnection(files.Path);
                        var message = $"Reopen after abrupt termination failed. {diagnostics} evidence=[{evidence}] files=[{filesAtFailure}] shmAtFailure=[{shmAtFailure}] rawImmediate=[{rawImmediate}] retryOpen=[{retry}] filesAfterRetry=[{DescribeFiles(files.Path)}] attributes=[{attributes}] locks=[{locks}] processes=[{census}] {copies} raw=[{raw}] sqliteLog=[{sqliteLog}: {SqliteErrorLog.Describe()}] {DescribeException(exception)}";
                        WriteEvidenceText(evidence, message + Environment.NewLine + exception);
                        throw new Xunit.Sdk.XunitException(message, exception);
                    }

                    var counts = await Inspect(recovered);
                    Assert.Equal(phase == "precommit" ? (0, 0, 1L, 0L) : (1, 1, 2L, 1L), counts);
                    Assert.True(await recovered.DisposeAsync(TimeSpan.FromSeconds(5)));
                }
                finally
                {
                    SqliteErrorLog.Stop();
                    if (childAliveAtReopen && IsChildAlive(childPid!.Value, out _)) output.WriteLine($"cleanup childPid={childPid} {KillChild(childPid.Value)}");
                }
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
                    File.WriteAllText(signal, $"precommit:{Environment.ProcessId}");
                    Thread.Sleep(Timeout.Infinite);
                })
                : null;
            var store = NewStore(database, checkpoint);
            await store.CommitAsync("run-crash", Draft("0199f9a0-aaaa-7777-8888-999999999999", new byte[] { 11 }), InsertProjection("crash"), CancellationToken.None);
            File.WriteAllText(signal, $"postcommit:{Environment.ProcessId}");
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

        private static int? ReadChildPid(string signal)
        {
            try
            {
                var parts = File.ReadAllText(signal).Split(':');
                return parts.Length == 2 && int.TryParse(parts[1], out var pid) ? pid : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static bool IsChildAlive(int pid, out bool pidReused)
        {
            pidReused = false;
            try
            {
                using var child = Process.GetProcessById(pid);
                if (child.HasExited) return false;
                var name = child.ProcessName;
                if (name.Equals("testhost", StringComparison.OrdinalIgnoreCase) || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return true;
                pidReused = true;
                return false;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static string KillChild(int pid)
        {
            try
            {
                using var child = Process.GetProcessById(pid);
                child.Kill(entireProcessTree: true);
                return $"killExited={child.WaitForExit(10_000)}";
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception)
            {
                return $"killFailed={exception.GetType().Name}";
            }
        }

        private static async Task<string> ProbeLocksOverTime(string databasePath)
        {
            var parts = new List<string> { "t0 " + ProbeLocks(databasePath) };
            await Task.Delay(250);
            parts.Add("t250 " + ProbeLocks(databasePath));
            await Task.Delay(1750);
            parts.Add("t2000 " + ProbeLocks(databasePath));
            return string.Join(" | ", parts);
        }

        private static string ProbeLocks(string databasePath)
        {
            var results = new List<string>();
            foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
            {
                var name = System.IO.Path.GetFileName(path);
                if (!File.Exists(path))
                {
                    results.Add($"{name}: missing");
                    continue;
                }
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    results.Add($"{name}: locked=false");
                }
                catch (IOException exception)
                {
                    results.Add($"{name}: locked=true win32=0x{exception.HResult:X8} {exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    results.Add($"{name}: locked=unknown win32=0x{exception.HResult:X8} {exception.Message}");
                }
            }
            ProbeShmTruncation(databasePath, out var shm);
            results.Add($"shm-truncate: {shm}");
            return string.Join("; ", results);
        }

        // A FileShare.None open does not see another process's mapping; a shrink reaches the same
        // user-mapped check (0x800704C8) as SQLite's first-connection -shm truncate. The last byte is restored.
        private static bool ProbeShmTruncation(string databasePath, out string result)
        {
            var shrunk = false;
            try
            {
                using var stream = new FileStream(databasePath + "-shm", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                var length = stream.Length;
                if (length == 0)
                {
                    result = "empty";
                    return true;
                }
                stream.Position = length - 1;
                var last = stream.ReadByte();
                stream.SetLength(length - 1);
                shrunk = true;
                stream.SetLength(length);
                stream.Position = length - 1;
                stream.WriteByte((byte)last);
                result = "released";
                return true;
            }
            catch (FileNotFoundException)
            {
                result = "missing";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                result = $"{(shrunk ? "restoreFailed" : "held")} win32=0x{exception.HResult:X8} {exception.GetType().Name}";
                return false;
            }
        }

        private static string CopyEvidence(string databasePath, string phase)
        {
            var root = Environment.GetEnvironmentVariable("GP_CL008_DIAG_DIR");
            if (string.IsNullOrEmpty(root)) root = System.IO.Path.Combine(FindRepositoryRoot(), "artifacts", "diagnostics", "cl008");
            var directory = System.IO.Path.Combine(root, phase + "-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture));
            try
            {
                Directory.CreateDirectory(directory);
                foreach (var source in Directory.GetFiles(System.IO.Path.GetDirectoryName(databasePath)!))
                    File.Copy(source, System.IO.Path.Combine(directory, System.IO.Path.GetFileName(source)));
                return directory;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return $"failed {directory} {exception.GetType().Name}: {exception.Message}";
            }
        }

        private static void WriteEvidenceText(string evidence, string text)
        {
            if (!Directory.Exists(evidence)) return;
            try
            {
                File.WriteAllText(System.IO.Path.Combine(evidence, "diagnostics.txt"), text);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
            }
        }

        private static string DescribeProcesses(DateTime launcherStart)
        {
            var entries = new List<string>();
            foreach (var name in new[] { "testhost", "dotnet", "vstest.console", "datacollector" })
            {
                foreach (var candidate in Process.GetProcessesByName(name))
                {
                    using (candidate)
                    {
                        try
                        {
                            if (candidate.StartTime >= launcherStart) entries.Add($"{candidate.ProcessName}:{candidate.Id}@{candidate.StartTime:HH:mm:ss.fff}");
                        }
                        catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception)
                        {
                            entries.Add($"{name}:{candidate.Id}@unavailable");
                        }
                    }
                }
            }
            return string.Join(", ", entries);
        }

        private static string DescribeAttributes(string databasePath)
        {
            var results = new List<string>();
            foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
            {
                var name = System.IO.Path.GetFileName(path);
                try
                {
                    results.Add($"{name}: {File.GetAttributes(path)} readOnly={new FileInfo(path).IsReadOnly}");
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    results.Add($"{name}: {exception.GetType().Name}");
                }
            }
            return string.Join("; ", results);
        }

        private async Task<string> CopyOpenDiagnostics(string databasePath)
        {
            var withShm = await CopyOpen(databasePath, includeShm: true);
            var withoutShm = await CopyOpen(databasePath, includeShm: false);
            return $"copyOpen={withShm} copyOpenNoShm={withoutShm}";
        }

        private async Task<string> CopyOpen(string databasePath, bool includeShm)
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl008", "diag-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var copyPath = System.IO.Path.Combine(directory, "platform.sqlite3");
                File.Copy(databasePath, copyPath);
                if (File.Exists(databasePath + "-wal")) File.Copy(databasePath + "-wal", copyPath + "-wal");
                if (includeShm && File.Exists(databasePath + "-shm")) File.Copy(databasePath + "-shm", copyPath + "-shm");
                return await OpenAndInspect(copyPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return $"failed {DescribeException(exception)}";
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static async Task<string> OpenAndInspect(string databasePath)
        {
            try
            {
                var database = await SqliteDatabase.OpenAsync(databasePath, Scope, Migrations, CancellationToken.None);
                var counts = await Inspect(database);
                await database.DisposeAsync(TimeSpan.FromSeconds(5));
                return $"ok counts={counts}";
            }
            catch (Exception exception)
            {
                return $"failed {DescribeException(exception)}";
            }
        }

        private static string ProbeRawConnection(string databasePath)
        {
            SQLite.SQLiteConnection? connection = null;
            try
            {
                connection = new SQLite.SQLiteConnection(databasePath, SQLite.SQLiteOpenFlags.ReadWrite | SQLite.SQLiteOpenFlags.FullMutex);
            }
            catch (Exception exception)
            {
                return "open failed " + DescribeRawFailure(exception, null);
            }
            using (connection)
            {
                return "journal_mode=" + RawQuery(connection, "PRAGMA journal_mode") + " integrity_check=" + RawQuery(connection, "PRAGMA integrity_check");
            }
        }

        private static string RawQuery(SQLite.SQLiteConnection connection, string sql)
        {
            try
            {
                return connection.ExecuteScalar<string>(sql);
            }
            catch (Exception exception)
            {
                return "failed " + DescribeRawFailure(exception, connection);
            }
        }

        private static string DescribeRawFailure(Exception exception, SQLite.SQLiteConnection? connection)
        {
            var result = exception is SQLite.SQLiteException sqlite ? sqlite.Result.ToString() : "n/a";
            var extended = connection == null ? "unavailable" : "0x" + ((int)SQLite.SQLite3.ExtendedErrCode(connection.Handle)).ToString("X");
            return $"{exception.GetType().Name} Result={result} Extended={extended}: {exception.Message}";
        }

        private static string DescribeFiles(string databasePath)
        {
            var directory = System.IO.Path.GetDirectoryName(databasePath)!;
            return string.Join(", ", Directory.GetFiles(directory).Select(file => $"{System.IO.Path.GetFileName(file)}={new FileInfo(file).Length}"));
        }

        private static string DescribeException(Exception exception)
        {
            var parts = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
            {
                var result = current.GetType().GetProperty("Result")?.GetValue(current);
                var failure = (current as StorageException)?.Failure;
                parts.Add($"{current.GetType().FullName}: {current.Message}" + (failure != null ? $" failure={failure}" : "") + (result != null ? $" sqliteResult={result}" : ""));
            }
            return string.Join(" <- ", parts);
        }

        private static string FindTestProject() => System.IO.Path.Combine(FindRepositoryRoot(), "tests", "GamePlatform.Tests", "GamePlatform.Tests.csproj");

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, "tests", "GamePlatform.Tests", "GamePlatform.Tests.csproj"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("The test project could not be located for the crash probe.");
        }

        // sqlite3_config is variadic; only the Windows x64 ABI passes the variadic pointers like fixed ones.
        private static class SqliteErrorLog
        {
            private const int ConfigLog = 16;
            private static readonly LogCallback Callback = Record;
            private static readonly ConcurrentQueue<string> Entries = new ConcurrentQueue<string>();
            private static string marker = string.Empty;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate void LogCallback(IntPtr argument, int code, IntPtr message);

            [DllImport(SQLite.SQLite3.LibraryPath, EntryPoint = "sqlite3_config", CallingConvention = CallingConvention.Cdecl)]
            private static extern int Configure(int option, IntPtr callback, IntPtr argument);

            private static bool Supported => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

            public static string Start(string databaseDirectory)
            {
                if (!Supported) return "unsupported";
                Entries.Clear();
                marker = databaseDirectory;
                var result = Configure(ConfigLog, Marshal.GetFunctionPointerForDelegate(Callback), IntPtr.Zero);
                return result == 0 ? "on" : $"configResult={result}";
            }

            public static void Stop()
            {
                if (Supported) Configure(ConfigLog, IntPtr.Zero, IntPtr.Zero);
            }

            public static string Describe() => string.Join(" | ", Entries);

            private static void Record(IntPtr argument, int code, IntPtr message)
            {
                var text = Marshal.PtrToStringUTF8(message) ?? string.Empty;
                var primary = code & 0xFF;
                if (Entries.Count < 32 && (primary == 10 || primary == 14 || text.Contains(marker, StringComparison.OrdinalIgnoreCase))) Entries.Enqueue($"0x{code:X} {text}");
            }
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
