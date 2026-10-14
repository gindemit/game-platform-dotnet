using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace GamePlatform.Storage.Sqlite.Migrations
{

    public sealed class SqliteExtensionMigration
    {
        public SqliteExtensionMigration(int version, string id, IReadOnlyList<string> statements)
        {
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (statements == null || statements.Count == 0)
                throw new ArgumentException("At least one statement is required.", nameof(statements));
            var copied = new string[statements.Count];
            for (var index = 0; index < statements.Count; index++)
            {
                var statement = statements[index];
                if (string.IsNullOrWhiteSpace(statement))
                    throw new ArgumentException("Migration statements cannot be empty.", nameof(statements));
                if (ReferencesPlatformTable(statement))
                    throw new ArgumentException("Consumer extension migrations cannot reference gp_* platform tables.", nameof(statements));
                copied[index] = statement;
            }
            Version = version;
            Id = id;
            Statements = new ReadOnlyCollection<string>(copied);
        }

        public int Version { get; }
        public string Id { get; }
        public IReadOnlyList<string> Statements { get; }

        internal string GetChecksum(string extensionNamespace, int minimumPlatformVersion)
        {
            var canonical = new StringBuilder();
            canonical.Append(extensionNamespace).Append('\n').Append(minimumPlatformVersion).Append('\n').Append(Version).Append('\n').Append(Id).Append('\n');
            for (var index = 0; index < Statements.Count; index++)
                canonical.Append(Statements[index].Length).Append(':').Append(Statements[index]).Append('\n');
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var result = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
                result.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }

        // Deliberately conservative review guard, not a SQL security sandbox. SQLite
        // identifiers can be quoted and expressions are too broad to parse safely here.
        private static bool ReferencesPlatformTable(string statement)
        {
            var lower = statement.ToLowerInvariant();
            for (var index = 0; index <= lower.Length - 3; index++)
            {
                if (lower[index] != 'g' || lower[index + 1] != 'p' || lower[index + 2] != '_')
                    continue;
                if (index == 0 || !IsIdentifierCharacter(lower[index - 1]))
                    return true;
            }
            return false;
        }

        private static bool IsIdentifierCharacter(char value) =>
            (value >= 'a' && value <= 'z') || (value >= '0' && value <= '9') || value == '_';
    }
}
