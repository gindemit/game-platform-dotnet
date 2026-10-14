using System;
using System.Collections.Generic;

namespace GamePlatform.Configuration
{
    public sealed class ActivationResult
    {
        public CompiledConfiguration? Active
        {
            get;
        }
        public bool Accepted
        {
            get;
        }
        public string Reason
        {
            get;
        }

        internal ActivationResult(CompiledConfiguration? active, bool accepted, string reason)
        {
            Active = active;
            Accepted = accepted;
            Reason = reason;
        }
    }

    public static class ActivationPolicy
    {
        public static ActivationResult Activate(
            CompiledConfiguration? lastKnownGood,
            CompiledConfiguration? completeCandidate,
            long requestGeneration,
            long currentGeneration,
            IReadOnlyList<FeatureRevisionReference>? verifiedDependencies = null)
        {
            if (requestGeneration < 0 || currentGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(requestGeneration));
            if (requestGeneration != currentGeneration)
                return new ActivationResult(lastKnownGood, false, "stale_generation");
            if (completeCandidate == null)
                return new ActivationResult(lastKnownGood, false, "candidate_rejected");
            if (lastKnownGood != null &&
                (lastKnownGood.Definition.App != completeCandidate.Definition.App ||
                 lastKnownGood.Definition.Environment != completeCandidate.Definition.Environment))
                return new ActivationResult(lastKnownGood, false, "scope_mismatch");
            foreach (var feature in completeCandidate.Definition.Features)
            {
                if (!DependenciesVerified(feature.Default, verifiedDependencies))
                    return new ActivationResult(lastKnownGood, false, "dependency_missing");
                foreach (var rule in feature.Rules)
                {
                    if (rule.Outcome.Choice != null &&
                        !DependenciesVerified(rule.Outcome.Choice, verifiedDependencies))
                        return new ActivationResult(lastKnownGood, false, "dependency_missing");
                    foreach (var allocation in rule.Outcome.Allocations)
                        if (!DependenciesVerified(allocation.Choice, verifiedDependencies))
                            return new ActivationResult(lastKnownGood, false, "dependency_missing");
                }
            }
            return new ActivationResult(completeCandidate, true, "accepted");
        }

        public static ActivationResult ReadAndActivate(
            CompiledConfiguration? lastKnownGood,
            byte[] candidate,
            string expectedApp,
            string expectedEnvironment,
            long requestGeneration,
            long currentGeneration,
            IReadOnlyList<FeatureRevisionReference>? verifiedDependencies = null)
        {
            if (requestGeneration != currentGeneration)
                return Activate(lastKnownGood, null, requestGeneration, currentGeneration);
            try
            {
                return Activate(
                    lastKnownGood,
                    ConfigurationReader.Read(candidate, expectedApp, expectedEnvironment),
                    requestGeneration,
                    currentGeneration,
                    verifiedDependencies);
            }
            catch (InvalidConfigurationException)
            {
                return Activate(lastKnownGood, null, requestGeneration, currentGeneration);
            }
        }

        public static FeatureRevisionReference ForInstance(
            InstanceReference? pinned,
            FeatureRevisionReference selectedForNewInstance)
        {
            if (selectedForNewInstance == null)
                throw new ArgumentNullException(nameof(selectedForNewInstance));
            if (pinned != null && pinned.Feature.Key != selectedForNewInstance.Key)
                throw new InvalidConfigurationException("pinned_feature_mismatch");
            return pinned?.Feature ?? selectedForNewInstance;
        }

        private static bool DependenciesVerified(
            Choice choice,
            IReadOnlyList<FeatureRevisionReference>? verified)
        {
            foreach (var dependency in choice.Dependencies)
            {
                bool found = false;
                if (verified != null)
                    foreach (var evidence in verified)
                        if (evidence != null &&
                            evidence.Key == dependency.Key &&
                            evidence.Revision == dependency.Revision &&
                            evidence.SemanticSha == dependency.SemanticSha)
                        {
                            found = true;
                            break;
                        }
                if (!found)
                    return false;
            }
            return true;
        }
    }
}
