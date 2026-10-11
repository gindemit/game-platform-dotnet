#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Recovery;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using GamePlatform.Storage.Sqlite.Recovery;
using SQLite;

namespace GamePlatform.Tests.Storage.Recovery
{
    public sealed class SqliteCommandStreamRecoveryJournalTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly AccountPrincipalDescriptor Principal = new AccountPrincipalDescriptor(new BackendNamespace("recovery-test"), "issuer", "subject");
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-3333-7777-8888-999999999999");
        private static readonly ClientStreamId InitialStream = new ClientStreamId(Guid.Parse("0199f9a0-4444-7777-8888-999999999999"));
        private const string Token = "abcdefghijklmnop";

        [Fact]
        public async Task JournalSurvivesReopenAndAdoptsOnlyAfterRemoteCommitThenAllowsNextRecovery()
        {
            var folder = Path.Combine(Path.GetTempPath(), "gp-recovery-journal", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(Principal.BackendNamespace, App);
            var successor = new ClientStreamId(Guid.Parse("0199f9a0-5555-7777-8888-999999999999"));
            var recoveryId = Guid.Parse("0199f9a0-6666-7777-8888-999999999999");
            var request = MakeRequest(InitialStream, successor, recoveryId, 41);
            try
            {
                var db = await OpenIssued(path, scope);
                var journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                var prepared = await journal.PrepareAsync(request, CancellationToken.None);
                Assert.Equal(CommandStreamRecoveryPhase.Prepared, prepared.Phase);
                Assert.Equal(prepared.Request.RecoveryId, (await journal.PrepareAsync(request, CancellationToken.None)).Request.RecoveryId);
                var altered = MakeRequest(InitialStream, new ClientStreamId(Guid.Parse("0199f9a0-7777-7777-8888-999999999999")), recoveryId, 41);
                await Assert.ThrowsAsync<StorageException>(() => journal.PrepareAsync(altered, CancellationToken.None));
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));

                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                Assert.Equal(CommandStreamRecoveryPhase.Prepared, (await journal.LoadAsync(CancellationToken.None))!.Phase);
                var committed = await journal.RecordRemoteCommitAsync(recoveryId, recoveryId, InitialStream, successor, 41, 1, CancellationToken.None);
                Assert.Equal(CommandStreamRecoveryPhase.RemoteCommitted, committed.Phase);
                Assert.Equal(CommandStreamRecoveryPhase.RemoteCommitted,
                    (await journal.RecordRemoteCommitAsync(recoveryId, recoveryId, InitialStream, successor, 41, 1, CancellationToken.None)).Phase);
                await Assert.ThrowsAsync<StorageException>(() => journal.RecordRemoteCommitAsync(recoveryId, recoveryId, InitialStream, successor, 40, 1, CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(() => journal.RecordRemoteCommitAsync(recoveryId, Guid.Parse("0199f9a0-aaaa-7777-8888-999999999999"), InitialStream, successor, 41, 1, CancellationToken.None));
                Assert.Equal(InitialStream, (await FindIssued(db, scope)).StreamId);

                using (var injection = new SQLiteConnection(path))
                    injection.Execute("CREATE TRIGGER fail_recovery_adoption BEFORE UPDATE OF phase ON gp_command_stream_recovery WHEN NEW.phase=3 BEGIN SELECT RAISE(ABORT,'injected adoption failure'); END");
                await Assert.ThrowsAsync<StorageException>(() => journal.AdoptAsync(recoveryId, CancellationToken.None));
                Assert.Equal(InitialStream, (await FindIssued(db, scope)).StreamId);
                Assert.Equal(CommandStreamRecoveryPhase.RemoteCommitted, (await journal.LoadAsync(CancellationToken.None))!.Phase);
                using (var injection = new SQLiteConnection(path)) injection.Execute("DROP TRIGGER fail_recovery_adoption");

                Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await journal.AdoptAsync(recoveryId, CancellationToken.None)).Phase);
                Assert.Equal(successor, (await FindIssued(db, scope)).StreamId);
                Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await journal.AdoptAsync(recoveryId, CancellationToken.None)).Phase);
                journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await journal.PrepareAsync(request, CancellationToken.None)).Phase);
                Assert.Equal(CommandStreamRecoveryPhase.BootstrapComplete, (await journal.MarkBootstrapCompleteAsync(recoveryId, CancellationToken.None)).Phase);
                Assert.Equal(CommandStreamRecoveryPhase.BootstrapComplete, (await journal.LoadAsync(CancellationToken.None))!.Phase);

                var nextSuccessor = new ClientStreamId(Guid.Parse("0199f9a0-8888-7777-8888-999999999999"));
                var nextRequest = MakeRequest(successor, nextSuccessor, Guid.Parse("0199f9a0-9999-7777-8888-999999999999"), 42);
                var newer = await journal.PrepareAsync(nextRequest, CancellationToken.None);
                Assert.Equal(CommandStreamRecoveryPhase.Prepared, newer.Phase);
                Assert.Equal(nextRequest.RecoveryId, (await journal.LoadAsync(CancellationToken.None))!.Request.RecoveryId);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task WrongOwnerCannotPrepareAndCancellationDoesNotPersistRequest()
        {
            var folder = Path.Combine(Path.GetTempPath(), "gp-recovery-journal", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(Principal.BackendNamespace, App);
            try
            {
                var db = await OpenIssued(path, scope);
                var journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                var forged = new AccountDirectoryEntry(Principal, App, Installation, InitialStream,
                    new PlatformUserId(Guid.Parse("0199f9a1-3333-7777-8888-999999999999")), "active");
                var wrongOwnerJournal = new SqliteCommandStreamRecoveryJournal(db, forged);
                await Assert.ThrowsAsync<StorageException>(() => wrongOwnerJournal.PrepareAsync(
                    new CommandStreamRecoveryRequest(Principal, App, forged.AccountId!.Value, Installation, InitialStream,
                        new ClientStreamId(Guid.Parse("0199f9a1-4444-7777-8888-999999999999")),
                        Guid.Parse("0199f9a1-5555-7777-8888-999999999999"), "backup_restored", Token, 0), CancellationToken.None));
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => journal.PrepareAsync(
                    MakeRequest(InitialStream, new ClientStreamId(Guid.Parse("0199f9a1-1111-7777-8888-999999999999")),
                        Guid.Parse("0199f9a1-2222-7777-8888-999999999999"), 0), cancelled.Token));
                Assert.Null(await journal.LoadAsync(CancellationToken.None));
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task ExpiryIsDurableRetainsHistoryAndCannotBeAdoptedOrCommitted()
        {
            var folder = Path.Combine(Path.GetTempPath(), "gp-recovery-expiry", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(Principal.BackendNamespace, App);
            var request = MakeRequest(InitialStream, new ClientStreamId(Guid.Parse("0199f9a0-5555-7777-8888-999999999999")), Guid.Parse("0199f9a0-6666-7777-8888-999999999999"), 41);
            try
            {
                var db = await OpenIssued(path, scope);
                var journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                await journal.PrepareAsync(request, CancellationToken.None);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                // Reconstruct the original delivered v1 schema with a real prepared request.
                using (var legacy = new SQLiteConnection(path))
                {
                    var sql = legacy.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name='gp_command_stream_recovery'")
                        .Replace("phase BETWEEN 0 AND 4", "phase BETWEEN 1 AND 4").Replace("phase IN (0,1) AND", "phase = 1 AND");
                    legacy.RunInTransaction(() =>
                    {
                        legacy.Execute("ALTER TABLE gp_command_stream_recovery RENAME TO legacy_recovery");
                        legacy.Execute(sql);
                        legacy.Execute("INSERT INTO gp_command_stream_recovery SELECT * FROM legacy_recovery ORDER BY rowid");
                        legacy.Execute("DROP TABLE legacy_recovery");
                        legacy.Execute("UPDATE gp_command_stream_recovery_schema SET version=1,migration_id='command-stream-recovery-journal-v1'");
                    });
                }
                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                Assert.Equal(request.RecoveryId, (await journal.LoadAsync(CancellationToken.None))!.Request.RecoveryId);
                Assert.Equal(CommandStreamRecoveryPhase.Expired, (await journal.MarkExpiredAsync(request.RecoveryId, CancellationToken.None)).Phase);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                journal = new SqliteCommandStreamRecoveryJournal(db, await FindIssued(db, scope));
                Assert.Equal(CommandStreamRecoveryPhase.Expired, (await journal.LoadAsync(CancellationToken.None))!.Phase);
                Assert.Equal(request.InspectionToken, (await journal.LoadAsync(CancellationToken.None))!.Request.InspectionToken);
                await Assert.ThrowsAsync<StorageException>(() => journal.RecordRemoteCommitAsync(request.RecoveryId, request.RecoveryId, request.PriorStreamId, request.SuccessorStreamId, 41, 1, CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(() => journal.AdoptAsync(request.RecoveryId, CancellationToken.None));
                var next = MakeRequest(InitialStream, new ClientStreamId(Guid.Parse("0199f9a0-7777-7777-8888-999999999999")), Guid.Parse("0199f9a0-8888-7777-8888-999999999999"), 41);
                await journal.PrepareAsync(next, CancellationToken.None);
                Assert.Equal(next.RecoveryId, (await journal.LoadAsync(CancellationToken.None))!.Request.RecoveryId);
                using (var inspection = new SQLiteConnection(path))
                {
                    Assert.Equal(2, inspection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_command_stream_recovery"));
                    Assert.Equal(1, inspection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_command_stream_recovery WHERE phase=0 AND recovery_id=? AND inspection_token=?", request.RecoveryId.ToString("D"), request.InspectionToken));
                }
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } }
        }

        private static CommandStreamRecoveryRequest MakeRequest(ClientStreamId prior, ClientStreamId next, Guid recovery, long finalized) =>
            new CommandStreamRecoveryRequest(Principal, App, Account, Installation, prior, next, recovery, "backup_restored", Token, finalized);

        private static async Task<SqliteAccountsDirectoryDatabase> OpenIssued(string path, AccountDirectoryScope scope)
        {
            var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
            var directory = new SqliteAccountsDirectoryStore(db, scope);
            var entry = await directory.ReserveAsync(Principal, App, Installation, InitialStream, CancellationToken.None);
            await directory.BindIssuedAccountAsync(entry, Account, "active", CancellationToken.None);
            return db;
        }

        private static async Task<AccountDirectoryEntry> FindIssued(SqliteAccountsDirectoryDatabase db, AccountDirectoryScope scope) =>
            await new SqliteAccountsDirectoryStore(db, scope).FindAsync(Principal, App, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The issued account directory entry was not found.");
    }
}
