using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Features.Accounts;

namespace GamePlatform.Storage.Sqlite.Migrations
{
    /// <summary>
    /// The ordered, immutable schema history for a scope-owned platform database.
    /// Hosts must open existing and new databases with this complete registry.
    /// </summary>
    public static class SqlitePlatformMigrationRegistry
    {
        private static readonly IReadOnlyList<SqliteMigration> migrations =
            new ReadOnlyCollection<SqliteMigration>(new[]
            {
                SqliteOutboxMigration.Create(1),
                SqlitePrivateSyncMigration.Create(2),
                SqliteFeatureStateMigration.Create(3),
                SqliteAccountsDirectoryMigration.Create(4),
                SqliteExtensionMigrationJournal.Create(5),
                SqliteEnvelopeAndZeroRevisionMigration.Create(6),
                SqliteConfirmedProjectionInvalidationMigration.Create(7)
            });

        public static IReadOnlyList<SqliteMigration> Migrations => migrations;
    }
}
