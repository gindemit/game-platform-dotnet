# Optional remote configuration core

The optional first-party `GamePlatform.Configuration` assembly implements immutable
selectors/refs, bounded restricted JSON, canonical semantic bytes/SHA256, strict
ASCII SemVer, local ordered AND evaluation, stable percentage allocation and pure
snapshot activation. Runtime target is netstandard2.1/C#9; zero runtime NuGet and
zero project dependencies. Reviewed BCL UTF8Encoding/SHA1/SHA256 are the only
encoding/crypto providers. No vendor code is copied/adapted; reuse inventory is
empty. ConfigCat allocation/version research is behavior evidence only.

```csharp
var compiled = ConfigurationReader.Read(jsonUtf8, "synthetic", "local");
var result = Evaluator.Evaluate(compiled, "progression",
    new EvaluationContext(scopedSubjectId, nowSeconds, suppliedAttributes));
// result.Choice.Value / Revision / SemanticSha, RuleId, Variant, Bucket, Reason
```

Selectors include required explicit source/freshness fields. Integer predicates
and context counters are nonnegative safe integers; generic JSON/choice values may
be signed. Protected verifiedSpendMinor/verifiedPurchaseCount require backend
source, verified evidence and a positive freshness bound. Context null/malformed/
wrong-type/stale/unverified data is unknown, including `ne`. Missing feature yields
no choice. Empty AND matches; disabled returns default. Rule IDs are unique per
feature. Unknown operators in any rule reject the complete candidate.

The fixed limits combine conservatively: a typed definition with exactly1000 rules
passes the independent rule-count check, but its complete verbose JSON shape can
exceed10000 nodes and is then rejected at compile. The rule ceiling is not a promise
that every1000-rule selector fits the aggregate structural caps. Nodes count values,
not keys; depth root=1. Read once, evaluate repeatedly. Full JSON and typed compile
both validate aggregate canonical bounds and calculate semantic digests.

The algorithm seed is stable experimentKey, never release/path/digest. Allocation
weights sum exactly100, zero weights permitted. ExperimentVersion is explicit
identity; publication must enforce changes against history. The core has no hidden
history/enrollment store. Immutable refs do not assert backend enrollment or grant.

Activation accepts only compiled selectors, rejects late caller generations, and
preserves last-known-good on malformed/scope/dependency rejection. Direct selector
dependency refs require explicit host-provided verified refs matching key+revision+
semanticSha; multiple revisions of a shared key can coexist. Reading a selector
does not create this evidence. The outer host/compiler must verify actual asset
bytes and the transitive graph. Selective feature activation can verify a selected
choice/graph in the host instead of loading unused A payloads; snapshot helper
checking the complete selector intentionally requires all its direct refs.
Pinned instances preserve original refs; a new attempt receives the next choice.

No network, clocks, pollers, cache, account store, exposure emission, telemetry,
dynamic code or provider IO is part of evaluation. Read/activation do not fetch.
Dependency/persistence/publishing/enrollment and game composition remain unbuilt;
the existing RemoteConfig feature facade is still unavailable. Platform/runtime
acceptance and isolated size results live in the workspace RC-TARGETS receipt.

Standalone validation from a disposable source copy:

```text
rtk dotnet test tests/GamePlatform.Configuration.Tests/GamePlatform.Configuration.Tests.csproj -c Release
rtk proxy python scripts/validate.py
rtk proxy python -m unittest discover -s scripts -p test_configuration_staging.py -v
```

Tests pin SDK mirror hashes and run all73 machine cases, the original22 guarded
research cases, exact/one-over byte/depth/node/string/rule/version boundaries,
malformed UTF-8/JSON, unknown/negative/verified/fresh context, weight edges,
immutable models, generation/pin preservation, dependency rejection and500 random
restricted JSON structures against independent framework System.Text.Json.
System.Text.Json and xUnit/test SDK remain test-only; no test assembly ships.
