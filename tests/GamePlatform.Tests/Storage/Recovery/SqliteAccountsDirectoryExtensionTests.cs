#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Sqlite.Features.Accounts;

namespace GamePlatform.Tests.Storage.Recovery
{
    public sealed class SqliteAccountsDirectoryExtensionTests
    {
        [Fact]
        public async Task NewReservationCallbackIsAtomicRunsOnceAndBorrowExpires()
        {
            var folder = Path.Combine(Path.GetTempPath(), "gp-directory-extension", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var app = new AppId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
            var scope = new AccountDirectoryScope(new BackendNamespace("extension-test"), app);
            var principal = new AccountPrincipalDescriptor(scope.BackendNamespace, "issuer", "subject");
            var installation = Guid.Parse("0199f9a0-3333-7777-8888-999999999999");
            var stream = new ClientStreamId(Guid.Parse("0199f9a0-4444-7777-8888-999999999999"));
            var db = await SqliteAccountsDirectoryDatabase.OpenAsync(Path.Combine(folder, "directory.db"), scope, CancellationToken.None);
            try
            {
                await db.ExecuteExtensionAsync(scope, t => t.Execute("CREATE TABLE pb_admission (stream_id TEXT PRIMARY KEY)"), CancellationToken.None);
                SqliteAccountsDirectoryExtensionTransaction? captured = null;
                bool fail = true;
                int calls = 0;
                var directory = new SqliteAccountsDirectoryStore(db, scope, (t, entry) =>
                {
                    captured = t;
                    calls++;
                    t.Execute("INSERT INTO pb_admission VALUES (?)", entry.StreamId.ToString());
                    if (fail) throw new InvalidOperationException("injected crash");
                });
                await Assert.ThrowsAsync<StorageException>(() => directory.ReserveAsync(principal, app, installation, stream, CancellationToken.None));
                Assert.Null(await directory.FindAsync(principal, app, CancellationToken.None));
                Assert.Equal(0, await db.ExecuteExtensionAsync(scope, t => t.ExecuteScalar<int>("SELECT COUNT(*) FROM pb_admission"), CancellationToken.None));
                Assert.Throws<InvalidOperationException>(() => captured!.ExecuteScalar<int>("SELECT COUNT(*) FROM pb_admission"));
                fail = false;
                await directory.ReserveAsync(principal, app, installation, stream, CancellationToken.None);
                await directory.ReserveAsync(principal, app, installation, stream, CancellationToken.None);
                Assert.Equal(2, calls);
                Assert.Equal(1, await db.ExecuteExtensionAsync(scope, t => t.ExecuteScalar<int>("SELECT COUNT(*) FROM pb_admission"), CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(() => db.ExecuteExtensionAsync(new AccountDirectoryScope(new BackendNamespace("other"), app), t => 0, CancellationToken.None));
                await db.ExecuteExtensionAsync(scope, t =>
                {
                    foreach (var sql in new[] { "BEGIN", "COMMIT", "ROLLBACK", "SAVEPOINT borrowed", "RELEASE borrowed", "END", "PRAGMA journal_mode=DELETE", "CREATE TRIGGER pb_bad BEFORE INSERT ON pb_admission BEGIN SELECT 1 END", "UPDATE gp_account_principal_directory SET subject='bad'" })
                        Assert.Throws<ArgumentException>(() => t.Execute(sql));
                    Assert.Throws<InvalidOperationException>(() => directory.HasAnyEntryAsync(CancellationToken.None).GetAwaiter().GetResult());
                    var reentry = Task.Run(() => directory.HasAnyEntryAsync(CancellationToken.None));
                    Assert.True(SpinWait.SpinUntil(() => reentry.IsCompleted, TimeSpan.FromSeconds(5)),
                        "A child task must reject reentry before waiting on the directory writer.");
                    Assert.Throws<InvalidOperationException>(() => reentry.GetAwaiter().GetResult());
                    Exception? otherThreadFailure = null;
                    var otherThread = new Thread(() =>
                    {
                        try { t.ExecuteScalar<int>("SELECT COUNT(*) FROM pb_admission"); }
                        catch (Exception error) { otherThreadFailure = error; }
                    });
                    otherThread.Start();
                    Assert.True(otherThread.Join(TimeSpan.FromSeconds(5)));
                    Assert.IsType<InvalidOperationException>(otherThreadFailure);
                    return true;
                }, CancellationToken.None);
            }
            finally { Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5))); Directory.Delete(folder, true); }
        }
    }
}
