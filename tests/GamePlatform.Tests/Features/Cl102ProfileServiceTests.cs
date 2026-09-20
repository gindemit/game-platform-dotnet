#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Profiles;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Features
{
    /// <summary>SQLite cases use the pinned native store; remote cases explicitly use an injected, non-production port.</summary>
    public sealed class Cl102ProfileServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("profile-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("profile-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1212-7777-8888-999999999999");
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 7);
        private static readonly IReadOnlyList<SqliteMigration> Migrations = SqlitePlatformMigrationRegistry.Migrations;

        [Fact]
        public async Task RealSqliteEditOutboxAndPendingProjectionCommitTogetherAndSurviveReopen()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Garden", 1, new byte[] { 7, 8 })));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, new byte[] { 7, 8 }), CancellationToken.None);
            var request = Request("0199f9a0-2222-7777-8888-999999999999", 1, "profile-edit-1", "Rose");
            var pending = await service.EditAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, pending.State);
            Assert.Equal("Garden", pending.Value!.Confirmed!.DisplayName);
            Assert.Equal("Rose", pending.Value.Pending!.Patch.DisplayName);
            var duplicate = await service.EditAsync(request, CancellationToken.None);
            Assert.Equal(pending.Value.Pending.OperationId, duplicate.Value!.Pending!.OperationId);
            var persisted = await database.ExecuteAsync(Scope, tx =>
            {
                var session = (SqliteTransactionSession)tx;
                return (Outbox: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox"),
                    Pending: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='profiles' AND entity_key='self.pending' AND length(payload)>0"),
                    Extension: session.ExecuteScalar<byte[]>("SELECT extensions FROM gp_feature_state WHERE feature_namespace='profiles' AND entity_key='self'"));
            }, CancellationToken.None);
            Assert.Equal(1, persisted.Outbox); Assert.Equal(1, persisted.Pending); Assert.Equal(new byte[] { 7, 8 }, persisted.Extension);
            service.Dispose();
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, seed: false);
            var restored = Create(reopened, new ImmediateRemote(Context, Confirmed("Garden", 1, new byte[] { 7, 8 })));
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, cached.State);
            Assert.Equal(new byte[] { 7, 8 }, cached.Value!.Confirmed!.CopyExtensions());
            Assert.Equal(request.OperationId, cached.Value.Pending!.OperationId);
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AcceptedReceiptThenMatchingPullConfirmsWithoutInventingAuditOrGrant()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Rose", 2, new byte[] { 1 })));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, new byte[] { 1 }), CancellationToken.None);
            var request = Request("0199f9a0-3333-7777-8888-999999999999", 1, "profile-edit-2", "Rose");
            await service.EditAsync(request, CancellationToken.None);
            var accepted = await service.MarkAcceptedAwaitingPullAsync(new ProfileUpdateAcceptance(Context, request.OperationId, 2), CancellationToken.None);
            Assert.Equal(ProfilePendingStatus.AcceptedAwaitingPull, accepted.Value!.Pending!.Status);
            await service.ApplyConfirmedAsync(Context, Confirmed("Rose", 2, new byte[] { 1 }), CancellationToken.None);
            var confirmed = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, confirmed.State);
            Assert.Null(confirmed.Value!.Pending); Assert.Equal("Rose", confirmed.Value.Confirmed!.DisplayName);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedProjectionSharesCursorTransactionAndSuppressesMatchingAcceptedPendingAfterReopen()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Rose", 2, new byte[] { 1 })));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, new byte[] { 1 }), CancellationToken.None);
            var request = Request("0199f9a0-0000-7000-8000-000000000001", 1, "profile-borrowed-accepted", "Rose");
            await service.EditAsync(request, CancellationToken.None);
            await service.MarkAcceptedAwaitingPullAsync(new ProfileUpdateAcceptance(Context, request.OperationId, 2), CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cl102_cursor_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);

            await Assert.ThrowsAsync<InjectedFailure>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, 1, Confirmed("Rose", 2, new byte[] { 1 }));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_cursor_sentinel VALUES (?)", 1);
                throw new InjectedFailure();
            }, CancellationToken.None));
            Assert.Equal(0, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl102_cursor_sentinel"), CancellationToken.None));
            Assert.Equal(1, (await service.ReadCachedAsync(Context, CancellationToken.None)).Revision);

            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, 1, Confirmed("Rose", 2, new byte[] { 1 }));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_cursor_sentinel VALUES (?)", 2);
                return true;
            }, CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, false); var restored = Create(reopened, new ImmediateRemote(Context, Confirmed("Rose", 2, new byte[] { 1 })));
            var cached = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, cached.State); Assert.Equal(2, cached.Revision);
            Assert.Null(cached.Value!.Pending); Assert.Equal("Rose", cached.Value.Confirmed!.DisplayName);
            Assert.Equal(1, await reopened.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl102_cursor_sentinel WHERE value=2"), CancellationToken.None));
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedProjectionRejectsSameOrLowerOverwriteAndSurfacesForeignPendingConflict()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Remote", 3, new byte[] { 3 })));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, new byte[] { 1 }), CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cl102_overwrite_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, 1, Confirmed("Rose", 2, new byte[] { 2 }));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_overwrite_sentinel VALUES (?)", 1); return true;
            }, CancellationToken.None);
            await Assert.ThrowsAsync<ProfileConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_overwrite_sentinel VALUES (?)", 2);
                service.ApplyConfirmedProjection(transaction, Context, 2, Confirmed("Changed", 2, new byte[] { 9 })); return true;
            }, CancellationToken.None));
            await Assert.ThrowsAsync<ProfileConflictException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_overwrite_sentinel VALUES (?)", 3);
                service.ApplyConfirmedProjection(transaction, Context, 2, Confirmed("Older", 1, new byte[] { 9 })); return true;
            }, CancellationToken.None));
            Assert.Equal(1, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl102_overwrite_sentinel"), CancellationToken.None));
            Assert.Equal("Rose", (await service.ReadCachedAsync(Context, CancellationToken.None)).Value!.Confirmed!.DisplayName);

            await service.EditAsync(Request("0199f9a0-0000-7000-8000-000000000002", 2, "profile-borrowed-foreign", "Local"), CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { service.ApplyConfirmedProjection(transaction, Context, 2, Confirmed("Remote", 3, new byte[] { 3 })); return true; }, CancellationToken.None);
            var conflict = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Error, conflict.State); Assert.Equal(ProfilePendingStatus.Conflict, conflict.Value!.Pending!.Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ConfirmedRevisionZeroIsDistinctFromAbsenceAndSurvivesBorrowedRollbackCommitAndReopen()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Bootstrap", 0, new byte[] { 4 })));
            Assert.Equal(FeatureSnapshotState.Missing, (await service.ReadCachedAsync(Context, CancellationToken.None)).State);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("CREATE TABLE cl102_zero_sentinel (value INTEGER NOT NULL)"); return true; }, CancellationToken.None);

            await Assert.ThrowsAsync<InjectedFailure>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, null, Confirmed("Bootstrap", 0, new byte[] { 4 }));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_zero_sentinel VALUES (?)", 1);
                throw new InjectedFailure();
            }, CancellationToken.None));
            Assert.Equal(0, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl102_zero_sentinel"), CancellationToken.None));
            Assert.Equal(FeatureSnapshotState.Missing, (await service.ReadCachedAsync(Context, CancellationToken.None)).State);

            await database.ExecuteAsync(Scope, transaction =>
            {
                service.ApplyConfirmedProjection(transaction, Context, null, Confirmed("Bootstrap", 0, new byte[] { 4 }));
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO cl102_zero_sentinel VALUES (?)", 2); return true;
            }, CancellationToken.None);
            Assert.Equal(1L, await database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE feature_namespace='profiles' AND entity_key='self'"), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await OpenReady(files.Path, false); var restored = Create(reopened, new ImmediateRemote(Context, Confirmed("Bootstrap", 0, new byte[] { 4 })));
            var zero = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, zero.State); Assert.Equal(0, zero.Revision); Assert.Equal(0, zero.Value!.Confirmed!.Revision);
            await restored.ApplyConfirmedAsync(Context, Confirmed("Bootstrap", 0, new byte[] { 4 }), CancellationToken.None);
            await Assert.ThrowsAsync<ProfileConflictException>(() => restored.ApplyConfirmedAsync(Context, Confirmed("Changed", 0, new byte[] { 4 }), CancellationToken.None));
            await restored.ApplyConfirmedAsync(Context, Confirmed("First", 1, new byte[] { 5 }), CancellationToken.None);
            var one = await restored.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(1, one.Revision); Assert.Equal(1, one.Value!.Confirmed!.Revision);
            Assert.Equal(1, await reopened.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM cl102_zero_sentinel WHERE value=2"), CancellationToken.None));
            restored.Dispose(); Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task NewerForeignRevisionMarksUnreceiptedLocalEditAsConflict()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var service = Create(database, new ImmediateRemote(Context, Confirmed("Other device", 2, Array.Empty<byte>())));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, Array.Empty<byte>()), CancellationToken.None);
            await service.EditAsync(Request("0199f9a0-4444-7777-8888-999999999999", 1, "profile-edit-3", "Local"), CancellationToken.None);
            await service.ApplyConfirmedAsync(Context, Confirmed("Other device", 2, Array.Empty<byte>()), CancellationToken.None);
            var snapshot = await service.ReadCachedAsync(Context, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Error, snapshot.State);
            Assert.Equal("profile_revision_conflict", snapshot.DiagnosticCode);
            Assert.Equal(ProfilePendingStatus.Conflict, snapshot.Value!.Pending!.Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RefreshCoalescesExactScopeAndDoesNotErasePendingEdit()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var remote = new ControlledRemote();
            var service = Create(database, remote);
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, new byte[] { 3 }), CancellationToken.None);
            await service.EditAsync(Request("0199f9a0-5555-7777-8888-999999999999", 1, "profile-edit-4", "Local"), CancellationToken.None);
            var one = service.RefreshAsync(Context, CancellationToken.None);
            var two = service.RefreshAsync(Context, CancellationToken.None);
            Assert.Equal(1, remote.Calls);
            remote.Complete(new ProfileRemoteRead(Context, Confirmed("Garden", 1, new byte[] { 3 })));
            var snapshots = await Task.WhenAll(one, two);
            Assert.All(snapshots, item => Assert.Equal(FeatureSnapshotState.Pending, item.State));
            Assert.Equal("Local", snapshots[0].Value!.Pending!.Patch.DisplayName);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ForeignLateRemoteAndWrongExpectedRevisionFailClosed()
        {
            using var files = new TemporaryDatabase(); var database = await OpenReady(files.Path);
            var foreign = new ScopedOwnerContext(Owner, new SemanticId("private"), 8);
            var service = Create(database, new ImmediateRemote(foreign, Confirmed("Foreign", 1, Array.Empty<byte>())));
            await service.ApplyConfirmedAsync(Context, Confirmed("Garden", 1, Array.Empty<byte>()), CancellationToken.None);
            await Assert.ThrowsAsync<ProfileOwnerMismatchException>(() => service.RefreshAsync(Context, CancellationToken.None));
            await Assert.ThrowsAsync<ProfileConflictException>(() => service.EditAsync(Request("0199f9a0-6666-7777-8888-999999999999", 0, "profile-edit-5", "Wrong"), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void PatchRejectsAuditFieldsByConstructionAndInvalidOrDeepExtensionsFail()
        {
            Assert.Throws<ArgumentException>(() => new ProfilePatch(1, false, null, false, null, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProfilePatch(1, true, " not canonical ", false, null, false, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileConfirmed("Name", false, null, false, null, 1, 1, new byte[16_385]));
            var codec = new TestCodec();
            Assert.Throws<InvalidOperationException>(() => codec.ValidateExtensions(new byte[] { 0xff }));
            Assert.Null(typeof(ProfilePatch).GetProperty("CreatedBy"));
            Assert.Null(typeof(ProfilePatch).GetProperty("UpdatedBy"));
        }

        private static ProfileService Create(SqliteDatabase database, IProfileRemote remote) => new ProfileService(Context, Scope,
            new SqliteDurableFeatureStateStore(database, Scope), database,
            new SqliteAtomicCommandStore(database, Scope, Owner, new TestFingerprint(), new FixedClock()), new TestCodec(), remote,
            8, TimeSpan.FromMinutes(1), () => 1000);
        private static ProfileConfirmed Confirmed(string name, long revision, byte[] extension) => new ProfileConfirmed(name, false, null, false, null, revision, 100, extension);
        private static ProfileUpdateRequest Request(string operation, long expected, string source, string name) => new ProfileUpdateRequest(Context,
            new OperationId(Guid.Parse(operation)), Stream, source, new ProfilePatch(expected, true, name, false, null, false, null));
        private static async Task<SqliteDatabase> OpenReady(string path, bool seed = true)
        {
            var database = await SqliteDatabase.OpenAsync(path, Scope, Migrations, CancellationToken.None);
            if (seed) await database.ExecuteAsync(Scope, tx =>
            {
                ((SqliteTransactionSession)tx).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) SELECT 1, ?, ?, ?, ?, ?, 1, 1, 0, 0 WHERE NOT EXISTS (SELECT 1 FROM gp_stream_state)", Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString(), Installation.ToString("D"));
                return true;
            }, CancellationToken.None);
            return database;
        }

        private sealed class ImmediateRemote : IProfileRemote
        {
            private readonly ScopedOwnerContext owner; private readonly ProfileConfirmed value;
            public ImmediateRemote(ScopedOwnerContext owner, ProfileConfirmed value) { this.owner = owner; this.value = value; }
            public Task<ProfileRemoteRead> ReadAsync(ScopedOwnerContext _, CancellationToken __) => Task.FromResult(new ProfileRemoteRead(owner, value));
        }
        private sealed class ControlledRemote : IProfileRemote
        {
            private readonly TaskCompletionSource<ProfileRemoteRead> completion = new TaskCompletionSource<ProfileRemoteRead>(TaskCreationOptions.RunContinuationsAsynchronously);
            public int Calls { get; private set; }
            public Task<ProfileRemoteRead> ReadAsync(ScopedOwnerContext _, CancellationToken __) { Calls++; return completion.Task; }
            public void Complete(ProfileRemoteRead value) => completion.TrySetResult(value);
        }
        private sealed class TestCodec : IProfileStateCodec
        {
            public byte[] EncodeConfirmed(ProfileConfirmed value) => Text("C", value.DisplayName, value.HasAvatarKey ? "1" : "0", value.AvatarKey, value.HasLocale ? "1" : "0", value.Locale);
            public ProfileConfirmed DecodeConfirmed(long revision, long at, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions)
            {
                var p = Parts(payload, 6); return new ProfileConfirmed(p[1], p[2] == "1", Null(p[3]), p[4] == "1", Null(p[5]), revision, at, extensions);
            }
            public byte[] EncodePending(PendingProfileEdit value) => Text("P", value.OperationId.ToString(), value.StreamId.ToString(), value.BusinessSource, value.Patch.ExpectedRevision.ToString(), value.Patch.HasDisplayName ? "1" : "0", value.Patch.DisplayName, value.Patch.HasAvatarKey ? "1" : "0", value.Patch.AvatarKey, value.Patch.HasLocale ? "1" : "0", value.Patch.Locale, ((int)value.Status).ToString(), value.AcceptedRevision?.ToString());
            public PendingProfileEdit DecodePending(long localRevision, ReadOnlySpan<byte> payload)
            {
                var p = Parts(payload, 13); return new PendingProfileEdit(new OperationId(Guid.Parse(p[1])), new ClientStreamId(Guid.Parse(p[2])), p[3], new ProfilePatch(long.Parse(p[4]), p[5] == "1", Null(p[6]), p[7] == "1", Null(p[8]), p[9] == "1", Null(p[10])), localRevision, (ProfilePendingStatus)int.Parse(p[11]), string.IsNullOrEmpty(p[12]) ? null : long.Parse(p[12]));
            }
            public byte[] EncodePatchCommand(ProfilePatch value) => Text("patch", value.ExpectedRevision.ToString(), value.HasDisplayName ? "1" : "0", value.DisplayName, value.HasAvatarKey ? "1" : "0", value.AvatarKey, value.HasLocale ? "1" : "0", value.Locale);
            public void ValidateExtensions(ReadOnlySpan<byte> extensions) { if (extensions.Length > 16_384 || (extensions.Length > 0 && extensions[0] == 0xff)) throw new InvalidOperationException("Unsupported extension shape."); }
            private static byte[] Text(params string?[] values) => Encoding.UTF8.GetBytes(string.Join("|", Array.ConvertAll(values, item => item == null ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(item)))));
            private static string[] Parts(ReadOnlySpan<byte> data, int count) { var values = Encoding.UTF8.GetString(data).Split('|'); if (values.Length != count) throw new InvalidOperationException("Invalid test state."); for (var i = 0; i < values.Length; i++) values[i] = Encoding.UTF8.GetString(Convert.FromBase64String(values[i])); return values; }
            private static string? Null(string value) => value.Length == 0 ? null : value;
        }
        private sealed class TestFingerprint : ICommandFingerprint
        {
            public int GetFingerprintLength(int version) => version == 1 ? 32 : 0;
            public void Compute(OwnerScope owner, OperationId operation, ClientStreamId stream, Guid installation, long sequence, string kind, int schema, int fingerprint, long clientCreatedAt, ReadOnlySpan<byte> body, Span<byte> destination) => SHA256.HashData(body).CopyTo(destination);
        }
        private sealed class FixedClock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1_789_555_200_000; }
        private sealed class InjectedFailure : Exception { }
        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl102", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
