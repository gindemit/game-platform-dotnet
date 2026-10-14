using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace GamePlatform.Configuration
{
    public sealed class ContextAttribute
    {
        public JsonValue Value { get; }
        public string Source { get; }
        public long ObservedAtSeconds { get; }
        public bool Verified { get; }
        public ContextAttribute(JsonValue value, string source, long observedAtSeconds, bool verified)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            // Malformed context remains unknown; it never becomes false/zero.
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ObservedAtSeconds = observedAtSeconds;
            Verified = verified;
        }
    }
    public sealed class EvaluationContext
    {
        public string SubjectId { get; }
        public long NowSeconds { get; }
        public IReadOnlyDictionary<string, ContextAttribute> Attributes { get; }
        public EvaluationContext(string subjectId, long nowSeconds, IReadOnlyDictionary<string, ContextAttribute> attributes)
        {
            ConfigurationLimits.String(subjectId);
            if (nowSeconds < 0 || nowSeconds > ConfigurationLimits.SafeInteger)
                throw new InvalidConfigurationException("clock_range");
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            var copy = new Dictionary<string, ContextAttribute>(StringComparer.Ordinal);
            foreach (var pair in attributes)
            {
                ConfigurationLimits.String(pair.Key, true);
                if (pair.Value == null)
                    throw new InvalidConfigurationException("context_null");
                if (copy.Count >= ConfigurationLimits.Nodes)
                    throw new InvalidConfigurationException("node_limit");
                copy.Add(pair.Key, pair.Value);
            }
            SubjectId = subjectId;
            NowSeconds = nowSeconds;
            Attributes = new ReadOnlyDictionary<string, ContextAttribute>(copy);
        }
    }
    public sealed class Selection
    {
        public Choice? Choice { get; }
        public string? RuleId { get; }
        public int? Bucket { get; }
        public string Reason { get; }
        public string? Variant => Choice?.Variant;
        public ExperimentReference? Experiment { get; }
        internal Selection(Choice? choice, string? ruleId, int? bucket, string reason, ExperimentReference? experiment = null)
        {
            Choice = choice;
            RuleId = ruleId;
            Bucket = bucket;
            Reason = reason;
            Experiment = experiment;
        }
    }
    public static class Evaluator
    {
        public static int Bucket(string experimentKey, string subjectId)
        {
            ConfigurationLimits.String(experimentKey, true);
            ConfigurationLimits.String(subjectId);
            byte[] bytes = ConfigurationLimits.Utf8.GetBytes(experimentKey + subjectId);
            byte[] hash;
            using (var sha = SHA1.Create())
                hash = sha.ComputeHash(bytes);
            uint firstSeven = ((uint)hash[0] << 20) | ((uint)hash[1] << 12) | ((uint)hash[2] << 4) | ((uint)hash[3] >> 4);
            return (int)(firstSeven % 100);
        }
        public static Selection Evaluate(CompiledConfiguration configuration, string featureKey, EvaluationContext context)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            ConfigurationLimits.String(featureKey, true);
            if (!configuration.Features.TryGetValue(featureKey, out var feature))
                return new Selection(null, null, null, "missing_feature");
            if (!feature.Enabled)
                return new Selection(feature.Default, null, null, "disabled");
            foreach (var rule in feature.Rules)
            {
                bool matches = true;
                foreach (var condition in rule.Conditions)
                    if (!Match(condition, context))
                    {
                        matches = false;
                        break;
                    }
                if (!matches)
                    continue;
                if (rule.Outcome.Choice != null)
                    return new Selection(rule.Outcome.Choice, rule.Id, null, "rule");
                int bucket = Bucket(rule.Outcome.Experiment!.Key, context.SubjectId);
                int upper = 0;
                foreach (var allocation in rule.Outcome.Allocations)
                {
                    upper += allocation.Weight;
                    if (bucket < upper)
                        return new Selection(allocation.Choice, rule.Id, bucket, "percentage", rule.Outcome.Experiment);
                }
                throw new InvalidConfigurationException("compiled_weight_invariant");
            }
            return new Selection(feature.Default, null, null, "default");
        }
        private static bool Match(Condition condition, EvaluationContext context)
        {
            if (!context.Attributes.TryGetValue(condition.Attribute, out var attribute) || attribute.Value.Kind == JsonKind.Null || attribute.Source != condition.Source)
                return false;
            if (condition.Source == "backend" && !attribute.Verified)
                return false;
            if (attribute.ObservedAtSeconds < 0 || attribute.ObservedAtSeconds > ConfigurationLimits.SafeInteger || attribute.ObservedAtSeconds > context.NowSeconds)
                return false;
            if (condition.MaxAgeSeconds > 0 && context.NowSeconds - attribute.ObservedAtSeconds > condition.MaxAgeSeconds)
                return false;
            int comparison;
            switch (condition.Type)
            {
                case "string":
                    if (attribute.Value.Kind != JsonKind.String)
                        return false;
                    comparison = string.CompareOrdinal(attribute.Value.AsString(), condition.Value.AsString());
                    break;
                case "bool":
                    if (attribute.Value.Kind != JsonKind.Boolean)
                        return false;
                    comparison = attribute.Value.AsBoolean().CompareTo(condition.Value.AsBoolean());
                    break;
                case "integer":
                    if (attribute.Value.Kind != JsonKind.Integer || attribute.Value.AsInteger() < 0)
                        return false;
                    comparison = attribute.Value.AsInteger().CompareTo(condition.Value.AsInteger());
                    break;
                case "version":
                    if (attribute.Value.Kind != JsonKind.String || !StrictVersion.TryParse(attribute.Value.AsString(), out var version))
                        return false;
                    comparison = version!.CompareTo(condition.Version);
                    break;
                default:
                    return false;
            }
            switch (condition.Operator)
            {
                case "eq":
                    return comparison == 0;
                case "ne":
                    return comparison != 0;
                case "lt":
                    return comparison < 0;
                case "lte":
                    return comparison <= 0;
                case "gt":
                    return comparison > 0;
                case "gte":
                    return comparison >= 0;
                default:
                    return false;
            }
        }
    }
}
