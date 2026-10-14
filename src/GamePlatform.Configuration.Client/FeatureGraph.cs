using System;
using System.Collections.Generic;

namespace GamePlatform.Configuration.Client
{
    internal static class FeatureGraph
    {
        internal static void VerifyFile(byte[] bytes, ImmutableFileReference reference, bool semanticWhole)
        {
            if (bytes.Length != reference.DecodedBytes || CanonicalJson.Sha256(bytes) != reference.RepresentationSha)
                throw new InvalidConfigurationException("integrity");
            if (semanticWhole && CanonicalJson.SemanticSha256(CanonicalJson.Parse(bytes)) != reference.SemanticSha)
                throw new InvalidConfigurationException("integrity");
        }
        private static Dictionary<string, AssetDescriptor> Assets(ReleaseManifest manifest)
        {
            var map = new Dictionary<string, AssetDescriptor>(StringComparer.Ordinal);
            foreach (var item in manifest.Assets)
                map.Add(Wire.Identity(item.Reference), item);
            return map;
        }
        internal static void ValidateMetadata(ReleasePointer pointer, ReleaseManifest manifest, CompiledConfiguration selector)
        {
            if (pointer.Release != manifest.Release)
                throw new InvalidConfigurationException("release_mismatch");
            ValidateMetadata(manifest, selector);
        }
        internal static void ValidateMetadata(ReleaseManifest manifest, CompiledConfiguration selector)
        {
            if (manifest.Release != selector.Definition.Release)
                throw new InvalidConfigurationException("release_mismatch");
            var assets = Assets(manifest);
            foreach (var feature in selector.Definition.Features)
            {
                var choices = new List<Choice> { feature.Default };
                foreach (var rule in feature.Rules)
                {
                    if (rule.Outcome.Choice != null)
                        choices.Add(rule.Outcome.Choice);
                    else
                        foreach (var allocation in rule.Outcome.Allocations)
                            choices.Add(allocation.Choice);
                }
                foreach (var choice in choices)
                {
                    var reference = new FeatureRevisionReference(feature.Key, choice.Revision, choice.SemanticSha);
                    if (!assets.TryGetValue(Wire.Identity(reference), out var asset) || !Wire.Same(asset.Reference, reference) || CanonicalJson.SemanticSha256(Wire.References(choice.Dependencies)) != CanonicalJson.SemanticSha256(Wire.References(asset.Dependencies)))
                        throw new InvalidConfigurationException("choice_graph_mismatch");
                }
            }
        }
        internal static IReadOnlyList<AssetDescriptor> Chosen(ReleaseManifest manifest, string featureKey, Choice choice)
        {
            var assets = Assets(manifest);
            var result = new List<AssetDescriptor>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int total = 0;
            void Walk(FeatureRevisionReference reference)
            {
                string id = Wire.Identity(reference);
                if (!assets.TryGetValue(id, out var descriptor) || !Wire.Same(reference, descriptor.Reference))
                    throw new InvalidConfigurationException("graph_missing");
                if (!seen.Add(id))
                    return;
                result.Add(descriptor);
                total = checked(total + descriptor.File.DecodedBytes);
                if (result.Count > ConfigurationClientPolicy.GraphFiles || total > ConfigurationClientPolicy.GraphBytes)
                    throw new InvalidConfigurationException("graph_bounds");
                foreach (var dependency in descriptor.Dependencies)
                    Walk(dependency);
            }
            Walk(new FeatureRevisionReference(featureKey, choice.Revision, choice.SemanticSha));
            return result.AsReadOnly();
        }
        internal static JsonValue Asset(byte[] bytes, ConfigurationScope scope, AssetDescriptor descriptor)
        {
            VerifyFile(bytes, descriptor.File, false);
            var value = CanonicalJson.Parse(bytes);
            Wire.Shape(value, "schema", "app", "environment", "key", "revision", "semanticSha", "payload", "dependencies");
            Wire.Scope(value, scope, "platform.remote-config.asset.v1");
            if (Wire.String(value["key"]) != descriptor.Reference.Key || Wire.String(value["revision"]) != descriptor.Reference.Revision || Wire.Digest(value["semanticSha"]) != descriptor.Reference.SemanticSha || CanonicalJson.SemanticSha256(value["payload"]) != descriptor.Reference.SemanticSha || CanonicalJson.SemanticSha256(Wire.References(Wire.References(value["dependencies"], true))) != CanonicalJson.SemanticSha256(Wire.References(descriptor.Dependencies)))
                throw new InvalidConfigurationException("asset_graph_mismatch");
            return value["payload"];
        }
        internal static JsonValue Activation(FeatureContext context) => Wire.Object(("selection", ProtocolReader.SelectionJson(context.FeatureKey, context.Selection)), ("origin", context.Origin.ToJson()), ("closure", Wire.References(context.Closure)), ("durable", JsonValue.Boolean(true)));
        internal static void ValidateSelection(CompiledConfiguration selector, string featureKey, FeatureSelection selection, string subject)
        {
            FeatureDefinition? feature = null;
            foreach (var item in selector.Definition.Features)
                if (item.Key == featureKey)
                    feature = item;
            if (feature == null)
                throw new InvalidConfigurationException("missing_feature");
            Choice? expected = null;
            if ((!feature.Enabled && selection.Reason != "disabled") || (feature.Enabled && selection.Reason == "disabled"))
                throw new InvalidConfigurationException("selection_invalid");
            if (selection.Reason == "default" || selection.Reason == "disabled")
            {
                if (selection.Reason == "disabled" && feature.Enabled)
                    throw new InvalidConfigurationException("selection_invalid");
                expected = feature.Default;
            }
            else
                foreach (var rule in feature.Rules)
                    if (rule.Id == selection.RuleId)
                    {
                        if (selection.Reason == "rule" && rule.Outcome.Choice != null)
                            expected = rule.Outcome.Choice;
                        if (selection.Reason == "percentage" && rule.Outcome.Experiment != null && selection.Experiment != null && rule.Outcome.Experiment.Key == selection.Experiment.Key && rule.Outcome.Experiment.Version == selection.Experiment.Version)
                        {
                            int bucket = Evaluator.Bucket(selection.Experiment.Key, subject);
                            if (bucket != selection.Bucket)
                                throw new InvalidConfigurationException("selection_bucket");
                            int upper = 0;
                            foreach (var allocation in rule.Outcome.Allocations)
                            {
                                upper += allocation.Weight;
                                if (bucket < upper)
                                {
                                    expected = allocation.Choice;
                                    break;
                                }
                            }
                        }
                    }
            if (expected == null || expected.Revision != selection.Choice.Revision || expected.SemanticSha != selection.Choice.SemanticSha || expected.Variant != selection.Choice.Variant || CanonicalJson.SemanticSha256(expected.Value) != CanonicalJson.SemanticSha256(selection.Choice.Value) || CanonicalJson.SemanticSha256(Wire.References(expected.Dependencies)) != CanonicalJson.SemanticSha256(Wire.References(selection.Choice.Dependencies)))
                throw new InvalidConfigurationException("selection_invalid");
        }
    }
}
