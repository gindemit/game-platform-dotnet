using System;
using System.Collections.Generic;

namespace GamePlatform.Configuration
{
    public static class ConfigurationReader
    {
        public static CompiledConfiguration Read(byte[] json, string expectedApp, string expectedEnvironment)
        {
            JsonValue root = CanonicalJson.Parse(json);
            ConfigurationDefinition definition = Definition(root);
            return ConfigurationCompiler.Compile(definition, expectedApp, expectedEnvironment, CanonicalJson.Sha256(json));
        }
        public static ConfigurationDefinition Definition(JsonValue root)
        {
            Fields(root, "schema", "app", "environment", "release", "features");
            if (root["schema"].AsString() != ConfigurationDefinition.Schema) throw new InvalidConfigurationException("schema_unsupported");
            var features = new List<FeatureDefinition>();
            foreach (var feature in root["features"].AsArray())
            {
                Fields(feature, "key", "enabled", "default", "rules");
                var rules = new List<Rule>();
                foreach (var rule in feature["rules"].AsArray())
                {
                    Fields(rule, "id", "conditions", "outcome");
                    var conditions = new List<Condition>();
                    foreach (var condition in rule["conditions"].AsArray())
                    {
                        Fields(condition, "attribute", "type", "op", "value", "source", "maxAgeSeconds");
                        conditions.Add(new Condition(condition["attribute"].AsString(), condition["type"].AsString(), condition["op"].AsString(), condition["value"], condition["source"].AsString(), condition["maxAgeSeconds"].AsInteger()));
                    }
                    rules.Add(new Rule(rule["id"].AsString(), conditions, OutcomeValue(rule["outcome"])));
                }
                features.Add(new FeatureDefinition(feature["key"].AsString(), feature["enabled"].AsBoolean(), ChoiceValue(feature["default"]), rules));
            }
            return new ConfigurationDefinition(root["app"].AsString(), root["environment"].AsString(), root["release"].AsString(), features);
        }
        private static void Fields(JsonValue value, params string[] names)
        {
            var map = value.AsObject();
            if (map.Count != names.Length) throw new InvalidConfigurationException("field_set");
            foreach (string name in names) if (!map.ContainsKey(name)) throw new InvalidConfigurationException("field_missing:" + name);
        }
        private static Choice ChoiceValue(JsonValue value)
        {
            Fields(value, "revision", "semanticSha", "variant", "value", "dependencies");
            var dependencies = new List<FeatureRevisionReference>();
            foreach (var dependency in value["dependencies"].AsArray())
            {
                Fields(dependency, "key", "revision", "semanticSha");
                dependencies.Add(new FeatureRevisionReference(dependency["key"].AsString(), dependency["revision"].AsString(), dependency["semanticSha"].AsString()));
            }
            return new Choice(value["revision"].AsString(), value["semanticSha"].AsString(), value["variant"].AsString(), value["value"], dependencies);
        }
        private static Outcome OutcomeValue(JsonValue value)
        {
            string kind = value["kind"].AsString();
            if (kind == "serve")
            {
                Fields(value, "kind", "choice");
                return Outcome.Serve(ChoiceValue(value["choice"]));
            }
            if (kind != "percentage") throw new InvalidConfigurationException("outcome_unknown");
            Fields(value, "kind", "experimentKey", "experimentVersion", "allocationAlgorithm", "allocations");
            var allocations = new List<Allocation>();
            foreach (var allocation in value["allocations"].AsArray())
            {
                Fields(allocation, "weight", "choice");
                long weight = allocation["weight"].AsInteger();
                if (weight < 0 || weight > 100) throw new InvalidConfigurationException("weight_range");
                allocations.Add(new Allocation((int)weight, ChoiceValue(allocation["choice"])));
            }
            return Outcome.Percentage(new ExperimentReference(value["experimentKey"].AsString(), value["experimentVersion"].AsString()), allocations, value["allocationAlgorithm"].AsString());
        }
    }

    public static class ConfigurationCompiler
    {
        public static CompiledConfiguration Compile(ConfigurationDefinition definition, string expectedApp, string expectedEnvironment)
            => Compile(definition, expectedApp, expectedEnvironment, null);
        internal static CompiledConfiguration Compile(ConfigurationDefinition definition, string expectedApp, string expectedEnvironment, string? representationSha)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ConfigurationLimits.String(expectedApp, true); ConfigurationLimits.String(expectedEnvironment, true);
            if (definition.App != expectedApp || definition.Environment != expectedEnvironment) throw new InvalidConfigurationException("scope_mismatch");
            // Typed constructors validate semantics; canonical traversal applies the same
            // total size/depth/node limits to typed and decoded definitions.
            byte[] bytes = CanonicalJson.Serialize(ToJson(definition));
            return new CompiledConfiguration(definition, CanonicalJson.Sha256(bytes), representationSha);
        }
        public static JsonValue ToJson(ConfigurationDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var features = new List<JsonValue>();
            foreach (var feature in definition.Features)
            {
                var rules = new List<JsonValue>();
                foreach (var rule in feature.Rules)
                {
                    var conditions = new List<JsonValue>();
                    foreach (var condition in rule.Conditions)
                        conditions.Add(Object("attribute", Text(condition.Attribute), "type", Text(condition.Type), "op", Text(condition.Operator), "value", condition.Value, "source", Text(condition.Source), "maxAgeSeconds", JsonValue.Integer(condition.MaxAgeSeconds)));
                    rules.Add(Object("id", Text(rule.Id), "conditions", JsonValue.Array(conditions), "outcome", OutcomeJson(rule.Outcome)));
                }
                features.Add(Object("key", Text(feature.Key), "enabled", JsonValue.Boolean(feature.Enabled), "default", ChoiceJson(feature.Default), "rules", JsonValue.Array(rules)));
            }
            return Object("schema", Text(ConfigurationDefinition.Schema), "app", Text(definition.App), "environment", Text(definition.Environment), "release", Text(definition.Release), "features", JsonValue.Array(features));
        }
        private static JsonValue ChoiceJson(Choice choice)
        {
            var dependencies = new List<JsonValue>();
            foreach (var dependency in choice.Dependencies) dependencies.Add(Object("key", Text(dependency.Key), "revision", Text(dependency.Revision), "semanticSha", Text(dependency.SemanticSha)));
            return Object("revision", Text(choice.Revision), "semanticSha", Text(choice.SemanticSha), "variant", Text(choice.Variant), "value", choice.Value, "dependencies", JsonValue.Array(dependencies));
        }
        private static JsonValue OutcomeJson(Outcome outcome)
        {
            if (outcome.Choice != null) return Object("kind", Text("serve"), "choice", ChoiceJson(outcome.Choice));
            var allocations = new List<JsonValue>();
            foreach (var allocation in outcome.Allocations) allocations.Add(Object("weight", JsonValue.Integer(allocation.Weight), "choice", ChoiceJson(allocation.Choice)));
            return Object("kind", Text("percentage"), "experimentKey", Text(outcome.Experiment!.Key), "experimentVersion", Text(outcome.Experiment.Version), "allocationAlgorithm", Text(Outcome.AllocationAlgorithm), "allocations", JsonValue.Array(allocations));
        }
        private static JsonValue Text(string value) => JsonValue.String(value);
        private static JsonValue Object(params object[] fields)
        {
            var pairs = new List<KeyValuePair<string, JsonValue>>();
            for (int i = 0; i < fields.Length; i += 2) pairs.Add(new KeyValuePair<string, JsonValue>((string)fields[i], (JsonValue)fields[i + 1]));
            return JsonValue.Object(pairs);
        }
    }
}
