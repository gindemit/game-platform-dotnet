using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlatform.Storage.Sqlite.Migrations
{
    /// <summary>
    /// Immutable, explicitly supplied consumer-owned schema history. It has no
    /// discovery mechanism and is intentionally separate from platform migrations.
    /// </summary>
    public sealed class SqliteExtensionDescriptor
    {
        public SqliteExtensionDescriptor(string extensionNamespace, int minimumPlatformVersion, IReadOnlyList<SqliteExtensionMigration> migrations)
        {
            if (!IsValidNamespace(extensionNamespace))
                throw new ArgumentOutOfRangeException(nameof(extensionNamespace));
            if (minimumPlatformVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumPlatformVersion));
            if (migrations == null || migrations.Count == 0)
                throw new ArgumentException("At least one extension migration is required.", nameof(migrations));
            var copied = new SqliteExtensionMigration[migrations.Count];
            for (var index = 0; index < migrations.Count; index++)
            {
                var migration = migrations[index];
                if (migration == null || migration.Version != index + 1)
                    throw new ArgumentException("Extension migrations must be complete and ordered from version one.", nameof(migrations));
                copied[index] = migration;
            }
            ExtensionNamespace = extensionNamespace;
            MinimumPlatformVersion = minimumPlatformVersion;
            Migrations = new ReadOnlyCollection<SqliteExtensionMigration>(copied);
        }

        public string ExtensionNamespace { get; }
        public int MinimumPlatformVersion { get; }
        public IReadOnlyList<SqliteExtensionMigration> Migrations { get; }

        internal string GetChecksum(SqliteExtensionMigration migration) => migration.GetChecksum(ExtensionNamespace, MinimumPlatformVersion);

        internal static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.IndexOf('.') <= 0)
                return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '-' && character != '_')
                    return false;
            }
            return true;
        }
    }
}
