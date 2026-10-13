using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlatform.Configuration
{
    internal static class ModelCopy
    {
        internal static IReadOnlyList<T> List<T>(IEnumerable<T> values) where T : class
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<T>();
            foreach (var value in values)
            {
                if (value == null) throw new InvalidConfigurationException("null_model");
                if (copy.Count >= ConfigurationLimits.Nodes) throw new InvalidConfigurationException("node_limit");
                copy.Add(value);
            }
            return copy.AsReadOnly();
        }
    }

    public sealed class FeatureRevisionReference
    {
        public string Key { get; }
        public string Revision { get; }
        public string SemanticSha { get; }
        public FeatureRevisionReference(string key, string revision, string semanticSha)
        {
            ConfigurationLimits.String(key, true);
            ConfigurationLimits.String(revision, true);
            ConfigurationLimits.Digest(semanticSha);
            Key = key; Revision = revision; SemanticSha = semanticSha;
        }
    }
    public sealed class ExperimentReference
    {
        public string Key { get; }
        public string Version { get; }
        public ExperimentReference(string key, string version)
        {
            ConfigurationLimits.String(key, true); ConfigurationLimits.String(version, true);
            Key = key; Version = version;
        }
    }
    public sealed class EnrollmentReference
    {
        public string Id { get; }
        public ExperimentReference Experiment { get; }
        public EnrollmentReference(string id, ExperimentReference experiment)
        {
            ConfigurationLimits.String(id, true);
            Id = id; Experiment = experiment ?? throw new ArgumentNullException(nameof(experiment));
        }
    }
    public sealed class InstanceReference
    {
        public string Id { get; }
        public FeatureRevisionReference Feature { get; }
        public InstanceReference(string id, FeatureRevisionReference feature)
        {
            ConfigurationLimits.String(id, true);
            Id = id; Feature = feature ?? throw new ArgumentNullException(nameof(feature));
        }
    }
    public sealed class Choice
    {
        public string Revision { get; }
        public string SemanticSha { get; }
        public string Variant { get; }
        public JsonValue Value { get; }
        public IReadOnlyList<FeatureRevisionReference> Dependencies { get; }
        public Choice(string revision, string semanticSha, string variant, JsonValue value, IEnumerable<FeatureRevisionReference> dependencies)
        {
            ConfigurationLimits.String(revision, true); ConfigurationLimits.Digest(semanticSha); ConfigurationLimits.String(variant, true);
            if (value == null || (value.Kind != JsonKind.String && value.Kind != JsonKind.Boolean && value.Kind != JsonKind.Integer)) throw new InvalidConfigurationException("choice_scalar_required");
            Revision = revision; SemanticSha = semanticSha; Variant = variant; Value = value; Dependencies = ModelCopy.List(dependencies);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in Dependencies) if (!keys.Add(reference.Key)) throw new InvalidConfigurationException("dependency_duplicate");
        }
    }
    public sealed class Condition
    {
        public string Attribute { get; }
        public string Type { get; }
        public string Operator { get; }
        public JsonValue Value { get; }
        public string Source { get; }
        public long MaxAgeSeconds { get; }
        internal StrictVersion? Version { get; }
        public Condition(string attribute, string type, string op, JsonValue value, string source, long maxAgeSeconds)
        {
            ConfigurationLimits.String(attribute, true);
            if (type != "string" && type != "bool" && type != "integer" && type != "version") throw new InvalidConfigurationException("type_unknown");
            if (op != "eq" && op != "ne" && !((type == "integer" || type == "version") && (op == "lt" || op == "lte" || op == "gt" || op == "gte"))) throw new InvalidConfigurationException("operator_unknown");
            if (source != "caller" && source != "backend") throw new InvalidConfigurationException("source_unknown");
            if (maxAgeSeconds < 0 || maxAgeSeconds > ConfigurationLimits.SafeInteger) throw new InvalidConfigurationException("freshness_range");
            if (value == null) throw new InvalidConfigurationException("condition_value_required");
            if ((type == "bool" && value.Kind != JsonKind.Boolean) || (type == "integer" && (value.Kind != JsonKind.Integer || value.AsInteger() < 0)) || ((type == "string" || type == "version") && value.Kind != JsonKind.String)) throw new InvalidConfigurationException("condition_type");
            if (type == "version") Version = StrictVersion.Parse(value.AsString());
            if ((attribute == "verifiedSpendMinor" || attribute == "verifiedPurchaseCount") && (type != "integer" || source != "backend" || maxAgeSeconds == 0)) throw new InvalidConfigurationException("verified_purchase_source");
            Attribute = attribute; Type = type; Operator = op; Value = value; Source = source; MaxAgeSeconds = maxAgeSeconds;
        }
    }
    public sealed class Allocation
    {
        public int Weight { get; }
        public Choice Choice { get; }
        public Allocation(int weight, Choice choice)
        {
            if (weight < 0 || weight > 100) throw new InvalidConfigurationException("weight_range");
            Weight = weight; Choice = choice ?? throw new ArgumentNullException(nameof(choice));
        }
    }
    public sealed class Outcome
    {
        public const string AllocationAlgorithm = "sha1-first7-mod100-v1";
        public Choice? Choice { get; }
        public ExperimentReference? Experiment { get; }
        public IReadOnlyList<Allocation> Allocations { get; }
        private Outcome(Choice? choice, ExperimentReference? experiment, IReadOnlyList<Allocation> allocations) { Choice = choice; Experiment = experiment; Allocations = allocations; }
        public static Outcome Serve(Choice choice) => new Outcome(choice ?? throw new ArgumentNullException(nameof(choice)), null, new List<Allocation>().AsReadOnly());
        public static Outcome Percentage(ExperimentReference experiment, IEnumerable<Allocation> allocations, string algorithm = AllocationAlgorithm)
        {
            if (algorithm != AllocationAlgorithm) throw new InvalidConfigurationException("allocation_algorithm");
            if (experiment == null) throw new ArgumentNullException(nameof(experiment));
            var copy = ModelCopy.List(allocations);
            int sum = 0;
            foreach (var item in copy) sum = checked(sum + item.Weight);
            if (copy.Count == 0 || sum != 100) throw new InvalidConfigurationException("weight_sum");
            return new Outcome(null, experiment, copy);
        }
    }
    public sealed class Rule
    {
        public string Id { get; }
        public IReadOnlyList<Condition> Conditions { get; }
        public Outcome Outcome { get; }
        public Rule(string id, IEnumerable<Condition> conditions, Outcome outcome)
        {
            ConfigurationLimits.String(id, true);
            Id = id; Conditions = ModelCopy.List(conditions); Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        }
    }
    public sealed class FeatureDefinition
    {
        public string Key { get; }
        public bool Enabled { get; }
        public Choice Default { get; }
        public IReadOnlyList<Rule> Rules { get; }
        public FeatureDefinition(string key, bool enabled, Choice @default, IEnumerable<Rule> rules)
        {
            ConfigurationLimits.String(key, true);
            Key = key; Enabled = enabled; Default = @default ?? throw new ArgumentNullException(nameof(@default)); Rules = ModelCopy.List(rules);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in Rules) if (!ids.Add(rule.Id)) throw new InvalidConfigurationException("rule_duplicate");
        }
    }
    public sealed class ConfigurationDefinition
    {
        public const string Schema = "platform.remote-config.v1";
        public string App { get; }
        public string Environment { get; }
        public string Release { get; }
        public IReadOnlyList<FeatureDefinition> Features { get; }
        public ConfigurationDefinition(string app, string environment, string release, IEnumerable<FeatureDefinition> features)
        {
            ConfigurationLimits.String(app, true); ConfigurationLimits.String(environment, true); ConfigurationLimits.String(release, true);
            App = app; Environment = environment; Release = release; Features = ModelCopy.List(features);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var feature in Features)
            {
                if (!keys.Add(feature.Key)) throw new InvalidConfigurationException("feature_duplicate");
                count = checked(count + feature.Rules.Count);
                if (count > ConfigurationLimits.Rules) throw new InvalidConfigurationException("rule_limit");
            }
        }
    }
    public sealed class CompiledConfiguration
    {
        public ConfigurationDefinition Definition { get; }
        public string SemanticSha { get; }
        public string? RepresentationSha { get; }
        internal IReadOnlyDictionary<string, FeatureDefinition> Features { get; }
        internal CompiledConfiguration(ConfigurationDefinition definition, string semanticSha, string? representationSha)
        {
            Definition = definition; SemanticSha = semanticSha; RepresentationSha = representationSha;
            var map = new Dictionary<string, FeatureDefinition>(StringComparer.Ordinal);
            foreach (var feature in definition.Features) map.Add(feature.Key, feature);
            Features = new ReadOnlyDictionary<string, FeatureDefinition>(map);
        }
    }
}
