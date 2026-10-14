using System;
using System.Text;
using System.Linq;
using Xunit;

namespace GamePlatform.Configuration.Client.Tests
{
    public sealed class ProtocolTests
    {
        [Theory]
        [InlineData("0", 0L)]
        [InlineData("9007199254740992", 9007199254740992L)]
        [InlineData("9223372036854775807", long.MaxValue)]
        public void Decimal64_remains_exact(string value, long expected) => Assert.Equal(expected, ProtocolReader.Sequence(JsonValue.String(value)));
        [Theory]
        [InlineData("01")]
        [InlineData("-1")]
        [InlineData("1e3")]
        [InlineData("1.0")]
        [InlineData(" 1")]
        [InlineData("")]
        [InlineData("9223372036854775808")]
        public void Invalid_decimal64_rejects(string value) => Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Sequence(JsonValue.String(value)));
        [Fact]
        public void Real_contract_fixture_metadata_and_controls_parse_without_core_changes()
        {
            var fixture = new Fixture();
            var pointer = ProtocolReader.Pointer(fixture.Documents["current.json"], fixture.Scope);
            var manifest = ProtocolReader.Manifest(fixture.Documents[pointer.Manifest.Path], fixture.Scope);
            Assert.Equal("release-7", manifest.Release);
            Assert.Equal(3, manifest.Assets.Count);
            Assert.Equal(11, ProtocolReader.Control(fixture.Documents["control.json"], fixture.Scope).Sequence);
            Assert.NotEqual(CanonicalJson.Sha256(CanonicalJson.Serialize(pointer.Document)), CanonicalJson.Sha256(fixture.Documents["current.json"]));
        }
        [Fact]
        public void Unknown_field_scope_and_control_cap_reject()
        {
            var fixture = new Fixture();
            var pointer = fixture.Example("current");
            var fields = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, JsonValue>>(pointer.AsObject()) { new System.Collections.Generic.KeyValuePair<string, JsonValue>("extra", JsonValue.Boolean(true)) };
            Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Pointer(CanonicalJson.Serialize(JsonValue.Object(fields)), fixture.Scope));
            Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Pointer(fixture.Documents["current.json"], new ConfigurationScope("wrong", "local")));
            Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Control(new byte[16385], fixture.Scope));
            Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Control(Encoding.UTF8.GetBytes("{\"schema\":\"platform.remote-config.control.v1\",\"app\":\"synthetic\",\"environment\":\"local\",\"sequence\":\"1\",\"issuedAtSeconds\":1,\"restrictions\":[{\"key\":\"analytics.send\",\"effect\":\"cap\",\"ratePermille\":100}]}"), fixture.Scope));
        }
        [Fact]
        public void Exact_origins_and_hashed_paths_are_enforced()
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationClientPolicy(new Uri("http://example.com/"), new Uri("https://control.example/")));
            Assert.Throws<ArgumentException>(() => new ConfigurationClientPolicy(new Uri("https://user:password@example.com/"), new Uri("https://control.example/")));
            var policy = new ConfigurationClientPolicy(new Uri("https://example.com/app/"), new Uri("https://control.example/app/"));
            Assert.Throws<InvalidConfigurationException>(() => policy.Resolve("../escape"));
            Assert.Throws<InvalidConfigurationException>(() => policy.Resolve("https://other.example/"));
            Assert.Equal("https://example.com/app/assets/" + new string('a', 64) + ".json", policy.Resolve("assets/" + new string('a', 64) + ".json").AbsoluteUri);
        }
        [Fact]
        public void Direct_dependencies_over_64_reject()
        {
            var fixture = new Fixture();
            var manifest = fixture.Example("manifest");
            var asset = manifest["assets"].AsArray()[0];
            var references = Enumerable.Range(0, 65).Select(i => Fixture.Object(("key", JsonValue.String("dependency" + i.ToString("D2"))), ("revision", JsonValue.String("v1")), ("semanticSha", JsonValue.String(new string('a', 64))))).ToArray();
            var changed = Fixture.Set(asset, "dependencies", JsonValue.Array(references));
            var assets = manifest["assets"].AsArray().Select((v, i) => i == 0 ? changed : v);
            var invalid = Fixture.Set(manifest, "assets", JsonValue.Array(assets));
            Assert.Throws<InvalidConfigurationException>(() => ProtocolReader.Manifest(CanonicalJson.Serialize(invalid), fixture.Scope));

        }
    }
}
