using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace GamePlatform.Storage.Sqlite.Migrations
{
    public sealed class SqliteMigration
    {
        public SqliteMigration(int version, string id, IReadOnlyList<string> statements)
        {
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentOutOfRangeException(nameof(id));
            if (statements == null || statements.Count == 0) throw new ArgumentException("At least one statement is required.", nameof(statements));
            var copied = new string[statements.Count];
            for (var index = 0; index < statements.Count; index++)
            {
                var statement = statements[index];
                if (string.IsNullOrWhiteSpace(statement)) throw new ArgumentException("Migration statements cannot be empty.", nameof(statements));
                copied[index] = statement;
            }
            Version = version;
            Id = id;
            Statements = new ReadOnlyCollection<string>(copied);
            Checksum = ComputeChecksum(version, id, copied);
        }

        public int Version { get; }
        public string Id { get; }
        public IReadOnlyList<string> Statements { get; }
        public string Checksum { get; }

        private static string ComputeChecksum(int version, string id, IReadOnlyList<string> statements)
        {
            var canonical = new StringBuilder();
            canonical.Append(version).Append('\n').Append(id).Append('\n');
            for (var index = 0; index < statements.Count; index++)
                canonical.Append(statements[index].Length).Append(':').Append(statements[index]).Append('\n');
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var result = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) result.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    internal enum MigrationCheckpoint
    {
        EffectsApplied,
        MarkerInserted
    }
}
