using System;
using System.Text.RegularExpressions;
using GamePlatform.Storage.Abstractions;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Executor
{
    public sealed class SqliteTransactionSession : ILocalStorageTransaction
    {
        private static readonly Regex TransactionControl = new Regex(
            @"^\s*(?:--[^\r\n]*(?:\r?\n|$)|/\*[\s\S]*?\*/\s*)*(?:begin|commit|end|rollback|savepoint|release|vacuum|attach|detach)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private readonly SQLiteConnection connection;
        private bool active = true;

        internal SqliteTransactionSession(SQLiteConnection connection, StorageScope scope)
        {
            this.connection = connection;
            Scope = scope;
        }

        public StorageScope Scope { get; }

        public int Execute(string sql, params object[] parameters)
        {
            EnsureActive();
            ValidateSql(sql);
            return connection.Execute(sql, parameters ?? Array.Empty<object>());
        }

        public T ExecuteScalar<T>(string sql, params object[] parameters)
        {
            EnsureActive();
            ValidateSql(sql);
            return connection.ExecuteScalar<T>(sql, parameters ?? Array.Empty<object>());
        }

        internal void Close() => active = false;

        private void EnsureActive()
        {
            if (!active) throw new ObjectDisposedException(nameof(SqliteTransactionSession));
        }

        private static void ValidateSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL is required.", nameof(sql));
            var withoutTrailingTerminator = sql.TrimEnd();
            if (withoutTrailingTerminator.EndsWith(";", StringComparison.Ordinal))
                withoutTrailingTerminator = withoutTrailingTerminator.Substring(0, withoutTrailingTerminator.Length - 1);
            if (withoutTrailingTerminator.IndexOf(';') >= 0)
                throw new InvalidOperationException("Borrowed sessions accept one SQL statement at a time.");
            if (TransactionControl.IsMatch(sql))
                throw new InvalidOperationException("Transaction control belongs to the outer storage executor.");
        }
    }
}
