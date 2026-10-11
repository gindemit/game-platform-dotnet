#nullable enable
using System;
using System.Threading;
using System.Text.RegularExpressions;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Features.Accounts
{
    /// <summary>
    /// Synchronous borrowed directory transaction for consumer-owned pb_ tables only.
    /// Never change gp_ schema/data. The directory owns commit, rollback and connection lifetime.
    /// Do not retain the transaction or invoke asynchronous/reentrant directory operations.
    /// </summary>
    public sealed class SqliteAccountsDirectoryExtensionTransaction
    {
        private SQLiteConnection? connection;
        private static readonly AsyncLocal<int> borrowedDepth = new AsyncLocal<int>();
        internal static bool IsBorrowedCallback => borrowedDepth.Value != 0;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        internal SqliteAccountsDirectoryExtensionTransaction(SQLiteConnection connection) { this.connection = connection; borrowedDepth.Value++; }
        internal void Invalidate() { connection = null; borrowedDepth.Value--; }
        public int Execute(string sql, params object[] args) => Require(sql, false, args).Execute(sql, args);
        public T ExecuteScalar<T>(string sql, params object[] args) => Require(sql, true, args).ExecuteScalar<T>(sql, args);
        private SQLiteConnection Require(string sql, bool scalar, object[] args)
        {
            if (connection == null || Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("The borrowed directory transaction is no longer available on this callback thread.");
            if (string.IsNullOrWhiteSpace(sql) || sql.Length > 16384 || args == null || args.Length > 64 || sql.IndexOf(';') >= 0 ||
                sql.Contains("--") || sql.Contains("/*") || sql.IndexOf("gp_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sql.IndexOf("pragma", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new ArgumentException("One extension SQL statement is required.", nameof(sql));
            var first = sql.TrimStart().Split(new[] { ' ', '\t', '\r', '\n' }, 2)[0].ToUpperInvariant();
            if (scalar ? first != "SELECT" : first != "SELECT" && first != "CREATE" && first != "INSERT" && first != "UPDATE" && first != "DELETE" && first != "ALTER" && first != "DROP")
                throw new ArgumentException("Extension SQL cannot control the directory transaction or connection.", nameof(sql));
            if (first != "SELECT" && !Regex.IsMatch(sql, @"^\s*(?:CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?|INSERT\s+INTO\s+|UPDATE\s+|DELETE\s+FROM\s+|ALTER\s+TABLE\s+|DROP\s+TABLE\s+)pb_[a-z0-9_]+\b", RegexOptions.IgnoreCase))
                throw new ArgumentException("Only consumer pb_ table writes are allowed.", nameof(sql));
            foreach (Match table in Regex.Matches(sql, @"\b(?:FROM|JOIN|REFERENCES)\s+([^\s(),]+)", RegexOptions.IgnoreCase))
            {
                var name = table.Groups[1].Value;
                if (!Regex.IsMatch(name, @"^pb_[a-z0-9_]+$", RegexOptions.IgnoreCase) && !(first == "SELECT" && name.Equals("sqlite_master", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Only consumer pb_ tables and read-only schema inspection are allowed.", nameof(sql));
            }
            return connection;
        }
    }
}
