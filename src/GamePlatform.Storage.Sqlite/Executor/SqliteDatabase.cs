#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Migrations;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Executor
{
    public sealed class SqliteDatabase : ISerializedStorageExecutor
    {
        private readonly SQLiteConnection connection;
        private readonly SemaphoreSlim writer = new SemaphoreSlim(1, 1);
        private readonly object lifecycle = new object();
        private TaskCompletionSource<bool> drained = CompletedDrain();
        private bool accepting = true;
        private bool disposed;
        private int admitted;
        private int activeTransactions;
        private int maximumConcurrentTransactions;

        private SqliteDatabase(SQLiteConnection connection, StorageScope scope)
        {
            this.connection = connection;
            Scope = scope;
        }

        public StorageScope Scope { get; }
        public int ConnectionCount => disposed ? 0 : 1;
        public int NativeVersionNumber => connection.LibVersionNumber;
        public int MaximumConcurrentTransactions => Volatile.Read(ref maximumConcurrentTransactions);

        public static Task<SqliteDatabase> OpenAsync(
            string databasePath,
            StorageScope scope,
            IReadOnlyList<SqliteMigration> migrations,
            CancellationToken cancellationToken) =>
            OpenAsync(databasePath, scope, migrations, SqliteDatabaseOptions.Default, null, cancellationToken);

        public static Task<SqliteDatabase> OpenAsync(
            string databasePath,
            StorageScope scope,
            IReadOnlyList<SqliteMigration> migrations,
            SqliteDatabaseOptions options,
            CancellationToken cancellationToken) =>
            OpenAsync(databasePath, scope, migrations, options, null, cancellationToken);

        internal static async Task<SqliteDatabase> OpenAsync(
            string databasePath,
            StorageScope scope,
            IReadOnlyList<SqliteMigration> migrations,
            SqliteDatabaseOptions options,
            Action<MigrationCheckpoint, SqliteMigration>? checkpoint,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A database path is required.", nameof(databasePath));
            if (migrations == null) throw new ArgumentNullException(nameof(migrations));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ValidateMigrationOrder(migrations);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => OpenCore(Path.GetFullPath(databasePath), scope, migrations, options, checkpoint, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        public async Task<T> ExecuteAsync<T>(
            StorageScope scope,
            Func<ILocalStorageTransaction, T> operation,
            CancellationToken cancellationToken)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (!Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The database owner scope does not match.");
            Admit();
            try
            {
                await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await Task.Run(() => ExecuteCore(operation, cancellationToken), CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    writer.Release();
                }
            }
            finally
            {
                ReleaseAdmission();
            }
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
            var timeoutTask = Task.Delay(timeout);
            if (await Task.WhenAny(drainTask, timeoutTask).ConfigureAwait(false) != drainTask) return false;
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
            finally
            {
                writer.Release();
            }
        }

        private static SqliteDatabase OpenCore(
            string databasePath,
            StorageScope scope,
            IReadOnlyList<SqliteMigration> migrations,
            SqliteDatabaseOptions options,
            Action<MigrationCheckpoint, SqliteMigration>? checkpoint,
            CancellationToken cancellationToken)
        {
            SQLiteConnection? connection = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(databasePath);
                if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Database path must have a directory.", nameof(databasePath));
                Directory.CreateDirectory(directory);
                connection = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                connection.BusyTimeout = options.BusyTimeout;
                connection.Execute("PRAGMA foreign_keys = ON");
                if (connection.ExecuteScalar<int>("PRAGMA foreign_keys") != 1)
                    throw new StorageException(StorageFailure.Unavailable, "SQLite foreign keys could not be enabled.");
                var journalMode = connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL");
                if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                    throw new StorageException(StorageFailure.Unavailable, "SQLite WAL journal mode is unavailable.");
                BootstrapScope(connection, scope);
                ApplyMigrations(connection, scope, migrations, options, checkpoint, cancellationToken);
                return new SqliteDatabase(connection, scope);
            }
            catch (Exception error)
            {
                connection?.Dispose();
                throw Map(error, StorageFailure.Migration, "SQLite database initialization failed.");
            }
        }

        private T ExecuteCore<T>(Func<ILocalStorageTransaction, T> operation, CancellationToken cancellationToken)
        {
            var concurrent = Interlocked.Increment(ref activeTransactions);
            UpdateMaximum(concurrent);
            SqliteTransactionSession? session = null;
            try
            {
                connection.BeginTransaction();
                session = new SqliteTransactionSession(connection, Scope);
                var result = operation(session);
                cancellationToken.ThrowIfCancellationRequested();
                connection.Commit();
                session.Close();
                return result;
            }
            catch (Exception error)
            {
                try { connection.Rollback(); }
                catch { }
                session?.Close();
                if (error is OperationCanceledException) throw;
                if (error is SQLiteException) throw Map(error, StorageFailure.Io, "SQLite transaction failed.");
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref activeTransactions);
            }
        }

        private void Admit()
        {
            lock (lifecycle)
            {
                if (!accepting || disposed) throw new StorageException(StorageFailure.Closing, "The database is closing.");
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

        private void UpdateMaximum(int concurrent)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maximumConcurrentTransactions);
                if (observed >= concurrent || Interlocked.CompareExchange(ref maximumConcurrentTransactions, concurrent, observed) == observed) return;
            }
        }

        private static void BootstrapScope(SQLiteConnection connection, StorageScope scope)
        {
            connection.BeginTransaction();
            try
            {
                connection.Execute("CREATE TABLE IF NOT EXISTS gp_database_scope (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL)");
                connection.Execute("CREATE TABLE IF NOT EXISTS gp_migrations (version INTEGER PRIMARY KEY, migration_id TEXT NOT NULL UNIQUE, checksum TEXT NOT NULL, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, applied_at INTEGER NOT NULL)");
                var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_database_scope");
                if (count == 0)
                {
                    connection.Execute("INSERT INTO gp_database_scope(singleton, backend_namespace, app_id, account_id) VALUES (1, ?, ?, ?)", scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value);
                }
                else
                {
                    var matches = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_database_scope WHERE singleton = 1 AND backend_namespace = ? AND app_id = ? AND account_id = ?", scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value);
                    if (matches != 1) throw new StorageException(StorageFailure.InvalidOwner, "The database belongs to another owner scope.");
                }
                connection.Commit();
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }

        private static void ApplyMigrations(
            SQLiteConnection connection,
            StorageScope scope,
            IReadOnlyList<SqliteMigration> migrations,
            SqliteDatabaseOptions options,
            Action<MigrationCheckpoint, SqliteMigration>? checkpoint,
            CancellationToken cancellationToken)
        {
            var recordedMaximum = connection.ExecuteScalar<int>("SELECT COALESCE(MAX(version), 0) FROM gp_migrations");
            var supportedMaximum = migrations.Count == 0 ? 0 : migrations[migrations.Count - 1].Version;
            if (recordedMaximum > supportedMaximum)
                throw new StorageException(StorageFailure.Migration, "The database schema is newer than this SDK supports.");
            foreach (var migration in migrations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var existing = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations WHERE version = ?", migration.Version);
                if (existing == 1)
                {
                    var identityMatches = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations WHERE version = ? AND migration_id = ? AND checksum = ? AND backend_namespace = ? AND app_id = ? AND account_id = ?", migration.Version, migration.Id, migration.Checksum, scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value);
                    if (identityMatches != 1) throw new StorageException(StorageFailure.Migration, "An applied migration identity or checksum changed.");
                    continue;
                }
                if (migration.Version <= recordedMaximum)
                    throw new StorageException(StorageFailure.Migration, "The migration journal has a gap or conflicting version.");
                connection.BeginTransaction();
                try
                {
                    foreach (var statement in migration.Statements) connection.Execute(statement);
                    checkpoint?.Invoke(MigrationCheckpoint.EffectsApplied, migration);
                    var appliedAt = options.NowMilliseconds();
                    if (appliedAt < 0 || appliedAt > 253_402_300_799_999L)
                        throw new StorageException(StorageFailure.Migration, "Migration clock returned an invalid timestamp.");
                    connection.Execute("INSERT INTO gp_migrations(version, migration_id, checksum, backend_namespace, app_id, account_id, applied_at) VALUES (?, ?, ?, ?, ?, ?, ?)", migration.Version, migration.Id, migration.Checksum, scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value, appliedAt);
                    checkpoint?.Invoke(MigrationCheckpoint.MarkerInserted, migration);
                    cancellationToken.ThrowIfCancellationRequested();
                    connection.Commit();
                    recordedMaximum = migration.Version;
                }
                catch
                {
                    connection.Rollback();
                    throw;
                }
            }
        }

        private static void ValidateMigrationOrder(IReadOnlyList<SqliteMigration> migrations)
        {
            for (var index = 0; index < migrations.Count; index++)
            {
                var expected = index + 1;
                if (migrations[index] == null || migrations[index].Version != expected)
                    throw new StorageException(StorageFailure.Migration, "Migrations must be complete and ordered from version one.");
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
}
