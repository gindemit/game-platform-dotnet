using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Snapshots
{
    public enum SnapshotFreshness
    {
        Missing = 0,
        Stale = 1,
        Current = 2
    }

    public enum FeatureSnapshotState
    {
        Missing = 0,
        Available = 1,
        Stale = 2,
        Pending = 3,
        Unavailable = 4,
        Error = 5
    }

    /// <summary>An immutable observation captured for exactly one owner/view generation.</summary>
    public sealed class FeatureSnapshot<T> where T : class
    {
        public FeatureSnapshot(ScopedOwnerContext owner, long revision, SnapshotFreshness freshness, T? value)
            : this(owner, revision, freshness == SnapshotFreshness.Missing ? FeatureSnapshotState.Missing :
                freshness == SnapshotFreshness.Stale ? FeatureSnapshotState.Stale : FeatureSnapshotState.Available,
                freshness, value, null, null)
        {
        }

        public FeatureSnapshot(
            ScopedOwnerContext owner,
            long revision,
            FeatureSnapshotState state,
            SnapshotFreshness freshness,
            T? value,
            long? lastConfirmedAtMilliseconds,
            string? diagnosticCode)
        {
            if (!owner.IsValid) throw new ArgumentException("A valid captured owner is required.", nameof(owner));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(FeatureSnapshotState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (!Enum.IsDefined(typeof(SnapshotFreshness), freshness)) throw new ArgumentOutOfRangeException(nameof(freshness));
            if (lastConfirmedAtMilliseconds.HasValue && (lastConfirmedAtMilliseconds.Value < 0 || lastConfirmedAtMilliseconds.Value > 253_402_300_799_999L))
                throw new ArgumentOutOfRangeException(nameof(lastConfirmedAtMilliseconds));
            if (diagnosticCode != null && (diagnosticCode.Length == 0 || diagnosticCode.Length > 128 || diagnosticCode.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0))
                throw new ArgumentOutOfRangeException(nameof(diagnosticCode));
            if (state == FeatureSnapshotState.Missing && (value != null || freshness != SnapshotFreshness.Missing || revision != 0))
                throw new ArgumentException("Missing snapshots have no value or confirmed revision.");
            if ((state == FeatureSnapshotState.Available || state == FeatureSnapshotState.Stale) && value == null)
                throw new ArgumentException("Available and stale snapshots require a value.", nameof(value));
            if (state == FeatureSnapshotState.Available && freshness != SnapshotFreshness.Current)
                throw new ArgumentException("Available snapshots are current.", nameof(freshness));
            if (state == FeatureSnapshotState.Stale && freshness != SnapshotFreshness.Stale)
                throw new ArgumentException("Stale snapshots declare stale freshness.", nameof(freshness));
            if (value == null && freshness == SnapshotFreshness.Current)
                throw new ArgumentException("Current snapshots require a value.", nameof(value));
            if ((state == FeatureSnapshotState.Error || state == FeatureSnapshotState.Unavailable) && diagnosticCode == null)
                throw new ArgumentException("Unavailable and error snapshots require a diagnostic code.", nameof(diagnosticCode));
            if (state != FeatureSnapshotState.Error && state != FeatureSnapshotState.Unavailable && diagnosticCode != null)
                throw new ArgumentException("Only unavailable or error snapshots carry a diagnostic code.", nameof(diagnosticCode));

            Owner = owner;
            Revision = revision;
            State = state;
            Freshness = freshness;
            Value = value;
            LastConfirmedAtMilliseconds = lastConfirmedAtMilliseconds;
            DiagnosticCode = diagnosticCode;
        }

        public ScopedOwnerContext Owner { get; }
        public long Revision { get; }
        public FeatureSnapshotState State { get; }
        public SnapshotFreshness Freshness { get; }
        public T? Value { get; }
        public long? LastConfirmedAtMilliseconds { get; }
        public string? DiagnosticCode { get; }
    }

    public interface IFeatureSnapshotReader<in TQuery, TSnapshot> where TSnapshot : class
    {
        FeatureSnapshot<TSnapshot> Read(ScopedOwnerContext owner, TQuery query);
    }
}
