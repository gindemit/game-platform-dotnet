using System.Collections.Generic;
using System.Linq;
namespace GamePlatform.Navigation.Kernel
{
    public sealed class NavigationSnapshot
    {
        internal NavigationSnapshot(long version, string scopeId, long generation,
            IEnumerable<NavigationEntrySnapshot> entries, IEnumerable<NavigationEntrySnapshot> modals,
            IEnumerable<NavigationEntrySnapshot> pending)
        {
            Version = version;
            ScopeId = scopeId;
            Generation = generation;
            Entries = new List<NavigationEntrySnapshot>(entries).AsReadOnly();
            Modals = new List<NavigationEntrySnapshot>(modals).AsReadOnly();
            Pending = new List<NavigationEntrySnapshot>(pending).AsReadOnly();
        }
        public long Version { get; }
        public string ScopeId { get; }
        public long Generation { get; }
        public IReadOnlyList<NavigationEntrySnapshot> Entries { get; }
        public IReadOnlyList<NavigationEntrySnapshot> Modals { get; }
        public IReadOnlyList<NavigationEntrySnapshot> Pending { get; }
        public NavigationEntrySnapshot? Current => Entries.LastOrDefault();
        public NavigationEntrySnapshot? Top => Modals.LastOrDefault() ?? Current;
    }
}
