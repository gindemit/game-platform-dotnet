#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Sqlite.Executor;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Features.Accounts
{
    /// <summary>Dedicated serialized SQLite owner for the principal directory, before account issuance.</summary>
    public sealed class SqliteAccountsDirectoryDatabase
    {
        private readonly SQLiteConnection connection;
        private readonly SemaphoreSlim writer = new SemaphoreSlim(1, 1);
        private readonly object lifecycle = new object();
        private TaskCompletionSource<bool> drained = CompletedDrain();
        private bool accepting = true;
        private bool disposed;
        private int admitted;

        private SqliteAccountsDirectoryDatabase(SQLiteConnection connection, AccountDirectoryScope scope)
        {
            this.connection = connection;
            Scope = scope;
        }

        public AccountDirectoryScope Scope { get; }
        public int ConnectionCount => disposed ? 0 : 1;

        public static async Task<SqliteAccountsDirectoryDatabase> OpenAsync(
            string databasePath, AccountDirectoryScope scope, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A database path is required.", nameof(databasePath));
            if (!scope.IsValid) throw new ArgumentException("A valid account directory scope is required.", nameof(scope));
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => OpenCore(Path.GetFullPath(databasePath), scope, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        internal async Task<T> ExecuteAsync<T>(
            AccountDirectoryScope scope, Func<SQLiteConnection, T> operation, CancellationToken cancellationToken)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (!Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The principal directory owner scope does not match.");
            Admit();
            try
            {
                await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await Task.Run(() => ExecuteCore(operation, cancellationToken), CancellationToken.None).ConfigureAwait(false);
                }
                finally { writer.Release(); }
            }
            finally { ReleaseAdmission(); }
        }

        public async Task<bool> DisposeAsync(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            Task drainTask;
            lock (lifecycle)
            {
                if (disposed) return true;
                accepting = false;
                drainTask = drained.Task;
            }
            if (await Task.WhenAny(drainTask, Task.Delay(timeout)).ConfigureAwait(false) != drainTask) return false;
            await writer.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (lifecycle)
                {
                    if (disposed) return true;
                    connection.Dispose();
                    disposed = true;
                    return true;
                }
            }
            finally { writer.Release(); }
        }

        private static SqliteAccountsDirectoryDatabase OpenCore(string databasePath, AccountDirectoryScope scope, CancellationToken cancellationToken)
        {
            SQLiteConnection? connection = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(databasePath);
                if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Database path must have a directory.", nameof(databasePath));
                Directory.CreateDirectory(directory);
                connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                connection.BusyTimeout = SqliteDatabaseOptions.Default.BusyTimeout;
                connection.Execute("PRAGMA foreign_keys = ON");
                if (connection.ExecuteScalar<int>("PRAGMA foreign_keys") != 1)
                    throw new StorageException(StorageFailure.Unavailable, "SQLite foreign keys could not be enabled for the account directory.");
                var journalMode = connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL");
                if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                    throw new StorageException(StorageFailure.Unavailable, "SQLite WAL journal mode is unavailable for the account directory.");
                BootstrapOwnerAndSchema(connection, scope, cancellationToken);
                return new SqliteAccountsDirectoryDatabase(connection, scope);
            }
            catch (Exception error)
            {
                connection?.Dispose();
                throw Map(error, StorageFailure.Migration, "Account directory SQLite initialization failed.");
            }
        }

        private static void BootstrapOwnerAndSchema(SQLiteConnection connection, AccountDirectoryScope scope, CancellationToken cancellationToken)
        {
            var migration = SqlitePrincipalDirectoryMigration.Create();
            connection.Execute("BEGIN IMMEDIATE");
            try
            {
                var ownerTableExists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='gp_principal_directory_owner'") == 1;
                var migrationTableExists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='gp_principal_directory_schema_migrations'") == 1;
                if (ownerTableExists != migrationTableExists)
                    throw new StorageException(StorageFailure.Corrupt, "The principal directory owner and migration markers are incomplete.");
                if (!ownerTableExists && connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") != 0)
                    throw new StorageException(StorageFailure.InvalidOwner, "The principal directory path contains an unowned database.");
                connection.Execute("CREATE TABLE IF NOT EXISTS gp_principal_directory_owner (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL)");
                connection.Execute("CREATE TABLE IF NOT EXISTS gp_principal_directory_schema_migrations (version INTEGER PRIMARY KEY, migration_id TEXT NOT NULL UNIQUE, checksum TEXT NOT NULL, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, applied_at INTEGER NOT NULL)");

                var ownerCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_principal_directory_owner");
                if (ownerCount == 0)
                    connection.Execute("INSERT INTO gp_principal_directory_owner(singleton,backend_namespace,app_id) VALUES (1,?,?)", scope.BackendNamespace.Value, scope.AppId.ToString());
                else if (ownerCount != 1 || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_principal_directory_owner WHERE singleton=1 AND backend_namespace=? AND app_id=?", scope.BackendNamespace.Value, scope.AppId.ToString()) != 1)
                    throw new StorageException(StorageFailure.InvalidOwner, "The principal directory belongs to another backend or app.");

                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_principal_directory_schema_migrations WHERE backend_namespace<>? OR app_id<>?", scope.BackendNamespace.Value, scope.AppId.ToString()) != 0)
                    throw new StorageException(StorageFailure.InvalidOwner, "A principal directory migration marker belongs to another backend or app.");

                var markerCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_principal_directory_schema_migrations");
                if (markerCount == 0)
                {
                    foreach (var statement in migration.Statements) connection.Execute(statement);
                    cancellationToken.ThrowIfCancellationRequested();
                    connection.Execute("INSERT INTO gp_principal_directory_schema_migrations(version,migration_id,checksum,backend_namespace,app_id,applied_at) VALUES (?,?,?,?,?,?)", migration.Version, migration.Id, migration.Checksum, scope.BackendNamespace.Value, scope.AppId.ToString(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
                else
                {
                    if (markerCount != 1) throw new StorageException(StorageFailure.Migration, "The principal directory migration journal contains extra or duplicate version rows.");
                    var markerVersion = connection.ExecuteScalar<int>("SELECT version FROM gp_principal_directory_schema_migrations");
                    if (markerVersion != migration.Version) throw new StorageException(StorageFailure.Migration, "The principal directory migration journal has a gap or unsupported version.");
                    var identityMatches = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_principal_directory_schema_migrations WHERE version=? AND migration_id=? AND checksum=? AND backend_namespace=? AND app_id=? AND applied_at BETWEEN 0 AND 253402300799999", migration.Version, migration.Id, migration.Checksum, scope.BackendNamespace.Value, scope.AppId.ToString());
                    if (identityMatches != 1) throw new StorageException(StorageFailure.Migration, "The principal directory migration identity, checksum, owner or timestamp changed.");
                }
                ValidateRequiredSchema(connection);
                cancellationToken.ThrowIfCancellationRequested();
                connection.Execute("COMMIT");
            }
            catch
            {
                try { connection.Execute("ROLLBACK"); }
                catch { }
                throw;
            }
        }

        private static void ValidateRequiredSchema(SQLiteConnection connection)
        {
            const string tableName = "gp_account_principal_directory";
            const string indexName = "gp_account_principal_directory_issued_account";
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", tableName) != 1)
                throw new StorageException(StorageFailure.Migration, "The principal directory data table is missing.");

            var columns = connection.Query<DirectoryColumn>("PRAGMA table_info('gp_account_principal_directory')");
            var expected = new[]
            {
                new DirectoryColumn { Name = "backend_namespace", Type = "TEXT", IsNotNull = 1, PrimaryKey = 1 },
                new DirectoryColumn { Name = "app_id", Type = "TEXT", IsNotNull = 1, PrimaryKey = 2 },
                new DirectoryColumn { Name = "issuer", Type = "TEXT", IsNotNull = 1, PrimaryKey = 3 },
                new DirectoryColumn { Name = "subject", Type = "TEXT", IsNotNull = 1, PrimaryKey = 4 },
                new DirectoryColumn { Name = "installation_id", Type = "TEXT", IsNotNull = 1, PrimaryKey = 0 },
                new DirectoryColumn { Name = "client_stream_id", Type = "TEXT", IsNotNull = 1, PrimaryKey = 0 },
                new DirectoryColumn { Name = "platform_user_id", Type = "TEXT", IsNotNull = 0, PrimaryKey = 0 },
                new DirectoryColumn { Name = "membership_status", Type = "TEXT", IsNotNull = 0, PrimaryKey = 0 }
            };
            if (columns.Count != expected.Length)
                throw new StorageException(StorageFailure.Migration, "The principal directory data table has an unexpected column set.");
            for (var i = 0; i < expected.Length; i++)
            {
                if (!string.Equals(columns[i].Name, expected[i].Name, StringComparison.Ordinal) ||
                    !string.Equals(columns[i].Type, expected[i].Type, StringComparison.OrdinalIgnoreCase) ||
                    columns[i].IsNotNull != expected[i].IsNotNull || columns[i].PrimaryKey != expected[i].PrimaryKey)
                    throw new StorageException(StorageFailure.Migration, "The principal directory data table schema changed.");
            }

            var indexes = connection.Query<DirectoryIndex>("PRAGMA index_list('gp_account_principal_directory')");
            var issuedIndex = indexes.Find(index => string.Equals(index.Name, indexName, StringComparison.Ordinal));
            if (issuedIndex == null || issuedIndex.IsUnique != 1 || issuedIndex.IsPartial != 1)
                throw new StorageException(StorageFailure.Migration, "The principal directory issued-account uniqueness index is missing or malformed.");
            var indexColumns = connection.Query<DirectoryIndexColumn>("PRAGMA index_info('gp_account_principal_directory_issued_account')");
            if (indexColumns.Count != 3 || indexColumns[0].Name != "backend_namespace" || indexColumns[1].Name != "app_id" || indexColumns[2].Name != "platform_user_id")
                throw new StorageException(StorageFailure.Migration, "The principal directory issued-account index columns changed.");
            var indexSql = connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type='index' AND name=?", indexName);
            var normalizedIndexSql = (indexSql ?? string.Empty).Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).ToLowerInvariant();
            if (!normalizedIndexSql.Contains("whereplatform_user_idisnotnull"))
                throw new StorageException(StorageFailure.Migration, "The principal directory issued-account index predicate changed.");
        }

        private sealed class DirectoryColumn
        {
            public int Cid { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            [Column("notnull")] public int IsNotNull { get; set; }
            [Column("pk")] public int PrimaryKey { get; set; }
        }

        private sealed class DirectoryIndex
        {
            [Column("seq")] public int Sequence { get; set; }
            public string Name { get; set; } = string.Empty;
            [Column("unique")] public int IsUnique { get; set; }
            public string Origin { get; set; } = string.Empty;
            [Column("partial")] public int IsPartial { get; set; }
        }

        private sealed class DirectoryIndexColumn
        {
            [Column("seq")] public int Sequence { get; set; }
            public int Cid { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private T ExecuteCore<T>(Func<SQLiteConnection, T> operation, CancellationToken cancellationToken)
        {
            try
            {
                connection.BeginTransaction();
                var result = operation(connection);
                cancellationToken.ThrowIfCancellationRequested();
                connection.Commit();
                return result;
            }
            catch (Exception error)
            {
                try { connection.Rollback(); }
                catch { }
                if (error is OperationCanceledException) throw;
                throw Map(error, StorageFailure.Io, "Account directory SQLite transaction failed.");
            }
        }

        private void Admit()
        {
            lock (lifecycle)
            {
                if (!accepting || disposed) throw new StorageException(StorageFailure.Closing, "The account directory database is closing.");
                if (admitted == 0) drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                admitted++;
            }
        }

        private void ReleaseAdmission()
        {
            lock (lifecycle)
            {
                admitted--;
                if (admitted == 0) drained.TrySetResult(true);
            }
        }

        private static Exception Map(Exception error, StorageFailure fallback, string message)
        {
            if (error is StorageException || error is OperationCanceledException) return error;
            if (error is SQLiteException sqlite)
            {
                var failure = sqlite.Result == SQLite3.Result.Busy || sqlite.Result == SQLite3.Result.Locked ? StorageFailure.Busy :
                    sqlite.Result == SQLite3.Result.Full || sqlite.Result == SQLite3.Result.NoMem ? StorageFailure.Capacity :
                    sqlite.Result == SQLite3.Result.Corrupt || sqlite.Result == SQLite3.Result.NonDBFile ? StorageFailure.Corrupt :
                    sqlite.Result == SQLite3.Result.Constraint ? StorageFailure.Constraint :
                    sqlite.Result == SQLite3.Result.IOError || sqlite.Result == SQLite3.Result.CannotOpen ? StorageFailure.Io : fallback;
                return new StorageException(failure, message, sqlite);
            }
            return new StorageException(fallback, message, error);
        }

        private static TaskCompletionSource<bool> CompletedDrain()
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult(true);
            return source;
        }
    }

    /// <summary>Durable account directory; its database owner is only backend plus app.</summary>
    public sealed class SqliteAccountsDirectoryStore : IAccountDirectoryStore
    {
        private readonly SqliteAccountsDirectoryDatabase database;
        private readonly AccountDirectoryScope scope;

        public SqliteAccountsDirectoryStore(SqliteAccountsDirectoryDatabase database, AccountDirectoryScope scope)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!scope.IsValid) throw new ArgumentException("A valid account directory scope is required.", nameof(scope));
            if (!database.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The account directory scope does not match its database.");
            this.scope = scope;
        }

        public Task<bool> HasAnyEntryAsync(CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection =>
            {
                var total = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_account_principal_directory");
                var inScope = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=?", scope.BackendNamespace.Value, scope.AppId.ToString());
                if (total != inScope) throw new StorageException(StorageFailure.InvalidOwner, "The principal directory contains an entry outside its backend/app scope.");
                return inScope != 0;
            }, cancellationToken);

        public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor principal, AppId appId, CancellationToken cancellationToken)
        {
            Validate(principal, appId);
            return database.ExecuteAsync(scope, connection => Read(connection, principal, appId), cancellationToken);
        }

        public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken)
        {
            Validate(principal, appId);
            if (installationId == Guid.Empty || !UuidIdentity.IsValid(installationId, false) || !streamId.IsValid)
                throw new ArgumentException("New installation and stream identifiers must be UUIDv7.");
            return database.ExecuteAsync(scope, connection =>
            {
                var existing = Read(connection, principal, appId);
                if (existing != null) return existing;
                connection.Execute("INSERT INTO gp_account_principal_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,NULL,NULL)", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject, installationId.ToString("D"), streamId.ToString());
                return new AccountDirectoryEntry(principal, appId, installationId, streamId, null, null);
            }, cancellationToken);
        }

        public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry reservation, PlatformUserId accountId, string membershipStatus, CancellationToken cancellationToken)
        {
            if (reservation == null) throw new ArgumentNullException(nameof(reservation));
            Validate(reservation.Principal, reservation.AppId);
            if (!accountId.IsValid || string.IsNullOrWhiteSpace(membershipStatus) || membershipStatus.Length > 64)
                throw new ArgumentException("A valid issued account and membership are required.");
            return database.ExecuteAsync(scope, connection =>
            {
                var current = Read(connection, reservation.Principal, reservation.AppId);
                if (current == null || current.InstallationId != reservation.InstallationId || current.StreamId != reservation.StreamId)
                    throw new StorageException(StorageFailure.IdentityConflict, "The durable reservation changed.");
                if (current.HasIssuedAccount)
                {
                    if (current.AccountId != accountId || !string.Equals(current.MembershipStatus, membershipStatus, StringComparison.Ordinal))
                        throw new StorageException(StorageFailure.IdentityConflict, "The issued account mapping changed.");
                    return current;
                }
                var updated = connection.Execute("UPDATE gp_account_principal_directory SET platform_user_id=?,membership_status=? WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id IS NULL", accountId.ToString(), membershipStatus, reservation.Principal.BackendNamespace.Value, reservation.AppId.ToString(), reservation.Principal.Issuer, reservation.Principal.Subject);
                if (updated != 1) throw new StorageException(StorageFailure.IdentityConflict, "The durable reservation could not be bound exactly once.");
                return new AccountDirectoryEntry(reservation.Principal, reservation.AppId, reservation.InstallationId, reservation.StreamId, accountId, membershipStatus);
            }, cancellationToken);
        }

        private static AccountDirectoryEntry? Read(SQLiteConnection connection, AccountPrincipalDescriptor principal, AppId appId)
        {
            var rows = connection.Query<DirectoryRow>("SELECT installation_id AS InstallationId,client_stream_id AS ClientStreamId,platform_user_id AS PlatformUserId,membership_status AS MembershipStatus FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject);
            if (rows.Count == 0) return null;
            if (rows.Count != 1) throw new StorageException(StorageFailure.Corrupt, "The account directory contains duplicate principal entries.");
            var row = rows[0];
            try
            {
                var installation = Guid.Parse(row.InstallationId);
                var stream = new ClientStreamId(Guid.Parse(row.ClientStreamId));
                return row.PlatformUserId == null
                    ? new AccountDirectoryEntry(principal, appId, installation, stream, null, null)
                    : new AccountDirectoryEntry(principal, appId, installation, stream, new PlatformUserId(Guid.Parse(row.PlatformUserId)), row.MembershipStatus);
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                throw new StorageException(StorageFailure.Corrupt, "The account directory contains an invalid identity record.", error);
            }
        }

        private void Validate(AccountPrincipalDescriptor principal, AppId appId)
        {
            if (principal == null) throw new ArgumentNullException(nameof(principal));
            if (!appId.IsValid) throw new ArgumentException("A valid app is required.", nameof(appId));
            if (principal.BackendNamespace != scope.BackendNamespace || appId != scope.AppId)
                throw new StorageException(StorageFailure.InvalidOwner, "The principal or app does not belong to this directory scope.");
        }

        private sealed class DirectoryRow
        {
            public string InstallationId { get; set; } = string.Empty;
            public string ClientStreamId { get; set; } = string.Empty;
            public string? PlatformUserId { get; set; }
            public string? MembershipStatus { get; set; }
        }
    }
}
