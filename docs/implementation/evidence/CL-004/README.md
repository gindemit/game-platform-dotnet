# CL-004 desktop wire candidate checkpoint

Date: 2026-09-20

Source commit: `83caf64596869fc7a1dc35ac6440e21ac97a7d68`

Qualification correction: `2e46b6f90ed60492d77114c655fb9788d4112e25`

`DesktopQualificationWireCodec` is an internal desktop-only candidate over the
existing generated DTO/union mappings and bounded MessagePack reader/writer. It
is visible only to the test assembly, does not implement `IWireCodec`, and
production registration remains unavailable.
It accepts only the reviewed core catalog. There is no typeless resolver,
reflection/runtime-code-generation path, serializer annotation, compression or
JSON fallback.

Desktop tests prove the approved C# producer bytes for
provision request/response, generated interface-union dispatch, named maps, RFC-order
UUID bin16, exact signed-64 extrema, opaque-token bin, absent versus explicit null,
unknown-field and arbitrary-type rejection, duplicate-key rejection, and body/depth
bounds. Provider-fit tests wrap the candidate in an explicit test-local
`IWireCodec` adapter and use a scripted executor. No such adapter or registration
exists in production source.

## Verification

```text
locked restore: 18 projects, exit 0
Release build --no-restore: 18 projects, 0 warnings/errors, exit 0
focused Serialization tests: 171 passed, exit 0
focused CL-005/CL-010/desktop-candidate tests: 46 passed, exit 0
desktop-candidate tests: 7 passed, exit 0
full .NET suite: 335 passed, 0 failed/skipped, exit 0
codec harness self-test: 73 schema, 32 fingerprint, 5 hostile JSON, exit 0
schema and DTO generator --check: exit 0
architecture/dependency validation: exit 0
script tests: 33 passed, exit 0
manifest validation: 52 tasks, 16 feature ledgers, 21 waves, exit 0
manifest tests: 14 passed, exit 0
package pipeline: 14 managed assemblies, symbols/dependency closure/licenses, nothing published, exit 0
git diff --check: exit 0
```

The dependency inventory remains exactly MessagePack 3.1.8 plus the reviewed
lockfile transitive closure. The current early package intentionally still excludes
the codec and its dependencies pending CL-015.

## Remaining limits

CL-004 remains `in_progress`. No Unity import, IL2CPP/AOT/stripping, device run,
complete transitive/native bundle, concrete HTTP executor/host, live backend
transcript or independent review was performed. The earlier G2 bidirectional core
exchange remains the peer evidence; this checkpoint does not rerun or broaden it.
Production `IWireCodec` remains a blocker for CL-005/CL-010 until required AOT
acceptance. No G3 assessment or P4 work occurred.

## Production adapter promotion — 2026-09-20

Source `684ed49c832f203326270546414244bc2adb78e8` promotes the exact bounded
implementation through public `MessagePackWireCodec : IWireCodec`. The adapter
delegates directly to the generated DTO mapping and low-level bounded codec; it
adds no alternate serializer, JSON fallback, reflection, typeless resolver,
runtime code generation, compression or game semantics.

The required AOT evidence is Unity INT-009 commit
`ff65dbed550b390c4129f471bf098559be37f5ea`, consumer source
`c2335be613fed95a1997f2718679e2f309eba7df`. On an authorized physical Android
API 31 ARM64 device, Unity 6000.5.3f1 built IL2CPP with high stripping and ran the
complete pinned approved G2 corpus on initial launch and after force-stop/relaunch.
Each run passed 34 accepted/39 rejected diagnostics, 34 TypeScript-consumed and
34 C#-produced exchange cases, 24 accepted/8 rejected fingerprints and 14
accepted/18 rejected raw probes with identical provenance hashes.

Production-interface tests pass 9/9 and the full SDK passes 351/351. Locked
restore/build pass for 18 projects with zero warnings/errors; validation, 48
script tests, 14 manifest tests and both generator checks pass. A fresh ordinary
package and verify at the promotion source pass with 23 managed assemblies,
three native targets and manifest SHA-256
`34febc70a5cc0b1b01f05075b3c26c75e131acc055511c038b033cd40e726893`.

CL-004 remains `in_progress` pending independent completion review. The codec
port is production-available, but no concrete HTTP executor or host composition,
HTTPS/live backend transcript, G3 or P4 is claimed. CL-015 remains `in_progress`
for independent reproducibility of the promoted bundle and macOS native execution.
