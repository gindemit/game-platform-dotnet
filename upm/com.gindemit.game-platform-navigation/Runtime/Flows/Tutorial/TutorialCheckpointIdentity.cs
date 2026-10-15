using System;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>Stable tutorial identity. ScopeKey is an owner-defined opaque account or event scope.</summary>
    public sealed class TutorialCheckpointIdentity : IEquatable<TutorialCheckpointIdentity>
    {
        public TutorialCheckpointIdentity(string tutorialKey, string version, TutorialScopeKind scopeKind, string scopeKey)
        {
            TutorialKey = Required(tutorialKey, nameof(tutorialKey));
            Version = Required(version, nameof(version));
            if (!Enum.IsDefined(typeof(TutorialScopeKind), scopeKind))
                throw new ArgumentOutOfRangeException(nameof(scopeKind));
            ScopeKind = scopeKind;
            ScopeKey = Required(scopeKey, nameof(scopeKey));
        }

        public string TutorialKey { get; }
        public string Version { get; }
        public TutorialScopeKind ScopeKind { get; }
        public string ScopeKey { get; }

        public bool Equals(TutorialCheckpointIdentity? other) => other != null &&
            ScopeKind == other.ScopeKind && string.Equals(TutorialKey, other.TutorialKey, StringComparison.Ordinal) &&
            string.Equals(Version, other.Version, StringComparison.Ordinal) && string.Equals(ScopeKey, other.ScopeKey, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as TutorialCheckpointIdentity);
        public override int GetHashCode() => HashCode.Combine(TutorialKey, Version, (int)ScopeKind, ScopeKey);

        private static string Required(string value, string parameter) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value;
    }

    /// <summary>The owner's durable tutorial boundary.</summary>
    public enum TutorialScopeKind { Account, Event }
}
