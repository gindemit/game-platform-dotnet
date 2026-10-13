#if !RC_TARGET_BASELINE
using System;
using System.Collections.Generic;
using System.Text;

namespace GamePlatform.Configuration
{
    // Integration-owned AOT harness, deliberately excluded from the runtime DLL.
    public static class ConfigurationProbe
    {
        private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public static string RunTypedSmoke()
        {
            var a = new Choice("policy-a", Digest, "A", JsonValue.String("A"), Array.Empty<FeatureRevisionReference>());
            var b = new Choice("policy-b", Digest, "B", JsonValue.String("B"), Array.Empty<FeatureRevisionReference>());
            var rule = new Rule("rollout", new[] { new Condition("version", "version", "gte", JsonValue.String("1.2.0"), "caller", 0) }, Outcome.Percentage(new ExperimentReference("flag", "1"), new[] { new Allocation(50, a), new Allocation(50, b) }));
            var definition = new ConfigurationDefinition("synthetic", "local", "typed-smoke", new[] { new FeatureDefinition("flag", true, a, new[] { rule }) });
            var compiled = ConfigurationCompiler.Compile(definition, "synthetic", "local");
            var attributes = new Dictionary<string, ContextAttribute> { ["version"] = new ContextAttribute(JsonValue.String("1.2.0"), "caller", 100, false) };
            string? first = null;
            string? second = null;
            for (int i = 0; i < 1000 && (first == null || second == null); i++)
            {
                string value = Evaluator.Evaluate(compiled, "flag", new EvaluationContext("id" + i, 100, attributes)).Choice!.Value.AsString();
                if (value == "A") first = value;
                else second = value;
            }
            if (first == null || second == null) throw new InvalidConfigurationException("smoke_allocation");
            return first + ":" + second;
        }
#if !RC_TARGET_TYPED
        public static string RunSmoke()
        {
            // Full path exercises reader, escaped Unicode and compiled evaluation.
            string json = "{\"schema\":\"platform.remote-config.v1\",\"app\":\"synthetic\",\"environment\":\"local\",\"release\":\"smoke\",\"features\":[{\"key\":\"flag\",\"enabled\":true,\"default\":{\"revision\":\"a\",\"semanticSha\":\"" + Digest + "\",\"variant\":\"A\",\"value\":\"A\",\"dependencies\":[]},\"rules\":[{\"id\":\"b\",\"conditions\":[],\"outcome\":{\"kind\":\"serve\",\"choice\":{\"revision\":\"b\",\"semanticSha\":\"" + Digest + "\",\"variant\":\"B\",\"value\":\"B\",\"dependencies\":[]}}}]}]}";
            var compiled = ConfigurationReader.Read(Encoding.UTF8.GetBytes(json), "synthetic", "local");
            var selection = Evaluator.Evaluate(compiled, "flag", new EvaluationContext("🙂", 100, new Dictionary<string, ContextAttribute>()));
            if (selection.Choice!.Value.AsString() != "B") throw new InvalidConfigurationException("smoke_reader");
            return "A:B";
        }
        public static string RunCorpus(byte[] corpus)
        {
            var root = CanonicalJson.Parse(corpus);
            int passed = 0;
            foreach (var vector in root["canonical"].AsArray())
            {
                Require(Encoding.UTF8.GetString(CanonicalJson.Serialize(vector["value"])) == vector["canonical"].AsString(), "canonical_bytes");
                Require(CanonicalJson.SemanticSha256(vector["value"]) == vector["semanticSha"].AsString(), "canonical_hash");
                passed++;
            }
            foreach (var vector in root["allocation"].AsArray())
            {
                Require(Evaluator.Bucket(vector["key"].AsString(), vector["subject"].AsString()) == vector["bucket"].AsInteger(), "bucket"); passed++;
            }
            foreach (var vector in root["versions"].AsArray())
            {
                Require(Math.Sign(StrictVersion.Parse(vector["left"].AsString()).CompareTo(StrictVersion.Parse(vector["right"].AsString()))) == vector["compare"].AsInteger(), "version"); passed++;
            }
            foreach (var vector in root["invalidVersions"].AsArray())
            {
                Require(!StrictVersion.TryParse(vector.AsString(), out _), "version_invalid"); passed++;
            }
            foreach (var vector in root["negativeJson"].AsArray())
            {
                bool rejected = false;
                try { CanonicalJson.Parse(Encoding.UTF8.GetBytes(vector.AsString())); }
                catch (InvalidConfigurationException) { rejected = true; }
                Require(rejected, "json_invalid"); passed++;
            }
            var compiled = ConfigurationReader.Read(CanonicalJson.Serialize(root["selector"]), "synthetic", "local");
            foreach (var vector in root["evaluations"].AsArray())
            {
                var result = Evaluator.Evaluate(compiled, vector["feature"].AsString(), Context(vector));
                var expected = vector["expected"];
                Require(Equal(result.Choice!.Value, expected["value"]) && result.Choice.Revision == expected["revision"].AsString() && result.Variant == expected["variant"].AsString() && result.Reason == expected["reason"].AsString(), "selection");
                Require((result.RuleId == null ? JsonValue.Null : JsonValue.String(result.RuleId)).Kind == expected["ruleId"].Kind && (result.RuleId == null || result.RuleId == expected["ruleId"].AsString()), "rule");
                Require(result.Bucket.HasValue ? expected["bucket"].Kind == JsonKind.Integer && result.Bucket.Value == expected["bucket"].AsInteger() : expected["bucket"].Kind == JsonKind.Null, "bucket_result");
                passed++;
            }
            foreach (var vector in root["researchSubset"].AsArray())
            {
                var config = ConfigurationReader.Read(CanonicalJson.Serialize(vector["selector"]), "synthetic", "local");
                Require(Evaluator.Evaluate(config, vector["feature"].AsString(), Context(vector)).Choice!.Value.AsString() == vector["expectedValue"].AsString(), "research_subset"); passed++;
            }
            return "passed:" + passed;
        }
        private static EvaluationContext Context(JsonValue vector)
        {
            var attributes = new Dictionary<string, ContextAttribute>();
            foreach (var pair in vector["attributes"].AsObject())
                attributes.Add(pair.Key, new ContextAttribute(pair.Value["value"], pair.Value["source"].AsString(), pair.Value["observedAtSeconds"].AsInteger(), pair.Value["verified"].AsBoolean()));
            return new EvaluationContext(vector["subject"].AsString(), vector["nowSeconds"].AsInteger(), attributes);
        }
        private static bool Equal(JsonValue left, JsonValue right) => Encoding.UTF8.GetString(CanonicalJson.Serialize(left)) == Encoding.UTF8.GetString(CanonicalJson.Serialize(right));
        private static void Require(bool value, string code) { if (!value) throw new InvalidConfigurationException("corpus:" + code); }
#endif
    }
}
#endif
