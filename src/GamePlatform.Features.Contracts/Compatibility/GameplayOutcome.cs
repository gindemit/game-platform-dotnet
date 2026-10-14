using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts
{
    /// <summary>Consumer-neutral legacy outcome. Wire admission applies its own stricter bounds without mutating this value.</summary>
    public sealed class GameplayOutcome
    {
        public GameplayOutcome(PlatformId session, PlatformId mode, PlatformId content, PlatformId difficulty,
            bool success, long score, long durationTicks, int ticksPerSecond,
            IReadOnlyDictionary<string, long>? metrics, string? validationReference = "")
        {
            CompatibilityGuard.Id(session, nameof(session));
            CompatibilityGuard.Id(mode, nameof(mode));
            CompatibilityGuard.Id(content, nameof(content));
            CompatibilityGuard.Id(difficulty, nameof(difficulty));
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (ticksPerSecond <= 0 || ticksPerSecond > 1000000)
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            Session = session;
            Mode = mode;
            Content = content;
            Difficulty = difficulty;
            Success = success;
            Score = score;
            DurationTicks = durationTicks;
            TicksPerSecond = ticksPerSecond;
            var copy = new Dictionary<string, long>(StringComparer.Ordinal);
            if (metrics != null)
            {
                if (metrics.Count > 1000)
                    throw new ArgumentOutOfRangeException(nameof(metrics));
                foreach (var item in metrics)
                    copy.Add(CompatibilityGuard.Required(item.Key, nameof(metrics), 128), item.Value);
            }
            Metrics = new ReadOnlyDictionary<string, long>(copy);
            ValidationReference = CompatibilityGuard.Optional(validationReference, nameof(validationReference));
        }
        public PlatformId Session { get; }
        public PlatformId Mode { get; }
        public PlatformId Content { get; }
        public PlatformId Difficulty { get; }
        public bool Success { get; }
        public long Score { get; }
        public long DurationTicks { get; }
        public int TicksPerSecond { get; }
        public IReadOnlyDictionary<string, long> Metrics { get; }
        public string ValidationReference { get; }
    }
}
