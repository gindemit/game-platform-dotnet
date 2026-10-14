using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GamePlatform.Configuration;
using Xunit;

namespace GamePlatform.Configuration.Tests
{
    public sealed class ConfigurationTests
    {
        private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
        private static string CorpusFile(string name) => Path.Combine(AppContext.BaseDirectory, "corpus", name);
        private static Choice Choice(string value) => new Choice(value, Digest, value, JsonValue.String(value), Array.Empty<FeatureRevisionReference>());
        private static CompiledConfiguration Config(IEnumerable<Condition> conditions, bool enabled = true)
            => ConfigurationCompiler.Compile(new ConfigurationDefinition("test", "local", "1", new[] { new FeatureDefinition("flag", enabled, Choice("default"), new[] { new Rule("first", conditions, Outcome.Serve(Choice("A"))) }) }), "test", "local");
        private static EvaluationContext Context(JsonValue value, string source = "caller", long observed = 100, bool verified = false)
            => new EvaluationContext("subject", 100, new Dictionary<string, ContextAttribute> { ["x"] = new ContextAttribute(value, source, observed, verified) });

        [Fact]
        public void HashPinnedMirrorAndSharedCorpus()
        {
            using var pins = JsonDocument.Parse(File.ReadAllBytes(CorpusFile("contract-pins.json")));
            foreach (var pin in pins.RootElement.GetProperty("files").EnumerateObject())
                Assert.Equal(pin.Value.GetString(), CanonicalJson.Sha256(File.ReadAllBytes(CorpusFile(pin.Name))));
            Assert.Equal("passed:73", ConfigurationProbe.RunCorpus(File.ReadAllBytes(CorpusFile("corpus-v1.json"))));
            Assert.Equal("A:B", ConfigurationProbe.RunSmoke());
            Assert.Equal("A:B", ConfigurationProbe.RunTypedSmoke());
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("tr-TR")]
        public void InvariantCultureCorpus(string culture)
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal("passed:73", ConfigurationProbe.RunCorpus(File.ReadAllBytes(CorpusFile("corpus-v1.json"))));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Theory]
        [InlineData("true false")]
        [InlineData("{\"a\":1,\"\\u0061\":2}")]
        [InlineData("{\"a\":-0}")]
        [InlineData("{\"a\":1.0}")]
        [InlineData("{\"a\":1e1}")]
        [InlineData("{\"a\":9223372036854775808}")]
        [InlineData("{\"a\":-9007199254740992}")]
        [InlineData("{\"a\":true,}")]
        [InlineData("[true,]")]
        [InlineData("[true")]
        [InlineData("\"\\ud800\"")]
        [InlineData("\"\\udfff\"")]
        [InlineData("\"\\ud800x\"")]
        [InlineData("\"\\x00\"")]
        [InlineData("\"\\u001g\"")]
        [InlineData("\"unescaped\nnewline\"")]
        [InlineData("{\"constructor\":0}")]
        [InlineData("{\"prototype\":0}")]
        [InlineData("{\"1\":0}")]
        [InlineData("{\"é\":0}")]
        [InlineData("01")]
        [InlineData("+1")]
        [InlineData("nullx")]
        [InlineData("/*comment*/true")]
        [InlineData("\ufefftrue")]
        public void MalformedJsonRejected(string value) => Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes(value)));

        [Theory]
        [InlineData(new byte[] { 0xc0, 0x80 })]
        [InlineData(new byte[] { 0xed, 0xa0, 0x80 })]
        [InlineData(new byte[] { 0xf4, 0x90, 0x80, 0x80 })]
        [InlineData(new byte[] { 0xe2, 0x82 })]
        public void StrictUtf8(byte[] bytes) => Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(bytes));

        [Fact]
        public void ExactAndOneOverByteDepthNodeStringBoundaries()
        {
            Assert.Equal(JsonKind.Boolean, CanonicalJson.Parse(Bytes("true" + new string(' ', ConfigurationLimits.Bytes - 4))).Kind);
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes("true" + new string(' ', ConfigurationLimits.Bytes - 3))));
            string nested = new string('[', 31) + "0" + new string(']', 31);
            Assert.Equal(nested, Encoding.UTF8.GetString(CanonicalJson.Serialize(CanonicalJson.Parse(Bytes(nested)))));
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes("[" + nested + "]")));
            string nodes = "[" + string.Join(",", Enumerable.Repeat("0", 9999)) + "]";
            Assert.Equal(9999, CanonicalJson.Parse(Bytes(nodes)).AsArray().Count);
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes(nodes.Substring(0, nodes.Length - 1) + ",0]")));
            Assert.Equal(4096, CanonicalJson.Parse(Bytes("\"" + new string('a', 4096) + "\"")).AsString().Length);
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes("\"" + new string('a', 4097) + "\"")));
            Assert.Equal(2048, CanonicalJson.Parse(Bytes("\"" + new string('é', 2048) + "\"")).AsString().Length);
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes("\"" + new string('é', 2049) + "\"")));
            Assert.Equal("🙂", CanonicalJson.Parse(Bytes("\"\\ud83d\\ude42\"")).AsString());
            Assert.Throws<InvalidConfigurationException>(() => JsonValue.String("\ud800"));
            // Object keys are bounded strings, and decoded key equality is ordinal.
            CanonicalJson.Parse(Bytes("{\"" + new string('a', 4096) + "\":0}"));
            Assert.Throws<InvalidConfigurationException>(() => CanonicalJson.Parse(Bytes("{\"" + new string('a', 4097) + "\":0}")));
        }

        [Fact]
        public void ExactAndOneOverRulesIndependentlyFromNodeBudget()
        {
            var rules = Enumerable.Range(0, 1000).Select(i => new Rule("r" + i, Array.Empty<Condition>(), Outcome.Serve(Choice("A")))).ToArray();
            // A valid model hits the exact independent rule count, but its complete
            // serialized shape legitimately exceeds the stricter node budget.
            var feature = new FeatureDefinition("flag", true, Choice("default"), rules);
            var definition = new ConfigurationDefinition("test", "local", "1", new[] { feature });
            Assert.Equal(1000, definition.Features[0].Rules.Count);
            var tooMany = rules.Concat(new[] { new Rule("over", Array.Empty<Condition>(), Outcome.Serve(Choice("A"))) });
            Assert.Throws<InvalidConfigurationException>(() => new ConfigurationDefinition("test", "local", "1", new[] { new FeatureDefinition("flag", true, Choice("default"), tooMany) }));
            Assert.Throws<InvalidConfigurationException>(() => ConfigurationCompiler.Compile(definition, "test", "local"));
        }

        [Theory]
        [InlineData("integer", "ne")]
        [InlineData("integer", "eq")]
        [InlineData("integer", "gte")]
        [InlineData("bool", "ne")]
        [InlineData("version", "ne")]
        [InlineData("string", "ne")]
        public void UnknownNeverMatchesIncludingInequality(string type, string op)
        {
            var expected = type == "integer" ? JsonValue.Integer(0) : type == "bool" ? JsonValue.Boolean(false) : JsonValue.String(type == "version" ? "1.2.0" : "Android");
            var config = Config(new[] { new Condition("x", type, op, expected, "caller", 0) });
            Assert.Equal("default", Evaluator.Evaluate(config, "flag", new EvaluationContext("id", 100, new Dictionary<string, ContextAttribute>())).Reason);
            Assert.Equal("default", Evaluator.Evaluate(config, "flag", Context(JsonValue.Null)).Reason);
            Assert.Equal("default", Evaluator.Evaluate(config, "flag", Context(JsonValue.Array(Array.Empty<JsonValue>()))).Reason);
        }

        [Fact]
        public void SourcesVerificationFreshnessAndTimestampBounds()
        {
            var config = Config(new[] { new Condition("x", "integer", "gte", JsonValue.Integer(1), "backend", 10) });
            Assert.Equal("rule", Evaluator.Evaluate(config, "flag", Context(JsonValue.Integer(1), "backend", 90, true)).Reason);
            foreach (var context in new[] { Context(JsonValue.Integer(1), "backend", 89, true), Context(JsonValue.Integer(1), "caller", 100, true), Context(JsonValue.Integer(1), "backend", 100, false), Context(JsonValue.Integer(-1), "backend", 100, true), Context(JsonValue.String("1"), "backend", 100, true), Context(JsonValue.Integer(1), "backend", 101, true), Context(JsonValue.Integer(1), "backend", -1, true), Context(JsonValue.Integer(1), "backend", long.MaxValue, true) })
                Assert.Equal("default", Evaluator.Evaluate(config, "flag", context).Reason);
            Assert.Throws<InvalidConfigurationException>(() => new Condition("verifiedSpendMinor", "integer", "gte", JsonValue.Integer(0), "caller", 1));
            Assert.Throws<InvalidConfigurationException>(() => new Condition("verifiedPurchaseCount", "integer", "gte", JsonValue.Integer(0), "backend", 0));
            Assert.Throws<InvalidConfigurationException>(() => new Condition("x", "integer", "ne", JsonValue.Integer(-1), "caller", 0));
            Assert.Throws<InvalidConfigurationException>(() => JsonValue.Integer(ConfigurationLimits.SafeInteger + 1));
        }

        [Fact]
        public void CompleteValidationScopeUnknownOperatorDisabledAndFirstMatch()
        {
            string source = File.ReadAllText(CorpusFile("selector-v1.json"));
            foreach (string candidate in new[] { source.Replace("\"gte\"", "\"unknown\""), source.Replace("\"serve\"", "\"unknown\""), source.Replace("\"platform.remote-config.v1\"", "\"v2\""), source.Replace("\"maxAgeSeconds\":0", "\"maxAgeSeconds\":-1"), source.Replace("\"enabled\":true", "\"enabled\":1"), source.Replace("\"source\":\"caller\"", "\"source\":\"unknown\"") })
                Assert.Throws<InvalidConfigurationException>(() => ConfigurationReader.Read(Bytes(candidate), "synthetic", "local"));
            Assert.Throws<InvalidConfigurationException>(() => ConfigurationReader.Read(Bytes(source), "wrong", "local"));
            Assert.Throws<InvalidConfigurationException>(() => ConfigurationReader.Read(Bytes(source), "synthetic", "wrong"));
            var disabled = Config(Array.Empty<Condition>(), false);
            Assert.Equal("disabled", Evaluator.Evaluate(disabled, "flag", Context(JsonValue.Null)).Reason);
            var config = Config(Array.Empty<Condition>());
            Assert.Equal("rule", Evaluator.Evaluate(config, "flag", Context(JsonValue.Null)).Reason);
            Assert.Equal("missing_feature", Evaluator.Evaluate(config, "absent", Context(JsonValue.Null)).Reason);
            Assert.Throws<InvalidConfigurationException>(() => new Condition("x", "string", "gte", JsonValue.String("a"), "caller", 0));
            Assert.Throws<InvalidConfigurationException>(() => new FeatureDefinition("flag", true, Choice("A"), new[] { new Rule("same", Array.Empty<Condition>(), Outcome.Serve(Choice("A"))), new Rule("same", Array.Empty<Condition>(), Outcome.Serve(Choice("B"))) }));
            // Later unreachable conditions still validate before any first-match run.
            Assert.Throws<InvalidConfigurationException>(() => new Condition("x", "string", "unknown", JsonValue.String("a"), "caller", 0));
        }

        [Fact]
        public void WeightsBucketEdgesRestartUnrelatedReleaseAndZeroWeight()
        {
            var a = Choice("A");
            var b = Choice("B");
            foreach (int sum in new[] { 99, 101 })
                Assert.Throws<InvalidConfigurationException>(() => Outcome.Percentage(new ExperimentReference("flag", "1"), new[] { new Allocation(50, a), new Allocation(sum - 50, b) }));
            Assert.Throws<InvalidConfigurationException>(() => new Allocation(101, a));
            Assert.Throws<InvalidConfigurationException>(() => new Allocation(-1, a));
            Assert.Throws<InvalidConfigurationException>(() => Outcome.Percentage(new ExperimentReference("flag", "1"), Array.Empty<Allocation>()));
            var outcome = Outcome.Percentage(new ExperimentReference("flag", "1"), new[] { new Allocation(0, a), new Allocation(100, b) });
            var feature = new FeatureDefinition("flag", true, a, new[] { new Rule("percent", Array.Empty<Condition>(), outcome) });
            var first = ConfigurationCompiler.Compile(new ConfigurationDefinition("test", "local", "one", new[] { feature }), "test", "local");
            var second = ConfigurationCompiler.Compile(new ConfigurationDefinition("test", "local", "two", new[] { feature, new FeatureDefinition("unrelated", true, a, Array.Empty<Rule>()) }), "test", "local");
            foreach (int bucket in new[] { 0, 49, 50, 99 })
            {
                string id = Enumerable.Range(0, 100000).Select(i => "edge" + i).First(x => Evaluator.Bucket("flag", x) == bucket);
                var context = new EvaluationContext(id, 0, new Dictionary<string, ContextAttribute>());
                Assert.Equal("B", Evaluator.Evaluate(first, "flag", context).Variant);
                Assert.Equal(bucket, Evaluator.Evaluate(second, "flag", context).Bucket);
                var restarted = ConfigurationReader.Read(CanonicalJson.Serialize(ConfigurationCompiler.ToJson(first.Definition)), "test", "local");
                Assert.Equal(bucket, Evaluator.Evaluate(restarted, "flag", context).Bucket);
            }
        }

        [Fact]
        public void ActivationRejectsMalformedLateScopeAndPreservesPinnedRevision()
        {
            var old = Config(Array.Empty<Condition>());
            Assert.Same(old, ActivationPolicy.ReadAndActivate(old, Bytes("broken"), "test", "local", 1, 1).Active);
            Assert.False(ActivationPolicy.ReadAndActivate(old, Bytes("broken"), "test", "local", 1, 1).Accepted);
            Assert.Equal("stale_generation", ActivationPolicy.Activate(old, old, 1, 2).Reason);
            Assert.Same(old, ActivationPolicy.Activate(old, null, 2, 2).Active);
            Assert.True(ActivationPolicy.Activate(old, old, 2, 2).Accepted);
            var otherScope = ConfigurationCompiler.Compile(new ConfigurationDefinition("other", "local", "1", Array.Empty<FeatureDefinition>()), "other", "local");
            Assert.Equal("scope_mismatch", ActivationPolicy.Activate(old, otherScope, 2, 2).Reason);
            var original = new FeatureRevisionReference("flag", "one", Digest);
            var next = new FeatureRevisionReference("flag", "two", Digest);
            Assert.Same(original, ActivationPolicy.ForInstance(new InstanceReference("instance", original), next));
            Assert.Same(next, ActivationPolicy.ForInstance(null, next));
            Assert.Throws<InvalidConfigurationException>(() => ActivationPolicy.ForInstance(new InstanceReference("instance", original), new FeatureRevisionReference("other", "two", Digest)));
        }

        [Fact]
        public void CompleteDependencyEvidenceIsInjectedAndPinsExactRevisionAndHash()
        {
            var old = Config(Array.Empty<Condition>());
            var one = new FeatureRevisionReference("shared", "one", Digest);
            var two = new FeatureRevisionReference("shared", "two", Digest);
            var choice = new Choice("new", Digest, "new", JsonValue.Boolean(true), new[] { one });
            var other = new Choice("other", Digest, "other", JsonValue.Boolean(false), new[] { two });
            var candidate = ConfigurationCompiler.Compile(new ConfigurationDefinition("test", "local", "new", new[] { new FeatureDefinition("flag", true, choice, new[] { new Rule("other", Array.Empty<Condition>(), Outcome.Serve(other)) }) }), "test", "local");
            Assert.Same(old, ActivationPolicy.Activate(old, candidate, 1, 1).Active);
            Assert.Equal("dependency_missing", ActivationPolicy.Activate(old, candidate, 1, 1, new[] { one }).Reason);
            Assert.Equal("dependency_missing", ActivationPolicy.Activate(old, candidate, 1, 1, new[] { one, new FeatureRevisionReference("shared", "two", new string('b', 64)) }).Reason);
            Assert.True(ActivationPolicy.Activate(old, candidate, 1, 1, new[] { one, two }).Accepted);
            byte[] serialized = CanonicalJson.Serialize(ConfigurationCompiler.ToJson(candidate.Definition));
            Assert.Same(old, ActivationPolicy.ReadAndActivate(old, serialized, "test", "local", 1, 1).Active);
            Assert.True(ActivationPolicy.ReadAndActivate(old, serialized, "test", "local", 1, 1, new[] { one, two }).Accepted);
        }

        [Fact]
        public void ImmutableModelsAndIndependentMatureParserRandomizedOracle()
        {
            var conditions = new List<Condition>();
            var rule = new Rule("rule", conditions, Outcome.Serve(Choice("A")));
            conditions.Add(new Condition("x", "bool", "eq", JsonValue.Boolean(true), "caller", 0));
            Assert.Empty(rule.Conditions);
            var attributes = new Dictionary<string, ContextAttribute>();
            var context = new EvaluationContext("subject", 0, attributes);
            attributes.Add("x", new ContextAttribute(JsonValue.Boolean(true), "caller", 0, false));
            Assert.Empty(context.Attributes);
            var random = new Random(20261013);
            for (int i = 0; i < 500; i++)
            {
                long number = random.NextInt64(-ConfigurationLimits.SafeInteger, ConfigurationLimits.SafeInteger + 1);
                string text = "<>&é🙂/\\\"\n\t" + i;
                byte[] nativeBytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> { ["z"] = text, ["a"] = number, ["b"] = i % 2 == 0 });
                var ours = CanonicalJson.Parse(nativeBytes);
                using var native = JsonDocument.Parse(CanonicalJson.Serialize(ours));
                Assert.Equal(number, native.RootElement.GetProperty("a").GetInt64());
                Assert.Equal(text, native.RootElement.GetProperty("z").GetString());
                Assert.Equal(i % 2 == 0, native.RootElement.GetProperty("b").GetBoolean());
                Assert.Equal(ours["a"].AsInteger(), JsonDocument.Parse(nativeBytes).RootElement.GetProperty("a").GetInt64());
            }
        }

        [Fact]
        public void VersionExactLengthAndNumericBounds()
        {
            Assert.True(StrictVersion.TryParse("1.2.3+" + new string('a', 250), out _));
            Assert.False(StrictVersion.TryParse("1.2.3+" + new string('a', 251), out _));
            Assert.True(StrictVersion.TryParse("2147483647.2147483647.2147483647-2147483647", out _));
            Assert.False(StrictVersion.TryParse("1.2.3-2147483648", out _));
            Assert.False(StrictVersion.TryParse("1.2.3-01", out _));
            Assert.Equal(0, StrictVersion.Parse("1.2.3+01").CompareTo(StrictVersion.Parse("1.2.3+other")));
        }
    }
}
